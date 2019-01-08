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
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;


namespace OsEngine.Market.Servers.Lmax
{
    public class LmaxFixClient
    {
        /// <summary>
        /// адрес FIX – Trading сервиса
        /// </summary>
        private string _fixTradingIp;

        /// <summary>
        /// адрес FIX – Market Data сервиса
        /// </summary>
        private string _fixMarketDataIp;

        /// <summary>
        /// url веб интерфейса
        /// </summary>
        private string _uiUrl;
        private string _userName;
        private string _password;

        /// <summary>
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
                SendLogMessage("Не удалось начать подключение, отсутствует один или несколько обязательных параметров", LogMessageType.Error);
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
        /// установить соединение с биржей 
        /// </summary>
        public void Connect()
        {
            LoginRequest loginRequest = new LoginRequest(_userName, _password, _isDemo ? ProductType.CFD_DEMO : ProductType.CFD_LIVE);

            _lmaxApi.Login(loginRequest, LoginCallback, FailureCallback("ошибка входа в систему"));

            IPAddress ipAddress = IPAddress.Parse(_fixTradingIp);

            IPEndPoint endPoint = new IPEndPoint(ipAddress, _port);

            _client = new TcpClient();

            try
            {
                _client.Connect(endPoint);
            }
            catch (Exception e)
            {
                SendLogMessage("Не удалось установить соединение с биржей", LogMessageType.Error);
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
                FailureCallback("Ошибка подписки на аккаунт инфо"));

            _session.Subscribe(new HistoricMarketDataSubscriptionRequest(),
                () => SendLogMessage("Подписались на получение свечей", LogMessageType.System),
                FailureCallback("Ошибка подписки на получение свечей"));

            _session.EventStreamSessionDisconnected += SessionOnEventStreamSessionDisconnected;

            _session.MarketDataChanged += SessionOnMarketDataChanged;

            _lmaxApiNetThread = new Thread(session.Start);
            _lmaxApiNetThread.Start();
            _lmaxApiNetThread.IsBackground = true;
        }

        /// <summary>
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
                () => SendLogMessage("Успешный запрос портфелей", LogMessageType.Error),
                FailureCallback("Ошибка запроса портфелей"));
        }

        /// <summary>
        /// запросить список инструментов
        /// </summary>
        public void GetSecyrities()
        {
            string query = "CURRENCY";
            long offsetInstrumentId = 0;

            _session.SearchInstruments(new SearchRequest(query, offsetInstrumentId), SearchCallback,
                FailureCallback("Ошибка запроса инструментов"));
        }

        /// <summary>
        /// пришли инструменты
        /// </summary>
        /// <param name="instruments">список инструментов</param>
        /// <param name="hasMoreResults">true если есть еще инструменты помимо пришедших</param>
        private void SearchCallback(List<Instrument> instruments, bool hasMoreResults)
        {
            UpdatedSecurities?.Invoke(instruments);

            SendLogMessage("Запрос инструментов прошел успешно", LogMessageType.System);

            if (hasMoreResults)
            {
                string query = "CURRENCY";
                _session.SearchInstruments(new SearchRequest(query, instruments.Count - 1), SearchCallback,
                    FailureCallback("Ошибка запроса инструментов"));
            }
        }

        /// <summary>
        /// подписаться на обновления стакана котировок
        /// </summary>
        public void SubscribeToPaper(string securityId)
        {
            _session.Subscribe(new OrderBookSubscriptionRequest(Convert.ToInt64(securityId)),
                () => SendLogMessage("Подписались на стакан", LogMessageType.System),
                FailureCallback("Ошибка подписки на стакан"));

        }

        /// <summary>
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
        /// отменить ордер
        /// </summary>
        /// <param name="securityId">id инструмента</param>
        /// <param name="order">ордер который нужно отменить</param>
        public void CancelOrder(string securityId, Order order)
        {
            string cancelOrderMsg = _creator.OrderCancelRequestMsg(order.NumberMarket, order.NumberUser.ToString(), securityId);

            _tradingSession.Send(cancelOrderMsg);
        }

        /// <summary>
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
                    SendLogMessage("Ошибка чтения данных из потока: " + e.Message, LogMessageType.Error);
                    Dispose(false);
                    Disconnected?.Invoke();
                    return;
                }
            }
        }

        /// <summary>
        /// обработчик события торговой сессии
        /// </summary>
        private void TradingSessionOnNewMessageEvent(StringBuilder message)
        {
            _newMessage.Enqueue(message.ToString());

            //SendLogMessage(message.ToString(), LogMessageType.Error);           
        }


        #region Разбор пришедших данных

        /// <summary>
        /// очередь новых данных, пришедших с сервера биржи
        /// </summary>
        private ConcurrentQueue<string> _newMessage = new ConcurrentQueue<string>();

        /// <summary>
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
                            // если пришел сигнал о новом ордере, но ClOrdID не может быть конвертирован в int, значит ордер создавался не в OsEngine, игнорим его
                            order.NumberUser = Convert.ToInt32(numUser);
                        }
                        catch (Exception e)
                        {
                            return;
                        }

                        order.State = OrderStateType.Activ;
                        order.NumberMarket = entity.GetFieldByTag((int)Tags.OrderID);
                        order.Side = entity.GetFieldByTag((int)Tags.Side) == "1" ? Side.Buy : Side.Sell;
                        order.Volume = Convert.ToDecimal(entity.GetFieldByTag((int)Tags.OrderQty),
                            CultureInfo.InvariantCulture);

                        if (entity.GetFieldByTag((int)Tags.OrdType) == "2")
                        {
                            order.Price = Convert.ToDecimal(entity.GetFieldByTag((int)Tags.Price),
                                CultureInfo.InvariantCulture);
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
                            "Ошибка выставления ордера: " + _errorDictionary.OrdRejReason[rej] + "-" +
                            entity.GetFieldByTag((int)Tags.Text), LogMessageType.System);

                        MyOrderEvent?.Invoke(order);

                        return;
                    }

                    if (type == "4")
                    {
                        order.State = OrderStateType.Cancel;
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
                        order.Volume = Convert.ToDecimal(entity.GetFieldByTag((int)Tags.OrderQty),
                            CultureInfo.InvariantCulture);

                        if (entity.GetFieldByTag((int)Tags.OrdType) == "2")
                        {
                            order.Price = Convert.ToDecimal(entity.GetFieldByTag((int)Tags.Price),
                                CultureInfo.InvariantCulture);
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
                    trade.Volume = Convert.ToDecimal(entity.GetFieldByTag((int)Tags.LastQty),
                        CultureInfo.InvariantCulture);
                    trade.Price = Convert.ToDecimal(entity.GetFieldByTag((int)Tags.LastPx),
                        CultureInfo.InvariantCulture);
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
                SendLogMessage("Ошибка отмены ордера. Причина: " + _errorDictionary.CxlRejReason[rej] + "-" + entity.GetFieldByTag((int)Tags.Text), LogMessageType.Trade);
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

        #region исходящие события

        /// <summary>
        /// соединение с API установлено
        /// </summary>
        public event Action Connected;

        /// <summary>
        /// соединение с API разорвано
        /// </summary>
        public event Action Disconnected;

        /// <summary>
        /// новые бумаги в системе
        /// </summary>
        public event Action<List<Instrument>> UpdatedSecurities;

        /// <summary>
        /// обновились портфели
        /// </summary>
        public event Action<AccountStateEvent> UpdatePortfolios;

        /// <summary>
        /// обновился стакан
        /// </summary>
        public event Action<OrderBookEvent> UpdateMarketDepth;

        /// <summary>
        /// новые мои ордера
        /// </summary>
        public event Action<Order> MyOrderEvent;

        /// <summary>
        /// новые мои сделки
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        #endregion

        // сообщения для лога

        /// <summary>
        /// добавить в лог новое сообщение
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            LogMessageEvent?.Invoke(message, type);
        }

        /// <summary>
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}
