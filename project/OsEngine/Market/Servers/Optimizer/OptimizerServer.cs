/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Tester;

namespace OsEngine.Market.Servers.Optimizer
{
    /// <summary>
    /// сервер оптимизации.
    /// Во время оптимизации для каждого робота разворачивается отдельный экземпляр
    /// со своим потоком
    /// </summary>
    public class OptimizerServer:IServer
    {
       /// <summary>
       /// конструктор
       /// </summary>
       /// <param name="dataStorage">хранилище данных</param>
       /// <param name="num">номер сервера</param>
       /// <param name="portfolioStratValue">начальное значение для порфеля</param>
        public OptimizerServer(OptimizerDataStorage dataStorage, int num, decimal portfolioStratValue)
        {
            _storagePrime = dataStorage;
            _logMaster = new Log("OptimizerServer",StartProgram.IsOsOptimizer);
            _logMaster.Listen(this);
            _serverConnectStatus = ServerConnectStatus.Disconnect;
            ServerStatus = ServerConnectStatus.Disconnect;
            _testerRegime = TesterRegime.Pause;
            TypeTesterData = TesterDataType.Candle;
            CreatePortfolio(portfolioStratValue);
            NumberServer = num;

            if (_worker == null)
            {
                _worker = new Thread(WorkThreadArea);
                _worker.CurrentCulture = new CultureInfo("ru-RU");
                _worker.Name = "OptimizerThread " + num;
                _worker.IsBackground = true;
                _worker.Start();
            }

            _candleManager = new CandleManager(this);
            _candleManager.CandleUpdateEvent += _candleManager_CandleUpdateEvent;
            _candleManager.LogMessageEvent += SendLogMessage;
            _candleManager.TypeTesterData = TypeTesterData;

            _candleSeriesTesterActivate = new List<SecurityOptimizer>();

            OrdersActiv = new List<Order>();
        }

        /// <summary>
        /// номер сервера
        /// </summary>
        public int NumberServer;

        /// <summary>
        /// тип сервера
        /// </summary>
        public ServerType ServerType
        {
            get { return ServerType.Optimizer; }
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {

        }

        /// <summary>
        /// тип данных которые заказывает тестер
        /// </summary>
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
            }
        }
        private TesterDataType _typeTesterData;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {

        }

        /// <summary>
        /// очистить сервер от не нужных ссылок
        /// </summary>
        public void Clear()
        {
            _cleared = true;
        }
        private bool _cleared;

// аппендикс от нормальных серверов

        /// <summary>
        /// в тестовом сервере не используется
        /// </summary>
        public void StartServer(){}

        /// <summary>
        /// в тестовом сервере не используется
        /// </summary>
        public void StopServer(){}

// Управление

        /// <summary>
        /// начать тестирование
        /// </summary>
        public void TestingStart()
        {
            _serverTime = DateTime.MinValue;

            ServerMaster.ClearOrders();

            TimeNow = _storages[0].TimeStart;

            while (TimeNow.Hour != 10)
            {
                TimeNow = TimeNow.AddHours(-1);
            }

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

            if (TypeTesterData == TesterDataType.TickAllCandleState ||
    TypeTesterData == TesterDataType.TickOnlyReadyCandle)
            {
                _timeAddType = TimeAddInTestType.Second;
            }
            else if (TypeTesterData == TesterDataType.MarketDepthAllCandleState ||
                     TypeTesterData == TesterDataType.MarketDepthOnlyReadyCandle)
            {
                _timeAddType = TimeAddInTestType.MilliSecond;
            }
            else if (TypeTesterData == TesterDataType.Candle)
            {

                if (_candleSeriesTesterActivate.Find(name => name.TimeFrameSpan < new TimeSpan(0, 0, 1, 0)) == null)
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

            if (_candleSeriesTesterActivate != null)
            {
                for (int i = 0; i < _candleSeriesTesterActivate.Count; i++)
                {
                    _candleSeriesTesterActivate[i].Clear();
                }
            }

            _candleManager.Clear();

            _allTrades = null;

            _dataIsActive = false;

            ProfitArray = new List<decimal>();

            _testerRegime = TesterRegime.Play;
        }

        /// <summary>
        /// Тестирование запущено
        /// </summary>
        public event Action TestingStartEvent;

        /// <summary>
        /// тестирование прервано
        /// параметр: номер севера
        /// </summary>
        public event Action<int> TestingEndEvent;

        /// <summary>
        /// изменилось кол-во прогружаемых объектов
        /// первый параметр - текущее кол-во
        /// второй параметр - максимальное
        /// третий - номер сервера
        /// </summary>
        public event Action<int, int, int> TestintProgressChangeEvent;

// место работы основного потока

        /// <summary>
        /// точность синхронизатора. Для свечек выше минуты - минутки. Для тиков - секунды. Для стаканов - миллисекунды
        /// устанавливается в методе SynhSecurities
        /// </summary>
        private TimeAddInTestType _timeAddType;

        /// <summary>
        /// пошли ли данные из серий данных
        /// </summary>
        private bool _dataIsActive;

        /// <summary>
        /// режим тестирования
        /// </summary>
        private TesterRegime _testerRegime;

        /// <summary>
        /// основной поток, которые занимается прогрузкой всех данных
        /// </summary>
        private Thread _worker;

        /// <summary>
        /// место работы основного потока
        /// </summary>
        private void WorkThreadArea()
        {
            Thread.Sleep(2000);
            while (true)
            {
                try
                {
                    if (_cleared)
                    {
                        _securities = null;
                        _storages = null;
                        _storagePrime = null;
                        //_candleManager.Clear();
                        _candleManager = null;

                        for (int i = 0; i < _candleSeriesTesterActivate.Count; i++)
                        {
                            _candleSeriesTesterActivate[i].Clear();
                        }
                        _candleSeriesTesterActivate = null;
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
                        Thread.Sleep(2000);
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
        
// запрашивание данных у хранилища

        /// <summary>
        /// хранилище данных
        /// </summary>
        private OptimizerDataStorage _storagePrime;

        /// <summary>
        /// все хранилища свечей, тиков, стаканов, которые у нас запросил робот
        /// </summary>
        private List<DataStorage> _storages = new List<DataStorage>(); 

        /// <summary>
        /// загрузить в сервер данные
        /// </summary>
        /// <param name="security">спецификация инструмента</param>
        /// <param name="timeFrame">таймФрейм</param>
        /// <param name="timeStart">время старта данных</param>
        /// <param name="timeEnd">время завершения данных</param>
        public void GetDataToSecurity(Security security, TimeFrame timeFrame, DateTime timeStart, DateTime timeEnd)
        {
            DataStorage newStorage = _storagePrime.GetStorageToSecurity(security, timeFrame, timeStart, timeEnd);

            if (newStorage == null)
            {
                Thread.Sleep(500);
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
                                    s.MaketDepths == newStorage.MaketDepths) != null)
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
            securityOpt.NewCandleEvent += TesterServer_NewCandleEvent;
            securityOpt.NewTradesEvent += TesterServer_NewTradesEvent;
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
            else if (_storages[_storages.Count - 1].StorageType == TesterDataType.MarketDepthOnlyReadyCandle)
            {
                securityOpt.DataType = SecurityTesterDataType.MarketDepth;
                securityOpt.MarketDepths = _storages[_storages.Count - 1].MaketDepths;
            }

            _candleSeriesTesterActivate.Add(securityOpt);

            ServerStatus = ServerConnectStatus.Connect;
        }

// подгрузка данных

        /// <summary>
        /// время синхронизатора в данный момент подачи истории
        /// </summary>
        public DateTime TimeNow;

        /// <summary>
        /// запросить следующие данные
        /// </summary>
        private void LoadNextData()
        {
            if (_testerRegime == TesterRegime.Pause)
            {
                return;
            }
            if (_storages[0].TimeStart > _storages[0].TimeEnd || TimeNow > _storages[0].TimeEnd)
            {
                _testerRegime = TesterRegime.Pause;

                SendLogMessage(OsLocalization.Market.Message37, LogMessageType.System);
                if (TestingEndEvent != null)
                {
                    TestingEndEvent(NumberServer);
                }
                return;
            }

            if (_candleSeriesTesterActivate == null ||
                _candleSeriesTesterActivate.Count == 0)
            {
                _testerRegime = TesterRegime.Pause;

                SendLogMessage(OsLocalization.Market.Message38,
                    LogMessageType.System);
                if (TestingEndEvent != null)
                {
                    TestingEndEvent(NumberServer);
                }
                return;
            }

            if (_timeAddType == TimeAddInTestType.MilliSecond)
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

            for (int i = 0; _candleSeriesTesterActivate != null && i < _candleSeriesTesterActivate.Count; i++)
            {
                _candleSeriesTesterActivate[i].Load(TimeNow);
            }
        }

// проверка исполнения ордеров

        /// <summary>
        /// проверить ордера на исполненность
        /// </summary>
        private void CheckOrders()
        {
            if (OrdersActiv.Count == 0)
            {
                return;
            }

            for (int i = 0; i < OrdersActiv.Count; i++)
            {

                Order order = OrdersActiv[i];
                // проверяем наличие инструмента на рынке
                SecurityOptimizer security =
                    _candleSeriesTesterActivate.Find(
                        tester =>
                            tester.Security.Name == order.SecurityNameCode &&
                            (tester.LastCandle != null || tester.LastTradeSeries != null ||
                             tester.LastMarketDepth != null));

                if (security == null)
                {
                    return;
                }

                if (security.DataType == SecurityTesterDataType.Tick)
                { // прогон на тиках
                    List<Trade> trades = security.LastTradeSeries;

                    for (int indexTrades = 0; trades != null && indexTrades < trades.Count; indexTrades++)
                    {
                        if (CheckOrdersInTickTest(order, trades[indexTrades],false))
                        {
                            i--;
                            break;
                        }
                    }
                }
                else if(security.DataType == SecurityTesterDataType.Candle)
                { // прогон на свечках
                    Candle lastCandle = security.LastCandle;
                    if (CheckOrdersInCandleTest(order, lastCandle))
                    {
                        i--;
                    }
                }
                else if (security.DataType == SecurityTesterDataType.MarketDepth)
                {
                    // ЗДЕСЬ!!!!!!!!!!!!!!!!!!!!
                    MarketDepth depth = security.LastMarketDepth;

                    if (CheckOrdersInMarketDepthTest(order, depth))
                    {
                        i--;
                    }
                }
            }
        }

        /// <summary>
        /// проверить исполнение ордера при тестировании на свечках
        /// </summary>
        /// <param name="order">ордер</param>
        /// <param name="lastCandle">свеча на которой проверяем исполнение</param>
        /// <returns>если исполнилось или отозвалось по времени, возвратиться true</returns>
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

            if (time <= order.TimeCallBack && !order.IsStopOrProfit)
            {
                //CanselOnBoardOrder(order);
                return false;
            }

            // проверяем, прошёл ли ордер
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
                {// исполняем

                    decimal realPrice = order.Price;

                    if (realPrice > openPrice && order.IsStopOrProfit == false)
                    {
                        // если заявка не котировачная и выставлена в рынок
                        realPrice = openPrice;
                    }
                    else if (order.IsStopOrProfit && order.Price > maxPrice)
                    {
                        realPrice = maxPrice;
                    }

                    int slipage = 0;

                    ExecuteOnBoardOrder(order, realPrice, time, slipage);

                    for (int i = 0; i < OrdersActiv.Count; i++)
                    {
                        if (OrdersActiv[i].NumberUser == order.NumberUser)
                        {
                            OrdersActiv.RemoveAt(i);
                            break;
                        }
                    }

                    if (OrderExecutionType == OrderExecutionType.FiftyFifty)
                    {
                        if (_lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch)
                        {_lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Intersection;}
                        else
                        {_lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Touch;}
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
// исполняем
                    decimal realPrice = order.Price;

                    if (realPrice < openPrice && order.IsStopOrProfit == false)
                    {
                        // если заявка не котировачная и выставлена в рынок
                        realPrice = openPrice;
                    }
                    else if (order.IsStopOrProfit && order.Price < minPrice)
                    {
                        realPrice = minPrice;
                    }

                    int slipage = 0;

                    ExecuteOnBoardOrder(order, realPrice, time, slipage);

                    for (int i = 0; i < OrdersActiv.Count; i++)
                    {
                        if (OrdersActiv[i].NumberUser == order.NumberUser)
                        {
                            OrdersActiv.RemoveAt(i);
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

            // ордер не `исполнился. проверяем, не пора ли отзывать

            if (order.TimeCallBack.Add(order.LifeTime) <= ServerTime)
            {
                CanselOnBoardOrder(order);
                return true;
            }
            return false;
        }

        /// <summary>
        /// проверить исполнение ордера при тиковом прогоне
        /// </summary>
        /// <param name="order">ордер для исполнения</param>
        /// <param name="lastTrade">последняя цена по инструменту</param>
        /// <param name="firstTime">первая ли эта проверка на исполнение. Если первая то возможно исполнение по текущей цене.
        /// есил false, то исполнение только по цене ордером. В этом случае мы котируем</param>
        /// <returns></returns>
        private bool CheckOrdersInTickTest(Order order, Trade lastTrade, bool firstTime)
        {
            SecurityOptimizer security = _candleSeriesTesterActivate.Find(s => s.Security.Name == order.SecurityNameCode);

            if (security == null)
            {
                return false;
            }

            // проверяем, прошёл ли ордер
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
                {// исполняем
                    int slipage = 0;


                    ExecuteOnBoardOrder(order, lastTrade.Price, ServerTime, slipage);

                    for (int i = 0; i < OrdersActiv.Count; i++)
                    {
                        if (OrdersActiv[i].NumberUser == order.NumberUser)
                        {
                            OrdersActiv.RemoveAt(i);
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
                {// исполняем
                    int slipage = 0;

                    ExecuteOnBoardOrder(order, lastTrade.Price, ServerTime, slipage);

                    for (int i = 0; i < OrdersActiv.Count; i++)
                    {
                        if (OrdersActiv[i].NumberUser == order.NumberUser)
                        {
                            OrdersActiv.RemoveAt(i);
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

            // ордер не исполнился. проверяем, не пора ли отзывать

            if (order.TimeCallBack.Add(order.LifeTime) <= ServerTime)
            {
                CanselOnBoardOrder(order);
                return true;
            }
            return false;
        }

        /// <summary>
        /// проверить исполнение ордера при тестировании на свечках
        /// </summary>
        /// <param name="order">ордер</param>
        /// <param name="lastMarketDepth">стакан на которой проверяем исполнение</param>
        /// <returns>если исполнилось или отозвалось по времени, возвратиться true</returns>
        private bool CheckOrdersInMarketDepthTest(Order order, MarketDepth lastMarketDepth)
        {
            if (lastMarketDepth == null)
            {
                return false;
            }
            decimal maxPrice = lastMarketDepth.Asks[0].Price;
            decimal minPrice = lastMarketDepth.Bids[0].Price;
            decimal openPrice = lastMarketDepth.Asks[0].Price;

            DateTime time = lastMarketDepth.Time;

            if (time <= order.TimeCallBack && !order.IsStopOrProfit)
            {
                CanselOnBoardOrder(order);
                return false;
            }

            // проверяем, прошёл ли ордер
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
                 {
                    decimal realPrice = order.Price;

                    if (realPrice > openPrice && order.IsStopOrProfit == false)
                    {
                        // если заявка не котировачная и выставлена в рынок
                        realPrice = openPrice;
                    }
                    else if (order.IsStopOrProfit && order.Price > maxPrice)
                    {
                        realPrice = maxPrice;
                    }
                    int slipage = 0;

                    ExecuteOnBoardOrder(order, realPrice, time, slipage);

                    for (int i = 0; i < OrdersActiv.Count; i++)
                    {
                        if (OrdersActiv[i].NumberUser == order.NumberUser)
                        {
                            OrdersActiv.RemoveAt(i);
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
// исполняем
                    decimal realPrice = order.Price;

                    if (realPrice < openPrice && order.IsStopOrProfit == false)
                    {
                        // если заявка не котировачная и выставлена в рынок
                        realPrice = openPrice;
                    }
                    else if (order.IsStopOrProfit && order.Price < minPrice)
                    {
                        realPrice = minPrice;
                    }
                    int slipage = 0;

                    ExecuteOnBoardOrder(order, realPrice, time, slipage);

                    for (int i = 0; i < OrdersActiv.Count; i++)
                    {
                        if (OrdersActiv[i].NumberUser == order.NumberUser)
                        {
                            OrdersActiv.RemoveAt(i);
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

            // ордер не `исполнился. проверяем, не пора ли отзывать

            if (order.TimeCallBack.Add(order.LifeTime) <= ServerTime)
            {
                CanselOnBoardOrder(order);
                return true;
            }
            return false;
        }

        /// <summary>
        /// тип исполнения ордеров
        /// </summary>
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

        /// <summary>
        /// следующий по очереди тип исполнения заявки, 
        /// если мы выбрали тип 50*50 и они должны чередоваться
        /// </summary>
        private OrderExecutionType _lastOrderExecutionTypeInFiftyFiftyType;

// статус сервера

        /// <summary>
        /// статус сервера
        /// </summary>
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

        /// <summary>
        /// изменился статус соединения
        /// </summary>
        public event Action<string> ConnectStatusChangeEvent;

        public int CountDaysTickNeadToSave { get; set; }

        public bool NeadToSaveTicks { get; set; }

// время сервера

        private DateTime _serverTime;
        /// <summary>
        /// время сервера
        /// </summary>
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

        /// <summary>
        /// изменилось время сервера
        /// </summary>
        public event Action<DateTime> TimeServerChangeEvent;

// прибыли и убытки биржи

        /// <summary>
        /// лист с историей движения портфеля
        /// </summary>
        public List<decimal> ProfitArray;

        /// <summary>
        /// добавить в портфель изменение
        /// </summary>
        /// <param name="profit">прибыль со сделки</param>
        public void AddProfit(decimal profit)
        {
            Portfolios[0].ValueCurrent += profit;
            ProfitArray.Add(Portfolios[0].ValueCurrent);

            if (NewCurrentValue != null)
            {
                NewCurrentValue(Portfolios[0].ValueCurrent);
            }
        }

        /// <summary>
        /// событие: значение портфеля изменилось
        /// </summary>
        public event Action<decimal> NewCurrentValue; 


// портфели и позиция на бирже

        /// <summary>
        /// портфели
        /// </summary>
        public List<Portfolio> Portfolios { get; set; }

        /// <summary>
        /// создать портфель для тестового сервера
        /// </summary>
        private void CreatePortfolio(decimal startValue)
        {
            Portfolio portfolio = new Portfolio();
            portfolio.Number = "GodMode";
            portfolio.ValueBegin = startValue;
            portfolio.ValueBlocked = 0;
            portfolio.ValueCurrent = startValue;

            if (Portfolios == null)
            {
                Portfolios = new List<Portfolio>();
            }
            Portfolios.Add(portfolio);
        }

        /// <summary>
        /// взять портфель по номеру/названию
        /// </summary>
        public Portfolio GetPortfolioForName(string name)
        {
            return Portfolios[0];
        }

        /// <summary>
        /// изменился портфель
        /// </summary>
        public event Action<List<Portfolio>> PortfoliosChangeEvent;

// бумаги

        /// <summary>
        /// все бумаги доступные для торгов
        /// </summary>
        public List<Security> Securities
        {
            get { return _securities; }
        }
        private List<Security> _securities = new List<Security>();


        /// <summary>
        /// взять бумагу в виде класса Security по названию
        /// </summary>
        public Security GetSecurityForName(string name)
        {
            if (_securities == null)
            {
                return null;
            }

            return _securities.Find(security => security.Name == name);
        }

        /// <summary>
        /// входящие свечки из CandleManager
        /// </summary>
        void _candleManager_CandleUpdateEvent(CandleSeries series)
        {
            // перегружаем последним временем тика время сервера
            ServerTime = series.CandlesAll[series.CandlesAll.Count - 1].TimeStart;

            if (NewCandleIncomeEvent != null)
            {
                NewCandleIncomeEvent(series);
            }
        }

        /// <summary>
        /// инструменты тестера изменились
        /// </summary>
        public event Action<List<Security>> SecuritiesChangeEvent;

        /// <summary>
        /// показать инструменты 
        /// </summary>
        public void ShowSecuritiesDialog()
        {
            SecuritiesUi ui = new SecuritiesUi(this);
            ui.ShowDialog();
        }

// Заказ инструмента на скачивание

        /// <summary>
        /// серии свечек Тестера запущенные на скачивание
        /// </summary>
        private List<SecurityOptimizer> _candleSeriesTesterActivate;

        /// <summary>
        /// мастер загрузки свечек из тиков
        /// </summary>
        private CandleManager _candleManager;

        /// <summary>
        /// объект блокирующий многопоточный доступ в метод StartThisSecurity
        /// </summary>
        private object _starterLocker = new object();

        /// <summary>
        /// Начать выгрузку данных по инструменту. 
        /// </summary>
        /// <param name="namePaper">имя бумаги которую будем запускать</param>
        /// <param name="timeFrameBuilder">объект несущий в себе данные о таймФрейме</param>
        /// <returns>В случае удачи возвращает CandleSeries
        /// в случае неудачи null</returns>
        public CandleSeries StartThisSecurity(string namePaper, TimeFrameBuilder timeFrameBuilder)
        {
            lock (_starterLocker)
            {
                if (namePaper == "")
                {
                    return null;
                }
                // надо запустить сервер если он ещё отключен
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
                    if (_securities[i].Name == namePaper)
                    {
                        security = _securities[i];
                        break;
                    }
                }

                if (security == null)
                {
                    return null;
                }

                // находим бумагу

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

                _candleManager.StartSeries(series);

                SendLogMessage(OsLocalization.Market.Message14 + series.Security.Name + 
                               OsLocalization.Market.Message15 + series.TimeFrame +
                               OsLocalization.Market.Message16, LogMessageType.System);
                
                return series;
            }
        }

        /// <summary>
        /// Начать выгрузку данных по инструменту
        /// </summary>
        public CandleSeries GetCandleDataToSecurity(string namePaper, TimeFrameBuilder timeFrameBuilder, DateTime startTime,
            DateTime endTime, DateTime actualTime, bool neadToUpdate)
        {
            return StartThisSecurity(namePaper, timeFrameBuilder);
        }

        /// <summary>
        /// взять тиковые данные по инструменту за определённый период
        /// </summary>
        public bool GetTickDataToSecurity(string namePaper, DateTime startTime, DateTime endTime, DateTime actualTime,
            bool neadToUpdete)
        {
            return true;
        }

        /// <summary>
        /// взять таймФрейм в виде TimeSpan из перечисления TimeFrame
        /// </summary>
        private TimeSpan GetTimeFremeInSpan(TimeFrame frame)
        {
            TimeSpan result = new TimeSpan(0,0,1,0);

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

        /// <summary>
        /// прекратить принимать данные по бумаге 
        /// </summary>
        public void StopThisSecurity(CandleSeries series)
        {
            if (series != null)
            {
                _candleManager.StopSeries(series);
            }
        }

        /// <summary>
        /// коннекторам подключеным к серверу необходимо перезаказать данные
        /// </summary>
        public event Action NeadToReconnectEvent;

// свечи
        /// <summary>
        /// в сервере появилась новая свечка
        /// </summary>
        void TesterServer_NewCandleEvent(Candle candle, string nameSecurity, TimeSpan timeFrame, int currentCandleCount, int allCandleCount)
        {
            ServerTime = candle.TimeStart;

            if (_dataIsActive == false)
            {
                _dataIsActive = true;
            }

            if (NewBidAscIncomeEvent != null)
            {
                NewBidAscIncomeEvent(candle.Close, candle.Close,GetSecurityForName(nameSecurity));
            }

            _candleManager.SetNewCandleInSeries(candle, nameSecurity, timeFrame);

            if (TestintProgressChangeEvent != null && _lastTimeCountChange.AddMilliseconds(300) < DateTime.Now)
            {
                _lastTimeCountChange = DateTime.Now;
                TestintProgressChangeEvent(currentCandleCount, allCandleCount,NumberServer);
            }
        }

        private DateTime _lastTimeCountChange;

        /// <summary>
        /// появилась новая свеча
        /// </summary>
        public event Action<CandleSeries> NewCandleIncomeEvent;

// бид и аск

        /// <summary>
        /// обновился бид с аском
        /// </summary>
        public event Action<decimal, decimal, Security> NewBidAscIncomeEvent;

// стакан
        /// <summary>
        /// новый входящий стакан
        /// </summary>
        /// <param name="marketDepth">стакан</param>
        /// <param name="lastCount">последний индекс стакана в хранилище</param>
        /// <param name="maxCount">максимальный индекс</param>
        void TesterServer_NewMarketDepthEvent(MarketDepth marketDepth, int lastCount, int maxCount)
        {
            if (_dataIsActive == false)
            {
                _dataIsActive = true;  
            }
            
            if (NewMarketDepthEvent != null)
            {
                NewMarketDepthEvent(marketDepth);
            }

            if (TestintProgressChangeEvent != null && _lastTimeCountChange.AddMilliseconds(300) < DateTime.Now)
            {
                _lastTimeCountChange = DateTime.Now;
                TestintProgressChangeEvent(lastCount, maxCount, NumberServer);
            }
        }

        /// <summary>
        /// обновился стакан
        /// </summary>
        public event Action<MarketDepth> NewMarketDepthEvent;

// таблица всех сделок

        /// <summary>
        /// все сделки в хранилище
        /// </summary>
        private List<Trade>[] _allTrades;

        /// <summary>
        /// все тики имеющиеся у сервера
        /// </summary>
        public List<Trade>[] AllTrades { get { return _allTrades; } }

        /// <summary>
        /// пришли новые сделки из сервера
        /// </summary>
        void TesterServer_NewTradesEvent(List<Trade> tradesNew, int lastCount, int maxCount)
        {
            if (_dataIsActive == false)
            {
                _dataIsActive = true;
            }

            if (tradesNew.Count == 0)
            {
                return;
            }

            if (_allTrades == null)
            {
                _allTrades = new List<Trade>[1];
                _allTrades[0] = new List<Trade>(tradesNew);
            }
            else
            {// сортируем сделки по хранилищам

                for (int indTrade = 0; indTrade < tradesNew.Count; indTrade++)
                {
                   Trade trade = tradesNew[indTrade];
                   bool isSave = false;
                   for (int i = 0; i < _allTrades.Length; i++)
                   {
                       if (_allTrades[i] != null && _allTrades[i].Count != 0 &&
                           _allTrades[i][0].SecurityNameCode == trade.SecurityNameCode)
                       { // если для этого инструметна уже есть хранилище, сохраняем и всё
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
                   { // хранилища для инструмента нет
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

            if (tradesNew.Count == 0)
            {
                return;
            }

            ServerTime = tradesNew[tradesNew.Count - 1].Time;

            if (NewTradeEvent != null)
            {

                foreach (var trades in _allTrades)
                {
                    if (tradesNew[0].SecurityNameCode == trades[0].SecurityNameCode)
                    {
                        NewTradeEvent(trades);
                        break;
                    }
                }
            }

            if (maxCount != 0 && TestintProgressChangeEvent != null && _lastTimeCountChange.AddMilliseconds(300) < DateTime.Now)
            {
                _lastTimeCountChange = DateTime.Now;
                TestintProgressChangeEvent(lastCount, maxCount, NumberServer);
            }

            if (NewBidAscIncomeEvent != null)
            {
                NewBidAscIncomeEvent(tradesNew[tradesNew.Count - 1].Price, tradesNew[tradesNew.Count - 1].Price, GetSecurityForName(tradesNew[tradesNew.Count - 1].SecurityNameCode));
            }
        }

        /// <summary>
        /// взять все сделки по инструменту
        /// </summary>
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

        /// <summary>
        /// вызывается когда по инструменту приходят новые сделки
        /// </summary>
        public event Action<List<Trade>> NewTradeEvent;

// мои сделки

        /// <summary>
        /// входящие мои сделки
        /// </summary>
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

        /// <summary>
        /// мои сделки
        /// </summary>
        public List<MyTrade> MyTrades
        {
            get { return _myTrades; }
        }
        private List<MyTrade> _myTrades;

        /// <summary>
        /// вызывается когда приходит новая Моя Сделка
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent;

// работа по выставлению и снятию моих ордеров

        /// <summary>
        /// выставленные на биржу ордера
        /// </summary>
        private List<Order> OrdersActiv;

        /// <summary>
        /// итератор номеров ордеров на бирже
        /// </summary>
        private int _iteratorNumbersOrders;

        /// <summary>
        /// итератор номеров трэйдов на бирже
        /// </summary>
        private int _iteratorNumbersMyTrades;

        /// <summary>
        /// выставить ордер на биржу
        /// </summary>
        public void ExecuteOrder(Order order)
        {

            order.TimeCreate = ServerTime;

            if (OrdersActiv.Count != 0)
            {
                for (int i = 0; i < OrdersActiv.Count; i++)
                {
                    if (OrdersActiv[i].NumberUser == order.NumberUser)
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

            if (order.Price <= 0)
            {
                SendLogMessage(OsLocalization.Market.Message41 + order.Price, LogMessageType.Error);
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
            orderOnBoard.State = OrderStateType.Activ;
            orderOnBoard.TimeCallBack = ServerTime;
            orderOnBoard.TimeCreate = ServerTime;
            orderOnBoard.TypeOrder = order.TypeOrder;
            orderOnBoard.Volume = order.Volume;
            orderOnBoard.Comment = order.Comment;
            orderOnBoard.LifeTime = order.LifeTime;
            orderOnBoard.IsStopOrProfit = order.IsStopOrProfit;

            OrdersActiv.Add(orderOnBoard);

            if (NewOrderIncomeEvent != null)
            {
                NewOrderIncomeEvent(orderOnBoard);
            }

            if (_candleSeriesTesterActivate[0].DataType == SecurityTesterDataType.Tick)
            {
                SecurityOptimizer security = _candleSeriesTesterActivate[0];

                decimal f = order.Price/security.LastTradeSeries[security.LastTradeSeries.Count - 1].Price;
                if (f > 1.02m ||
                    f < 0.98m)
                {
                    
                }

                //CheckOrdersInTickTest(orderOnBoard, security.LastTradeSeries[security.LastTradeSeries.Count - 1].Price, toMarket);
                
            }

            if (orderOnBoard.IsStopOrProfit)
            {
                SecurityOptimizer security = _candleSeriesTesterActivate[0];
                CheckOrdersInCandleTest(order, security.LastCandle);
            }
        }

        /// <summary>
        /// отозвать ордер с биржи
        /// </summary>
        public void CanselOrder(Order order)
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                SendLogMessage(OsLocalization.Market.Message45, LogMessageType.Error);
                FailedOperationOrder(order);
                return;
            }

            CanselOnBoardOrder(order);
        }

        /// <summary>
        /// обновился ордер на бирже
        /// </summary>
        public event Action<Order> NewOrderIncomeEvent;

// внутренние операции "биржи" над ордерами

        /// <summary>
        /// провести отзыв ордера с биржи 
        /// </summary>
        private void CanselOnBoardOrder(Order order)
        {
            Order orderToClose = null;

            if (OrdersActiv.Count != 0)
            {
                for (int i = 0; i < OrdersActiv.Count; i++)
                {
                    if (OrdersActiv[i].NumberUser == order.NumberUser)
                    {
                        orderToClose = OrdersActiv[i];
                    }
                }
            }

            if (orderToClose == null)
            {
                SendLogMessage(OsLocalization.Market.Message46, LogMessageType.Error);
                FailedOperationOrder(order);
                return;
            }

            for (int i = 0; i < OrdersActiv.Count; i++)
            {
                if (OrdersActiv[i].NumberUser == order.NumberUser)
                {
                    OrdersActiv.RemoveAt(i);
                    break;
                }
            }

            orderToClose.State = OrderStateType.Cancel;

            if (NewOrderIncomeEvent != null)
            {
                NewOrderIncomeEvent(orderToClose);
            }
        }

        /// <summary>
        /// провести отбраковку ордера на бирже
        /// </summary>
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
        
        /// <summary>
        /// исполнить ордер на бирже
        /// </summary>
        private void ExecuteOnBoardOrder(Order order,decimal price, DateTime time, int slipage)
        {
            decimal realPrice = price;

            if(order.Volume == order.VolumeExecute ||
                order.State == OrderStateType.Done)
            {
                return;
            }


            if (slipage != 0)
            {
                if (order.Side == Side.Buy)
                {
                    Security mySecurity = GetSecurityForName(order.SecurityNameCode);

                    if (mySecurity != null && mySecurity.PriceStep != 0)
                    {
                        realPrice += mySecurity.PriceStep * slipage;
                    }
                }

                if (order.Side == Side.Sell)
                {
                    Security mySecurity = GetSecurityForName(order.SecurityNameCode);

                    if (mySecurity != null && mySecurity.PriceStep != 0)
                    {
                        realPrice -= mySecurity.PriceStep * slipage;
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

// работа с логами

        /// <summary>
        /// сохранить новую запись в лог
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// лог менеджер
        /// </summary>
        /// 
        private Log _logMaster;

        /// <summary>
        /// вызывается когда есть новое сообщение в логе
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }

    /// <summary>
    /// бумага в оптимизаторе
    /// Инкапсулирует данные для тестирования и методы прогрузки данных
    /// </summary>
    public class SecurityOptimizer
    {
        public SecurityOptimizer()
        {
            if (ServerMaster.GetServers() != null &&
                ServerMaster.GetServers()[0] != null)
            {
                ServerMaster.GetServers()[0].NewCandleIncomeEvent += SecurityTester_NewCandleIncomeEvent;
            }
        }

        /// <summary>
        /// в сервере новая свеча
        /// </summary>
        /// <param name="series"></param>
        void SecurityTester_NewCandleIncomeEvent(CandleSeries series)
        {
            if (series.Security.Name == Security.Name && DataType != SecurityTesterDataType.Candle ||
                series.Security.Name == Security.Name && series.TimeFrame == TimeFrame)
            {
                LastCandle = series.CandlesAll[series.CandlesAll.Count - 1];
            }
        }

        /// <summary>
        /// список свечей
        /// </summary>
        public List<Candle> Candles;

        /// <summary>
        /// список трейдов
        /// </summary>
        public List<Trade> Trades;

        /// <summary>
        /// список стаканов
        /// </summary>
        public List<MarketDepth> MarketDepths; 

        /// <summary>
        /// бумага которой принадлежит объект
        /// </summary>
        public Security Security;

        /// <summary>
        /// время начала данных в файле
        /// </summary>
        public DateTime TimeStart;

        /// <summary>
        /// время конца данных в файле
        /// </summary>
        public DateTime TimeEnd;

        /// <summary>
        /// Тип данных хранящихся в объекте
        /// </summary>
        public SecurityTesterDataType DataType;

        /// <summary>
        /// таймФрейм в TimeSpan
        /// </summary>
        public TimeSpan TimeFrameSpan;

        /// <summary>
        /// таймФрейм
        /// </summary>
        public TimeFrame TimeFrame;

        // управление выгрузгой данных

        /// <summary>
        /// активирована ли серия для выгрузки
        /// </summary>
        public bool IsActiv;

        /// <summary>
        /// очистить объект и привести к начальному, готовому к тестированию состоянию
        /// </summary>
        public void Clear()
        {
            try
            {
                LastCandle = null;
                LastTrade = null;
                LastMarketDepth = null;
                _lastTradeIndex = 0;
                _lastCandleIndex = 0;
                _lastMarketDepthIndex = 0;
            }
            catch (Exception errror)
            {
                SendLogMessage(errror.ToString(),LogMessageType.Error);
            }
        }

        /// <summary>
        /// прогрузить объект новым временем
        /// этот метод и прогружает свечи или тики
        /// </summary>
        /// <param name="now">время для синхронизации</param>
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

        // разбор файлов тиковых

        /// <summary>
        /// последний трейд инструмента из файла
        /// </summary>
        public Trade LastTrade;

        /// <summary>
        /// последний индекс трейда
        /// </summary>
        private int _lastTradeIndex;

        /// <summary>
        /// последние подгруженные тики за последнюю секунду
        /// </summary>
        public List<Trade> LastTradeSeries;

        /// <summary>
        /// проверить, не пора ли высылать новую партию тиков
        /// </summary>
        private void CheckTrades(DateTime now)
        {
            if (now > TimeEnd ||
                now < TimeStart)
            {
                return;
            }

            if (LastTrade != null &&
                LastTrade.Time > now)
            {
                return;
            }

            if (_lastTradeIndex >= Trades.Count)
            {
                return;
            }

            // качаем новый трейд

            if (LastTrade == null)
            {
                LastTrade = Trades[_lastTradeIndex];
                _lastTradeIndex++;
            }

            if (LastTrade.Time > now)
            {
                return;
            }

            // здесь имеем первый трейд в текущей секунде

            List<Trade> lastTradesSeries = new List<Trade>();


            while (_lastTradeIndex < Trades.Count)
            {
                Trade tradeN = Trades[_lastTradeIndex];
                _lastTradeIndex ++;

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

            LastTradeSeries = lastTradesSeries;

            if (NewTradesEvent != null)
            {
                NewTradesEvent(lastTradesSeries, _lastTradeIndex, Trades.Count);
            }
        }

        // разбор свечных файлов

        /// <summary>
        /// последняя свеча
        /// </summary>
        public Candle LastCandle
        {
            get { return _lastCandle; }
            set { _lastCandle = value; }
        }
        private Candle _lastCandle;

        /// <summary>
        /// последний индекс свечи в массиве
        /// </summary>
        private int _lastCandleIndex;

        /// <summary>
        /// провирить, не пора ли высылать свечку
        /// </summary>
        private void CheckCandles(DateTime now)
        {
            if (now > TimeEnd ||
                now < TimeStart)
            {
                return;
            }

            if (_lastCandleIndex >= Candles.Count)
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

                lastTradesSeries.Add(new Trade() { Price = LastCandle.Open, Volume = 1, Side = Side.Sell, Time = LastCandle.TimeStart, SecurityNameCode = Security.Name });
                lastTradesSeries.Add(new Trade() { Price = LastCandle.High, Volume = 1, Side = Side.Buy, Time = LastCandle.TimeStart, SecurityNameCode = Security.Name });
                lastTradesSeries.Add(new Trade() { Price = LastCandle.Low, Volume = 1, Side = Side.Sell, Time = LastCandle.TimeStart, SecurityNameCode = Security.Name });
                lastTradesSeries.Add(new Trade() { Price = LastCandle.Close, Volume = 1, Side = Side.Sell, Time = LastCandle.TimeStart, SecurityNameCode = Security.Name });

                if (NewTradesEvent != null)
                {
                    NewTradesEvent(lastTradesSeries,0,0);
                }

                LastCandle.State = CandleState.Finished;

                if (NewCandleEvent != null)
                {
                    NewCandleEvent(LastCandle, Security.Name, TimeFrameSpan, _lastCandleIndex,Candles.Count);
                }
                return;
            }

            while (LastCandle == null ||
                LastCandle.TimeStart < now)
            {
                LastCandle = Candles[_lastCandleIndex];
                LastCandle.State = CandleState.Finished;
                _lastCandleIndex++;
            }

            if (LastCandle.TimeStart == now)
            {
                List<Trade> lastTradesSeries = new List<Trade>();

                lastTradesSeries.Add(new Trade() { Price = LastCandle.Open, Volume = 1, Side = Side.Sell, Time = LastCandle.TimeStart, SecurityNameCode = Security.Name });
                lastTradesSeries.Add(new Trade() { Price = LastCandle.High, Volume = 1, Side = Side.Buy, Time = LastCandle.TimeStart, SecurityNameCode = Security.Name });
                lastTradesSeries.Add(new Trade() { Price = LastCandle.Low, Volume = 1, Side = Side.Sell, Time = LastCandle.TimeStart, SecurityNameCode = Security.Name });
                lastTradesSeries.Add(new Trade() { Price = LastCandle.Close, Volume = 1, Side = Side.Sell, Time = LastCandle.TimeStart, SecurityNameCode = Security.Name });

                if (NewTradesEvent != null)
                {
                    NewTradesEvent(lastTradesSeries,0,0);
                }

                LastCandle.State = CandleState.Finished;

                if (NewCandleEvent != null)
                {
                    NewCandleEvent(LastCandle, Security.Name, TimeFrameSpan, _lastCandleIndex, Candles.Count);
                }

            }
        }

        /// <summary>
        /// новые тики появились
        /// </summary>
        public event Action<List<Trade>,int, int> NewTradesEvent;

        /// <summary>
        /// новые свечи появились
        /// </summary>
        public event Action<Candle, string, TimeSpan,int,int> NewCandleEvent;

        /// <summary>
        /// новые тики появились
        /// </summary>
        public event Action<MarketDepth,int,int> NewMarketDepthEvent;

        // разбор стаканов

        /// <summary>
        /// последний трейд инструмента из файла
        /// </summary>
        public MarketDepth LastMarketDepth;

        /// <summary>
        /// последний индекс стакана в листе
        /// </summary>
        private int _lastMarketDepthIndex;

        /// <summary>
        /// подгрузить данные по стакану
        /// </summary>
        /// <param name="now">время</param>
        private void CheckMarketDepth(DateTime now)
        {
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

            if (_lastMarketDepthIndex >= MarketDepths.Count)
            {
                return;
            }

            // качаем первую секунду если 

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
                NewMarketDepthEvent(LastMarketDepth,_lastMarketDepthIndex,MarketDepths.Count);
            }
        }

        // работа с логами

        /// <summary>
        /// сохранить новую запись в лог
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message,type);
            }
        }

        /// <summary>
        /// вызывается когда есть новое сообщение в логе
        /// </summary>
        public event Action<string,LogMessageType> LogMessageEvent;

    }
}