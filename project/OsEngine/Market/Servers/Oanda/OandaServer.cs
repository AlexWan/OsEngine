using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OkonkwoOandaV20;
using OkonkwoOandaV20.TradeLibrary.DataTypes.Instrument;
using OkonkwoOandaV20.TradeLibrary.Primitives;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.InteractivBrokers;

namespace OsEngine.Market.Servers.Oanda
{
    public class OandaServer : IServer
    {

        //сервис. менеджмент первичных настроек

        /// <summary>
        ///  конструктор
        /// </summary>
        public OandaServer(bool neadToLoadTicks)
        {
            ClientIdInSystem = "";
            Token = "";
            _neadToSaveTicks = false;
            ServerType = ServerType.Oanda;
            ServerStatus = ServerConnectStatus.Disconnect;

            Load();

            _tickStorage = new ServerTickStorage(this);
            _tickStorage.NeadToSave = NeadToSaveTicks;
            _tickStorage.TickLoadedEvent += _tickStorage_TickLoadedEvent;
            _tickStorage.LogMessageEvent += SendLogMessage;

            if (neadToLoadTicks)
            {
                _tickStorage.LoadTick();
            }

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

            _logMaster = new Log("OandaServer");
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

            Thread watcherConnectThread = new Thread(ReconnectThread);
            watcherConnectThread.IsBackground = true;
            watcherConnectThread.Start();
        }

        /// <summary>
        /// номер клиента в системе
        /// </summary>
        public string ClientIdInSystem;

        /// <summary>
        /// секретный ключ для доступа к АПи. Пароль короч
        /// </summary>
        public string Token;

        /// <summary>
        /// тестовое ли это подключение
        /// </summary>
        public bool IsTestConnection;

        /// <summary>
        /// взять тип сервера
        /// </summary>
        public ServerType ServerType { get; set; }

        /// <summary>
        /// вызвать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            OandaServerUi ui = new OandaServerUi(this, _logMaster);
            ui.Show();
        }

        /// <summary>
        /// загрузить настройки из файла
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + @"OandaServer.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"OandaServer.txt"))
                {
                    IsTestConnection = Convert.ToBoolean(reader.ReadLine());
                    Token = reader.ReadLine();
                    ClientIdInSystem = reader.ReadLine();
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
        /// сохранить настройки в файл
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"OandaServer.txt", false))
                {
                    writer.WriteLine(IsTestConnection);
                    writer.WriteLine(Token);
                    writer.WriteLine(ClientIdInSystem);
                    writer.WriteLine(NeadToSaveTicks);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        //хранилище тиков

        /// <summary>
        /// хранилище тиков
        /// </summary>
        private ServerTickStorage _tickStorage;

        private bool _neadToSaveTicks;

        /// <summary>
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

        //статус сервера

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
                    SendLogMessage(_serverConnectStatus + " Изменилось состояние соединения", LogMessageType.Connect);
                    if (ConnectStatusChangeEvent != null)
                    {
                        ConnectStatusChangeEvent(_serverConnectStatus.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// нужный статус сервера. Нужен потоку который следит за соединением
        /// В зависимости от этого поля управляет соединением
        /// </summary>
        private ServerConnectStatus _serverStatusNead;

        /// <summary>
        /// вызывается когда статус соединения изменяется
        /// </summary>
        public event Action<string> ConnectStatusChangeEvent;

        //подключение / отключение

        /// <summary>
        /// запустить сервер
        /// </summary>
        public void StartServer()
        {
            _serverStatusNead = ServerConnectStatus.Connect;
        }

        /// <summary>
        /// остановить сервер
        /// </summary>
        public void StopServer()
        {
            _serverStatusNead = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// соединение установлено
        /// </summary>
        private void _ibClient_ConnectionSucsess()
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
        /// соединение разорвано
        /// </summary>
        private void _ibClient_ConnectionFail()
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

        // работа потока перезапускающего основное соединени
        // оно просто иногда перестаёт подавать данные. Нужно перезапускать

        private DateTime _lastTimeIncomeDate = DateTime.MinValue;

        private void ReconnectThread()
        {
            while (true)
            {
                Thread.Sleep(5000);
                if (_connectedContracts == null ||
                    _connectedContracts.Count == 0)
                {
                    continue;
                }

                if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday
                    ||
                    DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
                {
                    continue;
                }

                if (_lastTimeIncomeDate == DateTime.MinValue)
                {
                    continue;
                }

                if (_lastTimeIncomeDate.AddMinutes(5) < DateTime.Now)
                {
                    _lastTimeIncomeDate = DateTime.Now;
                    StopServer();
                    Thread.Sleep(15000);
                    StartServer();
                }
            }
        }

        //работа потока следящего за соединением и заказывающего первичные данные

        private OandaClient _Client;

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

                lock (_serverLocker)
                {
                    try
                    {
                        if (_Client == null)
                        {
                            SendLogMessage("Создаём коннектор", LogMessageType.System);
                            CreateNewServer();
                            continue;
                        }

                        ServerConnectStatus state = ServerStatus;

                        if (state == ServerConnectStatus.Disconnect
                            && _serverStatusNead == ServerConnectStatus.Connect)
                        {
                            SendLogMessage("Запущена процедура активации подключения", LogMessageType.System);
                            Connect();
                            continue;
                        }

                        if (state == ServerConnectStatus.Connect
                            && _serverStatusNead == ServerConnectStatus.Disconnect)
                        {
                            SendLogMessage("Запущена процедура отключения подключения", LogMessageType.System);
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
                            SendLogMessage("Создаём менеджер свечей", LogMessageType.System);
                            StartCandleManager();
                            continue;
                        }

                        if (Portfolios == null)
                        {
                            SendLogMessage("Портфели не найдены. Запрашиваем портфели", LogMessageType.System);
                            GetPortfolio();
                            SendLogMessage("Подписываемся на обновление времени сервера", LogMessageType.System);

                            continue;
                        }

                        if (Securities == null)
                        {
                            SendLogMessage("Бумаги не найдены. Запрашиваем бумаги", LogMessageType.System);
                            GetSecurities();
                            continue;
                        }

                        if (_neadToWatchSecurity)
                        {
                            _neadToWatchSecurity = false;
                            SendLogMessage("Обновляем список бумаг", LogMessageType.System);
                            GetSecurities();
                            continue;
                        }

                        if (_startListening == false)
                        {
                            SendLogMessage("Подписываемся на прослушивание данных", LogMessageType.System);
                            StartListeningPortfolios();
                            _startListening = true;
                        }
                    }
                    catch (Exception error)
                    {
                        SendLogMessage("КРИТИЧЕСКАЯ ОШИБКА. Реконнект", LogMessageType.Error);
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                        ServerStatus = ServerConnectStatus.Disconnect;
                        Dispose(); // очищаем данные о предыдущем коннекторе

                        Thread.Sleep(5000);
                        // переподключаемся
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
        /// время последнего старта сервера
        /// </summary>
        private DateTime _lastStartServerTime = DateTime.MinValue;

        /// <summary>
        /// создать новое подключение
        /// </summary>
        private void CreateNewServer()
        {
            if (_Client == null)
            {
                _Client = new OandaClient();
                _Client.ConnectionFail += _ibClient_ConnectionFail;
                _Client.ConnectionSucsess += _ibClient_ConnectionSucsess;
                _Client.LogMessageEvent += SendLogMessage;
                _Client.NewMyTradeEvent += _ibClient_NewMyTradeEvent;
                _Client.NewOrderEvent += _ibClient_NewOrderEvent;
                _Client.NewTradeEvent += AddTick;
                _Client.PortfolioChangeEvent += _Client_PortfolioChangeEvent;
                _Client.NewSecurityEvent += _Client_NewSecurityEvent;
                _Client.MarketDepthChangeEvent += _Client_MarketDepthChangeEvent;
            }
        }

        /// <summary>
        /// начать процесс подключения
        /// </summary>
        private void Connect()
        {
            if (string.IsNullOrEmpty(ClientIdInSystem))
            {
                SendLogMessage("В значении номер Id не верное значение. Подключение прервано.", LogMessageType.Error);
                _serverStatusNead = ServerConnectStatus.Disconnect;
                return;
            }

            _Client.Connect(ClientIdInSystem, Token, IsTestConnection);
            _lastStartServerTime = DateTime.Now;
            Thread.Sleep(5000);
        }

        /// <summary>
        /// приостановить подключение
        /// </summary>
        private void Disconnect()
        {
            if (_Client == null)
            {
                return;
            }
            _Client.Disconnect();
            Thread.Sleep(5000);

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

        /// <summary>
        /// включает загрузку инструментов и портфелей
        /// </summary>
        private void GetPortfolio()
        {
            _Client.GetPortfolios();
            Thread.Sleep(5000);
        }

        /// <summary>
        /// необходимо перезаказать информацию о контрактах
        /// </summary>
        private bool _neadToWatchSecurity;

        /// <summary>
        /// взять информацию о бумагах
        /// </summary>
        private void GetSecurities()
        {
            _Client.GetSecurities(_portfolios);
            Thread.Sleep(10000);
        }

        /// <summary>
        /// включена ли прослушка портфеля
        /// </summary>
        private bool _startListening;

        /// <summary>
        /// включает прослушивание портфелей
        /// </summary>
        private void StartListeningPortfolios()
        {
            Thread.Sleep(3000);
            _Client.StartStreamThreads();
            Thread.Sleep(5000);
        }

        /// <summary>
        /// привести программу к моменту запуска. Очистить все объекты участвующие в подключении к серверу
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private void Dispose()
        {
            if (_Client != null)
            {
                _Client.ConnectionFail -= _ibClient_ConnectionFail;
                _Client.ConnectionSucsess -= _ibClient_ConnectionSucsess;
                _Client.LogMessageEvent -= SendLogMessage;
                _Client.NewMyTradeEvent-= _ibClient_NewMyTradeEvent;
                _Client.NewOrderEvent -= _ibClient_NewOrderEvent;
                _Client.NewTradeEvent -= AddTick;
                _Client.PortfolioChangeEvent -= _Client_PortfolioChangeEvent;
                _Client.NewSecurityEvent -= _Client_NewSecurityEvent;
                _Client.MarketDepthChangeEvent -= _Client_MarketDepthChangeEvent;
            }

            try
            {
                if (_Client != null && ServerStatus == ServerConnectStatus.Connect)
                {
                    _Client.Disconnect();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            _Client = null;
        }

        /// <summary>
        /// блокиратор многопоточного доступа к серверу
        /// </summary>
        private object _serverLocker = new object();


        //работа потока рассылки входящих данных

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

        //время сервера

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

                if (_serverTime != lastTime &&
                    TimeServerChangeEvent != null)
                {
                    TimeServerChangeEvent(_serverTime);
                }
            }
        }

        /// <summary>
        /// вызывается когда изменяется время сервера
        /// </summary>
        public event Action<DateTime> TimeServerChangeEvent;

        // портфели и позиции

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

        void _Client_PortfolioChangeEvent(Portfolio portfolio)
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
                SendLogMessage("Доступен новый портфель. Номер: " + portfolio.Number, LogMessageType.System);
                _portfolios.Add(portfolio);
            }

            if (PortfoliosChangeEvent != null)
            {
                PortfoliosChangeEvent(_portfolios);
            }
        }

        /// <summary>
        /// вызывается когда в системе появляются новые портфели
        /// </summary>
        public event Action<List<Portfolio>> PortfoliosChangeEvent;

        //бумаги. формат Os.Engine

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

        void _Client_NewSecurityEvent(List<Security> securities)
        {
            _securities = securities;

            if (SecuritiesChangeEvent != null)
            {
                SecuritiesChangeEvent(_securities);
            }
        }

        /// <summary>
        /// вызывается при появлении новых инструментов
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

        //Подпись на данные

        /// <summary>
        /// мастер загрузки свечек
        /// </summary>
        private CandleManager _candleManager;

        /// <summary>
        /// объект блокирующий многопоточный доступ в StartThisSecurity
        /// </summary>
        private object _lockerStarter = new object();

        private List<string> _connectedContracts;

        /// <summary>
        /// Начать выгрузку данных по инструменту. 
        /// </summary>
        /// <param name="namePaper">имя бумаги которую будем запускать</param>
        /// <param name="timeFrameBuilder">объект несущий </param>
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

                       if (_connectedContracts == null)
                       {
                           _connectedContracts = new List<string>();
                       }

                       if (_connectedContracts.Find(s => s == security.Name) == null)
                       {
                           _connectedContracts.Add(security.Name);
                       }

                       _tickStorage.SetSecurityToSave(security);

                       // 2 создаём серию свечек
                       CandleSeries series = new CandleSeries(timeFrameBuilder, security);

                       if(NeadToGetCandles(timeFrameBuilder.TimeFrame))
                       { // подгружаем в серию свечки, если коннектор это позволяет
                           short count = 500;
                           string price = "MBA";
                           string instrument = security.Name;
                           string granularity = GetTimeFrameInOandaFormat(timeFrameBuilder.TimeFrame).ToString();

                           var parameters = new Dictionary<string, string>();
                           parameters.Add("price", price);
                           parameters.Add("granularity", granularity);
                           parameters.Add("count", count.ToString());

                           Task<List<CandlestickPlus>> result = Rest20.GetCandlesAsync(instrument, parameters);

                           while (!result.IsCanceled &&
                                  !result.IsCompleted &&
                                  !result.IsFaulted)
                           {
                               Thread.Sleep(10);
                           }

                           List<CandlestickPlus> candleOanda = result.Result;

                           List<Candle> candlesOsEngine = new List<Candle>();

                           for (int i = 0; i < candleOanda.Count; i++)
                           {
                               Candle newCandle = new Candle();
                               newCandle.Open = Convert.ToDecimal(candleOanda[i].bid.o);
                               newCandle.High = Convert.ToDecimal(candleOanda[i].bid.h);
                               newCandle.Low = Convert.ToDecimal(candleOanda[i].bid.l);
                               newCandle.Close = Convert.ToDecimal(candleOanda[i].bid.c);
                               newCandle.TimeStart = DateTime.Parse(candleOanda[i].time);
                               newCandle.State = CandleState.Finished;
                               newCandle.Volume = candleOanda[i].volume;

                               candlesOsEngine.Add(newCandle);
                           }
                           series.CandlesAll = candlesOsEngine;
                       }

                       _candleManager.StartSeries(series);

                       SendLogMessage("Инструмент " + series.Security.Name + "ТаймФрейм " + series.TimeFrame +
                                      " успешно подключен на получение данных и прослушивание свечек",
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
        /// позволяет ли выбранный таймфрейм запросить историю торгов
        /// </summary>
        /// <returns>true - позволяет</returns>
        private bool NeadToGetCandles(TimeFrame frame)
        {
            if (frame == TimeFrame.Sec5)
            {
                return true;
            }
            else if (frame == TimeFrame.Sec10)
            {
                return true;
            }
            else if (frame == TimeFrame.Sec15)
            {
                return true;
            }
            else if (frame == TimeFrame.Sec30)
            {
                return true;
            }
            else if (frame == TimeFrame.Min1)
            {
                return true;
            }
            else if (frame == TimeFrame.Min5)
            {
                return true;
            }
            else if (frame == TimeFrame.Min10)
            {
                return true;
            }
            else if (frame == TimeFrame.Min15)
            {
                return true;
            }
            else if (frame == TimeFrame.Min30)
            {
                return true;
            }
            else if (frame == TimeFrame.Hour1)
            {
                return true;
            }
            else if (frame == TimeFrame.Hour2)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// взять таймфрейм в формате Oanda
        /// </summary>
        private string GetTimeFrameInOandaFormat(TimeFrame frame)
        {
            if (frame == TimeFrame.Sec5)
            {
                return CandleStickGranularity.Seconds05;
            }
            else if (frame == TimeFrame.Sec10)
            {
                return CandleStickGranularity.Seconds10;
            }
            else if (frame == TimeFrame.Sec15)
            {
                return CandleStickGranularity.Seconds15;
            }
            else if (frame == TimeFrame.Sec30)
            {
                return CandleStickGranularity.Seconds30;
            }
            else if (frame == TimeFrame.Min1)
            {
                return CandleStickGranularity.Minutes01;
            }
            else if (frame == TimeFrame.Min5)
            {
                return CandleStickGranularity.Minutes05;
            }
            else if (frame == TimeFrame.Min10)
            {
                return CandleStickGranularity.Minutes10;
            }
            else if (frame == TimeFrame.Min15)
            {
                return CandleStickGranularity.Minutes10;
            }
            else if (frame == TimeFrame.Min30)
            {
                return CandleStickGranularity.Minutes10;
            }
            else if (frame == TimeFrame.Hour1)
            {
                return CandleStickGranularity.Hours01;
            }
            else if (frame == TimeFrame.Hour2)
            {
                return CandleStickGranularity.Hours02;
            }
            return null;
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
        /// стаканы по инструментам
        /// </summary>
        private List<MarketDepth> _marketDepths = new List<MarketDepth>();

        /// <summary>
        /// взять стакан по названию бумаги
        /// </summary>
        public MarketDepth GetMarketDepth(string securityName)
        {
            return _marketDepths.Find(m => m.SecurityNameCode == securityName);
        }

        void _Client_MarketDepthChangeEvent(MarketDepth marketDepth)
        {
            _marketDepthsToSend.Enqueue(marketDepth);
        }


        // СДЕЛАТЬ!!!!!!!!!!!!!!!!!!!!сохранение расширенных данных по трейду 


        /// <summary>
        /// прогрузить трейды данными стакана
        /// </summary>
        private void BathTradeMarketDepthData(List<Trade> trades)
        {
            MarketDepth depth = null;

            for (int i = 0; i < trades.Count; i++)
            {
                if (i != 0 && depth == null &&
                    trades[i - 1].SecurityNameCode == trades[i].SecurityNameCode)
                {
                    continue;
                }

                if (depth == null ||
                    depth.SecurityNameCode != trades[i].SecurityNameCode)
                {
                    depth = _marketDepths.Find(d => d.SecurityNameCode == trades[i].SecurityNameCode);
                }

                if (depth == null ||
                    depth.Asks == null || depth.Asks.Count == 0 ||
                    depth.Bids == null || depth.Bids.Count == 0)
                {
                    return;
                }

                trades[i].Ask = depth.Asks[0].Price;
                trades[i].Bid = depth.Bids[0].Price;
                trades[i].BidsVolume = depth.BidSummVolume;
                trades[i].AsksVolume = depth.AskSummVolume;
            }
        }

        /// <summary>
        /// вызывается когда изменяется бид или аск по инструменту
        /// </summary>
        public event Action<decimal, decimal, Security> NewBidAscIncomeEvent;

        /// <summary>
        /// вызывается когда изменяется стакан
        /// </summary>
        public event Action<MarketDepth> NewMarketDepthEvent;

        //тики

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
        /// входящие тики из системы
        /// </summary>
        private void AddTick(Trade trade)
        {
            _lastTimeIncomeDate = DateTime.Now;
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
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// пришли тики из хранилища тиков. Происходит сразу после загрузки
        /// </summary>
        private void _tickStorage_TickLoadedEvent(List<Trade>[] trades)
        {
            _allTrades = trades;
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

        //мои сделки

        private List<MyTrade> _myTrades;

        /// <summary>
        /// мои сделки
        /// </summary>
        public List<MyTrade> MyTrades
        {
            get { return _myTrades; }
        }

        private void _ibClient_NewMyTradeEvent(MyTrade trade)
        {
            if (_myTrades == null)
            {
                _myTrades = new List<MyTrade>();
            }
            _myTrades.Add(trade);
            _myTradesToSend.Enqueue(trade);
        }

        /// <summary>
        /// вызывается когда приходит новая моя сделка
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent;

        //исполнение ордеров

        /// <summary>
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
                                _Client.ExecuteOrder(order);
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
                                _Client.CanselOrder(order);
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

        /// <summary>
        /// очередь ордеров для выставления в систему
        /// </summary>
        private ConcurrentQueue<Order> _ordersToExecute;

        /// <summary>
        /// очередь ордеров для отмены в системе
        /// </summary>
        private ConcurrentQueue<Order> _ordersToCansel;

        /// <summary>
        /// ордера в формате IB
        /// </summary>
        private List<Order> _orders;

        /// <summary>
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
        /// отменить ордер
        /// </summary>
        public void CanselOrder(Order order)
        {
            _ordersToCansel.Enqueue(order);
        }

        private void _ibClient_NewOrderEvent(Order order)
        {
            try
            {
                if (_orders == null)
                {
                    _orders = new List<Order>();
                }

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

        //обработка лога

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
}