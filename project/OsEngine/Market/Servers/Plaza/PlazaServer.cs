using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.Plaza.Entity;
using ru.micexrts.cgate;
using ru.micexrts.cgate.message;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading;
using Message = ru.micexrts.cgate.message.Message;

namespace OsEngine.Market.Servers.Plaza
{
    public class PlazaServer : AServer
    {
        public PlazaServer()
        {
            PlazaServerRealization realization = new PlazaServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "11111111");
            CreateParameterInt("Max orders per second", 30);
            CreateParameterBoolean("Cancel on disconnect", true);
            CreateParameterEnum("Connection type", "p2lrpcq", new List<string> { "p2tcp", "p2lrpcq" });
        }
    }

    public class PlazaServerRealization : IServerRealization
    {
        #region 1 Constructor, Connection, Dispose

        public PlazaServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
            _statusNeeded = ServerConnectStatus.Disconnect;

            Thread worker1 = new Thread(PrimeWorkerThreadSpace);
            worker1.CurrentCulture = new CultureInfo("ru-RU");
            worker1.IsBackground = true;
            worker1.Start();

            Thread worker2 = new Thread(HeartBeatSender);
            worker2.CurrentCulture = new CultureInfo("ru-RU");
            worker2.IsBackground = true;
            worker2.Start();
        }

        public void Connect(WebProxy proxy)
        {
            string key = ((ServerParameterString)ServerParameters[0]).Value;
            int limitation = ((ServerParameterInt)ServerParameters[1]).Value;
            _COD = ((ServerParameterBool)ServerParameters[2]).Value;
            string connectionType = ((ServerParameterEnum)ServerParameters[3]).Value;

            _rateGate = new RateGate(1, TimeSpan.FromMilliseconds(1000 / limitation));

            if (string.IsNullOrEmpty(key))
            {
                SendLogMessage("Connection terminated. You must specify the api token.",
                    LogMessageType.Error);
                return;
            }

            string gateOpenString = "ini=Plaza/PlazaStartSettings.ini;key=" + key;

            // Initializing the environment | Инициализация среды
            try
            {
                if (!_flagCGate)
                {
                    CGate.Open(gateOpenString);

                    _flagCGate = true;
                }
            }
            catch (Exception error)
            {
                SendLogMessage($"Initialization error. Check the configuration. Errors - {error}", LogMessageType.Error);

                Dispose();

                return;
            }

            // Creating a connection to the router | Создание соединения с роутером
            try
            {
                if (connectionType == "p2tcp")
                {
                    Connection = new Connection(ConnectionOpenString_p2tcp);
                }
                else
                {
                    Connection = new Connection(ConnectionOpenString_p2lrpcq);
                }
            }
            catch (CGateException error)
            {
                SendLogMessage($"Failed to initialize connection. " +
                    $"Incorrect arguments may have been passed to the function. Errors - {error}", LogMessageType.Error);
                return;
            }

            _statusNeeded = ServerConnectStatus.Connect;
        }

        /// <summary>
        /// Clear all objects involved in the connection with Plaza 2 | 
        /// Очистить все объекты участвующие в соединение с Плаза 2
        /// </summary>
        public void Dispose()
        {
            lock (_ConnectionLocker)
            {
                _statusNeeded = ServerConnectStatus.Disconnect;

                Thread.Sleep(1000);

                // closing the connection to the router | закрываем соединение с роутером

                if (Connection != null)
                {
                    try
                    {
                        Connection.Close();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage($"The connection to the router could not be closed. Error - {error}", LogMessageType.Error);
                    }

                    try
                    {
                        Connection.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage($"An error occurred when closing the connection to the router. Error - {error}", LogMessageType.Error);
                    }
                    Connection = null;
                }

                // closing the listener // отключаем листнеры

                if (_listenerInfo != null)
                {
                    try
                    {
                        if (_listenerInfo.State == State.Active)
                        {
                            _listenerInfo.Close();
                        }
                        _listenerInfo.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                    }

                    _listenerInfoNeedToReconnect = false;
                    _listenerInfo = null;
                }

                if (_listenerPortfolio != null)
                {
                    try
                    {
                        if (_listenerPortfolio.State == State.Active)
                        {
                            _listenerPortfolio.Close();
                        }
                        _listenerPortfolio.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                    }

                    _listenerPortfolioNeedToReconnect = false;
                    _listenerPortfolio = null;
                }

                if (_listenerPosition != null)
                {
                    try
                    {
                        if (_listenerPosition.State == State.Active)
                        {
                            _listenerPosition.Close();
                        }
                        _listenerPosition.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                    }

                    _listenerPositionNeedToReconnect = false;
                    _listenerPosition = null;
                }

                if (_listenerTrade != null)
                {
                    try
                    {
                        if (_listenerTrade.State == State.Active)
                        {
                            _listenerTrade.Close();
                        }
                        _listenerTrade.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                    }

                    _listenerTradeNeedToReconnect = false;
                    _listenerTrade = null;
                }

                if (_listenerMarketDepth != null)
                {
                    try
                    {
                        if (_listenerMarketDepth.State == State.Active)
                        {
                            _listenerMarketDepth.Close();
                        }
                        _listenerMarketDepth.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                    }

                    _listenerMarketDepthNeedToReconnect = false;
                    _listenerMarketDepth = null;
                }

                if (_listenerOrderAndMyDeal != null)
                {
                    try
                    {
                        if (_listenerOrderAndMyDeal.State == State.Active)
                        {
                            _listenerOrderAndMyDeal.Close();
                        }
                        _listenerOrderAndMyDeal.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                    }

                    _listenerOrderAndMyDealNeedToReconnect = false;
                    _listenerOrderAndMyDeal = null;
                }

                if (_listenerUserOrderBook != null)
                {
                    try
                    {
                        if (_listenerUserOrderBook.State == State.Active)
                        {
                            _listenerUserOrderBook.Close();
                        }
                        _listenerUserOrderBook.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                    }

                    _listenerUserOrderBook = null;
                }

                if (_listenerOrderSendMirror != null)
                {
                    try
                    {
                        if (_listenerOrderSendMirror.State == State.Active)
                        {
                            _listenerOrderSendMirror.Close();
                        }
                        _listenerOrderSendMirror.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                    }

                    _listenerOrderSendMirror = null;
                }

                //closing the publisher | отключаем паблишер ордеров

                if (_publisher != null)
                {
                    try
                    {
                        if (_publisher.State == State.Active)
                        {
                            _publisher.Close();
                            _publisher.Dispose();
                        }
                        else
                        {
                            _publisher.Dispose();
                        }
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                    }

                    _publisher = null;
                }

                _marketDepths = new List<MarketDepth>();
                _lastRevisions = null;
                _currentOrders = new List<Order>();
                _ordersToExecute = new Queue<Order>();
                _ordersToCansel = new Queue<Order>();
                _ordersToChange = new Queue<Order>();
                _securitiesDepth = new List<Security>();

                // closing the connection with the environment | закрываем соединение с окружением

                if (_flagCGate)
                {
                    try
                    {
                        CGate.Close();
                    }
                    catch (CGateException error)
                    {
                        SendLogMessage($"An error occurred when closing the connection to the environment. Error - {error}", LogMessageType.Error);
                    }

                    _flagCGate = false;
                }

                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
        }

        public ServerType ServerType => ServerType.Plaza;

        public DateTime ServerTime { get; set; }

        public ServerConnectStatus ServerStatus { get; set; }

        public List<IServerParameter> ServerParameters { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region 2 Properties, Connection strings

        private RateGate _rateGate;

        /// <summary>
        /// a flag indicating whether the connection to CGate is open or not |
        /// флаг, указывающий, открыто ли соединение с CGate или нет
        /// </summary>
        private bool _flagCGate;

        /// <summary>
        /// flag. Do transactions go online. used on first boot, the program caches all the tics from the plaza is very hard to take
        /// and until all ticks reach the first time, subscribing to depths and trading will not work | 
        /// флаг. идут ли сделки онлайн. используется при первой загрузке, 
        /// т.к. программа кэширует все тики из плазы это очень тяжело принимать и пока все тики первый раз не дойдут, 
        /// подписываться на стаканы и торговать не выйдет
        /// </summary>
        private bool _dealsOnLine;

        /// <summary>
		/// object to connect to router
        /// объект подключения к Роутеру
        /// </summary>
        public Connection Connection;

        /// <summary>
        /// User-ordered status. If it is different from the current server status main thread will try to bring them to the same value | 
        /// Статус заказанный пользователем. Если он будет отличаться от текущего статуса сервера основной поток будет пытаться привести их к одному значению
        /// </summary>
        private ServerConnectStatus _statusNeeded;

        /// <summary>
		/// multi-threaded access locker to Plaza object |
        /// объект участвующий в блокировке многопоточного доступа к объектам Плаза
        /// </summary>

        private object _publisherLocker = new object();

        private object _currentOrdersLosker = new object();

        /// <summary>
        /// if the limit on the number of applications per second is exceeded, the system blocks the acceptance of new applications for a certain time |
        /// при превышении ограничения по количеству заявок в секунду, система блокирует на определенное время прием новых заявок
        /// </summary>
        private int _penaltyRemain = 0;

        private object _penaltyRemainLock = new object();

        /// <summary>
		/// order queue for execution |
        /// очередь заявок на исполнение
        /// </summary>
        private Queue<Order> _ordersToExecute;

        private List<Order> _currentOrders;

        /// <summary>
		/// order queue for cancellation |
        /// очередь заявок на отмену
        /// </summary>
        private Queue<Order> _ordersToCansel;

        /// <summary>
        /// order queue for price changes |
        /// очередь заказов на изменение цены
        /// </summary>
        private Queue<Order> _ordersToChange;

        /// <summary>
        /// a flag indicating the use of the Cancel on Disconnect method | 
        /// флаг, указывающий на использование метода Cancel on Disconnect
        /// </summary>
        private bool _COD = false;

        /// <summary>
        /// Connector connection string. This connection string does not need to be deleted. It should be used during tests on a test server. It initializes the TCP connector to the Router, it is slower, but safer. 
        ///  With this connection, the connection with the router is automatically disconnected when the connection is abnormally closed. | 
        ///  Строка подключения коннектора. Эту строку подключения не нужно удалять. Её нужно использовать во время тестов на тестовом сервере. Она инициализирует коннектор к Роутеру по ТСП, это медленне, но безопаснее.
        ///  При таком подключении соединение с Роутером автоматически разрывается при нештатном закрытии соединения
        /// </summary>
        private const string ConnectionOpenString_p2tcp = "p2tcp://127.0.0.1:4001;app_name=OsEngine100;name=connectorPrime100";

        /// <summary>
        /// Connector connection string. Initializes the connection to the router through shared memory. If such a connection is not closed normally, you will have to go to the task manager and wet the P2MQRouter * 32 
        /// from the C drive, launch the Router via start_router.cmd | Строка подключения коннектора. Инициализирует подключение к Роутеру через разделяемую память
        /// В случае, если такое соединение закрылось не штатно, придётся идти в диспетчер задач и закрывать P2MQRouter*32. Далее из диска С запускать Роутер через start_router.cmd
        /// </summary>
        private const string ConnectionOpenString_p2lrpcq = "p2lrpcq://127.0.0.1:4001;app_name=OsEngine;timeout=2000;local_timeout=500;name=connectorPrime";

        /// <summary>
        /// initialization line of the Listner responsible for receiving information about the instruments | 
        /// строка инициализации листнера отвечающего за приём информации об инструментах
        /// </summary>
        private const string ListenInfoString = "p2repl://FORTS_REFDATA_REPL;scheme=|FILE|Plaza/Schemas/refdata.ini|CustReplScheme";

        /// <summary>
		/// initialization string of the Listner responsible for getting portfolios | 
        /// строка инициализации листнера отвечающего за приём портфелей
        /// </summary>
        private const string ListenPortfolioString = "p2repl://FORTS_PART_REPL;scheme=|FILE|Plaza/Schemas/part.ini|CustReplScheme";

        /// <summary>
		/// initialization string of the Listner responsible for receiving position | 
        /// строка инициализации листнера отвечающего за приём позиции
        /// </summary>
        private const string ListenPositionString = "p2repl://FORTS_POS_REPL;scheme=|FILE|Plaza/Schemas/pos.ini|CustReplScheme";

        /// <summary>
		/// initialization string of the Listner responsible for receiving ticks | 
        /// строка инициализации листнера отвечающего за приём тиков
        /// </summary>
        private const string ListenTradeString = "p2repl://FORTS_DEALS_REPL;scheme=|FILE|Plaza/Schemas/deals.ini|CustReplScheme";

        /// <summary>
		/// initialization string of the Listner responsible for receiving depth | 
        /// строка инициализации для листнера отвечающего за приём среза стакана
        /// </summary>
        private const string ListenMarketDepth = "p2repl://FORTS_AGGR20_REPL;scheme=|FILE|Plaza/Schemas/orders_aggr.ini|CustReplScheme";

        /// <summary>
		/// initialization string of the Listner responsible for receiving my trades and my orders |
        /// строка инициализации листнера отвечающего за приём Моих сделок и моих ордеров
        /// </summary>
        private const string ListenOrderAndMyTradesString = "p2repl://FORTS_TRADE_REPL;scheme=|FILE|Plaza/Schemas/trades.ini|CustReplScheme";

        /// <summary>
		/// initialization string of the lister responsible for accepting active user orders |
        /// строка инициализации листнера отвечающего за приём активных заявок пользователя
        /// </summary>
        private const string ListenUserOrderBookString = "p2repl://FORTS_USERORDERBOOK_REPL;scheme=|FILE|Plaza/Schemas/userorderbook.ini|CustReplScheme";

        /// <summary>
		/// initialization string for order publisher | 
        /// строка инициализации для публишера ордеров
        /// </summary>
        private const string PublisherOrdersString = "p2mq://FORTS_SRV;category=FORTS_MSG;name=srvlink;timeout=5000;scheme=|FILE|Plaza/Schemas/forts_messages.ini|message";

        /// <summary>
		/// initialization string for transaction tracing list listener | 
        /// строка инициализации для листнера следящего за реакцией публишера сделок
        /// </summary>
        private const string ListnerOrders = "p2mqreply://;ref=srvlink";

        #endregion

        #region 3 Main stream, ConnectEvent

        private object _ConnectionLocker = new object();

        private DateTime _connectionDateTime = DateTime.MinValue;

        /// <summary>
		/// place of main stream work
        /// место работы основного потока
        /// </summary>
        /// 
        private void PrimeWorkerThreadSpace()
        {
            try
            {
                while (true)
                {
                    lock (_ConnectionLocker)
                    {
                        // Reconnect if there are problems with the router at startup | Переподключаемся если проблемы с роутером при запуске
                        if (Connection == null && _statusNeeded == ServerConnectStatus.Connect)
                        {
                            Dispose();
                            Connect(null);

                            continue;
                        }
                        else if (Connection == null)
                        {
                            Thread.Sleep(1000);

                            continue;
                        }

                        if (_statusNeeded == ServerConnectStatus.Disconnect && Connection.State == State.Closed)
                        {
                            Thread.Sleep(1000);

                            continue;
                        }

                        // Reconnect if there are problems with the router | Переподключаемся если возникли проблемы с роутером 
                        if (Connection.State == State.Error && _statusNeeded == ServerConnectStatus.Connect)
                        {
                            SendLogMessage("Router error", LogMessageType.Error);

                            Dispose();
                            Connect(null);

                            continue;
                        }

                        if (Connection.State == State.Closed &&
                            _statusNeeded == ServerConnectStatus.Connect)
                        {
                            try
                            {
                                if (_connectionDateTime.AddSeconds(1) > DateTime.Now)
                                {
                                    continue;
                                }

                                _connectionDateTime = DateTime.Now;

                                Connection.Open("");

                                continue;
                            }
                            catch (Exception error)
                            {
                                SendLogMessage($"The connection to the router could not be opened. Error - {error} ", LogMessageType.Connect);

                                continue;
                            }
                        }

                        if (_statusNeeded == ServerConnectStatus.Connect && Connection.State == State.Active && ServerStatus == ServerConnectStatus.Disconnect && _dealsOnLine)
                        {
                            ServerStatus = ServerConnectStatus.Connect;

                            if (ConnectEvent != null)
                            {
                                ConnectEvent();
                            }
                        }

                        if (Connection.State != State.Active)
                        {
                            continue;
                        }

                        //connect listeners | подключаем листнеры

                        CheckOrderSender(); // publisher | паблишер 

                        CheckListnerTradedInstruments(); // Directory of traded instruments | Список торгуемых инструментов

                        CheckListnerPortfolio(); // Listening to the portfolio | Прослушка портфелей

                        CheckListnerPosition(); // Listening to positions on portfolios | Прослушка позиции по портфелям

                        CheckUserOrderBook(); // User orders book | активные заявки пользователя

                        CheckListnerTrades(); // listening to deals and applications | прослушка сделки и завки

                        CheckListnerMyTrades(); // Listening to order log and my trades | прослушка ордер лог и мои сделки

                        CheckListnerMarketDepth(); // depth | стакан

                        Connection.Process(1);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);

                Dispose();
            }
        }

        #endregion

        #region 4 Check for a listener connection

        /// <summary>
        /// listner responsible for accepting tools
        /// листнер отвечающий за приём инструментов
        /// </summary>
        private Listener _listenerInfo;

        /// <summary>
        /// Flag saying that the connection with the list was interrupted and it requires a full reboot | 
        /// Флаг, говорящий о том что коннект с листнером прервался и требуется его полная перезагрузка
        /// </summary>
        private bool _listenerInfoNeedToReconnect;

        /// <summary>
		/// thread state of instrument listener | 
        /// состояние потока листнера инструментов
        /// </summary>
        private string _listenerInfoReplState;

        private DateTime _listenerInfoDateTime = DateTime.MinValue;

        /// <summary>
		/// check the instrument listener for connection and validity | 
        /// проверить на подключение и валидность листнер инструментов
        /// </summary>
        private void CheckListnerTradedInstruments()
        {
            try
            {
                if (_listenerInfo == null)
                {
                    _listenerInfo = new Listener(Connection, ListenInfoString);
                    _listenerInfo.Handler += ClientMessage_Board;
                }

                if (_listenerInfoNeedToReconnect || _listenerInfo.State == State.Error)
                {
                    _listenerInfoNeedToReconnect = false;
                    try
                    {
                        _listenerInfo.Close();
                        _listenerInfo.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.Message, LogMessageType.Error);
                    }

                    _listenerInfo = null;
                    return;
                }

                if (_listenerInfo.State == State.Closed)
                {
                    if (_listenerInfoReplState == null)
                    {
                        _listenerInfoDateTime = DateTime.Now;

                        _listenerInfo.Open("mode=snapshot+online");
                    }
                    else
                    {
                        if (_listenerInfoDateTime.AddSeconds(1) > DateTime.Now)
                        {
                            return;
                        }

                        _listenerInfoDateTime = DateTime.Now;

                        _listenerInfo.Open("mode=snapshot+online;" + "replstate=" + _listenerInfoReplState);
                    }
                }
            }
            catch
            {
                if (_listenerInfo != null)
                {
                    try
                    {
                        _listenerInfo.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                _listenerInfo = null;
            }
        }

        /// <summary>
		/// portfolio listener | 
        /// листнер портфелей
        /// </summary>
        private Listener _listenerPortfolio;

        /// <summary>
        /// flag saying that the connection with the list was interrupted and it requires a full reboot | 
        /// флаг, говорящий о том что коннект с листнером прервался и требуется его полная перезагрузка
        /// </summary>
        private bool _listenerPortfolioNeedToReconnect;

        /// <summary>
		/// thread state of portfolio listener | 
        /// состояние потока листнера портфелей
        /// </summary>
        private string _listenerPortfolioReplState;

        private DateTime _listenerPortfolioDateTime = DateTime.MinValue;

        /// <summary>
		/// check the position and portfolio listener for connection and validity | 
        /// проверить на подключение и валидность листнер позиций и портфелей
        /// </summary>
        private void CheckListnerPortfolio()
        {
            try
            {
                if (_listenerPortfolio == null)
                {
                    _listenerPortfolio = new Listener(Connection, ListenPortfolioString);
                    _listenerPortfolio.Handler += ClientMessage_Portfolio;
                }

                if (_listenerPortfolioNeedToReconnect || _listenerPortfolio.State == State.Error)
                {
                    _listenerPortfolioNeedToReconnect = false;
                    try
                    {
                        _listenerPortfolio.Close();
                        _listenerPortfolio.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.Message, LogMessageType.Error);
                    }

                    _listenerPortfolio = null;
                    return;
                }

                if (_listenerPortfolio.State == State.Closed)
                {
                    if (_listenerPortfolioReplState == null)
                    {
                        _listenerPortfolioDateTime = DateTime.Now;

                        _listenerPortfolio.Open("mode=snapshot+online");
                    }
                    else
                    {
                        if (_listenerPortfolioDateTime.AddSeconds(1) > DateTime.Now)
                        {
                            return;
                        }

                        _listenerPortfolioDateTime = DateTime.Now;

                        _listenerPortfolio.Open("mode=snapshot+online;" + "replstate=" + _listenerPortfolioReplState);
                    }
                }
            }
            catch (CGateException)
            {
                if (_listenerPortfolio != null)
                {
                    try
                    {
                        _listenerPortfolio.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                _listenerPortfolio = null;
            }
        }

        /// <summary>
		/// position listener | 
        /// листнет позиций
        /// </summary>
        private Listener _listenerPosition;

        /// <summary>
		/// thread state of position listener | 
        /// состояние потока листнера позиций
        /// </summary>
        private string _listenerPositionReplState;

        /// <summary>
        /// flag saying that the connection with the list was interrupted and it requires a full reboot |
        /// флаг, говорящий о том что коннект с листнером прервался и требуется его полная перезагрузка
        /// </summary>
        private bool _listenerPositionNeedToReconnect;

        private DateTime _listenerPositionDateTime = DateTime.MinValue;

        /// <summary>
		/// check the position listener for connection and validity | 
        /// проверить на подключение и валидность листнер позиций
        /// </summary>
        private void CheckListnerPosition()
        {
            try
            {
                if (_listenerPosition == null)
                {
                    _listenerPosition = new Listener(Connection, ListenPositionString);
                    _listenerPosition.Handler += ClientMessage_Position;
                }

                if (_listenerPositionNeedToReconnect || _listenerPosition.State == State.Error)
                {
                    _listenerPositionNeedToReconnect = false;
                    try
                    {
                        _listenerPosition.Close();
                        _listenerPosition.Dispose();
                    }
                    catch (Exception ex)
                    {
                        SendLogMessage(ex.Message, LogMessageType.Error);
                    }

                    _listenerPosition = null;
                    return;
                }

                if (_listenerPosition.State == State.Closed)
                {
                    if (_listenerPositionReplState == null)
                    {
                        _listenerPositionDateTime = DateTime.Now;

                        _listenerPosition.Open("mode=snapshot+online");
                    }
                    else
                    {
                        if (_listenerPositionDateTime.AddSeconds(1) > DateTime.Now)
                        {
                            return;
                        }

                        _listenerPositionDateTime = DateTime.Now;

                        _listenerPosition.Open("mode=snapshot+online;replstate=" + _listenerPositionReplState);
                    }
                }
            }
            catch
            {
                if (_listenerPosition != null)
                {
                    try
                    {
                        _listenerPosition.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                _listenerPosition = null;
            }
        }

        /// <summary>
		/// tick listener
        /// листнер тиков
        /// </summary>
        private Listener _listenerTrade;

        /// <summary>
        /// flag saying that the connection with the list was interrupted and it requires a full reboot | 
        ///флаг, говорящий о том что коннект с листнером прервался и требуется его полная перезагрузка
        /// </summary>
        private bool _listenerTradeNeedToReconnect;

        /// <summary>
		/// thread state of tick listener | 
        /// состояние потока листнера тиков
        /// </summary>
        private string _listenerTradeReplState;

        private DateTime _listenerTradeDateTime = DateTime.MinValue;

        /// <summary>
        /// check tick listener for connection and validity | 
        /// проверить на подключение и валидность листнер тиков
        /// </summary>
        private void CheckListnerTrades()
        {
            try
            {
                if (_listenerTrade == null)
                {
                    _listenerTrade = new Listener(Connection, ListenTradeString);
                    _listenerTrade.Handler += ClientMessage_Trades;
                }
                if (_listenerTradeNeedToReconnect || _listenerTrade.State == State.Error)
                {
                    _listenerTradeNeedToReconnect = false;
                    try
                    {
                        _listenerTrade.Close();
                        _listenerTrade.Dispose();
                    }
                    catch (Exception ex)
                    {
                        SendLogMessage(ex.Message, LogMessageType.Error);
                    }

                    _listenerTrade = null;
                    return;
                }
                if (_listenerTrade.State == State.Closed)
                {
                    if (_listenerTradeReplState == null)
                    {
                        _listenerTradeDateTime = DateTime.Now;

                        _listenerTrade.Open("mode=online");
                    }
                    else
                    {
                        if (_listenerTradeDateTime.AddSeconds(1) > DateTime.Now)
                        {
                            return;
                        }

                        _listenerTradeDateTime = DateTime.Now;

                        _listenerTrade.Open("mode=snapshot+online;" + "replstate=" + _listenerTradeReplState);
                    }
                }

            }
            catch
            {
                if (_listenerTrade != null)
                {
                    try
                    {
                        _listenerTrade.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                _listenerTrade = null;
            }
        }

        /// <summary>
		/// depth listener
        /// листнер стаканов
        /// </summary>
        private Listener _listenerMarketDepth;

        /// <summary>
        /// flag saying that the connection with the list was interrupted and it requires a full reboot | 
        /// флаг, говорящий о том что коннект с листнером прервался и требуется его полная перезагрузка
        /// </summary>
        private bool _listenerMarketDepthNeedToReconnect;

        private DateTime _listenerMarketDepthDateTime = DateTime.MinValue;

        /// <summary>
		/// check connection and validity listener of depth
        /// проверить на подключение и валидность листнер среза стаканов
        /// </summary>
        private void CheckListnerMarketDepth()
        {
            try
            {
                if (_listenerMarketDepth == null)
                {
                    _listenerMarketDepth = new Listener(Connection, ListenMarketDepth);
                    _listenerMarketDepth.Handler += ClientMessage_OrderLog;
                }

                if (_listenerMarketDepthNeedToReconnect || _listenerMarketDepth.State == State.Error)
                {
                    _listenerMarketDepthNeedToReconnect = false;
                    try
                    {
                        _listenerMarketDepth.Close();
                        _listenerMarketDepth.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.Message, LogMessageType.Error);
                    }

                    _listenerMarketDepth = null;
                    return;
                }

                if (_listenerMarketDepth.State == State.Closed)
                {
                    if (_listenerMarketDepthDateTime.AddSeconds(1) > DateTime.Now)
                    {
                        return;
                    }

                    _listenerMarketDepthDateTime = DateTime.Now;

                    _listenerMarketDepth.Open("mode=snapshot+online");
                }
            }
            catch
            {
                if (_listenerMarketDepth != null)
                {
                    try
                    {
                        _listenerMarketDepth.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                _listenerMarketDepth = null;
            }
        }

        /// <summary>
		/// listener of my deals and my orders
        /// листнер моих сделок и моих ордеров
        /// </summary>
        private Listener _listenerOrderAndMyDeal;

        /// <summary>
        /// flag saying that the connection with the list was interrupted/флаг, говорящий о том что коннект с листнером прервался 
        /// and it requires a full reboot/и требуется его полная перезагрузка
        /// </summary>
        private bool _listenerOrderAndMyDealNeedToReconnect;

        /// <summary>
		/// thread state of my trades and my orders listener
        /// состояние потока листнера моих сделок и моих ордеров
        /// </summary>
        private string _listenerOrderAndMyDealReplState;

        private DateTime _listenerOrderAndMyDealDateTime = DateTime.MinValue;

        /// <summary>
		/// check my orders and my trades listener for connection and validity | 
        /// проверить на подключение и валидность листнер моих ордеров и моих сделок
        /// </summary>
        private void CheckListnerMyTrades()
        {
            try
            {
                if (_listenerOrderAndMyDeal == null)
                {
                    _listenerOrderAndMyDeal = new Listener(Connection, ListenOrderAndMyTradesString);
                    _listenerOrderAndMyDeal.Handler += ClientMessage_OrderAndMyDeal;
                }

                if (_listenerOrderAndMyDealNeedToReconnect || _listenerOrderAndMyDeal.State == State.Error)
                {
                    _listenerOrderAndMyDealNeedToReconnect = false;

                    try
                    {
                        _listenerOrderAndMyDeal.Close();
                        _listenerOrderAndMyDeal.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.Message, LogMessageType.Error);
                    }

                    _listenerOrderAndMyDeal = null;
                    return;
                }

                if (_listenerOrderAndMyDeal.State == State.Closed)
                {
                    if (_listenerOrderAndMyDealReplState == null)
                    {
                        _listenerOrderAndMyDealDateTime = DateTime.Now;

                        _listenerOrderAndMyDeal.Open("mode=snapshot+online");
                    }
                    else
                    {
                        if (_listenerOrderAndMyDealDateTime.AddSeconds(1) > DateTime.Now)
                        {
                            return;
                        }

                        _listenerOrderAndMyDealDateTime = DateTime.Now;

                        _listenerOrderAndMyDeal.Open("mode=snapshot+online;" + "replstate=" + _listenerOrderAndMyDealReplState);
                    }
                }
            }
            catch
            {
                if (_listenerOrderAndMyDeal != null)
                {
                    try
                    {
                        _listenerOrderAndMyDeal.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                _listenerOrderAndMyDeal = null;

            }
        }

        private DateTime _listenerUserOrderBookDateTime = DateTime.MinValue;

        /// <summary>
		/// listener of user order book
        /// листенер активных заявок пользователя
        /// </summary>
        private Listener _listenerUserOrderBook;

        private void CheckUserOrderBook()
        {
            try
            {
                if (_listenerUserOrderBook == null)
                {
                    _listenerUserOrderBook = new Listener(Connection, ListenUserOrderBookString);
                    _listenerUserOrderBook.Handler += ClientMessage_UserOrderBook;
                }

                if (_listenerUserOrderBook.State == State.Error)
                {
                    try
                    {
                        _listenerUserOrderBook.Close();
                        _listenerUserOrderBook.Dispose();

                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.Message, LogMessageType.Error);
                    }

                    _listenerUserOrderBook = null;
                    return;
                }

                if (_listenerUserOrderBook.State == State.Closed)
                {
                    if (_listenerUserOrderBookDateTime.AddSeconds(1) > DateTime.Now)
                    {
                        return;
                    }

                    _listenerUserOrderBookDateTime = DateTime.Now;

                    _listenerUserOrderBook.Open("mode=online");
                }
            }
            catch
            {
                if (_listenerUserOrderBook != null)
                {
                    try
                    {
                        _listenerUserOrderBook.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                _listenerUserOrderBook = null;
            }
        }

        /// <summary>
		/// trades publisher and CODHeartbeat | 
        /// публишер сделок и CODHeartbeat
        /// </summary>
        private Publisher _publisher;

        /// <summary>
		/// listener listening to publisher's answers (thing that transmit orders to the system) | 
        /// листнер прослушивающий ответы публишера(штуки которая передаёт ордера в систему)
        /// </summary>
        private Listener _listenerOrderSendMirror;

        private DateTime _publisherDateTime = DateTime.Now;

        private DateTime _listenerOrderSendMirrorDateTime = DateTime.MinValue;

        /// <summary>
		/// check on the connection and validity of the publisher and listner watching his responses |
        /// проверить на подключение и валидность публишер и листнер следящий за его откликами
        /// </summary>
        private void CheckOrderSender()
        {
            try
            {
                if (_publisher == null)
                {
                    _publisher = new Publisher(Connection, PublisherOrdersString);
                }

                if (_publisher.State == State.Closed)
                {
                    if (_publisherDateTime.AddSeconds(1) > DateTime.Now)
                    {
                        return;
                    }
                    _publisherDateTime = DateTime.Now;

                    _publisher.Open();
                }

                if (_publisher.State == State.Error)
                {
                    SendLogMessage("Publisher error. Subscribe again", LogMessageType.System);

                    try
                    {
                        _publisher.Close();
                        _publisher.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.Message, LogMessageType.Error);
                    }

                    _publisher = null;
                }
            }
            catch
            {
                if (_publisher != null)
                {
                    try
                    {
                        _publisher.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                _publisher = null;

                SendLogMessage($"Publisher error. Reconnect.", LogMessageType.System);

                Dispose();
                Connect(null);
            }

            try
            {
                if (_listenerOrderSendMirror == null)
                {
                    _listenerOrderSendMirror = new Listener(Connection, ListnerOrders);
                    _listenerOrderSendMirror.Handler += ClientMessage_OrderSenderMirror;
                    return;
                }

                if (_listenerOrderSendMirror.State == State.Error)
                {
                    try
                    {
                        _listenerOrderSendMirror.Close();
                        _listenerOrderSendMirror.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.Message, LogMessageType.Error);
                    }

                    _listenerOrderSendMirror = null;
                    return;
                }

                if (_listenerOrderSendMirror.State == State.Closed)
                {
                    if (_listenerOrderSendMirrorDateTime.AddSeconds(1) > DateTime.Now)
                    {
                        return;
                    }

                    _listenerOrderSendMirrorDateTime = DateTime.Now;

                    _listenerOrderSendMirror.Open("mode=online");
                }
            }
            catch
            {
                if (_listenerOrderSendMirror != null)
                {
                    try
                    {
                        _listenerOrderSendMirror.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                _listenerOrderSendMirror = null;
            }
        }

        #endregion

        #region 5 Data

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            return null;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public event Action<Trade> NewTradesEvent;

        /// <summary>
		/// tick table is processed here
        /// здесь обрабатываются таблица тиков
        /// </summary>
        private int ClientMessage_Trades(Connection conn, Listener listener, Message msg)
        {
            try
            {
                switch (msg.Type)
                {
                    case MessageType.MsgStreamData:
                        {
                            StreamDataMessage replmsg = (StreamDataMessage)msg;
                            if (replmsg.MsgName == "deal")
                            {
                                // ticks/тики
                                try
                                {
                                    //- if id_ord_sell < id_ord_buy, то Side = buy/если id_ord_sell < id_ord_buy, то Операция = Купля
                                    //- if id_ord_sell > id_ord_buy, то Side = sell/если id_ord_sell > id_ord_buy, то Операция = Продажа

                                    byte isSystem = replmsg["nosystem"].asByte();

                                    // если сделка внесистемная, пропускаем ее
                                    if (isSystem == 1)
                                    {
                                        return 0;
                                    }

                                    Trade trade = new Trade();
                                    trade.Price = Convert.ToDecimal(replmsg["price"].asDecimal());
                                    trade.Id = replmsg["id_deal"].asLong().ToString();
                                    trade.Time = replmsg["moment"].asDateTime();
                                    trade.Volume = replmsg["xamount"].asInt();
                                    string securityNameCode = replmsg["isin_id"].asInt().ToString();

                                    long numberBuyOrder = replmsg["public_order_id_buy"].asLong();
                                    long numberSellOrder = replmsg["public_order_id_sell"].asLong();

                                    if (numberBuyOrder > numberSellOrder)
                                    {
                                        trade.Side = Side.Buy;
                                    }
                                    else
                                    {
                                        trade.Side = Side.Sell;
                                    }

                                    Security security = _securities.Find(s => s.NameId == securityNameCode);

                                    if (security == null)
                                    {
                                        break;
                                    }

                                    trade.SecurityNameCode = security.Name;

                                    if (NewTradesEvent != null)
                                    {
                                        NewTradesEvent(trade);
                                    }
                                }
                                catch (Exception error)
                                {
                                    SendLogMessage(error.ToString(), LogMessageType.Error);
                                }
                            }
                            break;
                        }
                    case MessageType.MsgP2ReplOnline:
                        {
                            _dealsOnLine = true;

                            break;
                        }
                    case MessageType.MsgP2ReplReplState:
                        {
                            _listenerTradeReplState = ((P2ReplStateMessage)msg).ReplState;
                            _listenerTradeNeedToReconnect = true;
                            break;
                        }
                    case MessageType.MsgClose:
                        {
                            _dealsOnLine = false;

                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
                return 0;
            }
            catch (CGateException error)
            {
                return (int)error.ErrCode;
            }
        }

        #endregion

        #region 6 Portfolio, Position

        public void GetPortfolios()
        {
            // ignore
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        /// <summary>
		/// portfolios in the system
        /// портфели в системе
        /// </summary>
        private List<Portfolio> _portfolios;

        /// <summary>
		/// process the portfolio table here
        /// здесь обрабатывается таблица портфелей
        /// </summary>
        private int ClientMessage_Portfolio(Connection conn, Listener listener, Message msg)
        {
            try
            {
                switch (msg.Type)
                {
                    case MessageType.MsgStreamData:
                        {
                            StreamDataMessage replmsg = (StreamDataMessage)msg;

                            if (replmsg.MsgName == "part")
                            {
                                try
                                {
                                    if (_portfolios == null)
                                    {
                                        _portfolios = new List<Portfolio>();
                                    }

                                    string clientCode = replmsg["client_code"].asString();

                                    Portfolio portfolio = _portfolios.Find(portfolio1 => portfolio1.Number == clientCode);

                                    if (portfolio == null)
                                    {
                                        portfolio = new Portfolio();
                                        portfolio.Number = clientCode;
                                        portfolio.ValueCurrent = 1;
                                        portfolio.ValueBegin = 1;
                                        _portfolios.Add(portfolio);
                                    }

                                    portfolio.ValueBegin = Convert.ToDecimal(replmsg["money_old"].asDecimal());
                                    portfolio.ValueCurrent = Convert.ToDecimal(replmsg["money_amount"].asDecimal());
                                    portfolio.ValueBlocked = Convert.ToDecimal(replmsg["money_blocked"].asDecimal());

                                    if (PortfolioEvent != null)
                                    {
                                        PortfolioEvent(_portfolios);
                                    }
                                }
                                catch (Exception error)
                                {
                                    SendLogMessage(error.ToString(), LogMessageType.Error);
                                }
                            }
                            break;
                        }
                    case MessageType.MsgP2ReplReplState:
                        {
                            _listenerPortfolioReplState = ((P2ReplStateMessage)msg).ReplState;
                            _listenerPortfolioNeedToReconnect = true;
                            break;
                        }
                    default:
                        {
                            break;
                        }

                }
                return 0;
            }
            catch (CGateException error)
            {
                return (int)error.ErrCode;
            }
        }

        /// <summary>
		/// here process the table of positions by portfolios |
        /// здесь обрабатывается таблица позиций по портфелям
        /// </summary>
        private int ClientMessage_Position(
            Connection conn, Listener listener, Message msg)
        {
            try
            {
                switch (msg.Type)
                {
                    case MessageType.MsgStreamData:
                        {
                            StreamDataMessage replmsg = (StreamDataMessage)msg;
                            if (replmsg.MsgName == "position")
                            {
                                try
                                {
                                    PositionOnBoard positionOnBoard = new PositionOnBoard();

                                    positionOnBoard.SecurityNameCode = replmsg["isin_id"].asInt().ToString();
                                    positionOnBoard.PortfolioName = replmsg["client_code"].asString();
                                    positionOnBoard.ValueBegin = replmsg["xopen_qty"].asInt();
                                    positionOnBoard.ValueCurrent = replmsg["xpos"].asInt();
                                    positionOnBoard.ValueBlocked = positionOnBoard.ValueCurrent;

                                    PlazaControllerOnUpdatePosition(positionOnBoard);
                                }
                                catch (Exception error)
                                {
                                    SendLogMessage(error.ToString(), LogMessageType.Error);
                                }
                            }
                            break;
                        }
                    case MessageType.MsgP2ReplReplState:
                        {
                            _listenerPositionReplState = ((P2ReplStateMessage)msg).ReplState;
                            _listenerPositionNeedToReconnect = true;
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
                return 0;
            }
            catch (CGateException error)
            {
                return (int)error.ErrCode;
            }
        }

        private object _lockerUpdatePosition = new object();

        private void PlazaControllerOnUpdatePosition(PositionOnBoard positionOnBoard)
        {
            lock (_lockerUpdatePosition)
            {
                try
                {
                    Security security = null;

                    security = _securities.Find(security1 =>
                        security1.NameId == positionOnBoard.SecurityNameCode
                        || security1.Name == positionOnBoard.SecurityNameCode);

                    if (security == null)
                    {
                        PositionOnBoardSander sender = new PositionOnBoardSander();
                        sender.PositionOnBoard = positionOnBoard;
                        sender.TimeSendPortfolio += PlazaControllerOnUpdatePosition;

                        Thread worker = new Thread(sender.Go);
                        worker.CurrentCulture = new CultureInfo("ru-RU");
                        worker.IsBackground = true;
                        worker.Start();
                        return;
                    }

                    positionOnBoard.SecurityNameCode = security.Name;

                    Portfolio myPortfolio = null;

                    if (_portfolios != null)
                    {
                        myPortfolio = _portfolios.Find(portfolio => portfolio.Number == positionOnBoard.PortfolioName);
                    }

                    if (myPortfolio == null)
                    {
                        PositionOnBoardSander sender = new PositionOnBoardSander();
                        sender.PositionOnBoard = positionOnBoard;
                        sender.TimeSendPortfolio += PlazaControllerOnUpdatePosition;

                        Thread worker = new Thread(sender.Go);
                        worker.CurrentCulture = new CultureInfo("ru-RU");
                        worker.IsBackground = true;
                        worker.Start();
                        return;
                    }

                    myPortfolio.SetNewPosition(positionOnBoard);

                    if (PortfolioEvent != null)
                    {
                        PortfolioEvent(_portfolios);
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        #endregion

        #region 7 Security

        public void GetSecurities() { }

        public event Action<List<Security>> SecurityEvent;

        private List<Security> _securities = new List<Security>();

        /// <summary>
		/// handles the tool table here
        /// здесь обрабатывается таблица инструментов 
        /// </summary>
        private int ClientMessage_Board(Connection conn, Listener listener, Message msg)
        {
            try
            {
                switch (msg.Type)
                {
                    case MessageType.MsgStreamData:
                        {
                            StreamDataMessage replmsg = (StreamDataMessage)msg;
                            if (replmsg.MsgName == "fut_sess_contents")
                            {
                                try
                                {
                                    Security security = new Security();
                                    security.NameFull = replmsg["name"].asString();
                                    security.Name = replmsg["isin"].asString();
                                    security.NameId = replmsg["isin_id"].asInt().ToString();
                                    security.State = SecurityStateType.Activ;
                                    security.VolumeStep = 1;
                                    security.Exchange = "MOEX";

                                    security.PriceStep = Convert.ToDecimal(replmsg["min_step"].asDecimal());
                                    security.PriceStepCost = Convert.ToDecimal(replmsg["step_price"].asDecimal());

                                    security.PriceLimitLow = Convert.ToDecimal(replmsg["settlement_price"].asDecimal()) - Convert.ToDecimal(replmsg["limit_down"].asDecimal());
                                    security.PriceLimitHigh = Convert.ToDecimal(replmsg["settlement_price"].asDecimal()) + Convert.ToDecimal(replmsg["limit_up"].asDecimal());

                                    security.Lot = replmsg["lot_volume"].asInt(); //Convert.ToDecimal(replmsg["lot_volume"].asInt());

                                    security.SecurityType = SecurityType.Futures;

                                    security.NameClass = "FORTS";

                                    if (_securities.Find(s => s.NameId.Equals(security.NameId)) == null)
                                    {
                                        _securities.Add(security);
                                    }

                                    if (SecurityEvent != null)
                                    {
                                        SecurityEvent(_securities);
                                    }

                                }
                                catch (Exception error)
                                {
                                    SendLogMessage(error.ToString(), LogMessageType.Error);
                                }
                            }
                            break;
                        }
                    case MessageType.MsgP2ReplReplState:
                        {
                            _listenerInfoReplState = ((P2ReplStateMessage)msg).ReplState;
                            _listenerInfoNeedToReconnect = true;
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
                return 0;
            }
            catch (CGateException error)
            {
                return (int)error.ErrCode;
            }
        }

        #endregion

        #region 8 Security subscribe, Depth processing

        public event Action<MarketDepth> MarketDepthEvent;

        /// <summary>
		/// instruments for which to collect depths |
        /// инструменты по которым надо собирать стаканы
        /// </summary>
        private List<Security> _securitiesDepth;

        public void Subscribe(Security security)
        {
            if (_securitiesDepth == null)
            {
                _securitiesDepth = new List<Security>();
            }

            if (_securitiesDepth.Find(security1 => security1.Name == security.Name) == null)
            {
                _securitiesDepth.Add(security);
            }
        }

        /// <summary>
		/// depth is processed here
        /// здесь обрабатывается стакан
        /// </summary>
        /// 
        private int ClientMessage_OrderLog(
            Connection conn, Listener listener, Message msg)
        {
            try
            {
                switch (msg.Type)
                {
                    case MessageType.MsgStreamData:
                        {
                            StreamDataMessage replmsg = (StreamDataMessage)msg;
                            if (replmsg.MsgName == "orders_aggr")
                            {
                                try
                                {
                                    if (_securitiesDepth == null)
                                    {
                                        return 0;
                                    }

                                    string idSecurity = replmsg["isin_id"].asInt().ToString();

                                    Security mySecurity = _securitiesDepth.Find(security => security.NameId == idSecurity);

                                    if (mySecurity == null)
                                    {
                                        return 0;
                                    }

                                    MarketDepth myDepth = SetNewMessage(replmsg, mySecurity);

                                    if (myDepth == null)
                                    {
                                        return 0;
                                    }

                                    if (MarketDepthEvent != null)
                                    {
                                        MarketDepthEvent(myDepth);
                                    }
                                }
                                catch (Exception error)
                                {
                                    SendLogMessage(error.ToString(), LogMessageType.Error);
                                }
                            }
                            break;
                        }
                    case MessageType.MsgP2ReplClearDeleted:
                        {
                            // it is necessary to remove levels from revision from depths | надо удалить из стаканов уровни с ревизией младше указанного
                            P2ReplClearDeletedMessage cdMessage = (P2ReplClearDeletedMessage)msg;
                            ClearFromRevision(cdMessage);
                            break;
                        }
                    case MessageType.MsgOpen:
                        {
                            ClearAll();
                            break;
                        }
                    case MessageType.MsgClose:
                        {
                            ClearAll();
                            break;
                        }
                    case MessageType.MsgP2ReplLifeNum:
                        {
                            ClearAll();
                            break;
                        }
                    case MessageType.MsgP2ReplReplState:
                        {
                            _listenerMarketDepthNeedToReconnect = true;
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
                return 0;
            }
            catch (CGateException error)
            {
                return (int)error.ErrCode;
            }
        }

        /// <summary>
        /// download a new line with changing in the depth | 
        /// загрузить новую строку с изменением в станане
        /// </summary>
        /// <param name="replmsg">message line | строка сообщения</param>
        /// <param name="security">message instrument | инструмент которому принадлежит сообщение</param>
        /// <returns>returns depth with changings | возвращаем стакан в который было внесено изменение</returns>
        private MarketDepth SetNewMessage(StreamDataMessage replmsg, Security security)
        {
            long replAct = replmsg["replAct"].asLong(); // признак того, что запись удалена. Если replAct не нулевой — запись удалена. Если replAct = 0 — запись активна

            int volume = replmsg["volume"].asInt();

            MarketDepth depth = null;

            if (volume != 0 && replAct == 0 && replmsg.MsgName == "orders_aggr")
            {
                depth = Insert(replmsg, security);

                return depth;
            }
            else
            {
                depth = Delete(replmsg, security);

                return depth;
            }
        }

        private List<RevisionInfo> _lastRevisions;

        private List<MarketDepth> _marketDepths;

        private MarketDepth Insert(StreamDataMessage replmsg, Security security)
        {
            // process revision | обрабатываем ревизию

            // create new | создаём новую
            RevisionInfo revision = new RevisionInfo();
            revision.Price = Convert.ToDecimal(replmsg["price"].asDecimal());
            revision.TableRevision = replmsg["replRev"].asLong(); // уникальный номер изменения в таблице. При любом изменении в таблице (вставке, редактировании, удалении записи) затронутая запись получает значение replRev
            revision.ReplId = replmsg["replID"].asLong(); // уникальный идентификатор записи в таблице. При вставке каждой новой записи, этой записи присваивается уникальный идентификатор
            revision.Security = security.Name;

            if (_lastRevisions == null)
            {
                _lastRevisions = new List<RevisionInfo>();
            }

            // remove at the same price, if any | удаляем по этой же цене, если такая есть
            RevisionInfo revisionInArray = _lastRevisions.Find(info => info.Security == revision.Security && info.ReplId == revision.ReplId);

            if (revisionInArray != null)
            {
                _lastRevisions.Remove(revisionInArray);
            }

            // add new | добавляем новую
            _lastRevisions.Add(revision);

            // create line for depth | создаём строку для стакана
            MarketDepthLevel depthLevel = new MarketDepthLevel();

            int direction = replmsg["dir"].asInt(); // 1 buy 2 sell | 1 покупка 2 продажа

            if (direction == 1)
            {
                depthLevel.Bid = replmsg["volume"].asInt();
            }
            else
            {
                depthLevel.Ask = replmsg["volume"].asInt();
            }

            depthLevel.Price = replmsg["price"].asDouble();
            depthLevel.Id = replmsg["replID"].asLong();

            // take our depth | берём наш стакан
            if (_marketDepths == null)
            {
                _marketDepths = new List<MarketDepth>();
            }

            MarketDepth myDepth = null;
            for (int i = 0; i < _marketDepths.Count; i++)
            {
                if (_marketDepths[i].SecurityNameCode == security.Name)
                {
                    myDepth = _marketDepths[i];
                    break;
                }
            }

            if (myDepth == null)
            {
                myDepth = new MarketDepth();
                myDepth.SecurityNameCode = security.Name;
                _marketDepths.Add(myDepth);
            }

            myDepth.Time = DateTime.Now; //replmsg["moment"].asDateTime();

            // add line in our depth | добавляем строку в наш стакан

            List<MarketDepthLevel> bids = null;

            if (myDepth.Bids != null)
            {
                bids = myDepth.Bids;
            }

            List<MarketDepthLevel> asks = null;

            if (myDepth.Asks != null)
            {
                asks = myDepth.Asks;
            }

            if (direction == 1)
            {
                // buy levels | уровни покупок
                if (bids == null || bids.Count == 0)
                {
                    bids = new List<MarketDepthLevel>();
                    bids.Add(depthLevel);
                }
                else
                {
                    // proccess the situation when this level is removed and replaced with another one | обрабатываем ситуацию, когда текущий уровень удаляется и заменяется другим
                    for (int i = 0; i < bids.Count; i++)
                    {
                        if (bids[i].Id == depthLevel.Id)
                        {
                            bids.Remove(bids[i]);
                        }
                    }

                    bool isInArray = false;
                    for (int i = 0; i < bids.Count; i++)
                    {
                        // proccess the situation when this level is already there | обрабатываем ситуацию когда такой уровень уже есть
                        if (bids[i].Price == depthLevel.Price)
                        {
                            bids[i] = depthLevel;
                            isInArray = true;
                            break;
                        }
                    }

                    if (isInArray == false)
                    {
                        // proccess the situation when this level isn't there | обрабатываем ситуацию когда такого уровня нет
                        List<MarketDepthLevel> asksNew = new List<MarketDepthLevel>();
                        bool isIn = false;

                        for (int i = 0, i2 = 0; i2 < bids.Count + 1; i2++)
                        {
                            if (i == bids.Count && isIn == false || (isIn == false && depthLevel.Price > bids[i].Price))
                            {
                                isIn = true;
                                asksNew.Add(depthLevel);
                            }
                            else
                            {
                                asksNew.Add(bids[i]);
                                i++;
                            }
                        }

                        while (asksNew.Count > 20)
                        {
                            asksNew.Remove(asksNew[asksNew.Count - 1]);
                        }

                        bids = asksNew;
                    }

                    if (asks != null && asks.Count != 0 && asks[0].Price <= bids[0].Price)
                    {
                        while (asks.Count != 0 &&
                               asks[0].Price <= bids[0].Price)
                        {
                            asks.Remove(asks[0]);
                        }

                        myDepth.Asks = asks;
                    }
                }

                myDepth.Bids = bids;
            }

            if (direction == 2)
            {
                // sell levels | уровни продажи
                if (asks == null || asks.Count == 0)
                {
                    asks = new List<MarketDepthLevel>();
                    asks.Add(depthLevel);
                }
                else
                {
                    // proccess the situation when this level is removed and replaced with another one | обрабатываем ситуацию, когда текущий уровень удаляется и заменяется другим
                    for (int i = 0; i < asks.Count; i++)
                    {
                        if (asks[i].Id == depthLevel.Id)
                        {
                            asks.Remove(asks[i]);
                        }
                    }

                    bool isInArray = false;
                    for (int i = 0; i < asks.Count; i++)
                    {
                        // proccess the situation when this level is already there | обрабатываем ситуацию когда такой уровень уже есть
                        if (asks[i].Price == depthLevel.Price)
                        {
                            asks[i] = depthLevel;
                            isInArray = true;
                            break;
                        }
                    }
                    if (isInArray == false)
                    {
                        // proccess the situation when this level isn't there | обрабатываем ситуацию когда такого уровня нет
                        List<MarketDepthLevel> bidsNew = new List<MarketDepthLevel>();
                        bool isIn = false;
                        for (int i = 0, i2 = 0; i2 < asks.Count + 1; i2++)
                        {
                            if (i == asks.Count && isIn == false ||
                                (isIn == false &&
                                 depthLevel.Price < asks[i].Price))
                            {
                                isIn = true;
                                bidsNew.Add(depthLevel);
                            }
                            else
                            {
                                bidsNew.Add(asks[i]);
                                i++;
                            }
                        }

                        while (bidsNew.Count > 20)
                        {
                            bidsNew.Remove(bidsNew[bidsNew.Count - 1]);
                        }

                        asks = bidsNew;
                    }

                    if (bids != null && bids.Count != 0 &&
                        asks[0].Price <= bids[0].Price)
                    {
                        while (bids.Count != 0 && asks[0].Price <= bids[0].Price)
                        {
                            bids.Remove(bids[0]);
                        }

                        myDepth.Bids = bids;
                    }
                }

                myDepth.Asks = asks;
            }

            return myDepth;
        }

        public MarketDepth Delete(StreamDataMessage replmsg, Security security)
        {
            // process revision | обрабатываем ревизию

            if (_lastRevisions == null)
            {
                return null;
            }

            RevisionInfo revision = new RevisionInfo();
            revision.ReplId = replmsg["replID"].asLong();
            revision.Security = security.Name;

            // remove at the same price, if any | удаляем по этой же цене, если такая есть
            RevisionInfo revisionInArray =
                _lastRevisions.Find(info => info.Security == revision.Security && info.ReplId == revision.ReplId);

            if (revisionInArray != null)
            {
                _lastRevisions.Remove(revisionInArray);
            }

            MarketDepth myDepth = _marketDepths.Find(depth => depth.SecurityNameCode == security.Name);

            if (myDepth == null)
            {
                myDepth = new MarketDepth();
                myDepth.SecurityNameCode = security.Name;
                myDepth.Time = replmsg["moment"].asDateTime();
                _marketDepths.Add(myDepth);
                return myDepth;
            }
            myDepth.Time = DateTime.Now; // replmsg["moment"].asDateTime();

            if (revisionInArray == null)
            {
                return myDepth;
            }

            int direction = replmsg["dir"].asInt(); // 1 покупка 2 продажа

            // remove our line from the depth | удаляем нашу строку из стакана

            List<MarketDepthLevel> asks = null;

            if (myDepth.Bids != null)
            {
                asks = myDepth.Bids;
            }

            if (direction == 1)
            {
                // buy levels | уровни покупок
                if (asks == null || asks.Count == 0)
                {
                    return myDepth;
                }
                else
                {
                    for (int i = 0; i < asks.Count; i++)
                    {
                        // proccess the situation when this level is already there | обрабатываем ситуацию когда такой уровень уже есть
                        if (asks[i].Price == Convert.ToDouble(revisionInArray.Price))
                        {
                            asks.Remove(asks[i]);
                            break;
                        }
                    }
                }

                myDepth.Bids = asks;
            }

            if (direction == 2)
            {
                // sell levels | уровни продажи
                List<MarketDepthLevel> bids = null;

                if (myDepth.Asks != null)
                {
                    bids = myDepth.Asks;
                }

                if (bids == null || bids.Count == 0)
                {
                    return myDepth;
                }
                else
                {
                    for (int i = 0; i < bids.Count; i++)
                    {
                        // proccess the situation when this level is already there | обрабатываем ситуацию когда такой уровень уже есть
                        if (bids[i].Price == Convert.ToDouble(revisionInArray.Price))
                        {
                            bids.Remove(bids[i]);
                            break;
                        }
                    }
                }

                myDepth.Asks = bids;
            }

            return myDepth;
        }

        public void ClearFromRevision(P2ReplClearDeletedMessage msg)
        {
            // msg.TableIdx;
            // msg.TableRev;

            for (int i = 0; _lastRevisions != null && i < _lastRevisions.Count; i++)
            {
                if (_lastRevisions[i].TableRevision < msg.TableRev)
                {
                    ClearThisRevision(_lastRevisions[i]);
                }
            }
        }

        private void ClearThisRevision(RevisionInfo info)
        {
            try
            {
                _lastRevisions.Remove(info);

                MarketDepth myDepth = _marketDepths.Find(depth => depth.SecurityNameCode == info.Security);

                if (myDepth == null)
                {
                    return;
                }

                if (myDepth.Bids != null && myDepth.Bids.Count != 0)
                {
                    List<MarketDepthLevel> ask = myDepth.Bids;

                    for (int i = 0; i < ask.Count; i++)
                    {
                        if (ask[i].Price == Convert.ToDouble(info.Price))
                        {
                            ask.Remove(ask[i]);
                            myDepth.Bids = ask;
                        }
                    }
                }

                if (myDepth.Asks != null && myDepth.Asks.Count == 0)
                {
                    List<MarketDepthLevel> bid = myDepth.Asks;

                    for (int i = 0; i < bid.Count; i++)
                    {
                        if (bid[i].Price == Convert.ToDouble(info.Price))
                        {
                            bid.Remove(bid[i]);
                            myDepth.Asks = bid;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        public void ClearAll()
        {
            _marketDepths = new List<MarketDepth>();
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        #endregion

        #region 9 OrderEvent, TradeEvent

        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
		/// here my trades and orders table is processed | 
        /// здесь обрабатывается таблица мои сделки и ордера
        /// </summary>
        public int ClientMessage_OrderAndMyDeal(Connection conn, Listener listener, Message msg)
        {
            try
            {
                switch (msg.Type)
                {
                    case MessageType.MsgStreamData:
                        {
                            StreamDataMessage replmsg = (StreamDataMessage)msg;
                            if (replmsg.MsgName == "user_deal")
                            {
                                try
                                {
                                    MyTrade trade = new MyTrade();
                                    trade.Price = Convert.ToDecimal(replmsg["price"].asDecimal());
                                    trade.NumberTrade = replmsg["id_deal"].asLong().ToString();
                                    trade.SecurityNameCode = replmsg["isin_id"].asInt().ToString();

                                    if (_securities != null && _securities.Count != 0)
                                    {
                                        Security security = _securities.Find(security1 => security1.NameId == trade.SecurityNameCode);

                                        trade.SecurityNameCode = security.Name;
                                    }

                                    trade.Time = replmsg["moment"].asDateTime();
                                    trade.Volume = replmsg["xamount"].asInt();

                                    string portfolioBuy = replmsg["code_buy"].asString();

                                    string portfolioSell = replmsg["code_sell"].asString();

                                    if (!string.IsNullOrWhiteSpace(portfolioBuy))
                                    {
                                        if (_portfolios != null && _portfolios.Find(portfolio => portfolio.Number == portfolioBuy) != null)
                                        {
                                            trade.NumberOrderParent = replmsg["public_order_id_buy"].asLong().ToString();
                                            trade.Side = Side.Buy;
                                        }
                                    }
                                    else if (!string.IsNullOrWhiteSpace(portfolioSell))
                                    {
                                        if (_portfolios != null && _portfolios.Find(portfolio => portfolio.Number == portfolioSell) != null)
                                        {
                                            trade.NumberOrderParent = replmsg["public_order_id_sell"].asLong().ToString();
                                            trade.Side = Side.Sell;
                                        }
                                    }

                                    SendLogMessage($"Пришел трейд. NumberTrade: {trade.NumberTrade}, Sec: {trade.SecurityNameCode}, Price: {trade.Price}, Time: {trade.Time}, Volume: {trade.Volume}, " +
                                        $"Order: {trade.NumberOrderParent}", LogMessageType.System);

                                    if (MyTradeEvent != null)
                                    {
                                        MyTradeEvent(trade);
                                    }
                                }
                                catch (Exception error)
                                {
                                    SendLogMessage(error.Message, LogMessageType.Error);
                                }
                            }
                            else if (replmsg.MsgName == "orders_log")
                            {
                                try
                                {
                                    Order order = new Order();

                                    order.NumberUser = replmsg["ext_id"].asInt();

                                    lock (_currentOrdersLosker)
                                    {
                                        bool inArray = false;
                                        for (int i = 0; i < _currentOrders.Count; i++)
                                        {
                                            if (_currentOrders[i].NumberUser == order.NumberUser)
                                            {
                                                order = _currentOrders[i];
                                                inArray = true;
                                                break;
                                            }
                                        }

                                        if (inArray == false)
                                        {
                                            _currentOrders.Add(order);
                                        }
                                    }

                                    long xstatus = replmsg["xstatus"].asLong();

                                    if (((long)BitMask.Auction & xstatus) != 0) // признак лимитной заявки( Котировочная заявка (Day))
                                    {
                                        order.TypeOrder = OrderPriceType.Limit;
                                    }
                                    else if (((long)BitMask.Opposite & xstatus) != 0) // признак маркет заявки ( Встречная заявка (IOC))
                                    {
                                        order.TypeOrder = OrderPriceType.Market;
                                    }

                                    order.NumberMarket = replmsg["public_order_id"].asLong().ToString();

                                    if (order.Volume == 0)
                                        order.Volume = (decimal)replmsg["public_amount"].asLong();

                                    long remainingVolume = replmsg["public_amount_rest"].asLong(); // это у нас оставшееся в заявке
                                    long currentVolume = replmsg["public_amount"].asLong();


                                    order.Price = Convert.ToDecimal(replmsg["price"].asDecimal());
                                    order.PortfolioNumber = replmsg["client_code"].asString();
                                    string securityNameCode = replmsg["isin_id"].asInt().ToString();

                                    if (_securities != null && _securities.Count != 0)
                                    {
                                        Security security = _securities.Find(sec => sec.NameId == securityNameCode);

                                        order.SecurityNameCode = security.Name;
                                    }

                                    order.TimeCallBack = replmsg["moment"].asDateTime();
                                    order.ServerType = ServerType.Plaza;

                                    int action = replmsg["public_action"].asByte();

                                    if (action == 0)
                                    {
                                        lock (_changePriceOrdersArrayLocker)
                                        {
                                            DateTime now = DateTime.Now;
                                            for (int i = 0; i < _changePriceOrders.Count; i++)
                                            {
                                                if (_changePriceOrders[i].TimeChangePriceOrder.AddSeconds(2) < now)
                                                {
                                                    _changePriceOrders.RemoveAt(i);
                                                    i--;
                                                    continue;
                                                }

                                                if (_changePriceOrders[i].NumberMarket == order.NumberMarket)
                                                {
                                                    return 0;
                                                }
                                            }
                                        }

                                        order.State = OrderStateType.Cancel;
                                        order.TimeCancel = order.TimeCallBack;
                                    }
                                    else if (action == 1)
                                    {
                                        order.VolumeExecute = order.Volume - remainingVolume;
                                        order.State = OrderStateType.Active;
                                    }
                                    else if (action == 2)
                                    {
                                        order.VolumeExecute = order.Volume - remainingVolume;

                                        if (order.Volume != order.VolumeExecute)
                                        {
                                            order.State = OrderStateType.Partial;
                                        }
                                        else
                                        {
                                            order.State = OrderStateType.Done;

                                            lock (_currentOrdersLosker)
                                            {
                                                for (int i = _currentOrders.Count - 1; i >= 0; i--)
                                                {
                                                    if (_currentOrders[i].NumberUser == order.NumberUser)
                                                    {
                                                        _currentOrders.RemoveAt(i);
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    byte dir = replmsg["dir"].asByte();

                                    if (dir == 1)
                                    {
                                        order.Side = Side.Buy;
                                    }
                                    else if (dir == 2)
                                    {
                                        order.Side = Side.Sell;
                                    }

                                    SendLogMessage($"Пришел ордер, orders_log. Number: {order.NumberMarket}, User: {order.NumberUser}, Volume: {order.Volume}, Execute: {order.VolumeExecute}, Price: {order.Price}, " +
                                        $"State: {order.State}, Time: {order.TimeCallBack}", LogMessageType.Error);

                                    if (MyOrderEvent != null)
                                    {
                                        MyOrderEvent(order);
                                    }
                                }
                                catch (Exception error)
                                {
                                    SendLogMessage(error.Message, LogMessageType.Error);
                                }
                            }
                            break;
                        }
                    case MessageType.MsgP2ReplReplState:
                        {
                            _listenerOrderAndMyDealReplState = ((P2ReplStateMessage)msg).ReplState;
                            _listenerOrderAndMyDealNeedToReconnect = true;
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
                return 0;
            }
            catch (CGateException error)
            {
                return (int)error.ErrCode;
            }
        }

        /// <summary>
		/// here we receive information from the server about the submission of applications | 
        /// здесь получаем информацию от сервера о выставлении заявок
        /// </summary>
        public int ClientMessage_OrderSenderMirror(Connection conn, Listener listener, Message msg)
        {
            try
            {
                switch (msg.Type)
                {
                    case MessageType.MsgData:
                        {
                            try
                            {
                                DataMessage msgData = (DataMessage)msg;

                                if (msgData.MsgId == 179)
                                {
                                    // received response to place order | пришёл отклик на выставление заявки

                                    int code = msgData["code"].asInt();

                                    Order order = _ordersToExecute.Dequeue();

                                    if (code == 0)
                                    {
                                        lock (_currentOrdersLosker)
                                        {
                                            _currentOrders.Add(order);
                                        }

                                        order.State = OrderStateType.Active;
                                        order.NumberMarket = msgData["order_id"].asLong().ToString();
                                    }
                                    else
                                    {
                                        order.State = OrderStateType.Fail;

                                        SendLogMessage($"Order fail. Message - {msgData["message"].asString()}", LogMessageType.System);
                                    }

                                    SendLogMessage($"Пришел ордер, Msg179. Number: {order.NumberMarket}, User: {order.NumberUser}, Volume: {order.Volume}, Execute: {order.VolumeExecute}, Price: {order.Price}, " +
                                        $"State: {order.State}, Time: {order.TimeCallBack}", LogMessageType.Error);

                                    if (MyOrderEvent != null)
                                    {
                                        MyOrderEvent(order);
                                    }
                                }
                                else if (msgData.MsgId == 177)
                                {
                                    // received response to cancel order | пришёл отклик на удаление заявки

                                    int code = msgData["code"].asInt();

                                    Order order = new Order();

                                    if (code == 0)
                                    {
                                        order = _ordersToCansel.Dequeue();

                                        lock (_currentOrdersLosker)
                                        {
                                            for (int i = 0; i < _currentOrders.Count; i++)
                                            {
                                                if (_currentOrders[i].NumberUser == order.NumberUser)
                                                {
                                                    order = _currentOrders[i];
                                                    break;
                                                }
                                            }
                                        }

                                        order.State = OrderStateType.Cancel;
                                    }
                                    else if (code == 14)
                                    {
                                        order = _ordersToCansel.Dequeue();

                                        lock (_currentOrdersLosker)
                                        {
                                            for (int i = 0; i < _currentOrders.Count; i++)
                                            {
                                                if (_currentOrders[i].NumberUser == order.NumberUser)
                                                {
                                                    order = _currentOrders[i];
                                                    break;
                                                }
                                            }
                                        }

                                        order.State = OrderStateType.Cancel;

                                        SendLogMessage($"The warrant has already been cancelled. Message - {msgData["message"].asString()}", LogMessageType.System);
                                    }
                                    else
                                    {
                                        order = _ordersToCansel.Dequeue();

                                        lock (_currentOrdersLosker)
                                        {
                                            for (int i = 0; i < _currentOrders.Count; i++)
                                            {
                                                if (_currentOrders[i].NumberUser == order.NumberUser)
                                                {
                                                    order = _currentOrders[i];
                                                    break;
                                                }
                                            }
                                        }

                                        order.State = OrderStateType.Fail;

                                        SendLogMessage($"Order fail. Message - {msgData["message"].asString()}", LogMessageType.System);
                                    }

                                    SendLogMessage($"Пришел ордер, Msg177. Number: {order.NumberMarket}, User: {order.NumberUser}, Volume: {order.Volume}, Execute: {order.VolumeExecute}, Price: {order.Price}, " +
                                        $"State: {order.State}, Time: {order.TimeCallBack}", LogMessageType.Error);

                                    if (MyOrderEvent != null)
                                    {
                                        MyOrderEvent(order);
                                    }
                                }
                                else if (msgData.MsgId == 176)
                                {
                                    Order order = new Order();

                                    // received response to change price order | пришёл отклик на изменение цены заявки

                                    int code = msgData["code"].asInt();

                                    if (code != 0)
                                    {
                                        order = _ordersToChange.Dequeue();

                                        lock (_currentOrdersLosker)
                                        {
                                            for (int i = 0; i < _currentOrders.Count; i++)
                                            {
                                                if (_currentOrders[i].NumberUser == order.NumberUser)
                                                {
                                                    order = _currentOrders[i];
                                                    break;
                                                }
                                            }
                                        }

                                        order.State = OrderStateType.Fail;

                                        SendLogMessage($"Order fail. Message - {msgData["message"].asString()}", LogMessageType.System);

                                        return 0;
                                    }
                                    else
                                    {
                                        order = _ordersToChange.Dequeue();

                                        lock (_currentOrdersLosker)
                                        {
                                            for (int i = 0; i < _currentOrders.Count; i++)
                                            {
                                                if (_currentOrders[i].NumberUser == order.NumberUser)
                                                {
                                                    order = _currentOrders[i];
                                                    break;
                                                }
                                            }
                                        }

                                        order.NumberMarket = msgData["order_id1"].asLong().ToString();
                                        order.State = OrderStateType.Active;

                                        SendLogMessage($"Move order. NumberUser - {order.NumberUser}. Message - {msgData["message"].asString()}", LogMessageType.System);
                                    }

                                    SendLogMessage($"Пришел ордер, Msg176. Number: {order.NumberMarket}, User: {order.NumberUser}, Volume: {order.Volume}, Execute: {order.VolumeExecute}, Price: {order.Price}, " +
                                        $"State: {order.State}, Time: {order.TimeCallBack}", LogMessageType.Error);

                                    if (MyOrderEvent != null)
                                    {
                                        MyOrderEvent(order);
                                    }
                                }
                                if (msgData.MsgId == 99)
                                {
                                    SendLogMessage($"{OsLocalization.Market.Message81}. " +
                                        $"The restriction will be lifted after {msgData["penalty_remain"].asInt()} milliseconds", LogMessageType.System);

                                    lock (_penaltyRemainLock)
                                    {
                                        _penaltyRemain = msgData["penalty_remain"].asInt();
                                    }
                                    return 0;
                                }
                                else if (msgData.MsgId == 100)
                                {
                                    SendLogMessage(msgData.ToString(), LogMessageType.Error);
                                }
                            }
                            catch (Exception error)
                            {
                                SendLogMessage(error.ToString(), LogMessageType.Error);
                            }

                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
                return 0;
            }
            catch (CGateException error)
            {
                return (int)error.ErrCode;
            }
        }

        /// <summary>
		/// here process the table of user orders |
        /// здесь обрабатывается таблица заявок пользователя
        /// </summary>
        private int ClientMessage_UserOrderBook(
            Connection conn, Listener listener, Message msg)
        {
            try
            {
                switch (msg.Type)
                {
                    case MessageType.MsgStreamData:
                        {
                            StreamDataMessage replmsg = (StreamDataMessage)msg;
                            if (replmsg.MsgName == "orders")
                            {
                                try
                                {
                                    Order order = new Order();

                                    order.NumberUser = replmsg["ext_id"].asInt();

                                    lock (_currentOrdersLosker)
                                    {
                                        bool inArray = false;
                                        for (int i = 0; i < _currentOrders.Count; i++)
                                        {
                                            if (_currentOrders[i].NumberUser == order.NumberUser)
                                            {
                                                order = _currentOrders[i];
                                                inArray = true;
                                                break;
                                            }
                                        }

                                        if (inArray == false)
                                        {
                                            _currentOrders.Add(order);
                                        }
                                    }

                                    order.NumberMarket = replmsg["public_order_id"].asLong().ToString();

                                    if (order.Volume == 0)
                                        order.Volume = (decimal)replmsg["public_amount"].asLong();

                                    long remainingVolume = replmsg["public_amount_rest"].asLong(); // это у нас оставшееся в заявке

                                    order.Price = Convert.ToDecimal(replmsg["price"].asDecimal());
                                    order.PortfolioNumber = replmsg["client_code"].asString();
                                    string securityNameCode = replmsg["isin_id"].asInt().ToString();

                                    if (_securities != null)
                                    {
                                        Security security = _securities.Find(sec => sec.NameId == securityNameCode);

                                        if (security != null)
                                            order.SecurityNameCode = security.Name;
                                    }

                                    long xstatus = replmsg["xstatus"].asLong();

                                    if (((long)BitMask.Auction & xstatus) != 0) // признак лимитной заявки( Котировочная заявка (Day))
                                    {
                                        order.TypeOrder = OrderPriceType.Limit;
                                    }
                                    else if (((long)BitMask.Opposite & xstatus) != 0) // признак маркет заявки ( Встречная заявка (IOC))
                                    {
                                        order.TypeOrder = OrderPriceType.Market;
                                    }

                                    order.TimeCallBack = replmsg["moment"].asDateTime();
                                    order.ServerType = ServerType.Plaza;

                                    byte action = replmsg["public_action"].asByte();

                                    if (action == 0)
                                    {
                                        order.VolumeExecute = order.VolumeExecute;
                                        order.State = OrderStateType.Cancel;
                                        order.TimeCancel = order.TimeCallBack;
                                    }
                                    else if (action == 1)
                                    {
                                        order.VolumeExecute = order.Volume - remainingVolume;
                                        order.State = OrderStateType.Active;
                                    }
                                    else if (action == 2)
                                    {
                                        order.VolumeExecute = order.Volume - remainingVolume;

                                        if (order.Volume != order.VolumeExecute)
                                        {
                                            order.State = OrderStateType.Partial;
                                        }
                                        else
                                        {
                                            order.State = OrderStateType.Done;

                                            lock (_currentOrdersLosker)
                                            {
                                                for (int i = _currentOrders.Count; i >= 0; i--)
                                                {
                                                    if (_currentOrders[i].NumberUser == order.NumberUser)
                                                    {
                                                        _currentOrders.RemoveAt(i);
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    byte dir = replmsg["dir"].asByte();

                                    if (dir == 1)
                                    {
                                        order.Side = Side.Buy;
                                    }
                                    else if (dir == 2)
                                    {
                                        order.Side = Side.Sell;
                                    }

                                    SendLogMessage($"Пришел ордер, orders. Number: {order.NumberMarket}, User: {order.NumberUser}, Volume: {order.Volume}, Execute: {order.VolumeExecute}, Price: {order.Price}, " +
                                        $"State: {order.State}, Time: {order.TimeCallBack}", LogMessageType.Error);

                                    if (MyOrderEvent != null)
                                    {
                                        MyOrderEvent(order);
                                    }
                                }
                                catch (Exception error)
                                {
                                    SendLogMessage(error.ToString(), LogMessageType.Error);
                                }
                            }
                            break;
                        }
                    case MessageType.MsgP2ReplOnline:
                        {
                            break;
                        }
                    case MessageType.MsgClose:
                        {
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
                return 0;
            }
            catch (CGateException error)
            {
                return (int)error.ErrCode;
            }
        }

        #endregion

        #region 10 Trade

        public event Action<Order> MyOrderEvent;

        public void CancelAllOrders() { }

        public void CancelAllOrdersToSecurity(Security security) { }

        public bool CancelOrder(Order order)
        {
            lock (_publisherLocker)
            {
                _rateGate.WaitToProceed();

                try
                {
                    if (_publisher == null)
                    {
                        return false;
                    }

                    if (_penaltyRemain != 0)
                    {
                        lock (_penaltyRemainLock)
                        {
                            Thread.Sleep(_penaltyRemain);
                            _penaltyRemain = 0;
                        }
                    }

                    Message sendMessage = _publisher.NewMessage(MessageKeyType.KeyName, "DelOrder");

                    string code = order.PortfolioNumber;

                    string brockerCode = code[0].ToString() + code[1].ToString() + code[2].ToString() + code[3].ToString();

                    DataMessage smsg = (DataMessage)sendMessage;
                    smsg.UserId = (uint)order.NumberUser;
                    smsg["broker_code"].set(brockerCode);
                    smsg["order_id"].set(Convert.ToInt64(order.NumberMarket));

                    SendLogMessage($"Отменен ордер. Price {order.Price.ToString().Replace(',', '.')}, User: {order.NumberUser}, Number: {order.NumberMarket}, Volume: {Convert.ToInt32(order.Volume)} ", LogMessageType.System);

                    _ordersToCansel.Enqueue(order);

                    _publisher.Post(sendMessage, PublishFlag.NeedReply);
                    sendMessage.Dispose();
                    return true;
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
            return false;
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            lock (_publisherLocker)
            {
                _rateGate.WaitToProceed();

                try
                {
                    if (_publisher == null)
                    {
                        return;
                    }

                    if (_penaltyRemain != 0)
                    {
                        lock (_penaltyRemainLock)
                        {
                            Thread.Sleep(_penaltyRemain);
                            _penaltyRemain = 0;
                        }
                    }

                    Message sendMessage = _publisher.NewMessage(MessageKeyType.KeyName, "MoveOrder");

                    string code = order.PortfolioNumber;

                    string brockerCode = code[0].ToString() + code[1].ToString() + code[2].ToString() + code[3].ToString();
                    string clientCode = code[4].ToString() + code[5].ToString() + code[6].ToString();

                    DataMessage smsg = (DataMessage)sendMessage;
                    smsg.UserId = (uint)order.NumberUser;
                    smsg["broker_code"].set(brockerCode);

                    int isinId = GetIsinId(order.SecurityNameCode);

                    if (isinId == -1)
                    {
                        return;
                    }

                    smsg["isin_id"].set(isinId);
                    smsg["order_id1"].set(Convert.ToInt64(order.NumberMarket));
                    smsg["client_code"].set(clientCode);
                    smsg["ext_id1"].set(order.NumberUser);
                    smsg["price1"].set(newPrice.ToString().Replace(',', '.'));

                    order.Price = newPrice;

                    _ordersToChange.Enqueue(order);

                    PlazaChangePriceOrderEntity changePriceOrder = new PlazaChangePriceOrderEntity();
                    changePriceOrder.NumberMarket = order.NumberMarket;
                    changePriceOrder.NumberUser = order.NumberUser;
                    changePriceOrder.TimeChangePriceOrder = DateTime.Now;

                    lock (_changePriceOrdersArrayLocker)
                    {
                        _changePriceOrders.Add(changePriceOrder);
                    }

                    _publisher.Post(sendMessage, PublishFlag.NeedReply);
                    sendMessage.Dispose();
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        private object _changePriceOrdersArrayLocker = new object();

        /// <summary>
        /// contains a list of change orders |
        /// содержит список ордеров на изменение
        /// </summary>
        private List<PlazaChangePriceOrderEntity> _changePriceOrders = new List<PlazaChangePriceOrderEntity>();

        public void SendOrder(Order order)
        {
            lock (_publisherLocker)
            {
                _rateGate.WaitToProceed();

                try
                {
                    if (_publisher == null)
                    {
                        return;
                    }

                    if (_penaltyRemain != 0)
                    {
                        lock (_penaltyRemainLock)
                        {
                            Thread.Sleep(_penaltyRemain);
                            _penaltyRemain = 0;
                        }
                    }

                    Message sendMessage = _publisher.NewMessage(MessageKeyType.KeyName, "AddOrder");

                    int dir = 0;

                    if (order.Side == Side.Buy)
                    {
                        dir = 1;
                    }
                    else
                    {
                        dir = 2;
                    }

                    string code = order.PortfolioNumber;

                    string brockerCode = code[0].ToString() + code[1].ToString() + code[2].ToString() +
                                         code[3].ToString();
                    string clientCode = code[4].ToString() + code[5].ToString() + code[6].ToString();

                    DataMessage smsg = (DataMessage)sendMessage;
                    smsg.UserId = (uint)order.NumberUser;
                    smsg["broker_code"].set(brockerCode);

                    int isinId = GetIsinId(order.SecurityNameCode);

                    if (isinId == -1)
                    {
                        return;
                    }

                    if (order.TypeOrder == OrderPriceType.Market)
                    {
                        smsg["type"].set(2);

                        //для экстренного закрытия позиций
                        if (order.Price == 0)
                        {
                            Security security = null;

                            security = _securities.Find(security1 =>
                                security1.NameId == order.SecurityNameCode
                                || security1.Name == order.SecurityNameCode);

                            if (security != null)
                            {
                                if (dir == 1)
                                {
                                    order.Price = security.PriceLimitHigh;
                                }
                                else
                                {
                                    order.Price = security.PriceLimitLow;
                                }
                            }
                        }
                    }
                    else
                    {
                        smsg["type"].set(1);
                    }

                    smsg["price"].set(order.Price.ToString().Replace(',', '.'));
                    smsg["isin_id"].set(isinId);
                    smsg["client_code"].set(clientCode);
                    smsg["dir"].set(dir);
                    smsg["amount"].set(Convert.ToInt32(order.Volume));
                    smsg["ext_id"].set(order.NumberUser);

                    SendLogMessage($"Выслан ордер. Price {order.Price.ToString().Replace(',', '.')}, User: {order.NumberUser}, Volume: {Convert.ToInt32(order.Volume)} ", LogMessageType.System);

                    _ordersToExecute.Enqueue(order);

                    _publisher.Post(sendMessage, PublishFlag.NeedReply);
                    sendMessage.Dispose();
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            return OrderStateType.None;
        }

        public void GetAllActivOrders()
        {
            // игнорируем. Активные заявки отправляются каждые 2 минуты через поток FORTS_USERORDERBOOK_REPL.
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            return null;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            return null;
        }

        #endregion

        #region 11 CODHeartbeat

        /// <summary>
        /// work place of thread sending signals CODHeartbeat to the system
        /// место работы потока отправляющего сигналы CODHeartbeat в систему
        /// </summary>
        private void HeartBeatSender()
        {
            while (true)
            {
                Thread.Sleep(1000);

                try
                {
                    if (_COD == false)
                    {
                        continue;
                    }

                    if (Connection == null)
                    {
                        continue;
                    }

                    if (_statusNeeded == ServerConnectStatus.Disconnect)
                    {
                        continue;
                    }

                    if (Connection.State != State.Active)
                    {
                        continue;
                    }

                    lock (_publisherLocker)
                    {
                        try
                        {
                            if (_publisher != null && _publisher.State == State.Active)
                            {
                                Message sendMessage = _publisher.NewMessage(MessageKeyType.KeyName, "CODHeartbeat");
                                _publisher.Post(sendMessage, 0);

                                sendMessage.Dispose();
                            }
                        }
                        catch (Exception error)
                        {
                            SendLogMessage(error.ToString(), LogMessageType.System);
                        }
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.System);
                }
            }
        }

        #endregion

        #region 12 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

        #region 13 Helpers

        private int GetIsinId(string isin)
        {

            for (int i = 0; i < _securities.Count; i++)
            {
                if (_securities[i].Name.Equals(isin))
                {
                    return Convert.ToInt32(_securities[i].NameId);
                }
            }

            return -1;
        }

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        #endregion
    }
}
