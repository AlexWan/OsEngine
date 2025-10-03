/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Tester;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Optimizer
{
    public class OptimizerServer : IServer
    {
        #region Service and base settings

        public OptimizerServer(OptimizerDataStorage dataStorage, int num, decimal portfolioStratValue)
        {
            _storagePrime = dataStorage;
            _logMaster = new Log("OptimizerServer", StartProgram.IsOsOptimizer);
            _logMaster.Listen(this);
            _serverConnectStatus = ServerConnectStatus.Disconnect;
            ServerStatus = ServerConnectStatus.Disconnect;
            _testerRegime = TesterRegime.Pause;
            TypeTesterData = dataStorage.TypeTesterData;
            CreatePortfolio(portfolioStratValue);
            NumberServer = num;

            Task.Run(WorkThreadArea);

            _candleManager = new CandleManager(this, StartProgram.IsOsOptimizer);
            _candleManager.CandleUpdateEvent += _candleManager_CandleUpdateEvent;
            _candleManager.LogMessageEvent += SendLogMessage;
            _candleManager.TypeTesterData = dataStorage.TypeTesterData;

            _candleSeriesTesterActivate = new List<SecurityOptimizer>();

            OrdersActive = new List<Order>();
        }

        public int NumberServer;

        public ServerType ServerType
        {
            get { return ServerType.Optimizer; }
        }

        public string ServerNameAndPrefix
        {
            get
            {
                return ServerType.ToString();
            }
        }

        public void ShowDialog(int num = 0)
        {

        }

        public TesterDataType TypeTesterData
        {
            get { return _typeTesterData; }
            set
            {
                if (_typeTesterData == value)
                {
                    return;
                }

                if (_candleManager != null)
                {
                    _candleManager.Clear();
                    _candleManager.TypeTesterData = value;
                }
                _typeTesterData = value;
            }
        }
        private TesterDataType _typeTesterData;

        public void ClearDelete()
        {
            // обозначаем рабочему потоку что надо за собой почистить
            _cleared = true;
        }
        private bool _cleared;

        #endregion

        #region Server status

        public ServerConnectStatus ServerStatus
        {
            get { return _serverConnectStatus; }
            private set
            {
                if (value != _serverConnectStatus)
                {
                    _serverConnectStatus = value;
                    SendLogMessage(_serverConnectStatus + OsLocalization.Market.Message7, LogMessageType.Connect);
                    if (ConnectStatusChangeEvent != null)
                    {
                        ConnectStatusChangeEvent(_serverConnectStatus.ToString());
                    }
                }
            }
        }
        private ServerConnectStatus _serverConnectStatus;

        public event Action<string> ConnectStatusChangeEvent;

        public int CountDaysTickNeedToSave { get; set; }

        public bool NeedToSaveTicks { get; set; }

        #endregion

        #region Server time

        public DateTime ServerTime
        {
            get { return _serverTime; }

            private set
            {
                if (value > _serverTime)
                {
                    _serverTime = value;
                    TimeServerChangeEvent?.Invoke(_serverTime);
                }
            }
        }
        private DateTime _serverTime;

        public event Action<DateTime> TimeServerChangeEvent;

        #endregion

        #region Additional part from standard servers

        public void StartServer() { }

        public void StopServer() { }

        public DateTime LastStartServerTime { get; set; }

        #endregion

        #region Management

        public void TestingStart()
        {
            _serverTime = DateTime.MinValue;

            _lastCheckSessionOrdersTime = DateTime.MinValue;

            _lastCheckDayOrdersTime = DateTime.MinValue;

            TimeNow = _storages[0].TimeStart;

            TimeNow = new DateTime(TimeNow.Year, TimeNow.Month, TimeNow.Day, 0, 0, 0);

            if (TypeTesterData == TesterDataType.TickAllCandleState ||
    TypeTesterData == TesterDataType.TickOnlyReadyCandle)
            {
                _timeAddType = TimeAddInTestType.Second;
            }
            else if (TypeTesterData == TesterDataType.MarketDepthAllCandleState ||
                     TypeTesterData == TesterDataType.MarketDepthOnlyReadyCandle)
            {
                _timeAddType = TimeAddInTestType.Millisecond;
            }
            else if (TypeTesterData == TesterDataType.Candle)
            {

                if (_candleSeriesTesterActivate.Count != 0 &&
                    _candleSeriesTesterActivate[0].TimeFrameSpan.TotalMinutes % 5 == 0)
                {
                    _timeAddType = TimeAddInTestType.FiveMinute;
                }
                else if (_candleSeriesTesterActivate.Find(name => name.TimeFrameSpan < new TimeSpan(0, 0, 1, 0)) == null)
                {
                    _timeAddType = TimeAddInTestType.Minute;
                }
                else
                {
                    _timeAddType = TimeAddInTestType.Second;
                }
            }

            SendLogMessage(OsLocalization.Market.Message35, LogMessageType.System);

            if (TestingStartEvent != null)
            {
                TestingStartEvent();
            }

            _dataIsActive = false;

            _lastTimeStartTest = DateTime.Now;

            _testerRegime = TesterRegime.Play;
            _manualReset.Set();
        }

        public DateTime _lastTimeStartTest = DateTime.MinValue;

        public event Action TestingStartEvent;

        public event Action<int, TimeSpan> TestingEndEvent;

        public event Action<int, int, int> TestingProgressChangeEvent;

        #endregion

        #region Main thread work place

        private TimeAddInTestType _timeAddType;

        private bool _dataIsActive;

        private TesterRegime _testerRegime;

        public AutoResetEvent _manualReset = new AutoResetEvent(true);

        private void WorkThreadArea()
        {
            while (true)
            {
                try
                {
                    if (_cleared)
                    {

                        _storagePrime = null;


                        if (_candleManager != null)
                        {
                            _candleManager.CandleUpdateEvent -= _candleManager_CandleUpdateEvent;
                            _candleManager.LogMessageEvent -= SendLogMessage;
                            _candleManager.Clear();
                            _candleManager.Dispose();
                            _candleManager = null;
                        }

                        if (_manualReset != null)
                        {
                            _manualReset.Set();
                            _manualReset.Dispose();
                            _manualReset = null;
                        }

                        if (_logMaster != null)
                        {
                            _logMaster.Clear();
                            _logMaster.Delete();
                            _logMaster = null;
                        }

                        if (_securities != null)
                        {
                            _securities.Clear();
                            _securities = null;
                        }

                        if (_storages != null)
                        {
                            _storages.Clear();
                            _storages = null;
                        }

                        if (_candleSeriesTesterActivate != null)
                        {
                            for (int i = 0; i < _candleSeriesTesterActivate.Count; i++)
                            {
                                SecurityOptimizer securityOpt = _candleSeriesTesterActivate[i];
                                securityOpt.NewCandleEvent -= TesterServer_NewCandleEvent;
                                securityOpt.NewTradesEvent -= TesterServer_NewTradesEvent;
                                securityOpt.NeedToCheckOrders -= TesterServer_NeedToCheckOrders;
                                securityOpt.NewMarketDepthEvent -= TesterServer_NewMarketDepthEvent;
                                securityOpt.LogMessageEvent -= SendLogMessage;
                                securityOpt.Clear();
                                _candleSeriesTesterActivate[i] = null;
                            }
                            _candleSeriesTesterActivate = null;
                        }

                        if (_allTrades != null &&
                            _allTrades.Length > 0)
                        {
                            for (int i = 0; i < _allTrades.Length; i++)
                            {
                                _allTrades[i].Clear();
                                _allTrades[i] = null;
                            }

                            _allTrades = null;
                        }

                        if (_myTrades != null)
                        {
                            _myTrades.Clear();
                            _myTrades = null;
                        }

                        NonTradePeriods = null;
                        ClearingTimes = null;

                        if (ProfitArray != null)
                        {
                            ProfitArray.Clear();
                            ProfitArray = null;
                        }
                        
                        if(Portfolios != null)
                        {
                            Portfolios.Clear();
                            Portfolios = null;
                        }

                        return;
                    }

                    if (_serverConnectStatus != ServerConnectStatus.Connect)
                    {
                        if (Securities != null && Securities.Count != 0)
                        {
                            ServerStatus = ServerConnectStatus.Connect;
                        }
                    }

                    if (_testerRegime == TesterRegime.Pause)
                    {
                        _manualReset.WaitOne();
                        continue;
                    }


                    if (_testerRegime == TesterRegime.Play)
                    {
                        LoadNextData();
                        CheckOrders();
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    Thread.Sleep(1000);
                }
            }
        }

        private OptimizerDataStorage _storagePrime;

        public List<SecurityTester> SecuritiesTester
        {
            get
            {
                return _storagePrime.SecuritiesTester;
            }
        }

        public List<Security> SecuritiesFromStorage
        {
            get
            {
                return _storagePrime.Securities;
            }
        }

        private List<DataStorage> _storages = new List<DataStorage>();

        public void GetDataToSecurity(Security security, TimeFrame timeFrame, DateTime timeStart, DateTime timeEnd)
        {
            DataStorage newStorage = _storagePrime.GetStorageToSecurity(security, timeFrame, timeStart, timeEnd);

            if (newStorage == null)
            {
                newStorage = _storagePrime.GetStorageToSecurity(security, timeFrame, timeStart, timeEnd);

                if (newStorage == null)
                {
                    SendLogMessage(OsLocalization.Market.Message36, LogMessageType.Error);
                    return;
                }
            }

            if (_storages.Find(s => s.Security.Name == newStorage.Security.Name &&
                                    s.Candles == newStorage.Candles &&
                                    s.TimeFrame == newStorage.TimeFrame &&
                                    s.Trades == newStorage.Trades &&
                                    s.MarketDepths == newStorage.MarketDepths) != null)
            {
                return;
            }

            _storages.Add(newStorage);

            if (_securities.Find(s => s.Name == _storages[_storages.Count - 1].Security.Name) == null)
            {
                _securities.Add(_storages[_storages.Count - 1].Security);
            }

            SecurityOptimizer securityOpt = new SecurityOptimizer();
            securityOpt.Security = security;
            securityOpt.TimeFrame = timeFrame;
            securityOpt.TimeFrameSpan = GetTimeFremeInSpan(timeFrame);
            securityOpt.TimeStart = timeStart;
            securityOpt.TimeEnd = timeEnd;
            securityOpt.RealEndTime = timeEnd.AddDays(1);
            securityOpt.NewCandleEvent += TesterServer_NewCandleEvent;
            securityOpt.NewTradesEvent += TesterServer_NewTradesEvent;
            securityOpt.NeedToCheckOrders += TesterServer_NeedToCheckOrders;
            securityOpt.NewMarketDepthEvent += TesterServer_NewMarketDepthEvent;
            securityOpt.LogMessageEvent += SendLogMessage;

            if (_storages[_storages.Count - 1].StorageType == TesterDataType.Candle)
            {
                securityOpt.DataType = SecurityTesterDataType.Candle;
                securityOpt.Candles = _storages[_storages.Count - 1].Candles;
            }
            else if (_storages[_storages.Count - 1].StorageType == TesterDataType.TickOnlyReadyCandle)
            {
                securityOpt.DataType = SecurityTesterDataType.Tick;
                securityOpt.Trades = _storages[_storages.Count - 1].Trades;
            }
            else if (_storages[_storages.Count - 1].StorageType == TesterDataType.TickAllCandleState)
            {
                securityOpt.DataType = SecurityTesterDataType.Tick;
                securityOpt.Trades = _storages[_storages.Count - 1].Trades;
            }
            else if (_storages[_storages.Count - 1].StorageType == TesterDataType.MarketDepthOnlyReadyCandle)
            {
                securityOpt.DataType = SecurityTesterDataType.MarketDepth;
                securityOpt.MarketDepths = _storages[_storages.Count - 1].MarketDepths;
            }

            _candleSeriesTesterActivate.Add(securityOpt);

            ServerStatus = ServerConnectStatus.Connect;
        }

        public DateTime TimeNow;

        private void LoadNextData()
        {
            for (int i = 0; i < _storages.Count; i++)
            {
                if (TimeNow > _storages[i].TimeEndAddDay)
                {
                    _testerRegime = TesterRegime.Pause;

                    SendLogMessage(OsLocalization.Market.Message37, LogMessageType.System);
                    if (TestingEndEvent != null)
                    {
                        TimeSpan testLiveTime = DateTime.Now - _lastTimeStartTest;

                        TestingEndEvent(NumberServer, testLiveTime);
                    }
                    return;
                }
            }

            if (_candleSeriesTesterActivate == null ||
                _candleSeriesTesterActivate.Count == 0)
            {
                _testerRegime = TesterRegime.Pause;

                SendLogMessage(OsLocalization.Market.Message38,
                    LogMessageType.System);
                if (TestingEndEvent != null)
                {
                    TimeSpan testLiveTime = DateTime.Now - _lastTimeStartTest;

                    TestingEndEvent(NumberServer, testLiveTime);
                }
                return;
            }

            if (_timeAddType == TimeAddInTestType.FiveMinute)
            {
                TimeNow = TimeNow.AddMinutes(5);
            }
            if (_timeAddType == TimeAddInTestType.Minute)
            {
                TimeNow = TimeNow.AddMinutes(1);
            }
            else if (_timeAddType == TimeAddInTestType.Millisecond)
            {
                TimeNow = TimeNow.AddMilliseconds(1);
            }
            else if (_timeAddType == TimeAddInTestType.Second)
            {
                TimeNow = TimeNow.AddSeconds(1);
            }

            bool haveLoadingSec = false;

            for (int i = 0; _candleSeriesTesterActivate != null && i < _candleSeriesTesterActivate.Count; i++)
            {
                if (TimeNow > _candleSeriesTesterActivate[i].RealEndTime)
                {
                    continue;
                }
                haveLoadingSec = true;
                _candleSeriesTesterActivate[i].Load(TimeNow);
            }

            if (haveLoadingSec == false)
            {

            }
        }

        #endregion

        #region Orders 1. Check order execution

        private void CheckOrders()
        {
            if (OrdersActive.Count == 0)
            {
                return;
            }

            CheckRejectOrdersOnClearing(OrdersActive, ServerTime);

            for (int i = 0; i < OrdersActive.Count; i++)
            {

                Order order = OrdersActive[i];
                // check instrument availability on the market / проверяем наличие инструмента на рынке
                SecurityOptimizer security = GetMySecurity(order);

                if (security == null)
                {
                    continue;
                }

                if (security.DataType == SecurityTesterDataType.Tick)
                { // running on ticks / прогон на тиках
                    List<Trade> trades = security.LastTradeSeries;

                    for (int indexTrades = 0; trades != null && indexTrades < trades.Count; indexTrades++)
                    {
                        if (CheckOrdersInTickTest(order, trades[indexTrades], false, security.IsNewDayTrade))
                        {
                            i--;
                            break;
                        }
                    }
                }
                else if (security.DataType == SecurityTesterDataType.Candle)
                { // running on candles / прогон на свечках
                    Candle lastCandle = security.LastCandle;

                    if (order.Price == 0)
                    {
                        order.Price = lastCandle.Open;
                    }

                    if (CheckOrdersInCandleTest(order, lastCandle))
                    {
                        i--;
                    }
                }
                else if (security.DataType == SecurityTesterDataType.MarketDepth)
                {
                    // here / ЗДЕСЬ!!!!!!!!!!!!!!!!!!!!
                    MarketDepth depth = security.LastMarketDepth;

                    if (CheckOrdersInMarketDepthTest(order, depth))
                    {
                        i--;
                    }
                }
            }
        }

        private bool CheckOrdersInCandleTest(Order order, Candle lastCandle)
        {
            decimal minPrice = decimal.MaxValue;
            decimal maxPrice = 0;
            decimal openPrice = 0;
            DateTime time = DateTime.MinValue;

            if (lastCandle != null)
            {
                minPrice = lastCandle.Low;
                maxPrice = lastCandle.High;
                openPrice = lastCandle.Open;
                time = lastCandle.TimeStart;
            }

            if (time <= order.TimeCallBack
                && order.IsStopOrProfit != true)
            {
                //CanselOnBoardOrder(order);
                return false;
            }

            if (order.IsStopOrProfit)
            {
                int slippage = 0;

                if (_slippageToStopOrder > 0)
                {
                    slippage = _slippageToStopOrder;
                }

                decimal realPrice = order.Price;

                if (order.Side == Side.Buy)
                {
                    if (minPrice > realPrice)
                    {
                        realPrice = lastCandle.Open;
                    }
                }
                if (order.Side == Side.Sell)
                {
                    if (maxPrice < realPrice)
                    {
                        realPrice = lastCandle.Open;
                    }
                }

                ExecuteOnBoardOrder(order, realPrice, time, slippage);

                for (int i = 0; i < OrdersActive.Count; i++)
                {
                    if (OrdersActive[i].NumberUser == order.NumberUser)
                    {
                        OrdersActive.RemoveAt(i);
                        break;
                    }
                }

                return true;
            }

            if (order.TypeOrder == OrderPriceType.Market)
            {
                if (order.TimeCreate >= lastCandle.TimeStart)
                {
                    return false;
                }

                decimal realPrice = lastCandle.Open;

                ExecuteOnBoardOrder(order, realPrice, time, 0);

                for (int i = 0; i < OrdersActive.Count; i++)
                {
                    if (OrdersActive[i].NumberUser == order.NumberUser)
                    {
                        OrdersActive.RemoveAt(i);
                        break;
                    }
                }

                return true;
            }


            // check whether the order passed / проверяем, прошёл ли ордер
            if (order.Side == Side.Buy)
            {
                if ((OrderExecutionType == OrderExecutionType.Intersection && order.Price > minPrice)
                    ||
                    (OrderExecutionType == OrderExecutionType.Touch && order.Price >= minPrice)
                    ||
                    (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                    _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Intersection &&
                    order.Price > minPrice)
                    ||
                    (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                    _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch &&
                    order.Price >= minPrice)
                    )
                {// execute / исполняем

                    decimal realPrice = order.Price;

                    if (realPrice > openPrice && order.IsStopOrProfit == false)
                    {
                        // if order is not quotation and put into the market / если заявка не котировачная и выставлена в рынок
                        realPrice = openPrice;
                    }
                    else if (order.IsStopOrProfit && order.Price > maxPrice)
                    {
                        realPrice = maxPrice;
                    }

                    int slippage = 0;

                    if (order.IsStopOrProfit && _slippageToStopOrder > 0)
                    {
                        slippage = _slippageToStopOrder;
                    }
                    else if (order.IsStopOrProfit == false && _slippageToSimpleOrder > 0)
                    {
                        slippage = _slippageToSimpleOrder;
                    }

                    if (realPrice > maxPrice)
                    {
                        realPrice = maxPrice;
                    }

                    ExecuteOnBoardOrder(order, realPrice, time, slippage);

                    for (int i = 0; i < OrdersActive.Count; i++)
                    {
                        if (OrdersActive[i].NumberUser == order.NumberUser)
                        {
                            OrdersActive.RemoveAt(i);
                            break;
                        }
                    }

                    if (OrderExecutionType == OrderExecutionType.FiftyFifty)
                    {
                        if (_lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch)
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Intersection; }
                        else
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Touch; }
                    }

                    return true;
                }
            }
            else if (order.Side == Side.Sell)
            {
                if ((OrderExecutionType == OrderExecutionType.Intersection && order.Price < maxPrice)
                    ||
                    (OrderExecutionType == OrderExecutionType.Touch && order.Price <= maxPrice)
                    ||
                    (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                     _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Intersection &&
                     order.Price < maxPrice)
                    ||
                    (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                     _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch &&
                     order.Price <= maxPrice)
                    )
                {
                    // execute / исполняем
                    decimal realPrice = order.Price;

                    if (realPrice < openPrice && order.IsStopOrProfit == false)
                    {
                        // if order is not quotation and put into the market / если заявка не котировачная и выставлена в рынок
                        realPrice = openPrice;
                    }
                    else if (order.IsStopOrProfit && order.Price < minPrice)
                    {
                        realPrice = minPrice;
                    }

                    int slippage = 0;
                    if (order.IsStopOrProfit && _slippageToStopOrder > 0)
                    {
                        slippage = _slippageToStopOrder;
                    }
                    else if (order.IsStopOrProfit == false && _slippageToSimpleOrder > 0)
                    {
                        slippage = _slippageToSimpleOrder;
                    }

                    if (realPrice < minPrice)
                    {
                        realPrice = minPrice;
                    }

                    ExecuteOnBoardOrder(order, realPrice, time, slippage);

                    for (int i = 0; i < OrdersActive.Count; i++)
                    {
                        if (OrdersActive[i].NumberUser == order.NumberUser)
                        {
                            OrdersActive.RemoveAt(i);
                            break;
                        }
                    }

                    if (OrderExecutionType == OrderExecutionType.FiftyFifty)
                    {
                        if (_lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch)
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Intersection; }
                        else
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Touch; }
                    }

                    return true;
                }
            }

            // order not filled. check if it's time to recall / ордер не `исполнился. проверяем, не пора ли отзывать


            if (order.OrderTypeTime == OrderTypeTime.Specified)
            {
                if (order.TimeCallBack.Add(order.LifeTime) <= ServerTime)
                {
                    CancelOnBoardOrder(order);
                    return true;
                }
            }
            return false;
        }

        private bool CheckOrdersInTickTest(Order order, Trade lastTrade, bool firstTime, bool isNewDay)
        {
            SecurityOptimizer security = _candleSeriesTesterActivate.Find(s => s.Security.Name == order.SecurityNameCode);

            if (security == null)
            {
                return false;
            }

            if (order.IsStopOrProfit)
            {
                int slippage = 0;
                if (_slippageToStopOrder > 0)
                {
                    slippage = _slippageToStopOrder;
                }

                decimal realPrice = order.Price;

                if (isNewDay == true)
                {
                    realPrice = lastTrade.Price;
                }

                ExecuteOnBoardOrder(order, realPrice, lastTrade.Time, slippage);

                for (int i = 0; i < OrdersActive.Count; i++)
                {
                    if (OrdersActive[i].NumberUser == order.NumberUser)
                    {
                        OrdersActive.RemoveAt(i);
                        break;
                    }
                }

                return true;
            }

            if (order.TypeOrder == OrderPriceType.Market)
            {
                if (order.TimeCreate >= lastTrade.Time)
                {
                    return false;
                }

                int slippage = 0;
                if (_slippageToSimpleOrder > 0)
                {
                    slippage = _slippageToSimpleOrder;
                }

                decimal realPrice = lastTrade.Price;

                ExecuteOnBoardOrder(order, realPrice, lastTrade.Time, slippage);

                for (int i = 0; i < OrdersActive.Count; i++)
                {
                    if (OrdersActive[i].NumberUser == order.NumberUser)
                    {
                        OrdersActive.RemoveAt(i);
                        break;
                    }
                }

                return true;
            }

            // check the order / проверяем, прошёл ли ордер
            if (order.Side == Side.Buy)
            {
                if ((OrderExecutionType == OrderExecutionType.Intersection && order.Price > lastTrade.Price)
                   ||
                   (OrderExecutionType == OrderExecutionType.Touch && order.Price >= lastTrade.Price)
                   ||
                   (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                   _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Intersection &&
                   order.Price > lastTrade.Price)
                   ||
                   (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                   _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch &&
                   order.Price >= lastTrade.Price)
                   )
                {// execute / исполняем
                    int slippage = 0;

                    if (order.IsStopOrProfit && _slippageToStopOrder > 0)
                    {
                        slippage = _slippageToStopOrder;
                    }
                    else if (order.IsStopOrProfit == false && _slippageToSimpleOrder > 0)
                    {
                        slippage = _slippageToSimpleOrder;
                    }

                    decimal realPrice = order.Price;

                    if (isNewDay == true)
                    {
                        realPrice = lastTrade.Price;
                    }

                    ExecuteOnBoardOrder(order, realPrice, lastTrade.Time, slippage);

                    for (int i = 0; i < OrdersActive.Count; i++)
                    {
                        if (OrdersActive[i].NumberUser == order.NumberUser)
                        {
                            OrdersActive.RemoveAt(i);
                            break;
                        }
                    }

                    if (OrderExecutionType == OrderExecutionType.FiftyFifty)
                    {
                        if (_lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch)
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Intersection; }
                        else
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Touch; }
                    }

                    return true;
                }
            }

            if (order.Side == Side.Sell)
            {
                if ((OrderExecutionType == OrderExecutionType.Intersection && order.Price < lastTrade.Price)
                   ||
                   (OrderExecutionType == OrderExecutionType.Touch && order.Price <= lastTrade.Price)
                   ||
                   (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                   _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Intersection &&
                   order.Price < lastTrade.Price)
                   ||
                   (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                   _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch &&
                   order.Price <= lastTrade.Price)
                   )
                {// execute / исполняем
                    int slippage = 0;

                    if (order.IsStopOrProfit && _slippageToStopOrder > 0)
                    {
                        slippage = _slippageToStopOrder;
                    }
                    else if (order.IsStopOrProfit == false && _slippageToSimpleOrder > 0)
                    {
                        slippage = _slippageToSimpleOrder;
                    }

                    decimal realPrice = order.Price;

                    if (isNewDay == true)
                    {
                        realPrice = lastTrade.Price;
                    }

                    ExecuteOnBoardOrder(order, realPrice, lastTrade.Time, slippage);

                    for (int i = 0; i < OrdersActive.Count; i++)
                    {
                        if (OrdersActive[i].NumberUser == order.NumberUser)
                        {
                            OrdersActive.RemoveAt(i);
                            break;
                        }
                    }

                    if (OrderExecutionType == OrderExecutionType.FiftyFifty)
                    {
                        if (_lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch)
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Intersection; }
                        else
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Touch; }
                    }

                    return true;
                }
            }

            // order is not executed. check if it's time to recall
            // ордер не исполнился. проверяем, не пора ли отзывать


            if (order.OrderTypeTime == OrderTypeTime.Specified)
            {
                if (order.TimeCallBack.Add(order.LifeTime) <= ServerTime)
                {
                    CancelOnBoardOrder(order);
                    return true;
                }
            }
            return false;
        }

        private bool CheckOrdersInMarketDepthTest(Order order, MarketDepth lastMarketDepth)
        {
            if (lastMarketDepth == null)
            {
                return false;
            }
            decimal sellBestPrice = lastMarketDepth.Asks[0].Price.ToDecimal();
            decimal buyBestPrice = lastMarketDepth.Bids[0].Price.ToDecimal();

            DateTime time = lastMarketDepth.Time;

            if (time <= order.TimeCallBack && !order.IsStopOrProfit)
            {
                // CanselOnBoardOrder(order);
                return false;
            }

            if (order.IsStopOrProfit)
            {
                int slippage = 0;
                if (_slippageToStopOrder > 0)
                {
                    slippage = _slippageToStopOrder;
                }

                decimal realPrice = order.Price;
                ExecuteOnBoardOrder(order, realPrice, time, slippage);

                for (int i = 0; i < OrdersActive.Count; i++)
                {
                    if (OrdersActive[i].NumberUser == order.NumberUser)
                    {
                        OrdersActive.RemoveAt(i);
                        break;
                    }
                }

                return true;
            }

            if (order.TypeOrder == OrderPriceType.Market)
            {
                if (order.TimeCreate >= lastMarketDepth.Time)
                {
                    return false;
                }

                decimal realPrice = 0;

                if (order.Side == Side.Buy)
                {
                    realPrice = sellBestPrice;
                }
                else //if(order.Side == Side.Sell)
                {
                    realPrice = buyBestPrice;
                }

                int slippage = 0;
                if (_slippageToSimpleOrder > 0)
                {
                    slippage = _slippageToSimpleOrder;
                }

                ExecuteOnBoardOrder(order, realPrice, lastMarketDepth.Time, slippage);

                for (int i = 0; i < OrdersActive.Count; i++)
                {
                    if (OrdersActive[i].NumberUser == order.NumberUser)
                    {
                        OrdersActive.RemoveAt(i);
                        break;
                    }
                }

                return true;
            }

            // check the order / проверяем, прошёл ли ордер
            if (order.Side == Side.Buy)
            {
                if ((OrderExecutionType == OrderExecutionType.Intersection && order.Price > buyBestPrice)
                   ||
                   (OrderExecutionType == OrderExecutionType.Touch && order.Price >= buyBestPrice)
                   ||
                   (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                   _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Intersection &&
                   order.Price > buyBestPrice)
                   ||
                   (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                   _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch &&
                   order.Price >= buyBestPrice)
                   )
                {
                    decimal realPrice = order.Price;

                    if (realPrice > sellBestPrice)
                    {
                        realPrice = sellBestPrice;
                    }

                    int slippage = 0;

                    if (order.IsStopOrProfit && _slippageToStopOrder > 0)
                    {
                        slippage = _slippageToStopOrder;
                    }
                    else if (order.IsStopOrProfit == false && _slippageToSimpleOrder > 0)
                    {
                        slippage = _slippageToSimpleOrder;
                    }

                    ExecuteOnBoardOrder(order, realPrice, time, slippage);

                    for (int i = 0; i < OrdersActive.Count; i++)
                    {
                        if (OrdersActive[i].NumberUser == order.NumberUser)
                        {
                            OrdersActive.RemoveAt(i);
                            break;
                        }
                    }

                    if (OrderExecutionType == OrderExecutionType.FiftyFifty)
                    {
                        if (_lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch)
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Intersection; }
                        else
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Touch; }
                    }
                    return true;
                }
            }

            if (order.Side == Side.Sell)
            {
                if ((OrderExecutionType == OrderExecutionType.Intersection && order.Price < sellBestPrice)
                    ||
                    (OrderExecutionType == OrderExecutionType.Touch && order.Price <= sellBestPrice)
                    ||
                    (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                     _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Intersection &&
                     order.Price < sellBestPrice)
                    ||
                    (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                     _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch &&
                     order.Price <= sellBestPrice)
                    )
                {
                    // execute
                    // исполняем
                    decimal realPrice = order.Price;

                    if (realPrice < buyBestPrice)
                    {
                        realPrice = buyBestPrice;
                    }

                    int slippage = 0;

                    if (order.IsStopOrProfit && _slippageToStopOrder > 0)
                    {
                        slippage = _slippageToStopOrder;
                    }
                    else if (order.IsStopOrProfit == false && _slippageToSimpleOrder > 0)
                    {
                        slippage = _slippageToSimpleOrder;
                    }

                    ExecuteOnBoardOrder(order, realPrice, time, slippage);

                    for (int i = 0; i < OrdersActive.Count; i++)
                    {
                        if (OrdersActive[i].NumberUser == order.NumberUser)
                        {
                            OrdersActive.RemoveAt(i);
                            break;
                        }
                    }

                    if (OrderExecutionType == OrderExecutionType.FiftyFifty)
                    {
                        if (_lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch)
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Intersection; }
                        else
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Touch; }
                    }
                    return true;
                }
            }
            // order didn't execute. check if it's time to recall
            // ордер не `исполнился. проверяем, не пора ли отзывать


            if (order.OrderTypeTime == OrderTypeTime.Specified)
            {
                if (order.TimeCallBack.Add(order.LifeTime) <= ServerTime)
                {
                    CancelOnBoardOrder(order);
                    return true;
                }
            }
            return false;
        }

        public OrderExecutionType OrderExecutionType
        {
            get { return _orderExecutionType; }
            set
            {
                _orderExecutionType = value;
            }
        }
        private OrderExecutionType _orderExecutionType;

        private OrderExecutionType _lastOrderExecutionTypeInFiftyFiftyType;

        public int SlippageToSimpleOrder
        {
            get { return _slippageToSimpleOrder; }
            set
            {
                _slippageToSimpleOrder = value;
            }
        }
        private int _slippageToSimpleOrder;

        public int SlippageToStopOrder
        {
            get { return _slippageToStopOrder; }
            set
            {
                _slippageToStopOrder = value;
            }
        }
        private int _slippageToStopOrder;

        #endregion

        #region Orders 2. Work with placing and cancellation of my orders

        private List<Order> OrdersActive;

        private int _iteratorNumbersOrders;

        private int _iteratorNumbersMyTrades;

        public void ExecuteOrder(Order order)
        {
            order.TimeCreate = ServerTime;

            if (order.PositionConditionType == OrderPositionConditionType.Open
                && OrderCanExecuteByNonTradePeriods(order) == false)
            {
                SendLogMessage("No trading period. Open order cancel", LogMessageType.System);
                FailedOperationOrder(order);
                return;
            }

            if (OrdersActive.Count != 0)
            {
                for (int i = 0; i < OrdersActive.Count; i++)
                {
                    if (OrdersActive[i].NumberUser == order.NumberUser)
                    {
                        SendLogMessage(OsLocalization.Market.Message39, LogMessageType.Error);
                        FailedOperationOrder(order);
                        return;
                    }
                }
            }

            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                SendLogMessage(OsLocalization.Market.Message40, LogMessageType.Error);
                FailedOperationOrder(order);
                return;
            }

            if (order.Volume <= 0)
            {
                SendLogMessage(OsLocalization.Market.Message42 + order.Volume, LogMessageType.Error);
                FailedOperationOrder(order);
                return;
            }

            if (string.IsNullOrWhiteSpace(order.PortfolioNumber))
            {
                SendLogMessage(OsLocalization.Market.Message43, LogMessageType.Error);
                FailedOperationOrder(order);
                return;
            }

            if (string.IsNullOrWhiteSpace(order.SecurityNameCode))
            {
                SendLogMessage(OsLocalization.Market.Message44, LogMessageType.Error);
                FailedOperationOrder(order);
                return;
            }

            Order orderOnBoard = new Order();
            orderOnBoard.NumberMarket = _iteratorNumbersOrders++.ToString();
            orderOnBoard.NumberUser = order.NumberUser;
            orderOnBoard.PortfolioNumber = order.PortfolioNumber;
            orderOnBoard.Price = order.Price;
            orderOnBoard.SecurityNameCode = order.SecurityNameCode;
            orderOnBoard.Side = order.Side;
            orderOnBoard.State = OrderStateType.Active;
            orderOnBoard.TimeCallBack = ServerTime;
            orderOnBoard.TimeCreate = ServerTime;
            orderOnBoard.TypeOrder = order.TypeOrder;
            orderOnBoard.Volume = order.Volume;
            orderOnBoard.Comment = order.Comment;
            orderOnBoard.LifeTime = order.LifeTime;
            orderOnBoard.IsStopOrProfit = order.IsStopOrProfit;
            orderOnBoard.TimeFrameInTester = order.TimeFrameInTester;
            orderOnBoard.OrderTypeTime = order.OrderTypeTime;

            OrdersActive.Add(orderOnBoard);

            if (NewOrderIncomeEvent != null)
            {
                NewOrderIncomeEvent(orderOnBoard);
            }

            if (orderOnBoard.IsStopOrProfit)
            {
                if (_candleSeriesTesterActivate == null)
                {
                    return;
                }

                SecurityOptimizer security = GetMySecurity(order);

                if (security.DataType == SecurityTesterDataType.Candle)
                { // testing with using candles / прогон на свечках
                    if (CheckOrdersInCandleTest(orderOnBoard, security.LastCandle))
                    {
                        OrdersActive.Remove(orderOnBoard);
                    }
                }
            }
        }

        private SecurityOptimizer GetMySecurity(Order order)
        {
            SecurityOptimizer security = null;

            if (TypeTesterData == TesterDataType.Candle)
            {
                for (int i = 0; i < _candleSeriesTesterActivate.Count; i++)
                {
                    if (_candleSeriesTesterActivate[i].Security.Name == order.SecurityNameCode
                        && _candleSeriesTesterActivate[i].TimeFrame == order.TimeFrameInTester)
                    {
                        security = _candleSeriesTesterActivate[i];
                        break;
                    }
                }

                if (security == null)
                {
                    security =
                         _candleSeriesTesterActivate.Find(
                             tester =>
                                 tester.Security.Name == order.SecurityNameCode
                                 &&
                                 (tester.LastCandle != null
                                 || tester.LastTradeSeries != null
                                 || tester.LastMarketDepth != null));
                }
            }
            else
            {
                security =
                     _candleSeriesTesterActivate.Find(
                         tester =>
                             tester.Security.Name == order.SecurityNameCode
                             &&
                             (tester.LastCandle != null
                             || tester.LastTradeSeries != null
                             || tester.LastMarketDepth != null));
            }

            return security;
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        public void CancelOrder(Order order)
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                SendLogMessage(OsLocalization.Market.Message45, LogMessageType.Error);
                FailedOperationOrder(order);
                return;
            }

            CancelOnBoardOrder(order);
        }

        public void CancelAllOrders()
        {

        }

        public event Action<Order> NewOrderIncomeEvent;

        public event Action<Order> CancelOrderFailEvent { add { } remove { } }

        #endregion

        #region Orders 3. Internal operations of the "exchange" on orders

        private void CancelOnBoardOrder(Order order)
        {
            Order orderToClose = null;

            if (OrdersActive.Count != 0)
            {
                for (int i = 0; i < OrdersActive.Count; i++)
                {
                    if (OrdersActive[i].NumberUser == order.NumberUser)
                    {
                        orderToClose = OrdersActive[i];
                    }
                }
            }

            if (orderToClose == null)
            {
                SendLogMessage(OsLocalization.Market.Message46, LogMessageType.Error);
                FailedOperationOrder(order);
                return;
            }

            for (int i = 0; i < OrdersActive.Count; i++)
            {
                if (OrdersActive[i].NumberUser == order.NumberUser)
                {
                    OrdersActive.RemoveAt(i);
                    break;
                }
            }

            orderToClose.State = OrderStateType.Cancel;

            if (NewOrderIncomeEvent != null)
            {
                NewOrderIncomeEvent(orderToClose);
            }
        }

        private void FailedOperationOrder(Order order)
        {
            Order orderOnBoard = new Order();
            orderOnBoard.NumberMarket = _iteratorNumbersOrders++.ToString();
            orderOnBoard.NumberUser = order.NumberUser;
            orderOnBoard.PortfolioNumber = order.PortfolioNumber;
            orderOnBoard.Price = order.Price;
            orderOnBoard.SecurityNameCode = order.SecurityNameCode;
            orderOnBoard.Side = order.Side;
            orderOnBoard.State = OrderStateType.Fail;
            orderOnBoard.TimeCallBack = ServerTime;
            orderOnBoard.TimeCreate = order.TimeCreate;
            orderOnBoard.TypeOrder = order.TypeOrder;
            orderOnBoard.Volume = order.Volume;
            orderOnBoard.Comment = order.Comment;

            if (NewOrderIncomeEvent != null)
            {
                NewOrderIncomeEvent(orderOnBoard);
            }
        }

        private void ExecuteOnBoardOrder(Order order, decimal price, DateTime time, int slippage)
        {
            decimal realPrice = price;

            if (order.Volume == order.VolumeExecute ||
                order.State == OrderStateType.Done)
            {
                return;
            }


            if (slippage != 0)
            {
                if (order.Side == Side.Buy)
                {
                    Security mySecurity = GetSecurityForName(order.SecurityNameCode, "");

                    if (mySecurity != null && mySecurity.PriceStep != 0)
                    {
                        realPrice += mySecurity.PriceStep * slippage;
                    }
                }

                if (order.Side == Side.Sell)
                {
                    Security mySecurity = GetSecurityForName(order.SecurityNameCode, "");

                    if (mySecurity != null && mySecurity.PriceStep != 0)
                    {
                        realPrice -= mySecurity.PriceStep * slippage;
                    }
                }
            }

            MyTrade trade = new MyTrade();
            trade.NumberOrderParent = order.NumberMarket;
            trade.NumberTrade = _iteratorNumbersMyTrades++.ToString();
            trade.SecurityNameCode = order.SecurityNameCode;
            trade.Volume = order.Volume;
            trade.Time = time;
            trade.Price = realPrice;
            trade.Side = order.Side;

            MyTradesIncome(trade);

            order.State = OrderStateType.Done;
            order.Price = realPrice;

            if (NewOrderIncomeEvent != null)
            {
                NewOrderIncomeEvent(order);
            }
        }

        #endregion

        #region My trades

        private void MyTradesIncome(MyTrade trade)
        {
            if (_myTrades == null)
            {
                _myTrades = new List<MyTrade>();
            }

            _myTrades.Add(trade);

            if (NewMyTradeEvent != null)
            {
                NewMyTradeEvent(trade);
            }
        }

        public List<MyTrade> MyTrades
        {
            get { return _myTrades; }
        }
        private List<MyTrade> _myTrades;

        public event Action<MyTrade> NewMyTradeEvent;

        #endregion

        #region Clearing system 

        public List<OrderClearing> ClearingTimes = new List<OrderClearing>();

        private DateTime _lastCheckSessionOrdersTime;

        private DateTime _lastCheckDayOrdersTime;

        private void CheckRejectOrdersOnClearing(List<Order> orders, DateTime timeOnMarket)
        {
            if (orders.Count == 0)
            {
                return;
            }

            List<Order> dayLifeOrders = new List<Order>();

            for (int i = 0; i < orders.Count; i++)
            {
                if (orders[i].OrderTypeTime == OrderTypeTime.Day)
                {
                    dayLifeOrders.Add(orders[i]);
                }
            }

            if (ClearingTimes != null
                && ClearingTimes.Count != 0)
            {
                CheckOrderBySessionLife(dayLifeOrders, timeOnMarket);
            }
            else
            {
                CheckOrderByDayLife(dayLifeOrders, timeOnMarket);
            }
        }

        private void CheckOrderBySessionLife(List<Order> orders, DateTime timeOnMarket)
        {
            if (ClearingTimes.Count == 0
                || orders.Count == 0)
            {
                _lastCheckSessionOrdersTime = timeOnMarket;
                return;
            }

            for (int i = 0; i < ClearingTimes.Count; i++)
            {
                if (ClearingTimes[i].IsOn == false)
                {
                    continue;
                }

                if (_lastCheckSessionOrdersTime.TimeOfDay < ClearingTimes[i].Time.TimeOfDay
                    &&
                    timeOnMarket.TimeOfDay >= ClearingTimes[i].Time.TimeOfDay)
                {
                    Order[] ordersToCancel = orders.ToArray();

                    for (int j = 0; j < ordersToCancel.Length; j++)
                    {
                        CancelOnBoardOrder(ordersToCancel[j]);
                    }

                    _lastCheckSessionOrdersTime = timeOnMarket;
                    return;
                }
            }

            _lastCheckSessionOrdersTime = timeOnMarket;
        }

        private void CheckOrderByDayLife(List<Order> orders, DateTime timeOnMarket)
        {
            if (orders.Count == 0)
            {
                _lastCheckDayOrdersTime = timeOnMarket;
                return;
            }

            if (_lastCheckDayOrdersTime == DateTime.MinValue)
            {
                _lastCheckDayOrdersTime = timeOnMarket;
                return;
            }

            if (_lastCheckDayOrdersTime.Date != timeOnMarket.Date)
            {
                Order[] ordersToCancel = orders.ToArray();

                for (int j = 0; j < ordersToCancel.Length; j++)
                {
                    CancelOnBoardOrder(ordersToCancel[j]);
                }

                _lastCheckDayOrdersTime = timeOnMarket;
                return;
            }

            _lastCheckDayOrdersTime = timeOnMarket;
        }

        #endregion

        #region Non-trade periods

        public List<NonTradePeriod> NonTradePeriods = new List<NonTradePeriod>();

        public bool OrderCanExecuteByNonTradePeriods(Order order)
        {
            if (NonTradePeriods == null
                || NonTradePeriods.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < NonTradePeriods.Count; i++)
            {
                if (NonTradePeriods[i].IsOn == false)
                {
                    continue;
                }

                DateTime timeStart = NonTradePeriods[i].DateStart;
                DateTime timeEnd = NonTradePeriods[i].DateEnd;

                if (order.TimeCreate > timeStart
                    && order.TimeCreate < timeEnd)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Profits and losses of exchange

        public List<decimal> ProfitArray = new List<decimal>();

        public void AddProfit(decimal profit)
        {
            Portfolios[0].ValueCurrent += profit;
            ProfitArray.Add(Portfolios[0].ValueCurrent);

            if (NewCurrentValue != null)
            {
                NewCurrentValue(Portfolios[0].ValueCurrent);
            }
        }

        public event Action<decimal> NewCurrentValue;

        #endregion

        #region Portfolios and positions on the exchange

        public List<Portfolio> Portfolios { get; set; }

        private void CreatePortfolio(decimal startValue)
        {
            Portfolio portfolio = new Portfolio();
            portfolio.Number = "GodMode";
            portfolio.ValueBegin = startValue;
            portfolio.ValueBlocked = 0;
            portfolio.ValueCurrent = startValue;
            portfolio.ServerUniqueName = ServerNameAndPrefix;

            if (Portfolios == null)
            {
                Portfolios = new List<Portfolio>();
            }
            Portfolios.Add(portfolio);
        }

        public Portfolio GetPortfolioForName(string name)
        {
            return Portfolios[0];
        }

        public event Action<List<Portfolio>> PortfoliosChangeEvent { add { } remove { } }

        #endregion

        #region Securities

        public List<Security> Securities
        {
            get
            {
                return _securities;
            }
        }
        private List<Security> _securities = new List<Security>();

        public Security GetSecurityForName(string securityName, string securityClass)
        {
            if (_securities == null)
            {
                return null;
            }

            for (int i = 0; i < _securities.Count; i++)
            {
                if (_securities[i].Name == securityName)
                {
                    return _securities[i];
                }
            }

            return null;

            //return _securities.Find(security => security.Name == name);
        }

        private void _candleManager_CandleUpdateEvent(CandleSeries series)
        {
            // write last tick time in the server time / перегружаем последним временем тика время сервера
            ServerTime = series.CandlesAll[series.CandlesAll.Count - 1].TimeStart;

            if (NewCandleIncomeEvent != null)
            {
                NewCandleIncomeEvent(series);
            }
        }

        public event Action<List<Security>> SecuritiesChangeEvent { add { } remove { } }

        public void ShowSecuritiesDialog()
        {
            SecuritiesUi ui = new SecuritiesUi(this);
            ui.ShowDialog();
        }

        #endregion

        #region Subscribe securities to robots

        private List<SecurityOptimizer> _candleSeriesTesterActivate;

        private CandleManager _candleManager;

        private object _starterLocker = new object();

        public CandleSeries StartThisSecurity(string securityName, TimeFrameBuilder timeFrameBuilder, string securityClass)
        {
            lock (_starterLocker)
            {
                if (_cleared)
                {
                    return null;
                }

                if (securityName == "")
                {
                    return null;
                }
                // need to start the server if it is still disabled / надо запустить сервер если он ещё отключен
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    return null;
                }

                if (_securities == null)
                {
                    return null;
                }

                Security security = null;

                for (int i = 0; i < _securities.Count; i++)
                {
                    if (_securities[i].Name == securityName)
                    {
                        security = _securities[i];
                        break;
                    }
                }

                if (security == null)
                {
                    return null;
                }

                // find security / находим бумагу

                if (TypeTesterData == TesterDataType.MarketDepthAllCandleState ||
                    TypeTesterData == TesterDataType.MarketDepthOnlyReadyCandle)
                {
                    timeFrameBuilder.CandleMarketDataType = CandleMarketDataType.MarketDepth;
                }

                if (TypeTesterData == TesterDataType.TickAllCandleState ||
                    TypeTesterData == TesterDataType.TickOnlyReadyCandle)
                {
                    timeFrameBuilder.CandleMarketDataType = CandleMarketDataType.Tick;
                }

                CandleSeries series = new CandleSeries(timeFrameBuilder, security, StartProgram.IsOsOptimizer);

                if (_candleManager != null)
                {
                    _candleManager.StartSeries(series);
                }

                SendLogMessage(OsLocalization.Market.Message14 + series.Security.Name +
                               OsLocalization.Market.Message15 + series.TimeFrame +
                               OsLocalization.Market.Message16, LogMessageType.System);

                return series;
            }
        }

        public List<Candle> GetCandleDataToSecurity(string securityName, string securityClass, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime, bool needToUpdate)
        {
            return null;
        }

        public List<Trade> GetTickDataToSecurity(string securityName, string securityClass,
            DateTime startTime, DateTime endTime, DateTime actualTime, bool needToUpdete)
        {
            return null;
        }

        private TimeSpan GetTimeFremeInSpan(TimeFrame frame)
        {
            TimeSpan result = new TimeSpan(0, 0, 1, 0);

            if (frame == TimeFrame.Day)
            {
                result = new TimeSpan(1, 0, 0, 0);
            }
            if (frame == TimeFrame.Hour1)
            {
                result = new TimeSpan(0, 1, 0, 0);
            }
            if (frame == TimeFrame.Hour2)
            {
                result = new TimeSpan(0, 2, 0, 0);
            }
            if (frame == TimeFrame.Hour4)
            {
                result = new TimeSpan(0, 4, 0, 0);
            }
            if (frame == TimeFrame.Min1)
            {
                result = new TimeSpan(0, 0, 1, 0);
            }
            if (frame == TimeFrame.Min10)
            {
                result = new TimeSpan(0, 0, 10, 0);
            }
            if (frame == TimeFrame.Min15)
            {
                result = new TimeSpan(0, 0, 15, 0);
            }
            if (frame == TimeFrame.Min2)
            {
                result = new TimeSpan(0, 0, 2, 0);
            }
            if (frame == TimeFrame.Min20)
            {
                result = new TimeSpan(0, 0, 20, 0);
            }
            if (frame == TimeFrame.Min30)
            {
                result = new TimeSpan(0, 0, 30, 0);
            }
            if (frame == TimeFrame.Min5)
            {
                result = new TimeSpan(0, 0, 5, 0);
            }
            if (frame == TimeFrame.Sec1)
            {
                result = new TimeSpan(0, 0, 0, 1);
            }
            if (frame == TimeFrame.Sec10)
            {
                result = new TimeSpan(0, 0, 0, 10);
            }
            if (frame == TimeFrame.Sec15)
            {
                result = new TimeSpan(0, 0, 0, 15);
            }
            if (frame == TimeFrame.Sec2)
            {
                result = new TimeSpan(0, 0, 0, 2);
            }
            if (frame == TimeFrame.Sec20)
            {
                result = new TimeSpan(0, 0, 0, 20);
            }
            if (frame == TimeFrame.Sec30)
            {
                result = new TimeSpan(0, 0, 0, 30);
            }
            if (frame == TimeFrame.Sec5)
            {
                result = new TimeSpan(0, 0, 0, 5);
            }

            return result;
        }

        public void StopThisSecurity(CandleSeries series)
        {
            if (series != null && _candleManager != null)
            {
                _candleManager.StopSeries(series);
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<OptionMarketData> NewAdditionalMarketDataEvent { add { } remove { } }

        public event Action<News> NewsEvent { add { } remove { } }

        public event Action NeedToReconnectEvent { add { } remove { } }

        #endregion

        #region Candles

        private void TesterServer_NewCandleEvent(Candle candle, string nameSecurity, TimeSpan timeFrame, int currentCandleCount, int allCandleCount)
        {
            ServerTime = candle.TimeStart;

            if (_dataIsActive == false)
            {
                _dataIsActive = true;
            }

            if (NewBidAskIncomeEvent != null)
            {
                NewBidAskIncomeEvent((decimal)candle.Close, (decimal)candle.Close, GetSecurityForName(nameSecurity, ""));
            }

            _candleManager.SetNewCandleInSeries(candle, nameSecurity, timeFrame);

            if (TestingProgressChangeEvent != null && _lastTimeCountChange.AddMilliseconds(300) < DateTime.Now)
            {
                _lastTimeCountChange = DateTime.Now;
                TestingProgressChangeEvent(currentCandleCount, allCandleCount, NumberServer);
            }
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            return null;
        }

        private DateTime _lastTimeCountChange;

        public event Action<CandleSeries> NewCandleIncomeEvent;

        #endregion

        #region Market depth 

        private void TesterServer_NewMarketDepthEvent(MarketDepth marketDepth, int lastCount, int maxCount)
        {
            if (_dataIsActive == false)
            {
                _dataIsActive = true;
            }

            if (NewMarketDepthEvent != null)
            {
                NewMarketDepthEvent(marketDepth);
            }

            if (TestingProgressChangeEvent != null && _lastTimeCountChange.AddMilliseconds(300) < DateTime.Now)
            {
                _lastTimeCountChange = DateTime.Now;
                TestingProgressChangeEvent(lastCount, maxCount, NumberServer);
            }
        }

        public event Action<decimal, decimal, Security> NewBidAskIncomeEvent;

        public event Action<MarketDepth> NewMarketDepthEvent;

        #endregion

        #region All trades table

        private void TesterServer_NewTradesEvent(List<Trade> tradesNew, int lastCount, int maxCount)
        {
            if (_dataIsActive == false)
            {
                _dataIsActive = true;
            }

            if (tradesNew.Count == 0)
            {
                return;
            }

            List<Trade> fullTradesArrayInServer = null;

            if (_allTrades == null)
            {
                _allTrades = new List<Trade>[1];
                _allTrades[0] = new List<Trade>(tradesNew);
                fullTradesArrayInServer = tradesNew;
            }
            else
            {// sort trades by storages / сортируем сделки по хранилищам

                for (int indTrade = 0; indTrade < tradesNew.Count; indTrade++)
                {
                    Trade trade = tradesNew[indTrade];

                    bool isSave = false;

                    for (int i = 0; i < _allTrades.Length; i++)
                    {
                        if (_allTrades[i] != null && _allTrades[i].Count != 0 &&
                            _allTrades[i][0].SecurityNameCode == trade.SecurityNameCode &&
                            _allTrades[i][0].TimeFrameInTester == trade.TimeFrameInTester)
                        { // if there is already storage for this instrument, save / если для этого инструметна уже есть хранилище, сохраняем и всё
                            isSave = true;
                            if (_allTrades[i][0].Time > trade.Time)
                            {
                                break;
                            }
                            _allTrades[i].Add(trade);
                            fullTradesArrayInServer = _allTrades[i];
                            break;
                        }
                    }

                    if (isSave == false)
                    { // there is no storage for instrument / хранилища для инструмента нет
                        List<Trade>[] allTradesNew = new List<Trade>[_allTrades.Length + 1];
                        for (int i = 0; i < _allTrades.Length; i++)
                        {
                            allTradesNew[i] = _allTrades[i];
                        }
                        allTradesNew[allTradesNew.Length - 1] = new List<Trade>();
                        allTradesNew[allTradesNew.Length - 1].Add(trade);
                        _allTrades = allTradesNew;
                        fullTradesArrayInServer = allTradesNew[allTradesNew.Length - 1];
                    }
                }
            }

            if (tradesNew.Count == 0)
            {
                return;
            }

            ServerTime = tradesNew[tradesNew.Count - 1].Time;

            if (NewTradeEvent != null)
            {
                NewTradeEvent(fullTradesArrayInServer);
            }

            if (maxCount != 0 && TestingProgressChangeEvent != null && _lastTimeCountChange.AddMilliseconds(300) < DateTime.Now)
            {
                _lastTimeCountChange = DateTime.Now;
                TestingProgressChangeEvent(lastCount, maxCount, NumberServer);
            }

            if (NewBidAskIncomeEvent != null)
            {
                NewBidAskIncomeEvent((decimal)tradesNew[tradesNew.Count - 1].Price, (decimal)tradesNew[tradesNew.Count - 1].Price, GetSecurityForName(tradesNew[tradesNew.Count - 1].SecurityNameCode, ""));
            }
        }

        public List<Trade>[] AllTrades { get { return _allTrades; } }
        private List<Trade>[] _allTrades;

        private void TesterServer_NeedToCheckOrders()
        {
            CheckOrders();
        }

        public List<Trade> GetAllTradesToSecurity(Security security)
        {
            for (int i = 0; _allTrades != null && i < _allTrades.Length; i++)
            {
                if (_allTrades[i] != null && _allTrades[i].Count != 0 &&
                    _allTrades[i][0].SecurityNameCode == security.Name)
                {
                    return _allTrades[i];
                }
            }
            return null;
        }

        public event Action<List<Trade>> NewTradeEvent;

        #endregion

        #region Log

        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        private Log _logMaster;

        public event Action<string, LogMessageType> LogMessageEvent;

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }
        public event Action<Funding> NewFundingEvent { add { } remove { } }
        public event Action<SecurityVolumes> NewVolume24hUpdateEvent { add { } remove { } }

        #endregion
    }

    /// <summary>
	/// security in Optimizer. Encapsulate test data and data upload methods.
    /// бумага в оптимизаторе
    /// Инкапсулирует данные для тестирования и методы прогрузки данных
    /// </summary>
    public class SecurityOptimizer
    {
        public SecurityOptimizer()
        {
            /*if (ServerMaster.GetServers() != null &&
                ServerMaster.GetServers()[0] != null)
            {
                ServerMaster.GetServers()[0].NewCandleIncomeEvent += SecurityTester_NewCandleIncomeEvent;
            }*/
        }

        public Security Security;

        public DateTime TimeStart;

        public DateTime TimeEnd;

        public DateTime RealEndTime;

        public SecurityTesterDataType DataType;

        public TimeSpan TimeFrameSpan;

        public TimeFrame TimeFrame;

        // data upload management

        public bool IsActive;

        public void Clear()
        {
            try
            {
                LastCandle = null;
                LastTrade = null;
                LastMarketDepth = null;
                _lastTradeIndexInArray = 0;
                _lastCandleIndex = 0;
                _lastMarketDepthIndex = 0;
                Candles = null;
                Trades = null;
                LastTradeSeries = null;
                MarketDepths = null;
                _tradesId = 0;

                Security = null;
            }
            catch (Exception errror)
            {
                SendLogMessage(errror.ToString(), LogMessageType.Error);
            }
        }

        public void Load(DateTime now)
        {
            try
            {

                if (DataType == SecurityTesterDataType.Tick)
                {
                    CheckTrades(now);
                }
                else if (DataType == SecurityTesterDataType.Candle)
                {
                    CheckCandles(now);
                }
                else if (DataType == SecurityTesterDataType.MarketDepth)
                {
                    CheckMarketDepth(now);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        // parsing candle files

        public List<Candle> Candles;

        public Candle LastCandle
        {
            get { return _lastCandle; }
            set { _lastCandle = value; }
        }
        private Candle _lastCandle;

        private int _lastCandleIndex;

        private void CheckCandles(DateTime now)
        {
            if (now > RealEndTime ||
                now < TimeStart)
            {
                return;
            }

            if (Candles == null)
            {
                return;
            }

            if (_lastCandleIndex >= Candles.Count)
            {
                return;
            }

            if (_lastCandleIndex == 0)
            {
                _lastCandle = null;
            }

            if (LastCandle != null &&
                LastCandle.TimeStart > now)
            {
                return;
            }

            if (LastCandle != null &&
                LastCandle.TimeStart == now)
            {
                Trade[] array = new Trade[4];

                array[0] = (new Trade()
                {
                    Price = LastCandle.Open,
                    Volume = 1,
                    Side = Side.Sell,
                    Time = LastCandle.TimeStart,
                    TimeFrameInTester = TimeFrame,
                    SecurityNameCode = Security.Name,
                    IdInTester = _tradesId++
                });

                array[1] = (new Trade()
                {
                    Price = LastCandle.High,
                    Volume = 1,
                    Side = Side.Buy,
                    Time = LastCandle.TimeStart,
                    TimeFrameInTester = TimeFrame,
                    SecurityNameCode = Security.Name,
                    IdInTester = _tradesId++
                });

                array[2] = (new Trade()
                {
                    Price = LastCandle.Low,
                    Volume = 1,
                    Side = Side.Sell,
                    Time = LastCandle.TimeStart,
                    TimeFrameInTester = TimeFrame,
                    SecurityNameCode = Security.Name,
                    IdInTester = _tradesId++
                });

                array[3] = (new Trade()
                {
                    Price = LastCandle.Close,
                    Volume = 1,
                    Side = Side.Sell,
                    Time = LastCandle.TimeStart,
                    TimeFrameInTester = TimeFrame,
                    SecurityNameCode = Security.Name,
                    IdInTester = _tradesId++
                });

                List<Trade> lastTradesSeries = new List<Trade>(array);

                if (NewTradesEvent != null)
                {
                    NewTradesEvent(lastTradesSeries, 0, 0);
                }

                LastCandle.State = CandleState.Finished;

                if (NewCandleEvent != null)
                {
                    NewCandleEvent(LastCandle, Security.Name, TimeFrameSpan, _lastCandleIndex, Candles.Count);
                }
                return;
            }

            while (LastCandle == null ||
                LastCandle.TimeStart < now)
            {
                if (_lastCandleIndex >= Candles.Count)
                {
                    _lastCandleIndex = Candles.Count - 1;
                    LastCandle = Candles[_lastCandleIndex];
                    LastCandle.State = CandleState.Finished;
                    break;
                }

                LastCandle = Candles[_lastCandleIndex];
                LastCandle.State = CandleState.Finished;
                _lastCandleIndex++;
            }

            if (LastCandle.TimeStart == now)
            {
                Trade[] array = new Trade[4];

                array[0] = (new Trade()
                {
                    Price = LastCandle.Open,
                    Volume = 1,
                    Side = Side.Sell,
                    Time = LastCandle.TimeStart,
                    TimeFrameInTester = TimeFrame,
                    SecurityNameCode = Security.Name,
                    IdInTester = _tradesId++
                });

                array[1] = (new Trade()
                {
                    Price = LastCandle.High,
                    Volume = 1,
                    Side = Side.Buy,
                    Time = LastCandle.TimeStart,
                    TimeFrameInTester = TimeFrame,
                    SecurityNameCode = Security.Name,
                    IdInTester = _tradesId++
                });

                array[2] = (new Trade()
                {
                    Price = LastCandle.Low,
                    Volume = 1,
                    Side = Side.Sell,
                    Time = LastCandle.TimeStart,
                    TimeFrameInTester = TimeFrame,
                    SecurityNameCode = Security.Name,
                    IdInTester = _tradesId++
                });

                array[3] = (new Trade()
                {
                    Price = LastCandle.Close,
                    Volume = 1,
                    Side = Side.Sell,
                    Time = LastCandle.TimeStart,
                    TimeFrameInTester = TimeFrame,
                    SecurityNameCode = Security.Name,
                    IdInTester = _tradesId++
                });

                List<Trade> lastTradesSeries = new List<Trade>(array);

                if (NewTradesEvent != null)
                {
                    NewTradesEvent(lastTradesSeries, 0, 0);
                }

                LastCandle.State = CandleState.Finished;

                if (NewCandleEvent != null)
                {
                    NewCandleEvent(LastCandle, Security.Name, TimeFrameSpan, _lastCandleIndex, Candles.Count);
                }

            }
        }

        public event Action<Candle, string, TimeSpan, int, int> NewCandleEvent;

        public event Action<MarketDepth, int, int> NewMarketDepthEvent;

        // parsing ticks files

        public List<Trade> Trades;

        public Trade LastTrade;

        private int _lastTradeIndexInArray;

        private long _tradesId;

        public List<Trade> LastTradeSeries;

        public bool IsNewDayTrade;

        public DateTime LastTradeTime;

        private void CheckTrades(DateTime now)
        {
            if (now > RealEndTime ||
                now < TimeStart)
            {
                return;
            }

            if (LastTrade != null &&
                LastTrade.Time > now)
            {
                return;
            }

            if (_lastTradeIndexInArray >= Trades.Count)
            {
                return;
            }

            // upload new trades / качаем новый трейд

            if (LastTrade == null)
            {
                LastTrade = Trades[_lastTradeIndexInArray];
                _lastTradeIndexInArray++;
            }

            if (LastTrade.Time > now)
            {
                return;
            }

            // here we have the first trade in the current second / здесь имеем первый трейд в текущей секунде

            List<Trade> lastTradesSeries = new List<Trade>();

            if(LastTrade != null 
                && LastTrade.Time == now)
            {
                lastTradesSeries.Add(LastTrade);
            }

            while (_lastTradeIndexInArray < Trades.Count)
            {
                Trade tradeN = Trades[_lastTradeIndexInArray];
                _lastTradeIndexInArray++;

                if (tradeN.Time == now)
                {
                    lastTradesSeries.Add(tradeN);
                }
                else
                {
                    LastTrade = tradeN;
                    break;
                }
            }

            if (LastTradeTime != DateTime.MinValue
                && lastTradesSeries.Count > 0
                && LastTradeTime.Date < lastTradesSeries[0].Time.Date)
            {
                IsNewDayTrade = true;
            }
            else
            {
                IsNewDayTrade = false;
            }

            LastTradeSeries = lastTradesSeries;

            for (int i = 0; i < lastTradesSeries.Count; i++)
            {
                List<Trade> trades = new List<Trade>() { lastTradesSeries[i] };
                LastTradeSeries = trades;
                NeedToCheckOrders();
                NewTradesEvent(trades, _lastTradeIndexInArray, Trades.Count);
            }

            if (lastTradesSeries.Count > 0)
            {
                LastTradeTime = lastTradesSeries[^1].Time;
            }
        }

        public event Action<List<Trade>, int, int> NewTradesEvent;

        public event Action NeedToCheckOrders;

        // parsing market depths

        public List<MarketDepth> MarketDepths;

        public MarketDepth LastMarketDepth;

        private int _lastMarketDepthIndex;

        private void CheckMarketDepth(DateTime now)
        {
            if (now > RealEndTime ||
                now < TimeStart)
            {
                return;
            }

            if (LastMarketDepth != null &&
                LastMarketDepth.Time > now)
            {
                return;
            }

            if (_lastMarketDepthIndex >= MarketDepths.Count)
            {
                return;
            }

            // if download the first second / качаем первую секунду если 

            if (LastMarketDepth == null)
            {
                LastMarketDepth = MarketDepths[_lastMarketDepthIndex];
                _lastMarketDepthIndex++;

            }

            while (MarketDepths.Count > _lastMarketDepthIndex &&
                   LastMarketDepth.Time < now)
            {
                LastMarketDepth = MarketDepths[_lastMarketDepthIndex];
                _lastMarketDepthIndex++;
            }

            if (LastMarketDepth.Time > now)
            {
                return;
            }

            if (NewMarketDepthEvent != null)
            {
                NewMarketDepthEvent(LastMarketDepth, _lastMarketDepthIndex, MarketDepths.Count);
            }
        }

        // logging

        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

    }
}