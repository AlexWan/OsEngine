using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using QuikSharp;
using QuikSharp.DataStructures;
using QuikSharp.DataStructures.Transaction;
using Candle = OsEngine.Entity.Candle;
using Order = OsEngine.Entity.Order;
using State = QuikSharp.DataStructures.State;
using Trade = OsEngine.Entity.Trade;

namespace OsEngine.Market.Servers.QuikLua
{
    public class QuikLuaServer:IServer
    {
        /// <summary>
        /// сервер квиклуа
        /// </summary>
        public  QuikSharp.Quik QuikLua;

        public QuikLuaServer(bool neadToLoadTicks)
        {
            ServerStatus = ServerConnectStatus.Disconnect;
            ServerType = ServerType.QuikLua;
            _neadToSaveTicks = true;

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

            _logMaster = new Log("QuikLuaServer", StartProgram.IsOsTrader);
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

//сервис

        /// <summary>
        /// взять тип сервера. 
        /// </summary>
        /// <returns></returns>
        public ServerType ServerType { get; set; }

        /// <summary>
        /// адрес сервера по которому нужно соединяться с сервером
        /// </summary>
        public string ServerAdress;

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
            }
        }

        /// <summary>
        /// показать настройки
        /// </summary>
        public void ShowDialog()
        {
            if (_ui == null)
            {
                _ui = new QuikLuaServerUi(this, _logMaster);
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
        private QuikLuaServerUi _ui;

        /// <summary>
        /// загрузить настройки
        /// </summary>
        public void Load()
        {
            if (!File.Exists(@"Engine\" + @"QuikLuaServer.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"QuikLuaServer.txt"))
                {
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
        /// сохранить настройки в файл
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"QuikLuaServer.txt", false))
                {
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
        /// было ли первое подключение
        /// </summary>
        private bool firstConnected = false;

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
        /// нужный статус сервера. Нужен потоку который следит за соединением
        /// В зависимости от этого поля управляет соединением
        /// </summary>
        private ServerConnectStatus _serverStatusNead;

        /// <summary>
        /// пришло оповещение, что соединение установлено
        /// </summary>
        void QuikLua_Connected(int port)
        {
            SendLogMessage("Соединение установлено", LogMessageType.System);
            ServerStatus = ServerConnectStatus.Connect;
        }

        /// <summary>
        /// пришло оповещение, что соединение установлено
        /// </summary>
        void QuikLua_Connected()
        {
            SendLogMessage("Соединение установлено", LogMessageType.System);
            ServerStatus = ServerConnectStatus.Connect;
        }

        /// <summary>
        /// пришло оповещение, что соединение разорвано
        /// </summary>
        void QuikLua_Disconnected()
        {
            SendLogMessage("Соединение разорвано", LogMessageType.System);
            ServerStatus = ServerConnectStatus.Disconnect;  
        }

 // статус сервера

        private ServerConnectStatus _serverConnectStatus;

        /// <summary>
        /// взять статус сервера
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
        /// блокиратор многопоточного доступа к серверу 
        /// </summary>
        private object _serverLocker = new object();

        /// <summary>
        /// основной поток, следящий за подключением, загрузкой портфелей и бумаг
        /// </summary>
        private Thread _threadPrime;
        
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
                        if (QuikLua == null && _serverStatusNead == ServerConnectStatus.Connect)
                        {
                            SendLogMessage("Создаём подключение QuikLua", LogMessageType.System);
                            Connect();
                            continue;
                        }

                        if (QuikLua == null)
                        {
                            continue;
                        }

                        if (firstConnected && _serverStatusNead == ServerConnectStatus.Connect)
                        {
                            QuikLua.Service.QuikService.Start();
                            ServerStatus = ServerConnectStatus.Connect;
                            firstConnected = false;
                            Thread.Sleep(200);
                        }

                        if (ServerStatus != ServerConnectStatus.Connect)
                        {
                            continue;
                        }
                        bool quikStateIsActiv = QuikLua.Service.IsConnected().Result;
                        
                        if (quikStateIsActiv && _serverStatusNead == ServerConnectStatus.Disconnect)
                        {
                            SendLogMessage("Запущена процедура отключения от Квика", LogMessageType.System);
                            Disconnect();
                            continue;
                        }

                        if (_candleManager == null)
                        {
                            SendLogMessage("Создаём менеджер свечей", LogMessageType.System);
                            StartCandleManager();
                            continue;
                        }

                        if (_getPortfolios == false)
                        {
                            SendLogMessage("Скачиваем портфели", LogMessageType.System);
                            GetPortfolio();
                            _getPortfolios = true;
                            continue;
                        }

                        if (Portfolios == null)
                        {
                            _getPortfolios = false;
                            continue;
                        }

                        if (_startListeningPortfolios == false)
                        {
                            if (_portfolios != null)
                            {
                                SendLogMessage("Подписываемся на обновления портфелей. Берём активные ордера", LogMessageType.System);
                                StartListeningPortfolios();
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
        private bool _getPortfolios;

        /// <summary>
        /// создать новое подключение
        /// </summary>
        private void Connect()
        {
            if (QuikLua == null)
            {
                QuikLua = new QuikSharp.Quik(QuikSharp.Quik.DefaultPort, new InMemoryStorage());

                
                QuikLua.Events.OnConnected += QuikLua_Connected;
                QuikLua.Events.OnDisconnected += QuikLua_Disconnected;
                QuikLua.Events.OnConnectedToQuik += QuikLua_Connected;
                QuikLua.Events.OnDisconnectedFromQuik += QuikLua_Disconnected;
                QuikLua.Events.OnTrade += QuikLua_AddTrade;
                QuikLua.Events.OnOrder += QuikLua_UpdateOrder;
                QuikLua.Events.OnQuote += QuikLua_UpdateQuote;

                QuikLua.Events.OnFuturesClientHolding += EventsOnFuturesClientHolding;
                QuikLua.Events.OnFuturesLimitChange += EventsOnOnFuturesLimitChange;
                
                _lastStartServerTime = DateTime.Now;

                Thread getSec = new Thread(GetSecurities);
                getSec.CurrentCulture = new CultureInfo("ru-RU");
                getSec.IsBackground = true;
                getSec.Start();
                ServerStatus = ServerConnectStatus.Connect;
            }
        }

        /// <summary>
        /// приостановить подключение
        /// </summary>
        private void Disconnect()
        {
            QuikLua.Service.QuikService.Stop();
            firstConnected = true;
            ServerStatus = ServerConnectStatus.Disconnect;
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
        /// включает загрузку портфелей
        /// </summary>
        private void GetPortfolio()
        {
            Thread getPortfolios = new Thread(GetPortfolios);
            getPortfolios.CurrentCulture = new CultureInfo("ru-RU");
            getPortfolios.IsBackground = true;
            getPortfolios.Start();
            //GetPortfolios();
            Thread.Sleep(3000);
        }

        /// <summary>
        /// включает прослушивание портфелей
        /// </summary>
        private void StartListeningPortfolios()
        {
            Thread updateSpotPos = new Thread(UpdateSpotPosition);
            updateSpotPos.CurrentCulture = new CultureInfo("ru-RU");
            updateSpotPos.IsBackground = true;
            updateSpotPos.Start();
        }

        /// <summary>
        /// привести программу к моменту запуска. Очистить все объекты участвующие в подключении к серверу
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private void Dispose()
        {
            try
            {
                if (QuikLua != null && QuikLua.Service.IsConnected().Result)
                {
                    QuikLua.Service.QuikService.Stop();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(),LogMessageType.Error);
                
            }

            if (QuikLua != null)
            {
                QuikLua.Events.OnConnected -= QuikLua_Connected;
                QuikLua.Events.OnDisconnected -= QuikLua_Disconnected;
                QuikLua.Events.OnConnectedToQuik -= QuikLua_Connected;
                QuikLua.Events.OnDisconnectedFromQuik -= QuikLua_Disconnected;
                QuikLua.Events.OnTrade -= QuikLua_AddTrade;
                QuikLua.Events.OnOrder -= QuikLua_UpdateOrder;
                QuikLua.Events.OnQuote -= QuikLua_UpdateQuote;

                QuikLua.Events.OnFuturesClientHolding -= EventsOnFuturesClientHolding;
                QuikLua.Events.OnFuturesLimitChange -= EventsOnOnFuturesLimitChange;
            }

            QuikLua = null;

        }

// работа потока рассылки

        #region очереди данных

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

#endregion

        /// <summary>
        /// место в котором работает поток рассылки данных на верх
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
                    _serverTime = value;
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
        /// метод который  запрашивает из квика портфели 
        /// </summary>
        private void GetPortfolios()
        {
            try
            {
                if (_portfolios == null)
                {
                    _portfolios = new List<Portfolio>();
                }
                List<TradesAccounts> accaunts = QuikLua.Class.GetTradeAccounts().Result;
                var clientCode = QuikLua.Class.GetClientCode().Result;
                while (true)
                {
                    Thread.Sleep(5000);

                    for (int i = 0; i < accaunts.Count; i++)
                    {
                        if (String.IsNullOrWhiteSpace(accaunts[i].ClassCodes))
                        {
                            continue;
                        }

                        Portfolio myPortfolio = _portfolios.Find(p => p.Number == accaunts[i].TrdaccId);

                        if (myPortfolio == null)
                        {
                            myPortfolio = new Portfolio();
                        }

                        myPortfolio.Number = accaunts[i].TrdaccId;

                        if (myPortfolio.Number.Remove(6) != "SPBFUT")
                        {
                            var qPortfolio = QuikLua.Trading.GetPortfolioInfo(accaunts[i].Firmid, clientCode).Result;

                            if (qPortfolio != null && qPortfolio.InAssets != null)
                            {
                                var begin = qPortfolio.InAssets.Replace('.', separator);
                                myPortfolio.ValueBegin = Convert.ToDecimal(begin.Remove(begin.Length - 4));
                            }

                            if (qPortfolio != null && qPortfolio.Assets != null)
                            {
                                var current = qPortfolio.Assets.Replace('.', separator);
                                myPortfolio.ValueCurrent = Convert.ToDecimal(current.Remove(current.Length - 4));
                            }

                            if (qPortfolio != null && qPortfolio.TotalLockedMoney != null)
                            {
                                var blocked = qPortfolio.TotalLockedMoney.Replace('.', separator);
                                myPortfolio.ValueBlocked = Convert.ToDecimal(blocked.Remove(blocked.Length - 4));
                            }

                            if (qPortfolio != null && qPortfolio.ProfitLoss != null)
                            {
                                var profit = qPortfolio.ProfitLoss.Replace('.', separator);
                                myPortfolio.Profit = Convert.ToDecimal(profit.Remove(profit.Length - 4));
                            }
                        }
                        else
                        {
                            // TODO сделать получение информации по фьючерсным лимитам для счетов без ЕБС
                        }

                        _portfolios.Add(myPortfolio);
                    }
                    _portfolioToSend.Enqueue(_portfolios);
                    
                }
            }

            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

        }

        /// <summary>
        /// метод, который раз в 5 секунд запрашивает информацию о позициях на фондовом рынке
        /// </summary>
        private void UpdateSpotPosition()
        {
            while (true)
            {
                Thread.Sleep(5000);

                
                List<DepoLimitEx> spotPos = QuikLua.Trading.GetDepoLimits().Result;
                Portfolio needPortf;
                foreach (var pos in spotPos)
                {
                    if (pos.LimitKind == LimitKind.T0)
                    {
                        needPortf = _portfolios.Find(p => p.Number == pos.TrdAccId);

                        PositionOnBoard position = new PositionOnBoard();

                        if (needPortf != null)
                        {
                            position.PortfolioName = pos.TrdAccId;
                            position.ValueBegin = pos.OpenBalance;
                            position.ValueCurrent = pos.CurrentBalance;
                            position.ValueBlocked = pos.LockedSell;
                            position.SecurityNameCode = pos.SecCode;

                            needPortf.SetNewPosition(position);
                            
                        }
                    }
                }
                
                _portfolioToSend.Enqueue(_portfolios);
            }
        }

        /// <summary>
        /// блокиратор доступа к фьючерсным позициям
        /// </summary>
        private object changeFutPosLocker = new object();

        /// <summary>
        /// вызывается при изменений позиций по фьючерсам
        /// </summary>
        /// <param name="futPos"></param>
        private void EventsOnFuturesClientHolding(FuturesClientHolding futPos)
        {
            lock (changeFutPosLocker)
            {
                if (_portfolios != null)
                {
                    Portfolio needPortfolio = _portfolios.Find(p => p.Number == futPos.trdAccId);

                    PositionOnBoard newPos = new PositionOnBoard();

                    newPos.PortfolioName = futPos.trdAccId;
                    newPos.SecurityNameCode = futPos.secCode;
                    newPos.ValueBegin = Convert.ToDecimal(futPos.startNet);
                    newPos.ValueCurrent = Convert.ToDecimal(futPos.totalNet);
                    newPos.ValueBlocked = 0;

                    needPortfolio.SetNewPosition(newPos);

                    _portfolioToSend.Enqueue(_portfolios);
                }

            }
        }

        /// <summary>
        /// блокиратор доступа к фьючерсным портфелям
        /// </summary>
        private object changeFutPortf = new object();

        /// <summary>
        /// обработчик события изменения лимитов по фьючерсам
        /// </summary>
        /// <param name="futLimit"></param>
        private void EventsOnOnFuturesLimitChange(FuturesLimits futLimit)
        {
            lock (changeFutPortf)
            {
                Portfolio needPortf = _portfolios.Find(p => p.Number == futLimit.TrdAccId);
                if (needPortf != null)
                {
                    needPortf.ValueBegin = Convert.ToDecimal(futLimit.CbpPrevLimit);
                    needPortf.ValueCurrent = Convert.ToDecimal(futLimit.CbpLimit);
                    needPortf.ValueBlocked = Convert.ToDecimal(futLimit.CbpLUsedForOrders + futLimit.CbpLUsedForPositions);
                    needPortf.Profit = Convert.ToDecimal(futLimit.VarMargin);

                    _portfolioToSend.Enqueue(_portfolios);
                }
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
        /// взять инструмент по короткому имени инструмента
        /// </summary>
        public Security GetSecurityForName(string name)
        {
            if (_securities == null)
            {
                return null;
            }
            return _securities.Find(securiti => securiti.Name == name);
        }
        
        Char separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];

        /// <summary>
        /// метод, который загружает из квике все инструменты, переделывает их в формат осы и ставит в очередь на отправку 
        /// </summary>
        public void GetSecurities()
        {
            try
            {
                #region

                SendLogMessage("Начинаю скачивать инструменты", LogMessageType.System);
                string[] classesList;

                lock (_serverLocker)
                {
                    classesList = QuikLua.Class.GetClassesList().Result;
                }

                List<SecurityInfo> allSec = new List<SecurityInfo>();

                for (int i = 0; i < classesList.Length; i++)
                {
                    if (classesList[i].EndsWith("INFO"))
                    {
                        continue;
                    }
                    string[] secCodes = QuikLua.Class.GetClassSecurities(classesList[i]).Result;
                    for (int j = 0; j < secCodes.Length; j++)
                    {
                        allSec.Add(QuikLua.Class.GetSecurityInfo(classesList[i], secCodes[j]).Result);
                    }
                }
                
                List < Security > securities = new List<Security>();
                foreach (var oneSec in allSec)
                {
                    try
                    {
                        if (oneSec != null)
                        {
                            Security newSec = new Security();
                            string secCode = oneSec.SecCode;
                            string classCode = oneSec.ClassCode;
                            if (oneSec.ClassCode == "SPBFUT")
                            {
                                newSec.SecurityType = SecurityType.Futures;
                                var exp = oneSec.MatDate;
                                newSec.Expiration = new DateTime(Convert.ToInt32(exp.Substring(0, 4))
                                    , Convert.ToInt32(exp.Substring(4, 2))
                                    , Convert.ToInt32(exp.Substring(6, 2)));

                                newSec.Go = Convert.ToDecimal(QuikLua.Trading
                                    .GetParamEx(classCode, secCode, "SELLDEPO")
                                    .Result.ParamValue.Replace('.', separator));
                            }
                            else if (oneSec.ClassCode == "SPBOPT")
                            {
                                newSec.SecurityType = SecurityType.Option;

                                newSec.OptionType = QuikLua.Trading.GetParamEx(classCode, secCode, "OPTIONTYPE")
                                                        .Result.ParamImage == "Put"
                                    ? OptionType.Put
                                    : OptionType.Call;

                                var exp = oneSec.MatDate;
                                newSec.Expiration = new DateTime(Convert.ToInt32(exp.Substring(0, 4))
                                    , Convert.ToInt32(exp.Substring(4, 2))
                                    , Convert.ToInt32(exp.Substring(6, 2)));

                                newSec.Go = Convert.ToDecimal(QuikLua.Trading
                                    .GetParamEx(classCode, secCode, "SELLDEPO")
                                    .Result.ParamValue.Replace('.', separator));

                                newSec.Strike = Convert.ToDecimal(QuikLua.Trading
                                    .GetParamEx(classCode, secCode, "STRIKE")
                                    .Result.ParamValue.Replace('.', separator));
                            }
                            else 
                            {
                                newSec.SecurityType = SecurityType.Stock;
                            }
                            
                            newSec.Name = oneSec.SecCode; // тест
                            newSec.NameFull = oneSec.Name;
                            newSec.Decimals = Convert.ToInt32(oneSec.Scale);
                            newSec.Lot = Convert.ToDecimal(oneSec.LotSize);
                            newSec.NameClass = oneSec.ClassCode;
                            newSec.PriceLimitHigh = Convert.ToDecimal(QuikLua.Trading
                                .GetParamEx(classCode, secCode, "PRICEMAX")
                                .Result.ParamValue.Replace('.', separator));
                            newSec.PriceLimitLow = Convert.ToDecimal(QuikLua.Trading
                                .GetParamEx(classCode, secCode, "PRICEMIN")
                                .Result.ParamValue.Replace('.', separator));
                            newSec.PriceStep = Convert.ToDecimal(QuikLua.Trading
                                .GetParamEx(classCode, secCode, "SEC_PRICE_STEP")
                                .Result.ParamValue.Replace('.', separator));
                            newSec.PriceStepCost = Convert.ToDecimal(QuikLua.Trading
                                .GetParamEx(classCode, secCode, "STEPPRICET")
                                .Result.ParamValue.Replace('.', separator));

                            securities.Add(newSec);
                        }
                    }

                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                    }
                }
                _securities = new List<Security>();
                _securities = securities;
                _securitiesToSend.Enqueue(_securities);

                SendLogMessage("Загрузка инструментов окончена", LogMessageType.System);

                #endregion
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

                    //_candles = null;
                    
                    CandleSeries series = new CandleSeries(timeFrameBuilder, security,StartProgram.IsOsTrader)
                    {
                        CandlesAll = null
                    };

                    lock (_serverLocker)
                    {
                        QuikLua.OrderBook.Subscribe(security.NameClass, security.Name);
                        subscribedBook.Add(security.Name);
                        QuikLua.Events.OnAllTrade +=  QuilLua_AddTick;
                    }
                    Thread.Sleep(100);

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
        /// вызывается в момент изменения серий свечек
        /// </summary>
        public event Action<CandleSeries> NewCandleIncomeEvent;

        /// <summary>
        /// необходимо перезаказать данные у сервера
        /// </summary>
        public event Action NeadToReconnectEvent;

// свечи, взять историю свечек

        /// <summary>
        /// блокиратор многопоточного доступа к GetQuikLuaCandleHistory
        /// </summary>
        private object _getCandlesLocker = new object();

        /// <summary>
        /// взять свечи по инструменту
        /// </summary>
        /// <param name="security"> короткое название бумаги</param>
        /// <param name="timeSpan">таймФрейм</param>
        /// <returns>в случае неудачи вернётся null</returns>
        public List<Candle> GetQuikLuaCandleHistory(string security, TimeSpan timeSpan)
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

                CandleInterval tf = CandleInterval.M5;

                if (Convert.ToInt32(timeSpan.TotalMinutes) == 1)
                {
                    tf = CandleInterval.M1;
                }
                else if (Convert.ToInt32(timeSpan.TotalMinutes) == 2)
                {
                    tf = CandleInterval.M2;
                }
                else if (Convert.ToInt32(timeSpan.TotalMinutes) == 5)
                {
                    tf = CandleInterval.M5;
                }
                else if (Convert.ToInt32(timeSpan.TotalMinutes) == 10)
                {
                    tf = CandleInterval.M10;
                }
                else if (Convert.ToInt32(timeSpan.TotalMinutes) == 15)
                {
                    tf = CandleInterval.M15;
                }
                else if (Convert.ToInt32(timeSpan.TotalMinutes) == 30)
                {
                    tf = CandleInterval.M30;
                }
                else if (Convert.ToInt32(timeSpan.TotalMinutes) == 60)
                {
                    tf = CandleInterval.H1;
                }
                else if (Convert.ToInt32(timeSpan.TotalMinutes) == 120)
                {
                    tf = CandleInterval.H2;
                }
                

                #region MyRegion

                _candles = null;

                var needSec = _securities.Find(sec => sec.Name == security);

                if (needSec != null)
                {
                    _candles = new List<Candle>();
                    string classCode = needSec.NameClass;

                    var allCandlesForSec = QuikLua.Candles.GetAllCandles(classCode, needSec.Name, tf).Result;

                    for (int i = 0; i < allCandlesForSec.Count; i++)
                    {
                        if (allCandlesForSec[i] != null)
                        {
                            Candle newCandle = new Candle();

                            newCandle.Close = allCandlesForSec[i].Close;
                            newCandle.High = allCandlesForSec[i].High;
                            newCandle.Low = allCandlesForSec[i].Low;
                            newCandle.Open = allCandlesForSec[i].Open;
                            newCandle.Volume = allCandlesForSec[i].Volume;

                            if (i == allCandlesForSec.Count - 1)
                            {
                                newCandle.State = CandleState.None;
                            }
                            else
                            {
                                newCandle.State = CandleState.Finished;
                            }

                            newCandle.TimeStart = new DateTime(allCandlesForSec[i].Datetime.year,
                                allCandlesForSec[i].Datetime.month,
                                allCandlesForSec[i].Datetime.day,
                                allCandlesForSec[i].Datetime.hour,
                                allCandlesForSec[i].Datetime.min,
                                allCandlesForSec[i].Datetime.sec);

                            _candles.Add(newCandle);
                        }
                    }
                }

                #endregion

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
        /// свечи скаченные из метода GetQuikLuaCandleHistory
        /// </summary>
        private List<Candle> _candles;

// стакан

        /// <summary>
        /// стаканы по инструментам
        /// </summary>
        private List<MarketDepth> _marketDepths = new List<MarketDepth>();

        /// <summary>
        /// взять стакан по инструменту
        /// </summary>
        public MarketDepth GetMarketDepth(string securityName)
        {
            return _marketDepths.Find(m => m.SecurityNameCode == securityName);
        }

        // сохранение расширенных данных по трейду

        /// <summary>
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

        private List<string> subscribedBook= new List<string>();

        private object quoteLock = new object();

        /// <summary>
        /// в квике обновился стакан
        /// </summary>
        void QuikLua_UpdateQuote(OrderBook orderBook)
        {
            lock (quoteLock)
            {
                if (subscribedBook.Find(name => name == orderBook.sec_code)== null)
                {
                    return;
                }

                if (orderBook.bid == null || orderBook.offer == null)
                {
                    return;
                }

                MarketDepth myDepth = new MarketDepth();

                myDepth.SecurityNameCode = orderBook.sec_code;
                myDepth.Time = DateTime.Now;

                myDepth.Bids = new List<MarketDepthLevel>();
                for (int i = 0; i < orderBook.bid.Length; i++)
                {
                    myDepth.Bids.Add(new MarketDepthLevel()
                    {
                        Bid = Convert.ToDecimal(orderBook.bid[i].quantity),
                        Price = Convert.ToDecimal(orderBook.bid[i].price),
                        Ask = 0
                    });
                }
                myDepth.Bids.Reverse();

                myDepth.Asks = new List<MarketDepthLevel>();
                for (int i = 0; i < orderBook.offer.Length; i++)
                {
                    
                    myDepth.Asks.Add(new MarketDepthLevel()
                    {
                        Ask = Convert.ToDecimal(orderBook.offer[i].quantity),
                        Price = Convert.ToDecimal(orderBook.offer[i].price),
                        Bid = 0

                    });
                }

                if (NewMarketDepthEvent != null)
                {
                    _marketDepthsToSend.Enqueue(myDepth);
                    _bidAskToSend.Enqueue(new BidAskSender
                    {
                        Ask = myDepth.Bids[0].Price,
                        Bid = myDepth.Asks[0].Price,
                        Security = GetSecurityForName(orderBook.sec_code)
                    });
                }

                // грузим стаканы в хранилище
                for (int i = 0; i < _marketDepths.Count; i++)
                {
                    if (_marketDepths[i].SecurityNameCode == myDepth.SecurityNameCode)
                    {
                        _marketDepths[i] = myDepth;
                        return;
                    }
                }
                _marketDepths.Add(myDepth);
            }
        }

        /// <summary>
        /// изменился лучший бид / аск по инструменту
        /// </summary>
        public event Action<decimal, decimal, Security> NewBidAscIncomeEvent;

        /// <summary>
        /// вызывается когда изменяется стакан
        /// </summary>
        public event Action<MarketDepth> NewMarketDepthEvent;


// тики

        private ServerTickStorage _tickStorage;

        /// <summary>
        /// все тики
        /// </summary>
        private List<Trade>[] _allTrades;

        /// <summary>
        /// хранилище тиков
        /// </summary>
        /// <param name="trades"></param>
        void _tickStorage_TickLoadedEvent(List<Trade>[] trades)
        {
            _allTrades = trades;
        }

        /// <summary>
        /// все тики имеющиеся у сервера
        /// </summary>
        public List<Trade>[] AllTrades { get 
        {
            return _allTrades;
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
        /// блокиратор многопоточного доступа к тикам
        /// </summary>
        private object _newTradesLoker = new object();

        /// <summary>
        /// из системы пришли новые тики
        /// </summary>
        /// <param name="allTrade"></param>
        void QuilLua_AddTick(AllTrade allTrade)
        {
            try
            {
                if (allTrade== null)
                {
                    return;
                }
                   
                lock (_newTradesLoker)
                {
                    Trade trade = new Trade();
                    trade.SecurityNameCode = allTrade.SecCode;
                    trade.Id = allTrade.TradeNum.ToString();
                    trade.Price = Convert.ToDecimal(allTrade.Price);
                    trade.Volume = Convert.ToInt32(allTrade.Qty);
                    trade.Side = Convert.ToInt32(allTrade.Flags) == 1025 ? Side.Sell : Side.Buy;
                    trade.Time = new DateTime(allTrade.Datetime.year, allTrade.Datetime.month, allTrade.Datetime.day,
                                              allTrade.Datetime.hour, allTrade.Datetime.min, allTrade.Datetime.sec);

                    BathTradeMarketDepthData(trade);

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
        /// новый тик
        /// </summary>
        public event Action<List<Trade>> NewTradeEvent;

// мои сделка

        private List<MyTrade> _myTrades;

        /// <summary>
        /// мои сделки
        /// </summary>
        public List<MyTrade> MyTrades
        {
            get { return _myTrades; }
        }

        private object myTradeLocker = new object();

        /// <summary>
        /// входящие из системы мои сделки
        /// </summary>
        private void QuikLua_AddTrade(QuikSharp.DataStructures.Transaction.Trade qTrade)
        {
            lock (myTradeLocker)
            {
                try
                {
                    if (_lastStartServerTime.AddSeconds(15) > DateTime.Now)
                    {
                        return;
                    }

                    MyTrade trade = new MyTrade();
                    trade.NumberTrade = qTrade.TradeNum.ToString();
                    trade.SecurityNameCode = qTrade.SecCode;
                    trade.NumberOrderParent = qTrade.OrderNum.ToString();
                    trade.Price = Convert.ToDecimal(qTrade.Price);
                    trade.Volume = qTrade.Quantity;
                    trade.Time = new DateTime(qTrade.QuikDateTime.year, qTrade.QuikDateTime.month,
                                              qTrade.QuikDateTime.day, qTrade.QuikDateTime.hour,
                                              qTrade.QuikDateTime.min, qTrade.QuikDateTime.sec);
                    trade.Side = qTrade.Flags == OrderTradeFlags.IsSell ? Side.Sell : Side.Buy;

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
                            QuikSharp.DataStructures.Transaction.Order qOrder = new QuikSharp.DataStructures.Transaction.Order();

                            qOrder.SecCode = order.SecurityNameCode;
                            qOrder.Account = order.PortfolioNumber;
                            qOrder.ClassCode = _securities.Find(sec => sec.Name == order.SecurityNameCode).NameClass;
                            qOrder.Quantity = Convert.ToInt32(order.Volume);
                            qOrder.Operation = order.Side == Side.Buy ? Operation.Buy : Operation.Sell;
                            qOrder.Price = order.Price;
                            qOrder.Comment = order.NumberUser.ToString();
                            
                            lock (_serverLocker)
                            {
                                var res = QuikLua.Orders.CreateOrder(qOrder).Result;

                                if (res > 0)
                                {
                                    order.NumberUser = Convert.ToInt32(res);
                                    _ordersToSend.Enqueue(order);
                                }

                                if (res < 0)
                                {
                                    order.State = OrderStateType.Fail;
                                    _ordersToSend.Enqueue(order);
                                }

                            }
                        }
                    }
                    else if (_ordersToCansel != null && _ordersToCansel.Count != 0)
                    {
                        Order order;
                        if (_ordersToCansel.TryDequeue(out order))
                        {
                            QuikSharp.DataStructures.Transaction.Order qOrder = new QuikSharp.DataStructures.Transaction.Order();

                            qOrder.SecCode = order.SecurityNameCode;
                            qOrder.Account = order.PortfolioNumber;
                            qOrder.ClassCode = _securities.Find(sec => sec.Name == order.SecurityNameCode).NameClass;

                            if (order.NumberMarket == "")
                            {
                                qOrder.OrderNum = 0;
                            }
                            else
                            {
                                qOrder.OrderNum = Convert.ToInt64(order.NumberMarket);
                            }
                            //qOrder.OrderNum = Convert.ToInt64(order.NumberMarket);

                            lock (_serverLocker)
                            {
                                var res = QuikLua.Orders.KillOrder(qOrder).Result;
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
        /// очередь ордеров для выставления в систему
        /// </summary>
        private ConcurrentQueue<Order> _ordersToExecute;

        /// <summary>
        /// очередь ордеров для отмены в системе
        /// </summary>
        private ConcurrentQueue<Order> _ordersToCansel;

        private List<Order> _ordersAllReadyCanseled = new List<Order>(); 

        /// <summary>
        /// блокиратор доступа к ордерам
        /// </summary>
        private object orderLocker = new object();

        /// <summary>
        /// входящий из системы ордер
        /// </summary>
        private void QuikLua_UpdateOrder(QuikSharp.DataStructures.Transaction.Order qOrder)
        {
            lock (orderLocker)
            {
                try
                {
                    if (_lastStartServerTime.AddSeconds(15) > DateTime.Now)
                    {
                        return;
                    }

                    if (qOrder.TransID == 0)
                    {
                        return;
                    }

                    Order order = new Order();
                    order.NumberUser = Convert.ToInt32(qOrder.TransID); //Convert.qOrder.OrderNum;TransID
                    order.NumberMarket = qOrder.OrderNum.ToString(new CultureInfo("ru-RU"));
                    order.TimeCallBack = ServerTime;
                    order.SecurityNameCode = qOrder.SecCode;
                    order.Price = qOrder.Price;
                    order.Volume = qOrder.Quantity;
                    order.VolumeExecute = qOrder.Quantity - qOrder.Balance;
                    order.PortfolioNumber = qOrder.Account;
                    order.TypeOrder = qOrder.Flags.ToString().Contains("IsLimit") ? OrderPriceType.Limit : OrderPriceType.Market;
                    
                    if (qOrder.State == State.Active)
                    {
                        order.State = OrderStateType.Activ;
                        order.TimeCallBack = new DateTime(qOrder.Datetime.year, qOrder.Datetime.month,
                            qOrder.Datetime.day,
                            qOrder.Datetime.hour, qOrder.Datetime.min, qOrder.Datetime.sec);
                    }
                    else if (qOrder.State == State.Completed)
                    {
                        order.State = OrderStateType.Done;
                        order.VolumeExecute = qOrder.Quantity;
                    }
                    else if (qOrder.State == State.Canceled)
                    {
                        order.TimeCancel = new DateTime(qOrder.WithdrawDatetime.year, qOrder.WithdrawDatetime.month,
                            qOrder.WithdrawDatetime.day,
                            qOrder.WithdrawDatetime.hour, qOrder.WithdrawDatetime.min, qOrder.WithdrawDatetime.sec);
                        order.State = OrderStateType.Cancel;
                        order.VolumeExecute = 0;
                    }
                    else if (qOrder.Balance != 0)
                    {
                        order.State = OrderStateType.Patrial;
                        order.VolumeExecute = qOrder.Quantity - qOrder.Balance;
                    }

                    if (_ordersAllReadyCanseled.Find(o => o.NumberUser == qOrder.TransID) != null)
                    {
                        order.State = OrderStateType.Cancel;
                    }

                    if (qOrder.Operation == Operation.Buy)
                    {
                        order.Side = Side.Buy;
                    }
                    else
                    {
                        order.Side = Side.Sell;
                    }

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
            _ordersAllReadyCanseled.Add(order);
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
