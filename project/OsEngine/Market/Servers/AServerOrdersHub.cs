/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using LiteDB;
using OsEngine.Entity;
using OsEngine.Logging;
using System.Linq;

namespace OsEngine.Market.Servers
{
    public class AServerOrdersHub
    {
        #region Constructor, Settings

        public AServerOrdersHub(AServer server)
        {
            _server = server;

            IServerPermission permission = ServerMaster.GetServerPermission(server.ServerType);

            if (permission == null)
            {
                return;
            }

            if (permission.CanQueryOrdersAfterReconnect == false
                && permission.CanQueryOrderStatus == false)
            {
                return;
            }

            _canQueryOrdersAfterReconnect = permission.CanQueryOrdersAfterReconnect;
            _canQueryOrderStatus = permission.CanQueryOrderStatus;
            _secondsToWaitRequest = permission.WaitTimeSecondsAfterFirstStartToSendOrders;

            if(_secondsToWaitRequest < 15)
            {
                _secondsToWaitRequest = 15;
            }

            Thread worker = new Thread(ThreadWorkerArea);
            worker.Name = "AServerOrdersHubThreadWorker";
            worker.Start();

        }

        AServer _server;

        bool _canQueryOrdersAfterReconnect;

        bool _canQueryOrderStatus;

        bool _fullLogIsOn = false;

        #endregion

        #region Set orders

        public void SetOrderFromOsEngine(Order order)
        {
            if (_canQueryOrderStatus == false)
            {
                return;
            }

            _ordersFromOsEngineQueue.Enqueue(order);

            if (_fullLogIsOn)
            {
                SendLogMessage("New order in OsEngine. NumUser: " + order.NumberUser
                     + " State: " + order.State
                    , LogMessageType.System);
            }
        }

        public void SetOrderFromApi(Order order)
        {
            if (_canQueryOrderStatus == false)
            {
                return;
            }

            _orderFromApiQueue.Enqueue(order);

            if (_fullLogIsOn)
            {
                SendLogMessage("New order in Api. NumUser: " + order.NumberUser
                    + " NumMarket: " + order.NumberMarket
                    + " State: " + order.State
                    , LogMessageType.System);
            }
        }

        ConcurrentQueue<Order> _ordersFromOsEngineQueue = new ConcurrentQueue<Order>();

        ConcurrentQueue<Order> _orderFromApiQueue = new ConcurrentQueue<Order>();

        #endregion

        #region Main Thread

        private void ThreadWorkerArea()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(1000);

                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    // 1 проверяем не надо ли запросить список активных ордеров после переподключения
                    
                    if(_canQueryOrdersAfterReconnect)
                    {
                        CheckReconnectStatus();
                    }

                    if (_server.ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        continue;
                    }

                    // 2 загружаем ордера внутрь из очередей и из баз. Сохраняем

                    if (_canQueryOrderStatus)
                    {
                        ManageOrders();
                    }
                   
                    // 3 проверка статусов ордеров

                    if(_canQueryOrderStatus)
                    {
                        CheckOrdersStatus();
                    }

                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        #endregion

        #region Query orders after reconnect

        private void CheckReconnectStatus()
        {
            if (_server.ServerStatus == ServerConnectStatus.Disconnect)
            {
                _lastDisconnectTime = DateTime.Now;
                _checkOrdersAfterLastConnect = false;
                return;
            }

            if (_checkOrdersAfterLastConnect == true)
            {
                return;
            }

            if (_lastDisconnectTime.AddSeconds(_secondsToWaitRequest) < DateTime.Now)
            {
                _checkOrdersAfterLastConnect = true;

                if (GetAllActivOrdersOnReconnectEvent != null)
                {
                    GetAllActivOrdersOnReconnectEvent();

                    if (_fullLogIsOn)
                    {
                        SendLogMessage("Event: GetAllActivOrdersOnReconnectEvent", LogMessageType.System);
                    }
                }
            }
        }

        private DateTime _lastDisconnectTime;

        private int _secondsToWaitRequest;

        private bool _checkOrdersAfterLastConnect = false;

        public event Action GetAllActivOrdersOnReconnectEvent;

        #endregion

        #region Orders Hub

        private List<OrderToWatch> _ordersActiv = new List<OrderToWatch>();

        bool _ordersIsLoaded = false;

        private void ManageOrders()
        {
            if (_ordersIsLoaded == false)
            {
                _ordersIsLoaded = true;
                LoadOrdersFromFile();
            }

            if (_orderFromApiQueue.IsEmpty == false
                || _ordersFromOsEngineQueue.IsEmpty == false)
            {
                GetOrdersFromQueue();
            }

            TryRemoveOrders();
        }

        private void TryRemoveOrders()
        {
            // 1 удаляем все ордера старше 24 часов

            bool orderIsDelete = false;

            for (int i = 0; i < _ordersActiv.Count; i++)
            {
                Order order = _ordersActiv[i].Order;

                if(order.TimeCreate != DateTime.MinValue 
                    && order.TimeCreate.AddDays(1) < DateTime.Now)
                {
                    SendLogMessage("Order remove BY TIME 1. NumUser: " + order.NumberUser
                     + " NumMarket: " + order.NumberMarket
                     + " Status: " + order.State
                     + " TimeCreate: " + order.TimeCreate
                     , LogMessageType.System);

                    _ordersActiv.RemoveAt(i);
                    i--;
                    orderIsDelete = true;
                }

                if (order.TimeCallBack != DateTime.MinValue
                    && order.TimeCallBack.AddDays(1) < DateTime.Now)
                {
                    SendLogMessage("Order remove BY TIME 2. NumUser: " + order.NumberUser
                    + " NumMarket: " + order.NumberMarket
                    + " Status: " + order.State
                    + " TimeCallBack: " + order.TimeCallBack
                    , LogMessageType.System);

                    _ordersActiv.RemoveAt(i);
                    i--;
                    orderIsDelete = true;
                }
            }

            // 2 удаляем окончательно потерянные ордера о которых на верх уже выслали сообщение

            for (int i = 0; i < _ordersActiv.Count; i++)
            {
                OrderToWatch order = _ordersActiv[i];

                if (order.IsFinallyLost)
                {
                    SendLogMessage("Order remove BY FINALLY LOST. NumUser: " + order.Order.NumberUser
                     + " NumMarket: " + order.Order.NumberMarket
                     + " Status: " + order.Order.State
                     , LogMessageType.System);

                    _ordersActiv.RemoveAt(i);
                    i--;
                    orderIsDelete = true;
                }
            }

            if(orderIsDelete)
            {
                SaveOrdersInFile();
            }
        }

        private void GetOrdersFromQueue()
        {
            // 1 перегружаем ордера из очередей в соответствующие массивы

            while (_ordersFromOsEngineQueue.IsEmpty == false)
            {
                Order newOpenOrder = null;

                if(_ordersFromOsEngineQueue.TryDequeue(out newOpenOrder))
                {
                    OrderToWatch orderToWatch = new OrderToWatch();
                    orderToWatch.Order = newOpenOrder;

                    _ordersActiv.Add(orderToWatch);
                }
            }

            while (_orderFromApiQueue.IsEmpty == false)
            {
                Order newOrder = null;

                if (_orderFromApiQueue.TryDequeue(out newOrder))
                {
                   // 2 перегружаем ордера которые пришли из АПИ в хранилище ордеров которые сгенерировал OsEngine
                    TrySetOrderInHub(newOrder);
                }
            }

            // 3 сохраняем

            SaveOrdersInFile();
        }

        private void TrySetOrderInHub(Order orderFromApi)
        {
            // удаляем всё что исполнилось или отменено или ошибочно

            bool isInArray = false;

            for (int i = 0;i < _ordersActiv.Count;i++)
            {
                Order curOrderFromOsEngine = _ordersActiv[i].Order;

                if(orderFromApi.NumberUser != curOrderFromOsEngine.NumberUser)
                {
                    continue;
                }

                if(orderFromApi.State == OrderStateType.Activ
                    || orderFromApi.State == OrderStateType.Patrial
                    || orderFromApi.State == OrderStateType.Pending)
                {
                    
                    _ordersActiv[i].Order = orderFromApi;
                    _ordersActiv[i].CountEventsFromApi++;

                    if (_fullLogIsOn)
                    {
                        SendLogMessage("New order alive status. NumUser: " + orderFromApi.NumberUser
                           + " NumMarket: " + orderFromApi.NumberMarket
                           + " Status: " + orderFromApi.State, LogMessageType.System);
                    }

                    break;
                }
                else if(orderFromApi.State == OrderStateType.Cancel 
                    || orderFromApi.State == OrderStateType.Fail
                    || orderFromApi.State == OrderStateType.Done
                    || orderFromApi.State == OrderStateType.LostAfterActive)
                {
                    _ordersActiv.RemoveAt(i);

                    if (_fullLogIsOn)
                    {
                        SendLogMessage("New order dead status. NumUser: " + orderFromApi.NumberUser
                             + " NumMarket: " + orderFromApi.NumberMarket
                             + " Status: " + orderFromApi.State, LogMessageType.System);
                    }

                    break;
                }
                else
                {
                    SendLogMessage(
                        "Error status. State: " + orderFromApi.State 
                        + " NumUser: " + orderFromApi.NumberUser
                         + " NumMarket: " + orderFromApi.NumberMarket
                         + " Connection: " + orderFromApi.ServerType
                        , LogMessageType.Error);
                }
            }
        }

        private void LoadOrdersFromFile()
        {
            try
            {
                string dir = Directory.GetCurrentDirectory();
                dir += "\\Engine\\DataBases\\";

                if (Directory.Exists(dir) == false)
                {
                    Directory.CreateDirectory(dir);
                }

                dir += _server.ServerType + "_active_orders.db";

                using (LiteDatabase db = new LiteDatabase(dir))
                {
                    var collection = db.GetCollection<OrderToSave>("orders");

                    List<OrderToSave> col = collection.FindAll().ToList();

                    for (int i = 0; i < col.Count; i++)
                    {
                        OrderToSave curOrdInBd = col[i];

                        string orderInString = curOrdInBd.SaveString;

                        if (string.IsNullOrEmpty(orderInString) == false)
                        {
                            Order newOrder = new Order();
                            newOrder.SetOrderFromString(orderInString);

                            if (newOrder.State == OrderStateType.Fail
                                || newOrder.State == OrderStateType.Cancel
                                || newOrder.State == OrderStateType.Done)
                            {
                                if (_fullLogIsOn)
                                {
                                    SendLogMessage("Bad State order LOAD. Ignore. NumUser: " + newOrder.NumberUser
                                        + " NumMarket: " + newOrder.NumberMarket
                                        + " Status: " + newOrder.State, LogMessageType.System);
                                }
                                continue;
                            }
                            OrderToWatch orderToWatch = new OrderToWatch();
                            orderToWatch.Order = newOrder;

                            _ordersActiv.Add(orderToWatch);

                            if (_fullLogIsOn)
                            {
                                SendLogMessage("New alive order LOAD. NumUser: " + newOrder.NumberUser
                                    + " NumMarket: " + newOrder.NumberMarket
                                    + " Status: " + newOrder.State, LogMessageType.System);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        private void SaveOrdersInFile()
        {
            try
            {
                string dir = Directory.GetCurrentDirectory();
                dir += "\\Engine\\DataBases\\";

                if (Directory.Exists(dir) == false)
                {
                    Directory.CreateDirectory(dir);
                }

                dir += _server.ServerType + "_active_orders.db";

                using (LiteDatabase db = new LiteDatabase(dir))
                {
                    var collection = db.GetCollection<OrderToSave>("orders");

                    List<OrderToSave> col = collection.FindAll().ToList();

                    // 1 вставляем в базу ордера которые сейчас есть в массиве активных ордеров

                    for (int i = 0; i < _ordersActiv.Count; i++)
                    {
                        OrderToSave orderToSave = new OrderToSave();
                        orderToSave.NumberId = i;
                        orderToSave.NumberMarket = _ordersActiv[i].Order.NumberMarket;
                        orderToSave.NumberUser = _ordersActiv[i].Order.NumberUser;
                        orderToSave.SaveString = _ordersActiv[i].Order.GetStringForSave().ToString();

                        bool isInArray = false;

                        for (int j = 0; j < col.Count; j++)
                        {
                            OrderToSave curOrd = col[j];

                            if (curOrd.NumberUser != 0 &&
                                orderToSave.NumberUser != 0
                                && curOrd.NumberUser == orderToSave.NumberUser)
                            {
                                col[j] = orderToSave;
                                isInArray = true;
                                break;
                            }

                            if (string.IsNullOrEmpty(curOrd.NumberMarket) == false
                                && string.IsNullOrEmpty(orderToSave.NumberMarket) == false
                                && curOrd.NumberMarket == orderToSave.NumberMarket)
                            {
                                col[j] = orderToSave;
                                isInArray = true;
                                break;
                            }
                        }

                        if (isInArray == false)
                        {
                            col.Add(orderToSave);
                        }
                    }

                    // 2 удаляем лишние ордера из базы

                    for (int i = 0; i < col.Count; i++)
                    {
                        OrderToSave curOrdInBd = col[i];

                        bool isInArray = false;

                        for (int j = 0; j < _ordersActiv.Count; j++)
                        {
                            OrderToWatch order = _ordersActiv[j];

                            if (order.Order.NumberUser != 0 &&
                                curOrdInBd.NumberUser != 0 &&
                                order.Order.NumberUser == curOrdInBd.NumberUser)
                            {
                                isInArray = true;
                                break;
                            }
                            if (string.IsNullOrEmpty(order.Order.NumberMarket) == false &&
                                string.IsNullOrEmpty(curOrdInBd.NumberMarket) == false &&
                                order.Order.NumberMarket == curOrdInBd.NumberMarket)
                            {
                                isInArray = true;
                                break;
                            }
                        }

                        if (isInArray == false)
                        {
                            col.RemoveAt(i);
                            i--;
                        }
                    }

                    collection.DeleteAll();

                    for (int i = 0; i < col.Count; i++)
                    {
                        collection.Insert(i, col[i]);
                    }

                    if (col.Count > 0)
                    {
                        collection.EnsureIndex(x => x.NumberId);
                    }

                    db.Commit();
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Query order status

        private void CheckOrdersStatus()
        {
            if (_server.ServerStatus != ServerConnectStatus.Connect)
            {
                return;
            }

            for (int i = 0;i < _ordersActiv.Count;i++)
            {
                CheckOrderState(_ordersActiv[i]);
            }
        }

        private void CheckOrderState(OrderToWatch order)
        {
            if(order.IsFinallyLost)
            {
                return;
            }

            if(order.CountTriesToGetOrderStatus >= 5)
            {
                order.IsFinallyLost = true;

                if(LostOrderEvent != null)
                {
                    LostOrderEvent(order.Order);
                }
            }

            if (order.LastTryGetStatusTime == DateTime.MinValue)
            {
                order.LastTryGetStatusTime = DateTime.Now;
            }

            if(order.Order.TypeOrder == OrderPriceType.Market)
            {
                CheckMarketOrder(order);
            }
            else if (order.Order.TypeOrder == OrderPriceType.Limit)
            {
                CheckLimitOrder(order);
            }
        }

        private void CheckMarketOrder(OrderToWatch order)
        {
            if(order.CountEventsFromApi == 0
                && order.CountTriesToGetOrderStatus == 0
                && order.LastTryGetStatusTime.AddSeconds(5) < DateTime.Now)
            { // не пришло ни одного отклика от АПИ. Запрашиваем статус ордера в первый раз

                if (_fullLogIsOn)
                {
                    SendLogMessage("Ask order status. Market. No response from API after 5 sec NumUser: " + order.Order.NumberUser
                        + " NumMarket: " + order.Order.NumberMarket
                        + " Status: " + order.Order.State
                        + " Try: " + order.CountTriesToGetOrderStatus
                        , LogMessageType.System);
                }

                order.CountTriesToGetOrderStatus++;
                ActivStateOrderCheckStatusEvent(order.Order);
                order.LastTryGetStatusTime = DateTime.Now;

                return;
            }

            if (order.Order.State == OrderStateType.None
                 && order.CountTriesToGetOrderStatus > 0
                 && order.LastTryGetStatusTime.AddSeconds(5 * order.CountTriesToGetOrderStatus) < DateTime.Now)
            { // не пришёл статус Activ. Всё ещё NONE
              // периоды запросов: через 5 сек. через 5 сек. через 10 сек. через 15 сек. через 20 сек. Всё.

                if (_fullLogIsOn)
                {
                    SendLogMessage("Ask order status. Market. No response from API. sec NumUser: " + order.Order.NumberUser
                        + " NumMarket: " + order.Order.NumberMarket
                        + " Status: " + order.Order.State
                        + " Try: " + order.CountTriesToGetOrderStatus
                        , LogMessageType.System);
                }

                order.CountTriesToGetOrderStatus++;
                ActivStateOrderCheckStatusEvent(order.Order);
                order.LastTryGetStatusTime = DateTime.Now;


                return;
            }
        }

        private void CheckLimitOrder(OrderToWatch order)
        {
            if (order.CountEventsFromApi == 0
               && order.CountTriesToGetOrderStatus == 0
               && order.LastTryGetStatusTime.AddSeconds(5) < DateTime.Now)
            { // не пришло ни одного отклика от АПИ. Запрашиваем статус ордера в первый раз

                if (_fullLogIsOn)
                {
                    SendLogMessage("Ask order status. Limit. No response from API after 5 sec NumUser: " + order.Order.NumberUser
                        + " NumMarket: " + order.Order.NumberMarket
                        + " Status: " + order.Order.State
                        + " Try: " + order.CountTriesToGetOrderStatus
                        , LogMessageType.System);
                }

                order.CountTriesToGetOrderStatus++;
                ActivStateOrderCheckStatusEvent(order.Order);
                order.LastTryGetStatusTime = DateTime.Now;

                return;
            }

            if (order.Order.State == OrderStateType.None
                && order.CountTriesToGetOrderStatus > 0
                && order.LastTryGetStatusTime.AddSeconds(5 * order.CountTriesToGetOrderStatus) < DateTime.Now)
            {   // не пришёл статус Activ. Всё ещё NONE
                // периоды запросов: через 5 сек. через 5 сек. через 10 сек. через 15 сек. через 20 сек. Всё.

                if (_fullLogIsOn)
                {
                    SendLogMessage("Ask order status. Limit. No response from API. sec NumUser: " + order.Order.NumberUser
                        + " NumMarket: " + order.Order.NumberMarket
                        + " Status: " + order.Order.State
                        + " Try: " + order.CountTriesToGetOrderStatus
                        , LogMessageType.System);
                }

                order.CountTriesToGetOrderStatus++;
                ActivStateOrderCheckStatusEvent(order.Order);
                order.LastTryGetStatusTime = DateTime.Now;

                return;
            }

            if (order.LastTryGetStatusTime.AddSeconds(300) < DateTime.Now)
            {   // статусы лимиток дополнительно проверяем раз в 5ть минут. 

                if (_fullLogIsOn)
                {
                    SendLogMessage("Ask order status. Limit. Standart ask in five minutes. NumUser: " + order.Order.NumberUser
                        + " NumMarket: " + order.Order.NumberMarket
                        + " Status: " + order.Order.State
                        , LogMessageType.System);
                }

                ActivStateOrderCheckStatusEvent(order.Order);
                order.LastTryGetStatusTime = DateTime.Now;
                return;
            }
        }

        public event Action<Order> ActivStateOrderCheckStatusEvent;

        public event Action<Order> LostOrderEvent;

        #endregion

        #region Log

        /// <summary>
        /// add a new message in the log
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent("AServerOrderHub: " + message, type);
            }
        }

        /// <summary>
        /// outgoing messages for the log event
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    public class OrderToWatch
    {
        public Order Order;

        public int CountTriesToGetOrderStatus;

        public int CountEventsFromApi;

        public bool IsFinallyLost;

        public DateTime LastTryGetStatusTime;

    }

    public class OrderToSave
    {
        public int NumberId { get; set; }

        public int NumberUser { get; set; }

        public string NumberMarket { get; set; }

        public string SaveString { get; set; }
    }
}