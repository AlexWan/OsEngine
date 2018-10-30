using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.Quik
{
    /// <summary>
    /// сервер Квик ДДЕ
    /// </summary>
    public class QuikServer:AServer
    {
        public QuikServer()
        {
            QuikDdeServerRealization realization = new QuikDdeServerRealization();
            ServerRealization = realization;

            CreateParameterPath("Путь к Квик");
        }
    }

    /// <summary>
    /// реализация сервера Квик ДДЕ
    /// </summary>
    public class QuikDdeServerRealization : IServerRealization
    {

// сервис
        public QuikDdeServerRealization()
        {
            Thread statusWatcher = new Thread(StatusWatcherArea);
            statusWatcher.IsBackground = true;
            statusWatcher.CurrentCulture = CultureInfo.InvariantCulture;
            statusWatcher.Name = "ThreadQuikDdeServerRealizationStatusWatcher";
            statusWatcher.Start();

            _status = ServerConnectStatus.Disconnect;
            _ddeStatus = ServerConnectStatus.Disconnect;
            _transe2QuikStatus = ServerConnectStatus.Disconnect;
            _tradesStatus = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// тип сервера
        /// </summary>
        public ServerType ServerType
        {
            get { return ServerType.QuikDde; }
        }

        public List<IServerParameter> ServerParameters { get; set; }

        public DateTime ServerTime { get; set; }


// подключение / отключение

        /// <summary>
        /// время старта севрера
        /// </summary>
        private DateTime _lastStartServerTime;

        /// <summary>
        /// освободить объекты связанные с сервером
        /// </summary>
        public void Dispose()
        {
            if (_serverDde != null && _serverDde.IsRegistered)
            {
                _serverDde.StopDdeInQuik();
                _serverDde = null;
            }

            try
            {
                int error;
                var msg = new StringBuilder(256);
                Trans2Quik.DISCONNECT(out error, msg, msg.Capacity);
            }
            catch (Exception erorr)
            {
                SendLogMessage(erorr.ToString(), LogMessageType.Error);
            }

            if (DisconnectEvent != null)
            {
                DisconnectEvent();
            }
        }

        /// <summary>
        /// подключиться к Апи
        /// </summary>
        public void Connect()
        {
            try
            {
                _lastStartServerTime = DateTime.Now;

                

                if (string.IsNullOrWhiteSpace(((ServerParameterPath)ServerParameters[0]).Value))
                {
                    SendLogMessage("Ошибка. Необходимо указать местоположение Quik", LogMessageType.Error);
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Connect)
                {
                    SendLogMessage("Ошибка. Соединение уже установлено", LogMessageType.Error);
                    return;
                }

                if (_transe2QuikStatus != ServerConnectStatus.Connect)
                {
                    int error;
                    var msg = new StringBuilder(256);

                    Trans2Quik.QuikResult result = Trans2Quik.SET_CONNECTION_STATUS_CALLBACK(StatusCallback, out error, msg,
                        msg.Capacity);

                    if (result != Trans2Quik.QuikResult.SUCCESS)
                    {
                        SendLogMessage("Ошибка. Trans2Quik не хочет подключаться " + error, LogMessageType.Error);
                        return;
                    }

                    result = Trans2Quik.CONNECT(((ServerParameterPath)ServerParameters[0]).Value, out error, msg, msg.Capacity);

                    if (result != Trans2Quik.QuikResult.SUCCESS && result != Trans2Quik.QuikResult.ALREADY_CONNECTED_TO_QUIK)
                    {
                        SendLogMessage("Ошибка при попытке подклчиться через Transe2Quik." + msg, LogMessageType.Error);
                        return;
                    }

                    Trans2Quik.SET_TRANSACTIONS_REPLY_CALLBACK(TransactionReplyCallback, out error, msg,
                         msg.Capacity);

                    result = Trans2Quik.SUBSCRIBE_ORDERS("", "");
                    while (result != Trans2Quik.QuikResult.SUCCESS)
                    {
                        Thread.Sleep(5000);
                        result = Trans2Quik.SUBSCRIBE_ORDERS("", "");
                        SendLogMessage("Повторно пытаемся включить поток сделок" + msg, LogMessageType.Error);
                    }

                    result = Trans2Quik.START_ORDERS((PfnOrderStatusCallback));
                    while (result != Trans2Quik.QuikResult.SUCCESS)
                    {
                        Thread.Sleep(5000);
                        result = Trans2Quik.START_ORDERS((PfnOrderStatusCallback));
                        SendLogMessage("Повторно пытаемся включить поток сделок" + msg, LogMessageType.Error);
                    }

                    Trans2Quik.SUBSCRIBE_TRADES("", "");
                    Trans2Quik.START_TRADES(PfnTradeStatusCallback);

                    Trans2Quik.GetTradeAccount(0);
                }

                if (_serverDde == null)
                {
                    _serverDde = new QuikDde("OSA_DDE");
                    _serverDde.StatusChangeEvent += _serverDde_StatusChangeEvent;
                    _serverDde.UpdatePortfolios += _serverDde_UpdatePortfolios;
                    _serverDde.UpdateSecurity += _serverDde_UpdateSecurity;
                    _serverDde.UpdateTrade += _serverDde_UpdateTrade;
                    _serverDde.UpdateGlass += _serverDde_UpdateGlass;
                    _serverDde.LogMessageEvent += SendLogMessage;
                }

                Thread.Sleep(1000);

                if (!_serverDde.IsRegistered)
                {
                    _serverDde.StartServer();
                }
                else
                {
                    _ddeStatus = ServerConnectStatus.Connect;
                }
            }
            catch (Exception erorr)
            {
                SendLogMessage(erorr.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// в этом месте живёт поток следящий за готовностью сервера
        /// </summary>
        private void StatusWatcherArea()
        {
            while (true)
            {
                Thread.Sleep(300);

                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }

                if (_ddeStatus == ServerConnectStatus.Connect &&
                    _transe2QuikStatus == ServerConnectStatus.Connect &&
                    _tradesStatus == ServerConnectStatus.Connect &&
                     _connectToBrokerStatus == ServerConnectStatus.Connect &&
                    _status == ServerConnectStatus.Disconnect)
                {
                    if (_lastStartServerTime.AddSeconds(40) > DateTime.Now)
                    {
                        continue;
                    }
                    _status = ServerConnectStatus.Connect;

                    if (ConnectEvent != null)
                    {
                        ConnectEvent();
                    }
                }

                if ((_ddeStatus == ServerConnectStatus.Disconnect ||
                    _transe2QuikStatus == ServerConnectStatus.Disconnect ||
                     _connectToBrokerStatus == ServerConnectStatus.Disconnect ||
                     _tradesStatus == ServerConnectStatus.Disconnect) &&
                    _status == ServerConnectStatus.Connect)
                {
                    _status = ServerConnectStatus.Disconnect;
                    if (DisconnectEvent != null)
                    {
                        DisconnectEvent();
                    }
                }

            }
        }

        /// <summary>
        /// статус ДДЕ сервера
        /// </summary>
        private ServerConnectStatus _ddeStatus;

        /// <summary>
        /// статус трансТуКвик библиотеки
        /// </summary>
        private ServerConnectStatus _transe2QuikStatus;

        /// <summary>
        /// подключены ли к серверу
        /// </summary>
        private ServerConnectStatus _connectToBrokerStatus;

        /// <summary>
        /// статус подгрузки тиков
        /// </summary>
        private ServerConnectStatus _tradesStatus;

        /// <summary>
        /// основной статус сервера. Connect если верхние три тоже коннект
        /// </summary>
        private ServerConnectStatus _status;

        /// <summary>
        /// статус сервера
        /// </summary>
        public ServerConnectStatus ServerStatus 
        { get { return _status; } set { _status = value; } }

        /// <summary>
        /// входящее сообщение об изменении состояния подключения к Квик
        /// </summary>
        private void StatusCallback(Trans2Quik.QuikResult connectionEvent,
            int extendedErrorCode, string infoMessage)
        {
            try
            {
                if (connectionEvent == Trans2Quik.QuikResult.DLL_CONNECTED)
                {
                    _transe2QuikStatus = ServerConnectStatus.Connect;
                    SendLogMessage("Transe2Quik изменение статуса " + _transe2QuikStatus, LogMessageType.System);
                }
                if (connectionEvent == Trans2Quik.QuikResult.DLL_DISCONNECTED)
                {
                    _transe2QuikStatus = ServerConnectStatus.Disconnect;
                    SendLogMessage("Transe2Quik изменение статуса " + _transe2QuikStatus, LogMessageType.System);
                }
                if (connectionEvent == Trans2Quik.QuikResult.QUIK_CONNECTED)
                {
                    _connectToBrokerStatus = ServerConnectStatus.Connect;
                    SendLogMessage("Соединение Квик с сервером брокера установлено. ", LogMessageType.System);
                }
                if (connectionEvent == Trans2Quik.QuikResult.QUIK_DISCONNECTED)
                {
                    _connectToBrokerStatus = ServerConnectStatus.Disconnect;
                    SendLogMessage("Соединение Квик с сервером брокера разорвано. ", LogMessageType.System);
                }
            }
            catch (Exception erorr)
            {
                SendLogMessage(erorr.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// входящее сообщение об изменении состояния ДДЕ сервера
        /// </summary>
        void _serverDde_StatusChangeEvent(DdeServerStatus status)
        {
            try
            {
                if (status == DdeServerStatus.Connected)
                {
                    _ddeStatus = ServerConnectStatus.Connect;
                    SendLogMessage("DDE Server изменение статуса " + _ddeStatus, LogMessageType.System);
                }
                if (status == DdeServerStatus.Disconnected)
                {
                    _ddeStatus = ServerConnectStatus.Disconnect;
                    SendLogMessage("DDE Server изменение статуса " + _ddeStatus, LogMessageType.System);
                }
            }
            catch (Exception erorr)
            {
                SendLogMessage(erorr.ToString(), LogMessageType.Error);
            }

        }

// портфели

        /// <summary>
        /// портфели
        /// </summary>
        private List<Portfolio> _portfolios; 

        /// <summary>
        /// Апи прислал нам протфели
        /// </summary>
        void _serverDde_UpdatePortfolios(List<Portfolio> portfolios)
        {
            _portfolios = portfolios;

            if (PortfolioEvent != null)
            {
                PortfolioEvent(portfolios);
            }
        }

// трейды
        /// <summary>
        /// пришли новые сделки из ДДЕ сервера
        /// </summary>
        private void _serverDde_UpdateTrade(List<Trade> tradesNew)
        {
            if (tradesNew == null ||
                tradesNew.Count == 0)
            {
                return;
            }

            if (NewTradesEvent != null)
            {
                for (int i = 0; i < tradesNew.Count; i++)
                {
                    NewTradesEvent(tradesNew[i]);
                }
            }

            if (_tradesStatus == ServerConnectStatus.Disconnect &&
            tradesNew[tradesNew.Count - 1].Time.AddSeconds(10) > ServerTime)
            {
                _tradesStatus = ServerConnectStatus.Connect;
                SendLogMessage("Зафиксирован переход тиков в онЛайн трансляцию. ", LogMessageType.System);
            }
        }

// бумаги

        /// <summary>
        /// бумаги
        /// </summary>
        private List<Security> _securities; 

        /// <summary>
        /// входящие бумаги из ДДЕ сервера
        /// </summary>
        void _serverDde_UpdateSecurity(Security security, decimal bestAsk, decimal bestBid)
        {
            bool newSecurity = false;

            if (ServerTime == DateTime.MinValue)
            {
                return;
            }

            security.NameId = security.Name + security.NameClass;

            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            if (_securities.Find(s => s.Name == security.Name) == null)
            {
                _securities.Add(security);
                newSecurity = true;
            }

            if (SecurityEvent != null && newSecurity)
            {
                SecurityEvent(_securities);
            }

            if (bestBid == 0 ||
                bestAsk == 0)
            {
                return;
            }

            if (bestBid > bestAsk)
            {
                return;
            }

            if (_marketDepths.Find(depth => depth.SecurityNameCode == security.Name) == null)
            {
                MarketDepth depth = new MarketDepth();
                depth.SecurityNameCode = security.Name;
                depth.Time = ServerTime;
                depth.Asks.Add(new MarketDepthLevel() { Ask = 1, Price = bestAsk });
                depth.Bids.Add(new MarketDepthLevel() { Bid = 1, Price = bestBid });

                if (MarketDepthEvent != null)
                {
                    MarketDepthEvent(depth);
                }
            }
        }

// стакан

        /// <summary>
        /// пришёл новый стакан
        /// </summary>
        void _serverDde_UpdateGlass(MarketDepth marketDepth)
        {
            if (ServerTime == DateTime.MinValue)
            {
                return;
            }
            marketDepth.Time = ServerTime;

            if (MarketDepthEvent != null)
            {
                MarketDepthEvent(marketDepth);
            }

            // грузим стаканы в хранилище
            for (int i = 0; i < _marketDepths.Count; i++)
            {
                if (_marketDepths[i].SecurityNameCode == marketDepth.SecurityNameCode)
                {
                    _marketDepths[i] = marketDepth;
                    return;
                }
            }
            _marketDepths.Add(marketDepth.GetCopy());
        }

        /// <summary>
        /// стаканы
        /// </summary>
        private List<MarketDepth> _marketDepths = new List<MarketDepth>(); 

// обработка запросов

        /// <summary>
        /// запрашивает у коннектора бумаги
        /// </summary>
        public void GetSecurities()
        {
     
        }

        /// <summary>
        /// запрашивает у коннектора портфели
        /// </summary>
        public void GetPortfolios()
        {
         
        }

        /// <summary>
        /// подписаться на информацию по бумаге
        /// </summary>
        /// <param name="security"></param>
        public void Subscrible(Security security)
        {

        }

        /// <summary>
        /// выслать ордер
        /// </summary>
        public void SendOrder(Order order)
        {
            string command = ConvertToSimpleQuikOrder(order);

            int error;
            var msg = new StringBuilder(256);

            if (command == null)
            {
                return;
            }

            order.TimeCreate = ServerTime;
            SetOrder(order);

            Trans2Quik.QuikResult result = Trans2Quik.SEND_ASYNC_TRANSACTION(command, out error, msg, msg.Capacity);

            if (msg.ToString() != "")
            {
                Order newOrder = new Order();
                newOrder.NumberUser = order.NumberUser;
                newOrder.State = OrderStateType.Fail;
                if (MyOrderEvent != null)
                {
                    MyOrderEvent(newOrder);
                }
                return;
            }

            SendLogMessage(result.ToString(), LogMessageType.System);
        }

        /// <summary>
        /// отозвать ордер
        /// </summary>
        public void CanselOrder(Order order)
        {
            string command = ConvertToKillQuikOrder(order);

            int error;
            var msg = new StringBuilder(256);


            if (command == null)
            {
                return;
            }

            Trans2Quik.QuikResult result = Trans2Quik.SEND_ASYNC_TRANSACTION(command, out error, msg, msg.Capacity);

            SendLogMessage(result.ToString(), LogMessageType.System);
        }

        /// <summary>
        /// взять статус ордеров
        /// </summary>
        public void GetOrdersState(List<Order> orders)
        {
           
        }

        /// <summary>
        /// преобразовать ордер Os.Engine в строку выставления ордера Квик
        /// </summary>
        private string ConvertToSimpleQuikOrder(Order order)
        {
            Portfolio myPortfolio = _portfolios.Find(por => por.Number == order.PortfolioNumber);
            Security mySecurity = _securities.Find(sec => sec.Name == order.SecurityNameCode);

            if (myPortfolio == null ||
                mySecurity == null)
            {
                return null;
            }

            var cookie = order.NumberUser;

            var operation = order.Side == Side.Buy ? "B" : "S";

            string price = order.Price.ToString(new CultureInfo("ru-RU"));

            string type;

            if (order.TypeOrder == OrderPriceType.Limit)
            {
                type = "L";
            }
            else
            {
                type = "M";
                price = "0";
            }

            string accaunt;

            string codeClient;

            if (myPortfolio.Number.Split('@').Length == 1)
            {
                accaunt = myPortfolio.Number;
                codeClient = "";
            }
            else
            {
                accaunt = myPortfolio.Number.Split('@')[0];
                codeClient = myPortfolio.Number.Split('@')[1];
            }

            string command;

            if (codeClient == "")
            {
                command =
                    "TRANS_ID=" + cookie +
                    "; ACCOUNT=" + accaunt +
                    // "; CLIENT_CODE=" + ClientCode +
                    "; SECCODE=" + mySecurity.Name +
                    "; CLASSCODE=" + mySecurity.NameClass +
                    "; TYPE=" + type +
                    "; ACTION=NEW_ORDER; OPERATION=" + operation +
                    "; PRICE=" + price +
                    "; QUANTITY=" + order.Volume +
                    ";";
            }
            else
            {
                command =
                    "TRANS_ID=" + cookie +
                    "; ACCOUNT=" + accaunt +
                    "; CLIENT_CODE=" + codeClient +
                    "; SECCODE=" + mySecurity.Name +
                    "; CLASSCODE=" + mySecurity.NameClass +
                    "; TYPE=" + type +
                    "; ACTION=NEW_ORDER; OPERATION=" + operation +
                    "; PRICE=" + price +
                    "; QUANTITY=" + order.Volume +
                    ";";
            }
            return command;
        }

        /// <summary>
        /// преобразовать ордер Os.Engine в строку отзыва ордера Квик
        /// </summary>
        private string ConvertToKillQuikOrder(Order order)
        {
            Portfolio myPortfolio = _portfolios.Find(por => por.Number == order.PortfolioNumber);
            Security mySecurity = _securities.Find(sec => sec.Name == order.SecurityNameCode);

            if (myPortfolio == null ||
                mySecurity == null)
            {
                return null;
            }


            // CLASSCODE = TQBR; SECCODE = RU0009024277; TRANS_ID = 5; ACTION = KILL_ORDER; ORDER_KEY = 503983;

            string accaunt;

            string codeClient;

            if (myPortfolio.Number.Split('@').Length == 1)
            {
                accaunt = myPortfolio.Number;
                codeClient = "";
            }
            else
            {
                accaunt = myPortfolio.Number.Split('@')[0];
                codeClient = myPortfolio.Number.Split('@')[1];
            }

            string command;

            if (codeClient == "")
            {
                command =
                      "TRANS_ID=" + NumberGen.GetNumberOrder(StartProgram.IsOsTrader) +
                      "; ACCOUNT=" + accaunt +
                      "; SECCODE=" + mySecurity.Name +
                      "; CLASSCODE=" + mySecurity.NameClass +
                      "; ACTION=KILL_ORDER" +
                      "; ORDER_KEY=" + order.NumberMarket +
                      ";";
            }
            else
            {
                command =
                      "TRANS_ID=" + NumberGen.GetNumberOrder(StartProgram.IsOsTrader) +
                      "; ACCOUNT=" + accaunt +
                      "; CLIENT_CODE=" + codeClient +
                      "; SECCODE=" + mySecurity.Name +
                      "; CLASSCODE=" + mySecurity.NameClass +
                      "; ACTION=KILL_ORDER" +
                      "; ORDER_KEY=" + order.NumberMarket +
                      ";";

            }

            return command;
        }

// обработка данных из вспомогательных классов

        /// <summary>
        /// ДДЕ сервер
        /// </summary>
        private QuikDde _serverDde;

        // новая моя сделка

        /// <summary>
        /// Апи выслал нам новую Мою Сделку
        /// </summary>
        private void PfnTradeStatusCallback(int nMode, ulong nNumber, ulong nOrderNumber, string classCode,
        string secCode, double dPrice, long nQty, double dValue, int nIsSell, IntPtr pTradeDescriptor)
        {
            try
            {

                MyTrade trade = new MyTrade();
                trade.NumberTrade = nNumber.ToString();
                trade.NumberOrderParent = nOrderNumber.ToString();
                trade.Price = (decimal)dPrice;
                trade.SecurityNameCode = secCode;

                if (ServerTime != DateTime.MinValue)
                {
                    trade.Time = ServerTime;
                }
                else
                {
                    trade.Time = DateTime.Now;
                }

                trade.Volume = Convert.ToInt32(nQty);

                if (_myTrades == null)
                {
                    _myTrades = new List<MyTrade>();
                }

                if (nIsSell == 0)
                {
                    trade.Side = Side.Buy;
                }
                else
                {
                    trade.Side = Side.Sell;
                }

                _myTrades.Add(trade);

                if (MyTradeEvent != null)
                {
                    MyTradeEvent(trade);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// мои сделки
        /// </summary>
        private List<MyTrade> _myTrades;

        /// <summary>
        /// входящие ордера по протоколам Trance2Quik
        /// </summary>
        private void TransactionReplyCallback(int nTransactionResult, int transactionExtendedErrorCode,
           int transactionReplyCode, uint dwTransId, ulong dOrderNum, string transactionReplyMessage, IntPtr pTransReplyDescriptor)
        {
            try
            {
                Order order = GetOrderFromUserId(dwTransId.ToString());

                if (order == null)
                {
                    order = new Order();
                    order.NumberUser = Convert.ToInt32(dwTransId);
                }

                if (dOrderNum != 0)
                {
                    order.NumberMarket = dOrderNum.ToString(new CultureInfo("ru-RU"));
                }

                if (transactionReplyCode == 6)
                {
                    order.State = OrderStateType.Fail;

                    SendLogMessage(dwTransId + " Ошибка выставления заявки " + transactionReplyMessage, LogMessageType.System);

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }
                }
                else
                {
                    if (order.NumberMarket == "0" || dOrderNum == 0)
                    {
                        return;
                    }
                    order.State = OrderStateType.Activ;
                    if (MyOrderEvent  != null)
                    {
                        MyOrderEvent(order);
                    }
                }
            }
            catch (Exception erorr)
            {
                SendLogMessage(erorr.ToString(), LogMessageType.Error);
            }
        }

        // из трансТуКвик пришло оповещение об ордере

        /// <summary>
        /// оповещение об изменившемся ордере
        /// </summary>
        private void PfnOrderStatusCallback(int nMode, int dwTransId, ulong nOrderNum, string classCode, string secCode,
            double dPrice, long l, double dValue, int nIsSell, int nStatus, IntPtr pOrderDescriptor)
        {
            try
            {
                if (dwTransId == 0)
                {
                    return;
                }
                Order order = new Order();

                order.SecurityNameCode = secCode;
                order.ServerType = ServerType.QuikDde;
                order.Price = (decimal)dPrice;
                order.Volume = (int)dValue;
                order.NumberUser = dwTransId;
                order.NumberMarket = nOrderNum.ToString();
                order.SecurityNameCode = secCode;

                Order originOrder = _allOrders.Find(ord => ord.NumberUser == order.NumberUser);

                if (originOrder != null)
                {
                    order.PortfolioNumber = originOrder.PortfolioNumber;
                }

                if (ServerTime != DateTime.MinValue)
                {
                    order.TimeCallBack = ServerTime;
                }
                else
                {
                    order.TimeCallBack = DateTime.Now;
                }

                if (nIsSell == 0)
                {
                    order.Side = Side.Buy;
                }
                else
                {
                    order.Side = Side.Sell;
                }

                if (nStatus == 1)
                {
                    order.State = OrderStateType.Activ;
                }
                else if (nStatus == 2)
                {
                    order.State = OrderStateType.Cancel;
                }
                else
                {
                    order.State = OrderStateType.Done;
                }

                SetOrder(order);

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(order);
                }

                if (_myTrades != null &&
                        _myTrades.Count != 0)
                {
                    List<MyTrade> myTrade =
                        _myTrades.FindAll(trade => trade.NumberOrderParent == order.NumberMarket);

                    for (int tradeNum = 0; tradeNum < myTrade.Count; tradeNum++)
                    {
                        if (MyTradeEvent != null)
                        {
                            MyTradeEvent(myTrade[tradeNum]);
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// сохранить ордер в массив
        /// </summary>
        private void SetOrder(Order newOrder)
        {
            lock (_orderSenderLocker)
            {
                for (int i = 0; i < _allOrders.Count; i++)
                {
                    if (_allOrders[i].NumberUser == newOrder.NumberUser)
                    {
                        _allOrders[i] = newOrder;
                        return;
                    }
                }

                if (_allOrders.Find(ord => ord.NumberUser == newOrder.NumberUser) == null)
                {
                    _allOrders.Add(newOrder);
                }
            }
        }

        /// <summary>
        /// все ордера пришедшие из сервера
        /// </summary>
        private List<Order> _allOrders = new List<Order>();

        /// <summary>
        /// объект блокирующий многопоточный доступ в SetOrder
        /// </summary>
        private object _orderSenderLocker = new object();

        /// <summary>
        /// взять ордер по внутреннему Id
        /// </summary>
        private Order GetOrderFromUserId(string userId)
        {
            return _allOrders.Find(ord => ord.NumberUser.ToString() == userId);
        }

// исходящие события

        /// <summary>
        /// вызывается когда изменился ордер
        /// </summary>
        public event Action<Order> MyOrderEvent;

        /// <summary>
        /// вызывается когда изменился мой трейд
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// появились новые портфели
        /// </summary>
        public event Action<List<Portfolio>> PortfolioEvent;

        /// <summary>
        /// новые бумаги
        /// </summary>
        public event Action<List<Security>> SecurityEvent;

        /// <summary>
        /// новый стакан
        /// </summary>
        public event Action<MarketDepth> MarketDepthEvent;

        /// <summary>
        /// новый трейд
        /// </summary>
        public event Action<Trade> NewTradesEvent;

        /// <summary>
        /// соединение с API установлено
        /// </summary>
        public event Action ConnectEvent;

        /// <summary>
        /// соединение с API разорвано
        /// </summary>
        public event Action DisconnectEvent;

//логирование

        /// <summary>
        /// добавить в лог новое сообщение
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// отправляет исключения
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}
