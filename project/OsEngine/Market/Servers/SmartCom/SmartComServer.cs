using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.Transaq;
using SmartCOM4Lib;

namespace OsEngine.Market.Servers.SmartCom
{
    public class SmartComServer : AServer
    {
        public SmartComServer()
        {
            SmartComServerRealization realization = new SmartComServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.Message59, "mxdemo.ittrade.ru");
            CreateParameterString(OsLocalization.Market.Message90, "8443");
            CreateParameterString(OsLocalization.Market.Message63, "");
            CreateParameterPassword(OsLocalization.Market.Message64, "");
        }

        public List<Candle> GetSmartComCandleHistory(string security, TimeSpan timeSpan, int count)
        {
            return ((SmartComServerRealization)ServerRealization).GetSmartComCandleHistory(security, timeSpan, count);
        }
    }

    public class SmartComServerRealization : IServerRealization
    {
        public SmartComServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread orderStatusCheckThread = new Thread(ConnectionCheckerToOrdersThreadArea);
            orderStatusCheckThread.IsBackground = true;
            orderStatusCheckThread.Name = "SmartComOrdersExecutionChekerThread";
            orderStatusCheckThread.Start();
        }

        public ServerType ServerType => ServerType.SmartCom;

        public ServerConnectStatus ServerStatus { get; set; }

        public List<IServerParameter> ServerParameters { get; set; }

        public DateTime ServerTime { get; set; }

        /// <summary>
        /// Сервер СмартКом
        /// </summary>
        public StServerClass SmartServer;

        /// <summary>
        /// блокиратор многопоточного доступа к серверу СмартКом
        /// </summary>
        private object _smartComServerLocker = new object();

        public void Connect()
        {
            if (SmartServer == null)
            {
                SmartServer = new StServerClass();
                SmartServer.ConfigureClient("logLevel=0;maxWorkerThreads=10");

                SmartServer.Connected += SmartServerOnConnected;
                SmartServer.Disconnected += SmartServerOnDisconnected;
                SmartServer.AddSymbol += SmartServerOnAddSymbol;
                SmartServer.AddPortfolio += SmartServerOnAddPortfolio;
                SmartServer.SetPortfolio += SmartServerOnSetPortfolio;
                SmartServer.UpdatePosition += SmartServerOnUpdatePosition;
                SmartServer.AddTick += SmartServerOnAddTick;
                SmartServer.AddTrade += SmartServerOnAddTrade;
                SmartServer.AddBar += SmartServerOnAddBar;
                SmartServer.UpdateBidAsk += SmartServerOnUpdateBidAsk;
                SmartServer.UpdateOrder += SmartServerOnUpdateOrder;
                SmartServer.OrderFailed += SmartServerOnOrderFailed;
                SmartServer.OrderSucceeded += SmartServerOnOrderSucceeded;
                SmartServer.OrderCancelFailed += SmartServerOnOrderCancelFailed;
                SmartServer.OrderCancelSucceeded += SmartServerOnOrderCancelSucceeded;
            }

            string ip = ((ServerParameterString)ServerParameters[0]).Value;
            ushort port = Convert.ToUInt16(((ServerParameterString)ServerParameters[1]).Value);
            string username = ((ServerParameterString)ServerParameters[2]).Value;
            string userpassword = ((ServerParameterPassword)ServerParameters[3]).Value;

            SmartServer.connect(ip, port, username, userpassword);

            Thread.Sleep(10000);
        }

        public void Dispose()
        {
            try
            {
                if (SmartServer != null && SmartServer.IsConnected())
                {
                    SmartServer.disconnect();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            if (SmartServer != null)
            {
                SmartServer.Connected -= SmartServerOnConnected;
                SmartServer.Disconnected -= SmartServerOnDisconnected;
                SmartServer.AddSymbol -= SmartServerOnAddSymbol;
                SmartServer.AddPortfolio -= SmartServerOnAddPortfolio;
                SmartServer.SetPortfolio -= SmartServerOnSetPortfolio;
                SmartServer.UpdatePosition -= SmartServerOnUpdatePosition;
                SmartServer.AddTick -= SmartServerOnAddTick;
                SmartServer.AddTrade -= SmartServerOnAddTrade;
                SmartServer.AddBar -= SmartServerOnAddBar;
                SmartServer.UpdateBidAsk -= SmartServerOnUpdateBidAsk;
                SmartServer.UpdateOrder -= SmartServerOnUpdateOrder;
                SmartServer.OrderFailed -= SmartServerOnOrderFailed;
                SmartServer.OrderSucceeded -= SmartServerOnOrderSucceeded;
                SmartServer.OrderCancelFailed -= SmartServerOnOrderCancelFailed;
                SmartServer.OrderCancelSucceeded -= SmartServerOnOrderCancelSucceeded;
            }

            _startedSecurities = new List<string>();
            _numsSendToExecuteOrders = new List<TransactioinSmartComSendState>();
            _numsSendToCancelOrders = new List<TransactioinSmartComSendState>();
            _numsIncomeExecuteOrders = new List<int>();
            _numsIncomeCancelOrders = new List<int>();

            SmartServer = null;
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        public void GetSecurities()
        {
            SmartServer.GetSymbols();
        }

        public void GetPortfolios()
        {
            SmartServer.GetPrortfolioList();

            if (_portfolios != null)
            {
                for (int i = 0; i < _portfolios.Count; i++)
                {
                    SmartServer.CancelPortfolio(_portfolios[i].Number);
                    SmartServer.ListenPortfolio(_portfolios[i].Number);
                }
            }
        }

        /// <summary>
        /// бумаги уже добавленные на скачивание данных
        /// </summary>
        private List<string> _startedSecurities = new List<string>();

        public void Subscrible(Security security)
        {
            string namePaper = security.Name;

            lock (_smartComServerLocker)
            {
                bool isStarted = false;
                for (int i = 0; i < _startedSecurities.Count; i++)
                {
                    if (_startedSecurities[i] == namePaper)
                    {
                        isStarted = true;
                    }
                }

                if (isStarted == false)
                {
                    SmartServer.ListenBidAsks(namePaper);
                    SmartServer.ListenQuotes(namePaper);
                    SmartServer.ListenTicks(namePaper);
                    _startedSecurities.Add(namePaper);
                }
            }
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            return null;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public void GetOrdersState(List<Order> orders)
        {
            
        }

// работа с ордерами

        /// <summary>
        /// номера ордеров отправленных на исполнение в СмартКом
        /// </summary>
        private List<TransactioinSmartComSendState> _numsSendToExecuteOrders = new List<TransactioinSmartComSendState>();

        /// <summary>
        /// номера ордеров отправленных на отзыв в СмартКом
        /// </summary>
        private List<TransactioinSmartComSendState> _numsSendToCancelOrders = new List<TransactioinSmartComSendState>();

        /// <summary>
        /// номера ордеров на открытие позиций входящих из СмартКом
        /// </summary>
        private List<int> _numsIncomeExecuteOrders = new List<int>();

        /// <summary>
        /// номера ордеров отправленных к отмене входящих из СмартКом
        /// </summary>
        private List<int> _numsIncomeCancelOrders = new List<int>();

        public void SendOrder(Order order)
        {
            StOrder_Action action;
            if (order.Side == Side.Buy)
            {
                action = StOrder_Action.StOrder_Action_Buy;
            }
            else
            {
                action = StOrder_Action.StOrder_Action_Sell;
            }

            StOrder_Type type;

            type = StOrder_Type.StOrder_Type_Limit;


            StOrder_Validity validity = StOrder_Validity.StOrder_Validity_Day;

            double price = Convert.ToDouble(order.Price);
            double volume = Convert.ToDouble(order.Volume);
            int cookie = Convert.ToInt32(order.NumberUser);

            lock (_smartComServerLocker)
            {
                SmartServer.PlaceOrder(order.PortfolioNumber, order.SecurityNameCode, action, type,
                    validity,
                    price, volume, 0, cookie);
            }
            _numsSendToExecuteOrders.Add(new TransactioinSmartComSendState()
            {
                NumTransaction = order.NumberUser,
                TimeSendTransaction = DateTime.Now
            });
        }

        public void CanselOrder(Order order)
        {
            lock (_smartComServerLocker)
            {
                Order realOrder = _ordersWhithId.Find(o => o.NumberUser == order.NumberUser);
                if (realOrder != null)
                {
                    SmartServer.CancelOrder(order.PortfolioNumber, order.SecurityNameCode,
                        realOrder.Comment);
                }
            }
            _numsSendToCancelOrders.Add(new TransactioinSmartComSendState()
            {
                NumTransaction = order.NumberUser,
                TimeSendTransaction = DateTime.Now
            });
        }

        private void SmartServerOnUpdateOrder(string portfolio, string symbol, StOrder_State state, StOrder_Action action, StOrder_Type type, StOrder_Validity validity, double price, double amount, double stop, double filled, DateTime datetime, string orderid, string orderno, int statusMask, int cookie, string description)
        {
            try
            {
                Order order = new Order();
                order.NumberUser = cookie;
                order.NumberMarket = orderno;
                order.SecurityNameCode = symbol;
                order.Price = Convert.ToDecimal(price);
                order.Volume = Convert.ToInt32(amount);
                order.VolumeExecute = Convert.ToInt32(amount) - Convert.ToInt32(filled);
                order.NumberUser = cookie;
                order.Comment = orderid;
                order.PortfolioNumber = portfolio;

                if (_ordersWhithId.Find(o => o.Comment == order.Comment) == null)
                {
                    _ordersWhithId.Add(order);
                }


                if (state == StOrder_State.StOrder_State_Open ||
                    state == StOrder_State.StOrder_State_Submited)
                {
                    order.State = OrderStateType.Activ;
                    order.TimeCallBack = datetime;
                    _numsIncomeExecuteOrders.Add(cookie);
                }
                if (state == StOrder_State.StOrder_State_Pending)
                {
                    order.TimeCallBack = datetime;
                    order.State = OrderStateType.Pending;
                    _numsIncomeExecuteOrders.Add(cookie);
                    return;
                }
                if (state == StOrder_State.StOrder_State_Cancel ||
                    state == StOrder_State.StOrder_State_SystemCancel)
                {
                    order.TimeCancel = datetime;
                    order.State = OrderStateType.Cancel;
                    _numsIncomeCancelOrders.Add(cookie);
                    _numsIncomeExecuteOrders.Add(cookie);
                }
                if (state == StOrder_State.StOrder_State_SystemReject)
                {
                    order.State = OrderStateType.Fail;
                    order.VolumeExecute = 0;
                    order.TimeCancel = datetime;
                    order.TimeCallBack = datetime;
                    _numsIncomeExecuteOrders.Add(cookie);
                    _numsIncomeCancelOrders.Add(cookie);
                }

                if (state == StOrder_State.StOrder_State_Filled)
                {
                    order.VolumeExecute = order.Volume;
                    order.TimeCallBack = datetime;
                    order.TimeDone = datetime;
                    order.State = OrderStateType.Done;
                    _numsIncomeExecuteOrders.Add(cookie);
                }
                if (state == StOrder_State.StOrder_State_Partial)
                {
                    order.State = OrderStateType.Patrial;
                    order.TimeCallBack = datetime;
                    _numsIncomeExecuteOrders.Add(cookie);
                }


                if (action == StOrder_Action.StOrder_Action_Buy ||
                    action == StOrder_Action.StOrder_Action_Cover)
                {
                    order.Side = Side.Buy;
                }
                else
                {
                    order.Side = Side.Sell;
                }

                if (type == StOrder_Type.StOrder_Type_Limit)
                {
                    order.TypeOrder = OrderPriceType.Limit;
                }
                else
                {
                    order.TypeOrder = OrderPriceType.Market;
                }

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(order);
                }

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void SmartServerOnOrderFailed(int cookie, string orderid, string reason)
        {
            try
            {
                Order order = new Order();
                order.NumberUser = cookie;
                order.NumberMarket = orderid;
                order.State = OrderStateType.Fail;
                order.ServerType = ServerType.SmartCom;

                SendLogMessage(order.NumberUser + OsLocalization.Market.Message89 + reason, LogMessageType.Error);

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(order);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void SmartServerOnOrderCancelSucceeded(string orderid)
        {
            SendLogMessage(orderid + OsLocalization.Market.Message91, LogMessageType.System);
        }

        private void SmartServerOnOrderCancelFailed(string orderid)
        {
            SendLogMessage(orderid + OsLocalization.Market.Message92, LogMessageType.Error);
        }

        private void SmartServerOnOrderSucceeded(int cookie, string orderid)
        {
            //SendLogMessage(cookie + " Ордер выставлен успешно", LogMessageType.System);
        }

        private List<Order> _ordersWhithId = new List<Order>();

        /// <summary>
        /// метод для работы потока проверяющего соединение со смартком
        /// анализируя исходящие и входящие ордера
        /// </summary>
        private void ConnectionCheckerToOrdersThreadArea()
        {
            while (true)
            {
                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }

                Thread.Sleep(3000);

                if (_numsSendToExecuteOrders.Count != 0 ||
                    _numsSendToCancelOrders.Count != 0)
                {
                    for (int i = 0; i < _numsSendToExecuteOrders.Count; i++)
                    {
                        if (_numsSendToExecuteOrders[i].TimeSendTransaction.AddSeconds(10) > DateTime.Now)
                        {
                            continue;
                        }

                        bool isInArray = false;

                        for (int i2 = 0; i2 < _numsIncomeExecuteOrders.Count; i2++)
                        {
                            if (_numsSendToExecuteOrders[i].NumTransaction == _numsIncomeExecuteOrders[i2])
                            {
                                isInArray = true;
                                break;
                            }
                        }

                        if (isInArray == false)
                        { // не нашли ответа на транзакцию
                            SendLogMessage(
                                OsLocalization.Market.Message93,
                                LogMessageType.System);
                            Dispose();
                        }
                        else
                        { // нашли ответ на транзакцию
                            _numsSendToExecuteOrders.RemoveAt(i);
                            i--;
                        }
                    }

                    for (int i = 0; i < _numsSendToCancelOrders.Count; i++)
                    {
                        if (_numsSendToCancelOrders[i].TimeSendTransaction.AddSeconds(10) > DateTime.Now)
                        {
                            continue;
                        }

                        bool isInArray = false;
                        for (int i2 = 0; i2 < _numsIncomeCancelOrders.Count; i2++)
                        {
                            if (_numsSendToCancelOrders[i].NumTransaction == _numsIncomeCancelOrders[i2])
                            {
                                isInArray = true;
                                break;
                            }
                        }

                        if (isInArray == false)
                        {
                            SendLogMessage(
                                OsLocalization.Market.Message93,
                                LogMessageType.System);
                            Dispose();
                        }
                        else
                        { // нашли ответ на транзакцию
                            _numsSendToCancelOrders.RemoveAt(i);
                            i--;
                        }
                    }
                }
            }
        }

// свечи. Внутренняя тема смартКом

        /// <summary>
        /// взять свечи по инструменту
        /// </summary>
        /// <param name="security"> короткое название бумаги</param>
        /// <param name="timeSpan">таймФрейм</param>
        /// <param name="count">количество свечек</param>
        /// <returns>в случае неудачи вернётся null</returns>
        public List<Candle> GetSmartComCandleHistory(string security, TimeSpan timeSpan, int count)
        {
            if (timeSpan.TotalMinutes > 60 ||
                timeSpan.TotalMinutes < 1)
            {
                return null;
            }

            StBarInterval tf = StBarInterval.StBarInterval_Quarter;

            if (Convert.ToInt32(timeSpan.TotalMinutes) == 1)
            {
                tf = StBarInterval.StBarInterval_1Min;
            }
            else if (Convert.ToInt32(timeSpan.TotalMinutes) == 5)
            {
                tf = StBarInterval.StBarInterval_5Min;
            }
            else if (Convert.ToInt32(timeSpan.TotalMinutes) == 10)
            {
                tf = StBarInterval.StBarInterval_10Min;
            }
            else if (Convert.ToInt32(timeSpan.TotalMinutes) == 15)
            {
                tf = StBarInterval.StBarInterval_15Min;
            }
            else if (Convert.ToInt32(timeSpan.TotalMinutes) == 30)
            {
                tf = StBarInterval.StBarInterval_30Min;
            }
            else if (Convert.ToInt32(timeSpan.TotalMinutes) == 60)
            {
                tf = StBarInterval.StBarInterval_60Min;
            }

            if (tf == StBarInterval.StBarInterval_Quarter)
            {
                return null;
            }

            _candles = null;

            while (_candles == null)
            {
                lock (_smartComServerLocker)
                {
                    SmartServer.GetBars(security, tf, DateTime.Now.AddHours(6), count);
                }
            }

            return _candles;
        }

        /// <summary>
        /// свечи скаченные из метода GetSmartComCandleHistory
        /// </summary>
        private List<Candle> _candles;

        /// <summary>
        /// входящие из системы свечи
        /// </summary>
        private void SmartServerOnAddBar(int row, int nrows, string symbol, StBarInterval interval, DateTime datetime,
            double open, double high, double low, double close, double volume, double openInt)
        {
            Candle candle = new Candle();
            candle.Volume = Convert.ToInt32(volume);
            candle.Open = Convert.ToDecimal(open);
            candle.High = Convert.ToDecimal(high);
            candle.Low = Convert.ToDecimal(low);
            candle.Close = Convert.ToDecimal(close);

            if (interval == StBarInterval.StBarInterval_1Min) candle.TimeStart = datetime.AddMinutes(-1.0);
            else if (interval == StBarInterval.StBarInterval_5Min) candle.TimeStart = datetime.AddMinutes(-5.0);
            else if (interval == StBarInterval.StBarInterval_10Min) candle.TimeStart = datetime.AddMinutes(-10.0);
            else if (interval == StBarInterval.StBarInterval_15Min) candle.TimeStart = datetime.AddMinutes(-15.0);
            else if (interval == StBarInterval.StBarInterval_30Min) candle.TimeStart = datetime.AddMinutes(-30.0);
            else if (interval == StBarInterval.StBarInterval_60Min) candle.TimeStart = datetime.AddMinutes(-60.0);
            else candle.TimeStart = datetime;

            if (_candles == null || row == 0)
            {
                _candles = new List<Candle>();
            }

            _candles.Add(candle);
        }

// портфели

        private List<Portfolio> _portfolios;

        private object _lockerUpdatePosition = new object();

        private object _lockerSetPortfolio = new object();

        private void SmartServerOnUpdatePosition(string portfolio, string symbol, double avprice, double amount, double planned)
        {
            lock (_lockerUpdatePosition)
            {
                try
                {
                    if (_portfolios == null ||
                        _portfolios.Count == 0)
                    {
                        PositionSmartComSender peredast = new PositionSmartComSender();
                        peredast.Portfolio = portfolio;
                        peredast.Symbol = symbol;
                        peredast.Avprice = avprice;
                        peredast.Amount = amount;
                        peredast.Planned = planned;
                        peredast.PositionEvent += SmartServerOnUpdatePosition;

                        Thread worker = new Thread(peredast.Sand);
                        worker.CurrentCulture = new CultureInfo("ru-RU");
                        worker.IsBackground = true;
                        worker.Start();
                        return;
                    }

                    Portfolio myPortfolio = _portfolios.Find(portfolio1 => portfolio1.Number == portfolio);

                    if (myPortfolio == null)
                    {
                        PositionSmartComSender peredast = new PositionSmartComSender();
                        peredast.Portfolio = portfolio;
                        peredast.Symbol = symbol;
                        peredast.Avprice = avprice;
                        peredast.Amount = amount;
                        peredast.Planned = planned;
                        peredast.PositionEvent += SmartServerOnUpdatePosition;

                        Thread worker = new Thread(peredast.Sand);
                        worker.CurrentCulture = new CultureInfo("ru-RU");
                        worker.IsBackground = true;
                        worker.Start();
                        return;
                    }

                    PositionOnBoard position = new PositionOnBoard();
                    position.PortfolioName = portfolio;
                    position.SecurityNameCode = symbol;
                    position.ValueBegin = Convert.ToInt32(amount);
                    position.ValueCurrent = Convert.ToInt32(amount);
                    position.ValueBlocked = Convert.ToInt32(planned - amount);

                    myPortfolio.SetNewPosition(position);

                    if (PortfolioEvent != null)
                    {
                        PortfolioEvent(_portfolios);
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        private void SmartServerOnSetPortfolio(string portfolio, double cash, double leverage, double comission, double saldo, double liquidationvalue, double initialmargin, double totalassets)
        {
            lock (_lockerSetPortfolio)
            {
                try
                {
                    if (cash == 0 ||
                        saldo == 0)
                    {
                        PortfolioSmartComStateSender peredast = new PortfolioSmartComStateSender();
                        peredast.Portfolio = portfolio;
                        peredast.Cash = cash;
                        peredast.Leverage = leverage;
                        peredast.Comission = comission;
                        peredast.Saldo = saldo;
                        peredast.PortfolioEvent += SmartServerOnSetPortfolio;

                        Thread worker = new Thread(peredast.Sand);
                        worker.CurrentCulture = new CultureInfo("ru-RU");
                        worker.IsBackground = true;
                        worker.Start();

                        return;
                    }

                    if (_portfolios == null ||
                        _portfolios.Count == 0)
                    {
                        PortfolioSmartComStateSender peredast = new PortfolioSmartComStateSender();
                        peredast.Portfolio = portfolio;
                        peredast.Cash = cash;
                        peredast.Leverage = leverage;
                        peredast.Comission = comission;
                        peredast.Saldo = saldo;
                        peredast.PortfolioEvent += SmartServerOnSetPortfolio;

                        Thread worker = new Thread(peredast.Sand);
                        worker.CurrentCulture = new CultureInfo("ru-RU");
                        worker.IsBackground = true;
                        worker.Start();
                        return;
                    }

                    Portfolio myPortfolio = _portfolios.Find(portfolio1 => portfolio1.Number == portfolio);

                    if (myPortfolio == null)
                    {
                        return;
                    }

                    myPortfolio.ValueCurrent = Convert.ToDecimal(cash);
                    myPortfolio.ValueBegin = Convert.ToDecimal(saldo);
                    myPortfolio.ValueBlocked = 0;

                    if (myPortfolio.ValueCurrent != 0)
                    {
                        for (int i = 0; i < _portfolios.Count; i++)
                        {

                            if (_portfolios[i].ValueCurrent == 0)
                            {
                                _portfolios[i].ValueCurrent = Convert.ToDecimal(cash);
                                _portfolios[i].ValueBegin = Convert.ToDecimal(saldo);
                                _portfolios[i].ValueBlocked = 0;
                            }
                        }
                    }

                    if (PortfolioEvent != null)
                    {
                        PortfolioEvent(_portfolios);
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        private void SmartServerOnAddPortfolio(int row, int nrows, string portfolioName, string portfolioexch, StPortfolioStatus portfoliostatus)
        {
            try
            {
                if (_portfolios == null)
                {
                    _portfolios = new List<Portfolio>();
                }

                if (_portfolios.Find(portfolio => portfolio.Number == portfolioName) == null)
                {
                    _portfolios.Add(new Portfolio() { Number = portfolioName });


                }

                if (PortfolioEvent != null)
                {
                    PortfolioEvent(_portfolios);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }


// разбор входящих данных


        private List<MarketDepth> _depths = new List<MarketDepth>();

        private void SmartServerOnUpdateBidAsk(string symbol, int row, int nrows, double bid, double bidsize, double ask, double asksize)
        {
            MarketDepthLevel askOs = new MarketDepthLevel();
            askOs.Bid = Convert.ToDecimal(bidsize);
            askOs.Ask = 0;
            askOs.Price = Convert.ToDecimal(bid);

            MarketDepthLevel bidOs = new MarketDepthLevel();
            bidOs.Ask = Convert.ToDecimal(asksize);
            bidOs.Bid = 0;
            bidOs.Price = Convert.ToDecimal(ask);

            MarketDepth myDepth = _depths.Find(depth => depth.SecurityNameCode == symbol);

            if (myDepth == null)
            {
                myDepth = new MarketDepth();
                myDepth.SecurityNameCode = symbol;
                _depths.Add(myDepth);
            }

            myDepth.Time = ServerTime;

            List<MarketDepthLevel> asks = myDepth.Bids;
            List<MarketDepthLevel> bids = myDepth.Asks;

            if (asks == null || asks.Count != nrows)
            {
                asks = new List<MarketDepthLevel>();
                bids = new List<MarketDepthLevel>();
                for (int i = 0; i < nrows; i++)
                {
                    asks.Add(new MarketDepthLevel());
                    bids.Add(new MarketDepthLevel());
                }
                myDepth.Bids = asks;
                myDepth.Asks = bids;
            }

            asks[row] = askOs;
            bids[row] = bidOs;

            if (MarketDepthEvent != null)
            {
                MarketDepthEvent(myDepth.GetCopy());
            }

        }

        private void SmartServerOnAddTrade(string portfolio, string symbol, string orderid, double price, double amount, DateTime datetime, string tradeno, double value, double accruedint)
        {
            try
            {
                MyTrade trade = new MyTrade();
                trade.NumberTrade = tradeno;
                trade.SecurityNameCode = symbol;
                trade.NumberOrderParent = orderid;
                trade.Price = Convert.ToDecimal(price);
                trade.Volume = Convert.ToInt32(Math.Abs(amount));

                if (amount > 0)
                {
                    trade.Side = Side.Buy;
                }
                else
                {
                    trade.Side = Side.Sell;
                }

                trade.Time = datetime;

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

        private void SmartServerOnAddTick(string symbol, DateTime datetime, double price, double volume, string tradeno, StOrder_Action action)
        {
            Trade trade = new Trade();
            trade.SecurityNameCode = symbol;
            trade.Price = Convert.ToDecimal(price);
            trade.Id = tradeno;
            trade.Time = datetime;
            trade.Volume = Convert.ToInt32(volume);

            if (action == StOrder_Action.StOrder_Action_Buy ||
                action == StOrder_Action.StOrder_Action_Cover)
            {
                trade.Side = Side.Buy;
            }
            if (action == StOrder_Action.StOrder_Action_Sell ||
                action == StOrder_Action.StOrder_Action_Short)
            {
                trade.Side = Side.Sell;
            }

            ServerTime = trade.Time;

            if (NewTradesEvent != null)
            {
                NewTradesEvent(trade);
            }
        }

        private List<Security> _securities = new List<Security>();

        private void SmartServerOnAddSymbol(int row, int nrows, string symbol, 
            string shortName, string longName, string type, int decimals, 
            int lotSize, double punkt, double step, string secExtId, 
            string secExchName, DateTime expiryDate, double daysBeforeExpiry, double strike)
        {
            try
            {
                if (_securities.Find(securiti => securiti.Name == symbol) == null)
                {
                    Security security = new Security();
                    security.Name = symbol;
                    security.NameFull = longName;
                    security.NameClass = type;
                    security.NameId = longName;

                    security.Strike = Convert.ToDecimal(strike);
                    security.Expiration = expiryDate;

                    if (decimals == 5.0)
                    {
                        security.PriceStep = 0.00001m;
                        security.PriceStepCost = 0.00001m;
                    }
                    else if (decimals == 4.0)
                    {
                        security.PriceStep = 0.0001m;
                        security.PriceStepCost = 0.0001m;
                    }
                    else if (decimals == 3.0)
                    {
                        security.PriceStep = 0.001m;
                        security.PriceStepCost = 0.001m;
                    }
                    else if (decimals == 2.0)
                    {
                        security.PriceStep = 0.01m;
                        security.PriceStepCost = 0.01m;
                    }
                    else if (decimals == 1.0)
                    {
                        security.PriceStep = 0.1m;
                        security.PriceStepCost = 0.1m;
                    }
                    else if (decimals == 0)
                    {
                        security.PriceStep = Convert.ToDecimal(step);
                        security.PriceStepCost = Convert.ToDecimal(punkt);
                    }

                    if (type == "FUT")
                    {
                        security.Lot = 1;
                    }
                    else
                    {
                        security.Lot = lotSize;
                    }

                    security.PriceLimitLow = 0;
                    security.PriceLimitHigh = 0;

                    _securities.Add(security);
                }

                if (row+1 < nrows)
                {
                    return;
                }

                if (SecurityEvent != null)
                {
                    SecurityEvent(_securities);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void SmartServerOnDisconnected(string reason)
        {
            if (DisconnectEvent != null)
            {
                DisconnectEvent();
            }
            SendLogMessage("SmartCom disconnect " +reason,LogMessageType.Error);
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        private void SmartServerOnConnected()
        {
            if (ConnectEvent != null)
            {
                ConnectEvent();
            }

            ServerStatus = ServerConnectStatus.Connect;
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

// сообщения для лога

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
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }

    public class PositionSmartComSender
    {
        public string Portfolio;
        public string Symbol;
        public double Avprice;
        public double Amount;
        public double Planned;

        public void Sand()
        {
            Thread.Sleep(5000);
            if (PositionEvent != null)
            {
                PositionEvent(Portfolio, Symbol, Avprice, Amount, Planned);
            }
        }

        public event Action<string, string, double, double, double> PositionEvent;
    }

    public class PortfolioSmartComStateSender
    {
        public string Portfolio;
        public double Cash;
        public double Leverage;
        public double Comission;
        public double Saldo;

        public void Sand()
        {
            Thread.Sleep(5000);
            if (PortfolioEvent != null)
            {
                PortfolioEvent(Portfolio, Cash, Leverage, Comission, Saldo, 0, 0, 0);
            }
        }

        public event Action<string, double, double, double, double, double, double, double> PortfolioEvent;
    }

    /// <summary>
    /// объект для хранения статуса транзакции
    /// в логике проверки ответа от сервера
    /// </summary>
    public class TransactioinSmartComSendState
    {
        /// <summary>
        /// время отправки транзакции
        /// </summary>
        public DateTime TimeSendTransaction;

        /// <summary>
        /// номер транзакции
        /// </summary>
        public int NumTransaction;
    }
}
