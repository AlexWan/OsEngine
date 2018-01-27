/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using OsEngine.Market.Servers.BitStamp.BitStampEntity;


namespace OsEngine.Market.Servers.BitStamp
{
    public class BitStampServer: IServer
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public BitStampServer(bool neadToLoadTicks)
        {
            ServerStatus = ServerConnectStatus.Disconnect;
            ServerType = ServerType.BitStamp;

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

            Thread ordersExecutor = new Thread(ExecutorOrdersThreadArea);
            ordersExecutor.CurrentCulture = new CultureInfo("ru-RU");
            ordersExecutor.IsBackground = true;
            ordersExecutor.Start();

            _logMaster = new Log("BitStampServer");
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
        }

        /// <summary>
        /// взять тип сервера. 
        /// </summary>
        public ServerType ServerType { get; set; }

//сервис

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
            BitStampServerUi ui = new BitStampServerUi(this, _logMaster);
            ui.ShowDialog();
        }

        /// <summary>
        /// публичный ключ пользователя
        /// </summary>
        public string UserId;

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
            if (!File.Exists(@"Engine\" + @"BitStampServer.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"BitStampServer.txt"))
                {
                    UserId = reader.ReadLine();
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
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"BitStampServer.txt", false))
                {
                    writer.WriteLine(UserId);
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
        void Сlient_Connected()
        {
            ServerStatus = ServerConnectStatus.Connect;
        }

// статус соединения

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
                    if (_clientBitStamp == null)
                    {
                        SendLogMessage("Создаём коннектор BitStamp", LogMessageType.System);
                        CreateNewServer();
                        continue;
                    }

                    bool stateIsActiv = _clientBitStamp.IsConnected;

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
                        Thread threadGetSec = new Thread(GetSecurities);
                        threadGetSec.CurrentCulture = new CultureInfo("ru-RU");
                        threadGetSec.IsBackground = true;
                        threadGetSec.Start();
                        _getPortfoliosAndSecurities = true;
                        continue;
                    }

                    if (_portfolios == null || _portfolios.Count == 0)
                    {
                        SubscribePortfolio();
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
            if (_clientBitStamp == null)
            {
                _clientBitStamp = new BitstampClient(UserKey,UserPrivateKey,UserId);
                _clientBitStamp.Connected += Сlient_Connected;
                _clientBitStamp.UpdatePairs += _clientBitStamp_UpdatePairs;
                _clientBitStamp.Disconnected += ClientnDisconnected;
                _clientBitStamp.UpdatePortfolio += UpdatePortfolios;
                _clientBitStamp.UpdateMarketDepth += UpdateMarketDepth;
                _clientBitStamp.NewTradesEvent += NewTrades;
                _clientBitStamp.MyTradeEvent += NewMyTrade;
                _clientBitStamp.MyOrderEvent += BitMex_UpdateOrder;
                _clientBitStamp.LogMessageEvent += SendLogMessage;

                _clientBitStamp.NeadReconnectEvent += _clientBitStamp_NeadReconnectEvent;
            }
        }

        void _clientBitStamp_NeadReconnectEvent()
        {
            StopServer();
            Thread.Sleep(5000);
            StartServer();
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
        /// начать процесс подключения
        /// </summary>
        private void Connect()
        {
            _lastStartServerTime = DateTime.Now;

            _clientBitStamp.Connect();
            Thread.Sleep(1000);
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
        /// подписываемся на обновление портфеля и позиций
        /// </summary>
        private void SubscribePortfolio()
        {
            _clientBitStamp.GetBalance();
            Thread.Sleep(500);
        }

        /// <summary>
        /// привести программу к моменту запуска. Очистить все объекты участвующие в подключении к серверу
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private void Dispose()
        {
            if (_clientBitStamp != null)
            {
                _clientBitStamp.Dispose();

                _clientBitStamp.Connected -= Сlient_Connected;
                _clientBitStamp.Disconnected -= ClientnDisconnected;
                _clientBitStamp.UpdatePortfolio -= UpdatePortfolios;
                _clientBitStamp.UpdateMarketDepth -= UpdateMarketDepth;
                _clientBitStamp.NewTradesEvent -= NewTrades;
                _clientBitStamp.MyTradeEvent -= NewMyTrade;
                _clientBitStamp.MyOrderEvent -= BitMex_UpdateOrder;
            }

            _clientBitStamp = null;

            _candleManager = null;

            _startListeningPortfolios = false;

            _getPortfoliosAndSecurities = false;
        }

        /// <summary>
        /// bitmex client
        /// </summary>
        private BitstampClient _clientBitStamp;

// работа потока рассылки !!!!!

        #region MyRegion

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
        private void UpdatePortfolios(BalanceResponse portf)
        {
            try
            {
                if (portf == null ||
                    portf.eur_balance == null)
                {
                    return;
                }

                if (_portfolios == null)
                {
                    _portfolios = new List<Portfolio>();
                }

                Portfolio osPortEur = _portfolios.Find(p => p.Number == "eurPortfolio");

                if (osPortEur == null)
                {
                    osPortEur = new Portfolio();
                    osPortEur.Number = "eurPortfolio";
                    _portfolios.Add(osPortEur);
                }

                osPortEur.ValueBegin = Convert.ToDecimal(portf.eur_balance.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                osPortEur.ValueBlocked = Convert.ToDecimal(portf.eur_reserved.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);

                Portfolio osPortUsd = _portfolios.Find(p => p.Number == "usdPortfolio");

                if (osPortUsd == null)
                {
                    osPortUsd = new Portfolio();
                    osPortUsd.Number = "usdPortfolio";
                    _portfolios.Add(osPortUsd);
                }

                osPortUsd.ValueBegin = Convert.ToDecimal(portf.usd_balance.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                osPortUsd.ValueBlocked = Convert.ToDecimal(portf.usd_reserved.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);


                Portfolio osPortBtc = _portfolios.Find(p => p.Number == "btcPortfolio");

                if (osPortBtc == null)
                {
                    osPortBtc = new Portfolio();
                    osPortBtc.Number = "btcPortfolio";
                    _portfolios.Add(osPortBtc);
                }

                osPortBtc.ValueBegin = Convert.ToDecimal(portf.btc_balance.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                osPortBtc.ValueBlocked = Convert.ToDecimal(portf.btc_reserved.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);

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
                _clientBitStamp.GetSecurities();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(),LogMessageType.Error);
            }
        }

        /// <summary>
        /// обновился список бумаг
        /// </summary>
        void _clientBitStamp_UpdatePairs(List<PairInfoResponse> pairs)
        {
            if (_securities == null)
            {
                 _securities = new List<Security>();
            }
            for (int i = 0; i < pairs.Count; i++)
            {

                Security security = new Security();
                security.Name = pairs[i].url_symbol;
                security.NameFull = pairs[i].url_symbol;
                security.NameClass = "currency";
                security.Lot = 1;
                security.PriceStep = 0.01m;
                security.PriceStepCost = 0.01m;

                if (security.PriceStep < 1)
                {
                    security.Decimals = Convert.ToString(security.PriceStep).Split(',')[1].Length;
                }
                else
                {
                    security.Decimals = 0;
                }


                security.State = SecurityStateType.Activ;

                _securities.Add(security);

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
                if (_lastStartServerTime.AddSeconds(15) > DateTime.Now)
                {
                    return null;
                }

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

                    CandleSeries series = new CandleSeries(timeFrameBuilder, security);

                    _clientBitStamp.SubscribleTradesAndDepths(security);

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
        /// <param name="quotes"></param>
        private void UpdateMarketDepth(MarketDepth myDepth)
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
                            Ask = myDepth.Bids[0].Price,
                            Bid = myDepth.Asks[0].Price,
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
        private void NewTrades(Trade trade)
        {
            try
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
                try
                {
                    Thread.Sleep(20);
                    if (_ordersToExecute != null && _ordersToExecute.Count != 0)
                    {
                        Order order;
                        if (_ordersToExecute.TryDequeue(out order))
                        {
                            _clientBitStamp.ExecuteOrder(order);
                        }
                    }
                    else if (_ordersToCansel != null && _ordersToCansel.Count != 0)
                    {
                        Order order;
                        if (_ordersToCansel.TryDequeue(out order))
                        {
                            _clientBitStamp.CanselOrder(order);
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
        /// входящий из системы ордер
        /// </summary>
        private void BitMex_UpdateOrder(Order myOrder)
        {
            _ordersToSend.Enqueue(myOrder);
        }

        /// <summary>
        /// выслать ордер на исполнение в торговую систему
        /// </summary>
        /// <param name="order">ордер</param>
        public void ExecuteOrder(Order order)
        {
            order.TimeCreate = ServerTime;
            _ordersToExecute.Enqueue(order);
        }

        /// <summary>
        /// отозвать ордер из торговой системы
        /// </summary>
        /// <param name="order">ордер</param>
        public void CanselOrder(Order order)
        {
            _ordersToCansel.Enqueue(order);
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
        private Log _logMaster;

        /// <summary>
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }

}
