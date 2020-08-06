/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.Kraken.KrakenEntity;
using Order = OsEngine.Entity.Order;

namespace OsEngine.Market.Servers.Kraken
{
    public class KrakenServer: IServer
    {

        //service. primary management
        //сервис. менеджмент первичных настроек

        /// <summary>
        /// constructor
        ///  конструктор
        /// </summary>
        public KrakenServer(bool neadLoadTrades)
        {
            PrivateKey = "";
            PublicKey = "";
            _neadToSaveTicks = false;
            _countDaysTickNeadToSave = 2;
            ServerType = ServerType.Kraken;
            ServerStatus = ServerConnectStatus.Disconnect;
            LoadDateType = KrakenDateType.OnlyTrades;
            LeverageType = "none";
            Load();
            LoadProxies();

            _tickStorage = new ServerTickStorage(this);
            _tickStorage.NeadToSave = NeadToSaveTicks;
            _tickStorage.DaysToLoad = CountDaysTickNeadToSave;
            _tickStorage.TickLoadedEvent += _tickStorage_TickLoadedEvent;
            _tickStorage.LogMessageEvent += SendLogMessage;

            if (neadLoadTrades)
            {
                _tickStorage.LoadTick();
            }

            _ordersToExecute = new ConcurrentQueue<Order>();
            _ordersToCansel = new ConcurrentQueue<Order>();
            _ordersToSend = new ConcurrentQueue<Order>();
            _tradesToSend = new ConcurrentQueue<List<OsEngine.Entity.Trade>>();
            _portfolioToSend = new ConcurrentQueue<List<Portfolio>>();
            _securitiesToSend = new ConcurrentQueue<List<Security>>();
            _myTradesToSend = new ConcurrentQueue<MyTrade>();
            _newServerTime = new ConcurrentQueue<DateTime>();
            _candleSeriesToSend = new ConcurrentQueue<CandleSeries>();
            _marketDepthsToSend = new ConcurrentQueue<MarketDepth>();
            _bidAskToSend = new ConcurrentQueue<BidAskSender>();

            Thread ordersExecutor = new Thread(ExecutorOrdersThreadArea);
            ordersExecutor.CurrentCulture = new CultureInfo("ru-RU");
            ordersExecutor.IsBackground = true;
            ordersExecutor.Start();

            _logMaster = new Log("KrakenServer", StartProgram.IsOsTrader);
            _logMaster.Listen(this);

            _serverStatusNead = ServerConnectStatus.Disconnect;

            _threadPrime = new Thread(PrimeThreadArea);
            _threadPrime.CurrentCulture = new CultureInfo("ru-RU");
            _threadPrime.IsBackground = true;
            _threadPrime.Start();

            Thread threadDataSender = new Thread(SenderThreadArea);
            threadDataSender.CurrentCulture = CultureInfo.InvariantCulture;
            threadDataSender.IsBackground = true;
            threadDataSender.Start();
        }

        /// <summary>
        /// public access key to Kraken
        /// публичный ключ доступа к Кракену
        /// </summary>
        public string PublicKey;

        /// <summary>
        /// private access key to Kraken
        /// приватный ключ доступа к Кракену
        /// </summary>
        public string PrivateKey;

        /// <summary>
        /// downloaded data type
        /// тип загружаемых данных
        /// </summary>
        public KrakenDateType LoadDateType
        {
            set
            {
                _loadDateType = value;
                if (_krakenClient != null)
                {
                    _krakenClient.DataType = value;
                }
            }
            get { return _loadDateType; }
        }
        private KrakenDateType _loadDateType;

        /// <summary>
        /// take server type
        /// взять тип сервера
        /// </summary>
        public ServerType ServerType { get; set; }

        public string LeverageType;

        /// <summary>
        /// show settings
        /// показать настройки
        /// </summary>
        public void ShowDialog()
        {
            if (_ui == null)
            {
                _ui = new KrakenServerUi(this, _logMaster);
                _ui.Show();
                _ui.Closing += (sender, args) => { _ui = null; };
            }
            else
            {
                _ui.Activate();
            }
        }

        /// <summary>
        /// item control window
        /// окно управления элемента
        /// </summary>
        private KrakenServerUi _ui;

        /// <summary>
        /// download settings from the file
        /// загрузить настройки из файла
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + @"KrakenServer.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"KrakenServer.txt"))
                {
                    PublicKey = reader.ReadLine();
                    PrivateKey = reader.ReadLine();
                    _countDaysTickNeadToSave = Convert.ToInt32(reader.ReadLine());
                    _neadToSaveTicks = Convert.ToBoolean(reader.ReadLine());

                    Enum.TryParse(reader.ReadLine(), out _loadDateType);
                    LoadDateType = _loadDateType;
                    LeverageType = reader.ReadLine();

                    reader.Close();
                }
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// save settings in the file
        /// сохранить настройки в файл
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"KrakenServer.txt", false))
                {
                    writer.WriteLine(PublicKey);
                    writer.WriteLine(PrivateKey);
                    writer.WriteLine(CountDaysTickNeadToSave);
                    writer.WriteLine(NeadToSaveTicks);
                    writer.WriteLine(_loadDateType);
                    writer.WriteLine(LeverageType);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }

            if (_krakenClient != null)
            {
                _krakenClient.InsertProxies(Proxies);
            }
        }

        // proxi storage
        // хранилище прокси

        public List<ProxyHolder> Proxies = new List<ProxyHolder>();

        public void LoadProxies()
        {
            if (!File.Exists(@"Engine\" + @"KrakenServerProxy.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"KrakenServerProxy.txt"))
                {
                    while (!reader.EndOfStream)
                    {
                        ProxyHolder newProxy = new ProxyHolder();
                        newProxy.LoadFromString(reader.ReadLine());
                        Proxies.Add(newProxy);
                    }
                    reader.Close();
                }
            }
            catch
            {
                // ignored
            }

            if (_krakenClient != null)
            {
                _krakenClient.InsertProxies(Proxies);
            }
        }

        public void SaveProxies(List<ProxyHolder> proxies)
        {
            Proxies = proxies;

            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"KrakenServerProxy.txt", false))
                {
                    for (int i = 0; i < proxies.Count; i++)
                    {
                        writer.WriteLine(proxies[i].GetStringToSave());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }

            if (_krakenClient != null)
            {
                _krakenClient.InsertProxies(Proxies);
            }
        }

        // tick storage
        //хранилище тиков

        /// <summary>
        /// tick storage
        /// хранилище тиков
        /// </summary>
        private ServerTickStorage _tickStorage;

        /// <summary>
        /// number of days ago, tick data to save
        /// количество дней назад, тиковые данные по которым нужно сохранять
        /// </summary>
        public int CountDaysTickNeadToSave
        {
            get { return _countDaysTickNeadToSave; }
            set
            {
                _countDaysTickNeadToSave = value;
                _tickStorage.DaysToLoad = value;
            }
        }
        private int _countDaysTickNeadToSave;

        /// <summary>
        /// whether need to save ticks
        /// нужно ли сохранять тики 
        /// </summary>
        public bool NeadToSaveTicks
        {
            get { return _neadToSaveTicks; }
            set
            {
                _neadToSaveTicks = value;
                _tickStorage.NeadToSave = value;
            }
        }
        private bool _neadToSaveTicks;

        // server status
        //статус сервера

        /// <summary>
        /// server status
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
        /// needed server status. Need a thread that monitors the connection. Depending on this field controls the connection
        /// нужный статус сервера. Нужен потоку который следит за соединением
        /// В зависимости от этого поля управляет соединением
        /// </summary>
        private ServerConnectStatus _serverStatusNead;

        /// <summary>
        /// called when server status changes
        /// вызывается когда статус соединения изменяется
        /// </summary>
        public event Action<string> ConnectStatusChangeEvent;

// connect / disconnect
//подключение / отключение

        /// <summary>
        /// start server
        /// запустить сервер
        /// </summary>
        public void StartServer()
        {
            _serverStatusNead = ServerConnectStatus.Connect;
        }

        /// <summary>
        /// stop server
        /// остановить сервер
        /// </summary>
        public void StopServer()
        {
            _serverStatusNead = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// connection established
        /// соединение установлено
        /// </summary>
        void ConnectionSucsess()
        {
            try
            {
                ServerStatus = ServerConnectStatus.Connect;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// connection lost
        /// соединение разорвано
        /// </summary>
        void ConnectionFail()
        {
            try
            {
                ServerStatus = ServerConnectStatus.Disconnect;

                if (NeadToReconnectEvent != null)
                {
                    NeadToReconnectEvent();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        //work of the connection tracking and ordering primary data
        //работа потока следящего за соединением и заказывающего первичные данные

        /// <summary>
        /// client for connection to Kraken
        /// клиент для подключения к кракену
        /// </summary>
        private KrakenServerClient _krakenClient;

        /// <summary>
        /// server time of last starting
        /// время последнего старта сервера
        /// </summary>
        public DateTime LastStartServerTime { get; set; }

        /// <summary>
        /// main thread that monitors the connection, loading portfolios and papers, sending data to up
        /// основной поток, следящий за подключением, загрузкой портфелей и бумаг, пересылкой данных на верх
        /// </summary>
        private Thread _threadPrime;

        /// <summary>
        /// place where the connection is controlled. listen to data thread 
        /// место в котором контролируется соединение.
        /// опрашиваются потоки данных
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private void PrimeThreadArea()
        {
            while (true)
            {
                Thread.Sleep(1000);

                lock (_serverLocker)
                {
                    try
                    {
                        if (_krakenClient == null)
                        {
                            CreateNewServer();
                            continue;
                        }

                        ServerConnectStatus state = ServerStatus;

                        if (state == ServerConnectStatus.Disconnect
                            && _serverStatusNead == ServerConnectStatus.Connect)
                        {
                            SendLogMessage(OsLocalization.Market.Message8, LogMessageType.System);
                            Connect();
                            continue;
                        }

                        if (state == ServerConnectStatus.Connect
                            && _serverStatusNead == ServerConnectStatus.Disconnect)
                        {
                            SendLogMessage(OsLocalization.Market.Message9, LogMessageType.System);
                            Disconnect();
                            continue;
                        }

                        if (state == ServerConnectStatus.Disconnect)
                        {
                            continue;
                        }

                        if (_candleManager == null)
                        {
                            SendLogMessage(OsLocalization.Market.Message10, LogMessageType.System);
                            StartCandleManager();
                            continue;
                        }

                        if (Portfolios == null)
                        {
                           _krakenClient.InizialazeListening();
                        }

                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                        ServerStatus = ServerConnectStatus.Disconnect;
                        Dispose(); // clear the data about the previous connector / очищаем данные о предыдущем коннекторе

                        Thread.Sleep(5000);
                        // reconnect / переподключаемся
                        _threadPrime = new Thread(PrimeThreadArea);
                        _threadPrime.CurrentCulture = new CultureInfo("us-US");
                       // _threadPrime.IsBackground = true;
                        _threadPrime.Start();
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// create new connection
        /// создать новое подключение
        /// </summary>
        private void CreateNewServer()
        {
            if (_krakenClient == null)
            {
                _krakenClient = new KrakenServerClient();
                _krakenClient.ConnectionFail += ConnectionFail;
                _krakenClient.ConnectionSucsess += ConnectionSucsess;
                _krakenClient.LogMessageEvent += SendLogMessage;
                _krakenClient.NewPortfolio += NewPortfolio;
                _krakenClient.NewMyTradeEvent += _Client_NewMyTradeEvent;
                _krakenClient.NewOrderEvent += _newOrderEvent;
                _krakenClient.NewTradeEvent += AddTick;
                _krakenClient.NewSecuritiesEvent += _krakenClient_NewSecuritiesEvent;
                _krakenClient.NewMarketDepthEvent += _krakenClient_NewMarketDepthEvent;
                _krakenClient.DataType = _loadDateType;

                _krakenClient.InsertProxies(Proxies);
            }
        }

        /// <summary>
        /// start connection process
        /// начать процесс подключения
        /// </summary>
        private void Connect()
        {
            if (string.IsNullOrWhiteSpace(PublicKey))
            {
                SendLogMessage(OsLocalization.Market.Message65, LogMessageType.Error);
                _serverStatusNead = ServerConnectStatus.Disconnect;
                return;
            }
            if (string.IsNullOrEmpty(PrivateKey))
            {
                SendLogMessage(OsLocalization.Market.Message65, LogMessageType.Error);
                _serverStatusNead = ServerConnectStatus.Disconnect;
                return;
            }

            _krakenClient.Connect(PublicKey, PrivateKey);
            LastStartServerTime = DateTime.Now;
            Thread.Sleep(5000);
        }

        /// <summary>
        /// suspend connection
        /// приостановить подключение
        /// </summary>
        private void Disconnect()
        {
            if (_krakenClient == null)
            {
                return;
            }
            _krakenClient.Disconnect();
            Thread.Sleep(5000);
        }

        /// <summary>
        /// start candle downloading
        /// запускает скачиватель свечек
        /// </summary>
        private void StartCandleManager()
        {
            if (_candleManager == null)
            {
                _candleManager = new CandleManager(this);
                _candleManager.CandleUpdateEvent += _candleManager_CandleUpdateEvent;
                _candleManager.LogMessageEvent += SendLogMessage;
            }
        }

        /// <summary>
        /// bring the program to the start. Clear all objects involved in connecting to the server
        /// привести программу к моменту запуска. Очистить все объекты участвующие в подключении к серверу
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private void Dispose()
        {
            if (_krakenClient != null)
            {
                _krakenClient.ConnectionFail -= ConnectionFail;
                _krakenClient.ConnectionSucsess -= ConnectionSucsess;
                _krakenClient.LogMessageEvent -= SendLogMessage;
                _krakenClient.NewPortfolio -= NewPortfolio;
                _krakenClient.NewMyTradeEvent -= _Client_NewMyTradeEvent;
                _krakenClient.NewOrderEvent -= _newOrderEvent;
                _krakenClient.NewTradeEvent -= AddTick;
                _krakenClient.NewSecuritiesEvent -= _krakenClient_NewSecuritiesEvent;
                _krakenClient.NewMarketDepthEvent -= _krakenClient_NewMarketDepthEvent;
            }

           try
            {
                if (_krakenClient != null && ServerStatus == ServerConnectStatus.Connect)
                {
                    _krakenClient.Disconnect();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

           _krakenClient = null;
        }

        /// <summary>
        /// multi-threaded access locker to the server
        /// блокиратор многопоточного доступа к серверу
        /// </summary>
        private object _serverLocker = new object();

        //work thread of incoming data
        //работа потока рассылки входящих данных

        /// <summary>
        /// queue of new oreders
        /// очередь новых ордеров
        /// </summary>
        private ConcurrentQueue<Order> _ordersToSend;

        /// <summary>
        /// queue of ticks
        /// очередь тиков
        /// </summary>
        private ConcurrentQueue<List<OsEngine.Entity.Trade>> _tradesToSend;

        /// <summary>
        /// queue of new portfolios
        /// очередь новых портфелей
        /// </summary>
        private ConcurrentQueue<List<Portfolio>> _portfolioToSend;

        /// <summary>
        /// queue of new instruments
        /// очередь новых инструментов
        /// </summary>
        private ConcurrentQueue<List<Security>> _securitiesToSend;

        /// <summary>
        /// queue of my new trades
        /// очередь новых моих сделок
        /// </summary>
        private ConcurrentQueue<MyTrade> _myTradesToSend;

        /// <summary>
        /// queue of new time
        /// очередь нового времени
        /// </summary>
        private ConcurrentQueue<DateTime> _newServerTime;

        /// <summary>
        /// queue of updated candle series
        /// очередь обновлённых серий свечек
        /// </summary>
        private ConcurrentQueue<CandleSeries> _candleSeriesToSend;

        /// <summary>
        /// queue of new depths
        /// очередь новых стаканов
        /// </summary>
        private ConcurrentQueue<MarketDepth> _marketDepthsToSend;

        /// <summary>
        /// queue of bid/ask updating on instrument
        /// очередь обновлений бида с аска по инструментам 
        /// </summary>
        private ConcurrentQueue<BidAskSender> _bidAskToSend;

        /// <summary>
        /// thread operation method sending out incoming data
        /// метод работы потока рассылающий входящие данные
        /// </summary>
        private void SenderThreadArea()
        {
            while (true)
            {
               try
                {
                    if (!_ordersToSend.IsEmpty)
                    {
                        Order order;
                        if (_ordersToSend.TryDequeue(out order))
                        {
                            if (NewOrderIncomeEvent != null)
                            {
                                NewOrderIncomeEvent(order);
                            }
                        }
                    }
                    else if (!_myTradesToSend.IsEmpty &&
                             (_ordersToSend.IsEmpty))
                    {
                        MyTrade myTrade;

                        if (_myTradesToSend.TryDequeue(out myTrade))
                        {
                            if (NewMyTradeEvent != null)
                            {
                                NewMyTradeEvent(myTrade);
                            }
                        }
                    }
                    else if (!_tradesToSend.IsEmpty)
                    {
                        List<OsEngine.Entity.Trade> trades;

                        if (_tradesToSend.TryDequeue(out trades))
                        {
                            if (NewTradeEvent != null)
                            {
                                NewTradeEvent(trades);
                            }
                        }
                    }

                    else if (!_portfolioToSend.IsEmpty)
                    {
                        List<Portfolio> portfolio;

                        if (_portfolioToSend.TryDequeue(out portfolio))
                        {
                            if (PortfoliosChangeEvent != null)
                            {
                                PortfoliosChangeEvent(portfolio);
                            }
                        }
                    }

                    else if (!_securitiesToSend.IsEmpty)
                    {
                        List<Security> security;

                        if (_securitiesToSend.TryDequeue(out security))
                        {
                            if (SecuritiesChangeEvent != null)
                            {
                                SecuritiesChangeEvent(security);
                            }
                        }
                    }
                    else if (!_newServerTime.IsEmpty)
                    {
                        DateTime time;

                        if (_newServerTime.TryDequeue(out time))
                        {
                            ServerTime = time;
                        }
                    }

                    else if (!_candleSeriesToSend.IsEmpty)
                    {
                        CandleSeries series;

                        if (_candleSeriesToSend.TryDequeue(out series))
                        {
                            if (NewCandleIncomeEvent != null)
                            {
                                NewCandleIncomeEvent(series);
                            }
                        }
                    }

                    else if (!_marketDepthsToSend.IsEmpty)
                    {
                        MarketDepth depth;

                        if (_marketDepthsToSend.TryDequeue(out depth))
                        {
                            if (NewMarketDepthEvent != null)
                            {
                                NewMarketDepthEvent(depth);
                            }
                        }
                    }
                    else if (!_bidAskToSend.IsEmpty)
                    {
                        BidAskSender bidAsk;

                        if (_bidAskToSend.TryDequeue(out bidAsk))
                        {
                            if (NewBidAscIncomeEvent != null)
                            {
                                NewBidAscIncomeEvent(bidAsk.Bid, bidAsk.Ask, bidAsk.Security);
                            }
                        }
                    }
                    else
                    {
                        if (MainWindow.ProccesIsWorked == false)
                        {
                            return;
                        }
                        Thread.Sleep(1);
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        // server time
        // время сервера

        /// <summary>
        /// server time
        /// время сервера
        /// </summary>
        public DateTime ServerTime
        {
            get { return _serverTime; }

            private set
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
        private DateTime _serverTime;

        /// <summary>
        /// called when the server time changes
        /// вызывается когда изменяется время сервера
        /// </summary>
        public event Action<DateTime> TimeServerChangeEvent;

// portfolios and positions
// портфели и позиции

        /// <summary>
        /// all accounts from system
        /// все счета в системе
        /// </summary>
        public List<Portfolio> Portfolios
        {
            get { return _portfolios; }
        }
        private List<Portfolio> _portfolios;

        /// <summary>
        /// take portfolio by number/name
        /// взять портфель по его номеру/имени
        /// </summary>
        public Portfolio GetPortfolioForName(string name)
        {
            try
            {
                if (_portfolios == null)
                {
                    return null;
                }
                return _portfolios.Find(portfolio => portfolio.Number == name);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }

        }

        /// <summary>
        /// called when new portfolios appear in the system
        /// вызывается когда в системе появляются новые портфели
        /// </summary>
        public event Action<List<Portfolio>> PortfoliosChangeEvent;

        /// <summary>
        /// incoming event. new portfolio
        /// входящее событие. Новый портфель
        /// </summary>
        void NewPortfolio(Portfolio portfolio)
        {
            if (portfolio == null)
            {
                return;
            }

            if (_portfolios == null)
            {
                _portfolios = new List<Portfolio>();
            }

            if (_portfolios.Find(p => p.Number == portfolio.Number) == null)
            {
                _portfolios.Add(portfolio);
            }

            _portfolioToSend.Enqueue(_portfolios);
        }

//security. Os.Engine format
//бумаги. формат Os.Engine

        /// <summary>
        /// all instruments in the system
        /// все инструменты в системе
        /// </summary>
        public List<Security> Securities
        {
            get { return _securities; }
        }
        private List<Security> _securities;

        /// <summary>
        /// take instrument as Security class by security name
        /// взять инструмент в виде класса Security, по имени инструмента 
        /// </summary>
        public Security GetSecurityForName(string name)
        {
            if (_securities == null)
            {
                return null;
            }
            return _securities.Find(securiti => securiti.Name == name);
        }

        /// <summary>
        /// incoming event: new securities
        /// входящее событие: новые бумаги
        /// </summary>
        void _krakenClient_NewSecuritiesEvent(List<Security> securities)
        {
            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            for (int i = 0; i < securities.Count; i++)
            {
                if (_securities.Find(s => s.Name == securities[i].Name) == null)
                {
                    _securities.Add(securities[i]);
                }
            }

            _securitiesToSend.Enqueue(_securities);
        }

        /// <summary>
        /// called when new instruments appear
        /// вызывается при появлении новых инструментов
        /// </summary>
        public event Action<List<Security>> SecuritiesChangeEvent;

        /// <summary>
        /// show instruments
        /// показать инструменты 
        /// </summary>
        public void ShowSecuritiesDialog()
        {
            SecuritiesUi ui = new SecuritiesUi(this);
            ui.ShowDialog();
        }

        //Subscribe to data
        //Подпись на данные

        /// <summary>
        /// master of downloading candles
        /// мастер загрузки свечек
        /// </summary>
        private CandleManager _candleManager;

        /// <summary>
        /// multi-threaded access locker in StartThisSecurity
        /// объект блокирующий многопоточный доступ в StartThisSecurity
        /// </summary>
        private object _lockerStarter = new object();

        /// <summary>
        /// start downloading data on instrument
        /// Начать выгрузку данных по инструменту. 
        /// </summary>
        /// <param name="namePaper">security name for downloading / имя бумаги которую будем запускать</param>
        /// <param name="timeFrameBuilder">object of timeframe / объект несущий </param>
        /// <returns>In case of success returns CandleSeries / В случае удачи возвращает CandleSeries
        /// in case of failure - null / в случае неудачи null</returns>
        public CandleSeries StartThisSecurity(string namePaper, TimeFrameBuilder timeFrameBuilder)
        {
            try
            {
                if (LastStartServerTime.AddSeconds(15) > DateTime.Now)
                {
                    return null;
                }

                lock (_lockerStarter)
                {
                    if (namePaper == "")
                    {
                        return null;
                    }
                    // it is necessary to start the server if it is still disabled / надо запустить сервер если он ещё отключен
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        //MessageBox.Show("Сервер не запущен. Скачивание данных прервано. Инструмент: " + namePaper);
                        return null;
                    }

                    if (_securities == null || _portfolios == null)
                    {
                        Thread.Sleep(5000);
                        return null;
                    }
                    if (LastStartServerTime != DateTime.MinValue &&
                        LastStartServerTime.AddSeconds(15) > DateTime.Now)
                    {
                        return null;
                    }

                    Security security = null;


                    for (int i = 0; _securities != null && i < _securities.Count; i++)
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
                    _tickStorage.SetSecurityToSave(security);
                    _krakenClient.ListenSecurity(namePaper);

                    // 2 create candle series / 2 создаём серию свечек
                    CandleSeries series = new CandleSeries(timeFrameBuilder, security, StartProgram.IsOsTrader);

                    _candleManager.StartSeries(series);

                    SendLogMessage(OsLocalization.Market.Label7 + series.Security.Name + OsLocalization.Market.Label10 + series.TimeFrame +
                                   OsLocalization.Market.Message16,
                        LogMessageType.System);

                    return series;

                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// start data downloading on instrument
        /// Начать выгрузку данных по инструменту
        /// </summary>
        public CandleSeries GetCandleDataToSecurity(string namePaper, TimeFrameBuilder timeFrameBuilder, DateTime startTime,
            DateTime endTime, DateTime actualTime, bool neadToUpdate)
        {
            return StartThisSecurity(namePaper, timeFrameBuilder);
        }

        /// <summary>
        /// take ticks data on istrument for period
        /// взять тиковые данные по инструменту за определённый период
        /// </summary>
        public bool GetTickDataToSecurity(string namePaper, DateTime startTime, DateTime endTime, DateTime actualTime,
            bool neadToUpdete)
        {
            return true;
        }

        /// <summary>
        /// stop instrument downloading
        /// остановить скачивание инструмента
        /// </summary>
        public void StopThisSecurity(CandleSeries series)
        {
            if (series != null)
            {
                _candleManager.StopSeries(series);
            }
        }

        /// <summary>
        /// changed candle series
        /// изменились серии свечек
        /// </summary>
        private void _candleManager_CandleUpdateEvent(CandleSeries series)
        {
            _candleSeriesToSend.Enqueue(series);
        }

        /// <summary>
        /// called when candle series changes
        /// вызывается в момент изменения серий свечек
        /// </summary>
        public event Action<CandleSeries> NewCandleIncomeEvent;

        /// <summary>
        /// connectors connected to the server need to re-order the data
        /// коннекторам подключеным к серверу необходимо перезаказать данные
        /// </summary>
        public event Action NeadToReconnectEvent;

        /// <summary>
        /// take instrument history
        /// взять историю по инструменту
        /// </summary>
        /// <param name="securityName">security name/название бумаги</param>
        /// <param name="minutesCount">amount of minutes in the candle / кол-во минут в свечке</param>
        /// <returns></returns>
        public List<Candle> GetHistory(string securityName, int minutesCount)
        {
            if (minutesCount == 2)
            {
                return null;
            }
            if (_krakenClient == null)
            {
                return null;
            }

            return _krakenClient.GetCandles(securityName, minutesCount);
        }

// depth
// стакан

        /// <summary>
        /// depth on instrument
        /// стаканы по инструментам
        /// </summary>
        private List<MarketDepth> _marketDepths = new List<MarketDepth>();

        /// <summary>
        /// take depth by security name
        /// взять стакан по названию бумаги
        /// </summary>
        public MarketDepth GetMarketDepth(string securityName)
        {
            return _marketDepths.Find(m => m.SecurityNameCode == securityName);
        }

        /// <summary>
        /// incoming event. Updated depth
        /// входящее событие. Обновился стакан
        /// </summary>
        void _krakenClient_NewMarketDepthEvent(MarketDepth marketDepth)
        {

            if (LoadDateType == KrakenDateType.AllData &&
                marketDepth.Bids.Count == 1 &&
                marketDepth.Asks.Count == 1)
            {
                return;
            }

            ServerTime = marketDepth.Time;
            _marketDepthsToSend.Enqueue(marketDepth);

            bool isInArra = false;
            for (int i = 0; i < _marketDepths.Count; i++)
            {
                if (_marketDepths[i].SecurityNameCode == marketDepth.SecurityNameCode)
                {
                    _marketDepths[i] = marketDepth;
                    isInArra = true;
                    break;
                }
            }
            if (isInArra == false)
            {
                _marketDepths.Add(marketDepth);
            }

            if (marketDepth.Asks.Count != 0 && marketDepth.Bids.Count != 0)
            {
                _bidAskToSend.Enqueue(new BidAskSender
                {
                    Ask = marketDepth.Bids[0].Price,
                    Bid = marketDepth.Asks[0].Price,
                    Security = GetSecurityForName(marketDepth.SecurityNameCode)
                });
            }
        }

// saving extended trade data
// сохранение расширенных данных по трейду

        /// <summary>
        /// upload trades by depth data
        /// прогрузить трейды данными стакана
        /// </summary>
        private void BathTradeMarketDepthData(OsEngine.Entity.Trade trade)
        {
            MarketDepth depth = _marketDepths.Find(d => d.SecurityNameCode == trade.SecurityNameCode);

            if (depth == null ||
                depth.Asks == null || depth.Asks.Count == 0 ||
                depth.Bids == null || depth.Bids.Count == 0)
            {
                return;
            }

            trade.Ask = depth.Asks[0].Price;
            trade.Bid = depth.Bids[0].Price;
            trade.BidsVolume = depth.BidSummVolume;
            trade.AsksVolume = depth.AskSummVolume;
        }

        /// <summary>
        /// called when the bid or ask for instrument is changed
        /// вызывается когда изменяется бид или аск по инструменту
        /// </summary>
        public event Action<decimal, decimal, Security> NewBidAscIncomeEvent;

        /// <summary>
        /// called when depth is changed
        /// вызывается когда изменяется стакан
        /// </summary>
        public event Action<MarketDepth> NewMarketDepthEvent;

//ticks
//тики

        /// <summary>
        /// all ticks
        /// все тики
        /// </summary>
        private List<OsEngine.Entity.Trade>[] _allTrades; 

        /// <summary>
        /// all server ticks
        /// все тики имеющиеся у сервера
        /// </summary>
        public List<OsEngine.Entity.Trade>[] AllTrades
        {
            get { return _allTrades; }
        }

        /// <summary>
        /// incoming ticks from system
        /// входящие тики из системы
        /// </summary>
        private void AddTick(OsEngine.Entity.Trade trade)
        {
            try
            {
                BathTradeMarketDepthData(trade);

                // save / сохраняем
                if (_allTrades == null)
                {
                    _allTrades = new List<OsEngine.Entity.Trade>[1];
                    _allTrades[0] = new List<OsEngine.Entity.Trade> { trade };
                }
                else
                {
                    // sort trades by storages / сортируем сделки по хранилищам
                    List<OsEngine.Entity.Trade> myList = null;
                    bool isSave = false;
                    for (int i = 0; i < _allTrades.Length; i++)
                    {
                        if (_allTrades[i] != null && _allTrades[i].Count != 0 &&
                            _allTrades[i][0].SecurityNameCode == trade.SecurityNameCode)
                        {
                            // if there is already a repository for this tool, save it / если для этого инструметна уже есть хранилище, сохраняем и всё
                            _allTrades[i].Add(trade);
                            myList = _allTrades[i];
                            isSave = true;
                            break;
                        }
                    }

                    if (isSave == false)
                    {
                        // no storage for the instrument / хранилища для инструмента нет
                        List<OsEngine.Entity.Trade>[] allTradesNew = new List<OsEngine.Entity.Trade>[_allTrades.Length + 1];
                        for (int i = 0; i < _allTrades.Length; i++)
                        {
                            allTradesNew[i] = _allTrades[i];
                        }
                        allTradesNew[allTradesNew.Length - 1] = new List<OsEngine.Entity.Trade>();
                        allTradesNew[allTradesNew.Length - 1].Add(trade);
                        myList = allTradesNew[allTradesNew.Length - 1];
                        _allTrades = allTradesNew;
                    }


                    _tradesToSend.Enqueue(myList);

                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// the ticks came from the ticks storage. Occurs immediately after loading
        /// пришли тики из хранилища тиков. Происходит сразу после загрузки
        /// </summary>
        void _tickStorage_TickLoadedEvent(List<OsEngine.Entity.Trade>[] trades)
        {
            _allTrades = trades;
        }

        /// <summary>
        /// take ticks by instrument
        /// взять тики по инструменту
        /// </summary>
        public List<OsEngine.Entity.Trade> GetAllTradesToSecurity(Security security)
        {
            if (_allTrades != null)
            {
                foreach (var tradesList in _allTrades)
                {
                    if (tradesList.Count > 1 &&
                        tradesList[0] != null &&
                        tradesList[0].SecurityNameCode == security.Name)
                    {
                        return tradesList;
                    }
                }
            }

            return new List<OsEngine.Entity.Trade>();
        }

        /// <summary>
        /// called when new trade appears
        /// вызывается в момет появления новых трейдов по инструменту
        /// </summary>
        public event Action<List<OsEngine.Entity.Trade>> NewTradeEvent;

//my trades
//мои сделки

        /// <summary>
        /// my trades
        /// мои сделки
        /// </summary>
        public List<MyTrade> MyTrades
        {
            get { return _myTrades; }
        }
        private List<MyTrade> _myTrades;
        
        /// <summary>
        /// incoming event. my new trade in the system 
        /// входящее событие. Новый мой трейд в системе
        /// </summary>
        void _Client_NewMyTradeEvent(MyTrade trade)
        {
            if (_myTrades == null)
            {
                _myTrades = new List<MyTrade>();
            }
            trade.Time = _serverTime;
            _myTrades.Add(trade);
            _myTradesToSend.Enqueue(trade);
        }

        /// <summary>
        /// called when my new trade comes
        /// вызывается когда приходит новая моя сделка
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent;

        //order execution
        //исполнение ордеров

        /// <summary>
        /// work place of thread with using queues of order execution and cancellation
        /// место работы потока на очередях исполнения заявок и их отмены
        /// </summary>
        private void ExecutorOrdersThreadArea()
        {
            while (true)
            {
                try
                {
                    if (_ordersToExecute != null && _ordersToExecute.Count != 0)
                    {
                        Order order;
                        if (_ordersToExecute.TryDequeue(out order))
                        {
                            lock (_serverLocker)
                            {
                                KrakenOrder orderKraken = new KrakenOrder();
                                orderKraken.Pair = order.SecurityNameCode;

                                if (order.Side == Side.Buy)
                                {
                                    orderKraken.Type = "buy";
                                    orderKraken.Leverage = LeverageType;
                                }
                                else
                                {
                                    orderKraken.Type = "sell";
                                    orderKraken.Leverage = LeverageType;
                                }
                                orderKraken.Price = order.Price;
                                orderKraken.OrderType = "limit";
                                orderKraken.Volume = order.Volume;
                                orderKraken.Validate = false;

                                _krakenClient.ExecuteOrder(orderKraken,order,ServerTime);

                            }
                        }
                    }
                    else if (_ordersToCansel != null && _ordersToCansel.Count != 0)
                    {
                        Order order;
                        if (_ordersToCansel.TryDequeue(out order))
                        {
                            lock (_serverLocker)
                            {
                                _krakenClient.CancelOrder(order);
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(),LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// queue of orders for placing in the system
        /// очередь ордеров для выставления в систему
        /// </summary>
        private ConcurrentQueue<Order> _ordersToExecute;

        /// <summary>
        /// queue of orders for cancellation in the system
        /// очередь ордеров для отмены в системе
        /// </summary>
        private ConcurrentQueue<Order> _ordersToCansel;

        /// <summary>
        /// orders in IB format
        /// ордера в формате IB
        /// </summary>
        private List<Order> _orders; 

        /// <summary>
        /// execute order
        /// исполнить ордер
        /// </summary>
        public void ExecuteOrder(Order order)
        {
            if (_orders == null)
            {
                _orders = new List<Order>();
            }
            _orders.Add(order);
            order.TimeCreate = ServerTime;
            _ordersToExecute.Enqueue(order);
        }

        /// <summary>
        /// cancel order
        /// отменить ордер
        /// </summary>
        public void CancelOrder(Order order)
        {
            _ordersToCansel.Enqueue(order);
        }

        void _newOrderEvent(Order order)
        {
            try
            {
                if (_orders == null)
                {
                    _orders = new List<Order>();
                }

                order.ServerType = ServerType.Kraken;
                _ordersToSend.Enqueue(order);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            } 
        }

        /// <summary>
        /// called when a new order appears in the system
        /// вызывается когда в системе появляется новый ордер
        /// </summary>
        public event Action<Order> NewOrderIncomeEvent;

//logging
//обработка лога

        /// <summary>
        /// add a new log message
        /// добавить в лог новое сообщение
        /// </summary>
        private void SendLogMessage(string message,LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// log manager
        /// менеджер лога
        /// </summary>
        private Log _logMaster;

        /// <summary>
        /// outgoing log message
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string,LogMessageType> LogMessageEvent;

    }

    /// <summary>
    /// class crutch working in the process of creating my trades
    /// класс костыль работающий в процессе создания моих трейдов
    /// </summary>
    public class MyTradeCreate
    {
        /// <summary>
        /// parent's order number
        /// номер ордера родителя
        /// </summary>
        public int idOrder;

        /// <summary>
        /// volume of parent's order in placing my trade time
        /// объём ордера родителя в момент выставления моего трейда
        /// </summary>
        public int FillOrderToCreateMyTrade;

    }

    /// <summary>
    /// data type for Kraken connector
    /// тип данных которые будет качать коннектор Кракен
    /// </summary>
    public enum KrakenDateType
    {
        /// <summary>
        /// only depths
        /// только стаканы
        /// </summary>
        OnlyMarketDepth,

        /// <summary>
        /// only trades
        /// только трейды
        /// </summary>
        OnlyTrades,

        /// <summary>
        /// all data types
        /// все типы данных
        /// </summary>
        AllData
    }
}