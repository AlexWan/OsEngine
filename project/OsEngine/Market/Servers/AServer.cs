using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers
{
    public abstract class AServer: IServer
    {
        /// <summary>
        /// реализация подключения к API
        /// </summary>
        public IServerRealization ServerRealization
        {
            set
            {
                _serverRealization = value;
                _serverRealization.NewTradesEvent += ServerRealization_NewTradesEvent;
                _serverRealization.ConnectEvent += _serverRealization_Connected;
                _serverRealization.DisconnectEvent += _serverRealization_Disconnected;
                _serverRealization.MarketDepthEvent += _serverRealization_MarketDepthEvent;
                _serverRealization.MyOrderEvent += _serverRealization_MyOrderEvent;
                _serverRealization.MyTradeEvent += _serverRealization_MyTradeEvent;
                _serverRealization.PortfolioEvent += _serverRealization_PortfolioEvent;
                _serverRealization.SecurityEvent += _serverRealization_SecurityEvent;
                _serverRealization.LogMessageEvent += SendLogMessage;

                CreateParameterBoolean("Сохранять историю", true);
                _neadToSaveTicksParam = (ServerParameterBool)ServerParameters[ServerParameters.Count - 1];
                _neadToSaveTicksParam.ValueChange += SaveHistoryParam_ValueChange;


                CreateParameterInt("Дней подгружать", 5);
                _neadToSaveTicksDaysCountParam = (ServerParameterInt)ServerParameters[ServerParameters.Count - 1];
                _neadToSaveTicksDaysCountParam.ValueChange += _neadToSaveTicksDaysCountParam_ValueChange;

                _serverRealization.ServerParameters = ServerParameters;

                _tickStorage = new ServerTickStorage(this);
                _tickStorage.NeadToSave = _neadToSaveTicksParam.Value;
                _tickStorage.DaysToLoad = _neadToSaveTicksDaysCountParam.Value;
                _tickStorage.TickLoadedEvent += _tickStorage_TickLoadedEvent;
                _tickStorage.LogMessageEvent += SendLogMessage;
                _tickStorage.LoadTick();
                

                Thread ordersExecutor = new Thread(ExecutorOrdersThreadArea);
                ordersExecutor.CurrentCulture = CultureInfo.InvariantCulture;
                ordersExecutor.IsBackground = true;
                ordersExecutor.Name = "ServerThreadOrderExecutor" + _serverRealization.ServerType;
                ordersExecutor.Start();

                Log = new Log(_serverRealization.ServerType + "Server", StartProgram.IsOsTrader);
                Log.Listen(this);

                _serverStatusNead = ServerConnectStatus.Disconnect;

                _threadPrime = new Thread(PrimeThreadArea);
                _threadPrime.CurrentCulture = CultureInfo.InvariantCulture;
                _threadPrime.IsBackground = true;
                _threadPrime.Name = "ServerThreadPrime" + _serverRealization.ServerType;
                _threadPrime.Start();

                Thread threadDataSender = new Thread(SenderThreadArea);
                threadDataSender.CurrentCulture = CultureInfo.InvariantCulture;
                threadDataSender.IsBackground = true;
                threadDataSender.Name = "ServerThreadDataSender" + _serverRealization.ServerType;
                threadDataSender.Start();

                AServerTests tester = new AServerTests();
                tester.Listen(this);
                tester.LogMessageEvent += SendLogMessage;

                _serverIsStart = true;

            }
            get { return _serverRealization; }
        }

        private IServerRealization _serverRealization;

        /// <summary>
        /// тип сервера
        /// </summary>
        public ServerType ServerType { get { return ServerRealization.ServerType; } }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            if (_ui == null)
            {
                _ui = new AServerParameterUi(this);
                _ui.Show();
                _ui.Closing += (sender, args) => { _ui = null; };
            }
            else
            {
                _ui.Activate();
            }
        }

        /// <summary>
        /// окно настроек
        /// </summary>
        private AServerParameterUi _ui;

        void _neadToSaveTicksDaysCountParam_ValueChange()
        {
            if (_tickStorage != null)
            {
                _tickStorage.DaysToLoad = _neadToSaveTicksDaysCountParam.Value;
            }
        }

        void SaveHistoryParam_ValueChange()
        {
            if (_tickStorage != null)
            {
                _tickStorage.NeadToSave = _neadToSaveTicksParam.Value;
            }
        }

        // параметры

        /// <summary>
        /// запустился ли сервер
        /// </summary>
        private bool _serverIsStart;

        /// <summary>
        /// параметр с флагом о том, нужно ли сохранять тики для сервера
        /// </summary>
        private ServerParameterBool _neadToSaveTicksParam;

        /// <summary>
        /// параметр с количеством дней которые нужно сохранять тики
        /// </summary>
        private ServerParameterInt _neadToSaveTicksDaysCountParam;

        /// <summary>
        /// параметры сервера
        /// </summary>
        public List<IServerParameter> ServerParameters = new List<IServerParameter>();

        /// <summary>
        /// создать строковый параметр сервера
        /// </summary>
        public void CreateParameterString(string name, string param)
        {
            ServerParameterString newParam = new ServerParameterString();
            newParam.Name = name;
            newParam.Value = param;

            newParam = (ServerParameterString)LoadParam(newParam);
            if (_serverIsStart)
            {
                ServerParameters.Insert(ServerParameters.Count - 2, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }

            newParam.ValueChange += newParam_ValueChange;
        }

        /// <summary>
        /// создать интовый параметр сервера
        /// </summary>
        public void CreateParameterInt(string name, int param)
        {
            ServerParameterInt newParam = new ServerParameterInt();
            newParam.Name = name;
            newParam.Value = param;

            newParam = (ServerParameterInt)LoadParam(newParam);
            if (_serverIsStart)
            {
                ServerParameters.Insert(ServerParameters.Count - 2, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }

            newParam.ValueChange += newParam_ValueChange;
        }

        /// <summary>
        /// создать десимал параметр сервера
        /// </summary>
        public void CreateParameterDecimal(string name, decimal param)
        {
            ServerParameterDecimal newParam = new ServerParameterDecimal();
            newParam.Name = name;
            newParam.Value = param;

            newParam = (ServerParameterDecimal)LoadParam(newParam);
            if (_serverIsStart)
            {
                ServerParameters.Insert(ServerParameters.Count - 2, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }

            newParam.ValueChange += newParam_ValueChange;
        }

        /// <summary>
        /// создать булевый параметр сервера
        /// </summary>
        public void CreateParameterBoolean(string name, bool param)
        {
            ServerParameterBool newParam = new ServerParameterBool();
            newParam.Name = name;
            newParam.Value = param;

            newParam = (ServerParameterBool)LoadParam(newParam);
            if (_serverIsStart)
            {
                ServerParameters.Insert(ServerParameters.Count - 2, newParam);
            }
            else
            {
                ServerParameters.Add( newParam);
            }
            

            newParam.ValueChange += newParam_ValueChange;
        }

        /// <summary>
        /// создать парольный параметр сервера
        /// </summary>
        public void CreateParameterPassword(string name, string param)
        {
            ServerParameterPassword newParam = new ServerParameterPassword();
            newParam.Name = name;
            newParam.Value = param;

            newParam = (ServerParameterPassword)LoadParam(newParam);
            if (_serverIsStart)
            {
                ServerParameters.Insert(ServerParameters.Count - 2, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }

            newParam.ValueChange += newParam_ValueChange;
        }

        /// <summary>
        /// создать параметр сервера для пути к папке
        /// </summary>
        public void CreateParameterPath(string name)
        {
            ServerParameterPath newParam = new ServerParameterPath();
            newParam.Name = name;

            newParam = (ServerParameterPath)LoadParam(newParam);
            if (_serverIsStart)
            {
                ServerParameters.Insert(ServerParameters.Count - 2, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }

            newParam.ValueChange += newParam_ValueChange;
        }

        /// <summary>
        /// изменилось состояние параметра
        /// </summary>
        void newParam_ValueChange()
        {
            SaveParam();
        }

        /// <summary>
        /// сохранить параметры
        /// </summary>
        private void SaveParam()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + ServerType + @"Params.txt", false)
                    )
                {
                    for (int i = 0; i < ServerParameters.Count; i++)
                    {
                        writer.WriteLine(ServerParameters[i].GetStringToSave());
                    }
                    
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить параметр
        /// </summary>
        private IServerParameter LoadParam(IServerParameter param)
        {
            try
            {
                if (ServerType == ServerType.Binance)
                {
                    
                }
            }
            catch (Exception)
            {
                throw new Exception("You try create Parameter befor create realization. Set CreateParam method after create ServerRealization");
            }


            if (!File.Exists(@"Engine\" + ServerType + @"Params.txt"))
            {
                return param;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + ServerType + @"Params.txt"))
                {
                    while(reader.EndOfStream == false)
                    {
                        string save = reader.ReadLine();

                        string[] saveAr = save.Split('^');

                        ServerParameterType type;
                        Enum.TryParse(saveAr[0], out type);

                        IServerParameter oldParam = null;

                        if (type == ServerParameterType.String)
                        {
                            oldParam = new ServerParameterString();
                        }
                        if (type == ServerParameterType.Decimal)
                        {
                            oldParam = new ServerParameterDecimal();
                        }
                        if (type == ServerParameterType.Int)
                        {
                            oldParam = new ServerParameterInt();
                        }
                        if (type == ServerParameterType.Bool)
                        {
                            oldParam = new ServerParameterBool();
                        }
                        if (type == ServerParameterType.Password)
                        {
                            oldParam = new ServerParameterPassword();
                        }
                        if (type == ServerParameterType.Path)
                        {
                            oldParam = new ServerParameterPath();
                        }

                        if (oldParam == null)
                        {
                            continue;
                        }

                        oldParam.LoadFromStr(save);

                        if (oldParam.Name == param.Name &&
                            oldParam.Type == param.Type)
                        {
                            return oldParam;
                        }
                    }

                    return param;
                    

                }
            }
            catch (Exception error)
            {
               SendLogMessage(error.ToString(),LogMessageType.Error);
            }
            return param;
        }

        // подключение/отключение

        /// <summary>
        /// нужный статус сервера. Нужен потоку который следит за соединением
        /// В зависимости от этого поля управляет соединением
        /// </summary>
        private ServerConnectStatus _serverStatusNead;

        /// <summary>
        /// запустить сервер. Подключиться к торговой системе
        /// </summary>
        public void StartServer()
        {
            if (UserWhantConnect != null)
            {
                UserWhantConnect();
            }
            _serverStatusNead = ServerConnectStatus.Connect;
        }

        /// <summary>
        /// остановить сервер
        /// </summary>
        public void StopServer()
        {
            if (UserWhantDisconnect != null)
            {
                UserWhantDisconnect();
            }
            _serverStatusNead = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// пришло оповещение от клиента, что соединение установлено
        /// </summary>
        void _serverRealization_Connected()
        {
            SendLogMessage("Соединение установлено", LogMessageType.System);
            ServerStatus = ServerConnectStatus.Connect;
        }

        // статус соединения

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
                    SendLogMessage(_serverConnectStatus + " Изменилось состояние соединения", LogMessageType.Connect);
                    if (ConnectStatusChangeEvent != null)
                    {
                        ConnectStatusChangeEvent(_serverConnectStatus.ToString());
                    }
                }
            }
        }
        private ServerConnectStatus _serverConnectStatus;

        /// <summary>
        /// изменилось состояние соединения
        /// </summary>
        public event Action<string> ConnectStatusChangeEvent;

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
            while (true)
            {
                Thread.Sleep(1000);
                try
                {
                    if (ServerRealization == null)
                    {
                        continue;
                    }

                    if ((ServerRealization.ServerStatus != ServerConnectStatus.Connect)
                        && _serverStatusNead == ServerConnectStatus.Connect &&
                        _lastStartServerTime.AddSeconds(60) < DateTime.Now)
                    {
                        SendLogMessage("Запущена процедура активации подключения", LogMessageType.System);
                        ServerRealization.Dispose();
                        _candleManager = null;
                        ServerRealization.Connect();
                        _lastStartServerTime = DateTime.Now;
                        continue;
                    }

                    if (ServerRealization.ServerStatus == ServerConnectStatus.Connect && _serverStatusNead == ServerConnectStatus.Disconnect)
                    {
                        SendLogMessage("Запущена процедура отключения подключения", LogMessageType.System);
                        ServerRealization.Dispose();
                        _candleManager = null;
                        continue;
                    }

                    if (ServerRealization.ServerStatus != ServerConnectStatus.Connect)
                    {
                        continue;
                    }

                    if (_candleManager == null)
                    {
                        SendLogMessage("Создаём менеджер свечей", LogMessageType.System);
                        StartCandleManager();
                        continue;
                    }

                    if (_portfolios == null || _portfolios.Count == 0)
                    {
                        ServerRealization.GetPortfolios();
                    }

                    if (_securities== null || Securities.Count == 0)
                    {
                        ServerRealization.GetSecurities();
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage("КРИТИЧЕСКАЯ ОШИБКА. Реконнект", LogMessageType.Error);
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    ServerRealization.Dispose();
                    _candleManager = null;

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

        /// <summary>
        /// время последнего старта сервера
        /// </summary>
        private DateTime _lastStartServerTime = DateTime.MinValue;

        /// <summary>
        /// соединение с клиентом разорвано
        /// </summary>
        void _serverRealization_Disconnected()
        {
            SendLogMessage("Соединение разорвано", LogMessageType.System);
            ServerStatus = ServerConnectStatus.Disconnect;

            if (NeadToReconnectEvent != null)
            {
                NeadToReconnectEvent();
            }
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

        #region Работа потока рассылки

        /// <summary>
        /// очередь новых ордеров
        /// </summary>
        private ConcurrentQueue<Order> _ordersToSend = new ConcurrentQueue<Order>();

        /// <summary>
        /// очередь тиков
        /// </summary>
        private ConcurrentQueue<List<Trade>> _tradesToSend = new ConcurrentQueue<List<Trade>>();

        /// <summary>
        /// очередь новых портфелей
        /// </summary>
        private ConcurrentQueue<List<Portfolio>> _portfolioToSend = new ConcurrentQueue<List<Portfolio>>();

        /// <summary>
        /// очередь новых инструментов
        /// </summary>
        private ConcurrentQueue<List<Security>> _securitiesToSend = new ConcurrentQueue<List<Security>>();

        /// <summary>
        /// очередь новых моих сделок
        /// </summary>
        private ConcurrentQueue<MyTrade> _myTradesToSend = new ConcurrentQueue<MyTrade>();

        /// <summary>
        /// очередь нового времени
        /// </summary>
        private ConcurrentQueue<DateTime> _newServerTime = new ConcurrentQueue<DateTime>();

        /// <summary>
        /// очередь обновлённых серий свечек
        /// </summary>
        private ConcurrentQueue<CandleSeries> _candleSeriesToSend = new ConcurrentQueue<CandleSeries>();

        /// <summary>
        /// очередь новых стаканов
        /// </summary>
        private ConcurrentQueue<MarketDepth> _marketDepthsToSend = new ConcurrentQueue<MarketDepth>();

        /// <summary>
        /// очередь обновлений бида с аска по инструментам 
        /// </summary>
        private ConcurrentQueue<BidAskSender> _bidAskToSend = new ConcurrentQueue<BidAskSender>();

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

        #endregion

        // время сервера

        /// <summary>
        /// время сервера
        /// </summary>
        public DateTime ServerTime
        {
            get { return _serverTime; }

            private set
            {
                if (value < _serverTime)
                {
                    return;
                }

                DateTime lastTime = _serverTime;
                _serverTime = value;

                if (_serverTime != lastTime)
                {
                    _newServerTime.Enqueue(_serverTime);
                }
                ServerRealization.ServerTime = _serverTime;
            }
        }
        private DateTime _serverTime;

        /// <summary>
        /// изменилось время сервера
        /// </summary>
        public event Action<DateTime> TimeServerChangeEvent;

        // портфели

        /// <summary>
        /// все счета в системе
        /// </summary>
        public List<Portfolio> Portfolios
        {
            get { return _portfolios; }
        }
        private List<Portfolio> _portfolios;

        /// <summary>
        /// взять портфель по номеру
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
        /// обновился портфель
        /// </summary>
        private void _serverRealization_PortfolioEvent(List<Portfolio> portf)
        {
            try
            {
                if (_portfolios == null)
                {
                    _portfolios = new List<Portfolio>();
                }

                for (int i = 0; i < portf.Count; i++)
                {
                    if (_portfolios.Find(p => p.Number == portf[i].Number) == null)
                    {
                        _portfolios.Add(portf[i]);
                    }
                }

                _portfolioToSend.Enqueue(_portfolios);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// изменились портфели
        /// </summary>
        public event Action<List<Portfolio>> PortfoliosChangeEvent;

        // инструменты

        /// <summary>
        /// все инструменты в системе
        /// </summary>
        public List<Security> Securities
        {
            get { return _securities; }
        }
        private List<Security> _securities;

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
        /// обновился список бумаг
        /// </summary>
        void _serverRealization_SecurityEvent(List<Security> securities)
        {
            if (_securities == null)
            {
                _securities = new List<Security>();
            }
            for (int i = 0; i < securities.Count; i++)
            {
                if (securities[i].NameId == null)
                {
                    SendLogMessage("Во входящих из реализации бумагах, отсутствуют NameId",LogMessageType.Error);
                    return;
                }
                if (_securities.Find(s => s.NameId == securities[i].NameId) == null)
                {
                    _securities.Add(securities[i]);
                }
            }

            _securitiesToSend.Enqueue(_securities);
        }

        /// <summary>
        /// изменились инструменты
        /// </summary>
        public event Action<List<Security>> SecuritiesChangeEvent;

        /// <summary>
        /// показать бумаги
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
        /// начать выкачивать данный иснтрументн
        /// </summary>
        /// <param name="namePaper"> название инструмента</param>
        /// <param name="timeFrameBuilder">объект несущий в себе данные о ТаймФрейме нужном для серии</param>
        /// <returns>в случае успешного запуска возвращает CandleSeries, объект генерирующий свечи</returns>
        public CandleSeries StartThisSecurity(string namePaper, TimeFrameBuilder timeFrameBuilder)
        {
            try
            {
                lock (_lockerStarter)
                {
                    if (namePaper == "")
                    {
                        return null;
                    }

                    if (Portfolios == null || Securities == null)
                    {
                        return null;
                    }

                    if (_lastStartServerTime != DateTime.MinValue &&
                        _lastStartServerTime.AddSeconds(15) > DateTime.Now)
                    {
                        return null;
                    }

                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        return null;
                    }

                    if (_candleManager == null)
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

                    ServerRealization.Subscrible(security);

                    Thread.Sleep(300);

                    _candleManager.StartSeries(series);

                    SendLogMessage("Инструмент " + series.Security.Name + "ТаймФрейм" + series.TimeFrame +
                                   " успешно подключен на получение данных и прослушивание свечек", LogMessageType.System);

                    if (_tickStorage != null)
                    {
                        _tickStorage.SetSecurityToSave(security);
                    }

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
        /// остановить скачивание свечек
        /// </summary>
        /// <param name="series"> серия свечек которую надо остановить</param>
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

        /// <summary>
        /// необходимо перезаказать данные у сервера
        /// </summary>
        public event Action NeadToReconnectEvent;

        /// <summary>
        /// новые свечи
        /// </summary>
        public event Action<CandleSeries> NewCandleIncomeEvent;

        // стакан

        /// <summary>
        /// все стаканы
        /// </summary>
        private List<MarketDepth> _depths;

        /// <summary>
        /// пришел обновленный стакан
        /// </summary>
        /// <param name="myDepth"></param>
        void _serverRealization_MarketDepthEvent(MarketDepth myDepth)
        {
            try
            {
                if (_depths == null)
                {
                    _depths = new List<MarketDepth>();
                }

                myDepth.Time = ServerTime;

                if (NewMarketDepthEvent != null)
                {
                    _marketDepthsToSend.Enqueue(myDepth);

                    if (myDepth.Asks.Count != 0 && myDepth.Bids.Count != 0)
                    {
                        _bidAskToSend.Enqueue(new BidAskSender
                        {
                            Bid = myDepth.Bids[0].Price,
                            Ask = myDepth.Asks[0].Price,
                            Security = GetSecurityForName(myDepth.SecurityNameCode)
                        });
                    }
                }

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// изменился лучший бид / аск по инструменту
        /// </summary>
        public event Action<decimal, decimal, Security> NewBidAscIncomeEvent;

        /// <summary>
        /// новый стакан в системе
        /// </summary>
        public event Action<MarketDepth> NewMarketDepthEvent;

        // тики

        /// <summary>
        /// хранилище тиков
        /// </summary>
        private ServerTickStorage _tickStorage;

        /// <summary>
        /// хранилище тиков
        /// </summary>
        /// <param name="trades"></param>
        void _tickStorage_TickLoadedEvent(List<Trade>[] trades)
        {
            _allTrades = trades;
        }

        /// <summary>
        /// все тики
        /// </summary>
        private List<Trade>[] _allTrades;

        /// <summary>
        /// взять историю тиков по инструменту
        /// </summary>
        /// <param name="security"> инстурмент</param>
        /// <returns>сделки</returns>
        public List<Trade> GetAllTradesToSecurity(Security security)
        {
            try
            {
                if (_allTrades == null)
                {
                    return null;
                }
                List<Trade> trades = new List<Trade>();

                for (int i = 0; i < _allTrades.Length; i++)
                {
                    if (_allTrades[i] != null && _allTrades[i].Count != 0 &&
                        _allTrades[i][0].SecurityNameCode == security.Name)
                    {
                        return _allTrades[i];
                    }
                }

                return trades;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// все тики имеющиеся у сервера
        /// </summary>
        public List<Trade>[] AllTrades { get { return _allTrades; } }

        /// <summary>
        /// пришли новые тики
        /// </summary>
        void ServerRealization_NewTradesEvent(Trade trade)
        {
            try
            {
                // сохраняем
                if (_allTrades == null)
                {
                    _allTrades = new List<Trade>[1];
                    _allTrades[0] = new List<Trade> { trade };
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
                            if (trade.Time < _allTrades[i][_allTrades[i].Count - 1].Time)
                            {
                                return;
                            }

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
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// новый тик
        /// </summary>
        public event Action<List<Trade>> NewTradeEvent;

        // новая моя сделка

        private List<MyTrade> _myTrades = new List<MyTrade>();

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
        void _serverRealization_MyTradeEvent(MyTrade trade)
        {
            if (trade.Time == DateTime.MinValue)
            {
                trade.Time = ServerTime;
            }
            
            _myTradesToSend.Enqueue(trade);
            _myTrades.Add(trade);
        }

        /// <summary>
        /// изменилась моя сделка
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent;

        // работа с ордерами

        /// <summary>
        /// место работы потока на очередях исполнения заявок и их отмены
        /// </summary>
        private void ExecutorOrdersThreadArea()
        {
            while (true)
            {
                if (_lastStartServerTime.AddSeconds(30) > DateTime.Now)
                {
                    Thread.Sleep(1000);
                    continue;
                }
                try
                {
                    Thread.Sleep(20);
                    if (_ordersToExecute != null && _ordersToExecute.Count != 0)
                    {
                        Order order;
                        if (_ordersToExecute.TryDequeue(out order))
                        {
                            ServerRealization.SendOrder(order);
                        }
                    }
                    else if (_ordersToCansel != null && _ordersToCansel.Count != 0)
                    {
                        Order order;
                        if (_ordersToCansel.TryDequeue(out order))
                        {
                            ServerRealization.CanselOrder(order);
                        }
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// очередь ордеров для выставления в систему
        /// </summary>
        private ConcurrentQueue<Order> _ordersToExecute = new ConcurrentQueue<Order>();

        /// <summary>
        /// очередь ордеров для отмены в системе
        /// </summary>
        private ConcurrentQueue<Order> _ordersToCansel = new ConcurrentQueue<Order>();

        /// <summary>
        /// входящий из системы ордер
        /// </summary>
        void _serverRealization_MyOrderEvent(Order myOrder)
        {
            _ordersToSend.Enqueue(myOrder);
        }

        /// <summary>
        /// выслать ордер на исполнение в торговую систему
        /// </summary>
        /// <param name="order">ордер</param>
        public void ExecuteOrder(Order order)
        {
            if (UserSetOrderOnExecute != null)
            {
                UserSetOrderOnExecute(order);
            }
            if (_lastStartServerTime.AddMinutes(1) > DateTime.Now)
            {
                order.State = OrderStateType.Fail;
                _ordersToSend.Enqueue(order);

                SendLogMessage("Ордер № " + order.NumberUser +
                    " не может быть выставлен, т.к. с времени предыдущего включения сервера прошло менее одной минуты", LogMessageType.Error);
                return;
            }

            order.TimeCreate = ServerTime;
            _ordersToExecute.Enqueue(order);

            SendLogMessage("Выставлен ордер, цена: " + order.Price + " Сторона: " + order.Side + ", Объём: " + order.Volume +
            ", Инструмент: " + order.SecurityNameCode + "Номер " + order.NumberUser, LogMessageType.System);
        }

        /// <summary>
        /// отозвать ордер из торговой системы
        /// </summary>
        /// <param name="order">ордер</param>
        public void CanselOrder(Order order)
        {
            if (UserSetOrderOnCancel != null)
            {
                UserSetOrderOnCancel(order);
            }
            _ordersToCansel.Enqueue(order);
            SendLogMessage("Отзываем ордер: " + order.NumberUser, LogMessageType.System);
        }

        /// <summary>
        /// изменился ордер
        /// </summary>
        public event Action<Order> NewOrderIncomeEvent;

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
        /// менеджер лога
        /// </summary>
        public Log Log;

        /// <summary>
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        // исходящие события для автоматического тестирования

        /// <summary>
        /// внешние системы запросили исполнение ордера
        /// </summary>
        public event Action<Order> UserSetOrderOnExecute;

        /// <summary>
        /// внешние системы запросили отзыв ордера
        /// </summary>
        public event Action<Order> UserSetOrderOnCancel;

        /// <summary>
        /// пользователь запросил подключение к АПИ
        /// </summary>
        public event Action UserWhantConnect;

        /// <summary>
        /// пользователь запросил отключение от АПИ
        /// </summary>
        public event Action UserWhantDisconnect;
    }
}
