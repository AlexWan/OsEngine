using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.BitMex.BitMexEntity;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;


namespace OsEngine.Market.Servers.BitMex
{
    public class BitMexServer: IServer
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public BitMexServer(bool neadToLoadTicks)
        {
            ServerStatus = ServerConnectStatus.Disconnect;
            ServerType = ServerType.BitMex;

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

            if (neadToLoadTicks)
            {
                _tickStorage = new ServerTickStorage(this);
                _tickStorage.NeadToSave = NeadToSaveTicks;
                _tickStorage.DaysToLoad = CountDaysTickNeadToSave;
                _tickStorage.TickLoadedEvent += _tickStorage_TickLoadedEvent;
                _tickStorage.LogMessageEvent += SendLogMessage;
                _tickStorage.LoadTick();
            }

            Thread ordersExecutor = new Thread(ExecutorOrdersThreadArea);
            ordersExecutor.CurrentCulture = new CultureInfo("ru-RU");
            ordersExecutor.IsBackground = true;
            ordersExecutor.Start();

            _logMaster = new Log("BitMexServer");
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
        /// <returns></returns>
        public ServerType ServerType { get; set; }

//сервис

        private bool _isDemo;

        /// <summary>
        /// является ли подключение тестовым
        /// </summary>
        public bool IsDemo
        {
            get { return _isDemo; }
            set { _isDemo = value; }
        }

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
                if (_tickStorage == null)
                {
                    return;
                }
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
            BitMexServerUi ui = new BitMexServerUi(this, _logMaster);
            ui.ShowDialog();
        }

        /// <summary>
        /// адрес сервера по которому нужно соединяться с сервером
        /// </summary>
        public string ServerAdress;

        /// <summary>
        /// адрес сокета
        /// </summary>
        public string WebSocketAdress;

        /// <summary>
        /// публичный ключ пользователя
        /// </summary>
        public string UserId;

        /// <summary>
        /// секретный ключ пользователя
        /// </summary>
        public string UserKey;

        /// <summary>
        /// загрузить настройки сервера из файла
        /// </summary>
        public void Load()
        {
            if (!File.Exists(@"Engine\" + @"BitMexServer.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"BitMexServer.txt"))
                {
                    UserId = reader.ReadLine();
                    UserKey = reader.ReadLine();
                    _countDaysTickNeadToSave = Convert.ToInt32(reader.ReadLine());
                    _neadToSaveTicks = Convert.ToBoolean(reader.ReadLine());
                    IsDemo = Convert.ToBoolean(reader.ReadLine());

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
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"BitMexServer.txt", false))
                {
                    writer.WriteLine(UserId);
                    writer.WriteLine(UserKey);
                    writer.WriteLine(CountDaysTickNeadToSave);
                    writer.WriteLine(NeadToSaveTicks);
                    writer.WriteLine(IsDemo);

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
        void BitMexClient_Connected()
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
        /// блокиратор многопоточного доступа к серверу СмартКом
        /// </summary>
        private object _bitMexServerLocker = new object();

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
                lock (_bitMexServerLocker)
                {
                    try
                    {
                        if (_clientBitMex == null)
                        {
                            SendLogMessage("Создаём коннектор BitMex", LogMessageType.System);
                            CreateNewServerBitMex();
                            continue;
                        }

                        bool stateIsActiv = _clientBitMex.IsConnected;

                        if (stateIsActiv == false && _serverStatusNead == ServerConnectStatus.Connect)
                        {
                            SendLogMessage("Запущена процедура активации подключения", LogMessageType.System);
                            Dispose();
                            CreateNewServerBitMex();
                            Connect();
                            continue;
                        }

                        if (stateIsActiv && _serverStatusNead == ServerConnectStatus.Disconnect)
                        {
                            SendLogMessage("Запущена процедура отключения подключения", LogMessageType.System);
                            Disconnect();
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

                        if (_startListeningPortfolios == false)
                        {
                            if (_portfolios != null)
                            {
                                SendLogMessage("Подписываемся на обновления портфелей. Берём активные ордера", LogMessageType.System);
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
        private void CreateNewServerBitMex()
        {
            if (_clientBitMex == null)
            {
                _clientBitMex = new BitMexClient();
                _clientBitMex.Connected += BitMexClient_Connected;
                _clientBitMex.Disconnected += ClientBitMexOnDisconnected;
               _clientBitMex.UpdatePortfolio += UpdatePortfolios;
                _clientBitMex.UpdatePosition += UpdatePosition;
                _clientBitMex.UpdateMarketDepth += UpdateMarketDepth;
                _clientBitMex.NewTradesEvent += NewTrades;
                _clientBitMex.MyTradeEvent += NewMyTrade;
                _clientBitMex.MyOrderEvent += BitMex_UpdateOrder;
                _clientBitMex.ErrorEvent += ErrorEvent;
            }
        }

        private void ClientBitMexOnDisconnected()
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
            if (IsDemo)
            {
                _clientBitMex.Domain = "https://testnet.bitmex.com";
                _clientBitMex.ServerAdres = "wss://testnet.bitmex.com/realtime";
            }
            else
            {
                _clientBitMex.Domain = "https://www.bitmex.com";
                _clientBitMex.ServerAdres = "wss://www.bitmex.com/realtime";
            }

            _clientBitMex.Id = UserId;
            _clientBitMex.SecKey = UserKey;

            _lastStartServerTime = DateTime.Now;

            _clientBitMex.Connect();

            Thread.Sleep(1000);
        }

        /// <summary>
        /// приостановить подключение
        /// </summary>
        private void Disconnect()
        {
            _clientBitMex.Disconnect();
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
        /// подписываемся на обновление портфеля и позиций
        /// </summary>
        private void SubscribePortfolio()
        {
            string queryPortf = "{\"op\": \"subscribe\", \"args\": [\"margin\"]}";
            string queryPos = "{\"op\": \"subscribe\", \"args\": [\"position\"]}";

            _clientBitMex.SendQuery(queryPortf);
            Thread.Sleep(500);
            _clientBitMex.SendQuery(queryPos);
        }

        /// <summary>
        /// привести программу к моменту запуска. Очистить все объекты участвующие в подключении к серверу
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private void Dispose()
        {
            try
            {
                if (_clientBitMex != null)
                {
                    _clientBitMex.Disconnect();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            if (_clientBitMex != null)
            {
                _clientBitMex.Connected -= BitMexClient_Connected;
                _clientBitMex.Disconnected -= ClientBitMexOnDisconnected;
                _clientBitMex.UpdatePortfolio -= UpdatePortfolios;
                _clientBitMex.UpdatePosition -= UpdatePosition;
                _clientBitMex.UpdateMarketDepth -= UpdateMarketDepth;
                _clientBitMex.NewTradesEvent -= NewTrades;
                _clientBitMex.MyTradeEvent -= NewMyTrade;
                _clientBitMex.MyOrderEvent -= BitMex_UpdateOrder;
                _clientBitMex.ErrorEvent -= ErrorEvent;
            }

            _clientBitMex = null;

            _candleManager = null;

            _startListeningPortfolios = false;

            _getPortfoliosAndSecurities = false;

            _subscribedSec = new List<string>();
        }

        /// <summary>
        /// bitmex client
        /// </summary>
        private BitMexClient _clientBitMex;

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
        private void UpdatePortfolios(BitMexPortfolio portf)
        {
            try
            {
                if (_portfolios == null)
                {
                    _portfolios = new List<Portfolio>();
                }
                Portfolio osPortf = _portfolios.Find(p => p.Number == portf.data[0].account.ToString());

                if (osPortf == null)
                {
                    osPortf = new Portfolio();
                    osPortf.Number = portf.data[0].account.ToString();
                    osPortf.ValueBegin = portf.data[0].walletBalance;

                    _portfolios.Add(osPortf);
                }

                if (portf.action == "update")
                {
                    osPortf.ValueCurrent = portf.data[0].availableMargin;
                    osPortf.Profit = portf.data[0].unrealisedPnl;
                    //osPortf.ValueBlocked = portf.data[0].marginBalance - portf.data[0].availableMargin;
                    //_portfolios.Add(osPortf);
                    _portfolioToSend.Enqueue(_portfolios);                  
                }
                else
                {
                    
                    osPortf.ValueCurrent = portf.data[0].availableMargin;
                    //osPortf.ValueBlocked = portf.data[0].marginBalance - portf.data[0].availableMargin;
                    osPortf.Profit = portf.data[0].unrealisedPnl;
                   
                    _portfolioToSend.Enqueue(_portfolios);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// обновилась позиция
        /// </summary>
        private void UpdatePosition(BitMexPosition pos)
        {
            if (_portfolios != null)
            {
                for (int i = 0; i < pos.data.Count; i++)
                {
                    Portfolio needPortfolio = _portfolios.Find(p => p.Number == pos.data[i].account.ToString());

                    PositionOnBoard newPos = new PositionOnBoard();

                    newPos.PortfolioName = pos.data[i].account.ToString();
                    newPos.SecurityNameCode = pos.data[i].symbol;
                    newPos.ValueCurrent = pos.data[i].currentQty;

                    needPortfolio.SetNewPosition(newPos);
                }
                _portfolioToSend.Enqueue(_portfolios);
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
                var res = _clientBitMex.CreateQuery("GET", "/instrument/active");
                List<BitMexSecurity> bmSecurities = JsonConvert.DeserializeObject<List<BitMexSecurity>>(res);

                if (_securities == null)
                {
                    _securities = new List<Security>();
                }

                foreach (var oneBmSec in bmSecurities)
                {
                    Security security = new Security();
                    security.Name = oneBmSec.symbol;
                    security.NameFull = oneBmSec.symbol;
                    security.NameClass = oneBmSec.typ;
                    security.Lot = Convert.ToDecimal(oneBmSec.lotSize);
                    if (oneBmSec.tickSize < 1)
                    {
                        security.Decimals = Convert.ToString(oneBmSec.tickSize).Split(',')[1].Length;
                    }
                    else
                    {
                        security.Decimals = 0;
                    }
                    security.Expiration = Convert.ToDateTime(oneBmSec.expiry);
                    security.PriceStep = Convert.ToDecimal(oneBmSec.tickSize);
                    security.PriceStepCost = Convert.ToDecimal(oneBmSec.tickSize);

                    security.State = oneBmSec.state == "Open" ? SecurityStateType.Activ : SecurityStateType.UnKnown;

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

        private List<string> _subscribedSec = new List<string>();
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

                    _candles = null;

                    CandleSeries series = new CandleSeries(timeFrameBuilder, security)
                    {
                        CandlesAll = _candles
                    };

                    if (_subscribedSec.Find(s => s == namePaper) == null)
                    {


                        string queryQuotes = "{\"op\": \"subscribe\", \"args\": [\"orderBookL2:" + security.Name + "\"]}";

                        _clientBitMex.SendQuery(queryQuotes);

                        

                        string queryTrades = "{\"op\": \"subscribe\", \"args\": [\"trade:" + security.Name + "\"]}";

                        _clientBitMex.SendQuery(queryTrades);

                        

                        string queryMyTrades = "{\"op\": \"subscribe\", \"args\": [\"execution:" + security.Name + "\"]}";

                        _clientBitMex.SendQuery(queryMyTrades);

                        

                        string queryorders = "{\"op\": \"subscribe\", \"args\": [\"order:" + security.Name + "\"]}";

                        _clientBitMex.SendQuery(queryorders);

                        _subscribedSec.Add(namePaper);

                    }


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

// свечи


        private object _getCandles = new object();

        /// <summary>
        /// взять историю свечек
        /// </summary>
        /// <param name="security"></param>
        /// <param name="tf"></param>
        /// <param name="shift"></param>
        /// <returns></returns>
        private List<Candle> GetCandlesTf(string security, string tf, int shift)
        {
            try
            {
                lock (_getCandles)
                {
                    List<BitMexCandle> allbmcandles = new List<BitMexCandle>();

                    DateTime endTime = DateTime.MinValue;
                    DateTime startTime = DateTime.MinValue;

                    _candles = null;

                    for (int i = 0; i < 15; i++)
                    {
                        if (i == 0)
                        {
                            endTime = DateTime.UtcNow.Add(TimeSpan.FromMinutes(shift));
                            startTime = endTime.Subtract(TimeSpan.FromMinutes(480));
                        }
                        else
                        {
                            endTime = startTime.Subtract(TimeSpan.FromMinutes(1));
                            startTime = startTime.Subtract(TimeSpan.FromMinutes(480));
                        }

                        string end = endTime.ToString("yyyy-MM-dd HH:mm");
                        string start = startTime.ToString("yyyy-MM-dd HH:mm");

                        var param = new Dictionary<string, string>();
                        param["symbol"] = security;
                        param["count"] = 500.ToString();
                        param["binSize"] = tf;
                        param["reverse"] = true.ToString();
                        param["startTime"] = start;
                        param["endTime"] = end;
                        param["partial"] = true.ToString();

                        try
                        {
                            var res = _clientBitMex.CreateQuery("GET", "/trade/bucketed", param);

                            List<BitMexCandle> bmcandles =
                                JsonConvert.DeserializeAnonymousType(res, new List<BitMexCandle>());

                            allbmcandles.AddRange(bmcandles);
                        }
                        catch
                        {
                            // ignored
                        }
                    }

                    if (_candles == null)
                    {
                        _candles = new List<Candle>();
                    }

                    foreach (var bitMexCandle in allbmcandles)
                    {
                        Candle newCandle = new Candle();

                        newCandle.Open = bitMexCandle.open;
                        newCandle.High = bitMexCandle.high;
                        newCandle.Low = bitMexCandle.low;
                        newCandle.Close = bitMexCandle.close;
                        newCandle.TimeStart = Convert.ToDateTime(bitMexCandle.timestamp).Subtract(TimeSpan.FromMinutes(shift));
                        newCandle.Volume = bitMexCandle.volume;

                        _candles.Add(newCandle);
                    }
                    _candles.Reverse();
                    return _candles;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// свечи скаченные с сервера
        /// </summary>
        private List<Candle> _candles;

        /// <summary>
        /// блокиратор многопоточного доступа к GetBitMexCandleHistory
        /// </summary>
        private readonly object _getCandlesLocker = new object();

        /// <summary>
        /// взять свечи по инструменту
        /// </summary>
        /// <param name="security"> короткое название бумаги</param>
        /// <param name="timeSpan">таймФрейм</param>
        /// <returns>в случае неудачи вернётся null</returns>
        public List<Candle> GetBitMexCandleHistory(string security, TimeSpan timeSpan)
        {
            try
            {
                lock (_getCandlesLocker)
                {
                    if (timeSpan.TotalMinutes > 60 ||
                        timeSpan.TotalMinutes < 1)
                    {
                        return null;
                    }

                    if (timeSpan.Minutes == 1)
                    {
                        return GetCandlesTf(security, "1m", 1);
                    }
                    if (timeSpan.Minutes == 5)
                    {
                        return GetCandlesTf(security, "5m", 5);
                    }
                    if (timeSpan.Minutes == 00)
                    {
                        return GetCandlesTf(security, "1h", 60);
                    }
                    else
                    {
                        return СandlesBuilder(security, timeSpan.Minutes);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// метод возврящает свечи большего таймфрейма, сделанные из меньшего
        /// </summary>
        /// <param name="security"></param>
        /// <param name="tf"></param>
        /// <returns></returns>
        private List<Candle> СandlesBuilder(string security, int tf)
        {
            List<Candle> candles1M = new List<Candle>();
            int a = 0;
            if (tf >= 10)
            {
                candles1M = GetCandlesTf(security, "5m", 5);
                a = tf / 5;
            }
            else
            {
                candles1M = GetCandlesTf(security, "1m", 1);
                a = tf / 1;
            }
            
            int index = candles1M.FindIndex(can => can.TimeStart.Minute % tf == 0);

            List<Candle> candlestf = new List<Candle>();

            int count = 0;

            Candle newCandle = new Candle();

            for (int i = index; i < candles1M.Count; i ++)
            {
                count++;
                if (count == 1)
                {
                    newCandle = new Candle();
                    newCandle.Open = candles1M[i].Open;
                    newCandle.TimeStart = candles1M[i].TimeStart;
                    newCandle.Low = Decimal.MaxValue;
                }

                newCandle.High = candles1M[i].High > newCandle.High
                    ? candles1M[i].High
                    : newCandle.High;

                newCandle.Low = candles1M[i].Low < newCandle.Low
                    ? candles1M[i].Low
                    : newCandle.Low;

                newCandle.Volume += candles1M[i].Volume;

                if (i == candles1M.Count - 1 && count != a)
                {
                    newCandle.Close = candles1M[i].Close;
                    newCandle.State = CandleStates.None;
                    candlestf.Add(newCandle);
                }

                if (count == a)
                {
                    newCandle.Close = candles1M[i].Close;
                    newCandle.State = CandleStates.Finished;
                    candlestf.Add(newCandle);
                    count = 0;
                }               
            }

            return candlestf;          
        }

        /// <summary>
        /// новые свечи
        /// </summary>
        public event Action<CandleSeries> NewCandleIncomeEvent;

// стакан

        /// <summary>
        /// все стаканы
        /// </summary>
        private List<MarketDepth> _depths;

        private object _quoteLock = new object();

        /// <summary>
        /// пришел обновленный стакан
        /// </summary>
        /// <param name="quotes"></param>
        private void UpdateMarketDepth(BitMexQuotes quotes)
        {
            try
            {
                lock (_quoteLock)
                {
                    if (_depths == null)
                    {
                        _depths = new List<MarketDepth>();
                    }

                    if (quotes.action == "partial")
                    {
                        MarketDepth myDepth = new MarketDepth();
                        myDepth.SecurityNameCode = quotes.data[0].symbol;
                        List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                        List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                        for (int i = 0; i < quotes.data.Count; i++)
                        {
                            if (quotes.data[i].side == "Sell")
                            {
                                ascs.Add(new MarketDepthLevel()
                                {
                                    Ask = quotes.data[i].size,
                                    Price = quotes.data[i].price,
                                    Id = quotes.data[i].id
                                });
                            }
                            else
                            {
                                bids.Add(new MarketDepthLevel()
                                {
                                    Bid = quotes.data[i].size,
                                    Price = quotes.data[i].price,
                                    Id = quotes.data[i].id
                                });
                            }
                        }

                        ascs.Reverse();
                        myDepth.Asks = ascs;
                        myDepth.Bids = bids;
                        _depths.Add(myDepth);

                        if (NewMarketDepthEvent != null)
                        {
                            _marketDepthsToSend.Enqueue(myDepth);

                            if (myDepth.Asks.Count != 0 && myDepth.Bids.Count != 0)
                            {
                                _bidAskToSend.Enqueue(new BidAskSender
                                {
                                    Ask = myDepth.Bids[0].Price,
                                    Bid = myDepth.Asks[0].Price,
                                    Security = quotes.data[0].symbol != null
                                        ? GetSecurityForName(quotes.data[0].symbol)
                                        : null
                                });
                            }
                        }
                    }

                    if (quotes.action == "update")
                    {
                        MarketDepth myDepth = _depths.Find(depth => depth.SecurityNameCode == quotes.data[0].symbol);

                        if (myDepth == null)
                            return;

                        for (int i = 0; i < quotes.data.Count; i++)
                        {
                            if (quotes.data[i].side == "Sell")
                            {
                                if (myDepth.Asks.Find(asc => asc.Id == quotes.data[i].id) != null)
                                    myDepth.Asks.Find(asc => asc.Id == quotes.data[i].id).Ask = quotes.data[i].size;
                                else
                                {
                                    long id = quotes.data[i].id;

                                    for (int j = 0; j < myDepth.Asks.Count; j++)
                                    {
                                        if (j == 0 && id > myDepth.Asks[j].Id)
                                        {
                                            myDepth.Asks.Insert(j, new MarketDepthLevel()
                                            {
                                                Ask = quotes.data[i].size,
                                                Price = quotes.data[i].price,
                                                Id = quotes.data[i].id
                                            });
                                        }
                                        else if (j != myDepth.Asks.Count - 1 && id < myDepth.Asks[j].Id && id > myDepth.Asks[j + 1].Id)
                                        {
                                            myDepth.Asks.Insert(j + 1, new MarketDepthLevel()
                                            {
                                                Ask = quotes.data[i].size,
                                                Price = quotes.data[i].price,
                                                Id = quotes.data[i].id
                                            });
                                        }
                                        else if (j == myDepth.Asks.Count - 1 && id < myDepth.Asks[j].Id)
                                        {
                                            myDepth.Asks.Add(new MarketDepthLevel()
                                            {
                                                Ask = quotes.data[i].size,
                                                Price = quotes.data[i].price,
                                                Id = quotes.data[i].id
                                            });
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (myDepth.Bids.Find(bid => bid.Id == quotes.data[i].id) != null)
                                    myDepth.Bids.Find(bid => bid.Id == quotes.data[i].id).Bid = quotes.data[i].size;
                                else
                                {
                                    long id = quotes.data[i].id;

                                    for (int j = 0; j < myDepth.Bids.Count; j++)
                                    {
                                        if (j == 0 && id < myDepth.Bids[j].Id)
                                        {
                                            myDepth.Bids.Insert(j, new MarketDepthLevel()
                                            {
                                                Bid = quotes.data[i].size,
                                                Price = quotes.data[i].price,
                                                Id = quotes.data[i].id
                                            });
                                        }
                                        else if (j != myDepth.Bids.Count - 1 && id > myDepth.Bids[j].Id && id < myDepth.Bids[j + 1].Id)
                                        {
                                            myDepth.Bids.Insert(j + 1, new MarketDepthLevel()
                                            {
                                                Bid = quotes.data[i].size,
                                                Price = quotes.data[i].price,
                                                Id = quotes.data[i].id
                                            });
                                        }
                                        else if (j == myDepth.Bids.Count - 1 && id > myDepth.Bids[j].Id)
                                        {
                                            myDepth.Bids.Add(new MarketDepthLevel()
                                            {
                                                Bid = quotes.data[i].size,
                                                Price = quotes.data[i].price,
                                                Id = quotes.data[i].id
                                            });
                                        }

                                    }
                                }
                            }
                        }

                        DepthsToSend(myDepth);
                    }

                    if (quotes.action == "delete")
                    {
                        MarketDepth myDepth = _depths.Find(depth => depth.SecurityNameCode == quotes.data[0].symbol);

                        if (myDepth == null)
                            return;

                        for (int i = 0; i < quotes.data.Count; i++)
                        {
                            if (quotes.data[i].side == "Sell")
                            {
                                myDepth.Asks.Remove(myDepth.Asks.Find(asc => asc.Id == quotes.data[i].id));
                            }
                            else
                            {
                                myDepth.Bids.Remove(myDepth.Bids.Find(bid => bid.Id == quotes.data[i].id));
                            }
                        }

                        DepthsToSend(myDepth);
                    }

                    if (quotes.action == "insert")
                    {
                        MarketDepth myDepth = _depths.Find(depth => depth.SecurityNameCode == quotes.data[0].symbol);

                        if (myDepth == null)
                            return;

                        for (int i = 0; i < quotes.data.Count; i++)
                        {
                            if (quotes.data[i].side == "Sell")
                            {
                                long id = quotes.data[i].id;

                                for (int j = 0; j < myDepth.Asks.Count; j++)
                                {
                                    if (j == 0 && id > myDepth.Asks[j].Id)
                                    {
                                        myDepth.Asks.Insert(j, new MarketDepthLevel()
                                        {
                                            Ask = quotes.data[i].size,
                                            Price = quotes.data[i].price,
                                            Id = quotes.data[i].id
                                        });
                                    }
                                    else if (j != myDepth.Asks.Count - 1 && id < myDepth.Asks[j].Id && id > myDepth.Asks[j + 1].Id)
                                    {
                                        myDepth.Asks.Insert(j + 1, new MarketDepthLevel()
                                        {
                                            Ask = quotes.data[i].size,
                                            Price = quotes.data[i].price,
                                            Id = quotes.data[i].id
                                        });
                                    }
                                    else if (j == myDepth.Asks.Count - 1 && id < myDepth.Asks[j].Id)
                                    {
                                        myDepth.Asks.Add(new MarketDepthLevel()
                                        {
                                            Ask = quotes.data[i].size,
                                            Price = quotes.data[i].price,
                                            Id = quotes.data[i].id
                                        });
                                    }
                                }
                            }
                            else
                            {
                                long id = quotes.data[i].id;

                                for (int j = 0; j < myDepth.Bids.Count; j++)
                                {
                                    if (j == 0 && id < myDepth.Bids[j].Id)
                                    {
                                        myDepth.Bids.Insert(j, new MarketDepthLevel()
                                        {
                                            Bid = quotes.data[i].size,
                                            Price = quotes.data[i].price,
                                            Id = quotes.data[i].id
                                        });
                                    }
                                    else if (j != myDepth.Bids.Count - 1 && id > myDepth.Bids[j].Id && id < myDepth.Bids[j + 1].Id)
                                    {
                                        myDepth.Bids.Insert(j + 1, new MarketDepthLevel()
                                        {
                                            Bid = quotes.data[i].size,
                                            Price = quotes.data[i].price,
                                            Id = quotes.data[i].id
                                        });
                                    }
                                    else if (j == myDepth.Bids.Count - 1 && id > myDepth.Bids[j].Id)
                                    {
                                        myDepth.Bids.Add(new MarketDepthLevel()
                                        {
                                            Bid = quotes.data[i].size,
                                            Price = quotes.data[i].price,
                                            Id = quotes.data[i].id
                                        });
                                    }

                                }
                            }
                        }

                        DepthsToSend(myDepth);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        void DepthsToSend(MarketDepth myDepth)
        {
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


        /// <summary>
        /// изменился лучший бид / аск по инструменту
        /// </summary>
        public event Action<decimal, decimal, Security> NewBidAscIncomeEvent;

        public event Action<MarketDepth> NewMarketDepthEvent;

// тики

        
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
                
                Dictionary<string, string> param = new Dictionary<string, string>();

                param["symbol"] = security.Name;
                param["count"] = 500.ToString();
                param["reverse"] = true.ToString();

                var res = _clientBitMex.CreateQuery("GET", "/trade", param, true);

                List<TradeBitMex> tradeHistory = JsonConvert.DeserializeAnonymousType(res, new List<TradeBitMex>());

                tradeHistory.Reverse();

                foreach (var oneTrade in tradeHistory)
                {
                    Trade trade = new Trade();
                    trade.SecurityNameCode = oneTrade.symbol;
                    trade.Id = oneTrade.trdMatchID;
                    trade.Time = Convert.ToDateTime(oneTrade.timestamp);
                    trade.Price = oneTrade.price;
                    trade.Volume = oneTrade.size;
                    trade.Side = oneTrade.side == "Sell" ? Side.Sell : Side.Buy;

                    trades.Add(trade);
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
        private void NewTrades(BitMexTrades trades)
        {
            try
            {
                lock (_newTradesLoker)
                {
                    for (int j = 0; j < trades.data.Count; j++)
                    {
                        Trade trade = new Trade();
                        trade.SecurityNameCode = trades.data[j].symbol;
                        trade.Price = trades.data[j].price;
                        trade.Id = trades.data[j].trdMatchID;
                        trade.Time = Convert.ToDateTime(trades.data[j].timestamp);
                        trade.Volume = trades.data[j].size;
                        trade.Side = trades.data[j].side == "Buy" ? Side.Buy : Side.Sell;

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

        private object _myTradeLocker = new object();

        /// <summary>
        /// входящие из системы мои сделки
        /// </summary>
        private void NewMyTrade(BitMexMyOrders order)
        {
            try
            {
                lock (_myTradeLocker)
                {
                    for (int i = 0; i < order.data.Count; i++)
                    {
                        MyTrade trade = new MyTrade();
                        trade.NumberTrade = order.data[i].execID;
                        trade.NumberOrderParent = order.data[i].orderID;
                        trade.SecurityNameCode = order.data[i].symbol;
                        trade.Price = Convert.ToDecimal(order.data[i].avgPx);
                        trade.Time = Convert.ToDateTime(order.data[i].transactTime);
                        trade.Side = order.data[i].side == "Buy" ? Side.Buy : Side.Sell;

                        if (order.data[i].lastQty != null)
                        {
                            trade.Volume = (int) order.data[i].lastQty;
                        }

                        if (_myTrades == null)
                        {
                            _myTrades = new List<MyTrade>();
                        }
                        _myTrades.Add(trade);

                        _myTradesToSend.Enqueue(trade);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

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
                            Dictionary<string,string> param = new Dictionary<string, string>();

                            param["symbol"] = order.SecurityNameCode;
                            param["price"] = order.Price.ToString().Replace(",",".");
                            param["side"] = order.Side == Side.Buy ? "Buy" : "Sell";
                            param["orderQty"] = order.Volume.ToString();
                            param["clOrdID"] = order.NumberUser.ToString();
                            param["ordType"] = order.TypeOrder == OrderPriceType.Limit ? "Limit" : "Market";

                            var res = _clientBitMex.CreateQuery("POST", "/order", param, true);
                        }
                    }
                    else if (_ordersToCansel != null && _ordersToCansel.Count != 0)
                    {
                        Order order;
                        if (_ordersToCansel.TryDequeue(out order))
                        {
                            Dictionary<string, string> param = new Dictionary<string, string>();
                            //param["clOrdID"] = order.NumberUser.ToString();
                            param["orderID"] = order.NumberMarket;

                            var res = _clientBitMex.CreateQuery("DELETE", "/order", param, true);
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
        /// ордера, ожидающие регистрации
        /// </summary>
        private List<Order> _newOrders = new List<Order>();

        /// <summary>
        /// блокиратор доступа к ордерам
        /// </summary>
        private object _orderLocker = new object();

        /// <summary>
        /// входящий из системы ордер
        /// </summary>
        private void BitMex_UpdateOrder(BitMexOrder myOrder)
        {
            lock (_orderLocker)
            {
                try
                {
                    if (_lastStartServerTime.AddSeconds(15) > DateTime.Now)
                    {
                        return;
                    }
                    for (int i = 0; i < myOrder.data.Count; i++)
                    {
                        if (myOrder.action == "insert")
                        {
                            Order order = new Order();
                            order.NumberUser = Convert.ToInt32(myOrder.data[i].clOrdID);
                            order.NumberMarket = myOrder.data[i].orderID;
                            order.SecurityNameCode = myOrder.data[i].symbol;
                            order.Price = myOrder.data[i].price;
                            order.State = OrderStateType.Pending;

                            if (myOrder.data[i].orderQty != null)
                            {
                                order.Volume = (int)myOrder.data[i].orderQty;
                            }

                            order.Comment = myOrder.data[i].text;
                            order.TimeCallBack = Convert.ToDateTime(myOrder.data[0].transactTime);
                            order.PortfolioNumber = myOrder.data[i].account.ToString();
                            order.TypeOrder = myOrder.data[i].ordType == "Limit"
                                ? OrderPriceType.Limit
                                : OrderPriceType.Market;

                            if (myOrder.data[i].side == "Sell")
                            {
                                order.Side = Side.Sell;
                            }
                            else if (myOrder.data[i].side == "Buy")
                            {
                                order.Side = Side.Buy;
                            }
                            
                            _newOrders.Add(order);

                        }

                        else if (myOrder.action == "update" )
                        {
                            var needOrder = _newOrders.Find(order => order.NumberMarket == myOrder.data[i].orderID);

                            if (needOrder != null)
                            {
                                if (myOrder.data[i].workingIndicator)
                                {
                                    needOrder.State = OrderStateType.Activ;
                                }

                                if (myOrder.data[i].ordStatus == "Canceled")
                                {
                                    needOrder.State = OrderStateType.Cancel;
                                }

                                if (myOrder.data[i].ordStatus == "Rejected")
                                {
                                    needOrder.State = OrderStateType.Fail;
                                    needOrder.VolumeExecute = 0;
                                }

                                if (myOrder.data[i].ordStatus == "PartiallyFilled")
                                {
                                    needOrder.State = OrderStateType.Patrial;
                                    needOrder.VolumeExecute = myOrder.data[i].cumQty;
                                }

                                if (myOrder.data[i].ordStatus == "Filled")
                                {
                                    needOrder.State = OrderStateType.Done;
                                    needOrder.VolumeExecute = myOrder.data[i].cumQty;
                                }
                                if (_myTrades != null &&
                                    _myTrades.Count != 0)
                                {
                                    List<MyTrade> myTrade =
                                        _myTrades.FindAll(trade => trade.NumberOrderParent == needOrder.NumberMarket);

                                    for (int tradeNum = 0; tradeNum < myTrade.Count; tradeNum++)
                                    {
                                        _myTradesToSend.Enqueue(myTrade[tradeNum]);
                                    }
                                }
                                _ordersToSend.Enqueue(needOrder);
                            }
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
        /// ошибки клиента
        /// </summary>
        /// <param name="error"></param>
        private void ErrorEvent(string error)
        {
            SendLogMessage(error, LogMessageType.Error);
        }

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
    /// один тик BitMex
    /// </summary>
    public class TradeBitMex
    {
        public string timestamp { get; set; }
        public string symbol { get; set; }
        public string side { get; set; }
        public int size { get; set; }
        public decimal price { get; set; }
        public string tickDirection { get; set; }
        public string trdMatchID { get; set; }
        public object grossValue { get; set; }
        public double homeNotional { get; set; }
        public int foreignNotional { get; set; }
    }

    /// <summary>
    /// свеча BitMex
    /// </summary>
    public class BitMexCandle
    {
        public string timestamp { get; set; }
        public string symbol { get; set; }
        public decimal open { get; set; }
        public decimal high { get; set; }
        public decimal low { get; set; }
        public decimal close { get; set; }
        public int volume { get; set; }

        //public int trades { get; set; }
        //public double? vwap { get; set; }
        //public int? lastSize { get; set; }
        //public object turnover { get; set; }
        //public double homeNotional { get; set; }
        //public int foreignNotional { get; set; }
    }
}
