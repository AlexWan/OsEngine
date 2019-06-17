using Com.Lmax.Api;
using Com.Lmax.Api.Account;
using Com.Lmax.Api.MarketData;
using Com.Lmax.Api.OrderBook;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Lmax.LmaxEntity;
using OsEngine.Market.Servers.FixProtocolEntities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using OsEngine.Language;


namespace OsEngine.Market.Servers.Lmax
{
    public class LmaxFixClient
    {
        /// <summary>
		/// address of FIX-Trading service
        /// адрес FIX – Trading сервиса
        /// </summary>
        private string _fixTradingIp;

        /// <summary>
		/// address of FIX-Market data service 
        /// адрес FIX – Market Data сервиса
        /// </summary>
        private string _fixMarketDataIp;

        /// <summary>
		/// url of web-interface
        /// url веб интерфейса
        /// </summary>
        private string _uiUrl;
        private string _userName;
        private string _password;

        /// <summary>
		/// port number
        /// номер порта
        /// </summary>
        private int _port;
        private bool _isDemo;

        private const string BeginString = "FIX.4.4";
        private string _targetCompIdTrd = "LMXBL";
        private string _targetCompIdMd = "LMXBLM";

        private readonly DateTime _startWorkingTime;
        private readonly DateTime _endWorkingTime;

        private FixMessageCreator _creator;
        private FixMessageParser _parser;
        private LmaxApi _lmaxApi;

        public bool IsCreated;

        /// <summary>
		/// constructor
        /// конструктор
        /// </summary>
        public LmaxFixClient(string fixTradingIp, string fixMarketDataIp, string uiUrl, int port, string username, string password, DateTime startWorkingTime, DateTime endWorkingTime)
        {
            if (string.IsNullOrEmpty(username) ||
                string.IsNullOrEmpty(password) ||
                string.IsNullOrEmpty(fixTradingIp) ||
                string.IsNullOrEmpty(uiUrl) ||
                port <= 0)
            {
                SendLogMessage(OsLocalization.Market.Label57, LogMessageType.Error);
                return;
            }

            _startWorkingTime = startWorkingTime;
            _endWorkingTime = endWorkingTime;

            _fixTradingIp = fixTradingIp;
            _fixMarketDataIp = fixMarketDataIp;
            _uiUrl = uiUrl;
            _userName = username;
            _password = password;
            _port = port;
            _isDemo = uiUrl.Contains("demo");

            var settings = new StandartHeaderSettings
            {
                BeginString = BeginString,
                SenderCompId = username,
                TargetCompIdTrd = _targetCompIdTrd,
                TargetCompIdMd = _targetCompIdMd,
                Username = username,
                Password = password
            };

            _creator = new FixMessageCreator(settings);
            _parser = new FixMessageParser();

            _lmaxApi = new LmaxApi(_uiUrl);

            IsCreated = true;
        }

        private bool _tradingSessionIsReady;

        Session _tradingSession;

        private TcpClient _client;

        /// <summary>
		/// connect to the exchange
        /// установить соединение с биржей 
        /// </summary>
        public void Connect()
        {
            LoginRequest loginRequest = new LoginRequest(_userName, _password, _isDemo ? ProductType.CFD_DEMO : ProductType.CFD_LIVE);

            _lmaxApi.Login(loginRequest, LoginCallback, FailureCallback(OsLocalization.Market.Label58));

            IPAddress ipAddress = IPAddress.Parse(_fixTradingIp);

            IPEndPoint endPoint = new IPEndPoint(ipAddress, _port);

            _client = new TcpClient();

            try
            {
                _client.Connect(endPoint);
            }
            catch (Exception e)
            {
                SendLogMessage(OsLocalization.Market.Label56, LogMessageType.Error);
                Disconnected?.Invoke();
                return;
            }

            _tradingSession = new Session(_client, _uiUrl);

            _tradingSession.NewMessageEvent += TradingSessionOnNewMessageEvent;

            StartAllThreads();

            string logOnMsg = _creator.LogOnMsg(true, _heartbeatInterval);
            _tradingSession.Send(logOnMsg);
        }

        private void CheckConnectorState()
        {
            if (_helperSessionIsReady && _tradingSessionIsReady)
            {
                Connected?.Invoke();
            }
        }

        private ISession _session;

        private bool _helperSessionIsReady;

        /// <summary>
		/// connect to Lmax NET API
        /// подключиться к Lmax NET API
        /// </summary>
        private void LoginCallback(ISession session)
        {
            _session = session;
            _session.AccountStateUpdated += OnAccountStateEvent;

            _session.Subscribe(new AccountSubscriptionRequest(), () =>
            {
                _helperSessionIsReady = true;
                CheckConnectorState();
            },
                FailureCallback(OsLocalization.Market.Message66));

            _session.Subscribe(new HistoricMarketDataSubscriptionRequest(),
                () => SendLogMessage(OsLocalization.Market.Message67, LogMessageType.System),
                FailureCallback(OsLocalization.Market.Message68));

            _session.EventStreamSessionDisconnected += SessionOnEventStreamSessionDisconnected;

            _session.MarketDataChanged += SessionOnMarketDataChanged;

            _lmaxApiNetThread = new Thread(session.Start);
            _lmaxApiNetThread.Start();
            _lmaxApiNetThread.IsBackground = true;
        }

        /// <summary>
		/// depth and ticks updated
        /// обновился стакан и тики
        /// </summary>
        private void SessionOnMarketDataChanged(OrderBookEvent orderBook)
        {
            UpdateMarketDepth?.Invoke(orderBook);
        }

        private void SessionOnEventStreamSessionDisconnected()
        {
            Disconnected?.Invoke();
        }

        /// <summary>
		/// portfolios updated
        /// обновились портфели
        /// </summary>
        public void OnAccountStateEvent(AccountStateEvent accountStateEvent)
        {
            UpdatePortfolios?.Invoke(accountStateEvent);
        }

        private OnFailure FailureCallback(string failedFunction)
        {
            return failureResponse => SendLogMessage(failedFunction, LogMessageType.Error);
        }

        public void GetPortfolios()
        {
            _session.RequestAccountState(new AccountStateRequest(),
                () => SendLogMessage(OsLocalization.Market.Message69, LogMessageType.Error),
                FailureCallback(OsLocalization.Market.Message70));
        }

        /// <summary>
		/// request list of instruments
        /// запросить список инструментов
        /// </summary>
        public void GetSecyrities()
        {
            string query = "CURRENCY";
            long offsetInstrumentId = 0;

            _session.SearchInstruments(new SearchRequest(query, offsetInstrumentId), SearchCallback,
                FailureCallback(OsLocalization.Market.Message71));
        }

        /// <summary>
		/// got instruments
        /// пришли инструменты
        /// </summary>
        /// <param name="instruments">list of instrument / список инструментов</param>
        /// <param name="hasMoreResults">true if there are more instruments besides comers/true если есть еще инструменты помимо пришедших</param>
        private void SearchCallback(List<Instrument> instruments, bool hasMoreResults)
        {
            UpdatedSecurities?.Invoke(instruments);

            SendLogMessage(OsLocalization.Market.Message72, LogMessageType.System);

            if (hasMoreResults)
            {
                string query = "CURRENCY";
                _session.SearchInstruments(new SearchRequest(query, instruments.Count - 1), SearchCallback,
                    FailureCallback(OsLocalization.Market.Message71));
            }
        }

        /// <summary>
		/// subscribe to updated quote depth
        /// подписаться на обновления стакана котировок
        /// </summary>
        public void SubscribeToPaper(string securityId)
        {
            _session.Subscribe(new OrderBookSubscriptionRequest(Convert.ToInt64(securityId)),
                () => SendLogMessage(OsLocalization.Market.Message73, LogMessageType.System),
                FailureCallback(OsLocalization.Market.Message74));

        }

        /// <summary>
		/// send a new order to the exchange
        /// отправить на биржу новый ордер
        /// </summary>
        public void SendNewOrderSingle(string securityId, Order order)
        {
            string newOrderMsg = _creator.NewOrderSingleMsg(order.NumberUser.ToString(), securityId, order.Side == Side.Buy ? "1" : "2",
                                                                  order.Volume.ToString(CultureInfo.InvariantCulture), order.TypeOrder == OrderPriceType.Limit ? "2" : "1",
                                                                  order.Price.ToString(CultureInfo.InvariantCulture));

            _tradingSession.Send(newOrderMsg);
        }

        /// <summary>
		/// cancel order
        /// отменить ордер
        /// </summary>
        /// <param name="securityId">instrument id / id инструмента</param>
        /// <param name="order">order for cancellation / ордер который нужно отменить</param>
        public void CancelOrder(string securityId, Order order)
        {
            string cancelOrderMsg = _creator.OrderCancelRequestMsg(order.NumberMarket, order.NumberUser.ToString(), securityId);

            _tradingSession.Send(cancelOrderMsg);
        }

        /// <summary>
		/// request order status
        /// запросить статус ордера
        /// </summary>
        public void GetOrderState(string securityId, Order order)
        {
            string orderStateRequest = _creator.OrderStatusRequestMsg(order.NumberUser.ToString(), securityId, order.Side == Side.Buy ? "1" : "2");

            _tradingSession.Send(orderStateRequest);
        }

        private Thread _tradingDataConverterThread;
        private Thread _tradingDataHandlerThread;
        private Thread _lmaxApiNetThread;


        /// <summary>
		/// start all auxiliary threads
        /// запустить все вспомогательные потоки
        /// </summary>
        private void StartAllThreads()
        {
            _inWork = true;

            _tradingDataConverterThread = new Thread(Converter);
            _tradingDataConverterThread.Start();
            _tradingDataConverterThread.IsBackground = true;

            _tradingDataHandlerThread = new Thread(TradingDataReader);
            _tradingDataHandlerThread.Start();
            _tradingDataHandlerThread.IsBackground = true;
        }

        private bool _inWork;

        /// <summary>
		/// work place of thread reading market data from the exchange
        /// место работы потока считывающего market data с биржи
        /// </summary>
        public void TradingDataReader()
        {
            while (_inWork)
            {
                try
                {
                    _tradingSession.Read();
                }
                catch (Exception e)
                {
                    SendLogMessage(e.Message, LogMessageType.Error);
                    Dispose(false);
                    Disconnected?.Invoke();
                    return;
                }
            }
        }

        /// <summary>
		/// session handler
        /// обработчик события торговой сессии
        /// </summary>
        private void TradingSessionOnNewMessageEvent(StringBuilder message)
        {
            _newMessage.Enqueue(message.ToString());

            //SendLogMessage(message.ToString(), LogMessageType.Error);           
        }


        #region Parsing incoming data / Разбор пришедших данных

        /// <summary>
		/// queue of new data coming from the exchange server
        /// очередь новых данных, пришедших с сервера биржи
        /// </summary>
        private ConcurrentQueue<string> _newMessage = new ConcurrentQueue<string>();

        /// <summary>
		/// takes messages from the data queue, converts them to C # classes and sends further
        /// берет сообщения из очереди данных, конвертирует их в классы C# и отправляет дальше
        /// </summary>
        public void Converter()
        {
            while (true)
            {
                try
                {
                    if (!_inWork)
                    {
                        return;
                    }

                    if (!_newMessage.IsEmpty)
                    {
                        string mes;

                        if (_newMessage.TryDequeue(out mes))
                        {
                            var fixEntity = _parser.ParseMessage(mes);

                            _lastMessageTime = DateTime.UtcNow;

                            for (int i = 0; i < fixEntity.Count; i++)
                            {
                                switch (fixEntity[i].EntityType)
                                {
                                    case "0":
                                        _tradingSession.Send(_creator.HeartbeatMsg(true));
                                        continue;
                                    case "1":
                                        _tradingSession.Send(_creator.TestRequestMsg("TEST", true));
                                        continue;
                                    case "2":
                                        continue;
                                    case "3":
                                        SendLogMessage(mes, LogMessageType.Error);
                                        continue;
                                    case "4":
                                        continue;
                                    case "5":
                                        _tradingSessionIsReady = false;
                                        Disconnected?.Invoke();
                                        continue;
                                    case "8":
                                        ExecutionReportHandler(fixEntity[i]);
                                        continue;
                                    case "9":
                                        OrderCancelRejectHandler(fixEntity[i]);
                                        continue;
                                    case "A":
                                        _tradingSessionIsReady = true;
                                        CheckConnectorState();
                                        continue;
                                    case "D":
                                        continue;
                                    case "F":
                                        continue;
                                    case "AQ":
                                        continue;
                                    case "AE":
                                        continue;
                                }
                            }
                        }
                    }

                    CheckConnection();

                    if (DateTime.UtcNow.Hour == _endWorkingTime.Hour && DateTime.UtcNow.Minute >= _endWorkingTime.Minute && DateTime.UtcNow.Minute < _startWorkingTime.Minute)
                    {
                        Dispose(true);
                        Disconnected?.Invoke();
                        return;
                    }
                }

                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
                Thread.Sleep(1);
            }
        }

        private int _heartbeatInterval = 20;

        private DateTime _lastMessageTime;

        /// <summary>
		/// check the connection
        /// проверить соединение
        /// </summary>
        private void CheckConnection()
        {
            // если с момента получения прошлого сообщения прошло больше положенного премени, значит связь потеряна
            if (_lastMessageTime != DateTime.MinValue && _lastMessageTime.AddSeconds(_heartbeatInterval + 10) < DateTime.UtcNow)
            {
                _tradingSessionIsReady = false;
                Disconnected?.Invoke();
            }
        }

        private readonly RejectReasons _errorDictionary = new RejectReasons();

        /// <summary>
		/// report handler of my orders and trades
        /// обработчик отчета о моих ордерах и сделках
        /// </summary>
        private void ExecutionReportHandler(FixEntity entity)
        {
            try
            {
                string type = entity.GetFieldByTag((int)Tags.ExecType);

                if (type != "F")
                {
                    Order order = new Order();
                    order.ServerType = ServerType.Lmax;
                    var time = entity.GetFieldByTag((int)Tags.TransactTime);
                    order.TimeCallBack = DateTime.ParseExact(time, "yyyyMMdd-HH:mm:ss.fff", CultureInfo.CurrentCulture);
                    order.SecurityNameCode = entity.GetFieldByTag((int)Tags.SecurityID);
                    var numUser = entity.GetFieldByTag((int)Tags.ClOrdID);

                    if (type == "0")
                    {
                        try
                        {
                            // if new order signal has come, but ClOrdID cannot be converted to int, then the order was not created in OsEngine, ignore it / если пришел сигнал о новом ордере, но ClOrdID не может быть конвертирован в int, значит ордер создавался не в OsEngine, игнорим его
                            order.NumberUser = Convert.ToInt32(numUser);
                        }
                        catch (Exception e)
                        {
                            return;
                        }

                        order.State = OrderStateType.Activ;
                        order.NumberMarket = entity.GetFieldByTag((int)Tags.OrderID);
                        order.Side = entity.GetFieldByTag((int)Tags.Side) == "1" ? Side.Buy : Side.Sell;
                        order.Volume = entity.GetFieldByTag((int)Tags.OrderQty).ToDecimal();

                        if (entity.GetFieldByTag((int)Tags.OrdType) == "2")
                        {
                            order.Price = entity.GetFieldByTag((int)Tags.Price).ToDecimal();
                        }

                        MyOrderEvent?.Invoke(order);

                        return;
                    }

                    if (type == "8")
                    {
                        try
                        {
                            order.NumberUser = Convert.ToInt32(numUser);
                        }
                        catch (Exception e)
                        {
                            return;
                        }

                        order.State = OrderStateType.Fail;

                        string rej = entity.GetFieldByTag((int)Tags.OrdRejReason);

                        SendLogMessage(
                           _errorDictionary.OrdRejReason[rej] + "-" +
                            entity.GetFieldByTag((int)Tags.Text), LogMessageType.System);

                        MyOrderEvent?.Invoke(order);

                        return;
                    }

                    if (type == "4")
                    {
                        order.State = OrderStateType.Cancel;
                        order.TimeCancel = order.TimeCallBack;
                        var oldNumUser = entity.GetFieldByTag((int)Tags.OrigClOrdID);

                        try
                        {
                            order.NumberUser = Convert.ToInt32(oldNumUser);
                        }
                        catch (Exception e)
                        {
                            return;
                        }

                        order.NumberMarket = entity.GetFieldByTag((int)Tags.OrderID);
                        order.Side = entity.GetFieldByTag((int)Tags.Side) == "1" ? Side.Buy : Side.Sell;
                        order.Volume = entity.GetFieldByTag((int)Tags.OrderQty).ToDecimal();

                        if (entity.GetFieldByTag((int)Tags.OrdType) == "2")
                        {
                            order.Price = entity.GetFieldByTag((int)Tags.Price).ToDecimal();
                        }

                        MyOrderEvent?.Invoke(order);
                    }
                    else if (type == "I")
                    {

                    }
                }
                else
                {
                    MyTrade trade = new MyTrade();
                    trade.Time = DateTime.ParseExact(entity.GetFieldByTag((int)Tags.TransactTime),
                        "yyyyMMdd-HH:mm:ss.fff", CultureInfo.CurrentCulture);
                    trade.NumberOrderParent = entity.GetFieldByTag((int)Tags.OrderID);
                    trade.NumberTrade = entity.GetFieldByTag((int)Tags.ExecID);
                    trade.Volume = entity.GetFieldByTag((int)Tags.LastQty).ToDecimal();
                    trade.Price = entity.GetFieldByTag((int)Tags.LastPx).ToDecimal();
                    trade.SecurityNameCode = entity.GetFieldByTag((int)Tags.SecurityID);

                    MyTradeEvent?.Invoke(trade);
                }
            }
            catch (ArgumentException e)
            {
                SendLogMessage(e.Message, LogMessageType.Error);
            }
            catch (Exception e)
            {
                SendLogMessage("ExecutionReportHandlerError " + e.Message, LogMessageType.Error);
            }
        }

        /// <summary>
		/// my order cancellation error report handler
        /// обработчик отчета об ошибках отмены моих ордеров
        /// </summary>
        private void OrderCancelRejectHandler(FixEntity entity)
        {
            try
            {
                string rej = entity.GetFieldByTag((int)Tags.CxlRejReason);

                if (rej == "1")
                {
                    Order order = new Order();
                    order.ServerType = ServerType.Lmax;
                    order.NumberMarket = entity.GetFieldByTag((int)Tags.ClOrdID);
                    order.TimeCallBack = DateTime.UtcNow;
                    var numUser = entity.GetFieldByTag((int)Tags.OrigClOrdID);

                    try
                    {
                        order.NumberUser = Convert.ToInt32(numUser);
                    }
                    catch (Exception e)
                    {
                        return;
                    }
                    order.State = OrderStateType.Fail;
                    MyOrderEvent?.Invoke(order);
                    return;
                }
                SendLogMessage(OsLocalization.Market.Message75 + _errorDictionary.CxlRejReason[rej] + "-" + entity.GetFieldByTag((int)Tags.Text), LogMessageType.Trade);
            }
            catch (ArgumentException e)
            {
                SendLogMessage(e.Message, LogMessageType.Error);
            }
            catch (Exception e)
            {
                SendLogMessage("OrderCancelRejectHandlerError " + e.Message, LogMessageType.Error);
            }
        }

        #endregion


        /// <summary>
		/// clear the resources used by the client
        /// очистить ресурсы используемые клиентом
        /// </summary>
        public void Dispose(bool needSendLogOutMsg = false)
        {
            _inWork = false;

            if (needSendLogOutMsg)
            {
                _tradingSession?.Send(_creator.LogOutMsg(true));
                Thread.Sleep(1000);
            }

            _tradingSession?.Dispose();
            _session?.Stop();
            _lmaxApi = null;
        }

        #region outgoing messages / исходящие события

        /// <summary>
		/// API connection established
        /// соединение с API установлено
        /// </summary>
        public event Action Connected;

        /// <summary>
		/// API connection lost
        /// соединение с API разорвано
        /// </summary>
        public event Action Disconnected;

        /// <summary>
		/// new securities in the system
        /// новые бумаги в системе
        /// </summary>
        public event Action<List<Instrument>> UpdatedSecurities;

        /// <summary>
		/// updated portfolios
        /// обновились портфели
        /// </summary>
        public event Action<AccountStateEvent> UpdatePortfolios;

        /// <summary>
		/// updated depth
        /// обновился стакан
        /// </summary>
        public event Action<OrderBookEvent> UpdateMarketDepth;

        /// <summary>
		/// my new orders
        /// новые мои ордера
        /// </summary>
        public event Action<Order> MyOrderEvent;

        /// <summary>
		/// my new trades
        /// новые мои сделки
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        #endregion

		// log messages
        // сообщения для лога

        /// <summary>
		/// add a new log message
        /// добавить в лог новое сообщение
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            LogMessageEvent?.Invoke(message, type);
        }

        /// <summary>
		/// outgoing message for log
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}
