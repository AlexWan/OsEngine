using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.Quik
{
    /// <summary>
    /// QUIK DDE server
    /// сервер Квик ДДЕ
    /// </summary>
    public class QuikServer:AServer
    {
        public QuikServer()
        {
            QuikDdeServerRealization realization = new QuikDdeServerRealization();
            ServerRealization = realization;

            CreateParameterPath(OsLocalization.Market.Message82);
            CreateParameterEnum("Number separator", "Dot", new List<string>(){"Dot","Comma"});
        }

        /// <summary>
        /// override method that gives the server state
        /// переопределяем метод отдающий состояние сервера
        /// </summary>
        public override bool IsTimeToServerWork
        {
            get { return ((QuikDdeServerRealization)ServerRealization).ServerInWork; }
        }
    }

    /// <summary>
    /// implementation of QUIK DDE Server
    /// реализация сервера Квик ДДЕ
    /// </summary>
    public class QuikDdeServerRealization : IServerRealization
    {

// service/сервис
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

            _workingTimeSettings = new ServerWorkingTimeSettings()
            {
                StartSessionTime = new TimeSpan(9, 55, 0),
                EndSessionTime = new TimeSpan(23, 50, 0),
                WorkingAtWeekend = false,
                ServerTimeZone = "Russian Standard Time",
            };

            Thread timeHolder = new Thread(SessionTimeHandler);
            timeHolder.IsBackground = true;
            timeHolder.Name = ServerType + "timeManager";
            timeHolder.Start();
        }

        /// <summary>
        /// server type
        /// тип сервера
        /// </summary>
        public ServerType ServerType
        {
            get { return ServerType.QuikDde; }
        }

        public List<IServerParameter> ServerParameters { get; set; }

        public DateTime ServerTime { get; set; }

        private readonly ServerWorkingTimeSettings _workingTimeSettings;

        public bool ServerInWork = true;

        private void SessionTimeHandler()
        {
            while (true)
            {
                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }

                var serverCurrentTime = TimeManager.GetExchangeTime(_workingTimeSettings.ServerTimeZone);

                if (serverCurrentTime.DayOfWeek == DayOfWeek.Saturday ||
                    serverCurrentTime.DayOfWeek == DayOfWeek.Sunday ||
                    serverCurrentTime.TimeOfDay < _workingTimeSettings.StartSessionTime ||
                    serverCurrentTime.TimeOfDay > _workingTimeSettings.EndSessionTime)
                {
                    ServerInWork = false;
                    ServerInWork = true;
                }
                else
                {
                    ServerInWork = true;
                }

                Thread.Sleep(15000);
            }
        }

        // connect / disconnect
        // подключение / отключение

        /// <summary>
        /// start server time
        /// время старта севрера
        /// </summary>
        private DateTime _lastStartServerTime;

        /// <summary>
        /// dispoese objects connected with server
        /// освободить объекты связанные с сервером
        /// </summary>
        public void Dispose()
        {
            if (_serverDde != null && _serverDde.IsRegistered)
            {
                //_serverDde.StopDdeInQuik();
                //_serverDde = null;
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
        /// connect to API
        /// подключиться к Апи
        /// </summary>
        public void Connect()
        {
            try
            {
                _lastStartServerTime = DateTime.Now;

                

                if (string.IsNullOrWhiteSpace(((ServerParameterPath)ServerParameters[0]).Value))
                {
                    SendLogMessage(OsLocalization.Market.Message83, LogMessageType.Error);
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Connect)
                {
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
                        SendLogMessage(OsLocalization.Market.Message84 + error, LogMessageType.Error);
                        return;
                    }

                    result = Trans2Quik.CONNECT(((ServerParameterPath)ServerParameters[0]).Value, out error, msg, msg.Capacity);

                    if (result != Trans2Quik.QuikResult.SUCCESS && result != Trans2Quik.QuikResult.ALREADY_CONNECTED_TO_QUIK)
                    {
                        SendLogMessage(OsLocalization.Market.Message84 + msg, LogMessageType.Error);
                        return;
                    }

                    Trans2Quik.SET_TRANSACTIONS_REPLY_CALLBACK(TransactionReplyCallback, out error, msg,
                         msg.Capacity);

                    result = Trans2Quik.SUBSCRIBE_ORDERS("", "");
                    while (result != Trans2Quik.QuikResult.SUCCESS)
                    {
                        Thread.Sleep(5000);
                        result = Trans2Quik.SUBSCRIBE_ORDERS("", "");
                    }

                    result = Trans2Quik.START_ORDERS((PfnOrderStatusCallback));
                    while (result != Trans2Quik.QuikResult.SUCCESS)
                    {
                        Thread.Sleep(5000);
                        result = Trans2Quik.START_ORDERS((PfnOrderStatusCallback));
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

                    if (!_serverDde.IsRegistered)
                    {
                        _serverDde.StartServer();
                    }
                    else
                    {
                        _ddeStatus = ServerConnectStatus.Connect;
                    }
                }

                if (_serverDde.IsRegistered)
                {
                    _ddeStatus = ServerConnectStatus.Connect;
                }

                Thread.Sleep(1000);

            }
            catch (Exception erorr)
            {
                SendLogMessage(erorr.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// in this place lives thread of a tracking of server readiness
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
        /// DDE server status
        /// статус ДДЕ сервера
        /// </summary>
        private ServerConnectStatus _ddeStatus;

        /// <summary>
        /// Trans2QUIK library status
        /// статус трансТуКвик библиотеки
        /// </summary>
        private ServerConnectStatus _transe2QuikStatus;

        /// <summary>
        /// whether server is connected
        /// подключены ли к серверу
        /// </summary>
        private ServerConnectStatus _connectToBrokerStatus;

        /// <summary>
        /// downloading ticks status
        /// статус подгрузки тиков
        /// </summary>
        private ServerConnectStatus _tradesStatus;

        /// <summary>
        /// main server status. Connect if the top three are also connect
        /// основной статус сервера. Connect если верхние три тоже коннект
        /// </summary>
        private ServerConnectStatus _status;

        /// <summary>
        /// server status
        /// статус сервера
        /// </summary>
        public ServerConnectStatus ServerStatus 
        { get { return _status; } set { _status = value; } }

        /// <summary>
        /// incoming message about changing connect to QUIK state
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
                    SendLogMessage(OsLocalization.Market.Message85 + _transe2QuikStatus, LogMessageType.System);
                }
                if (connectionEvent == Trans2Quik.QuikResult.DLL_DISCONNECTED)
                {
                    _transe2QuikStatus = ServerConnectStatus.Disconnect;
                    SendLogMessage(OsLocalization.Market.Message85 + _transe2QuikStatus, LogMessageType.System);
                }
                if (connectionEvent == Trans2Quik.QuikResult.QUIK_CONNECTED)
                {
                    _connectToBrokerStatus = ServerConnectStatus.Connect;
                    SendLogMessage(OsLocalization.Market.Message86 + ServerConnectStatus.Connect, LogMessageType.System);
                }
                if (connectionEvent == Trans2Quik.QuikResult.QUIK_DISCONNECTED)
                {
                    _connectToBrokerStatus = ServerConnectStatus.Disconnect;
                    SendLogMessage(OsLocalization.Market.Message86 + ServerConnectStatus.Disconnect, LogMessageType.System);
                }
            }
            catch (Exception erorr)
            {
                SendLogMessage(erorr.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// incoming message about changing DDE server state
        /// входящее сообщение об изменении состояния ДДЕ сервера
        /// </summary>
        void _serverDde_StatusChangeEvent(DdeServerStatus status)
        {
            try
            {
                if (status == DdeServerStatus.Connected)
                {
                    _ddeStatus = ServerConnectStatus.Connect;
                    SendLogMessage(OsLocalization.Market.Message87 + _ddeStatus, LogMessageType.System);
                }
                if (status == DdeServerStatus.Disconnected)
                {
                    _ddeStatus = ServerConnectStatus.Disconnect;
                    SendLogMessage(OsLocalization.Market.Message87 + _ddeStatus, LogMessageType.System);
                }
            }
            catch (Exception erorr)
            {
                SendLogMessage(erorr.ToString(), LogMessageType.Error);
            }

        }

        // portfolios / портфели

        /// <summary>
        /// portfolios
        /// портфели
        /// </summary>
        private List<Portfolio> _portfolios;

        /// <summary>
        /// API sent portfolios
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

// trades / трейды
        /// <summary>
        /// got new trades from DDE server
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
                SendLogMessage(OsLocalization.Market.Message88, LogMessageType.System);
            }
        }

// securities / бумаги

        /// <summary>
        /// securities
        /// бумаги
        /// </summary>
        private List<Security> _securities;

        /// <summary>
        /// incoming securities from DDE server
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

// depth / стакан

        /// <summary>
        /// got new depth
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

            // save depths in the storage  /грузим стаканы в хранилище
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
        /// depths
        /// стаканы
        /// </summary>
        private List<MarketDepth> _marketDepths = new List<MarketDepth>();

        // request processing / обработка запросов

        /// <summary>
        /// requests securities from the connector
        /// запрашивает у коннектора бумаги
        /// </summary>
        public void GetSecurities()
        {
     
        }

        /// <summary>
        /// requests portfolios from the connector
        /// запрашивает у коннектора портфели
        /// </summary>
        public void GetPortfolios()
        {
         
        }

        /// <summary>
        /// subscribe to security information
        /// подписаться на информацию по бумаге
        /// </summary>
        /// <param name="security"></param>
        public void Subscrible(Security security)
        {

        }

        /// <summary>
        /// take candle history for period
        /// взять историю свечек за период
        /// </summary>
        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        /// <summary>
        /// take tick data on instrument for period
        /// взять тиковые данные по инструменту за определённый период
        /// </summary>
        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        /// <summary>
        /// send order
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
        /// cancel order
        /// отозвать ордер
        /// </summary>
        public void CancelOrder(Order order)
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
        /// take order status
        /// взять статус ордеров
        /// </summary>
        public void GetOrdersState(List<Order> orders)
        {
           
        }

        /// <summary>
        /// convert Os.Engine order to string for placing in QUIK 
        /// преобразовать ордер Os.Engine в строку выставления ордера Квик
        /// </summary>
        private string ConvertToSimpleQuikOrder(Order order)
        {
            Portfolio myPortfolio = _portfolios.Find(por => por.Number == order.PortfolioNumber ||
                                                            por.Number.StartsWith(order.PortfolioNumber));
            Security mySecurity = _securities.Find(sec => sec.Name == order.SecurityNameCode);

            if (myPortfolio == null ||
                mySecurity == null)
            {
                return null;
            }

            var cookie = order.NumberUser;

            var operation = order.Side == Side.Buy ? "B" : "S";

            string price = order.Price.ToString();

            if (((ServerParameterEnum) ServerParameters[1]).Value == "Dot")
            {
                price = price.Replace(",", ".");
            }
            else
            {
                price = price.Replace(".", ",");
            }

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
        /// convert Os.Engine order to string for canceling in QUIK 
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

// processing data from auxiliary classes
// обработка данных из вспомогательных классов

        /// <summary>
        /// DDE server
        /// ДДЕ сервер
        /// </summary>
        private QuikDde _serverDde;

        // my new trade / новая моя сделка

        /// <summary>
        /// API sent a new my trade
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
        /// my trades
        /// мои сделки
        /// </summary>
        private List<MyTrade> _myTrades;

        /// <summary>
        /// incoming orders by protocol Trance2Quik
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

                    SendLogMessage(dwTransId + OsLocalization.Market.Message89 + transactionReplyMessage, LogMessageType.System);

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

        // from Trance2Quik got an alert about order / из трансТуКвик пришло оповещение об ордере

        /// <summary>
        /// aler about changing order
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
                //order.Volume = (int)dValue;

                Order oldOrder = GetOrderFromUserId(dwTransId.ToString());

                if (oldOrder != null)
                {
                    order.Volume = oldOrder.Volume;
                }

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
                    order.TimeCancel = order.TimeCallBack;
                }
                else
                {
                    order.State = OrderStateType.Done;
                    order.TimeDone = order.TimeCallBack;
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
        /// save order to the array
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
        /// all orders came from the server
        /// все ордера пришедшие из сервера
        /// </summary>
        private List<Order> _allOrders = new List<Order>();

        /// <summary>
        /// multi-threaded access locker in SetOrder
        /// объект блокирующий многопоточный доступ в SetOrder
        /// </summary>
        private object _orderSenderLocker = new object();

        /// <summary>
        /// take order by internal Id
        /// взять ордер по внутреннему Id
        /// </summary>
        private Order GetOrderFromUserId(string userId)
        {
            return _allOrders.Find(ord => ord.NumberUser.ToString() == userId);
        }

// outgoing events
// исходящие события

        /// <summary>
        /// called when order has changed
        /// вызывается когда изменился ордер
        /// </summary>
        public event Action<Order> MyOrderEvent;

        /// <summary>
        /// called when my trade has changed
        /// вызывается когда изменился мой трейд
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// appeared new portfolios
        /// появились новые портфели
        /// </summary>
        public event Action<List<Portfolio>> PortfolioEvent;

        /// <summary>
        /// new securities
        /// новые бумаги
        /// </summary>
        public event Action<List<Security>> SecurityEvent;

        /// <summary>
        /// new depth
        /// новый стакан
        /// </summary>
        public event Action<MarketDepth> MarketDepthEvent;

        /// <summary>
        /// new trade
        /// новый трейд
        /// </summary>
        public event Action<Trade> NewTradesEvent;

        /// <summary>
        /// API connection established
        /// соединение с API установлено
        /// </summary>
        public event Action ConnectEvent;

        /// <summary>
        /// API connection lost
        /// соединение с API разорвано
        /// </summary>
        public event Action DisconnectEvent;

// logging / логирование

        /// <summary>
        /// add a new log message
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
        /// send exeptions
        /// отправляет исключения
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}
