/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.OsMiner.Patterns;

namespace OsEngine.Market.Servers.AstsBridge
{
    public class AstsBridgeServer : IServer
    {

        /// <summary>
        /// конструктор
        /// </summary>
        public AstsBridgeServer(bool neadToLoadTicks)
        {
            ServerAdress = "91.208.232.211";

            ServerStatus = ServerConnectStatus.Disconnect;
            ServerType = ServerType.AstsBridge;
            Dislocation = AstsDislocation.Internet;

            _countDaysTickNeadToSave = 3;
            _neadToSaveTicks = true;

            Load();

            _logMaster = new Log("AstsBridgeServer", StartProgram.IsOsTrader);
            _logMaster.Listen(this);

            _serverStatusNead = ServerConnectStatus.Disconnect;

            _threadPrime = new Thread(PrimeThreadArea);
            _threadPrime.CurrentCulture = new CultureInfo("ru-RU");
            _threadPrime.IsBackground = true;
            _threadPrime.Start();

            Thread threadDataSender = new Thread(SenderThreadArea);
            threadDataSender.CurrentCulture = new CultureInfo("ru-RU");
            threadDataSender.IsBackground = true;
            threadDataSender.Start();

            _ordersToSend = new ConcurrentQueue<Order>();
            _tradesToSend = new ConcurrentQueue<List<Trade>>();
            _portfolioToSend = new ConcurrentQueue<List<Portfolio>>();
            _securitiesToSend = new ConcurrentQueue<List<Security>>();
            _myTradesToSend = new ConcurrentQueue<MyTrade>();
            _newServerTime = new ConcurrentQueue<DateTime>();
            _candleSeriesToSend = new ConcurrentQueue<CandleSeries>();
            _marketDepthsToSend = new ConcurrentQueue<MarketDepth>();
            _bidAskToSend = new ConcurrentQueue<BidAskSender>();
            _levelOneToSend = new ConcurrentQueue<SecurityLevelOne>();
            _tradesTableToSend = new ConcurrentQueue<List<Trade>>();

            if (neadToLoadTicks)
            {
                _tickStorage = new ServerTickStorage(this);
                _tickStorage.NeadToSave = NeadToSaveTicks;
                _tickStorage.DaysToLoad = CountDaysTickNeadToSave;
                _tickStorage.TickLoadedEvent += _tickStorage_TickLoadedEvent;
                _tickStorage.LogMessageEvent += SendLogMessage;
                _tickStorage.LoadTick();
            }
        }

        /// <summary>
        /// взять тип сервера
        /// </summary>
        /// <returns></returns>
        public ServerType ServerType { get; set; }

        /// <summary>
        /// показать окно настроект
        /// </summary>
        public void ShowDialog()
        {
            if (_ui == null)
            {
                _ui = new AstsServerUi(this, _logMaster);
                _ui.Show();
                _ui.Closing += (sender, args) => { _ui = null; };
            }
            else
            {
                _ui.Activate();
            }
            
        }

        /// <summary>
        /// окно управления элемента
        /// </summary>
        private AstsServerUi _ui;

        /// <summary>
        /// адрес сервера по которому нужно соединяться с сервером
        /// </summary>
        public string ServerAdress;

        /// <summary>
        /// имя сервера доступа
        /// </summary>
        public string ServerName;

        /// <summary>
        /// имя сервиса доступа
        /// </summary>
        public string ServiseName;

        /// <summary>
        /// логин пользователя для доступа к СмартКом
        /// </summary>
        public string UserLogin;

        /// <summary>
        /// пароль пользователя для доступа к СмартКом
        /// </summary>
        public string UserPassword;

        /// <summary>
        /// код клиента
        /// </summary>
        public string ClientCode
        {
            get { return _clientCode; }
            set
            {
                _clientCode = value;

                if (AstsServer != null)
                {
                    AstsServer.ClientCode = value;
                }
            }
        }

        private string _clientCode;

        /// <summary>
        /// расположение бота
        /// </summary>
        public AstsDislocation Dislocation;

        private int _countDaysTickNeadToSave;

        /// <summary>
        /// количество дней назад, тиковые данные по которым нужно сохранять
        /// </summary>
        public int CountDaysTickNeadToSave
        {
            get { return _countDaysTickNeadToSave; }
            set
            {
                if (_tickStorage == null)
                {
                    return;
                }
                _countDaysTickNeadToSave = value;
                _tickStorage.DaysToLoad = value;
                Save();
            }
        }

        private bool _neadToSaveTicks;

        /// <summary>
        /// нужно ли сохранять тики 
        /// </summary>
        public bool NeadToSaveTicks
        {
            get { return _neadToSaveTicks; }
            set
            {
                if (_tickStorage == null)
                {
                    return;
                }
                _neadToSaveTicks = value;
                _tickStorage.NeadToSave = value;
                Save();
            }
        }

        /// <summary>
        /// загрузить настройки сервера из файла
        /// </summary>
        public void Load()
        {
            if (!File.Exists(@"Engine\" + @"AstsServer.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"AstsServer.txt"))
                {
                    ServerAdress = reader.ReadLine();
                    UserLogin = reader.ReadLine();
                    UserPassword = reader.ReadLine();
                    ServerName = reader.ReadLine();
                    ServiseName = reader.ReadLine();
                    Enum.TryParse(reader.ReadLine(), out Dislocation);
                    _countDaysTickNeadToSave = Convert.ToInt32(reader.ReadLine());
                    _neadToSaveTicks = Convert.ToBoolean(reader.ReadLine());
                    _clientCode = reader.ReadLine();
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        /// <summary>
        /// сохранить настройки сервера в файл
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"AstsServer.txt", false))
                {
                    writer.WriteLine(ServerAdress);
                    writer.WriteLine(UserLogin);
                    writer.WriteLine(UserPassword);
                    writer.WriteLine(ServerName);
                    writer.WriteLine(ServiseName);
                    writer.WriteLine(Dislocation);
                    writer.WriteLine(CountDaysTickNeadToSave);
                    writer.WriteLine(NeadToSaveTicks);
                    writer.WriteLine(_clientCode);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private ServerTickStorage _tickStorage;

// статус сервера

        private ServerConnectStatus _serverConnectStatus;

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

        /// <summary>
        /// вызывается когда статус соединения изменяется
        /// </summary>
        public event Action<string> ConnectStatusChangeEvent;

// подключение / отключение

        /// <summary>
        /// запустить сервер СмартКом
        /// </summary>
        public void StartServer()
        {
            _serverStatusNead = ServerConnectStatus.Connect;
        }

        /// <summary>
        /// остановить сервер СмартКом
        /// </summary>
        public void StopServer()
        {
            _serverStatusNead = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// нужный статус сервера. Нужен потоку который следит за соединением
        /// В зависимости от этого поля управляет соединением
        /// </summary>
        private ServerConnectStatus _serverStatusNead;

        /// <summary>
        /// пришло оповещение от СмартКом, что соединение разорвано
        /// </summary>
        private void Disconnected(string reason)
        {
            ServerStatus = ServerConnectStatus.Disconnect;
            SendLogMessage( reason, LogMessageType.System);
        }

        /// <summary>
        /// пришло оповещение от СмартКом, что соединение установлено
        /// </summary>
        private void Connected()
        {
            ServerStatus = ServerConnectStatus.Connect;
        }

// работа основного потока !!!!!!

        /// <summary>
        /// основной поток, следящий за подключением, загрузкой портфелей и бумаг, пересылкой данных на верх
        /// </summary>
        private Thread _threadPrime;

        /// <summary>
        /// место в котором контролируется соединение.
        /// опрашиваются потоки данных
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private void PrimeThreadArea()
        {
            DateTime timeLastProcess = DateTime.MinValue;

            while (true)
            {
                Thread.Sleep(3);
                lock (_serverLocker)
                {
                    try
                    {
                        if (AstsServer == null)
                        {
                            CreateNewServer();
                            continue;
                        }

                        bool stateIsActiv = AstsServer.IsConnected;

                        if (stateIsActiv == false && _serverStatusNead == ServerConnectStatus.Connect)
                        {
                            SendLogMessage(OsLocalization.Market.Message8, LogMessageType.System);
                            Connect();
                            continue;
                        }

                        if (stateIsActiv && _serverStatusNead == ServerConnectStatus.Disconnect)
                        {
                            SendLogMessage(OsLocalization.Market.Message9, LogMessageType.System);
                            Disconnect();
                            continue;
                        }

                        if (stateIsActiv == false)
                        {
                            continue;
                        }

                        if (_candleManager == null)
                        {
                            StartCandleManager();
                            continue;
                        }

                        if (_metaDataIsExist == false)
                        {
                            AstsServer.GetStructureData();
                            _metaDataIsExist = true;
                        }

                        if (_getPortfoliosAndSecurities == false)
                        {
                            AstsServer.OpenTablesInFirstTime();
                            _getPortfoliosAndSecurities = true;
                            continue;
                        }

                        if (Portfolios == null || Securities == null)
                        {
                          _getPortfoliosAndSecurities = false;
                            Thread.Sleep(10000);
                            Disconnect();
                        }
                        if (Dislocation == AstsDislocation.Colo)
                        {
                            AstsServer.Process();
                        }
                        else
                        {
                            if (timeLastProcess == DateTime.MinValue ||
                                timeLastProcess.AddMilliseconds(100) < DateTime.Now)
                            {
                                AstsServer.Process();
                                timeLastProcess = DateTime.Now;
                            }
                        }
                        

                    }
                    catch (Exception error)
                    {

                        SendLogMessage(error.ToString(), LogMessageType.Error);
                        ServerStatus = ServerConnectStatus.Disconnect;
                        Dispose(); // очищаем данные о предыдущем коннекторе

                        Thread.Sleep(5000);
                        // переподключаемся
                        _threadPrime = new Thread(PrimeThreadArea);
                        _threadPrime.CurrentCulture = new CultureInfo("ru-RU");
                        _threadPrime.IsBackground = true;
                        _threadPrime.Start();

                        if (NeadToReconnectEvent != null)
                        {
                            NeadToReconnectEvent();
                        }

                        return;
                    }
                }
            }
        }

        /// <summary>
        /// время последнего старта сервера
        /// </summary>
        private DateTime _lastStartServerTime = DateTime.MinValue;

        /// <summary>
        /// запросили ли мы уже данные по структурам таблиц
        /// </summary>
        private bool _metaDataIsExist;

        /// <summary>
        /// скачаны ли портфели и бумаги
        /// </summary>
        private bool _getPortfoliosAndSecurities;

        /// <summary>
        /// создать новое подключение
        /// </summary>
        private void CreateNewServer()
        {
            if (AstsServer == null)
            {
                AstsServer = new AstsBridgeWrapper(); // Создать и назначить обработчики событий
                AstsServer.ConnectedEvent += Connected;
                AstsServer.DisconnectedEvent += Disconnected;
                AstsServer.NewSecurityEvent += NewSecurityEvent;
                AstsServer.PortfolioUpdateEvent += AddPortfolio;
                AstsServer.NewTradesEvent += NewTradesEvent;
                AstsServer.NewMyTradeEvent += AddMyTrade;
                AstsServer.MarketDepthUpdateEvent += UpdateBidAsk;
                AstsServer.OrderUpdateEvent += UpdateOrder;
                AstsServer.OrderFailedEvent += OrderFailedEvent;
                AstsServer.LogMessageEvent += SendLogMessage;
                AstsServer.SecurityMoexUpdateEvent +=LevelOneUpdateEvent;
                AstsServer.ClientCode = ClientCode;
            }
        }

        /// <summary>
        /// начать процесс подключения
        /// </summary>
        private void Connect()
        {
            StringBuilder settings = new StringBuilder();
            /*HOST={ip1:port1,ip2:port2,...}
              SERVER={te_access_point_id}
              USERID={te_userid}
              PASSWORD={te_password}
              INTERFACE={te_interface_id}
              FEEDBACK={contact_info}*/

            if (!Directory.Exists(Application.StartupPath + "\\AstsBridge"))
            {
                Directory.CreateDirectory(Application.StartupPath + "\\AstsBridge");
            }

             settings.Append("HOST=" + ServerAdress + "\r\n");
             settings.Append("PREFERREDHOST=" + ServerAdress + "\r\n");
             settings.Append("SERVER=" + ServerName + "\r\n");
             settings.Append("SERVICE=" + ServiseName + "\r\n");
             settings.Append("USERID=" + UserLogin + "\r\n");
             settings.Append("PASSWORD=" + UserPassword + "\r\n");
             settings.Append("INTERFACE=" + "IFCBroker_26" + "\r\n");
             settings.Append("COMPRESSION=" + "0" + "\r\n");
             settings.Append("LOGFOLDER=" + Application.StartupPath + "\\AstsBridge"+ "\r\n");
             settings.Append("LOGGING=" + "2,2" + "\r\n");

             AstsServer.Connect(settings);

            _lastStartServerTime = DateTime.Now;
            Thread.Sleep(10000);
        }

        /// <summary>
        /// приостановить подключение
        /// </summary>
        private void Disconnect()
        {
            AstsServer.Disconnect();
            Thread.Sleep(5000);
        }

        /// <summary>
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
        /// привести программу к моменту запуска. Очистить все объекты участвующие в подключении к серверу
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private void Dispose()
        {
            try
            {
                if (AstsServer != null && AstsServer.IsConnected)
                {
                    AstsServer.Disconnect();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            if (AstsServer != null)
            {
                AstsServer.ConnectedEvent -= Connected;
                AstsServer.DisconnectedEvent -= Disconnected;
                AstsServer.NewSecurityEvent -= NewSecurityEvent;
                AstsServer.PortfolioUpdateEvent -= AddPortfolio;
                AstsServer.NewTradesEvent -= NewTradesEvent;
                AstsServer.NewMyTradeEvent -= AddMyTrade;
                AstsServer.MarketDepthUpdateEvent -= UpdateBidAsk;
                AstsServer.OrderUpdateEvent -= UpdateOrder;
                AstsServer.OrderFailedEvent -= OrderFailedEvent;
                AstsServer.SecurityMoexUpdateEvent -= LevelOneUpdateEvent;
            }

            AstsServer = null;

            _getPortfoliosAndSecurities = false;
        }

        /// <summary>
        /// блокиратор многопоточного доступа к серверу СмартКом
        /// </summary>
        private object _serverLocker = new object();

        /// <summary>
        /// Сервер
        /// </summary>
        public AstsBridgeWrapper AstsServer;

// работа потока рассылки !!!!!

        /// <summary>
        /// очередь новых ордеров
        /// </summary>
        private ConcurrentQueue<Order> _ordersToSend;

        /// <summary>
        /// очередь тиков
        /// </summary>
        private ConcurrentQueue<List<Trade>> _tradesToSend;

        /// <summary>
        /// очередь новых портфелей
        /// </summary>
        private ConcurrentQueue<List<Portfolio>> _portfolioToSend;

        /// <summary>
        /// очередь новых инструментов
        /// </summary>
        private ConcurrentQueue<List<Security>> _securitiesToSend;

        /// <summary>
        /// очередь новых моих сделок
        /// </summary>
        private ConcurrentQueue<MyTrade> _myTradesToSend;

        /// <summary>
        /// очередь нового времени
        /// </summary>
        private ConcurrentQueue<DateTime> _newServerTime;

        /// <summary>
        /// очередь обновлённых серий свечек
        /// </summary>
        private ConcurrentQueue<CandleSeries> _candleSeriesToSend;

        /// <summary>
        /// очередь новых стаканов
        /// </summary>
        private ConcurrentQueue<MarketDepth> _marketDepthsToSend;

        /// <summary>
        /// очередь обновлений бида с аска по инструментам 
        /// </summary>
        private ConcurrentQueue<BidAskSender> _bidAskToSend;

        /// <summary>
        /// очередь обновлений level 1 по инструментам 
        /// </summary>
        private ConcurrentQueue<SecurityLevelOne> _levelOneToSend;

        /// <summary>
        /// очередь обновлений таблицы всех сделок по инструментам 
        /// </summary>
        private ConcurrentQueue<List<Trade>> _tradesTableToSend;

        /// <summary>
        /// место в котором контролируется соединение
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
                             _ordersToSend.IsEmpty)
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
                            if (TimeServerChangeEvent != null)
                            {
                                TimeServerChangeEvent(_serverTime);
                            }
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
                    else if (!_tradesTableToSend.IsEmpty)
                    {
                        List<Trade> trades;

                        if (_tradesTableToSend.TryDequeue(out trades))
                        {
                            if (AllTradesTableChangeEvent != null)
                            {
                                AllTradesTableChangeEvent(trades);
                            }
                        }
                    }
                    else if (!_levelOneToSend.IsEmpty)
                    {
                        SecurityLevelOne levelOne;

                        if (_levelOneToSend.TryDequeue(out levelOne))
                        {
                            if (SecurityLevelOneChange != null)
                            {
                                SecurityLevelOneChange(levelOne);
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
                DateTime lastTime = _serverTime;
                _serverTime = value;

                if (_serverTime != lastTime)
                {
                    _newServerTime.Enqueue(_serverTime);
                }
            }
        }

        /// <summary>
        /// вызывается когда изменяется время сервера
        /// </summary>
        public event Action<DateTime> TimeServerChangeEvent;

// портфели

        private List<Portfolio> _portfolios;

        /// <summary>
        /// все счета в системе
        /// </summary>
        public List<Portfolio> Portfolios
        {
            get { return _portfolios; }
        }

        /// <summary>
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

        private object _lockerUpdatePosition = new object();

        /// <summary>
        /// обновилась позиция на бирже
        /// </summary>
        /// <param name="position">позиция по инструменту на бирже</param>
        private void UpdatePosition(PositionOnBoard position)
        {
            lock (_lockerUpdatePosition)
            {
                try
                {
                    if (_portfolios == null ||
                        _portfolios.Count == 0)
                    {
                        return;
                    }

                    Portfolio myPortfolio = _portfolios.Find(portfolio1 => portfolio1.Number == position.PortfolioName);

                    if (myPortfolio == null)
                    {
                        return;
                    }

                    myPortfolio.SetNewPosition(position);

                    _portfolioToSend.Enqueue(_portfolios);
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// из системы пришли новые портфели
        /// </summary>
        private void AddPortfolio(Portfolio portfolio)
        {
            try
            {
                if (_portfolios == null)
                {
                    _portfolios = new List<Portfolio>();
                }
                Portfolio myPortfolio = _portfolios.Find(portfol => portfol.Number == portfolio.Number);

                if (myPortfolio == null)
                {
                    _portfolios.Add(portfolio);
                    _portfolioToSend.Enqueue(_portfolios);
                }
                else
                {
                    myPortfolio.Profit = portfolio.Profit;
                    myPortfolio.ValueBlocked = portfolio.ValueBlocked;
                    myPortfolio.ValueCurrent = portfolio.ValueCurrent;
                    _portfolioToSend.Enqueue(_portfolios);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

        }

        /// <summary>
        /// вызывается когда в системе появляются новые портфели
        /// </summary>
        public event Action<List<Portfolio>> PortfoliosChangeEvent;

// бумаги

        private List<Security> _securities;

        /// <summary>
        /// все инструменты в системе
        /// </summary>
        public List<Security> Securities
        {
            get { return _securities; }
        }

        /// <summary>
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
        /// в системе появлились новые инструменты
        /// </summary>
        private void NewSecurityEvent(Security security)
        {
            try
            {
                if (_securities == null)
                {
                    _securities = new List<Security>();
                }

                if (_securities.Find(securiti => securiti.Name == security.Name) == null)
                {
                    _securities.Add(security);

                    _securitiesToSend.Enqueue(_securities);

                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void LevelOneUpdateEvent(SecurityLevelOne levelOne)
        {
            _levelOneToSend.Enqueue(levelOne);
        }

        /// <summary>
        /// вызывается при появлении новых инструментов
        /// </summary>
        public event Action<List<Security>> SecuritiesChangeEvent;

        /// <summary>
        /// Level 1 по бумаге изменился
        /// </summary>
        public event Action<SecurityLevelOne> SecurityLevelOneChange;

        /// <summary>
        /// показать инструменты 
        /// </summary>
        public void ShowSecuritiesDialog()
        {
            SecuritiesUi ui = new SecuritiesUi(this);
            ui.ShowDialog();
        }

// Подпись на данные

        /// <summary>
        /// мастер загрузки свечек
        /// </summary>
        private CandleManager _candleManager;

        /// <summary>
        /// объект блокирующий многопоточный доступ в StartThisSecurity
        /// </summary>
        private object _lockerStarter = new object();

        /// <summary>
        /// Начать выгрузку данных по инструменту. 
        /// </summary>
        /// <param name="namePaper">имя бумаги которую будем запускать</param>
        /// <param name="timeFrameBuilder">объект несущий в себе данные о таймФрейме</param>
        /// <returns>В случае удачи возвращает CandleSeries
        /// в случае неудачи null</returns>
        public CandleSeries StartThisSecurity(string namePaper, TimeFrameBuilder timeFrameBuilder)
        {
            try
            {
                if (_lastStartServerTime.AddSeconds(15) > DateTime.Now)
                {
                    return null;
                }

                // дальше по одному
                lock (_lockerStarter)
                {
                    if (namePaper == "")
                    {
                        return null;
                    }
                    // надо запустить сервер если он ещё отключен
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
                    if (_lastStartServerTime != DateTime.MinValue &&
                        _lastStartServerTime.AddSeconds(15) > DateTime.Now)
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


                    CandleSeries series = new CandleSeries(timeFrameBuilder, security, StartProgram.IsOsTrader);

                    lock (_serverLocker)
                    {
                        AstsServer.ListenBidAsks(security);
                    }

                    Thread.Sleep(2000);

                    _candleManager.StartSeries(series);

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
        /// изменились серии свечек
        /// </summary>
        private void _candleManager_CandleUpdateEvent(CandleSeries series)
        {
            _candleSeriesToSend.Enqueue(series);
        }

        public CandleSeries GetCandleDataToSecurity(string namePaper, TimeFrameBuilder timeFrameBuilder, DateTime startTime,
            DateTime endTime, DateTime actualTime, bool neadToUpdate)
        {
            return StartThisSecurity(namePaper, timeFrameBuilder);
        }

        public bool GetTickDataToSecurity(string namePaper, DateTime startTime, DateTime endTime, DateTime actualTime,
            bool neadToUpdete)
        {
            return true;
        }

        /// <summary>
        /// вызывается в момент изменения серий свечек
        /// </summary>
        public event Action<CandleSeries> NewCandleIncomeEvent;

        /// <summary>
        /// коннекторам подключеным к серверу необходимо перезаказать данные
        /// </summary>
        public event Action NeadToReconnectEvent;

// стакан

        /// <summary>
        /// входящий срез стакана
        /// </summary>
        private void UpdateBidAsk(MarketDepth myDepth)
        {
            if (myDepth.Asks.Count == 0 ||
                myDepth.Bids.Count == 0)
            {
                return;
            }

            _marketDepthsToSend.Enqueue(myDepth);

            _bidAskToSend.Enqueue(new BidAskSender
            {
                Bid = myDepth.Asks[0].Price,
                Ask = myDepth.Bids[0].Price,
                Security = GetSecurityForName(myDepth.SecurityNameCode)
            });
        }

        /// <summary>
        /// вызывается когда изменяется бид или аск по инструменту
        /// </summary>
        public event Action<decimal, decimal, Security> NewBidAscIncomeEvent;

        /// <summary>
        /// вызывается когда изменяется стакан
        /// </summary>
        public event Action<MarketDepth> NewMarketDepthEvent;

// тики

        /// <summary>
        /// все тики
        /// </summary>
        private List<Trade>[] _allTrades;

        /// <summary>
        /// все тики имеющиеся у сервера
        /// </summary>
        public List<Trade>[] AllTrades
        {
            get { return _allTrades; }
        }

        /// <summary>
        /// блокиратор многопоточного доступа в SmartServer_AddTick
        /// </summary>
        private object _newTradesLoker = new object();

        void _tickStorage_TickLoadedEvent(List<Trade>[] trades)
        {
            _allTrades = trades;
        }

        /// <summary>
        /// обновилась таблица всех сделок
        /// </summary>
        /// <param name="newTrades"></param>
        private void NewTradesEvent(List<Trade> newTrades)
        {
            if (newTrades == null || newTrades.Count == 0)
            {
                return;
            }

            _tradesTableToSend.Enqueue(newTrades);

            for (int i = 0; i < newTrades.Count;i++)
            {
                AddTrade(newTrades[i]);
            }
        }

        /// <summary>
        /// входящие тики из системы
        /// </summary>
        private void AddTrade(Trade trade)
        {
            try
            {
                lock (_newTradesLoker)
                {
                    // сохраняем
                    if (_allTrades == null)
                    {
                        _allTrades = new List<Trade>[1];
                        _allTrades[0] = new List<Trade> {trade};
                    }
                    else
                    {
                        // сортируем сделки по хранилищам
                        List<Trade> myList = null;
                        bool isSave = false;
                        for (int i = 0; i < _allTrades.Length; i++)
                        {
                            if (_allTrades[i] != null && _allTrades[i].Count != 0 &&
                                _allTrades[i][0].SecurityNameCode == trade.SecurityNameCode)
                            {
                                // если для этого инструметна уже есть хранилище, сохраняем и всё
                                _allTrades[i].Add(trade);
                                myList = _allTrades[i];
                                isSave = true;
                                break;
                            }
                        }

                        if (isSave == false)
                        {
                            // хранилища для инструмента нет
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

                    // перегружаем последним временем тика время сервера
                    ServerTime = trade.Time;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

        }

        /// <summary>
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
        /// вызывается в момет появления новых трейдов по инструменту
        /// </summary>
        public event Action<List<Trade>> NewTradeEvent;

        /// <summary>
        /// изменилась таблица всех сделок
        /// </summary>
        public event Action<List<Trade>> AllTradesTableChangeEvent;

// мои сделки

        private List<MyTrade> _myTrades;

        /// <summary>
        /// мои сделки
        /// </summary>
        public List<MyTrade> MyTrades
        {
            get { return _myTrades; }
        }

        /// <summary>
        /// входящие из системы мои сделки
        /// </summary>
        private void AddMyTrade(MyTrade trade)
        {
            try
            {
                if (_myTrades == null)
                {
                    _myTrades = new List<MyTrade>();
                }
                _myTrades.Add(trade);

                _myTradesToSend.Enqueue(trade);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// вызывается когда приходит новая моя сделка
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent;

// работа с ордерами

        /// <summary>
        /// исполнить ордер
        /// </summary>
        public void ExecuteOrder(Order order)
        {
            order.TimeCreate = ServerTime;
            if (AstsServer != null)
            {
                AstsServer.ExecuteOrder(order);
            }
        }

        /// <summary>
        /// отменить ордер
        /// </summary>
        public void CanselOrder(Order order)
        {
            if (AstsServer != null)
            {
                AstsServer.CanselOrder(order);
            }
        }

        /// <summary>
        /// входящий из системы ордер
        /// </summary>
        private void UpdateOrder(Order order)
        {
            try
            {
                _ordersToSend.Enqueue(order);

                if (_myTrades != null &&
                    _myTrades.Count != 0)
                {
                    List<MyTrade> myTrade =
                        _myTrades.FindAll(trade => trade.NumberOrderParent == order.NumberMarket);

                    for (int tradeNum = 0; tradeNum < myTrade.Count; tradeNum++)
                    {
                        _myTradesToSend.Enqueue(myTrade[tradeNum]);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// ошибка при выставлении ордера
        /// </summary>
        /// <param name="reason">причина сбоя при отправке</param>
        /// <param name="number">номер ордера</param>
        private void OrderFailedEvent(string reason, int number)
        {
            try
            {
                Order order = new Order();
                order.NumberUser = number;
                order.NumberMarket = "";
                order.State = OrderStateType.Fail;
                order.ServerType = ServerType.AstsBridge;

                _ordersToSend.Enqueue(order);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// вызывается когда в системе появляется новый ордер
        /// </summary>
        public event Action<Order> NewOrderIncomeEvent;

// обработка лога

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
        /// менеджер лога
        /// </summary>
        private Log _logMaster;

        /// <summary>
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }

    /// <summary>
    /// расположение сервера
    /// </summary>
    public enum AstsDislocation
    {
        /// <summary>
        /// colocation, сервер биржи
        /// </summary>
        Colo,

        /// <summary>
        /// интернет
        /// </summary>
        Internet
    }
}
