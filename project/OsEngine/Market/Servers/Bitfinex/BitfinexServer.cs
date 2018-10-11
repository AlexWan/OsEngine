using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Bitfinex.BitfitnexEntity;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.Bitfinex
{
    public class BitfinexServer: IServer
    {
        public BitfinexServer(bool neadToLoadTicks)
        {
            ServerStatus = ServerConnectStatus.Disconnect;
            ServerType = ServerType.Bitfinex;

            Load();

            _ordersToSend = new ConcurrentQueue<Order>();
            _tradesToSend = new ConcurrentQueue<List<Trade>>();
            _portfolioToSend = new ConcurrentQueue<List<Portfolio>>();
            _securitiesToSend = new ConcurrentQueue<List<Security>>();
            _myTradesToSend = new ConcurrentQueue<MyTrade>();
            _newServerTime = new ConcurrentQueue<DateTime>();
            _candleSeriesToSend = new ConcurrentQueue<CandleSeries>();
            _marketDepthsToSend = new ConcurrentQueue<MarketDepth>();
            _bidAskToSend = new ConcurrentQueue<BidAskSender>();
            _ordersToExecute = new ConcurrentQueue<Order>();
            _ordersToCansel = new ConcurrentQueue<Order>();

            _tickStorage = new ServerTickStorage(this);
            _tickStorage.NeadToSave = NeadToSaveTicks;
            _tickStorage.DaysToLoad = CountDaysTickNeadToSave;
            _tickStorage.TickLoadedEvent += _tickStorage_TickLoadedEvent;
            _tickStorage.LogMessageEvent += SendLogMessage;

            if (neadToLoadTicks)
            {
                _tickStorage.LoadTick();
            }

            _logMaster = new Log("BitfinexServer");
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

            Thread ordersExecutor = new Thread(ExecutorOrdersThreadArea);
            ordersExecutor.CurrentCulture = new CultureInfo("ru-RU");
            ordersExecutor.IsBackground = true;
            ordersExecutor.Start();
        }

        #region Сервис

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
            }
        }
        private int _countDaysTickNeadToSave;

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
        private bool _neadToSaveTicks;

        /// <summary>
        /// показать настройки
        /// </summary>
        public void ShowDialog()
        {
            BitfinexServerUi ui = new BitfinexServerUi(this, _logMaster);
            ui.ShowDialog();
        }

        /// <summary>
        /// публичный ключ пользователя
        /// </summary>
        public string UserKey;

        /// <summary>
        /// секретный ключ пользователя
        /// </summary>
        public string UserPrivateKey;

        /// <summary>
        /// загрузить настройки сервера из файла
        /// </summary>
        public void Load()
        {
            if (!File.Exists(@"Engine\" + @"BitFinexServer.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"BitFinexServer.txt"))
                {
                    UserKey = reader.ReadLine();
                    UserPrivateKey = reader.ReadLine();
                    _countDaysTickNeadToSave = Convert.ToInt32(reader.ReadLine());
                    _neadToSaveTicks = Convert.ToBoolean(reader.ReadLine());

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
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"BitFinexServer.txt", false))
                {

                    writer.WriteLine(UserKey);
                    writer.WriteLine(UserPrivateKey);
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

        #endregion

        #region Подключение/отключение

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
        /// пришло оповещение от клиента, что соединение установлено
        /// </summary>
        void СlientConnected()
        {
            ServerStatus = ServerConnectStatus.Connect;
        }

        public ServerType ServerType { get; set; }

        #endregion

        #region Работа основного потока

        /// <summary>
        /// основной поток, следящий за подключением, загрузкой портфелей и бумаг, пересылкой данных на верх
        /// </summary>
        private Thread _threadPrime;

        /// <summary>
        /// клиент подключения к бирже Bitfinex
        /// </summary>
        private BitfinexClient _client;

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
                    if (_client == null)
                    {
                        SendLogMessage("Создаём коннектор Bitfinex", LogMessageType.System);
                        CreateNewServer();
                        continue;
                    }

                    bool stateIsActiv = _client.IsConnected;

                    if (stateIsActiv == false && _serverStatusNead == ServerConnectStatus.Connect)
                    {
                        SendLogMessage("Запущена процедура активации подключения", LogMessageType.System);
                        Dispose();
                        CreateNewServer();
                        Connect();
                        continue;
                    }

                    if (stateIsActiv && _serverStatusNead == ServerConnectStatus.Disconnect)
                    {
                        SendLogMessage("Запущена процедура отключения подключения", LogMessageType.System);
                        Dispose();
                        continue;
                    }

                    if (stateIsActiv == false)
                    {
                        continue;
                    }

                    if (_candleManager == null)
                    {
                        SendLogMessage("Создаём менеджер свечей", LogMessageType.System);
                        StartCandleManager();
                        continue;
                    }

                    if (_getPortfoliosAndSecurities == false)
                    {
                        SendLogMessage("Скачиваем бумаги и портфели", LogMessageType.System);
                        SubscribePortfolio();
                        GetSecurities();
                        _getPortfoliosAndSecurities = true;
                        continue;
                    }

                    if (_portfolios == null || _portfolios.Count == 0)
                    {
                        //SubscribePortfolio();
                    }

                    if (_startListeningPortfolios == false)
                    {
                        if (_portfolios != null)
                        {
                            SendLogMessage("Подписываемся на обновления портфелей. Берём активные ордера",
                                LogMessageType.System);
                            _startListeningPortfolios = true;
                        }
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
        /// включена ли прослушка портфелей
        /// </summary>
        private bool _startListeningPortfolios;

        /// <summary>
        /// скачаны ли портфели и бумаги
        /// </summary>
        private bool _getPortfoliosAndSecurities;

        /// <summary>
        /// создать новое подключение
        /// </summary>
        private void CreateNewServer()
        {
            if (_client == null)
            {
                _client = new BitfinexClient();
                _client.Connected += СlientConnected;
                _client.Disconnected += ClientnDisconnected;
                _client.NewPortfolio += NewPortfolios;
                _client.UpdatePortfolio += UpdatePortfolios;
                _client.UpdateMarketDepth += UpdateMarketDepth;
                _client.NewTradesEvent += NewTrades;
                _client.NewMarketDepth += NewMarketDepth;
                _client.MyTradeEvent += NewMyTrade;
                _client.MyOrderEvent += Bitfinex_UpdateOrder;
                _client.LogMessageEvent += SendLogMessage;
            }
        }

        /// <summary>
        /// начать процесс подключения
        /// </summary>
        private void Connect()
        {
            _lastStartServerTime = DateTime.Now;

            _client.Connect(UserKey, UserPrivateKey);
            Thread.Sleep(1000);
        }

        /// <summary>
        /// соединение с клиентом разорвано
        /// </summary>
        private void ClientnDisconnected()
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

        /// <summary>
        /// привести программу к моменту запуска. Очистить все объекты участвующие в подключении к серверу
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private void Dispose()
        {
            if (_client != null)
            {
                _client.Dispose();

                _client.Connected -= СlientConnected;
                _client.Disconnected -= ClientnDisconnected;
                _client.NewPortfolio -= NewPortfolios;
                _client.UpdatePortfolio -= UpdatePortfolios;
                _client.UpdateMarketDepth -= UpdateMarketDepth;
                _client.NewTradesEvent -= NewTrades;
                _client.NewMarketDepth -= NewMarketDepth;
                _client.MyTradeEvent -= NewMyTrade;
                _client.MyOrderEvent -= Bitfinex_UpdateOrder;
                _client.LogMessageEvent -= SendLogMessage;
            }

            _client = null;

            _candleManager = null;

            _startListeningPortfolios = false;

            _getPortfoliosAndSecurities = false;
        }

        #endregion

        #region Работа потока рассылки

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
        /// место работы потока рассылки
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

        #region Время сервера

        private DateTime _serverTime;

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
            }
        }

        /// <summary>
        /// изменилось время сервера
        /// </summary>
        public event Action<DateTime> TimeServerChangeEvent;

        #endregion

        #region Статус соединения

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
        public event Action<string> ConnectStatusChangeEvent;

        #endregion

        #region Инструменты

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
        /// получить инструменты
        /// </summary>
        private void GetSecurities()
        {
            try
            {
                var securities = _client.GetSecurities();

                if (_securities == null)
                {
                    _securities = new List<Security>();
                }

                foreach (var sec in securities)
                {
                    Security security = new Security();

                    security.Name = sec.pair.ToUpper();

                    security.NameFull = sec.pair;

                    security.NameClass = sec.pair.Substring(3);
                    
                    security.Lot = 1m;

                    if (security.NameClass == "BTC")
                    {
                        security.Decimals = 6;

                        security.PriceStep = 0.000001m;
                    }
                    
                    security.State = SecurityStateType.Activ;

                    _securities.Add(security);

                }

                _securitiesToSend.Enqueue(_securities);

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// обновить инструмент
        /// </summary>
        private void UpdateSecurity(string nameSec, decimal price)
        {
            try
            {
                var needSec = _securities.Find(sec => sec.Name == nameSec);

                if (needSec != null)
                {
                    string[] decimals = price.ToString(CultureInfo.InvariantCulture).Split('.');

                    if (decimals.Length == 1)
                    {
                        needSec.Decimals = 0;
                    }
                    else
                    {
                        needSec.Decimals = decimals[1].Length;
                    }

                    switch (needSec.Decimals)
                    {
                        case 0:
                            needSec.PriceStep = 1;
                            break;

                        case 1:
                            needSec.PriceStep = 0.1m;
                            break;

                        case 2:
                            needSec.PriceStep = 0.01m;
                            break;

                        case 3:
                            needSec.PriceStep = 0.001m;
                            break;

                        case 4:
                            needSec.PriceStep = 0.0001m;
                            break;

                        case 5:
                            needSec.PriceStep = 0.00001m;
                            break;

                        case 6:
                            needSec.PriceStep = 0.000001m;
                            break;

                        case 7:
                            needSec.PriceStep = 0.0000001m;
                            break;

                        case 8:
                            needSec.PriceStep = 0.00000001m;
                            break;
                    }

                    needSec.PriceStepCost = needSec.PriceStep;

                    _securitiesToSend.Enqueue(_securities);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
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

        #endregion

        #region Портфели

        private List<Portfolio> _portfolios;

        /// <summary>
        /// все счета в системе
        /// </summary>
        public List<Portfolio> Portfolios
        {
            get { return _portfolios; }
        }

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
        /// подписываемся на обновление портфеля и позиций
        /// </summary>
        private void SubscribePortfolio()
        {
            try
            {
                _client.SubscribeUserData();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// пришли портфели
        /// </summary>
        /// <param name="portfolios"></param>
        private void NewPortfolios(List<List<WaletWalet>> portfolios)
        {
            try
            {
                if (_portfolios == null)
                {
                    _portfolios = new List<Portfolio>();
                }

                for (int i = 0; i < portfolios.Count; i++)
                {
                    List<WaletWalet> portfolio = portfolios[i];

                    Portfolio newPortf = new Portfolio();
                    newPortf.Number = portfolio[1].String;

                    if (_portfolios.Find(p => p.Number == newPortf.Number) != null)
                    {
                        newPortf = _portfolios.Find(p => p.Number == newPortf.Number);
                    }

                    newPortf.ValueCurrent = Convert.ToDecimal(portfolio[2].Double);
                    newPortf.ValueBlocked = Convert.ToDecimal(portfolio[3].Double);
                    newPortf.ValueBegin = Convert.ToDecimal(portfolio[2].Double);

                    if (_portfolios.Find(p => p.Number == newPortf.Number) == null)
                    {
                        _portfolios.Add(newPortf);
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
        /// обновился портфель
        /// </summary>
        /// <param name="walletUpdateWalletUpdates"></param>
        private void UpdatePortfolios(List<WalletUpdateWalletUpdate> walletUpdateWalletUpdates)
        {
            try
            {
                if (_portfolios == null)
                {
                    _portfolios = new List<Portfolio>();
                }

                var needPortfolio = _portfolios.Find(p => p.Number == walletUpdateWalletUpdates[1].String);

                if (needPortfolio == null)
                {
                    needPortfolio = new Portfolio();

                    needPortfolio.Number = walletUpdateWalletUpdates[1].String;
                    needPortfolio.ValueCurrent = Convert.ToDecimal(walletUpdateWalletUpdates[2].Double);
                    needPortfolio.ValueBlocked = Convert.ToDecimal(walletUpdateWalletUpdates[3].Double);

                    _portfolios.Add(needPortfolio);
                }
                else
                {
                    needPortfolio.Number = walletUpdateWalletUpdates[1].String;
                    needPortfolio.ValueCurrent = Convert.ToDecimal(walletUpdateWalletUpdates[2].Double);
                    needPortfolio.ValueBlocked = Convert.ToDecimal(walletUpdateWalletUpdates[3].Double);
                }
               
                _portfolioToSend.Enqueue(_portfolios);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }


        /// <summary>
        /// обновились портфели
        /// </summary>
        public event Action<List<Portfolio>> PortfoliosChangeEvent;

        #endregion

        #region Подпись на данные

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
                if (_startListeningPortfolios == false)
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

                    CandleSeries series = new CandleSeries(timeFrameBuilder, security);

                    _client.SubscribleTradesAndDepths(security);

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

        #endregion
        
        #region тики

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
        /// блокиратор многопоточного доступа к тикам
        /// </summary>
        private readonly object _newTradesLoker = new object();

        /// <summary>
        /// пришли новые тики
        /// </summary>
        /// <param name="trades"></param>
        /// <param name="secName"></param>
        private void NewTrades(List<ChangedElement> trades, string secName)
        {
            try
            {
                lock (_newTradesLoker)
                {
                    if (trades == null || trades.Count == 0)
                    {
                        return;
                    }

                    Trade trade = new Trade();
                    trade.SecurityNameCode = secName;
                    trade.Price = Convert.ToDecimal(trades[5].Double);
                    trade.Id = trades[3].Double.ToString();
                    trade.Time = TakeDateFromTicks(trades[4].Double);

                    var amount = Convert.ToDecimal(trades[6].Double);

                    trade.Volume = Convert.ToDecimal(Math.Abs(amount));
                    trade.Side = amount > 0 ? Side.Buy : Side.Sell;

                    
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
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public event Action<List<Trade>> NewTradeEvent;

        #endregion

        #region Стаканы

        /// <summary>
        /// все стаканы
        /// </summary>
        private List<MarketDepth> _depths;

        private readonly object _depthLocker = new object();

        /// <summary>
        /// пришел новый стакан
        /// </summary>
        /// <param name="newOrderBook"></param>
        /// <param name="nameSecurity"></param>
        private void NewMarketDepth(List<DataObject> newOrderBook, string nameSecurity)
        {
            try
            {
                lock (_depthLocker)
                {
                    if (_depths == null)
                    {
                        _depths = new List<MarketDepth>();
                    }

                    var needDepth = _depths.Find(depth =>
                        depth.SecurityNameCode == nameSecurity);

                    if (needDepth == null)
                    {
                        needDepth = new MarketDepth();
                        needDepth.SecurityNameCode = nameSecurity;
                        _depths.Add(needDepth);
                    }

                    List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                    List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                    foreach (var value in newOrderBook[1].Values)
                    {
                        // value[2] - объем на уровне, если > 0 значит бид, иначе аск
                        if (value[2] > 0)
                        {
                            bids.Add(new MarketDepthLevel()
                            {
                                Bid = Convert.ToDecimal(value[2]),
                                Price = Convert.ToDecimal(value[0]),
                            });
                        }
                        else
                        {
                            ascs.Add(new MarketDepthLevel()
                            {
                                Ask = Convert.ToDecimal(Math.Abs(value[2])),
                                Price = Convert.ToDecimal(value[0]),
                            });
                        }
                    }

                    needDepth.Asks = ascs;
                    needDepth.Bids = bids;
                    needDepth.Time = ServerTime;

                    if (NewMarketDepthEvent != null)
                    {
                        _marketDepthsToSend.Enqueue(needDepth.GetCopy());

                        if (needDepth.Asks.Count != 0 && needDepth.Bids.Count != 0)
                        {
                            _bidAskToSend.Enqueue(new BidAskSender
                            {
                                Ask = needDepth.Bids[0].Price,
                                Bid = needDepth.Asks[0].Price,
                                Security = nameSecurity != null ? GetSecurityForName(nameSecurity) : null
                            });
                        }
                    }

                    UpdateSecurity(nameSecurity, needDepth.Bids[0].Price);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// изменился стакан
        /// </summary>
        /// <param name="newData">измененные данные</param>
        /// <param name="nameSecurity">имя инструмента</param>
        private void UpdateMarketDepth(List<ChangedElement> newData, string nameSecurity)
        {
            try
            {
                lock (_depthLocker)
                {
                    if(_depths == null)
                    {
                        return;
                    }
                    var needDepth = _depths.Find(depth =>
                        depth.SecurityNameCode == nameSecurity);

                    if (needDepth == null)
                    {
                        return;
                    }

                    if(newData.Count < 4)
                    {
                        return;
                    }

                    var price = Convert.ToDecimal(newData[1].Double);

                    var count = Convert.ToDecimal(newData[2].Double);

                    var amount = Convert.ToDecimal(newData[3].Double);

                    needDepth.Time = ServerTime;

                    // если колл-во ореров равно 0, значит надо найти уровень этой цены и удалить его
                    if (count == 0)
                    {
                        // удаляем уровень
                        if (amount < 0)  // удаляем из асков
                        {
                            needDepth.Asks.Remove(needDepth.Asks.Find(level => level.Price == price));
                        }

                        if (amount > 0)  // удаляем из бидов
                        {
                            needDepth.Bids.Remove(needDepth.Bids.Find(level => level.Price == price));
                        }
                        return;
                    }

                    // если объем больше нуля, значит изменился какой-то бид, находим его и обновляем
                    else if (amount > 0)
                    {
                        var needLevel = needDepth.Bids.Find(bid => bid.Price == price);

                        if(needLevel == null)  // если такого уровня нет, добавляем его
                        {
                            needDepth.Bids.Add(new MarketDepthLevel()
                            {
                                Bid = amount,
                                Price = price
                            });

                            needDepth.Bids.Sort((level, depthLevel) => level.Price > depthLevel.Price ? -1 : level.Price < depthLevel.Price ? 1 : 0);
                        }
                        else
                        {
                            needLevel.Bid = amount;
                        }
                        
                    }
                    // если меньше, значит обновляем аск
                    else if(amount < 0)
                    {
                        var needLevel = needDepth.Asks.Find(asc => asc.Price == price);

                        if (needLevel == null)  // если такого уровня нет, добавляем его
                        {
                            needDepth.Asks.Add(new MarketDepthLevel()
                            {
                                Ask = Math.Abs(amount),
                                Price = price
                            });

                            needDepth.Asks.Sort((level, depthLevel) => level.Price > depthLevel.Price ? 1 : level.Price < depthLevel.Price ? -1 : 0);
                        }
                        else
                        {
                            needLevel.Ask = Math.Abs(amount);
                        }

                    }
                    
                    if (NewMarketDepthEvent != null)
                    {
                        _marketDepthsToSend.Enqueue(needDepth.GetCopy());

                        if (needDepth.Asks.Count != 0 && needDepth.Bids.Count != 0)
                        {
                            _bidAskToSend.Enqueue(new BidAskSender
                            {
                                Ask = needDepth.Bids[0].Price,
                                Bid = needDepth.Asks[0].Price,
                                Security = nameSecurity != null ? GetSecurityForName(nameSecurity) : null
                            });
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public event Action<decimal, decimal, Security> NewBidAscIncomeEvent;
        public event Action<MarketDepth> NewMarketDepthEvent;

        #endregion

        #region Мои сделки

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
        private void NewMyTrade(MyTrade trade)
        {
            _myTradesToSend.Enqueue(trade);

            if (_myTrades == null)
            {
                _myTrades = new List<MyTrade>();
            }

            _myTrades.Add(trade);
        }

        /// <summary>
        /// изменилась моя сделка
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent;

        #endregion

        #region Работа с ордерами

        /// <summary>
        /// входящий из системы ордер
        /// </summary>
        private void Bitfinex_UpdateOrder(Order myOrder)
        {
           _ordersToSend.Enqueue(myOrder);
        }

        /// <summary>
        /// место работы потока на очередях исполнения заявок и их отмены
        /// </summary>
        private void ExecutorOrdersThreadArea()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(20);
                    if (_ordersToExecute != null && _ordersToExecute.Count != 0)
                    {
                        Order order;
                        if (_ordersToExecute.TryDequeue(out order))
                        {
                            _client.ExecuteOrder(order);
                        }
                    }
                    else if (_ordersToCansel != null && _ordersToCansel.Count != 0)
                    {
                        Order order;
                        if (_ordersToCansel.TryDequeue(out order))
                        {
                            _client.CanselOrder(order);
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
        private ConcurrentQueue<Order> _ordersToExecute;

        /// <summary>
        /// очередь ордеров для отмены в системе
        /// </summary>
        private ConcurrentQueue<Order> _ordersToCansel;

        /// <summary>
        /// выслать ордер на исполнение в торговую систему
        /// </summary>
        /// <param name="order">ордер</param>
        public void ExecuteOrder(Order order)
        {
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
            SendLogMessage("Отзываем ордер: " + order.NumberUser, LogMessageType.System);
            _ordersToCansel.Enqueue(order);
        }

        public event Action<Order> NewOrderIncomeEvent;

        #endregion
        
        #region Сообщения для лога

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

        #endregion

        #region Свечи

        private readonly object _candlesLocker = new object();
         
        /// <summary>
        /// взять свечи по инструменту
        /// </summary>
        /// <param name="securityName"></param>
        /// <param name="seriesTimeFrameSpan"></param>
        /// <returns></returns>
        public List<Candle> GetCandleHistory(string securityName, TimeSpan seriesTimeFrameSpan)
        {
            try
            {
                lock (_candlesLocker)
                {
                    int tf = Convert.ToInt32(seriesTimeFrameSpan.TotalMinutes);

                    if (tf == 1 || tf == 2 || tf == 3)
                    {
                        // строим свечи из минуток
                        var rawCandles = GetBitfinexCandles("1m", securityName);

                        var readyCandles = TransformCandles(1 , tf, rawCandles);

                        return readyCandles;

                    }
                    else if (tf == 5 || tf == 10 || tf == 20)
                    {
                        // строим свечи из 5минуток
                        var rawCandles = GetBitfinexCandles("5m", securityName);

                        var readyCandles = TransformCandles(5, tf, rawCandles);

                        return readyCandles;
                    }
                    else if (tf == 15 || tf == 30 || tf == 45)
                    {
                        // строим свечи из 15минуток
                        var rawCandles = GetBitfinexCandles("15m", securityName);

                        var readyCandles = TransformCandles(15, tf, rawCandles);

                        return readyCandles;
                    }
                    else if (tf == 60 || tf == 120)
                    {
                        // строим свечи из часовиков
                        var rawCandles = GetBitfinexCandles("1h", securityName);

                        var readyCandles = TransformCandles(60, tf, rawCandles);

                        return readyCandles;
                    }
                    else if (tf == 1440)
                    {
                        // строим свечи из дневок
                        var rawCandles = GetBitfinexCandles("1D", securityName);

                        List<Candle> daily = new List<Candle>();
                        
                        for (int i = rawCandles.Count-1; i > 0; i--)
                        {
                            Candle candle = new Candle();
                            candle.TimeStart = new DateTime(1970, 1, 1) + TimeSpan.FromMilliseconds(rawCandles[i][0]);
                            candle.Open = Convert.ToDecimal(rawCandles[i][1]);
                            candle.Close = Convert.ToDecimal(rawCandles[i][2]);
                            candle.High = Convert.ToDecimal(rawCandles[i][3]);
                            candle.Low = Convert.ToDecimal(rawCandles[i][4]);
                            candle.Volume = Convert.ToDecimal(rawCandles[i][5]);

                            daily.Add(candle);
                        }
                        return daily;
                    }
                    return null;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// получить с биржи свечи нужного ТФ
        /// </summary>
        /// <param name="tf"></param>
        /// <param name="security"></param>
        /// <returns></returns>
        private List<List<double>> GetBitfinexCandles(string tf, string security)
        {
            try
            {
                Thread.Sleep(8000);
                Dictionary<string, string> param = new Dictionary<string, string>();
                param.Add("trade:" + tf, ":t" + security + "/hist" + "?limit=1000");
                var candles = _client.GetCandles(param);
                var candleHist = Candles.FromJson(candles);

                param = new Dictionary<string, string>();
                param.Add("trade:" + tf, ":t" + security + "/last");
                var lastCandle = _client.GetCandles(param);
                var candleLast = LastCandle.FromJson(lastCandle);

                candleHist.Add(candleLast);

                return candleHist;
            }
            catch (Exception e)
            {
                SendLogMessage("Ошибка в методе разбора свечей", LogMessageType.Error);
                return null;
            }            
        }

        /// <summary>
        /// преобразовать свечи
        /// </summary>
        /// <param name="tf">пришедший таймфрейм</param>
        /// <param name="needTf">тф который нужно получить</param>
        /// <param name="rawCandles">заготовки для свечей</param>
        /// <returns></returns>
        private List<Candle> TransformCandles(int tf, int needTf, List<List<double>> rawCandles)
        {
            var entrance = needTf / tf;

            List<Candle> newCandles = new List<Candle>();

            //MTS,  - 0
            //OPEN, - 1 
            //CLOSE,- 2
            //HIGH, - 3
            //LOW,  - 4
            //VOLUME- 5

            int count = 0;

            Candle newCandle = new Candle();

            bool isStart = true;

            for (int i = rawCandles.Count-1; i > 0; i--)
            {
                var time = new DateTime(1970, 1, 1) + TimeSpan.FromMilliseconds(rawCandles[i][0]);

                if (time.Minute % needTf != 0 && isStart)
                {
                    continue;
                }

                isStart = false;

                count++;
                if (count == 1)
                {
                    newCandle = new Candle();
                    var resOpen = rawCandles[i];
                    newCandle.Open = Convert.ToDecimal(rawCandles[i][1]);
                    newCandle.TimeStart = time;
                    newCandle.Low = Decimal.MaxValue;
                    newCandle.High = Decimal.MinValue;
                }

                newCandle.High = Convert.ToDecimal(rawCandles[i][3]) > newCandle.High
                    ? Convert.ToDecimal(rawCandles[i][3])
                    : newCandle.High;

                newCandle.Low = Convert.ToDecimal(rawCandles[i][4]) < newCandle.Low
                    ? Convert.ToDecimal(rawCandles[i][4])
                    : newCandle.Low;

                newCandle.Volume += Convert.ToDecimal(rawCandles[i][5]);

                if (i == 1 && count != entrance)
                {
                    newCandle.Close = Convert.ToDecimal(rawCandles[i][2]);
                    newCandle.State = CandleState.None;
                    newCandles.Add(newCandle);
                }

                if (count == entrance)
                {
                    newCandle.Close = Convert.ToDecimal(rawCandles[i][2]);
                    newCandle.State = CandleState.Finished;
                    newCandles.Add(newCandle);
                    count = 0;
                }
            }            
            return newCandles;
        }

        #endregion
        
        /// <summary>
        /// преобразует timestamp в datetime
        /// </summary>
        /// <param name="timeStamp"></param>
        /// <returns></returns>
        private DateTime TakeDateFromTicks(double? timeStamp)
        {
            DateTime yearBegin = new DateTime(1970, 1, 1);

            if (timeStamp.HasValue)
            {
                var time = yearBegin + TimeSpan.FromSeconds((double)timeStamp);

                return time;
            }
            
            return DateTime.MinValue;
        }
    }
}
