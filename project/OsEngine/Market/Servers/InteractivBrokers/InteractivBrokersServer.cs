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
using Order = OsEngine.Entity.Order;

namespace OsEngine.Market.Servers.InteractivBrokers
{

    /// <summary>
    /// class-server for connection to Interactive Brokers with using terminal TWS
    /// класс - сервер для подключения к Interactive Brokers через терминал TWS
    /// </summary>
    public class InteractivBrokersServer : IServer
    {

        //service. primary settings management         
        //сервис. менеджмент первичных настроек

        /// <summary>
        /// constructor
        ///  конструктор
        /// </summary>
        public InteractivBrokersServer()
        {
            Port = 7497;
            Host = "127.0.0.1";
            ClientIdInSystem = 1;
            _neadToSaveTicks = false;
            _countDaysTickNeadToSave = 2;
            ServerType = ServerType.InteractivBrokers;
            ServerStatus = ServerConnectStatus.Disconnect;

            Load();

            _tickStorage = new ServerTickStorage(this);
            _tickStorage.NeadToSave = NeadToSaveTicks;
            _tickStorage.DaysToLoad = CountDaysTickNeadToSave;
            _tickStorage.TickLoadedEvent += _tickStorage_TickLoadedEvent;
            _tickStorage.LogMessageEvent += SendLogMessage;
            _tickStorage.LoadTick();

            _ordersToExecute = new ConcurrentQueue<Order>();
            _ordersToCansel = new ConcurrentQueue<Order>();
            _ordersToSend = new ConcurrentQueue<Order>();
            _tradesToSend = new ConcurrentQueue<List<Trade>>();
            _portfolioToSend = new ConcurrentQueue<List<Portfolio>>();
            _securitiesToSend = new ConcurrentQueue<List<Security>>();
            _myTradesToSend = new ConcurrentQueue<MyTrade>();
            _newServerTime = new ConcurrentQueue<DateTime>();
            _candleSeriesToSend = new ConcurrentQueue<CandleSeries>();
            _marketDepthsToSend = new ConcurrentQueue<MarketDepth>();

            Thread ordersExecutor = new Thread(ExecutorOrdersThreadArea);
            ordersExecutor.CurrentCulture = new CultureInfo("ru-RU");
            ordersExecutor.IsBackground = true;
            ordersExecutor.Start();

            _logMaster = new Log("IbServer", StartProgram.IsOsTrader);
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

            LoadIbSecurities();
        }

        /// <summary>
        /// host of connection to IB
        /// host подключения к Ib
        /// </summary>
        public string Host;

        /// <summary>
        /// port for connection to IB
        /// порт для подключеня к Ib
        /// </summary>
        public int Port;

        /// <summary>
        /// номер клиента в системе
        /// </summary>
        public int ClientIdInSystem;

        /// <summary>
        /// take server type
        /// взять тип сервера
        /// </summary>
        public ServerType ServerType { get; set; }

        /// <summary>
        /// show settings
        /// показать настройки
        /// </summary>
        public void ShowDialog()
        {
            if (_ui == null)
            {
                _ui = new InteractivBrokersUi(this, _logMaster);
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
        private InteractivBrokersUi _ui;

        /// <summary>
        /// download settings from file
        /// загрузить настройки из файла
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + @"IbServer.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"IbServer.txt"))
                {
                    Host = reader.ReadLine();
                    Port = Convert.ToInt32(reader.ReadLine());
                    ClientIdInSystem = Convert.ToInt32(reader.ReadLine());
                    _countDaysTickNeadToSave = Convert.ToInt32(reader.ReadLine());
                    _neadToSaveTicks = Convert.ToBoolean(reader.ReadLine());

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
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"IbServer.txt", false))
                {
                    writer.WriteLine(Host);
                    writer.WriteLine(Port);
                    writer.WriteLine(ClientIdInSystem);
                    writer.WriteLine(CountDaysTickNeadToSave);
                    writer.WriteLine(NeadToSaveTicks);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        // ticks storage
        //хранилище тиков

        /// <summary>
        /// ticks storage
        /// хранилище тиков
        /// </summary>
        private ServerTickStorage _tickStorage;

        private int _countDaysTickNeadToSave;

        /// <summary>
        /// number of days ago, tick data should be saved
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

        private bool _neadToSaveTicks;

        /// <summary>
        /// shows whether need to save ticks
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

// server status
//статус сервера

        private ServerConnectStatus _serverConnectStatus;

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

        /// <summary>
        /// nessasary server status. Need a thread that monitors the connection. Depending on this field, controls the connection
        /// нужный статус сервера. Нужен потоку который следит за соединением
        /// В зависимости от этого поля управляет соединением
        /// </summary>
        private ServerConnectStatus _serverStatusNead;

        /// <summary>
        /// called when connection status changes
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
        /// connection is established
        /// соединение установлено
        /// </summary>
        void _ibClient_ConnectionSucsess()
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
        /// connection is lost
        /// соединение разорвано
        /// </summary>
        void _ibClient_ConnectionFail()
        {
            try
            {
                ServerStatus = ServerConnectStatus.Disconnect;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        // thread work controlling the connection and rdering primary data
        //работа потока следящего за соединением и заказывающего первичные данные

        private IbClient _ibClient;

        /// <summary>
        /// the main thread controlling the connection, dowloading portfolios and securities, sending data to up
        /// основной поток, следящий за подключением, загрузкой портфелей и бумаг, пересылкой данных на верх
        /// </summary>
        private Thread _threadPrime;

        /// <summary>
        /// place where the connection is controlled
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
                        if (_ibClient == null)
                        {
                            SendLogMessage("Создаём коннектор", LogMessageType.System);
                            CreateNewServerTws();
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
                            _startListening = false;
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
                            GetPortfolio();

                            continue;
                        }

                        if (Securities == null)
                        {
                            GetSecurities();
                            continue;
                        }

                        if (_neadToWatchSecurity)
                        {
                            _neadToWatchSecurity = false;
                            GetSecurities();
                            continue;
                        }

                        if (_startListening == false)
                        {
                            StartListeningPortfolios();
                            _startListening = true;
                        }
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                        ServerStatus = ServerConnectStatus.Disconnect;
                        Dispose(); // clear the data on the previous connector/очищаем данные о предыдущем коннекторе

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
        /// server time of last starting
        /// время последнего старта сервера
        /// </summary>
        public DateTime LastStartServerTime { get; set; }

        /// <summary>
        /// create new connection
        /// создать новое подключение
        /// </summary>
        private void CreateNewServerTws()
        {
            if (_ibClient == null)
            {
                _ibClient = new IbClient();
                _ibClient.ConnectionFail += _ibClient_ConnectionFail;
                _ibClient.ConnectionSucsess += _ibClient_ConnectionSucsess;
                _ibClient.LogMessageEvent += SendLogMessage;
                _ibClient.NewAccauntValue += _ibClient_NewAccauntValue;
                _ibClient.NewPortfolioPosition += _ibClient_NewPortfolioPosition;
                _ibClient.NewContractEvent += _ibClient_NewContractEvent;
                _ibClient.NewMarketDepth += _ibClient_NewMarketDepth;
                _ibClient.NewMyTradeEvent += _ibClient_NewMyTradeEvent;
                _ibClient.NewOrderEvent += _ibClient_NewOrderEvent;
                _ibClient.NewTradeEvent += AddTick; 
            }
        }

        /// <summary>
        /// start the connection process
        /// начать процесс подключения
        /// </summary>
        private void Connect()
        {
            if (string.IsNullOrWhiteSpace(Host))
            {
                SendLogMessage(OsLocalization.Market.Label49, LogMessageType.Error);
                _serverStatusNead = ServerConnectStatus.Disconnect;
                return;
            }
            if (Port <= 0)
            {
                SendLogMessage(OsLocalization.Market.Label50, LogMessageType.Error);
                _serverStatusNead = ServerConnectStatus.Disconnect;
                return;
            }
            if (ClientIdInSystem <= 0)
            {
                SendLogMessage(OsLocalization.Market.Label51, LogMessageType.Error);
                _serverStatusNead = ServerConnectStatus.Disconnect;
                return;
            }

            _ibClient.Connect(Host, Port);
            LastStartServerTime = DateTime.Now;
            Thread.Sleep(5000);
        }

        /// <summary>
        /// suspend the connection
        /// приостановить подключение
        /// </summary>
        private void Disconnect()
        {
            if (_ibClient == null)
            {
                return;
            }
            _ibClient.Disconnect();
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
        /// start securities and portfolios downloading
        /// включает загрузку инструментов и портфелей
        /// </summary>
        private void GetPortfolio()
        {
            _ibClient.GetPortfolios();
            Thread.Sleep(5000);
        }

        /// <summary>
        /// need to get new information about contract 
        /// необходимо перезаказать информацию о контрактах
        /// </summary>
        private bool _neadToWatchSecurity;

        /// <summary>
        /// take information about securities 
        /// взять информацию о бумагах
        /// </summary>
        private void GetSecurities()
        {
            if (_secIB == null ||
                _secIB.Count == 0)
            {
                SendLogMessage(OsLocalization.Market.Label52, LogMessageType.System);
                Thread.Sleep(15000);
                return;
            }

            if (_namesSubscribleSecurities == null)
            {
                _namesSubscribleSecurities = new List<string>();
            }
            for (int i = 0; i < _secIB.Count; i++)
            {
                string name = _secIB[i].Symbol + "_" + _secIB[i].SecType + "_" + _secIB[i].Exchange;
                if (_namesSubscribleSecurities.Find(s => s == name) != null)
                {
                    // if we have already subscribed to this instrument / если мы уже подписывались на данные этого инструмента
                    continue;
                }
                _namesSubscribleSecurities.Add(name);

                _ibClient.GetSecurityDetail(_secIB[i]);

                //_twsServer.reqContractDetails(_secIB[i].Symbol, _secIB[i].SecType, _secIB[i].Expiry, _secIB[i].Strike,
                    //_secIB[i].Right, _secIB[i].Multiplier, _secIB[i].Exchange, _secIB[i].Currency,0);
            }

            Thread.Sleep(5000);
        }

        /// <summary>
        /// names of the instruments on which we have already subscribed
        /// названия инструментов на которые мы уже подписались
        /// </summary>
        private List<string> _namesSubscribleSecurities;

        /// <summary>
        /// whether portfolio listening is enabled 
        /// включена ли прослушка портфеля
        /// </summary>
        private bool _startListening;

        /// <summary>
        /// start portfolio listining 
        /// включает прослушивание портфелей
        /// </summary>
        private void StartListeningPortfolios()
        {
            Thread.Sleep(3000);

            for (int i = 0; i < Portfolios.Count; i++)
            {
                _ibClient.ListenPortfolio(Portfolios[i].Number);
            }
            
            Thread.Sleep(5000);
        }

        /// <summary>
        /// bring the program to the start time. Clear all objects involved in connecting to the server
        /// привести программу к моменту запуска. Очистить все объекты участвующие в подключении к серверу
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private void Dispose()
        {
            if (_ibClient != null)
            {
             /*   _twsServer.nextValidId -= _twsServer_Connected;
                _twsServer.connectionClosed -= _twsServer_Disconnect;
                _twsServer.errMsg -= _twsServer_errMsg;
                _twsServer.accountSummary -= _twsServer_accountSummary;
                _twsServer.contractDetails -= _twsServer_contractDetails;
                _twsServer.updatePortfolio -= _twsServer_updatePortfolio;
                _twsServer.tickPrice -= _twsServer_tickPrice;
                _twsServer.tickSize -= _twsServer_tickSize;
                _twsServer.updateMktDepth -= _twsServer_updateMktDepth;
                _twsServer.orderStatus -= _twsServer_orderStatus;
                _twsServer.nextValidId -= _twsServer_nextValidId;*/
            }

           try
            {
                if (_ibClient != null && ServerStatus == ServerConnectStatus.Connect)
                {
                    _ibClient.Disconnect();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

           _ibClient = null;
        }

        /// <summary>
        /// multi-threaded access locker to server
        /// блокиратор многопоточного доступа к серверу
        /// </summary>
        private object _serverLocker = new object();

        //work of thread with incoming data 
        //работа потока рассылки входящих данных

        /// <summary>
        /// new order queue
        /// очередь новых ордеров
        /// </summary>
        private ConcurrentQueue<Order> _ordersToSend;

        /// <summary>
        /// ticks queue
        /// очередь тиков
        /// </summary>
        private ConcurrentQueue<List<Trade>> _tradesToSend;

        /// <summary>
        /// new portfolios queue
        /// очередь новых портфелей
        /// </summary>
        private ConcurrentQueue<List<Portfolio>> _portfolioToSend;

        /// <summary>
        /// new instruments queue
        /// очередь новых инструментов
        /// </summary>
        private ConcurrentQueue<List<Security>> _securitiesToSend;

        /// <summary>
        /// my new trades queue
        /// очередь новых моих сделок
        /// </summary>
        private ConcurrentQueue<MyTrade> _myTradesToSend;

        /// <summary>
        /// new time queue
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
        /// work method of thread sending the incoming data
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
                        List<Trade> trades;

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
                    else
                    {
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

        private DateTime _serverTime;

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

        /// <summary>
        /// called when server time is changed
        /// вызывается когда изменяется время сервера
        /// </summary>
        public event Action<DateTime> TimeServerChangeEvent;

// portfolios and positions
// портфели и позиции

        private List<Portfolio> _portfolios;

        /// <summary>
        /// all account in the system
        /// все счета в системе
        /// </summary>
        public List<Portfolio> Portfolios
        {
            get { return _portfolios; }
        }

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

        void _ibClient_NewAccauntValue(string account, decimal value)
        {
            try
            {
                if (_portfolios == null)
                {
                    _portfolios = new List<Portfolio>();
                }

                Portfolio myPortfolio = _portfolios.Find(portfolio => portfolio.Number == account);

                if (myPortfolio == null)
                {
                    Portfolio newpPortfolio = new Portfolio();
                    newpPortfolio.Number = account;
                    _portfolios.Add(newpPortfolio);
                    myPortfolio = newpPortfolio;
                    myPortfolio.ValueBlocked = 0;
                    SendLogMessage(OsLocalization.Market.Label53+ account, LogMessageType.System);
                }

                myPortfolio.ValueCurrent = value;

                _portfolioToSend.Enqueue(_portfolios);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        void _ibClient_NewPortfolioPosition(SecurityIb contract, string accountName, int value)
        {
            try
            {
                if (Portfolios == null ||
               Portfolios.Count == 0)
                {
                    return;
                }
                // see if you already have the right portfolio / смотрим, есть ли уже нужный портфель
                Portfolio portfolio = Portfolios.Find(portfolio1 => portfolio1.Number == accountName);

                if (portfolio == null)
                {
                    //SendLogMessage("обновляли позицию. Не можем найти портфель");
                    return;
                }

                // see if you already have the right Os.Engine security / смотрим, есть ли нужная бумага в формате Os.Engine
                string name = contract.Symbol + "_" + contract.SecType + "_" + contract.Exchange;

                if (_securities.Find(security => security.Name == name) == null)
                {
                    //SendLogMessage("обновляли позицию. Не можем найти бумагу. " + contract.Symbol);
                    return;
                }

                // update the contract position / обновляем позицию по контракту

                PositionOnBoard positionOnBoard = new PositionOnBoard();

                positionOnBoard.SecurityNameCode = name;
                positionOnBoard.PortfolioName = accountName;
                positionOnBoard.ValueCurrent = value;

                portfolio.SetNewPosition(positionOnBoard);

                _portfolioToSend.Enqueue(Portfolios);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        // security. What the user enters for the subscription. Storage and management
        // бумаги. То что пользователь вводит для подписки. Хранение и менеджмент

        /// <summary>
        /// show security settings window
        /// показать окно настроек бумаг
        /// </summary>
        public void ShowSecuritySubscribleUi()
        {
            IbContractStorageUi ui = new IbContractStorageUi(_secIB);
            ui.ShowDialog();
            _secIB = ui.SecToSubscrible;
            _neadToWatchSecurity = true;
            SaveIbSecurities();
        }

        /// <summary>
        /// security for subscription to server in the IB format
        /// бумаги для подписи у сервера в формате IB
        /// </summary>
        private List<SecurityIb> _secIB;

        /// <summary>
        /// save security for connection
        /// сохранить бумаги для подключения
        /// </summary>
        private void SaveIbSecurities()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"IbSecuritiesToWatch.txt", false))
                {
                    for (int i = 0; _secIB != null && i < _secIB.Count; i++)
                    {
                        string saveStr = "";
                        //saveStr +=  _secToSubscrible[i].ComboLegs + "@";
                        saveStr += _secIB[i].ComboLegsDescription + "@";
                        saveStr += _secIB[i].ConId + "@";
                        saveStr += _secIB[i].Currency + "@";
                        saveStr += _secIB[i].Exchange + "@";
                        saveStr += _secIB[i].Expiry + "@";
                        saveStr += _secIB[i].IncludeExpired + "@";
                        saveStr += _secIB[i].LocalSymbol + "@";
                        saveStr += _secIB[i].Multiplier + "@";
                        saveStr += _secIB[i].PrimaryExch + "@";
                        saveStr += _secIB[i].Right + "@";
                        saveStr += _secIB[i].SecId + "@";
                        saveStr += _secIB[i].SecIdType + "@";
                        saveStr += _secIB[i].SecType + "@";
                        saveStr += _secIB[i].Strike + "@";
                        saveStr += _secIB[i].Symbol + "@";
                        saveStr += _secIB[i].TradingClass + "@";
                        saveStr += _secIB[i].CreateMarketDepthFromTrades + "@";
                        //saveStr += _secToSubscrible[i].UnderComp + "@";

                        writer.WriteLine(saveStr);
                    }
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        /// <summary>
        /// upload security for connection
        /// загрузить бумаги для подключения
        /// </summary>
        private void LoadIbSecurities()
        {
            if (!File.Exists(@"Engine\" + @"IbServer.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"IbSecuritiesToWatch.txt"))
                {
                    _secIB = new List<SecurityIb>();
                    while (!reader.EndOfStream)
                    {
                        SecurityIb security = new SecurityIb();

                        string[] contrStrings = reader.ReadLine().Split('@');

                        security.ComboLegsDescription = contrStrings[0];
                        security.ConId = Convert.ToInt32(contrStrings[1]);
                        security.Currency = contrStrings[2];
                        security.Exchange = contrStrings[3];
                        security.Expiry = contrStrings[4];
                        security.IncludeExpired = Convert.ToBoolean(contrStrings[5]);
                        security.LocalSymbol = contrStrings[6];
                        security.Multiplier = contrStrings[7];
                        security.PrimaryExch = contrStrings[8];
                        security.Right = contrStrings[9];
                        security.SecId = contrStrings[10];
                        security.SecIdType = contrStrings[11];
                        security.SecType = contrStrings[12];
                        security.Strike = Convert.ToDouble(contrStrings[13]);
                        security.Symbol = contrStrings[14];
                        security.TradingClass = contrStrings[15];

                        if (contrStrings.Length > 15 &&
                            string.IsNullOrEmpty(contrStrings[16]) == false)
                        {
                            security.CreateMarketDepthFromTrades = Convert.ToBoolean(contrStrings[16]);
                        }
                        
                        _secIB.Add(security);
                    }

                    if (_secIB.Count == 0)
                    {
                        SecurityIb sec1 = new SecurityIb();
                        sec1.Symbol = "AAPL";
                        sec1.Exchange = "SMART";
                        sec1.SecType = "STK";
                        _secIB.Add(sec1);

                        SecurityIb sec2 = new SecurityIb();
                        sec2.Symbol = "FB";
                        sec2.Exchange = "SMART";
                        sec2.SecType = "STK";
                        _secIB.Add(sec2);

                        SecurityIb sec3 = new SecurityIb();
                        sec3.Symbol = "EUR";
                        sec3.Exchange = "IDEALPRO";
                        sec3.SecType = "CASH";
                        _secIB.Add(sec3);

                        SecurityIb sec4 = new SecurityIb();
                        sec4.Symbol = "GBP";
                        sec4.Exchange = "IDEALPRO";
                        sec4.SecType = "CASH";
                        _secIB.Add(sec4);
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

// security. Os.Engine format
// бумаги. формат Os.Engine

        private List<Security> _securities;

        /// <summary>
        /// all instruments in the system
        /// все инструменты в системе
        /// </summary>
        public List<Security> Securities
        {
            get { return _securities; }
        }

        /// <summary>
        /// take instrument as Security class by instrument name
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

        void _ibClient_NewContractEvent(SecurityIb contract)
        {
            try
            {
                if (_securities == null)
                {
                    _securities = new List<Security>();
                }

                SecurityIb securityIb = _secIB.Find(security => security.Symbol == contract.Symbol
                                                                        && security.Exchange == contract.Exchange);
                securityIb.Exchange = contract.Exchange;
                securityIb.Expiry = contract.Expiry;
                securityIb.LocalSymbol = contract.LocalSymbol;
                securityIb.Multiplier = contract.Multiplier;
                securityIb.Right = contract.Right;
                securityIb.ConId = contract.ConId;
                securityIb.Currency = contract.Currency;
                securityIb.Strike = contract.Strike;
                securityIb.MinTick = contract.MinTick;
                //securityIb.Symbol = symbol;
                securityIb.TradingClass = contract.TradingClass;

                //_twsServer.reqMktData(securityIb.ConId, securityIb.Symbol, securityIb.SecType, securityIb.Expiry, securityIb.Strike,
                //    securityIb.Right, securityIb.Multiplier, securityIb.Exchange, securityIb.PrimaryExch, securityIb.Currency,"",true, new TagValueList());
                //_twsServer.reqMktData2(securityIb.ConId, securityIb.LocalSymbol, securityIb.SecType, securityIb.Exchange, securityIb.PrimaryExch, securityIb.Currency, "", false, new TagValueList());
                _ibClient.GetMarketDataToSecurity(securityIb);


                string name = securityIb.Symbol + "_" + securityIb.SecType + "_" + securityIb.Exchange;

                if (_securities.Find(securiti => securiti.Name == name) == null)
                {
                    Security security = new Security();
                    security.Name = name;
                    security.NameFull = name;
                    security.NameClass = securityIb.SecType;

                    if (string.IsNullOrWhiteSpace(security.NameClass))
                    {
                        security.NameClass = "Unknown";
                    }

                    security.PriceStep = Convert.ToDecimal(securityIb.MinTick);
                    security.PriceStepCost = Convert.ToDecimal(securityIb.MinTick);
                    security.Lot = 1;
                    security.PriceLimitLow = 0;
                    security.PriceLimitHigh = 0;
                    _securities.Add(security);

                    _securitiesToSend.Enqueue(_securities);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// called when new tools appear
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

        //Data subscription
        //Подпись на данные

        /// <summary>
        /// master of candle downloading
        /// мастер загрузки свечек
        /// </summary>
        private CandleManager _candleManager;

        /// <summary>
        /// multi-threaded access locker in StartThisSecurity
        /// объект блокирующий многопоточный доступ в StartThisSecurity
        /// </summary>
        private object _lockerStarter = new object();

        private List<string> _connectedContracts;

        /// <summary>
        /// Начать выгрузку данных по инструменту. 
        /// </summary>
        /// <param name="namePaper">security name for downloading/имя бумаги которую будем запускать</param>
        /// <param name="timeFrameBuilder">objsect with timeframe / объект несущий </param>
        /// <returns>In case of success returns CandleSeries/В случае удачи возвращает CandleSeries
        /// in case of failure null / в случае неудачи null</returns>
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
                    // need to start the server if it is still disabled/надо запустить сервер если он ещё отключен
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

                    if (security == null ||
                        _secIB == null)
                    {
                        return null;
                    }

                    SecurityIb contractIb =
                        _secIB.Find(
                            contract =>
                                contract.Symbol + "_" + contract.SecType + "_" + contract.Exchange == security.Name);

                    if (contractIb == null)
                    {
                        return null;
                    }

                    if (_connectedContracts == null)
                    {
                        _connectedContracts = new List<string>();
                    }

                    if (_connectedContracts.Find(s => s == security.Name) == null)
                    {
                        _connectedContracts.Add(security.Name);
                        lock (_serverLocker)
                        {
                            _ibClient.GetMarketDepthToSecurity(contractIb);
                        }
                    }

                    _tickStorage.SetSecurityToSave(security);

                    // 2 create candles series
                    // 2 создаём серию свечек
                    CandleSeries series = new CandleSeries(timeFrameBuilder, security, StartProgram.IsOsTrader);

                    _candleManager.StartSeries(series);

                    SendLogMessage(OsLocalization.Market.Message14 + series.Security.Name + OsLocalization.Market.Message15 + series.TimeFrame +
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
        /// start downloasing instrument data
        /// Начать выгрузку данных по инструменту
        /// </summary>
        public CandleSeries GetCandleDataToSecurity(string namePaper, TimeFrameBuilder timeFrameBuilder, DateTime startTime,
            DateTime endTime, DateTime actualTime, bool neadToUpdate)
        {
            return StartThisSecurity(namePaper, timeFrameBuilder);
        }

        /// <summary>
        /// take tick data on instrument for period
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
        /// canldle series changed
        /// изменились серии свечек
        /// </summary>
        private void _candleManager_CandleUpdateEvent(CandleSeries series)
        {
            _candleSeriesToSend.Enqueue(series);
        }

        /// <summary>
        /// called when canldle series changed
        /// вызывается в момент изменения серий свечек
        /// </summary>
        public event Action<CandleSeries> NewCandleIncomeEvent;

        /// <summary>
        /// connectors connected to server need to get a new data
        /// коннекторам подключеным к серверу необходимо перезаказать данные
        /// </summary>
        public event Action NeadToReconnectEvent;

        // depth
        // стакан

        /// <summary>
        /// depth by instruments
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

        // saving extended trade data
        // сохранение расширенных данных по трейду

        /// <summary>
        /// load trades with depth data
        /// прогрузить трейды данными стакана
        /// </summary>
        private void BathTradeMarketDepthData(Trade trade)
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
        /// all depths
        /// все стаканы
        /// </summary>
        private List<MarketDepth> _depths = new List<MarketDepth>();

        void _ibClient_NewMarketDepth(int id, int position, int operation, int side, decimal price, int size)
        {

            try
            {
                // take all the necessary data / берём все нужные данные
                SecurityIb myContract = _secIB.Find(contract => contract.ConId == id);

                if (myContract == null)
                {
                    return;
                }

                if (position > 10)
                {
                    return;
                }

                string name = myContract.Symbol + "_" + myContract.SecType + "_" + myContract.Exchange;

                Security mySecurity = Securities.Find(security => security.Name == name);

                if (mySecurity == null)
                {
                    return;
                }

                if (_depths == null)
                {
                    _depths = new List<MarketDepth>();
                }

                MarketDepth myDepth = _depths.Find(depth => depth.SecurityNameCode == name);
                if (myDepth == null)
                {
                    myDepth = new MarketDepth();
                    myDepth.SecurityNameCode = name;
                    _depths.Add(myDepth);
                }

                myDepth.Time = DateTime.Now;

                Side sideLine;
                if (side == 1)
                { // ask/аск
                    sideLine = Side.Buy;
                }
                else
                { // bid/бид
                    sideLine = Side.Sell;
                }

                List<MarketDepthLevel> bids  = myDepth.Bids;
                List<MarketDepthLevel> asks = myDepth.Asks;

                if (asks == null || asks.Count < 10)
                {
                    asks = new List<MarketDepthLevel>();
                    bids = new List<MarketDepthLevel>();

                    for (int i = 0; i < 10; i++)
                    {
                        asks.Add(new MarketDepthLevel());
                        bids.Add(new MarketDepthLevel());
                    }
                    myDepth.Bids = bids;
                    myDepth.Asks = asks;
                }

                if (operation == 2)
                {// if need to remove / если нужно удалить

                    if (sideLine == Side.Buy)
                    {
                        // asks.RemoveAt(position);
                        MarketDepthLevel level = bids[position];
                        level.Ask = 0;
                        level.Bid = 0;
                        level.Price = 0;
                    }
                    else if (sideLine == Side.Sell)
                    {
                        //bids.RemoveAt(position);
                        MarketDepthLevel level = asks[position];
                        level.Ask = 0;
                        level.Bid = 0;
                        level.Price = 0;
                    }
                }
                /*else if (operation == 1)
                { // нужно вставить
                    if (sideLine == Side.Buy)
                    {
                        MarketDepthLevel level = new MarketDepthLevel();
                        level.Bid = 0;
                        level.Ask = Convert.ToDecimal(size);
                        level.Price = Convert.ToDecimal(price);
                        asks.Insert(position,level);
                    }
                    else if (sideLine == Side.Sell)
                    {
                        MarketDepthLevel level = new MarketDepthLevel();
                        level.Bid = Convert.ToDecimal(size);
                        level.Ask = 0;
                        level.Price = Convert.ToDecimal(price);
                        bids.Insert(position,level);
                    }
                    if (asks.Count > 10)
                    {
                        asks.RemoveAt(asks.Count - 1);
                    }
                    if (bids.Count > 10)
                    {
                        bids.RemoveAt(bids.Count - 1);
                    }
                }*/
                else if (operation == 0 || operation == 1)
                { // need to update / нужно обновить
                    if (sideLine == Side.Buy)
                    {
                        MarketDepthLevel level = bids[position];
                        level.Bid = Convert.ToDecimal(size);
                        level.Ask = 0;
                        level.Price = price;
                    }
                    else if (sideLine == Side.Sell)
                    {
                        MarketDepthLevel level = asks[position];
                        level.Bid = 0;
                        level.Ask = Convert.ToDecimal(size);
                        level.Price = price;
                    }
                }

                if (myDepth.Bids[0].Price != 0 &&
                    myDepth.Asks[0].Price != 0)
                {
                    MarketDepth copy = myDepth.GetCopy();
                    _marketDepthsToSend.Enqueue(copy);

                    // load depths in storage / грузим стаканы в хранилище
                    for (int i = 0; i < _marketDepths.Count; i++)
                    {
                        if (_marketDepths[i].SecurityNameCode == copy.SecurityNameCode)
                        {
                            _marketDepths[i] = copy;
                            return;
                        }
                    }
                    _marketDepths.Add(copy);

                    if (NewBidAscIncomeEvent != null &&
                        copy.Bids.Count != 0 && copy.Asks.Count != 0)
                    {
                        NewBidAscIncomeEvent(copy.Bids[0].Price, copy.Asks[0].Price, mySecurity);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// called when bid or ask changes over the instrument
        /// вызывается когда изменяется бид или аск по инструменту
        /// </summary>
        public event Action<decimal, decimal, Security> NewBidAscIncomeEvent;

        /// <summary>
        /// called when depth changes
        /// вызывается когда изменяется стакан
        /// </summary>
        public event Action<MarketDepth> NewMarketDepthEvent;

//ticks
//тики

        /// <summary>
        /// all ticks
        /// все тики
        /// </summary>
        private List<Trade>[] _allTrades; 

        /// <summary>
        /// all server ticks
        /// все тики имеющиеся у сервера
        /// </summary>
        public List<Trade>[] AllTrades
        {
            get { return _allTrades; }
        }

        private void SendMdFromTrade(Trade trade)
        {
            MarketDepth myDepth = _depths.Find(depth => depth.SecurityNameCode == trade.SecurityNameCode);

            if (myDepth == null)
            {
                myDepth = new MarketDepth();
                myDepth.SecurityNameCode = trade.SecurityNameCode;
                _depths.Add(myDepth);
            }

            myDepth.Time = DateTime.Now;

            Security mySecurity = Securities.Find(security => security.Name == myDepth.SecurityNameCode);

            if (mySecurity == null)
            {
                return;
            }

            List<MarketDepthLevel> bids = myDepth.Bids;
            List<MarketDepthLevel> asks = myDepth.Asks;

            if (asks == null || asks.Count == 0)
            {
                asks = new List<MarketDepthLevel>();
                bids = new List<MarketDepthLevel>();

                asks.Add(new MarketDepthLevel()); 
                bids.Add(new MarketDepthLevel());
                
                myDepth.Bids = bids;
                myDepth.Asks = asks;
            }

            if (myDepth.Bids.Count > 1 &&
                myDepth.Asks.Count > 1)
            {
                return;
            }

            myDepth.Asks[0].Price = trade.Price + mySecurity.PriceStep;
            myDepth.Bids[0].Price = trade.Price - mySecurity.PriceStep;

            myDepth.Asks[0].Ask = 1;
            myDepth.Bids[0].Bid = 1;

            if (NewMarketDepthEvent != null)
            {
                NewMarketDepthEvent(myDepth.GetCopy());
            }
        }

        /// <summary>
        /// incoming ticks from the system
        /// входящие тики из системы
        /// </summary>
        private void AddTick(Trade trade, SecurityIb sec)
        {
            try
            {
                if (trade.Price <= 0)
                {
                    return;
                }

                SecurityIb contractIb =
                    _secIB.Find(
                        contract =>
                            contract.ConId == sec.ConId);

                if (contractIb != null && contractIb.CreateMarketDepthFromTrades)
                {
                    SendMdFromTrade(trade);
                }

                BathTradeMarketDepthData(trade);
                ServerTime = trade.Time;
                // save/сохраняем
                if (_allTrades == null)
                {
                    _allTrades = new List<Trade>[1];
                    _allTrades[0] = new List<Trade> {trade};
                }
                else
                {
                    // sort trades by storages / сортируем сделки по хранилищам
                    List<Trade> myList = null;
                    bool isSave = false;
                    for (int i = 0; i < _allTrades.Length; i++)
                    {
                        if (_allTrades[i] != null && _allTrades[i].Count != 0 &&
                            _allTrades[i][0].SecurityNameCode == trade.SecurityNameCode)
                        {
                            // if there is already storage for this instrument, we save/ если для этого инструметна уже есть хранилище, сохраняем и всё
                            _allTrades[i].Add(trade);
                            myList = _allTrades[i];
                            isSave = true;
                            break;
                        }
                    }

                    if (isSave == false)
                    {
                        // there is no storage for the instrument / хранилища для инструмента нет
                        List<Trade>[] allTradesNew = new List<Trade>[_allTrades.Length + 1];
                        for (int i = 0; i < _allTrades.Length; i++)
                        {
                            allTradesNew[i] = _allTrades[i];
                        }
                        allTradesNew[allTradesNew.Length - 1] = new List<Trade>();
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
        /// ticks came from the tick storege. Occurs immediately after loading.
        /// пришли тики из хранилища тиков. Происходит сразу после загрузки
        /// </summary>
        void _tickStorage_TickLoadedEvent(List<Trade>[] trades)
        {
            _allTrades = trades;
        }

        /// <summary>
        /// take ticks by instrument
        /// взять тики по инструменту
        /// </summary>
        public List<Trade> GetAllTradesToSecurity(Security security)
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

            return new List<Trade>();
        }

        /// <summary>
        /// called when new trades appear by the instrument
        /// вызывается в момет появления новых трейдов по инструменту
        /// </summary>
        public event Action<List<Trade>> NewTradeEvent;

// my trades
//мои сделки

        private List<MyTrade> _myTrades;

        /// <summary>
        /// my trades
        /// мои сделки
        /// </summary>
        public List<MyTrade> MyTrades
        {
            get { return _myTrades; }
        }

        void _ibClient_NewMyTradeEvent(MyTrade trade)
        {
            if (_myTrades == null)
            {
                _myTrades = new List<MyTrade>();
            }
            _myTrades.Add(trade);
            _myTradesToSend.Enqueue(trade);
        }

        /// <summary>
        /// called when my new deal comes
        /// вызывается когда приходит новая моя сделка
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent;

        //execution of orders
        //исполнение ордеров

        /// <summary>
        /// place of work thread on the queues of order execution and cancellation
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

                                SecurityIb contractIb =
                                    _secIB.Find(
                                        contract =>
                                            contract.Symbol + "_" + contract.SecType + "_" + contract.Exchange ==
                                            order.SecurityNameCode);

                                if (contractIb == null)
                                {
                                    return;
                                }

                                if (contractIb.MinTick < 1)
                                {
                                    int decimals = 0;
                                    decimal minTick = Convert.ToDecimal(contractIb.MinTick);

                                    while (true)
                                    {
                                        minTick = minTick*10;

                                        decimals++;

                                        if (minTick > 1)
                                        {
                                            break;
                                        }
                                    }

                                    while (true)
                                    {
                                        if (order.Price%Convert.ToDecimal(contractIb.MinTick) != 0)
                                        {
                                            string minusVal = "0.";

                                            for (int i = 0; i < decimals-1; i++)
                                            {
                                                minusVal += "0";
                                            }
                                            minusVal += "1";
                                            order.Price -= minusVal.ToDecimal();
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                }


                                _ibClient.ExecuteOrder(order, contractIb);
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
                                _ibClient.CanselOrder(order);
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
        /// queue of orders for cancelling from the system 
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
        public void CanselOrder(Order order)
        {
            _ordersToCansel.Enqueue(order);
        }

        void _ibClient_NewOrderEvent(Order order)
        {
            try
            {
                if (_orders == null)
                {
                    _orders = new List<Order>();
                }

                Order osOrder = _orders.Find(order1 => order1.NumberMarket == order.NumberMarket);

                if (osOrder == null)
                {
                    return;
                }

                if (order.State == OrderStateType.Fail)
                {
                    osOrder.State = OrderStateType.Fail;
                }

                _ordersToSend.Enqueue(osOrder);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            } 
        }

        /// <summary>
        /// called when new order appears in the system
        /// вызывается когда в системе появляется новый ордер
        /// </summary>
        public event Action<Order> NewOrderIncomeEvent;

// logging
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
    /// crutch class working in the process of creating my trades
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
        /// parent's order volume at the time of my trade
        /// объём ордера родителя в момент выставления моего трейда
        /// </summary>
        public decimal FillOrderToCreateMyTrade;

    }

    /// <summary>
    /// security in IB format
    /// бумага в представлении Ib
    /// </summary>
    public class SecurityIb
    {
        /// <summary>
        /// создавать для этой бумаги бид с аском по последнему трейду
        /// и не ждать стакана
        /// </summary>
        public bool CreateMarketDepthFromTrades;

        /// <summary>
        /// number
        /// номер
        /// </summary>
        public int ConId;

        /// <summary>
        /// full name
        /// название полное
        /// </summary>
        public string Symbol;

        /// <summary>
        /// name
        /// название
        /// </summary>
        public string LocalSymbol;

        /// <summary>
        /// contract currency
        /// валюта контракта
        /// </summary>
        public string Currency;

        /// <summary>
        /// exchange
        /// биржа
        /// </summary>
        public string Exchange;

        /// <summary>
        /// main exchange
        /// основная биржа
        /// </summary>
        public string PrimaryExch;

        /// <summary>
        /// strike
        /// страйк
        /// </summary>
        public double Strike;

        /// <summary>
        /// instrument class
        /// класс инструмента
        /// </summary>
        public string TradingClass;

        /// <summary>
        /// minimum price step
        /// минимальный шаг цены
        /// </summary>
        public double MinTick;

        /// <summary>
        /// multiplier
        /// мультипликатор?
        /// </summary>
        public string Multiplier;

        public string Expiry;

        public bool IncludeExpired;

        public string ComboLegsDescription;

        public string Right;

        public string SecId;

        public string SecIdType;

        public string SecType;
       
    }
}