/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Connectors;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Market.Servers.Tester
{
    public class TesterServer : IServer
    {
        #region Service and base settings

        private static readonly CultureInfo CultureInfo = CultureInfo.InvariantCulture;

        public TesterServer()
        {
            _portfolios = new List<Portfolio>();
            _logMaster = new Log("TesterServer", StartProgram.IsTester);
            _logMaster.Listen(this);
            _serverConnectStatus = ServerConnectStatus.Disconnect;
            ServerStatus = ServerConnectStatus.Disconnect;
            TesterRegime = TesterRegime.NotActive;
            _slippageToSimpleOrder = 0;
            _slippageToStopOrder = 0;
            StartPortfolio = 1000000;
            TypeTesterData = TesterDataType.Candle;
            Load();
            LoadClearingInfo();
            LoadNonTradePeriods();

            if (_activeSet != null)
            {
                _needToReloadSecurities = true;
            }

            if (_worker == null)
            {
                _worker = new Thread(WorkThreadArea);
                _worker.CurrentCulture = CultureInfo;
                _worker.IsBackground = true;
                _worker.Name = "TesterServerThread";
                _worker.Start();
            }

            _candleManager = new CandleManager(this, StartProgram.IsTester);
            _candleManager.CandleUpdateEvent += _candleManager_CandleUpdateEvent;
            _candleManager.LogMessageEvent += SendLogMessage;
            _candleManager.TypeTesterData = TypeTesterData;

            _candleSeriesTesterActivate = new List<SecurityTester>();

            OrdersActive = new List<Order>();

            CheckSet();
        }

        public ServerType ServerType
        {
            get { return ServerType.Tester; }
        }

        public string ServerNameAndPrefix
        {
            get
            {
                return ServerType.ToString();
            }
        }

        private TesterServerUi _ui;

        public void ShowDialog(int num = 0)
        {
            if (_ui == null)
            {
                _ui = new TesterServerUi(this, _logMaster);
                _ui.Show();
                _ui.Closing += _ui_Closing;
            }
            else
            {
                _ui.Focus();
            }

        }

        public bool GuiIsOpenFullSettings
        {
            get
            {
                return _guiIsOpenFullSettings;
            }
            set
            {
                if (_guiIsOpenFullSettings != value)
                {
                    _guiIsOpenFullSettings = value;
                    Save();
                }
            }
        }
        private bool _guiIsOpenFullSettings;

        private void _ui_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _ui = null;
        }

        public bool RemoveTradesFromMemory
        {
            get
            {
                return _removeTradesFromMemory;
            }
            set
            {
                if (value == _removeTradesFromMemory)
                {
                    return;
                }

                _removeTradesFromMemory = value;
                Save();
            }
        }
        private bool _removeTradesFromMemory;

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
                Save();
                ReloadSecurities();
            }

        }
        private TesterDataType _typeTesterData;

        private void Load()
        {
            if (!File.Exists(@"Engine\" + @"TestServer.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"TestServer.txt"))
                {
                    _activeSet = reader.ReadLine();
                    _slippageToSimpleOrder = Convert.ToInt32(reader.ReadLine());
                    StartPortfolio = reader.ReadLine().ToDecimal();
                    Enum.TryParse(reader.ReadLine(), out _typeTesterData);
                    Enum.TryParse(reader.ReadLine(), out _sourceDataType);
                    _pathToFolder = reader.ReadLine();
                    _slippageToStopOrder = Convert.ToInt32(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out _orderExecutionType);
                    _profitMarketIsOn = Convert.ToBoolean(reader.ReadLine());
                    _guiIsOpenFullSettings = Convert.ToBoolean(reader.ReadLine());
                    _removeTradesFromMemory = Convert.ToBoolean(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"TestServer.txt", false))
                {
                    writer.WriteLine(_activeSet);
                    writer.WriteLine(_slippageToSimpleOrder);
                    writer.WriteLine(StartPortfolio);
                    writer.WriteLine(_typeTesterData);
                    writer.WriteLine(_sourceDataType);
                    writer.WriteLine(_pathToFolder);
                    writer.WriteLine(_slippageToStopOrder);
                    writer.WriteLine(_orderExecutionType);
                    writer.WriteLine(_profitMarketIsOn);
                    writer.WriteLine(_guiIsOpenFullSettings);
                    writer.WriteLine(_removeTradesFromMemory);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public void SaveSecurityTestSettings()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(GetSecurityTestSettingsPath(), false))
                {
                    writer.WriteLine(TimeStart.ToString(CultureInfo));
                    writer.WriteLine(TimeEnd.ToString(CultureInfo));
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public void LoadSecurityTestSettings()
        {
            try
            {
                string pathToSettings = GetSecurityTestSettingsPath();
                if (!File.Exists(pathToSettings))
                {
                    return;
                }

                using (StreamReader reader = new StreamReader(pathToSettings))
                {
                    string timeStart = reader.ReadLine();
                    if (timeStart != null)
                    {
                        TimeStart = Convert.ToDateTime(timeStart, CultureInfo);
                    }
                    string timeEnd = reader.ReadLine();
                    if (timeEnd != null)
                    {
                        TimeEnd = Convert.ToDateTime(timeEnd, CultureInfo);
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private string GetSecurityTestSettingsPath()
        {
            string pathToSettings;

            if (SourceDataType == TesterSourceDataType.Set)
            {
                if (string.IsNullOrWhiteSpace(_activeSet))
                {
                    return "";
                }
                pathToSettings = _activeSet + "\\SecurityTestSettings.txt";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_pathToFolder))
                {
                    return "";
                }
                pathToSettings = _pathToFolder + "\\SecurityTestSettings.txt";
            }

            return pathToSettings;
        }

        private string GetSecuritiesSettingsPath()
        {
            string pathToSettings;

            if (SourceDataType == TesterSourceDataType.Set)
            {
                if (string.IsNullOrWhiteSpace(_activeSet))
                {
                    return "";
                }
                pathToSettings = _activeSet + "\\SecuritiesSettings.txt";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_pathToFolder))
                {
                    return "";
                }
                pathToSettings = _pathToFolder + "\\SecuritiesSettings.txt";
            }

            return pathToSettings;
        }

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
                    DateTime lastTime = _serverTime;
                    _serverTime = value;

                    if (_serverTime != lastTime &&
                        TimeServerChangeEvent != null)
                    {
                        TimeServerChangeEvent(_serverTime);
                    }
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
            try
            {
                if (_lastStartSecurityTime.AddSeconds(5) > DateTime.Now)
                {
                    SendLogMessage(OsLocalization.Market.Message97, LogMessageType.Error);
                    return;
                }

                TesterRegime = TesterRegime.Pause;
                Thread.Sleep(200);
                _serverTime = DateTime.MinValue;
                TestingFastIsActivate = false;

                ServerMaster.ClearOrders();

                SendLogMessage(OsLocalization.Market.Message35, LogMessageType.System);

                if (_candleSeriesTesterActivate != null)
                {
                    for (int i = 0; i < _candleSeriesTesterActivate.Count; i++)
                    {
                        _candleSeriesTesterActivate[i].Clear();
                    }
                }

                _candleSeriesTesterActivate = new List<SecurityTester>();

                int countSeriesInLastTest = _candleManager.ActiveSeriesCount;

                _candleManager.Clear();

                if (NeedToReconnectEvent != null)
                {
                    NeedToReconnectEvent();
                }

                int timeToWaitConnect = 100 + countSeriesInLastTest * 60;

                if (timeToWaitConnect > 10000)
                {
                    timeToWaitConnect = 10000;
                }

                if (timeToWaitConnect < 1000)
                {
                    timeToWaitConnect = 1000;
                }

                Thread.Sleep(timeToWaitConnect);

                _allTrades = null;

                if (TimeStart == DateTime.MinValue)
                {
                    SendLogMessage(OsLocalization.Market.Message47, LogMessageType.System);
                    return;
                }

                TimeNow = new DateTime(TimeStart.Year, TimeStart.Month, TimeStart.Day, 0, 0, 0);

                while (TimeNow.Minute != 0)
                {
                    TimeNow = TimeNow.AddMinutes(-1);
                }

                while (TimeNow.Second != 0)
                {
                    TimeNow = TimeNow.AddSeconds(-1);
                }

                while (TimeNow.Millisecond != 0)
                {
                    TimeNow = TimeNow.AddMilliseconds(-1);
                }

                if (_portfolios != null && _portfolios.Count != 0)
                {
                    _portfolios[0].ValueCurrent = StartPortfolio;
                    _portfolios[0].ValueBegin = StartPortfolio;
                    _portfolios[0].ValueBlocked = 0;
                    _portfolios[0].ClearPositionOnBoard();
                }

                ProfitArray = new List<decimal>();

                _dataIsActive = false;

                NumberGen.ResetToZeroInTester();

                OrdersActive.Clear();

                Thread.Sleep(2000);

                TesterRegime = TesterRegime.Play;

                if (TestingStartEvent != null)
                {
                    try
                    {
                        TestingStartEvent();
                    }
                    catch (Exception ex)
                    {
                        SendLogMessage(ex.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public bool IsAlreadyStarted;

        public void TestingFastOnOff()
        {
            if (TesterRegime == TesterRegime.NotActive)
            {
                return;
            }
            if (_dataIsActive == false)
            {
                return;
            }

            TesterRegime = TesterRegime.Play;

            if (TestingFastIsActivate == false)
            {
                TestingFastIsActivate = true;
            }
            else
            {
                TestingFastIsActivate = false;
            }

            if (TestingFastEvent != null)
            {
                TestingFastEvent();
            }
        }

        public bool TestingFastIsActivate;

        public void TestingPausePlay()
        {
            if (TesterRegime == TesterRegime.NotActive)
            {
                return;
            }
            if (TesterRegime == TesterRegime.Play)
            {
                TesterRegime = TesterRegime.Pause;
            }
            else
            {
                TesterRegime = TesterRegime.Play;
            }
        }

        public void TestingPlusOne()
        {
            if (TesterRegime == TesterRegime.NotActive)
            {
                return;
            }
            TesterRegime = TesterRegime.PlusOne;
        }

        public void ToNextPositionActionTestingFast()
        {
            if (TesterRegime == TesterRegime.NotActive)
            {
                return;
            }
            _waitSomeActionInPosition = true;

            if (TestingFastIsActivate == false)
            {
                TestingFastOnOff();
            }
        }

        private void CheckWaitOrdersRegime()
        {
            if (_waitSomeActionInPosition == true)
            {
                _waitSomeActionInPosition = false;

                if (TestingFastIsActivate == true)
                {
                    TestingFastOnOff();

                }
                TesterRegime = TesterRegime.Pause;
            }
        }

        public void ToDateTimeTestingFast(DateTime timeToGo)
        {
            if (TesterRegime == TesterRegime.NotActive)
            {
                return;
            }
            if (timeToGo < TimeNow)
            {
                return;
            }

            _timeWeAwaitToStopFastRegime = timeToGo;

            if (TestingFastIsActivate == false)
            {
                TestingFastOnOff();
            }
        }

        private void CheckGoTo()
        {
            if (_timeWeAwaitToStopFastRegime != DateTime.MinValue &&
               _timeWeAwaitToStopFastRegime < TimeNow)
            {
                _timeWeAwaitToStopFastRegime = DateTime.MinValue;

                if (TestingFastIsActivate)
                {
                    TestingFastOnOff();
                }

                TesterRegime = TesterRegime.Pause;
            }
        }

        private DateTime _timeWeAwaitToStopFastRegime;

        private bool _waitSomeActionInPosition;

        public event Action TestingStartEvent;

        public event Action TestingFastEvent;

        public event Action TestingEndEvent;

        public event Action TestingNewSecurityEvent;

        #endregion

        #region Main thread work place

        private Thread _worker;

        private void WorkThreadArea()
        {
            Thread.Sleep(2000);

            while (true)
            {
                try
                {
                    if (_serverConnectStatus != ServerConnectStatus.Connect)
                    {
                        if (Securities != null && Securities.Count != 0)
                        {
                            ServerStatus = ServerConnectStatus.Connect;
                        }
                    }

                    if (_serverConnectStatus == ServerConnectStatus.Connect)
                    {
                        if (Securities == null || Securities.Count == 0)
                        {
                            ServerStatus = ServerConnectStatus.Disconnect;
                        }
                    }

                    if (_portfolios.Count == 0)
                    {
                        CreatePortfolio();
                    }

                    if (_needToReloadSecurities)
                    {
                        _needToReloadSecurities = false;
                        TesterRegime = TesterRegime.NotActive;
                        LoadSecurities();
                    }

                    if (TesterRegime == TesterRegime.Pause ||
                        TesterRegime == TesterRegime.NotActive)
                    {
                        Thread.Sleep(500);
                        continue;
                    }

                    if (!_dataIsReady)
                    {

                        SendLogMessage(OsLocalization.Market.Message48, LogMessageType.System);
                        TesterRegime = TesterRegime.NotActive;
                        continue;
                    }


                    if (TesterRegime == TesterRegime.PlusOne)
                    {
                        if (TesterRegime != TesterRegime.Pause)
                        {
                            LoadNextData();
                        }
                        CheckOrders();
                        continue;
                    }
                    if (TesterRegime == TesterRegime.Play)
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

        private TimeAddInTestType _timeAddType;

        private bool _dataIsActive;

        private bool _needToReloadSecurities;

        public TesterRegime TesterRegime
        {
            get { return _testerRegime; }
            set
            {
                if (_testerRegime == value)
                {
                    return;
                }
                _testerRegime = value;

                if (TestRegimeChangeEvent != null)
                {
                    TestRegimeChangeEvent(_testerRegime);
                }
            }
        }
        private TesterRegime _testerRegime;

        public event Action<TesterRegime> TestRegimeChangeEvent;

        private void CreatePortfolio()
        {

            Portfolio portfolio = new Portfolio();
            portfolio.Number = "GodMode";
            portfolio.ValueBegin = 1000000;
            portfolio.ValueBlocked = 0;
            portfolio.ValueCurrent = 1000000;
            portfolio.ServerUniqueName = ServerNameAndPrefix;
            ProfitArray = new List<decimal>();

            _portfolios = new List<Portfolio>();

            UpdatePortfolios(new[] { portfolio }.ToList());
        }

        private void TesterServer_NeedToCheckOrders()
        {
            CheckOrders();
        }

        private TimeSpan GetTimeSpan(StreamReader reader)
        {

            Candle lastCandle = null;

            TimeSpan lastTimeSpan = TimeSpan.MaxValue;

            int counter = 0;

            while (true)
            {
                if (reader.EndOfStream)
                {
                    if (lastTimeSpan != TimeSpan.MaxValue)
                    {
                        return lastTimeSpan;
                    }
                    return TimeSpan.Zero;
                }

                if (lastCandle == null)
                {
                    lastCandle = new Candle();
                    lastCandle.SetCandleFromString(reader.ReadLine());
                    continue;
                }

                var currentCandle = new Candle();
                currentCandle.SetCandleFromString(reader.ReadLine());

                var currentTimeSpan = currentCandle.TimeStart - lastCandle.TimeStart;

                lastCandle = currentCandle;

                if (currentTimeSpan < lastTimeSpan)
                {
                    lastTimeSpan = currentTimeSpan;
                    continue;
                }

                if (currentTimeSpan == lastTimeSpan)
                {
                    counter++;
                }

                if (counter >= 100)
                {
                    return lastTimeSpan;
                }
            }
        }

        private void LoadNextData()
        {
            if (TimeNow > TimeEnd)
            {
                TesterRegime = TesterRegime.Pause;

                SendLogMessage(OsLocalization.Market.Message37, LogMessageType.System);
                if (TestingEndEvent != null)
                {
                    TestingEndEvent();
                }
                return;
            }

            if (_candleSeriesTesterActivate == null ||
                _candleSeriesTesterActivate.Count == 0)
            {
                TesterRegime = TesterRegime.Pause;

                SendLogMessage(OsLocalization.Market.Message38,
                    LogMessageType.System);
                if (TestingEndEvent != null)
                {
                    TestingEndEvent();
                }
                return;
            }

            if (_dataIsActive == false)
            {
                TimeNow = TimeNow.AddSeconds(1);
            }
            else if (_timeAddType == TimeAddInTestType.Millisecond)
            {
                TimeNow = TimeNow.AddMilliseconds(1);
            }
            else if (_timeAddType == TimeAddInTestType.Second)
            {
                TimeNow = TimeNow.AddSeconds(1);
            }
            else if (_timeAddType == TimeAddInTestType.Minute)
            {
                TimeNow = TimeNow.AddMinutes(1);
            }

            CheckGoTo();

            //_waitSomeActionInPosition;


            for (int i = 0; _candleSeriesTesterActivate != null && i < _candleSeriesTesterActivate.Count; i++)
            {
                _candleSeriesTesterActivate[i].Load(TimeNow);
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
                // check availability of securities on the market / проверяем наличие инструмента на рынке

                SecurityTester security = null;

                if (order.MySecurityInTester != null)
                {
                    security = order.MySecurityInTester;
                }
                else
                {
                    security = GetMySecurity(order);
                    order.MySecurityInTester = security;
                }

                if (security == null)
                {
                    continue;
                }

                if (security.DataType == SecurityTesterDataType.Tick)
                { // test with using ticks / прогон на тиках

                    List<Trade> lastTrades = security.LastTradeSeries;

                    if (lastTrades != null
                        && lastTrades.Count != 0
                        && CheckOrdersInTickTest(order, lastTrades[lastTrades.Count - 1], false, security.IsNewDayTrade))
                    {
                        i--;
                        break;
                    }
                }
                else if (security.DataType == SecurityTesterDataType.Candle)
                { // test with using candles / прогон на свечках
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
                    // HERE!!!!!!!!!!!! / ЗДЕСЬ!!!!!!!!!!!!!!!!!!!!
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
            DateTime time = ServerTime;

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
                    else if (order.IsStopOrProfit
                        && order.Price > maxPrice)
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

            if (order.Side == Side.Sell)
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

            // order didn't execute. check if it's time to recall / ордер не `исполнился. проверяем, не пора ли отзывать

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
            SecurityTester security = null;

            if (order.MySecurityInTester != null)
            {
                security = order.MySecurityInTester;
            }
            else
            {
                security = SecuritiesTester.Find(tester => tester.Security.Name == order.SecurityNameCode);
                order.MySecurityInTester = security;
            }

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
                if (order.TimeCreate > lastTrade.Time)
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

            // check whether the order passed/проверяем, прошёл ли ордер
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
                {// execute/исполняем
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
                {// execute/исполняем
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

            // order is not executed. check if it's time to recall / ордер не исполнился. проверяем, не пора ли отзывать

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

            // check whether the order passed / проверяем, прошёл ли ордер
            if (order.Side == Side.Buy)
            {
                if ((OrderExecutionType == OrderExecutionType.Intersection && order.Price > sellBestPrice)
                   ||
                   (OrderExecutionType == OrderExecutionType.Touch && order.Price >= sellBestPrice)
                   ||
                   (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                   _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Intersection &&
                   order.Price > sellBestPrice)
                   ||
                   (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                   _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch &&
                   order.Price >= sellBestPrice)
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
                if ((OrderExecutionType == OrderExecutionType.Intersection && order.Price < buyBestPrice)
                    ||
                    (OrderExecutionType == OrderExecutionType.Touch && order.Price <= buyBestPrice)
                    ||
                    (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                     _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Intersection &&
                     order.Price < buyBestPrice)
                    ||
                    (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                     _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch &&
                     order.Price <= buyBestPrice)
                    )
                {
                    // execute / исполняем
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

            // order didn't execute. check if it's time to recall / ордер не `исполнился. проверяем, не пора ли отзывать

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
                Save();
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
                Save();
            }
        }
        private int _slippageToSimpleOrder;

        public int SlippageToStopOrder
        {
            get { return _slippageToStopOrder; }
            set
            {
                _slippageToStopOrder = value;
                Save();
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
            orderOnBoard.ServerType = order.ServerType;
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
                SecurityTester security = null;

                if (order.MySecurityInTester != null)
                {
                    security = order.MySecurityInTester;
                }
                else
                {
                    security = GetMySecurity(order);
                    order.MySecurityInTester = security;
                }

                if (security.DataType == SecurityTesterDataType.Candle)
                { // testing with using candles / прогон на свечках
                    if (CheckOrdersInCandleTest(orderOnBoard, security.LastCandle))
                    {
                        OrdersActive.Remove(orderOnBoard);
                    }
                }
                else if (security.DataType == SecurityTesterDataType.Tick)
                {
                    if (CheckOrdersInTickTest(orderOnBoard, security.LastTrade, true, security.IsNewDayTrade))
                    {
                        OrdersActive.Remove(orderOnBoard);
                    }
                }
            }
        }

        private SecurityTester GetMySecurity(Order order)
        {
            SecurityTester security = null;

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
            orderOnBoard.ServerType = order.ServerType;

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

            ChangePosition(order);

            CheckWaitOrdersRegime();
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

        private List<MyTrade> _myTrades;

        public List<MyTrade> MyTrades
        {
            get { return _myTrades; }
        }

        public event Action<MyTrade> NewMyTradeEvent;

        #endregion

        #region Clearing system 

        public List<OrderClearing> ClearingTimes = new List<OrderClearing>();

        public void SaveClearingInfo()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"TestServerClearings.txt", false))
                {
                    for (int i = 0; i < ClearingTimes.Count; i++)
                    {
                        writer.WriteLine(ClearingTimes[i].GetSaveString());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void LoadClearingInfo()
        {
            if (!File.Exists(@"Engine\" + @"TestServerClearings.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"TestServerClearings.txt"))
                {
                    while (reader.EndOfStream == false)
                    {
                        string str = reader.ReadLine();

                        if (str != "")
                        {
                            OrderClearing clearings = new OrderClearing();
                            clearings.SetFromString(str);
                            ClearingTimes.Add(clearings);
                        }
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public void CreateNewClearing()
        {
            OrderClearing newClearing = new OrderClearing();

            newClearing.Time = new DateTime(2000, 1, 1, 19, 0, 0);
            ClearingTimes.Add(newClearing);
            SaveClearingInfo();
        }

        public void RemoveClearing(int num)
        {
            if (num > ClearingTimes.Count)
            {
                return;
            }

            ClearingTimes.RemoveAt(num);
            SaveClearingInfo();
        }

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

            if (ClearingTimes.Count != 0)
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

        public void SaveNonTradePeriods()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"TestServerNonTradePeriods.txt", false))
                {
                    for (int i = 0; i < NonTradePeriods.Count; i++)
                    {
                        writer.WriteLine(NonTradePeriods[i].GetSaveString());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void LoadNonTradePeriods()
        {
            if (!File.Exists(@"Engine\" + @"TestServerNonTradePeriods.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"TestServerNonTradePeriods.txt"))
                {
                    while (reader.EndOfStream == false)
                    {
                        string str = reader.ReadLine();

                        if (str != "")
                        {
                            NonTradePeriod period = new NonTradePeriod();
                            period.SetFromString(str);
                            NonTradePeriods.Add(period);
                        }
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public void CreateNewNonTradePeriod()
        {
            NonTradePeriod newClearing = new NonTradePeriod();

            NonTradePeriods.Add(newClearing);
            SaveNonTradePeriods();
        }

        public void RemoveNonTradePeriod(int num)
        {
            if (num > NonTradePeriods.Count)
            {
                return;
            }

            NonTradePeriods.RemoveAt(num);
            SaveNonTradePeriods();
        }

        public bool OrderCanExecuteByNonTradePeriods(Order order)
        {
            if (NonTradePeriods.Count == 0)
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

        public List<decimal> ProfitArray;

        public bool ProfitMarketIsOn
        {
            get { return _profitMarketIsOn; }
            set
            {
                _profitMarketIsOn = value;
                Save();
            }
        }
        private bool _profitMarketIsOn = true;

        public void AddProfit(decimal profit)
        {
            if (_profitMarketIsOn == false)
            {
                return;
            }
            _portfolios[0].ValueCurrent += profit;
            ProfitArray.Add(_portfolios[0].ValueCurrent);

            if (NewCurrentValue != null)
            {
                NewCurrentValue(_portfolios[0].ValueCurrent);
            }

            if (PortfoliosChangeEvent != null)
            {
                PortfoliosChangeEvent(_portfolios);
            }

        }

        public event Action<decimal> NewCurrentValue;

        #endregion

        #region Portfolios and positions on the exchange

        public decimal StartPortfolio
        {
            set
            {
                if (_startPortfolio == value)
                {
                    return;
                }
                _startPortfolio = value;

                if (_portfolios != null && _portfolios.Count != 0)
                {
                    _portfolios[0].ValueCurrent = StartPortfolio;
                    _portfolios[0].ValueBegin = StartPortfolio;
                    _portfolios[0].ValueBlocked = 0;

                    if (PortfoliosChangeEvent != null)
                    {
                        PortfoliosChangeEvent(_portfolios);
                    }
                }
            }
            get { return _startPortfolio; }
        }
        private decimal _startPortfolio;

        public List<Portfolio> Portfolios
        {
            get { return _portfolios; }
        }
        private List<Portfolio> _portfolios;

        private void ChangePosition(Order orderExecute)
        {
            List<PositionOnBoard> positions = _portfolios[0].GetPositionOnBoard();

            if (positions == null ||
                orderExecute == null)
            {
                return;
            }

            PositionOnBoard myPositioin =
                positions.Find(board => board.SecurityNameCode == orderExecute.SecurityNameCode);

            if (myPositioin == null)
            {
                myPositioin = new PositionOnBoard();
                myPositioin.SecurityNameCode = orderExecute.SecurityNameCode;
                myPositioin.PortfolioName = orderExecute.PortfolioNumber;
                myPositioin.ValueBegin = 0;
            }

            if (orderExecute.Side == Side.Buy)
            {
                myPositioin.ValueCurrent += orderExecute.Volume;
            }

            if (orderExecute.Side == Side.Sell)
            {
                myPositioin.ValueCurrent -= orderExecute.Volume;
            }

            _portfolios[0].SetNewPosition(myPositioin);

            if (PortfoliosChangeEvent != null)
            {
                PortfoliosChangeEvent(_portfolios);
            }
        }

        private void UpdatePortfolios(List<Portfolio> portfoliosNew)
        {

            _portfolios = portfoliosNew;

            if (PortfoliosChangeEvent != null)
            {
                PortfoliosChangeEvent(_portfolios);
            }
        }

        public Portfolio GetPortfolioForName(string name)
        {
            if (_portfolios == null)
            {
                return null;
            }

            return _portfolios.Find(portfolio => portfolio.Number == name);
        }

        public event Action<List<Portfolio>> PortfoliosChangeEvent;

        #endregion

        #region Securities

        private List<Security> _securities;

        public List<Security> Securities
        {
            get { return _securities; }
        }

        public Security GetSecurityForName(string name, string secClass)
        {
            if (_securities == null)
            {
                return null;
            }

            return _securities.Find(security => security.Name == name);
        }

        private void _candleManager_CandleUpdateEvent(CandleSeries series)
        {
            if (TesterRegime == TesterRegime.PlusOne)
            {
                TesterRegime = TesterRegime.Pause;
            }

            // write last tick time in server time / перегружаем последним временем тика время сервера
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

        #region Storage of additional security data: GO, Multipliers, Lots

        private void SetToSecuritiesDopSettings()
        {
            string pathToSecuritySettings = GetSecuritiesSettingsPath();
            List<string[]> array = LoadSecurityDopSettings(pathToSecuritySettings);

            for (int i = 0; array != null && i < array.Count; i++)
            {
                List<Security> secuAll = Securities.FindAll(s => s.Name == array[i][0]);

                if (secuAll != null && secuAll.Count != 0)
                {
                    for (int i2 = 0; i2 < secuAll.Count; i2++)
                    {
                        Security secu = secuAll[i2];

                        decimal lot = array[i][1].ToDecimal();
                        decimal go = array[i][2].ToDecimal();
                        decimal priceStepCost = array[i][3].ToDecimal();
                        decimal priceStep = array[i][4].ToDecimal();
                        decimal goSell = 0;
                        DateTime expiration = DateTime.MinValue;

                        int volDecimals = 0;

                        if (array[i].Length > 5)
                        {
                            volDecimals = Convert.ToInt32(array[i][5]);
                        }

                        if (array[i].Length > 6)
                        {
                            goSell = Convert.ToDecimal(array[i][6]);
                        }

                        if (array[i].Length > 7)
                        {
                            secu.Expiration = Convert.ToDateTime(array[i][7]);
                        }

                        if (lot != 0)
                        {
                            secu.Lot = lot;
                        }

                        if (go != 0)
                        {
                            secu.MarginBuy = go;
                        }

                        if (priceStepCost != 0)
                        {
                            secu.PriceStepCost = priceStepCost;
                        }

                        if (priceStep != 0)
                        {
                            secu.PriceStep = priceStep;
                        }

                        secu.DecimalsVolume = volDecimals;

                        if (goSell != 0)
                        {
                            secu.MarginSell = goSell;
                        }
                    }
                }
            }

            for (int i = 0; i < Securities.Count; i++)
            {
                Security etalonSecurity = Securities[i];

                for (int j = 0; j < SecuritiesTester.Count; j++)
                {
                    Security currentSecurity = SecuritiesTester[j].Security;
                    if (currentSecurity.Name == etalonSecurity.Name
                        && currentSecurity.NameClass == etalonSecurity.NameClass)
                    {
                        currentSecurity.LoadFromString(etalonSecurity.GetSaveStr());
                    }
                }
            }
        }

        private List<string[]> LoadSecurityDopSettings(string path)
        {
            if (SecuritiesTester.Count == 0)
            {
                return null;
            }

            if (!File.Exists(path))
            {
                return null;
            }
            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    List<string[]> array = new List<string[]>();

                    while (!reader.EndOfStream)
                    {
                        string[] set = reader.ReadLine().Split('$');
                        array.Add(set);
                    }

                    reader.Close();
                    return array;
                }
            }
            catch (Exception)
            {
                // send to the log / отправить в лог
            }
            return null;
        }

        public void SaveSecurityDopSettings(Security securityToSave)
        {
            if (SecuritiesTester.Count == 0)
            {
                return;
            }

            for (int i = 0; i < Securities.Count; i++)
            {
                if (Securities[i].Name == securityToSave.Name)
                {
                    Securities[i].LoadFromString(securityToSave.GetSaveStr());
                }
            }

            for (int i = 0; i < SecuritiesTester.Count; i++)
            {
                if (SecuritiesTester[i].Security.Name == securityToSave.Name)
                {
                    SecuritiesTester[i].Security.LoadFromString(securityToSave.GetSaveStr());
                }
            }

            string pathToSettings = GetSecuritiesSettingsPath();

            List<string[]> saves = LoadSecurityDopSettings(pathToSettings);

            if (saves == null)
            {
                saves = new List<string[]>();
            }

            CultureInfo culture = CultureInfo;

            for (int i = 0; i < saves.Count; i++)
            { // delete the same / удаляем совпадающие

                if (saves[i][0] == securityToSave.Name)
                {
                    saves.RemoveAt(i);
                    i--;
                }
            }

            if (saves.Count == 0)
            {
                saves.Add(new[]
                {
                    securityToSave.Name,
                    securityToSave.Lot.ToString(culture),
                    securityToSave.MarginBuy.ToString(culture),
                    securityToSave.PriceStepCost.ToString(culture),
                    securityToSave.PriceStep.ToString(culture),
                    securityToSave.DecimalsVolume.ToString(culture),
                    securityToSave.MarginSell.ToString(culture),
                    securityToSave.Expiration.ToString(culture)
                });
            }

            bool isInArray = false;

            for (int i = 0; i < saves.Count; i++)
            {
                if (saves[i][0] == securityToSave.Name)
                {
                    isInArray = true;
                }
            }

            if (isInArray == false)
            {
                saves.Add(new[]
                {
                    securityToSave.Name,
                    securityToSave.Lot.ToString(culture),
                    securityToSave.MarginBuy.ToString(culture),
                    securityToSave.PriceStepCost.ToString(culture),
                    securityToSave.PriceStep.ToString(culture),
                    securityToSave.DecimalsVolume.ToString(culture),
                    securityToSave.MarginSell.ToString(culture),
                    securityToSave.Expiration.ToString(culture)
                });
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(pathToSettings, false))
                {
                    // name, lot, GO, price step, cost of price step / Имя, Лот, ГО, Цена шага, стоимость цены шага
                    for (int i = 0; i < saves.Count; i++)
                    {
                        writer.WriteLine(
                            saves[i][0] + "$" +
                            saves[i][1] + "$" +
                            saves[i][2] + "$" +
                            saves[i][3] + "$" +
                            saves[i][4] + "$" +
                            saves[i][5] + "$" +
                            saves[i][6] + "$" +
                            saves[i][7]
                            );
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // send to the log / отправить в лог
            }

            if (NeedToReconnectEvent != null)
            {
                NeedToReconnectEvent();
            }
        }

        #endregion

        #region Get securities data from file system

        public TesterSourceDataType SourceDataType
        {
            get { return _sourceDataType; }
            set
            {
                if (value == _sourceDataType)
                {
                    return;
                }

                _sourceDataType = value;
                ReloadSecurities();
            }
        }
        private TesterSourceDataType _sourceDataType;

        private List<string> _sets;
        public List<string> Sets
        {
            get
            {
                return _sets;
            }
            private set
            {
                _sets = value;
            }
        }

        private void CheckSet()
        {
            if (!Directory.Exists(@"Data"))
            {
                Directory.CreateDirectory(@"Data");
            }

            string[] folders = Directory.GetDirectories(@"Data" + @"\");

            if (folders.Length == 0)
            {
                SendLogMessage(OsLocalization.Market.Message25, LogMessageType.System);
            }

            List<string> sets = new List<string>();

            for (int i = 0; i < folders.Length; i++)
            {
                string pathCurrent = folders[i];

                if (pathCurrent.Contains("Set_") == false)
                {
                    continue;
                }

                if (pathCurrent.Split('_').Length == 2)
                {
                    string setName = pathCurrent.Split('_')[1];

                    sets.Add(setName);
                    SendLogMessage(OsLocalization.Market.Label244 + ": " + setName, LogMessageType.System);
                }
            }

            if (sets.Count == 0)
            {
                SendLogMessage(OsLocalization.Market.Message25, LogMessageType.System);
            }
            Sets = sets;
        }

        public void SetNewSet(string setName)
        {
            string newSet = @"Data" + @"\" + @"Set_" + setName;
            if (newSet == _activeSet)
            {
                return;
            }

            SendLogMessage(OsLocalization.Market.Message27 + setName, LogMessageType.System);
            _activeSet = newSet;

            if (_sourceDataType == TesterSourceDataType.Set)
            {
                ReloadSecurities();
            }
            Save();
        }

        public void ReloadSecurities()
        {
            // clear all data and disconnect / чистим все данные, отключаемся
            TesterRegime = TesterRegime.NotActive;
            _dataIsReady = false;
            ServerStatus = ServerConnectStatus.Disconnect;
            _securities = null;
            SecuritiesTester = null;
            _candleManager.Clear();
            _candleSeriesTesterActivate = new List<SecurityTester>();
            Save();

            // update / обновляем

            _needToReloadSecurities = true;

            if (NeedToReconnectEvent != null)
            {
                NeedToReconnectEvent();
            }
        }

        public string PathToFolder
        {
            get { return _pathToFolder; }
        }
        private string _pathToFolder;

        public void ShowPathSenderDialog()
        {
            if (TesterRegime == TesterRegime.Play)
            {
                TesterRegime = TesterRegime.Pause;
            }

            System.Windows.Forms.FolderBrowserDialog myDialog = new System.Windows.Forms.FolderBrowserDialog();

            if (string.IsNullOrWhiteSpace(_pathToFolder))
            {
                myDialog.SelectedPath = _pathToFolder;
            }

            myDialog.ShowDialog();

            if (myDialog.SelectedPath != "" &&
                _pathToFolder != myDialog.SelectedPath) // если хоть что-то выбрано
            {
                _pathToFolder = myDialog.SelectedPath;
                if (_sourceDataType == TesterSourceDataType.Folder)
                {
                    ReloadSecurities();
                }
            }
        }

        private void LoadSecurities()
        {
            if ((_sourceDataType == TesterSourceDataType.Set && (string.IsNullOrWhiteSpace(_activeSet) || !Directory.Exists(_activeSet))) ||
                (_sourceDataType == TesterSourceDataType.Folder && (string.IsNullOrWhiteSpace(_pathToFolder) || !Directory.Exists(_pathToFolder))))
            {
                return;
            }

            TimeMax = DateTime.MinValue;
            TimeEnd = DateTime.MaxValue;
            TimeMin = DateTime.MaxValue;
            TimeStart = DateTime.MinValue;
            TimeNow = DateTime.MinValue;

            if (_sourceDataType == TesterSourceDataType.Set)
            { // Hercules data sets/сеты данных Геркулеса
                string[] directories = Directory.GetDirectories(_activeSet);

                if (directories.Length == 0)
                {
                    SendLogMessage(OsLocalization.Market.Message28, LogMessageType.System);
                    return;
                }

                for (int i = 0; i < directories.Length; i++)
                {
                    LoadSecurity(directories[i]);
                }

                _dataIsReady = true;
            }
            else // if (_sourceDataType == TesterSourceDataType.Folder)
            { // simple files from folder/простые файлы из папки

                string[] files = Directory.GetFiles(_pathToFolder);

                if (files.Length == 0)
                {
                    SendLogMessage(OsLocalization.Market.Message49, LogMessageType.Error);
                }

                LoadCandleFromFolder(_pathToFolder);
                LoadTickFromFolder(_pathToFolder);
                LoadMarketDepthFromFolder(_pathToFolder);
                _dataIsReady = true;
            }

            LoadSetSecuritiesTimeFrameSettings();
        }

        private void LoadSecurity(string path)
        {
            string[] directories = Directory.GetDirectories(path);

            if (directories.Length == 0)
            {
                return;
            }

            for (int i = 0; i < directories.Length; i++)
            {
                string name = directories[i].Split('\\')[3];

                if (name == "MarketDepth")
                {
                    LoadMarketDepthFromFolder(directories[i]);
                }
                else if (name == "Tick")
                {
                    LoadTickFromFolder(directories[i]);
                }
                else
                {
                    LoadCandleFromFolder(directories[i]);
                }
            }
        }

        private void LoadCandleFromFolder(string folderName)
        {

            string[] files = Directory.GetFiles(folderName);

            if (files.Length == 0)
            {
                return;
            }

            List<SecurityTester> security = new List<SecurityTester>();

            for (int i = 0; i < files.Length; i++)
            {
                security.Add(new SecurityTester());
                security[security.Count - 1].FileAddress = files[i];
                security[security.Count - 1].NewCandleEvent += TesterServer_NewCandleEvent;
                security[security.Count - 1].NewTradesEvent += TesterServer_NewTradesEvent;
                security[security.Count - 1].NewMarketDepthEvent += TesterServer_NewMarketDepthEvent;
                security[security.Count - 1].LogMessageEvent += TesterServer_LogMessageEvent;
                security[security.Count - 1].NeedToCheckOrders += TesterServer_NeedToCheckOrders;

                string name = files[i].Split('\\')[files[i].Split('\\').Length - 1];

                security[security.Count - 1].Security = new Security();
                security[security.Count - 1].Security.Name = name;
                security[security.Count - 1].Security.Lot = 1;
                security[security.Count - 1].Security.NameClass = "TestClass";
                security[security.Count - 1].Security.MarginBuy = 1;
                security[security.Count - 1].Security.MarginSell = 1;
                security[security.Count - 1].Security.PriceStepCost = 1;
                security[security.Count - 1].Security.PriceStep = 1;
                // timeframe / тф
                // price step / шаг цены
                // begin / начало
                // end / конец

                StreamReader reader = new StreamReader(files[i]);

                // candles / свечи: 20110111,100000,19577.00000,19655.00000,19533.00000,19585.00000,2752
                // ticks ver.1 / тики 1 вар: 20150401,100000,86160.000000000,2
                // ticks ver.2 / тики 2 вар: 20151006,040529,3010,5,Buy/Sell/Unknown

                string firstRowInFile = reader.ReadLine();

                if (string.IsNullOrEmpty(firstRowInFile))
                {
                    security.Remove(security[security.Count - 1]);
                    reader.Close();
                    continue;
                }

                try
                {
                    // check whether candles are in the file / смотрим свечи ли в файле
                    Candle candle = new Candle();
                    candle.SetCandleFromString(firstRowInFile);
                    // candles are in the file. We look at which ones / в файле свечи. Смотрим какие именно

                    security[security.Count - 1].TimeStart = candle.TimeStart;

                    Candle candle2 = new Candle();
                    candle2.SetCandleFromString(reader.ReadLine());

                    security[security.Count - 1].DataType = SecurityTesterDataType.Candle;
                    security[security.Count - 1].TimeFrameSpan = GetTimeSpan(reader);
                    security[security.Count - 1].TimeFrame = GetTimeFrame(security[security.Count - 1].TimeFrameSpan);
                    // step price / шаг цены

                    decimal minPriceStep = decimal.MaxValue;
                    int countFive = 0;

                    CultureInfo culture = CultureInfo;

                    for (int i2 = 0; i2 < 20; i2++)
                    {
                        if (reader.EndOfStream == true)
                        {
                            reader.Close();
                            reader = new StreamReader(files[i]);

                            if (reader.EndOfStream == true)
                            {
                                break;
                            }

                            continue;
                        }

                        Candle candleN = new Candle();
                        candleN.SetCandleFromString(reader.ReadLine());

                        decimal openD = (decimal)Convert.ToDouble(candleN.Open);
                        decimal highD = (decimal)Convert.ToDouble(candleN.High);
                        decimal lowD = (decimal)Convert.ToDouble(candleN.Low);
                        decimal closeD = (decimal)Convert.ToDouble(candleN.Close);

                        string open = openD.ToString().Replace(",", ".");
                        string high = highD.ToString().Replace(",", ".");
                        string low = lowD.ToString().Replace(",", ".");
                        string close = closeD.ToString().Replace(",", ".");

                        if (open.Split('.').Length > 1 ||
                            high.Split('.').Length > 1 ||
                            low.Split('.').Length > 1 ||
                            close.Split('.').Length > 1)
                        {
                            // if the real part takes place / если имеет место вещественная часть
                            int length = 1;

                            if (open.Split('.').Length > 1 &&
                                open.Split('.')[1].Length > length)
                            {
                                length = open.Split('.')[1].Length;
                            }

                            if (high.Split('.').Length > 1 &&
                                high.Split('.')[1].Length > length)
                            {
                                length = high.Split('.')[1].Length;
                            }

                            if (low.Split('.').Length > 1 &&
                                low.Split('.')[1].Length > length)
                            {
                                length = low.Split('.')[1].Length;
                            }

                            if (close.Split('.').Length > 1 &&
                                close.Split('.')[1].Length > length)
                            {
                                length = close.Split('.')[1].Length;
                            }

                            if (length == 1 && minPriceStep > 0.1m)
                            {
                                minPriceStep = 0.1m;
                            }
                            if (length == 2 && minPriceStep > 0.01m)
                            {
                                minPriceStep = 0.01m;
                            }
                            if (length == 3 && minPriceStep > 0.001m)
                            {
                                minPriceStep = 0.001m;
                            }
                            if (length == 4 && minPriceStep > 0.0001m)
                            {
                                minPriceStep = 0.0001m;
                            }
                            if (length == 5 && minPriceStep > 0.00001m)
                            {
                                minPriceStep = 0.00001m;
                            }
                            if (length == 6 && minPriceStep > 0.000001m)
                            {
                                minPriceStep = 0.000001m;
                            }
                            if (length == 7 && minPriceStep > 0.0000001m)
                            {
                                minPriceStep = 0.0000001m;
                            }
                            if (length == 8 && minPriceStep > 0.00000001m)
                            {
                                minPriceStep = 0.00000001m;
                            }
                            if (length == 9 && minPriceStep > 0.000000001m)
                            {
                                minPriceStep = 0.000000001m;
                            }
                        }
                        else
                        {
                            // if the real part doesn't take place / если вещественной части нет
                            int length = 1;

                            for (int i3 = open.Length - 1; open.ToString(culture)[i3] == '0'; i3--)
                            {
                                length = length * 10;
                            }

                            int lengthLow = 1;

                            for (int i3 = low.Length - 1; low[i3] == '0'; i3--)
                            {
                                lengthLow = lengthLow * 10;

                                if (length > lengthLow)
                                {
                                    length = lengthLow;
                                }
                            }

                            int lengthHigh = 1;

                            for (int i3 = high.Length - 1; high[i3] == '0'; i3--)
                            {
                                lengthHigh = lengthHigh * 10;

                                if (length > lengthHigh)
                                {
                                    length = lengthHigh;
                                }
                            }

                            int lengthClose = 1;

                            for (int i3 = close.Length - 1; close[i3] == '0'; i3--)
                            {
                                lengthClose = lengthClose * 10;

                                if (length > lengthClose)
                                {
                                    length = lengthClose;
                                }
                            }
                            if (minPriceStep > length)
                            {
                                minPriceStep = length;
                            }

                            if (minPriceStep == 1 &&
                                openD % 5 == 0 && highD % 5 == 0 &&
                                closeD % 5 == 0 && lowD % 5 == 0)
                            {
                                countFive++;
                            }
                        }
                    }

                    if (minPriceStep == 1 &&
                        countFive == 20)
                    {
                        minPriceStep = 5;
                    }

                    security[security.Count - 1].Security.PriceStep = minPriceStep;
                    security[security.Count - 1].Security.PriceStepCost = minPriceStep;

                    // last data / последняя дата
                    string lastString = firstRowInFile;

                    while (!reader.EndOfStream)
                    {
                        string curStr = reader.ReadLine();

                        if (string.IsNullOrEmpty(curStr))
                        {
                            continue;
                        }
                        lastString = curStr;
                    }

                    Candle candle3 = new Candle();
                    candle3.SetCandleFromString(lastString);
                    security[security.Count - 1].TimeEnd = candle3.TimeStart;
                    security[security.Count - 1].Security.Expiration = candle3.TimeStart;
                    continue;
                }
                catch (Exception)
                {
                    security.Remove(security[security.Count - 1]);
                }
                finally
                {
                    reader.Close();
                }
            }

            // save securities 
            // сохраняем бумаги

            if (security == null ||
                security.Count == 0)
            {
                return;
            }

            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            if (SecuritiesTester == null)
            {
                SecuritiesTester = new List<SecurityTester>();
            }

            for (int i = 0; i < security.Count; i++)
            {
                if (_securities.Find(security1 => security1.Name == security[i].Security.Name) == null)
                {
                    _securities.Add(security[i].Security);
                }

                SecuritiesTester.Add(security[i]);
            }

            // count the time
            // считаем время

            if (SecuritiesTester.Count != 0)
            {
                for (int i = 0; i < SecuritiesTester.Count; i++)
                {
                    if ((TimeMin == DateTime.MinValue && SecuritiesTester[i].TimeStart != DateTime.MinValue) ||
                        (SecuritiesTester[i].TimeStart != DateTime.MinValue && SecuritiesTester[i].TimeStart < TimeMin))
                    {
                        TimeMin = SecuritiesTester[i].TimeStart;
                        TimeStart = SecuritiesTester[i].TimeStart;
                        TimeNow = SecuritiesTester[i].TimeStart;
                    }
                    if (SecuritiesTester[i].TimeEnd != DateTime.MinValue &&
                        SecuritiesTester[i].TimeEnd > TimeMax)
                    {
                        TimeMax = SecuritiesTester[i].TimeEnd;
                        TimeEnd = SecuritiesTester[i].TimeEnd;
                    }
                }
            }

            // check in tester file data on the presence of multipliers and GO for securities
            // проверяем в файле тестера данные о наличии мультипликаторов и ГО для бумаг

            SetToSecuritiesDopSettings();
            LoadSecurityTestSettings();

            if (TestingNewSecurityEvent != null)
            {
                TestingNewSecurityEvent();
            }
        }

        private void LoadTickFromFolder(string folderName)
        {
            string[] files = Directory.GetFiles(folderName);

            if (files.Length == 0)
            {
                return;
            }

            List<SecurityTester> security = new List<SecurityTester>();

            for (int i = 0; i < files.Length; i++)
            {
                security.Add(new SecurityTester());
                security[security.Count - 1].FileAddress = files[i];
                security[security.Count - 1].NewCandleEvent += TesterServer_NewCandleEvent;
                security[security.Count - 1].NewTradesEvent += TesterServer_NewTradesEvent;
                security[security.Count - 1].NewMarketDepthEvent += TesterServer_NewMarketDepthEvent;
                security[security.Count - 1].LogMessageEvent += TesterServer_LogMessageEvent;
                security[security.Count - 1].NeedToCheckOrders += TesterServer_NeedToCheckOrders;

                string name = files[i].Split('\\')[files[i].Split('\\').Length - 1];

                security[security.Count - 1].Security = new Security();
                security[security.Count - 1].Security.Name = name;
                security[security.Count - 1].Security.Lot = 1;
                security[security.Count - 1].Security.NameClass = "TestClass";
                security[security.Count - 1].Security.MarginBuy = 1;
                security[security.Count - 1].Security.MarginSell = 1;
                security[security.Count - 1].Security.PriceStepCost = 1;
                security[security.Count - 1].Security.PriceStep = 1;
                // timeframe / тф
                // price step / шаг цены
                // begin / начало
                // end / конец

                StreamReader reader = new StreamReader(files[i]);

                // candles / свечи: 20110111,100000,19577.00000,19655.00000,19533.00000,19585.00000,2752
                // ticks ver.1 / тики 1 вар: 20150401,100000,86160.000000000,2
                // ticks ver.2 / тики 2 вар: 20151006,040529,3010,5,Buy/Sell/Unknown

                string firstRowInFile = reader.ReadLine();

                if (string.IsNullOrEmpty(firstRowInFile))
                {
                    security.Remove(security[security.Count - 1]);
                    reader.Close();
                    continue;
                }

                try
                {
                    // check whether ticks are in the file / смотрим тики ли в файле
                    Trade trade = new Trade();
                    trade.SetTradeFromString(firstRowInFile);
                    // ticks are in the file / в файле тики

                    security[security.Count - 1].TimeStart = trade.Time;
                    security[security.Count - 1].DataType = SecurityTesterDataType.Tick;

                    // price step / шаг цены

                    decimal minPriceStep = decimal.MaxValue;
                    int countFive = 0;

                    CultureInfo culture = CultureInfo;

                    for (int i2 = 0; i2 < 100; i2++)
                    {
                        Trade tradeN = new Trade();
                        tradeN.SetTradeFromString(reader.ReadLine());

                        decimal open = (decimal)Convert.ToDouble(tradeN.Price);

                        if (open.ToString(culture).Split('.').Length > 1)
                        {
                            // if the real part takes place / если имеет место вещественная часть
                            int length = 1;

                            if (open.ToString(culture).Split('.').Length > 1 &&
                                open.ToString(culture).Split('.')[1].Length > length)
                            {
                                length = open.ToString(culture).Split('.')[1].Length;
                            }


                            if (length == 1 && minPriceStep > 0.1m)
                            {
                                minPriceStep = 0.1m;
                            }
                            if (length == 2 && minPriceStep > 0.01m)
                            {
                                minPriceStep = 0.01m;
                            }
                            if (length == 3 && minPriceStep > 0.001m)
                            {
                                minPriceStep = 0.001m;
                            }
                            if (length == 4 && minPriceStep > 0.0001m)
                            {
                                minPriceStep = 0.0001m;
                            }
                            if (length == 5 && minPriceStep > 0.00001m)
                            {
                                minPriceStep = 0.00001m;
                            }
                            if (length == 6 && minPriceStep > 0.000001m)
                            {
                                minPriceStep = 0.000001m;
                            }
                            if (length == 7 && minPriceStep > 0.0000001m)
                            {
                                minPriceStep = 0.0000001m;
                            }
                            if (length == 8 && minPriceStep > 0.00000001m)
                            {
                                minPriceStep = 0.00000001m;
                            }
                            if (length == 9 && minPriceStep > 0.000000001m)
                            {
                                minPriceStep = 0.000000001m;
                            }
                        }
                        else
                        {
                            // if the real part doesn't take place / если вещественной части нет
                            int length = 1;

                            for (int i3 = open.ToString(culture).Length - 1; open.ToString(culture)[i3] == '0'; i3--)
                            {
                                length = length * 10;
                            }

                            if (minPriceStep > length)
                            {
                                minPriceStep = length;
                            }

                            if (length == 1 &&
                                open % 5 == 0)
                            {
                                countFive++;
                            }
                        }
                    }

                    if (minPriceStep == 1 &&
                        countFive == 20)
                    {
                        minPriceStep = 5;
                    }

                    security[security.Count - 1].Security.PriceStep = minPriceStep;
                    security[security.Count - 1].Security.PriceStepCost = minPriceStep;

                    // last data / последняя дата
                    string lastString2 = firstRowInFile;

                    while (!reader.EndOfStream)
                    {
                        string curRow = reader.ReadLine();

                        if (string.IsNullOrEmpty(curRow))
                        {
                            continue;
                        }

                        lastString2 = curRow;
                    }

                    Trade trade2 = new Trade();
                    trade2.SetTradeFromString(lastString2);
                    security[security.Count - 1].TimeEnd = trade2.Time;
                    security[security.Count - 1].Security.Expiration = trade2.Time;
                }
                catch (Exception)
                {
                    security.Remove(security[security.Count - 1]);
                }

                reader.Close();
            }

            // save securities / сохраняем бумаги

            if (security.Count == 0)
            {
                return;
            }

            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            if (SecuritiesTester == null)
            {
                SecuritiesTester = new List<SecurityTester>();
            }

            for (int i = 0; i < security.Count; i++)
            {
                if (_securities.Find(security1 => security1.Name == security[i].Security.Name) == null)
                {
                    _securities.Add(security[i].Security);
                }
                SecuritiesTester.Add(security[i]);
            }

            // count the time / считаем время 

            if (SecuritiesTester.Count != 0)
            {
                for (int i = 0; i < SecuritiesTester.Count; i++)
                {
                    if ((TimeMin == DateTime.MinValue && SecuritiesTester[i].TimeStart != DateTime.MinValue) ||
                        (SecuritiesTester[i].TimeStart != DateTime.MinValue && SecuritiesTester[i].TimeStart < TimeMin))
                    {
                        TimeMin = SecuritiesTester[i].TimeStart;
                        TimeStart = SecuritiesTester[i].TimeStart;
                        TimeNow = SecuritiesTester[i].TimeStart;
                    }
                    if (SecuritiesTester[i].TimeEnd != DateTime.MinValue &&
                        SecuritiesTester[i].TimeEnd > TimeMax)
                    {
                        TimeMax = SecuritiesTester[i].TimeEnd;
                        TimeEnd = SecuritiesTester[i].TimeEnd;
                    }
                }
            }

            // check in the tester file data on the presence of multipliers and GO for securities
            // проверяем в файле тестера данные о наличии мультипликаторов и ГО для бумаг

            SetToSecuritiesDopSettings();

            if (TestingNewSecurityEvent != null)
            {
                TestingNewSecurityEvent();
            }
        }

        private void LoadMarketDepthFromFolder(string folderName)
        {
            string[] files = Directory.GetFiles(folderName);

            if (files.Length == 0)
            {
                return;
            }

            List<SecurityTester> security = new List<SecurityTester>();

            for (int i = 0; i < files.Length; i++)
            {
                security.Add(new SecurityTester());
                security[security.Count - 1].FileAddress = files[i];
                security[security.Count - 1].NewCandleEvent += TesterServer_NewCandleEvent;
                security[security.Count - 1].NewTradesEvent += TesterServer_NewTradesEvent;
                security[security.Count - 1].LogMessageEvent += TesterServer_LogMessageEvent;
                security[security.Count - 1].NewMarketDepthEvent += TesterServer_NewMarketDepthEvent;
                security[security.Count - 1].NeedToCheckOrders += TesterServer_NeedToCheckOrders;

                string name = files[i].Split('\\')[files[i].Split('\\').Length - 1];

                security[security.Count - 1].Security = new Security();
                security[security.Count - 1].Security.Name = name;
                security[security.Count - 1].Security.Lot = 1;
                security[security.Count - 1].Security.NameClass = "TestClass";
                security[security.Count - 1].Security.MarginBuy = 1;
                security[security.Count - 1].Security.MarginSell = 1;
                security[security.Count - 1].Security.PriceStepCost = 1;
                security[security.Count - 1].Security.PriceStep = 1;
                // timeframe / тф
                // price step / шаг цены
                // begin / начало
                // end / конец

                StreamReader reader = new StreamReader(files[i]);

                // NameSecurity_Time_Bids_Asks
                // Bids: level*level*level
                // level: Bid&Ask&Price

                string firstRowInFile = reader.ReadLine();

                if (string.IsNullOrEmpty(firstRowInFile))
                {
                    security.Remove(security[security.Count - 1]);
                    reader.Close();
                    continue;
                }

                try
                {
                    // check whether depth is in the file / смотрим стакан ли в файле

                    MarketDepth trade = new MarketDepth();
                    trade.SetMarketDepthFromString(firstRowInFile);

                    // depth is in the file / в файле стаканы

                    security[security.Count - 1].TimeStart = trade.Time;
                    security[security.Count - 1].DataType = SecurityTesterDataType.MarketDepth;

                    // price step / шаг цены

                    decimal minPriceStep = decimal.MaxValue;
                    int countFive = 0;

                    CultureInfo culture = CultureInfo;

                    for (int i2 = 0; i2 < 20; i2++)
                    {
                        MarketDepth tradeN = new MarketDepth();
                        string lastStr = reader.ReadLine();
                        try
                        {
                            tradeN.SetMarketDepthFromString(lastStr);
                        }
                        catch (Exception error)
                        {
                            Thread.Sleep(2000);
                            SendLogMessage(error.ToString(), LogMessageType.Error);
                            continue;
                        }

                        decimal open = (decimal)Convert.ToDouble(tradeN.Bids[0].Price);

                        if (open == 0)
                        {
                            open = (decimal)Convert.ToDouble(tradeN.Asks[0].Price);
                        }

                        if (open.ToString(culture).Split('.').Length > 1)
                        {
                            // if the real part takes place / если имеет место вещественная часть
                            int length = 1;

                            if (open.ToString(culture).Split('.').Length > 1 &&
                                open.ToString(culture).Split('.')[1].Length > length)
                            {
                                length = open.ToString(culture).Split('.')[1].Length;
                            }

                            if (length == 1 && minPriceStep > 0.1m)
                            {
                                minPriceStep = 0.1m;
                            }
                            if (length == 2 && minPriceStep > 0.01m)
                            {
                                minPriceStep = 0.01m;
                            }
                            if (length == 3 && minPriceStep > 0.001m)
                            {
                                minPriceStep = 0.001m;
                            }
                            if (length == 4 && minPriceStep > 0.0001m)
                            {
                                minPriceStep = 0.0001m;
                            }
                            if (length == 5 && minPriceStep > 0.00001m)
                            {
                                minPriceStep = 0.00001m;
                            }
                            if (length == 6 && minPriceStep > 0.000001m)
                            {
                                minPriceStep = 0.000001m;
                            }
                            if (length == 7 && minPriceStep > 0.0000001m)
                            {
                                minPriceStep = 0.0000001m;
                            }
                            if (length == 8 && minPriceStep > 0.00000001m)
                            {
                                minPriceStep = 0.00000001m;
                            }
                            if (length == 9 && minPriceStep > 0.000000001m)
                            {
                                minPriceStep = 0.000000001m;
                            }
                        }
                        else
                        {
                            // if the real part doesn't take place / если вещественной части нет
                            int length = 1;

                            for (int i3 = open.ToString(culture).Length - 1; open.ToString(culture)[i3] == '0'; i3--)
                            {
                                length = length * 10;
                            }

                            if (minPriceStep > length)
                            {
                                minPriceStep = length;
                            }

                            if (length == 1 &&
                                open % 5 == 0)
                            {
                                countFive++;
                            }
                        }
                    }

                    if (minPriceStep == 1 &&
                        countFive == 20)
                    {
                        minPriceStep = 5;
                    }

                    security[security.Count - 1].Security.PriceStep = minPriceStep;
                    security[security.Count - 1].Security.PriceStepCost = minPriceStep;

                    // last data / последняя дата
                    string lastString2 = firstRowInFile;

                    while (!reader.EndOfStream)
                    {
                        string curRow = reader.ReadLine();

                        if (string.IsNullOrEmpty(curRow))
                        {
                            continue;
                        }

                        lastString2 = curRow;
                    }

                    MarketDepth trade2 = new MarketDepth();
                    trade2.SetMarketDepthFromString(lastString2);
                    security[security.Count - 1].TimeEnd = trade2.Time;
                    security[security.Count - 1].Security.Expiration = trade2.Time;
                }
                catch
                {
                    security.Remove(security[security.Count - 1]);
                }

                reader.Close();
            }

            // save securities
            // сохраняем бумаги

            if (security == null ||
                security.Count == 0)
            {
                return;
            }

            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            if (SecuritiesTester == null)
            {
                SecuritiesTester = new List<SecurityTester>();
            }

            for (int i = 0; i < security.Count; i++)
            {
                if (_securities.Find(security1 => security1.Name == security[i].Security.Name) == null)
                {
                    _securities.Add(security[i].Security);
                }
                SecuritiesTester.Add(security[i]);
            }

            // count the time
            // считаем время 

            if (SecuritiesTester.Count != 0)
            {
                for (int i = 0; i < SecuritiesTester.Count; i++)
                {
                    if ((TimeMin == DateTime.MinValue && SecuritiesTester[i].TimeStart != DateTime.MinValue) ||
                        (SecuritiesTester[i].TimeStart != DateTime.MinValue && SecuritiesTester[i].TimeStart < TimeMin))
                    {
                        TimeMin = SecuritiesTester[i].TimeStart;
                        TimeStart = SecuritiesTester[i].TimeStart;
                        TimeNow = SecuritiesTester[i].TimeStart;
                    }
                    if (SecuritiesTester[i].TimeEnd != DateTime.MinValue &&
                        SecuritiesTester[i].TimeEnd > TimeMax)
                    {
                        TimeMax = SecuritiesTester[i].TimeEnd;
                        TimeEnd = SecuritiesTester[i].TimeEnd;
                    }
                }
            }

            // check in the tester file data on the presence of multipliers and GO for securities
            // проверяем в файле тестера данные о наличии мультипликаторов и ГО для бумаг

            SetToSecuritiesDopSettings();

            if (TestingNewSecurityEvent != null)
            {
                TestingNewSecurityEvent();
            }
        }

        #endregion

        #region Subscribe securities to robots

        private List<SecurityTester> _candleSeriesTesterActivate;

        private CandleManager _candleManager;

        private object _starterLocker = new object();

        private DateTime _lastStartSecurityTime;

        public CandleSeries StartThisSecurity(string securityName, TimeFrameBuilder timeFrameBuilder, string securityClass)
        {
            lock (_starterLocker)
            {
                if (securityName == "")
                {
                    return null;
                }

                // need to start the server if it is still disabled / надо запустить сервер если он ещё отключен
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    return null;
                }

                if (_securities == null || _portfolios == null)
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

                CandleSeries series = new CandleSeries(timeFrameBuilder, security, StartProgram.IsTester);

                // start security for unloading / запускаем бумагу на выгрузку

                if (TypeTesterData != TesterDataType.Candle &&
                    timeFrameBuilder.CandleMarketDataType == CandleMarketDataType.Tick)
                {
                    if (_candleSeriesTesterActivate.Find(tester => tester.Security.Name == securityName &&
                                                                   tester.DataType == SecurityTesterDataType.Tick) == null)
                    {
                        if (SecuritiesTester != null &&
                            SecuritiesTester.Find(tester => tester.Security.Name == securityName &&
                                                            tester.DataType == SecurityTesterDataType.Tick) != null)
                        {
                            _candleSeriesTesterActivate.Add(
                                    SecuritiesTester.Find(tester => tester.Security.Name == securityName &&
                                           tester.DataType == SecurityTesterDataType.Tick));
                        }
                        else
                        { // there is nothing to run the series / нечем запускать серию
                            return null;
                        }
                    }
                }
                else if (TypeTesterData != TesterDataType.Candle &&
                         timeFrameBuilder.CandleMarketDataType == CandleMarketDataType.MarketDepth)
                {
                    if (_candleSeriesTesterActivate.Find(tester => tester.Security.Name == securityName &&
                                                                   tester.DataType == SecurityTesterDataType.MarketDepth) == null)
                    {
                        if (SecuritiesTester != null
                            && SecuritiesTester.Find(tester => tester.Security.Name == securityName &&
                                                            tester.DataType == SecurityTesterDataType.MarketDepth) != null)
                        {
                            _candleSeriesTesterActivate.Add(
                                    SecuritiesTester.Find(tester => tester.Security.Name == securityName &&
                                           tester.DataType == SecurityTesterDataType.MarketDepth));
                        }
                        else
                        { // there is nothing to run the series / нечем запускать серию
                            return null;
                        }
                    }
                }
                else if (TypeTesterData == TesterDataType.Candle)
                {
                    TimeSpan time = GetTimeFrameInSpan(timeFrameBuilder.TimeFrame);
                    if (_candleSeriesTesterActivate.Find(tester => tester.Security.Name == securityName &&
                                                                   tester.DataType == SecurityTesterDataType.Candle &&
                                                                   tester.TimeFrameSpan == time) == null)
                    {
                        if (SecuritiesTester == null ||
                            (SecuritiesTester.Find(tester => tester.Security.Name == securityName &&
                                                            tester.DataType == SecurityTesterDataType.Candle &&
                                                            tester.TimeFrameSpan == time) == null))
                        {
                            return null;
                        }

                        _candleSeriesTesterActivate.Add(
                            SecuritiesTester.Find(tester => tester.Security.Name == securityName &&
                                                            tester.DataType == SecurityTesterDataType.Candle &&
                                                            tester.TimeFrameSpan == time));
                    }
                }

                _candleManager.StartSeries(series);

                SendLogMessage(OsLocalization.Market.Message14 + series.Security.Name +
                               OsLocalization.Market.Message15 + series.TimeFrame +
                               OsLocalization.Market.Message16, LogMessageType.System);

                _lastStartSecurityTime = DateTime.Now;

                if (LoadSecurityEvent != null)
                {
                    LoadSecurityEvent();
                }

                return series;
            }
        }

        public List<Candle> GetCandleDataToSecurity(string securityName, string securityClass, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime, bool needToUpdate)
        {
            return null;
        }

        public List<Trade> GetTickDataToSecurity(string securityName, string securityClass, DateTime startTime, DateTime endTime, DateTime actualTime, bool needToUpdete)
        {
            return null;
        }

        private TimeSpan GetTimeFrameInSpan(TimeFrame frame)
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

        private TimeFrame GetTimeFrame(TimeSpan frameSpan)
        {
            TimeFrame timeFrame = TimeFrame.Min1;

            if (frameSpan == new TimeSpan(0, 0, 0, 1))
            {
                timeFrame = TimeFrame.Sec1;
            }
            else if (frameSpan == new TimeSpan(0, 0, 0, 2))
            {
                timeFrame = TimeFrame.Sec2;
            }
            else if (frameSpan == new TimeSpan(0, 0, 0, 5))
            {
                timeFrame = TimeFrame.Sec5;
            }
            else if (frameSpan == new TimeSpan(0, 0, 0, 10))
            {
                timeFrame = TimeFrame.Sec10;
            }
            else if (frameSpan == new TimeSpan(0, 0, 0, 15))
            {
                timeFrame = TimeFrame.Sec15;
            }
            else if (frameSpan == new TimeSpan(0, 0, 0, 20))
            {
                timeFrame = TimeFrame.Sec20;
            }
            else if (frameSpan == new TimeSpan(0, 0, 0, 30))
            {
                timeFrame = TimeFrame.Sec30;
            }
            else if (frameSpan == new TimeSpan(0, 0, 1, 0))
            {
                timeFrame = TimeFrame.Min1;
            }
            else if (frameSpan == new TimeSpan(0, 0, 2, 0))
            {
                timeFrame = TimeFrame.Min2;
            }
            else if (frameSpan == new TimeSpan(0, 0, 5, 0))
            {
                timeFrame = TimeFrame.Min5;
            }
            else if (frameSpan == new TimeSpan(0, 0, 10, 0))
            {
                timeFrame = TimeFrame.Min10;
            }
            else if (frameSpan == new TimeSpan(0, 0, 15, 0))
            {
                timeFrame = TimeFrame.Min15;
            }
            else if (frameSpan == new TimeSpan(0, 0, 20, 0))
            {
                timeFrame = TimeFrame.Min20;
            }
            else if (frameSpan == new TimeSpan(0, 0, 30, 0))
            {
                timeFrame = TimeFrame.Min30;
            }
            else if (frameSpan == new TimeSpan(0, 1, 0, 0))
            {
                timeFrame = TimeFrame.Hour1;
            }
            else if (frameSpan == new TimeSpan(0, 2, 0, 0))
            {
                timeFrame = TimeFrame.Hour2;
            }
            else if (frameSpan == new TimeSpan(0, 4, 0, 0))
            {
                timeFrame = TimeFrame.Hour4;
            }
            else if (frameSpan == new TimeSpan(1, 0, 0, 0))
            {
                timeFrame = TimeFrame.Day;
            }

            return timeFrame;
        }

        public void StopThisSecurity(CandleSeries series)
        {
            if (series != null)
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

        public event Action NeedToReconnectEvent;

        public event Action LoadSecurityEvent;

        #endregion

        #region Synchronizer 

        private string _activeSet;
        public string ActiveSet
        {
            get { return _activeSet; }
        }

        public DateTime TimeMin;

        public DateTime TimeMax;

        public DateTime TimeStart;

        public DateTime TimeEnd;

        public DateTime TimeNow;

        public bool DataIsReady
        {
            get
            {
                return _dataIsReady;
            }
        }

        private bool _dataIsReady;

        public List<SecurityTester> SecuritiesTester;

        public void SynchSecurities(List<BotPanel> bots)
        {
            if (bots == null || bots.Count == 0 ||
              SecuritiesTester == null || SecuritiesTester.Count == 0)
            {
                return;
            }

            List<string> namesSecurity = new List<string>();

            for (int i = 0; i < bots.Count; i++)
            {
                List<BotTabSimple> currentTabs = bots[i].TabsSimple;

                for (int i2 = 0; currentTabs != null && i2 < currentTabs.Count; i2++)
                {
                    if (currentTabs[i2].Security != null)
                    {
                        namesSecurity.Add(currentTabs[i2].Security.Name);
                    }
                }
            }

            for (int i = 0; i < bots.Count; i++)
            {
                List<BotTabPair> currentTabs = bots[i].TabsPair;

                for (int i2 = 0; currentTabs != null && i2 < currentTabs.Count; i2++)
                {
                    List<PairToTrade> pairs = currentTabs[i2].Pairs;

                    for (int i3 = 0; i3 < pairs.Count; i3++)
                    {
                        PairToTrade pair = pairs[i3];

                        if (pair.Tab1.Security != null)
                        {
                            namesSecurity.Add(pair.Tab1.Security.Name);
                        }
                        if (pair.Tab2.Security != null)
                        {
                            namesSecurity.Add(pair.Tab2.Security.Name);
                        }
                    }
                }
            }

            for (int i = 0; i < bots.Count; i++)
            {
                List<BotTabScreener> currentTabs = bots[i].TabsScreener;

                for (int i2 = 0; currentTabs != null && i2 < currentTabs.Count; i2++)
                {
                    List<string> secs = new List<string>();

                    for (int i3 = 0; i3 < currentTabs[i2].SecuritiesNames.Count; i3++)
                    {
                        if (string.IsNullOrEmpty(currentTabs[i2].SecuritiesNames[i3].SecurityName))
                        {
                            continue;
                        }
                        secs.Add(currentTabs[i2].SecuritiesNames[i3].SecurityName);
                    }

                    if (secs.Count == 0)
                    {
                        continue;
                    }

                    namesSecurity.AddRange(secs);
                }
            }

            for (int i = 0; i < bots.Count; i++)
            {
                List<BotTabCluster> currentTabs = bots[i].TabsCluster;

                for (int i2 = 0; currentTabs != null && i2 < currentTabs.Count; i2++)
                {
                    namesSecurity.Add(currentTabs[i2].CandleConnector.SecurityName);
                }
            }

            for (int i = 0; i < bots.Count; i++)
            {
                List<BotTabIndex> currentTabsSpread = bots[i].TabsIndex;

                for (int i2 = 0; currentTabsSpread != null && i2 < currentTabsSpread.Count; i2++)
                {
                    BotTabIndex index = currentTabsSpread[i2];

                    for (int i3 = 0; index.Tabs != null && i3 < index.Tabs.Count; i3++)
                    {
                        ConnectorCandles currentConnector = index.Tabs[i3];

                        if (!string.IsNullOrWhiteSpace(currentConnector.SecurityName))
                        {
                            namesSecurity.Add(currentConnector.SecurityName);
                        }
                    }

                }
            }

            for (int i = 0; i < SecuritiesTester.Count; i++)
            {
                if (namesSecurity.Find(name => name == SecuritiesTester[i].Security.Name) == null)
                {
                    SecuritiesTester[i].IsActive = false;
                }
                else
                {
                    SecuritiesTester[i].IsActive = true;
                }
            }

            _candleManager.SynhSeries(namesSecurity);

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

                if (SecuritiesTester.Find(name => name.TimeFrameSpan < new TimeSpan(0, 0, 1, 0)) == null)
                {
                    _timeAddType = TimeAddInTestType.Minute;
                }
                else
                {
                    _timeAddType = TimeAddInTestType.Second;
                }
            }
        }

        public void SaveSetSecuritiesTimeFrameSettings()
        {
            try
            {
                string fileName = @"Engine\TestServerSecuritiesTf"
                    + _sourceDataType.ToString()
                    + TypeTesterData.ToString();

                if (_sourceDataType == TesterSourceDataType.Set)
                {
                    if (string.IsNullOrEmpty(_activeSet))
                    {
                        return;
                    }
                    fileName += _activeSet.RemoveExcessFromSecurityName();
                }
                else if (_sourceDataType == TesterSourceDataType.Folder)
                {
                    if (string.IsNullOrEmpty(_pathToFolder))
                    {
                        return;
                    }
                    fileName += _pathToFolder.RemoveExcessFromSecurityName();
                }

                fileName += ".txt";

                using (StreamWriter writer = new StreamWriter(fileName, false))
                {
                    for (int i = 0; i < SecuritiesTester.Count; i++)
                    {
                        writer.WriteLine(SecuritiesTester[i].Security.Name + "#" + SecuritiesTester[i].TimeFrame);
                    }

                    writer.Close();
                }
            }
            catch
            {
                // ignored
            }
        }

        private void LoadSetSecuritiesTimeFrameSettings()
        {
            string fileName = @"Engine\TestServerSecuritiesTf"
                  + _sourceDataType.ToString()
                  + TypeTesterData.ToString();

            if (_sourceDataType == TesterSourceDataType.Set)
            {
                if (string.IsNullOrEmpty(_activeSet))
                {
                    return;
                }
                fileName += _activeSet.RemoveExcessFromSecurityName();
            }
            else if (_sourceDataType == TesterSourceDataType.Folder)
            {
                if (string.IsNullOrEmpty(_pathToFolder))
                {
                    return;
                }
                fileName += _pathToFolder.RemoveExcessFromSecurityName();
            }

            fileName += ".txt";

            if (!File.Exists(fileName))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(fileName))
                {
                    for (int i = 0; i < SecuritiesTester.Count; i++)
                    {
                        if (reader.EndOfStream == true)
                        {
                            return;
                        }

                        string[] security = reader.ReadLine().Split('#');

                        if (SecuritiesTester[i].Security.Name != security[0])
                        {
                            return;
                        }

                        TimeFrame frame;

                        if (Enum.TryParse(security[1], out frame))
                        {
                            SecuritiesTester[i].TimeFrame = frame;
                        }
                    }

                    reader.Close();
                }
            }
            catch
            {
                // ignored
            }

        }

        #endregion

        #region Candles

        private void TesterServer_NewCandleEvent(Candle candle, string nameSecurity, TimeSpan timeFrame)
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

        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            return null;
        }

        public event Action<CandleSeries> NewCandleIncomeEvent;

        #endregion

        #region Market depth 

        void TesterServer_NewMarketDepthEvent(MarketDepth marketDepth)
        {
            if (_dataIsActive == false)
            {
                _dataIsActive = true;
            }

            if (NewMarketDepthEvent != null)
            {
                NewMarketDepthEvent(marketDepth);
            }
        }

        public event Action<MarketDepth> NewMarketDepthEvent;

        public event Action<decimal, decimal, Security> NewBidAskIncomeEvent;

        #endregion

        #region All trades table

        private void TesterServer_NewTradesEvent(List<Trade> tradesNew)
        {
            if (_dataIsActive == false)
            {
                _dataIsActive = true;
            }

            if (_allTrades == null)
            {
                _allTrades = new List<Trade>[1];
                _allTrades[0] = new List<Trade>(tradesNew);
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
                        { // if there is already a storage for this instrument, save it/ если для этого инструметна уже есть хранилище, сохраняем и всё
                            isSave = true;
                            if (_allTrades[i][0].Time > trade.Time)
                            {
                                break;
                            }
                            _allTrades[i].Add(trade);
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
                    }
                }
            }

            if (_typeTesterData != TesterDataType.TickAllCandleState &&
                _typeTesterData != TesterDataType.TickOnlyReadyCandle)
            {
                for (int i = 0; i < _allTrades.Length; i++)
                {
                    List<Trade> curTrades = _allTrades[i];

                    if (curTrades != null &&
                        curTrades.Count > 100)
                    {
                        curTrades = curTrades.GetRange(curTrades.Count - 100, 100);
                        _allTrades[i] = curTrades;
                    }
                }
            }


            ServerTime = tradesNew[tradesNew.Count - 1].Time;

            if (NewTradeEvent != null)
            {
                for (int i = 0; i < _allTrades.Length; i++)
                {
                    List<Trade> trades = _allTrades[i];

                    if (tradesNew[0].SecurityNameCode == trades[0].SecurityNameCode
                        && tradesNew[0].TimeFrameInTester == trades[0].TimeFrameInTester)
                    {
                        if (_removeTradesFromMemory
                            && trades.Count > 1000)
                        {
                            _allTrades[i] = _allTrades[i].GetRange(trades.Count - 1000, 1000);
                            trades = _allTrades[i];
                        }

                        NewTradeEvent(trades);
                        break;
                    }
                }
            }
            if (NewBidAskIncomeEvent != null)
            {
                NewBidAskIncomeEvent((decimal)tradesNew[tradesNew.Count - 1].Price, (decimal)tradesNew[tradesNew.Count - 1].Price, GetSecurityForName(tradesNew[tradesNew.Count - 1].SecurityNameCode, ""));
            }
        }

        private List<Trade>[] _allTrades;

        public List<Trade>[] AllTrades { get { return _allTrades; } }

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

        void TesterServer_LogMessageEvent(string logMessage)
        {
            SendLogMessage(logMessage, LogMessageType.Error);
        }

        public void SendLogMessage(string message, LogMessageType type)
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
	/// Tester security. Encapsulates test data and data upload methods.
    /// </summary>
    public class SecurityTester
    {
        public Security Security;

        public string FileAddress;

        public DateTime TimeStart;

        public DateTime TimeEnd;

        public SecurityTesterDataType DataType;

        public TimeSpan TimeFrameSpan;

        public TimeFrame TimeFrame
        {
            get { return _timeFrame; }
            set
            {
                if (value == _timeFrame)
                {
                    return;
                }
                _timeFrame = value;

                if (value == TimeFrame.Sec1)
                {
                    TimeFrameSpan = new TimeSpan(0, 0, 0, 1);
                }
                else if (value == TimeFrame.Sec2)
                {
                    TimeFrameSpan = new TimeSpan(0, 0, 0, 2);
                }
                else if (value == TimeFrame.Sec5)
                {
                    TimeFrameSpan = new TimeSpan(0, 0, 0, 5);
                }
                else if (value == TimeFrame.Sec10)
                {
                    TimeFrameSpan = new TimeSpan(0, 0, 0, 10);
                }
                else if (value == TimeFrame.Sec15)
                {
                    TimeFrameSpan = new TimeSpan(0, 0, 0, 15);
                }
                else if (value == TimeFrame.Sec20)
                {
                    TimeFrameSpan = new TimeSpan(0, 0, 0, 20);
                }
                else if (value == TimeFrame.Sec30)
                {
                    TimeFrameSpan = new TimeSpan(0, 0, 0, 30);
                }
                else if (value == TimeFrame.Min1)
                {
                    TimeFrameSpan = new TimeSpan(0, 0, 1, 0);
                }
                else if (value == TimeFrame.Min2)
                {
                    TimeFrameSpan = new TimeSpan(0, 0, 2, 0);
                }
                else if (value == TimeFrame.Min3)
                {
                    TimeFrameSpan = new TimeSpan(0, 0, 3, 0);
                }
                else if (value == TimeFrame.Min5)
                {
                    TimeFrameSpan = new TimeSpan(0, 0, 5, 0);
                }
                else if (value == TimeFrame.Min10)
                {
                    TimeFrameSpan = new TimeSpan(0, 0, 10, 0);
                }
                else if (value == TimeFrame.Min15)
                {
                    TimeFrameSpan = new TimeSpan(0, 0, 15, 0);
                }
                else if (value == TimeFrame.Min20)
                {
                    TimeFrameSpan = new TimeSpan(0, 0, 20, 0);
                }
                else if (value == TimeFrame.Min30)
                {
                    TimeFrameSpan = new TimeSpan(0, 0, 30, 0);
                }
                else if (value == TimeFrame.Min45)
                {
                    TimeFrameSpan = new TimeSpan(0, 0, 45, 0);
                }
                else if (value == TimeFrame.Hour1)
                {
                    TimeFrameSpan = new TimeSpan(0, 1, 0, 0);
                }
                else if (value == TimeFrame.Hour2)
                {
                    TimeFrameSpan = new TimeSpan(0, 2, 0, 0);
                }
                else if (value == TimeFrame.Hour4)
                {
                    TimeFrameSpan = new TimeSpan(0, 4, 0, 0);
                }
                else if (value == TimeFrame.Day)
                {
                    TimeFrameSpan = new TimeSpan(0, 24, 0, 0);
                }
            }
        }
        TimeFrame _timeFrame;

        // data upload management

        public bool IsActive;

        private StreamReader _reader;

        public void Clear()
        {
            try
            {
                _reader = new StreamReader(FileAddress);
                LastCandle = null;
                LastTrade = null;
                LastMarketDepth = null;
                _tradesId = 0;
            }
            catch (Exception errror)
            {
                SendLogMessage(errror.ToString());
            }
        }

        public void Load(DateTime now)
        {
            if (IsActive == false)
            {
                return;
            }
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

        // parsing candle files

        private Candle _lastCandle;

        public Candle LastCandle
        {
            get { return _lastCandle; }
            set { _lastCandle = value; }
        }

        private void CheckCandles(DateTime now)
        {
            if (_reader == null || _reader.EndOfStream)
            {
                _reader = new StreamReader(FileAddress);
            }
            if (now > TimeEnd ||
                now < TimeStart)
            {
                return;
            }

            if (LastCandle != null &&
                LastCandle.TimeStart > now)
            {
                return;
            }

            if (LastCandle != null &&
                LastCandle.TimeStart == now)
            {
                List<Trade> lastTradesSeries = new List<Trade>();

                lastTradesSeries.Add(new Trade()
                {
                    Price = LastCandle.Open,
                    Volume = 1,
                    Side = Side.Sell,
                    Time = LastCandle.TimeStart,
                    SecurityNameCode = Security.Name,
                    TimeFrameInTester = TimeFrame,
                    IdInTester = _tradesId++
                });

                lastTradesSeries.Add(new Trade()
                {
                    Price = LastCandle.High,
                    Volume = 1,
                    Side = Side.Buy,
                    Time = LastCandle.TimeStart,
                    SecurityNameCode = Security.Name,
                    TimeFrameInTester = TimeFrame,
                    IdInTester = _tradesId++
                });

                lastTradesSeries.Add(new Trade()
                {
                    Price = LastCandle.Low,
                    Volume = 1,
                    Side = Side.Sell,
                    Time = LastCandle.TimeStart,
                    SecurityNameCode = Security.Name,
                    TimeFrameInTester = TimeFrame,
                    IdInTester = _tradesId++
                });

                lastTradesSeries.Add(new Trade()
                {
                    Price = LastCandle.Close,
                    Volume = 1,
                    Side = Side.Sell,
                    Time = LastCandle.TimeStart,
                    SecurityNameCode = Security.Name,
                    TimeFrameInTester = TimeFrame,
                    IdInTester = _tradesId++
                });

                if (NewTradesEvent != null)
                {
                    NewTradesEvent(lastTradesSeries);
                }

                if (NewCandleEvent != null)
                {
                    NewCandleEvent(LastCandle, Security.Name, TimeFrameSpan);
                }

                return;
            }

            while (LastCandle == null ||
                LastCandle.TimeStart < now)
            {
                LastCandle = new Candle();
                LastCandle.SetCandleFromString(_reader.ReadLine());
            }

            if (LastCandle.TimeStart <= now)
            {
                List<Trade> lastTradesSeries = new List<Trade>();

                lastTradesSeries.Add(new Trade()
                {
                    Price = LastCandle.Open,
                    Volume = 1,
                    Side = Side.Sell,
                    Time = LastCandle.TimeStart,
                    SecurityNameCode = Security.Name,
                    TimeFrameInTester = TimeFrame,
                    IdInTester = _tradesId++
                });

                lastTradesSeries.Add(new Trade()
                {
                    Price = LastCandle.High,
                    Volume = 1,
                    Side = Side.Buy,
                    Time = LastCandle.TimeStart,
                    SecurityNameCode = Security.Name,
                    TimeFrameInTester = TimeFrame,
                    IdInTester = _tradesId++
                });

                lastTradesSeries.Add(new Trade()
                {
                    Price = LastCandle.Low,
                    Volume = 1,
                    Side = Side.Sell,
                    Time = LastCandle.TimeStart,
                    SecurityNameCode = Security.Name,
                    TimeFrameInTester = TimeFrame,
                    IdInTester = _tradesId++
                });

                lastTradesSeries.Add(new Trade()
                {
                    Price = LastCandle.Close,
                    Volume = 1,
                    Side = Side.Sell,
                    Time = LastCandle.TimeStart,
                    SecurityNameCode = Security.Name,
                    TimeFrameInTester = TimeFrame,
                    IdInTester = _tradesId++
                });

                if (NewTradesEvent != null)
                {
                    NewTradesEvent(lastTradesSeries);
                }

                if (NewCandleEvent != null)
                {
                    NewCandleEvent(LastCandle, Security.Name, TimeFrameSpan);
                }

            }
        }

        public event Action<Candle, string, TimeSpan> NewCandleEvent;

        public event Action<MarketDepth> NewMarketDepthEvent;

        // parsing tick files

        public Trade LastTrade;

        public List<Trade> LastTradeSeries;

        private string _lastString;

        private long _tradesId;

        public bool IsNewDayTrade;

        public DateTime LastTradeTime;

        private void CheckTrades(DateTime now)
        {
            if (_reader == null || (_reader.EndOfStream && LastTrade == null))
            {
                _reader = new StreamReader(FileAddress);
            }
            if (now > TimeEnd ||
                now < TimeStart)
            {
                return;
            }

            if (LastTrade != null &&
                LastTrade.Time.AddMilliseconds(-LastTrade.Time.Millisecond) > now)
            {
                return;
            }

            // swing the first second if / качаем первую секунду если 

            if (LastTrade == null)
            {
                _lastString = _reader.ReadLine();
                LastTrade = new Trade();
                LastTrade.SetTradeFromString(_lastString);
                LastTrade.SecurityNameCode = Security.Name;
                LastTrade.IdInTester = _tradesId++;
            }

            while (!_reader.EndOfStream &&
                   LastTrade.Time.AddMilliseconds(-LastTrade.Time.Millisecond) < now)
            {
                _lastString = _reader.ReadLine();
                LastTrade.SetTradeFromString(_lastString);
                LastTrade.SecurityNameCode = Security.Name;
                LastTrade.IdInTester = _tradesId++;
            }

            if (LastTrade.Time.AddMilliseconds(-LastTrade.Time.Millisecond) > now)
            {
                return;
            }

            // here we have the first trade in the current second / здесь имеем первый трейд в текущей секунде

            List<Trade> lastTradesSeries = new List<Trade>();

            if (LastTrade != null
                && LastTrade.Time == now)
            {
                lastTradesSeries.Add(LastTrade);
            }

            while (!_reader.EndOfStream)
            {
                _lastString = _reader.ReadLine();
                Trade tradeN = new Trade() { SecurityNameCode = Security.Name };
                tradeN.SetTradeFromString(_lastString);
                tradeN.IdInTester = _tradesId++;

                if (tradeN.Time.AddMilliseconds(-tradeN.Time.Millisecond) <= now)
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

            for (int i = 0; i < lastTradesSeries.Count; i++)
            {
                List<Trade> trades = new List<Trade>() { lastTradesSeries[i] };
                LastTradeSeries = trades;
                NeedToCheckOrders();
                NewTradesEvent(trades);
            }

            if (lastTradesSeries.Count > 0)
            {
                LastTradeTime = lastTradesSeries[^1].Time;
            }
        }

        public event Action<List<Trade>> NewTradesEvent;

        public event Action NeedToCheckOrders;

        // parsing market depths

        public MarketDepth LastMarketDepth;

        private void CheckMarketDepth(DateTime now)
        {
            if (_reader == null || _reader.EndOfStream)
            {
                _reader = new StreamReader(FileAddress);
            }

            if (now > TimeEnd ||
                now < TimeStart)
            {
                return;
            }

            if (LastMarketDepth != null &&
                LastMarketDepth.Time > now)
            {
                return;
            }

            // if download the first second / качаем первую секунду если 

            if (LastMarketDepth == null)
            {
                _lastString = _reader.ReadLine();
                LastMarketDepth = new MarketDepth();
                LastMarketDepth.SetMarketDepthFromString(_lastString);
                LastMarketDepth.SecurityNameCode = Security.Name;
            }

            while (!_reader.EndOfStream &&
                   LastMarketDepth.Time < now)
            {
                _lastString = _reader.ReadLine();
                LastMarketDepth.SetMarketDepthFromString(_lastString);
            }

            if (LastMarketDepth.Time.AddSeconds(-1) > now)
            {
                return;
            }

            if (NewMarketDepthEvent != null)
            {
                NewMarketDepthEvent(LastMarketDepth);
            }
        }

        // logging

        private void SendLogMessage(string message)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message);
            }
        }

        public event Action<string> LogMessageEvent;

    }

    /// <summary>
	/// Data storage type
    /// </summary>
    public enum TesterSourceDataType
    {
        Set,

        Folder
    }

    /// <summary>
	/// Current operation mode of the tester
    /// </summary>
    public enum TesterRegime
    {
        NotActive,

        Pause,

        Play,

        PlusOne,
    }

    /// <summary>
	/// Type of data translation from the tester
    /// </summary>
    public enum TesterDataType
    {
        Candle,

        TickAllCandleState,

        TickOnlyReadyCandle,

        MarketDepthAllCandleState,

        MarketDepthOnlyReadyCandle,

        Unknown
    }

    /// <summary>
	/// Type of data stored
    /// </summary>
    public enum SecurityTesterDataType
    {
        Candle,

        Tick,

        MarketDepth
    }

    /// <summary>
	/// Time step in the synchronizer
    /// </summary>
    public enum TimeAddInTestType
    {
        FiveMinute,

        Minute,

        Second,

        Millisecond
    }

    /// <summary>
	/// type of limit order execution
    /// </summary>
    public enum OrderExecutionType
    {
        Touch,

        Intersection,

        FiftyFifty
    }

    /// <summary>
    /// Clearing for limit orders
    /// </summary>
    public class OrderClearing
    {
        public DateTime Time;

        public bool IsOn;

        public string GetSaveString()
        {
            string result = "";

            result += Time.ToString(CultureInfo.InvariantCulture) + "$";
            result += IsOn;

            return result;
        }

        public void SetFromString(string str)
        {
            string[] strings = str.Split('$');

            Time = Convert.ToDateTime(strings[0], CultureInfo.InvariantCulture);
            IsOn = Convert.ToBoolean(strings[1]);
        }
    }

    /// <summary>
    /// Period with NO new positions and NO new open orders in tester
    /// </summary>
    public class NonTradePeriod
    {
        public DateTime DateStart;

        public DateTime DateEnd;

        public bool IsOn;

        public string GetSaveString()
        {
            string result = "";

            result += DateStart.ToString(CultureInfo.InvariantCulture) + "$";
            result += DateEnd.ToString(CultureInfo.InvariantCulture) + "$";
            result += IsOn;

            return result;
        }

        public void SetFromString(string str)
        {
            string[] strings = str.Split('$');

            DateStart = Convert.ToDateTime(strings[0], CultureInfo.InvariantCulture);
            DateEnd = Convert.ToDateTime(strings[1], CultureInfo.InvariantCulture);
            IsOn = Convert.ToBoolean(strings[2]);
        }
    }

}