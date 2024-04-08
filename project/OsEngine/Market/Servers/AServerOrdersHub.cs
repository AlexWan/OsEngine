using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Logging;

namespace OsEngine.Market.Servers
{
    public class AServerOrdersHub
    {
        public AServerOrdersHub(AServer server) 
        {
            _server = server;

            IServerPermission permission = ServerMaster.GetServerPermission(server.ServerType);

            if(permission == null)
            {
                return;
            }

            if(permission.CanQueryOrdersAfterReconnect)
            {
                _canQueryOrdersAfterReconnect = true;
                Thread worker = new Thread(ThreadWorkerAreaQueryOrdersAfterReconnect);
                worker.Name = "ThreadWorkerAreaQueryOrdersAfterReconnect";
                worker.Start();
            }

            if(permission.CanQueryOrderStatus)
            {
                _canQueryOrderStatus = true;

                Thread worker = new Thread(ThreadWorkerAreaQueryOrderStatus);
                worker.Name = "ThreadWorkerAreaQueryOrderStatus";
                worker.Start();

                Thread worker2 = new Thread(ThreadWorkerAreaLoadSaveOrders);
                worker2.Name = "ThreadWorkerAreaLoadSaveOrders";
                worker2.Start();
            }
        }

        AServer _server;

        bool _canQueryOrdersAfterReconnect;

        bool _canQueryOrderStatus;

        bool _fullLogIsOn = false;

        #region Set orders

        public void SetOrderFromOsEngine(Order order)
        {
            if (_canQueryOrderStatus == false)
            {
                return;
            }

            _ordersFromOsEngineQueue.Enqueue(order);

            if(_fullLogIsOn)
            {
                SendLogMessage("New order in OsEngine. NumUser: " + order.NumberUser, LogMessageType.System);
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
                    + " NumMarket: " + order.NumberMarket, LogMessageType.System);
            }
        }

        ConcurrentQueue<Order> _ordersFromOsEngineQueue = new ConcurrentQueue<Order>();

        ConcurrentQueue<Order> _orderFromApiQueue = new ConcurrentQueue<Order>();

        #endregion

        #region Orders Hub

        private List<Order> _ordersActiv = new List<Order>();

        bool _ordersIsLoaded = false;

        private void ThreadWorkerAreaLoadSaveOrders()
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

                    if(_ordersIsLoaded == false)
                    {
                        _ordersIsLoaded = true;
                        LoadOrdersFromFile();
                    }

                    if(_orderFromApiQueue.IsEmpty == false
                        || _ordersFromOsEngineQueue.IsEmpty == false)
                    {
                        GetOrdersFromQueue();
                    }

                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
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
                    _ordersActiv.Add(newOpenOrder);
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
                Order curOrderFromOsEngine = _ordersActiv[i];

                if(orderFromApi.NumberUser != curOrderFromOsEngine.NumberUser)
                {
                    continue;
                }

                if(orderFromApi.State == OrderStateType.Activ
                    || orderFromApi.State == OrderStateType.Patrial
                    || orderFromApi.State == OrderStateType.Pending)
                {
                    
                    _ordersActiv[i] = orderFromApi;

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
            if (!File.Exists(@"Engine\" + _server.ServerType.ToString() + @"ordersHub.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + _server.ServerType.ToString() + @"ordersHub.txt"))
                {
                    while(reader.EndOfStream == false)
                    {
                        string orderInString = reader.ReadLine();

                        if(string.IsNullOrEmpty(orderInString) == false)
                        {
                            Order newOrder = new Order();
                            newOrder.SetOrderFromString(orderInString);

                            if (newOrder.State == OrderStateType.None
                                || newOrder.State == OrderStateType.Fail
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

                            _ordersActiv.Add(newOrder);

                            if (_fullLogIsOn)
                            {
                                SendLogMessage("New alive order LOAD. NumUser: " + newOrder.NumberUser
                                    + " NumMarket: " + newOrder.NumberMarket
                                    + " Status: " + newOrder.State, LogMessageType.System);
                            }
                        }
                    }

                    reader.Close();
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
                using (StreamWriter writer = new StreamWriter(@"Engine\" + _server.ServerType.ToString() + @"ordersHub.txt", false)
                    )
                {
                    for (int i = 0; i < _ordersActiv.Count; i++)
                    {
                        writer.WriteLine(_ordersActiv[i].GetStringForSave());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        #endregion

        #region Query orders after reconnect

        private void ThreadWorkerAreaQueryOrdersAfterReconnect()
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

                    if(_server.ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        _lastDisconnectTime = DateTime.Now;
                        _checkOrdersAfterLastConnect = false;
                        continue;
                    }

                    if(_checkOrdersAfterLastConnect == true)
                    {
                        continue;
                    }

                    if(_lastDisconnectTime.AddSeconds(15) < DateTime.Now)
                    {
                        _checkOrdersAfterLastConnect = true;

                        if(GetAllActivOrdersOnReconnectEvent != null)
                        {
                            GetAllActivOrdersOnReconnectEvent();
                        }
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private DateTime _lastDisconnectTime;

        private bool _checkOrdersAfterLastConnect = false;

        public event Action GetAllActivOrdersOnReconnectEvent;

        #endregion

        #region Query order status

        private void ThreadWorkerAreaQueryOrderStatus()
        {
            while(true)
            {
                try
                {
                    Thread.Sleep(1000);

                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    // Что должен делать этот поток:

                    // 1 копируем ордера из массива живых ордеров в свой массив
                    // 2 удаляем лимитки с дед статусами

                    // проверки
                    // статусы маркет ордеров начинаем вызывать через 5ть секунд как увидели
                    // статусы лимиток начинаем вызывать когда цена пересекла цену ордера. Через пять секунд
                    // статусы лимиток дополнительно проверяем раз в 5ть минут. 




                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        public void SetBidAsk(decimal bid, decimal ask, Security security)
        {
            // это мы должны проверять тут лимитки от OsEngine

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

    public class OrderToSave
    {
        public Order Order;

        public int CountTriesToGetOrderStatus;

        public DateTime LastTryGetStatusTime;

        public string GetSaveString()
        {

            return null;
        }

        public void LoadFromString(string str)
        {



        }
    }
}