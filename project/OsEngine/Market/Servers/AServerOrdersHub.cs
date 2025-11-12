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

        public void SetMyTradeFromApi(MyTrade myTrade)
        {
            if (_canQueryOrderStatus == false)
            {
                return;
            }

            _myTradesFromApiQueue.Enqueue(myTrade);

            if (_fullLogIsOn)
            {
                SendLogMessage("New my Trade in Api. Number: " + myTrade.NumberTrade
                    + " Order number: " + myTrade.NumberOrderParent
                    , LogMessageType.System);
            }
        }

        ConcurrentQueue<Order> _ordersFromOsEngineQueue = new ConcurrentQueue<Order>();

        ConcurrentQueue<Order> _orderFromApiQueue = new ConcurrentQueue<Order>();

        ConcurrentQueue<MyTrade> _myTradesFromApiQueue = new ConcurrentQueue<MyTrade>();

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
                        ManageMyTrades();
                    }
                   
                    // 3 проверка статусов ордеров и трейдов к ним

                    if(_canQueryOrderStatus)
                    {
                        CheckOrdersStatus();
                        CheckMyTradesStatus();
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

            if(_server.IsReadyToTrade == false)
            {
                return;
            }

            if (_lastDisconnectTime.AddSeconds(_secondsToWaitRequest) < DateTime.Now)
            {
                _checkOrdersAfterLastConnect = true;

                if (GetAllActiveOrdersOnReconnectEvent != null)
                {
                    GetAllActiveOrdersOnReconnectEvent();

                    if (_fullLogIsOn)
                    {
                        SendLogMessage("Event: GetAllActiveOrdersOnReconnectEvent", LogMessageType.System);
                    }
                }

                if(_canQueryOrderStatus == true)
                {
                    if (_fullLogIsOn)
                    {
                        SendLogMessage("We start requesting the status of our orders at the start. Number of: " + _ordersActive.Count, LogMessageType.System);
                    }

                    for (int i = 0; i < _ordersActive.Count; i++)
                    {
                        if (_ordersActive[i].IsFinallyLost == false
                            && _ordersActive[i].CountTriesToGetOrderStatus < 5)
                        {
                            ActiveStateOrderCheckStatusEvent(_ordersActive[i].Order);
                        }
                    }

                    if (_fullLogIsOn)
                    {
                        SendLogMessage("Completed requesting the status of their orders at the start. ", LogMessageType.System);
                    }
                }
            }
        }

        private DateTime _lastDisconnectTime;

        private int _secondsToWaitRequest;

        private bool _checkOrdersAfterLastConnect = false;

        public event Action GetAllActiveOrdersOnReconnectEvent;

        public void ForceCheckOrdersAfterReconnect()
        {
            _lastDisconnectTime = DateTime.Now;
            _checkOrdersAfterLastConnect = false;
        }

        #endregion

        #region Orders Hub

        private List<OrderToWatch> _ordersActive = new List<OrderToWatch>();

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

            for (int i = 0; i < _ordersActive.Count; i++)
            {
                Order order = _ordersActive[i].Order;

                if(order.TimeCreate != DateTime.MinValue 
                    && order.TimeCreate.AddDays(1) < DateTime.Now)
                {
                    SendLogMessage("Order remove BY TIME 1. NumUser: " + order.NumberUser
                     + " NumMarket: " + order.NumberMarket
                     + " Status: " + order.State
                     + " TimeCreate: " + order.TimeCreate
                     , LogMessageType.System);

                    _ordersActive.RemoveAt(i);
                    i--;
                    orderIsDelete = true;
                }

                else if (order.TimeCallBack != DateTime.MinValue
                    && order.TimeCallBack.AddDays(1) < DateTime.Now)
                {
                    SendLogMessage("Order remove BY TIME 2. NumUser: " + order.NumberUser
                    + " NumMarket: " + order.NumberMarket
                    + " Status: " + order.State
                    + " TimeCallBack: " + order.TimeCallBack
                    , LogMessageType.System);

                    _ordersActive.RemoveAt(i);
                    i--;
                    orderIsDelete = true;
                }
            }

            // 2 удаляем окончательно потерянные ордера о которых на верх уже выслали сообщение

            for (int i = 0; i < _ordersActive.Count; i++)
            {
                OrderToWatch order = _ordersActive[i];

                if (order.IsFinallyLost)
                {
                    SendLogMessage("Order remove BY FINALLY LOST. NumUser: " + order.Order.NumberUser
                     + " NumMarket: " + order.Order.NumberMarket
                     + " Status: " + order.Order.State
                     , LogMessageType.System);

                    _ordersActive.RemoveAt(i);
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

                    _ordersActive.Add(orderToWatch);
                }
            }

            while (_orderFromApiQueue.IsEmpty == false)
            {
                Order newOrder = null;

                if (_orderFromApiQueue.TryDequeue(out newOrder))
                {
                   // 2 перегружаем ордера которые пришли из АПИ в хранилище ордеров которые сгенерировал OsEngine
                    TrySetOrderInHub(newOrder);
                    TrySetOrderInOrdersWithVolume(newOrder);
                }
            }

            // 3 сохраняем

            SaveOrdersInFile();
        }

        private void TrySetOrderInHub(Order orderFromApi)
        {
            // удаляем всё что исполнилось или отменено или ошибочно

            for (int i = 0;i < _ordersActive.Count;i++)
            {
                Order curOrderFromOsEngine = _ordersActive[i].Order;

                if(orderFromApi.NumberUser != curOrderFromOsEngine.NumberUser)
                {
                    continue;
                }

                if(orderFromApi.State == OrderStateType.Active
                    || orderFromApi.State == OrderStateType.Partial
                    || orderFromApi.State == OrderStateType.Pending)
                {
                    
                    _ordersActive[i].Order = orderFromApi;
                    _ordersActive[i].CountEventsFromApi++;

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
                    _ordersActive.RemoveAt(i);

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

                dir += _server.ServerNameUnique + "_active_orders.db";

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

                            _ordersActive.Add(orderToWatch);

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

                dir += _server.ServerNameUnique + "_active_orders.db";

                using (LiteDatabase db = new LiteDatabase(dir))
                {
                    var collection = db.GetCollection<OrderToSave>("orders");

                    List<OrderToSave> col = collection.FindAll().ToList();

                    // 1 вставляем в базу ордера которые сейчас есть в массиве активных ордеров

                    for (int i = 0; i < _ordersActive.Count; i++)
                    {
                        OrderToSave orderToSave = new OrderToSave();
                        orderToSave.NumberId = i;
                        orderToSave.NumberMarket = _ordersActive[i].Order.NumberMarket;
                        orderToSave.NumberUser = _ordersActive[i].Order.NumberUser;
                        orderToSave.SaveString = _ordersActive[i].Order.GetStringForSave().ToString();

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

                        for (int j = 0; j < _ordersActive.Count; j++)
                        {
                            OrderToWatch order = _ordersActive[j];

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

            for (int i = 0;i < _ordersActive.Count;i++)
            {
                CheckOrderState(_ordersActive[i]);
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
                ActiveStateOrderCheckStatusEvent(order.Order);
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
                ActiveStateOrderCheckStatusEvent(order.Order);
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
                ActiveStateOrderCheckStatusEvent(order.Order);
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
                ActiveStateOrderCheckStatusEvent(order.Order);
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

                ActiveStateOrderCheckStatusEvent(order.Order);
                order.LastTryGetStatusTime = DateTime.Now;
                return;
            }
        }

        public event Action<Order> ActiveStateOrderCheckStatusEvent;

        public event Action<Order> LostOrderEvent;

        #endregion

        #region Query MyTrades to execute orders

        private List<OrderToWatch> _ordersWithVolume = new List<OrderToWatch>();

        private List<MyTrade> _myTrades = new List<MyTrade>();

        private void TrySetOrderInOrdersWithVolume(Order orderFromApi)
        {
            if(orderFromApi == null)
            {
                return;
            }

            if(orderFromApi.State != OrderStateType.Partial
                && orderFromApi.State != OrderStateType.Done)
            {
                return;
            }

            bool isInArray = false;

            for (int i = 0; i < _ordersWithVolume.Count; i++)
            {
                if (_ordersWithVolume[i].Order.NumberMarket == orderFromApi.NumberMarket)
                {
                    isInArray = true;
                    _ordersWithVolume[i].Order = orderFromApi;
                }
            }

            if(isInArray == false)
            {
                OrderToWatch newOrder = new OrderToWatch();
                newOrder.Order = orderFromApi;

                _ordersWithVolume.Add(newOrder);

                if (_fullLogIsOn)
                {
                    SendLogMessage("New order have volume.: "
                        + " NumMarket: " + orderFromApi.NumberMarket
                        + " Status: " + orderFromApi.State
                        + " Volume: " + orderFromApi.VolumeExecute
                        , LogMessageType.System);
                }
            }
        }

        private void ManageMyTrades()
        {
            while (_myTradesFromApiQueue.Count > 0)
            {
                MyTrade newMyTrade = null;

                if (_myTradesFromApiQueue.TryDequeue(out newMyTrade))
                {
                    bool isInArray = false;

                    for (int i = 0; i < _myTrades.Count; i++)
                    {
                        if (_myTrades[i].NumberTrade == newMyTrade.NumberTrade)
                        {
                            isInArray = true;
                        }
                    }

                    if(isInArray == false)
                    {
                        if (_fullLogIsOn)
                        {
                            SendLogMessage("New MyTrade"
                                + " NumMarket: " + newMyTrade.NumberTrade
                                + " NumOrder: " + newMyTrade.NumberOrderParent
                                , LogMessageType.System);
                        }

                        _myTrades.Add(newMyTrade);
                    }
                    
                    if(_myTrades.Count > 500)
                    {
                        _myTrades.RemoveAt(0);
                    }
                }
            }
        }

        private void CheckMyTradesStatus()
        {
            for(int i = 0;i < _ordersWithVolume.Count;i++)
            {
                OrderToWatch order = _ordersWithVolume[i];

                if (order.IsFinallyLost)
                {
                    continue;
                }

                if (order.CountTriesToGetOrderStatus >= 5)
                {
                    order.IsFinallyLost = true;

                    if (LostMyTradesEvent != null)
                    {
                        LostMyTradesEvent(order.Order);
                    }
                }

                if (order.LastTryGetStatusTime == DateTime.MinValue)
                {
                    order.LastTryGetStatusTime = DateTime.Now;
                }

                decimal volumeInMyTrades 
                    = GetVolumeToTradeNumInMyTradesArray(order.Order.NumberMarket);

                if((order.Order.State == OrderStateType.Partial
                    || order.Order.State == OrderStateType.Done)
                    && volumeInMyTrades == 0 
                    && order.LastTryGetStatusTime.AddSeconds(5 * order.CountTriesToGetOrderStatus) < DateTime.Now)
                { // проблема 1. Ордер частично исполнен по статусу, но трейдов нет вообще

                    if (_fullLogIsOn)
                    {
                        SendLogMessage("Error. No MyTrades by order." 
                            + " Order NumMarket: " + order.Order.NumberMarket
                            + " Status: " + order.Order.State
                            + " Try: " + order.CountTriesToGetOrderStatus
                            , LogMessageType.System);
                    }

                    order.CountTriesToGetOrderStatus++;
                    ActiveStateOrderCheckStatusEvent(order.Order);
                    order.LastTryGetStatusTime = DateTime.Now;

                }
                else if(order.Order.State == OrderStateType.Done
                    && volumeInMyTrades < order.Order.VolumeExecute
                    && order.LastTryGetStatusTime.AddSeconds(5 * order.CountTriesToGetOrderStatus) < DateTime.Now)
                {// проблема 2. Объёмов меньше чем заявлено в исполненном ордере

                    if (_fullLogIsOn)
                    {
                        SendLogMessage("Error in MyTrades volume to order." 
                            + " Order NumMarket: " + order.Order.NumberMarket
                            + " Status: " + order.Order.State
                            + " Try: " + order.CountTriesToGetOrderStatus
                            + " VolumeInMyTrades: " + volumeInMyTrades
                            , LogMessageType.System);
                    }

                    order.CountTriesToGetOrderStatus++;
                    ActiveStateOrderCheckStatusEvent(order.Order);
                    order.LastTryGetStatusTime = DateTime.Now;
                }
                else if((order.Order.State == OrderStateType.Cancel
                    || order.Order.State == OrderStateType.Done)
                    && volumeInMyTrades != 0
                    && volumeInMyTrades == order.Order.VolumeExecute)
                {
                    if (_fullLogIsOn)
                    {
                        SendLogMessage("Success. MyTrades volume to order."
                            + " Order NumMarket: " + order.Order.NumberMarket
                            + " Status: " + order.Order.State
                            + " Try: " + order.CountTriesToGetOrderStatus
                            + " VolumeInMyTrades: " + volumeInMyTrades
                            + " VolumeInOrder: " + order.Order.VolumeExecute
                            , LogMessageType.System);
                    }

                    RemoveTradesByOrder(order.Order.NumberMarket);
                    _ordersWithVolume.RemoveAt(i);
                    return;
                }
            }
        }

        private decimal GetVolumeToTradeNumInMyTradesArray(string orderNum)
        {
            decimal result = 0;

            for(int i = 0;i < _myTrades.Count;i++)
            {
                MyTrade trade = _myTrades[i];

                if(trade.NumberOrderParent == orderNum)
                {
                    result += trade.Volume;
                }
            }

            return result;
        }

        private void RemoveTradesByOrder(string orderNum)
        {
            for (int i = 0; i < _myTrades.Count; i++)
            {
                MyTrade trade = _myTrades[i];

                if (trade.NumberOrderParent == orderNum)
                {
                    _myTrades.RemoveAt(i);
                    i--;
                }
            }
        }

        public event Action<Order> LostMyTradesEvent;

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