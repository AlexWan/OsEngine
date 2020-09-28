/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using ru.micexrts.cgate;
using ru.micexrts.cgate.message;

namespace OsEngine.Market.Servers.Plaza.Internal
{
    /// <summary>
	/// class realizing interaction with Router Plaza 2 CGate
    /// класс реализующий взаимодействие с Роутером Плаза 2 CGate
    /// </summary>
    public class PlazaClient
    {
        /// <summary>
		/// constructor
        /// конструктор
        /// </summary>
        public PlazaClient(string key)
        {
            _depthCreator = new PlazaMarketDepthCreator();
            _statusNeeded = ServerConnectStatus.Disconnect;
            Status = ServerConnectStatus.Disconnect;
            _ordersToExecute = new Queue<Order>();
            _ordersToCansel = new Queue<Order>();
            GateOpenString = "ini=Plaza/PlazaStartSettings.ini;key=" + key;
        }

		// connection settings constants
        // константы настроек соединения

        /// <summary>
        /// maximum number of transactions per second for login/максимальное количество транзакций в секунду для логина
        /// taken into account when parsing the queue for placing/cancellation orders/учитывается при разборе очереди на выставление/снятие заявок
        /// if you set the wrong, your deposit will disappear / если выставить не то, Ваш депозит уйдёт в зрительный зал
        /// </summary>
        private const int MaxTransaction = 30; // ALARM !!! Find out what is the unit of performance for the login, or the exchange will select the entire deposit for fines / АХТУНГ!!! Узнай что такое единица производительности для логина, или биржа отберёт ВЕСЬ депозит за штрафы

        /// <summary>
		/// initialization string connection to the router
        /// строка инициализации подключения к Роутеру
        /// </summary>
        private string GateOpenString; // = "ini=Plaza/PlazaStartSettings.ini;key=11111111"; 

        /// <summary>
        /// connector connection string/строка подключения коннектора.
        /// This connection string does not need to be deleted. It should be used during tests on a test server./Эту строку подключения не нужно удалять. Её нужно использовать во время тестов на тестовом сервере
        /// It initializes the TCP connector to the Router, it is slower, but safer./Она инициализирует коннектор к Роутеру по ТСП, это медленне, но безопаснее
        /// With this connection, the connection with the router is automatically disconnected when the connection is abnormally closed./При таком подключении соединение с Роутером автоматически разрывается при нештатном закрытии соединения
        /// </summary>
        //private const string ConnectionOpenString = "p2tcp://127.0.0.1:4001;app_name=osaApplication;name=connectorPrime";

        /// <summary>
        /// connector connection string. Initializes the connection to the router through shared memory./строка подключения коннектора. Инициализирует подключение к Роутеру через разделяемую память
        /// if such a connection is not closed normally, you will have to go to the task manager and wet the P2MQRouter * 32/в случае, если такое соединение закрылось не штатно, придётся идти в диспетчер задач и мочить P2MQRouter*32
        /// from the C drive, launch the Router via start_router.cmd/далее из диска С запускать Роутер через start_router.cmd
        /// </summary>
        private const string ConnectionOpenString = "p2lrpcq://127.0.0.1:4001;app_name=osaApplication;name=connectorPrime";

        /// <summary>
		/// initialization line of the Listner responsible for receiving information about the instruments
        /// строка инициализации Листнера отвечающего за приём информации об инструментах
        /// </summary>
        private const string ListenInfoString = "p2repl://FORTS_FUTINFO_REPL;scheme=|FILE|Plaza/Schemas/fut_info.ini|CustReplScheme";

        /// <summary>
		/// initialization string of the Listner responsible for receiving position
        /// строка инициализации листнера отвечающего за приём позиции
        /// </summary>
        private const string ListenPositionString = "p2repl://FORTS_POS_REPL;scheme=|FILE|Plaza/Schemas/portfolios.ini|CustReplScheme";

        /// <summary>
		/// initialization string of the Listner responsible for getting portfolios
        /// строка инициализации листнера отвечающего за приём портфелей
        /// </summary>
        private const string ListenPortfolioString = "p2repl://FORTS_PART_REPL;scheme=|FILE|Plaza/Schemas/part.ini|CustReplScheme";

        /// <summary>
		/// initialization string of the Listner responsible for receiving ticks
        /// строка инициализации листнера отвечающего за приём тиков
        /// </summary>
        private const string ListenTradeString = "p2repl://FORTS_DEALS_REPL;scheme=|FILE|Plaza/Schemas/deals.ini|CustReplScheme";

        /// <summary>
		/// initialization string of the Listner responsible for receiving my trades and my orders
        /// строка инициализации листнера отвечающего за приём Моих сделок и моих ордеров
        /// </summary>
        private const string ListenOrderAndMyTradesString = "p2repl://FORTS_FUTTRADE_REPL;scheme=|FILE|Plaza/Schemas/fut_trades.ini|CustReplScheme";

        /// <summary>
		/// initialization string of the Listner responsible for receiving depth
        /// строка инициализации для листнера отвечающего за приём среза стакана
        /// </summary>
        private const string ListenMarketDepth = "p2repl://FORTS_FUTAGGR20_REPL;scheme=|FILE|Plaza/Schemas/orders_aggr.ini|CustReplScheme";

        /// <summary>
		/// initialization string for order publisher
        /// строка инициализации для публишера ордеров
        /// </summary>
        private const string PublisherOrdersString =
            "p2mq://FORTS_SRV;category=FORTS_MSG;name=srvlink;timeout=5000;scheme=|FILE|Plaza/Schemas/forts_messages.ini|message";

        /// <summary>
		/// initialization string for transaction tracing list listener
        /// строка инициализации для листнера следящего за реакцией публишера сделок
        /// </summary>
        private const string ListnerOrders = "p2mqreply://;ref=srvlink";

		// server status
        // статус сервера

        private ServerConnectStatus _serverConnectStatus;

        /// <summary>
		/// server status
        /// статус сервера
        /// </summary>
        public ServerConnectStatus Status
        {
            get { return _serverConnectStatus; }
            private set
            {
                if (value == _serverConnectStatus)
                {
                    return;
                }
                _serverConnectStatus = value;

                if (ConnectStatusChangeEvent != null)
                {
                    ConnectStatusChangeEvent(_serverConnectStatus);
                }
            }
        }

        /// <summary>
        /// user-ordered status/статус заказанный пользователем
        /// if it is different from the current server status/если он будет отличаться от текущего статуса сервера
        /// main thread will try to bring them to the same value/основной поток будет пытаться привести их к одному значению
        /// </summary>
        private ServerConnectStatus _statusNeeded;

        /// <summary>
		/// called when the server status changes
        /// вызывается при изменении статуса сервера
        /// </summary>
        public event Action<ServerConnectStatus> ConnectStatusChangeEvent;

		// connection setup and listening
        // установка соединения и слежение за ним

        /// <summary>
		/// object to connect to router
        /// объект подключения к Роутеру
        /// </summary>
        public Connection Connection;

        /// <summary>
		/// thread responsible for connecting to Plaza, monitoring threads and processing incoming data
        /// поток отвечающий за соединение с плазой, следящий за потоками и обрабатывающий входящие данные
        /// </summary>
        private Thread _threadPrime;

        /// <summary>
        /// thread watching the main thread/поток присматривающий за основным потоком
        /// and if the main thread does not respond, more than a minute/и если основной поток не отвечает, больше минуты
        /// reconnects the entire system/переподключает всю систему
        /// </summary>
        private Thread _threadNanny;

        /// <summary>
		/// multi-threaded access locker to Plaza object
        /// объект участвующий в блокировке многопоточного доступа к объектам Плаза
        /// </summary>
        private object _plazaThreadLocker;

        /// <summary>
		/// start Plaza server
        /// запустить сервер Плаза
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public void Start()
        {
            _statusNeeded = ServerConnectStatus.Connect;

            if (_threadPrime != null)
            {
                return;
            }
            try
            {
                _plazaThreadLocker = new object();

                CGate.Open(GateOpenString);
                Connection = new Connection(ConnectionOpenString);
                _statusNeeded = ServerConnectStatus.Connect;

                _threadPrime = new Thread(PrimeWorkerThreadSpace);
                _threadPrime.CurrentCulture = new CultureInfo("ru-RU");
                _threadPrime.IsBackground = true;
                _threadPrime.Start();

                _threadNanny = new Thread(ThreadNannySpace);
                _threadNanny.CurrentCulture = new CultureInfo("ru-RU");
                _threadNanny.IsBackground = true;
                _threadNanny.Start();

                _heartBeatSenderThread = new Thread(HeartBeatSender);
                _heartBeatSenderThread.CurrentCulture = new CultureInfo("ru-RU");
                _heartBeatSenderThread.IsBackground = true;
                _heartBeatSenderThread.Start();

                _threadOrderExecutor = new Thread(ExecutorOrdersThreadArea);
                _threadOrderExecutor.CurrentCulture = new CultureInfo("ru-RU");
                _threadOrderExecutor.IsBackground = true;
                _threadOrderExecutor.Start();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString());
            }
        }

        /// <summary>
		/// stop Plaza server
        /// остановить сервер Плаза
        /// </summary>
        public void Stop()
        {
            Thread worker = new Thread(Dispose);
            worker.CurrentCulture = new CultureInfo("ru-RU");
            worker.IsBackground = true;
            worker.Start();

            Thread.Sleep(2000);
        }

        /// <summary>
		/// clear all objects involved in the connection with Plaza 2
        /// очистить все объекты участвующие в соединение с Плаза 2
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public void Dispose()
        {
            _statusNeeded = ServerConnectStatus.Disconnect;
            Thread.Sleep(1000);

            // turn off thread watching the main thread/отключаем поток следящий за основным потоком
            if (_threadNanny != null)
            {
                try
                {
                    _threadNanny.Name = "deleteThread";
                    //_threadNanny.Abort();
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString());
                }
            }

            _threadNanny = null;

            // turn off the main thread/отключаем основной поток
            if (_threadPrime != null)
            {
                try
                {
                    _threadPrime.Name = "deleteThread";
                    //_threadPrime.Abort();
                    Thread.Sleep(500);
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString());
                }
            }
            _threadPrime = null;


            // turn off the heartbeat thread/ отключаем поток отправляющий хартбиты
            if (_heartBeatSenderThread != null)
            {
                try
                {
                    _heartBeatSenderThread.Name = "deleteThread";
                    //_heartBeatSenderThread.Abort();
                    Thread.Sleep(500);
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString());
                }
            }
            _heartBeatSenderThread = null;

            // turn off thread sending orders from queue/ отключаем поток отправляющий заявки из очереди
            if (_threadOrderExecutor != null)
            {
                try
                {
                    _threadOrderExecutor.Name = "deleteThread";
                    //_threadOrderExecutor.Abort();
                    Thread.Sleep(500);
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString());
                }
            }
            _threadOrderExecutor = null;

            // disconnect / отключаем соединение
            if (Connection != null)
            {
                try
                {
                    Connection.Dispose();
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString());
                }
                Connection = null;
            }
            // turn off listeners / отключаем листнеры

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
                    SendLogMessage(error.ToString());
                }

                _listenerInfoNeadToReconnect = false;
                _listenerInfo = null;
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
                    SendLogMessage(error.ToString());
                }

                _listenerMarketDepthNeadToReconnect = false;
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
                    SendLogMessage(error.ToString());
                }

                _listenerOrderAndMyDealNeadToReload = false;
                _listenerOrderAndMyDeal = null;
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
                    SendLogMessage(error.ToString());
                }
                _listenerPositionNeadToReconnect = false;
                _listenerPosition = null;
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
                    SendLogMessage(error.ToString());
                }
                _listenerPortfolioNeadToReconnect = false;
                _listenerPortfolio = null;
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
                    SendLogMessage(error.ToString());
                }
                _listenerTradeNeadToReconnect = false;
                _listenerTrade = null;
            }

            if (_publisher != null)
            {
                try
                {
                    if (_publisher.State == State.Active)
                    {
                        _publisher.Close();
                    }
                    _publisher.Dispose();
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString());
                }

                _publisher = null;
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
                    SendLogMessage(error.ToString());
                }

                _listenerOrderSendMirrorNeadToReload = false;
                _listenerOrderSendMirror = null;

            }

            // close connection with router / закрываем соединение с роутером
            try
            {
                CGate.Close();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString());
            }
        }

        /// <summary>
		/// reconnect
        /// переподключиться
        /// </summary>
        private void Reconnect()
        {
            Status = ServerConnectStatus.Disconnect;
            _lastMoveTime = DateTime.Now; // отмечаем флаг нахождения нового потока
            Stop();
            Dispose();
            Start();
        }

        /// <summary>
		/// time of the last checkpoint of main thread
        /// время последнего чекПоинта основного потока
        /// </summary>
        private DateTime _lastMoveTime = DateTime.MinValue;

        /// <summary>
		/// place of work of stream 2, which looks after the responses of stream 1
        /// место работы потока 2, который присматривает за откликами потока 1
        /// </summary>
        private void ThreadNannySpace()
        {
            while (Thread.CurrentThread.Name != "deleteThread")
            {
                Thread.Sleep(2000);

                if (_lastMoveTime != DateTime.MinValue &&
                    _lastMoveTime.AddMinutes(1) < DateTime.Now)
                {
                    SendLogMessage(OsLocalization.Market.Message78);
                    // we have an accident. Workflow lost and does not connect for three minutes/авария у нас. Рабочий поток потерялся и не выходит на связь три минуты
                    Thread worker = new Thread(Reconnect);
                    worker.CurrentCulture = new CultureInfo("ru-RU");
                    worker.IsBackground = true;
                    worker.Start();

                    return;
                }
            }
        }

        /// <summary>
		/// place of main stream work
        /// место работы основного потока
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private void PrimeWorkerThreadSpace()
        {
            try
            {

                while (Thread.CurrentThread.Name != "deleteThread")
                {
                    lock (_plazaThreadLocker)
                    {
                        _lastMoveTime = DateTime.Now;

                        State connectionState = Connection.State;

                        if (connectionState == State.Closed &&
                            _statusNeeded == ServerConnectStatus.Connect)
                        {
                            try
                            {
                                Connection.Open("");
                                Thread.Sleep(1000);
                                continue;
                            }
                            catch (Exception error)
                            {
                                // could not connect/не получилось подключиться
                               SendLogMessage(error.ToString());
                                Thread.Sleep(10000);
                                try
                                {
                                    //in ten seconds we try again/через десять секунд пытаемся ещё раз
                                    Connection.Open("");
                                    Thread.Sleep(1000);
                                }
                                catch (Exception)
                                {
                                    // something completely wrong. We are trying to reconnect completely/что-то совсем не так. Пытаемся переподключиться полностью
                                    Thread worker = new Thread(Reconnect);
                                    worker.CurrentCulture = new CultureInfo("ru-RU");
                                    worker.IsBackground = true;
                                    worker.Start();
                                    return;
                                }
                                continue;
                            }
                        }
                        else if (connectionState == State.Opening)
                        {
                            // ignore
                        }
                        else if (connectionState == State.Active &&
                                 _statusNeeded == ServerConnectStatus.Disconnect)
                        {
                            try
                            {
                                Connection.Close();

                            }
                            catch (Exception error)
                            {
                                SendLogMessage(error.ToString());
                            }

                            Status = ServerConnectStatus.Disconnect;
                            continue;
                        }
                        if (connectionState != State.Active)
                        {
                            continue;
                        }

                        // connect listeners/подключаем листнеры

                        CheckListnerInfo(); // instruments and portfolios/инструменты и портфели

                        CheckListnerPosition(); //listening to positions on portfolios/прослушка позиции по портфелям

                        CheckListnerTrades(); //listening to deals and applications/прослушка сделки и завки

                        CheckListnerMyTrades(); //listening to order log and my trades/прослушка ордер лог и мои сделки

                        CheckListnerMarketDepth(); // depth/стакан
                        // publisher/публишер 
                        CheckOrderSender();

                        // look at the data/смотрим очедедь данных

                        if (_ordersToExecute != null && _ordersToExecute.Count != 0)
                        {
                            continue;
                        }

                        Connection.Process(1);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString());
                // set the time of the last review from the main thread - four minutes ago/назначаем время последнего отзыва от основного потока - четырем минуты назад
                // thereafter, the stream watching the main stream starts the reconnection procedure/после этого поток следящий за основным потоком начинает процедуру переподключения
                _lastMoveTime = DateTime.Now.AddMinutes(-4);
            }
        }

		// Instruments. thread FORTS_FUTINFO_REPL table "fut_sess_contents"
        // Инструменты. поток FORTS_FUTINFO_REPL таблица "fut_sess_contents"

        /// <summary>
		/// listner responsible for accepting tools
        /// листнер отвечающий за приём инструментов
        /// </summary>
        private Listener _listenerInfo;

        /// <summary>
        /// flag saying that the connection with the list was interrupted/флаг, говорящий о том что коннект с листнером прервался 
        /// and it requires a full reboot/и требуется его полная перезагрузка
        /// </summary>
        private bool _listenerInfoNeadToReconnect;

        /// <summary>
		/// thread state of instrument listener
        /// состояние потока листнера инструментов
        /// </summary>
        private string _listenerInfoReplState;

        /// <summary>
		/// check the instrument listener for connection and validity
        /// проверить на подключение и валидность листнер инструментов
        /// </summary>
        private void CheckListnerInfo()
        {
            try
            {
                if (_listenerInfo == null)
                {
                    _listenerInfo = new Listener(Connection, ListenInfoString);
                    _listenerInfo.Handler += ClientMessage_Board;
                }
                if (_listenerInfoNeadToReconnect || _listenerInfo.State == State.Error)
                {
                    _listenerInfoNeadToReconnect = false;
                    try
                    {
                        _listenerInfo.Close();
                        _listenerInfo.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString());
                    }

                    _listenerInfo = null;
                    return;
                }
                if (_listenerInfo.State == State.Closed)
                {
                    if (_listenerInfoReplState == null)
                    {
                        _listenerInfo.Open("mode=snapshot+online");
                    }
                    else
                    {
                        _listenerInfo.Open("mode=snapshot+online;" + "replstate=" + _listenerInfoReplState);
                    }
                }
            }
            catch (Exception error)
            {
                try
                {
                    _listenerInfo.Dispose();
                }
                catch
                {
                    // ignore
                }

                _listenerInfo = null;

                SendLogMessage(error.ToString());
            }
        }

        /// <summary>
		/// handles the tool table here
        /// здесь обрабатывается таблица инструментов 
        /// </summary>
        public int ClientMessage_Board(
            Connection conn, Listener listener, Message msg)
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

                                    security.PriceStep = Convert.ToDecimal(replmsg["min_step"].asDecimal());

                                    security.PriceStepCost = Convert.ToDecimal(replmsg["step_price"].asDecimal());

                                    // From Spectra 6.5 "last_cl_quote" was deleted
                                    //security.PriceLimitLow = Convert.ToDecimal(replmsg["last_cl_quote"].asDecimal()) - Convert.ToDecimal(replmsg["limit_down"].asDecimal());
                                    security.PriceLimitLow = Convert.ToDecimal(replmsg["settlement_price"].asDecimal()) - Convert.ToDecimal(replmsg["limit_down"].asDecimal());

                                    //security.PriceLimitHigh = Convert.ToDecimal(replmsg["last_cl_quote"].asDecimal()) + Convert.ToDecimal(replmsg["limit_up"].asDecimal());
                                    security.PriceLimitHigh = Convert.ToDecimal(replmsg["settlement_price"].asDecimal()) + Convert.ToDecimal(replmsg["limit_up"].asDecimal());

                                    security.Lot = 1; //Convert.ToDecimal(replmsg["lot_volume"].asInt());

                                    security.SecurityType = SecurityType.Futures;

                                    security.NameClass = "FORTS";

                                    if (UpdateSecurity != null)
                                    {
                                        UpdateSecurity(security);
                                    }
                                }

                                catch (Exception error)
                                {
                                    SendLogMessage(error.ToString());
                                }

                            }
                            break;
                        }
                    case MessageType.MsgP2ReplReplState:
                        {
                            _listenerInfoReplState = ((P2ReplStateMessage)msg).ReplState;
                            _listenerInfoNeadToReconnect = true;
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
                return 0;
            }
            catch (CGateException e)
            {
                return (int)e.ErrCode;
            }
        }

        /// <summary>
		/// called when a new instrument appears
        /// вызывается при появлении нового инструмента
        /// </summary>
        public event Action<Security> UpdateSecurity;

// Positions and portfolios. thread FORTS_POS_REPL and tables "position" and "part"
// Позиции и портфели. поток FORTS_POS_REPL и c таблица "position" и "part"

        /// <summary>
		/// portfolio listener
        /// листнер портфелей
        /// </summary>
        private Listener _listenerPortfolio;

        /// <summary>
        /// flag saying that the connection with the list was interrupted/флаг, говорящий о том что коннект с листнером прервался 
        /// and it requires a full reboot/и требуется его полная перезагрузка
        /// </summary>
        private bool _listenerPortfolioNeadToReconnect;

        /// <summary>
		/// position listener
        /// листнет позиций
        /// </summary>
        private Listener _listenerPosition;

        /// <summary>
        /// flag saying that the connection with the list was interrupted/флаг, говорящий о том что коннект с листнером прервался 
        /// and it requires a full reboot/и требуется его полная перезагрузка
        /// </summary>
        private bool _listenerPositionNeadToReconnect;

        /// <summary>
		/// thread state of position listener
        /// состояние потока листнера позиций
        /// </summary>
        private string _listenerPositionReplState;

        /// <summary>
		/// thread state of portfolio listener
        /// состояние потока листнера портфелей
        /// </summary>
        private string _listenerPortfolioReplState;

        /// <summary>
		/// check the position and portfolio listener for connection and validity
        /// проверить на подключение и валидность листнер позиций и портфелей
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
                if (_listenerPositionNeadToReconnect || _listenerPosition.State == State.Error)
                {
                    _listenerPositionNeadToReconnect = false;
                    try
                    {
                        _listenerPosition.Close();
                        _listenerPosition.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString());
                    }

                    _listenerPosition = null;
                    return;
                }
                if (_listenerPosition.State == State.Closed)
                {
                    if (_listenerPositionReplState == null)
                    {
                        _listenerPosition.Open("mode=snapshot+online");
                    }
                    else
                    {
                        _listenerPosition.Open("mode=snapshot+online;replstate=" + _listenerPositionReplState);
                    }
                }

            }
            catch (Exception error)
            {
                try
                {
                    _listenerPosition.Dispose();
                }
                catch
                {
                    // ignore
                }

                _listenerPosition = null;

                SendLogMessage(error.ToString());
            }
            try
            {
                if (_listenerPortfolio == null)
                {
                    _listenerPortfolio = new Listener(Connection, ListenPortfolioString);
                    _listenerPortfolio.Handler += ClientMessage_Portfolio;
                }
                if (_listenerPortfolioNeadToReconnect || _listenerPortfolio.State == State.Error)
                {
                    _listenerPortfolioNeadToReconnect = false;
                    try
                    {
                        _listenerPortfolio.Close();
                        _listenerPortfolio.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString());
                    }

                    _listenerPortfolio = null;
                    return;
                }
                if (_listenerPortfolio.State == State.Closed)
                {
                    if (_listenerPortfolioReplState == null)
                    {
                        _listenerPortfolio.Open("mode=snapshot+online");
                    }
                    else
                    {
                        _listenerPortfolio.Open("mode=snapshot+online;" + "replstate=" + _listenerPortfolioReplState);
                    }
                }
            }
            catch (Exception error)
            {
                try
                {
                    _listenerPortfolio.Dispose();
                }
                catch
                {
                    // ignore
                }

                _listenerPortfolio = null;

                SendLogMessage(error.ToString());
            }
        }

        /// <summary>
		/// here process the table of positions by portfolios
        /// здесь обрабатывается таблица позиций по портфелям
        /// </summary>
        public int ClientMessage_Position(
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

                                    if (UpdatePosition != null)
                                    {
                                        UpdatePosition(positionOnBoard);
                                    }
                                }

                                catch (Exception error)
                                {
                                    SendLogMessage(error.ToString());
                                }

                            }
                            break;
                        }
                    case MessageType.MsgP2ReplReplState:
                        {
                            _listenerPositionReplState = ((P2ReplStateMessage)msg).ReplState;
                            _listenerPositionNeadToReconnect = true;
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
                return 0;
            }
            catch (CGateException e)
            {

                return (int)e.ErrCode;
            }

        }

        /// <summary>
		/// process the portfolio table here
        /// здесь обрабатывается таблица портфелей
        /// </summary>
        public int ClientMessage_Portfolio(
Connection conn, Listener listener, Message msg)
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
                                        _portfolios.Add(portfolio);
                                    }

                                    portfolio.ValueBegin = Convert.ToDecimal(replmsg["money_old"].asDecimal());
                                    portfolio.ValueCurrent = Convert.ToDecimal(replmsg["money_amount"].asDecimal());
                                    portfolio.ValueBlocked = Convert.ToDecimal(replmsg["money_blocked"].asDecimal());

                                    if (UpdatePortfolio != null)
                                    {
                                        UpdatePortfolio(portfolio);
                                    }
                                }

                                catch (Exception error)
                                {
                                    SendLogMessage(error.ToString());
                                }

                            }
                            break;
                        }
                    case MessageType.MsgP2ReplReplState:
                        {
                            _listenerPortfolioReplState = ((P2ReplStateMessage)msg).ReplState;
                            _listenerPortfolioNeadToReconnect = true;
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
                return 0;
            }
            catch (CGateException e)
            {
                return (int)e.ErrCode;
            }
        }

        /// <summary>
		/// called when changing position
        /// вызывается при изменении позиции
        /// </summary>
        public event Action<PositionOnBoard> UpdatePosition;

        /// <summary>
		/// called when new portfolios appear
        /// вызывается при появлении новых портфелей
        /// </summary>
        public event Action<Portfolio> UpdatePortfolio;

        /// <summary>
		/// portfolios in the system
        /// портфели в системе
        /// </summary>
        private List<Portfolio> _portfolios;

// listening ticks. thread FORTS_DEALS_REPL "deal" table
// Прослушка тиков. поток FORTS_DEALS_REPL таблица "deal"

        /// <summary>
		/// tick listener
        /// листнер тиков
        /// </summary>
        private Listener _listenerTrade;

        /// <summary>
        /// flag saying that the connection with the list was interrupted/флаг, говорящий о том что коннект с листнером прервался 
        /// and it requires a full reboot/и требуется его полная перезагрузка
        /// </summary>
        private bool _listenerTradeNeadToReconnect;

        /// <summary>
		/// thread state of tick listener
        /// состояние потока листнера тиков
        /// </summary>
        private string _listenerTradeReplState;

        /// <summary>
        /// flag. Do transactions go online. used on first boot,/флаг. идут ли сделки идут онлайн. используется при первой загрузке, 
        /// the program caches all the tics from the plaza is very hard to take/т.к. программа кэширует все тики из плазы это очень тяжело принимать
        /// and until all ticks reach the first time, subscribing to depths and trading will not work/и пока все тики первый раз не дойдут, подписываться на стаканы и торговать не выйдет
        /// </summary>
        private bool _dealsOnLine;

        /// <summary>
		/// check tick listener for connection and validity
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
                if (_listenerTradeNeadToReconnect || _listenerTrade.State == State.Error)
                {
                    _listenerTradeNeadToReconnect = false;
                    try
                    {
                        _listenerTrade.Close();
                        _listenerTrade.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString());
                    }

                    _listenerTrade = null;
                    return;
                }
                if (_listenerTrade.State == State.Closed)
                {
                    if (_listenerTradeReplState == null)
                    {
                        //_listenerTrade.Open("mode=online");
                        _listenerTrade.Open("mode=snapshot+online");
                        SendLogMessage(OsLocalization.Market.Message79);
                    }
                    else
                    {
                        _listenerTrade.Open("mode=snapshot+online;" + "replstate=" + _listenerTradeReplState);
                    }
                }

            }
            catch (Exception error)
            {
                try
                {
                    _listenerTrade.Dispose();
                }
                catch
                {
                    // ignore
                }

                _listenerTrade = null;

                SendLogMessage(error.ToString());
            }
        }

        /// <summary>
		/// tick table is processed here
        /// здесь обрабатываются таблица тиков
        /// </summary>
        public int ClientMessage_Trades(
Connection conn, Listener listener, Message msg)
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

                                    if (isSystem == 1)
                                    {
                                        return 0;
                                    }

                                    Trade trade = new Trade();
                                    trade.Price = Convert.ToDecimal(replmsg["price"].asDecimal());
                                    trade.Id = replmsg["id_deal"].asLong().ToString();
                                    trade.SecurityNameCode = replmsg["isin_id"].asInt().ToString();
                                    trade.Time = replmsg["moment"].asDateTime();
                                    trade.Volume = replmsg["xamount"].asInt();

                                    //From Spectra 6.5 "id_ord_buy" and "id_ord_sell" was changed to "public_order_id_buy" and "public_order_id_sell"
                                    //long numberBuyOrder = replmsg["id_ord_buy"].asLong();
                                    //long numberSellOrder = replmsg["id_ord_sell"].asLong();
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


                                    if (NewTradeEvent != null)
                                    {
                                        NewTradeEvent(trade, _dealsOnLine);
                                    }
                                }
                                catch (Exception error)
                                {
                                    SendLogMessage(error.ToString());
                                }
                            }
                            break;
                        }

                    case MessageType.MsgP2ReplOnline:
                        {
                            SendLogMessage(OsLocalization.Market.Message80);
                            // further data go Online/дальше данные идут ОнЛайн
                            _dealsOnLine = true;
                            // change the status of server so that you can add bots to it/меняем статус сервера, чтобы можно было к нему подцеплять ботов
                            Status = ServerConnectStatus.Connect;

                            break;
                        }
                    case MessageType.MsgP2ReplReplState:
                        {
                            _listenerTradeReplState = ((P2ReplStateMessage)msg).ReplState;
                            _listenerTradeNeadToReconnect = true;
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
                return 0;
            }
            catch (CGateException e)
            {
                return (int)e.ErrCode;
            }
        }

        /// <summary>
		/// called when a new tick comes from the system
        /// вызывается когда из системы приходит новый тик
        /// </summary>
        public event Action<Trade, bool> NewTradeEvent;

// depth thread FORTS_FUTAGGR20_REPL table "orders_aggr"
// Стаканы поток FORTS_FUTAGGR20_REPL таблица "orders_aggr"

        /// <summary>
		/// depth listener
        /// листнер стаканов
        /// </summary>
        private Listener _listenerMarketDepth;

        /// <summary>
        /// flag saying that the connection with the list was interrupted/флаг, говорящий о том что коннект с листнером прервался 
        /// and it requires a full reboot/и требуется его полная перезагрузка
        /// </summary>
        private bool _listenerMarketDepthNeadToReconnect;

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
                if (_listenerMarketDepthNeadToReconnect || _listenerMarketDepth.State == State.Error)
                {
                    _listenerMarketDepthNeadToReconnect = false;
                    try
                    {
                        _listenerMarketDepth.Close();
                        _listenerMarketDepth.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }

                    _listenerMarketDepth = null;
                    return;
                }
                if (_listenerMarketDepth.State == State.Closed)
                {
                    _listenerMarketDepth.Open("mode=snapshot+online");
                    //_listenerMarketDepth.Open();
                }

            }
            catch (Exception error)
            {
                try
                {
                    _listenerMarketDepth.Dispose();
                }
                catch
                {
                    // ignore
                }

                _listenerMarketDepth = null;

                SendLogMessage(error.ToString());
            }
        }

        /// <summary>
		/// instruments for which to collect depths
        /// инструменты по которым надо собирать стаканы
        /// </summary>
        private List<Security> _securitiesDepth;

        /// <summary>
		/// start unloading depth on this instrument
        /// начать выгружать стакан по этому инструменту
        /// </summary>
        public void StartMarketDepth(Security security)
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
		/// stop unloading depth on this instrument
        /// остановить выгрузку стакана по этому инструменту
        /// </summary>
        public void StopMarketDepth(Security security)
        {
            _securitiesDepth.Remove(security);
        }

        /// <summary>
		/// class responsible for the assembly depth from the slice
        /// класс отвечающий за сборку стакана из среза
        /// </summary>
        private PlazaMarketDepthCreator _depthCreator;

        /// <summary>
        /// depths that have been updated for the previous data series/стаканы которые обновились за предыдущую серию данных 
        /// and mailing these depths is required/и требуется рассылка этих стаканов
        /// </summary>
        private List<MarketDepth> rebildDepths;

        /// <summary>
		/// depth is processed here
        /// здесь обрабатывается стакан
        /// </summary>
        public int ClientMessage_OrderLog(
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

                                    Security mySecurity =
                                        _securitiesDepth.Find(security => security.NameId == idSecurity);

                                    if (mySecurity == null)
                                    {
                                        return 0;
                                    }

                                    MarketDepth myDepth = _depthCreator.SetNewMessage(replmsg, mySecurity);

                                    if (rebildDepths == null)
                                    {
                                        rebildDepths = new List<MarketDepth>();
                                    }

                                    if (rebildDepths.Find(depth => depth.SecurityNameCode == myDepth.SecurityNameCode) == null)
                                    {
                                        rebildDepths.Add(myDepth.GetCopy());
                                    }
                                }
                                catch (Exception error)
                                {
                                    SendLogMessage(error.ToString());
                                }
                            }
                            else
                            {

                            }
                            break;
                        }
                    case MessageType.MsgP2ReplClearDeleted:
                        {// it is necessary to remove levels from revision from depths/надо удалить из стаканов уровни с ревизией младше указанного
                            P2ReplClearDeletedMessage cdMessage = (P2ReplClearDeletedMessage)msg;
                            _depthCreator.ClearFromRevision(cdMessage);
                            break;
                        }
                    case MessageType.MsgOpen:
                        {
                            _depthCreator.ClearAll();
                            break;
                        }
                    case MessageType.MsgClose:
                        {
                            _depthCreator.ClearAll();
                            break;
                        }
                    case MessageType.MsgP2ReplLifeNum:
                        {
                            _depthCreator.ClearAll();
                            break;
                        }
                    case MessageType.MsgP2ReplReplState:
                        {
                            _listenerMarketDepthNeadToReconnect = true;
                            break;
                        }
                    case MessageType.MsgTnCommit:
                        {
                            if (rebildDepths != null && rebildDepths.Count != 0)
                            {
                                for (int i = 0; i < rebildDepths.Count; i++)
                                {
                                    if (MarketDepthChangeEvent != null && rebildDepths[i] != null &&
                                        rebildDepths[i].Bids != null && rebildDepths[i].Bids.Count != 0 &&
                                        rebildDepths[i].Asks != null && rebildDepths[i].Asks.Count != 0)
                                    {
                                        MarketDepthChangeEvent(rebildDepths[i]);
                                    }
                                }
                                rebildDepths.Clear();
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
            catch (CGateException e)
            {
                return (int)e.ErrCode;
            }
        }

        /// <summary>
		/// called when a glass has been updated.
        /// вызывается когда обновился какой-то стакан
        /// </summary>
        public event Action<MarketDepth> MarketDepthChangeEvent;

// my trades and orderLog thread FORTS_FUTTRADE_REPLL table "user_deal" "orders_log"
// Мои сделки и ордерЛог поток FORTS_FUTTRADE_REPLL таблица "user_deal" "orders_log"

        /// <summary>
		/// listener of my deals and my orders
        /// листнер моих сделок и моих ордеров
        /// </summary>
        private Listener _listenerOrderAndMyDeal;

        /// <summary>
        /// flag saying that the connection with the list was interrupted/флаг, говорящий о том что коннект с листнером прервался 
        /// and it requires a full reboot/и требуется его полная перезагрузка
        /// </summary>
        private bool _listenerOrderAndMyDealNeadToReload;

        /// <summary>
		/// thread state of my trades and my orders listener
        /// состояние потока листнера моих сделок и моих ордеров
        /// </summary>
        private string _listenerOrderAndMyDealReplState;

        /// <summary>
		/// check my orders and my trades listener for connection and validity
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
                if (_listenerOrderAndMyDealNeadToReload || _listenerOrderAndMyDeal.State == State.Error)
                {
                    _listenerOrderAndMyDealNeadToReload = false;
                    try
                    {
                        _listenerOrderAndMyDeal.Close();
                        _listenerOrderAndMyDeal.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString());
                    }

                    _listenerOrderAndMyDeal = null;
                    return;
                }
                if (_listenerOrderAndMyDeal.State == State.Closed)
                {
                    if (_listenerOrderAndMyDealReplState == null)
                    {
                        _listenerOrderAndMyDeal.Open("mode=snapshot+online");
                    }
                    else
                    {
                        _listenerOrderAndMyDeal.Open("mode=snapshot+online;" + "replstate=" + _listenerOrderAndMyDealReplState);
                    }
                }

            }
            catch (Exception error)
            {
                try
                {
                    _listenerOrderAndMyDeal.Dispose();
                }
                catch
                {
                    // ignore
                }

                _listenerOrderAndMyDeal = null;

                SendLogMessage(error.ToString());
            }
        }

        /// <summary>
		/// here my trades and orders table is processed
        /// здесь обрабатывается таблица мои сделки и ордеров
        /// </summary>
        public int ClientMessage_OrderAndMyDeal(
Connection conn, Listener listener, Message msg)
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
                                    trade.Time = replmsg["moment"].asDateTime();
                                    trade.Volume = replmsg["xamount"].asInt();


                                    string portfolioBuy = replmsg["code_buy"].asString();

                                    string portfolioSell = replmsg["code_sell"].asString();


                                    if (!string.IsNullOrWhiteSpace(portfolioBuy))
                                    {
                                        if (_portfolios != null && _portfolios.Find(portfolio => portfolio.Number == portfolioBuy) != null)
                                        {
                                            //trade.NumberOrderParent = replmsg["id_ord_buy"].asLong().ToString();
                                            trade.NumberOrderParent = replmsg["public_order_id_buy"].asLong().ToString();
                                            trade.Side = Side.Buy;
                                        }
                                    }
                                    else if (!string.IsNullOrWhiteSpace(portfolioSell))
                                    {
                                        if (_portfolios != null && _portfolios.Find(portfolio => portfolio.Number == portfolioSell) != null)
                                        {
                                            //trade.NumberOrderParent = replmsg["id_ord_sell"].asLong().ToString();
                                            trade.NumberOrderParent = replmsg["public_order_id_sell"].asLong().ToString();
                                            trade.Side = Side.Sell;
                                        }
                                    }

                                    if (NewMyTradeEvent != null)
                                    {
                                        NewMyTradeEvent(trade);
                                    }
                                }
                                catch (Exception error)
                                {
                                    SendLogMessage(error.ToString());
                                }
                            }
                            else if (replmsg.MsgName == "orders_log")
                            {
                                try
                                {
                                    Order order = new Order();

                                    order.NumberMarket = replmsg["id_ord"].asLong().ToString();
                                    order.NumberUser = replmsg["ext_id"].asInt();

                                    order.Volume = replmsg["xamount"].asInt();
                                    //order.VolumeExecute = 0;
                                    order.VolumeExecute = replmsg["xamount_rest"].asInt(); // это у нас оставшееся в заявке

                                    order.Price = Convert.ToDecimal(replmsg["price"].asDecimal());
                                    order.PortfolioNumber = replmsg["client_code"].asString();
                                    order.SecurityNameCode = replmsg["isin_id"].asInt().ToString();
                                    order.TimeCallBack = replmsg["moment"].asDateTime();
                                    order.ServerType = ServerType.Plaza;

                                    int action = replmsg["action"].asInt();

                                    if (action == 0)
                                    {
                                        order.State = OrderStateType.Cancel;
                                        order.TimeCancel = order.TimeCallBack;
                                    }
                                    else if (action == 1)
                                    {
                                        order.State = OrderStateType.Activ;
                                        int status = replmsg["xstatus"].asInt();
                                        if (status == 1025)
                                        {
                                            return 0;
                                        }
                                    }
                                    else if (action == 2)
                                    {
                                        order.State = OrderStateType.Done;
                                    }

                                    //int status = replmsg["status"].asInt(); //0x200000 



                                    int dir = replmsg["dir"].asInt();

                                    if (dir == 1)
                                    {
                                        order.Side = Side.Buy;
                                    }
                                    else if (dir == 2)
                                    {
                                        order.Side = Side.Sell;
                                    }

                                    if (NewMyOrderEvent != null)
                                    {
                                        NewMyOrderEvent(order);
                                    }
                                }
                                catch (Exception error)
                                {
                                    SendLogMessage(error.ToString());
                                }
                            }

                            break;
                        }
                    case MessageType.MsgP2ReplOnline:
                        {
                            break;
                        }
                    case MessageType.MsgTnBegin:
                        {
                            break;
                        }
                    case MessageType.MsgTnCommit:
                        {
                            break;
                        }
                    case MessageType.MsgOpen:
                        {
                            break;
                        }
                    case MessageType.MsgClose:
                        {
                            break;
                        }
                    case MessageType.MsgP2ReplLifeNum:
                        {
                            break;
                        }
                    case MessageType.MsgP2ReplReplState:
                        {
                            _listenerOrderAndMyDealReplState = ((P2ReplStateMessage)msg).ReplState;
                            _listenerOrderAndMyDealNeadToReload = true;
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
                return 0;
            }
            catch (CGateException e)
            {
                return (int)e.ErrCode;
            }
        }

        /// <summary>
		/// called when a new trade comes from the system
        /// вызывается когда из системы приходит новая моя сделка
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent;

        /// <summary>
		/// called when a new order comes from the system
        /// вызывается когда из системы приходит новый мой ордер
        /// </summary>
        public event Action<Order> NewMyOrderEvent;

		// order execution
        // Исполнение заявок

        /// <summary>
		/// listener listening to publisher's answers (thing that transmit orders to the system)
        /// листнер прослушивающий ответы публишера(штуки которая передаёт ордера в систему)
        /// </summary>
        private Listener _listenerOrderSendMirror;

        /// <summary>
        /// flag saying that the connection with the list was interrupted/флаг, говорящий о том что коннект с листнером прервался 
        /// and it requires a full reboot/и требуется его полная перезагрузка
        /// </summary>
        private bool _listenerOrderSendMirrorNeadToReload;

        /// <summary>
		/// thread status of listener monitoring the publisher
        /// состояние потока листнера следящего за публишером
        /// </summary>
        private string _listenerOrderSenderMirrorReplState;

        /// <summary>
		/// trades publisher and CODHeartbeat
        /// публишер сделок и CODHeartbeat
        /// </summary>
        private Publisher _publisher;

        /// <summary>
		/// check on the connection and validity of the publisher and listner watching his responses
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
                    _publisher.Open();
                }
                if (_publisher.State == State.Error)
                {
                    try
                    {
                        _publisher.Close();
                        _publisher.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString());
                    }

                    _publisher = null;
                }
            }
            catch (Exception error)
            {
                try
                {
                    _publisher.Dispose();
                }
                catch
                {
                    // ignore
                }

                _publisher = null;

                SendLogMessage(error.ToString());
            }

            try
            {
                if (_publisher == null || _publisher.State != State.Active)
                {
                    return;
                }
                if (_listenerOrderSendMirror == null)
                {
                    _listenerOrderSendMirror = new Listener(Connection, ListnerOrders);
                    _listenerOrderSendMirror.Handler += ClientMessage_OrderSenderMirror;
                    return;
                }
                if (_listenerOrderSendMirrorNeadToReload || _listenerOrderSendMirror.State == State.Error)
                {
                    _listenerOrderSendMirrorNeadToReload = false;
                    try
                    {
                        _listenerOrderSendMirror.Close();
                        _listenerOrderSendMirror.Dispose();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString());
                    }

                    _listenerOrderSendMirror = null;
                    return;
                }
                if (_listenerOrderSendMirror.State == State.Closed)
                {
                    if (_listenerOrderSenderMirrorReplState == null)
                    {
                        _listenerOrderSendMirror.Open("mode=online");
                    }
                    else
                    {
                        _listenerOrderSendMirror.Open("mode=snapshot+online;" + "replstate=" + _listenerOrderSenderMirrorReplState);
                    }
                }

            }
            catch (Exception error)
            {
                try
                {
                    _listenerOrderSendMirror.Dispose();
                }
                catch
                {
                    // ignore
                }

                _listenerOrderSendMirror = null;
                SendLogMessage(error.ToString());
            }
        }

        /// <summary>
		/// here my trade table and order log are processed
        /// здесь обрабатывается таблица мои сделки и ордерЛог
        /// </summary>
        public int ClientMessage_OrderSenderMirror(
            Connection conn, Listener listener, Message msg)
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

                                if (msgData.MsgId == 101)
                                {
                                    // received response to place order/пришёл отклик на выставление заявки
                                    int code = msgData["code"].asInt();

                                    Order order = new Order();
                                    order.NumberMarket = msgData["order_id"].asLong().ToString();
                                    order.NumberUser = Convert.ToInt32(msgData.UserId);
                                    order.ServerType = ServerType.Plaza;

                                    if (code == 0)
                                    {
                                        order.State = OrderStateType.Activ;
                                    }
                                    else
                                    {
                                        order.State = OrderStateType.Fail;
                                    }

                                    if (NewMyOrderEvent != null)
                                    {
                                        NewMyOrderEvent(order);
                                    }
                                }
                                else if (msgData.MsgId == 102)
                                {
                                    // received response to cancel order/пришёл отклик на удаление заявки
                                    int code = msgData["code"].asInt();

                                    Order order = new Order();
                                    //order.NumberMarket = msgData["order_id"].asLong().ToString();
                                    order.NumberUser = Convert.ToInt32(msgData.UserId);
                                    order.ServerType = ServerType.Plaza;

                                    if (code == 0 || code == 14)
                                    {
                                        order.State = OrderStateType.Cancel;
                                    }
                                    else
                                    {
                                        return 0;
                                    }

                                    if (NewMyOrderEvent != null)
                                    {
                                        NewMyOrderEvent(order);
                                    }
                                }
                                else if (msgData.MsgId == 99)
                                {
                                    SendLogMessage(OsLocalization.Market.Message81 + msgData);
                                    return 0;
                                }
                                else if (msgData.MsgId == 100)
                                {
                                    SendLogMessage(msgData.ToString());
                                    Thread worker = new Thread(Reconnect);
                                    worker.IsBackground = true;
                                    worker.CurrentCulture = new CultureInfo("ru-RU");
                                    worker.Start();
                                    return 0;
                                }
                            }
                            catch (Exception error)
                            {
                                SendLogMessage(error.ToString());
                            }

                            break;
                        }
                    case MessageType.MsgP2ReplReplState:
                        {
                            _listenerOrderSendMirrorNeadToReload = true;
                            _listenerOrderSenderMirrorReplState = ((P2ReplStateMessage)msg).ReplState;
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
                return 0;
            }
            catch (CGateException e)
            {

                return (int)e.ErrCode;
            }
        }

        /// <summary>
		/// execute order
        /// исполнить ордер
        /// </summary>
        public void ExecuteOrder(Order order)
        {
            _ordersToExecute.Enqueue(order);
        }

        /// <summary>
		/// cancel order from the system
        /// отозвать ордер из системы
        /// </summary>
        public void CancelOrder(Order order)
        {
            if (_canseledOrders == null)
            {
                _canseledOrders = new List<decimal[]>();
            }

            decimal[] record = _canseledOrders.Find(decimals => decimals[0] == order.NumberUser);

            if (record != null)
            {
                record[1] += 1;
				// if algorithm tries to remove one bid five times, then order cancellation will be ignored
                // если алгоритм пытается снять одну заявку пять раз, то снятие этой заявки будет проигнорировано
                if (record[1] > 5)
                {
                    return;
                }
            }
            else
            {
                record = new[] { Convert.ToDecimal(order.NumberUser), 1 };
                _canseledOrders.Add(record);
            }

            _ordersToCansel.Enqueue(order);
        }

        /// <summary>
		/// thread standing on the order queue and monitoring the execution of trades
        /// поток стоящий на очереди заявок и следящий за исполнением сделок
        /// </summary>
        private Thread _threadOrderExecutor;

        /// <summary>
		/// number of orders in the last second
        /// количество заявок в последней секунде
        /// </summary>
        private int _countActionInThisSecond = 0;

        /// <summary>
		/// last second in which we placed orders
        /// последняя секунда, в которой мы выставляли заявки
        /// </summary>
        private DateTime _thisSecond = DateTime.MinValue;

        /// <summary>
		/// method where the stream is running sending orders to the system
        /// метод где работает поток высылающий заявки в систему
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private void ExecutorOrdersThreadArea()
        {
            
            while (Thread.CurrentThread.Name != "deleteThread")
            {
                if (Status == ServerConnectStatus.Connect && _ordersToExecute != null && _ordersToExecute.Count != 0)
                {
                    if (_thisSecond != DateTime.MinValue &&
                        _thisSecond.AddSeconds(1) > DateTime.Now &&
                        _countActionInThisSecond >= MaxTransaction - 1)
                    {
                        continue;
                    }

                    if (_thisSecond.AddSeconds(1) < DateTime.Now ||
                        _thisSecond == DateTime.MinValue)
                    {
                        _thisSecond = DateTime.Now;
                        _countActionInThisSecond = 0;
                    }
                    try
                    {
                        Order order = _ordersToExecute.Dequeue();

                        lock (_plazaThreadLocker)
                        {
                            Message sendMessage = _publisher.NewMessage(MessageKeyType.KeyName, "FutAddOrder");

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
                            smsg["isin"].set(order.SecurityNameCode);
                            smsg["client_code"].set(clientCode);
                            smsg["type"].set(1);
                            smsg["dir"].set(dir);
                            smsg["amount"].set(Convert.ToInt32(order.Volume));
                            smsg["price"].set(order.Price.ToString(new CultureInfo("ru-RU")));
                            smsg["ext_id"].set(order.NumberUser);

                            _publisher.Post(sendMessage, PublishFlag.NeedReply);
                            sendMessage.Dispose();
                            SendLogMessage("выслали" + DateTime.Now.ToString() + DateTime.Now.Millisecond);
                        }
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString());
                    }
                }
                else if (Status == ServerConnectStatus.Connect && _ordersToCansel != null && _ordersToCansel.Count != 0)
                {
                    if (_thisSecond != DateTime.MinValue &&
                        _thisSecond.AddSeconds(1) > DateTime.Now &&
                        _countActionInThisSecond >= MaxTransaction - 1)
                    {
                        continue;
                    }

                    if (_thisSecond.AddSeconds(1) < DateTime.Now ||
                        _thisSecond == DateTime.MinValue)
                    {
                        _thisSecond = DateTime.Now;
                        _countActionInThisSecond = 0;
                    }

                    try
                    {
                        Order order = _ordersToCansel.Dequeue();
                        lock (_plazaThreadLocker)
                        {
                            Message sendMessage = _publisher.NewMessage(MessageKeyType.KeyName, "FutDelOrder");

                            string code = order.PortfolioNumber;

                            string brockerCode = code[0].ToString() + code[1].ToString() + code[2].ToString() +
                                                 code[3].ToString();

                            DataMessage smsg = (DataMessage)sendMessage;
                            smsg.UserId = (uint)order.NumberUser;
                            smsg["broker_code"].set(brockerCode);
                            smsg["order_id"].set(Convert.ToInt64(order.NumberMarket));

                            _publisher.Post(sendMessage, PublishFlag.NeedReply);
                            sendMessage.Dispose();
                            _countActionInThisSecond++;
                        }
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString());
                    }
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
        }

        /// <summary>
		/// order queue for execution
        /// очередь заявок на исполнение
        /// </summary>
        private Queue<Order> _ordersToExecute;

        /// <summary>
		/// order queue for cancellation
        /// очередь заявок на отмену
        /// </summary>
        private Queue<Order> _ordersToCansel;

        private List<decimal[]> _canseledOrders;

        // CODHeartbeat

        /// <summary>
        /// CODHeartbeat thread / поток отправляющий CODHeartbeat
        /// COD - specially ordered service from a broker/COD - специально заказываемая услуга у брокера
        /// allows you to delete all active orders of your login when the connection with your program is broken/позволяет при обрыве связи с Вашей программой удалять все активные заявки ВАшего логина
        /// It works as well - while signals from the program are being sent - everything is ok. How to stop - the exchange cancels all orders./Работает так- пока сигналы из программы идут - всё ок. Как прекращаются - биржа кроет все заявки.
        /// </summary>
        private Thread _heartBeatSenderThread;

        /// <summary>
		/// work place of thread sending signals CODHeartbeat to the system
        /// место работы потока отправляющего сигналы CODHeartbeat в систему
        /// </summary>
        private void HeartBeatSender()
        {
            while (Thread.CurrentThread.Name != "deleteThread")
            {
                Thread.Sleep(5000);

                lock (_plazaThreadLocker)
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
                        SendLogMessage(error.ToString());
                    }
                }
            }
        }
	
		// sending messages to up
        // Отправка сообщений на верх

        /// <summary>
		/// send log message
        /// отправить сообщение в лог
        /// </summary>
        private void SendLogMessage(string message)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message);
            }
        }

        /// <summary>
		/// called when a new log message appears
        /// вызывается когда появлось новое сообщение в Лог
        /// </summary>
        public event Action<string> LogMessageEvent;
    }
}
