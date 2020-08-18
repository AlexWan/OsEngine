using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Bitfinex.BitfitnexEntity;
using RestSharp;
using WebSocket4Net;

namespace OsEngine.Market.Servers.Bitfinex
{
    class BitfinexClient
    {
        string _apiKey;
        string _secretKey;

        public BitfinexClient()
        {
            _restClient = new RestClient(_baseUrlV1);

            for (int i = 0; i < 5; i++)
            {
               Thread converter = new Thread(Converter);
                converter.CurrentCulture = new CultureInfo("ru-RU");
                converter.Name = "BitFinexConverterFread" + i;
                converter.IsBackground = true;
                converter.Start();
            }
        }

        private string _baseUrlV1 = "https://api.bitfinex.com/v1";

        private string _baseUrlV2 = "https://api.bitfinex.com/v2";

        /// <summary>
        /// shows whether the connection works
        /// работает ли соединение
        /// </summary>
        public bool IsConnected;

        /// <summary>
        /// connecto to the exchange
        /// установить соединение с биржей 
        /// </summary>
        public void Connect(string pubKey, string secKey)
        {
            _apiKey = pubKey;
            _secretKey = secKey;

            if (string.IsNullOrWhiteSpace(_apiKey) ||
                string.IsNullOrWhiteSpace(_secretKey))
            {
                return;
            }

            // check server availability for HTTP communication with it / проверяем доступность сервера для HTTP общения с ним
            Uri uri = new Uri(_baseUrlV1 + "/symbols");

            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);

                HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            }
            catch (Exception exception)
            {
                SendLogMessage("Сервер не доступен. Отсутствует интернет. ", LogMessageType.Error);
                return;
            }

            IsConnected = true;

            CreateNewWebSocket();
        }

        /// <summary>
        /// there was a request to clear the object
        /// произошёл запрос на очистку объекта
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// bring the program to the start. Clear all objects involved in connecting to the server
        /// привести программу к моменту запуска. Очистить все объекты участвующие в подключении к серверу
        /// </summary>
        public void Dispose()
        {
            if (_wsClient != null)
            {
                _wsClient.Dispose();
                _wsClient = null;
            }
           
            IsConnected = false;

            _isDisposed = true;
        }

        /// <summary>
        /// request instruments
        /// запросить инструменты
        /// </summary>
        /// <returns></returns>
        public List<BitfinexSecurity> GetSecurities()
        {
            try
            {
                var res = CreateQuery(_baseUrlV1, Method.GET, "symbols_details", null);
                var parsSecurities = JsonConvert.DeserializeAnonymousType(res, new List<BitfinexSecurity>());
                return parsSecurities;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        public BitfinexTickerTradeInfo GetTradeInfo(string symbol)
        {
            try
            {
                var res = CreateQuery(_baseUrlV1, Method.GET, "pubticker/" + symbol.ToLower(), null);

                var pars = JsonConvert.DeserializeObject(res, typeof(BitfinexTickerTradeInfo));

                return (BitfinexTickerTradeInfo)pars;

            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// subscribe to get personal data, such as portfolios, positions, orders, trades
        /// подписаться на получение личных данных, таких как портфели, позиции, ордера, сделки
        /// </summary>
        public void SubscribeUserData()
        {
            string subscribeString = TakeSubscriptionMesAuthWebSoket();

            _wsClient.Send(subscribeString);
        }

        private readonly object _candlesLocker = new object();

        /// <summary>
        /// request candles
        /// запросить свечи
        /// </summary>
        public string GetCandles(Dictionary<string, string> param)
        {
            try
            {
                lock (_candlesLocker)
                {
                    var res = CreateQuery(_baseUrlV2, Method.GET, "candles", param);

                    return res;
                }                
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// request trades
        /// запросить трейды
        /// </summary>
        public string GetTrades(Dictionary<string, string> param)
        {
            try
            {
                lock (_candlesLocker)
                {
                    var res = CreateQuery(_baseUrlV2, Method.GET, "trades", param);

                    return res;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        private object _lockOrder = new object();

        /// <summary>
        /// execute order
        /// исполнить ордер
        /// </summary>
        public void ExecuteOrder(Order order, bool isMarginTrading)
        {
            lock (_lockOrder)
            {
                try
                {
                    if (IsConnected == false)
                    {
                        return;
                    }

                    NewOrderPayload newOrder = new NewOrderPayload();

                    if (isMarginTrading == false && order.SecurityNameCode.Contains(":") == false)
                    {
                        newOrder.type = "exchange limit";
                    }
                    else// if (isMarginTrading)
                    {
                        newOrder.type = "limit";
                    }
                    
                    newOrder.exchange = "bitfinex";
                    newOrder.request = "/v1/order/new";
                    newOrder.side = order.Side == Side.Buy ? "buy" : "sell";
                    newOrder.price = order.Price.ToString(CultureInfo.InvariantCulture);
                    newOrder.amount = order.Volume.ToString(CultureInfo.InvariantCulture);
                    newOrder.symbol = order.SecurityNameCode;
                    newOrder.nonce = GetNonce();

                    var jsonPayload = JsonConvert.SerializeObject(newOrder);

                    var res = CreateAuthQuery(_baseUrlV1, "/order/new", jsonPayload);

                    BitfinexResponseOrder newCreatedOrder;

                    if (res != null)
                    {
                        newCreatedOrder = JsonConvert.DeserializeObject<BitfinexResponseOrder>(res);

                        _osOrders.Add(newCreatedOrder.id.ToString(), order.NumberUser);

                        var newOsOrder = new Order();
                        newOsOrder.SecurityNameCode = newCreatedOrder.symbol;
                        newOsOrder.PortfolioNumber = newCreatedOrder.symbol.Substring(3);
                        newOsOrder.Side = newCreatedOrder.side == "buy" ? Side.Buy : Side.Sell;
                        newOsOrder.NumberMarket = newCreatedOrder.order_id.ToString();
                        newOsOrder.NumberUser = order.NumberUser;
                        newOsOrder.ServerType = ServerType.Bitfinex;
                        newOsOrder.Price = newCreatedOrder.price.ToDecimal();
                        newOsOrder.Volume = newCreatedOrder.original_amount.ToDecimal();
                        newOsOrder.VolumeExecute = newCreatedOrder.executed_amount.ToDecimal();

                        newOsOrder.TimeCallBack = new DateTime(1970, 1, 1) + TimeSpan.FromSeconds(Math.Round(Convert.ToDouble(newCreatedOrder.timestamp.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture)));

                        newOsOrder.State = OrderStateType.Activ;

                        if (MyOrderEvent != null)
                        {
                            MyOrderEvent(newOsOrder);
                        }
                    }
                    else
                    {
                         order.State = OrderStateType.Fail;
                         if (MyOrderEvent != null)
                         {
                             MyOrderEvent(order);
                         }
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// OS order ID and numbers in the bot
        /// id ордеров принадлежащих осе и номера в роботе
        /// </summary>
        Dictionary<string,int> _osOrders = new Dictionary<string, int>();

        /// <summary>
        /// cancel order
        /// отменить оредр
        /// </summary>
        public void CancelOrder(Order order)
        {
            lock (_lockOrder)
            {
                try
                {
                    var jsonPayload = string.Format("{{\"request\": \"/v1/order/cancel\",\"nonce\": \"{0}\",\"order_id\": {1}}}", GetNonce(), order.NumberMarket);

                    var res = CreateAuthQuery(_baseUrlV1, "/order/cancel", jsonPayload);

                    BitfinexResponseOrder CanceledOrder = JsonConvert.DeserializeObject<BitfinexResponseOrder>(res);
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// multi-threaded access locker to http-requests
        /// блокиратор многопоточного доступа к http запросам
        /// </summary>
        private readonly object _queryHttpLocker = new object();

        private readonly IRestClient _restClient;

        /// <summary>
        /// create authenticated request
        /// создать аутентифицированный запрос
        /// </summary>
        public string CreateAuthQuery(string apiVersionUrl, string endpoint, string payload)
        {
            try
            {
                lock (_queryHttpLocker)
                {
                    string payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));

                    var request = new RestRequest(endpoint, Method.POST);
                    request.AddHeader("X-BFX-APIKEY", _apiKey);
                    request.AddHeader("X-BFX-PAYLOAD", payloadBase64);
                    request.AddHeader("X-BFX-SIGNATURE", CreateSignature(payloadBase64, _secretKey));
                    
                    var response = _restClient.Execute(request);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return response.Content;
                    }
                    else
                    {
                        SendLogMessage("Ошибка в запросе" + response.Content, LogMessageType.Error);
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// method sends a request and returns a response from the server
        /// метод отправляет запрос и возвращает ответ от сервера
        /// </summary>
        public string CreateQuery(string apiVersionUrl, Method method, string endpoint, Dictionary<string, string> param = null)
        {
            try
            {
                lock (_queryHttpLocker)
                {
                    string fullUrl = "";

                    if (param != null)
                    {
                        fullUrl += "/";

                        foreach (var onePar in param)
                        {
                            fullUrl += onePar.Key + onePar.Value;
                        }
                    }

                    var request = new RestRequest(endpoint + fullUrl, method);

                    var response = new RestClient(apiVersionUrl).Execute(request);

                    var result = response.Content;

                    if (string.IsNullOrEmpty(result) || response.StatusCode != HttpStatusCode.OK)
                    {
                        SendLogMessage("Ошибка в запросе:" + result, LogMessageType.Error);
                        return null;
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        #region request authentication / Аутентификация запроса

        /// <summary>
        /// take a subscription message of authenticated web socket
        /// взять сообщение подписки аутентифицированного вебсокета
        /// </summary>
        /// <returns></returns>
        private string TakeSubscriptionMesAuthWebSoket()
        {
            string authNonce = GetNonce();

            string authPayload = "AUTH" + authNonce;

            string authSig = CreateSignature(authPayload, _secretKey);

            string payload = string.Format("{{\"apiKey\":\"{0}\", \"authSig\":\"{1}\", \"authNonce\":{2}, \"authPayload\":\"{3}\", \"event\":\"auth\"}}", _apiKey, authSig, authNonce, authPayload);

            return payload;
        }

        /// <summary>
        /// take milisecond time from 01.01.1970
        /// взять время в милисекундах, прошедшее с 1970, 1, 1 года
        /// </summary>
        /// <returns></returns>
        private string GetNonce()
        {
            DateTime yearBegin = new DateTime(1970, 1, 1);
            var timeStamp = DateTime.UtcNow - yearBegin;
            var r = timeStamp.TotalMilliseconds*1000;
            var re = Convert.ToInt64(r);
            return re.ToString();
        }

        /// <summary>
        /// encode message
        /// закодировать сообщение
        /// </summary>
        /// <param name="payload"> message / сообщение </param>
        /// <param name="apiSecret"> secret key / секретный ключ </param>
        /// <returns></returns>
        public string CreateSignature(string payload, string apiSecret)
        {
            var keyBytes = Encoding.UTF8.GetBytes(payload);
            var secretBytes = Encoding.UTF8.GetBytes(apiSecret);
            
            /*string ByteToString(byte[] buff)
            {
                var builder = new StringBuilder();

                for (var i = 0; i < buff.Length; i++)
                {
                    builder.Append(buff[i].ToString("X2")); // hex format
                }
                return builder.ToString();
            }*/

            using (var hmacsha384 = new HMACSHA384(secretBytes))
            {
                byte[] hashmessage = hmacsha384.ComputeHash(keyBytes);
                return ByteToString(hashmessage).ToLower();
            }
        }

        private string ByteToString(byte[] buff)
        {
            var builder = new StringBuilder();

            for (var i = 0; i < buff.Length; i++)
            {
                builder.Append(buff[i].ToString("X2")); // hex format
            }
            return builder.ToString();

        }

        #endregion

        #region log message / сообщения для лога

        /// <summary>
        /// add a new log message
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
        /// send exeptions
        /// отправляет исключения
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

        #region work with stream data with using web-sockets / Работа с потоковыми данными через вебсокеты

        /// <summary>
        /// adress for web-socket connection
        /// адрес для подключения к вебсокетам
        /// </summary>
        private string _wsUrl = "wss://api.bitfinex.com/ws";

        private WebSocket _wsClient;

        /// <summary>
        /// create a new web-socket connection
        /// создать новое подключение по сокетам
        /// </summary>
        private void CreateNewWebSocket()
        {
            if (_wsClient == null)
            {
                _wsClient = new WebSocket(_wsUrl);
            }

            _wsClient.Opened += new EventHandler(Connect);

            _wsClient.Closed += new EventHandler(Disconnect);

            _wsClient.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>(WsError);

            _wsClient.MessageReceived += new EventHandler<MessageReceivedEventArgs>(GetRes);

            _wsClient.Open();

            Thread.Sleep(2000);

            if (_wsClient.State != WebSocketState.Open)
            {
                if (Disconnected != null)
                {
                    SendLogMessage("Подключение к бирже битфайнекс не удалось. Отсутствует связь с биржей",LogMessageType.Error);
                    Disconnected();
                }
            }
        }

        private List<Security> _subscribedSecurities = new List<Security>();

        /// <summary>
        /// subscribe to this security to receive depths and trades
        /// подписать данную бумагу на получение стаканов и трейдов
        /// </summary>
        public void SubscribleTradesAndDepths(Security security)
        {
            try
            {
                Thread.Sleep(1000);
                var needSec = _subscribedSecurities.Find(s => s.Name == security.Name);

                if (needSec == null)
                {
                    _subscribedSecurities.Add(security);
                    // subscribe to depth / подписаться на стаканы
                    string subscribeTrades = string.Format("{{\"event\": \"subscribe\", \"channel\": \"book\", \"pair\": \"{0}\", \"prec\": \"P0\", \"freq\": \"F0\"}}", security.Name);
                    _wsClient.Send(subscribeTrades);

                    // subscribe to ticks / подписаться на тики
                    string subscribeOrderBook = string.Format("{{\"event\": \"subscribe\", \"channel\": \"trades\", \"pair\": \"{0}\"}}", security.Name);
                    _wsClient.Send(subscribeOrderBook);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// unsubscribe to data
        /// отписаться от данных
        /// </summary>
        /// <param name="security"></param>
        public void UnsubscribleTradesAndDepths(Security security)
        {
            // unsubscribe to depth / подписаться на стаканы
            string subscribeTrades = string.Format("{{\"event\": \"unsubscribe\", \"chanId\": \"{0}\"}}", security.Name);
            _wsClient.Send(subscribeTrades);

            // unsubscribe to ticks / подписаться на тики
            string subscribeOrderBook = string.Format("{{\"event\": \"unsubscribe\", \"channel\": \"trades\", \"pair\": \"{0}\"}}", security.Name);
            _wsClient.Send(subscribeOrderBook);
        }

        /// <summary>
        /// ws-connection opened
        /// соединение по ws открыто
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Connect(object sender, EventArgs e)
        {
            IsConnected = true;

            if (Connected != null)
            {
                Connected();
            }

            SendLogMessage("Соединение через вебсокет успешно установлено", LogMessageType.System);
        }

        /// <summary>
        /// ws-connection closed
        /// соединение по ws закрыто
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Disconnect(object sender, EventArgs e)
        {
            if (IsConnected)
            {
                IsConnected = false;

                SendLogMessage("Соединение через вебсокет разорвано", LogMessageType.System);

                if (Disconnected != null)
                {
                    Disconnected();
                }
            }
        }

        /// <summary>
        /// error from ws4net
        /// ошибка из ws4net
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WsError(object sender, EventArgs e)
        {
            SendLogMessage("Ошибка из ws4net :" + e, LogMessageType.Error);            
        }

        /// <summary>
        /// queue of new messages from the exchange server
        /// очередь новых сообщений, пришедших с сервера биржи
        /// </summary>
        private ConcurrentQueue<string> _newMessage = new ConcurrentQueue<string>();

        /// <summary>
        /// takes ws-messages and puts them in a general queue
        /// берет пришедшие через ws сообщения и кладет их в общую очередь
        /// </summary>        
        private void GetRes(object sender, MessageReceivedEventArgs e)
        {
            _newMessage.Enqueue(e.Message);
        }
        
        /// <summary>
        /// collect subscribed to ticks instruments
        /// коллекция инструментов подписанных на тики
        /// </summary>
        private Dictionary<string, int> _subscribedTradesSecurity = new Dictionary<string, int>();

        /// <summary>
        /// collect subscribed to depths instruments
        /// коллекция инструментов подписанных на стаканы
        /// </summary>
        private Dictionary<string, int> _subscribedBooksSecurity = new Dictionary<string, int>();

        /// <summary>
        /// multi-threaded sending locker to up 
        /// блокировка многопоточной высылки даных на верх
        /// </summary>
        private object _senderLocker = new object();

        /// <summary>
        /// takes messages from the common queue, converts them to C # classes and sends them to up
        /// берет сообщения из общей очереди, конвертирует их в классы C# и отправляет на верх
        /// </summary>
        public void Converter()
        {
            while (true)
            {
                try
                {
                    if (_isDisposed)
                    {
                        return;
                    }

                    if (!_newMessage.IsEmpty)
                    {
                        string mes;

                        if (_newMessage.TryDequeue(out mes))
                        {
                            if (mes.Contains("\"event\":\"error\""))
                            {
                                lock (_senderLocker)
                                {
                                    SendLogMessage(mes, LogMessageType.Error);
                                }
                            }
                            else if (mes.StartsWith("{\"event\":\"info\""))
                            {
                                lock (_senderLocker)
                                {
                                    if (mes.Contains("\"code\":20051") || mes.Contains("\"code\":20060"))
                                    {
                                        SendLogMessage("остановка/перезапуск сервера Websocket", LogMessageType.Error);

                                        if (Disconnected != null)
                                        {
                                            Disconnected();
                                        }
                                    }
                                }
                            }
                            else if (mes.Contains("\"event\":\"auth\""))
                            {

                            }
                            else if (mes.Contains("\"os\"")) // снимок моих ордеров
                            {


                            }
                            else if(mes.Contains("\"on\""))
                            {
                                
                            }
                            else if ( mes.Contains("\"ou\"") || mes.Contains("\"oc\"")) // обновление или закрытие моего ордера
                            {
                                lock (_senderLocker)
                                {
                                    Thread.Sleep(300);

                                    var values = ParseData(mes);

                                    int numUser = _osOrders[values[0]];

                                    var order = new Order();

                                    order.NumberUser = numUser;
                                    order.SecurityNameCode = values[1];
                                    order.PortfolioNumber = values[1].Substring(3);
                                    order.Side = values[2].ToDecimal() > 0 ? Side.Buy : Side.Sell;
                                    order.NumberMarket = values[0];
                                    order.Price = values[6].ToDecimal();
                                    order.Volume = Math.Abs(values[2].ToDecimal());

                                    order.TimeCallBack = DateTime.Parse(values[8].TrimEnd('Z'));

                                    if (values[5].Contains("EXECUTED"))
                                    {
                                        order.State = OrderStateType.Done;
                                    }
                                    else
                                    {
                                        switch (values[5])
                                        {
                                            case "ACTIVE":
                                                order.State = OrderStateType.Activ;
                                                break;

                                            case "PARTIALLY FILLED":
                                                order.State = OrderStateType.Patrial;
                                                break;

                                            case "CANCELED":
                                                order.TimeCancel = order.TimeCallBack;
                                                order.State = OrderStateType.Cancel;
                                                _osOrders.Remove(order.NumberMarket);
                                                break;

                                            default:
                                                order.State = OrderStateType.None;
                                                break;
                                        }
                                    }
                                    if (MyOrderEvent != null)
                                    {
                                        MyOrderEvent(order);
                                    }
                                }
                            }
                            else if (mes.Contains("[0,\"tu\",[")) // my new trade / новая моя сделка
                            {
                                lock (_senderLocker)
                                {
                                    // [0,"tu",["5809001-IOTBTC",275774974,"IOTBTC",1533415991,15073784940,13,0.00012184,"EXCHANGE LIMIT",0.00012184,-0.026,"IOT"]] моя сделка
                                    var valuesMyTrade = ParseData(mes);

                                    Thread.Sleep(300);

                                    MyTrade myTrade = new MyTrade();
                                    myTrade.Price = valuesMyTrade[6].ToDecimal();
                                    myTrade.NumberTrade = valuesMyTrade[1];
                                    myTrade.SecurityNameCode = valuesMyTrade[2];
                                    myTrade.Side = valuesMyTrade[5].Contains("-") ? Side.Sell : Side.Buy;
                                    myTrade.Volume = Math.Abs(valuesMyTrade[5].ToDecimal());
                                    myTrade.Time = new DateTime(1970, 01, 01) + TimeSpan.FromSeconds(Convert.ToDouble(valuesMyTrade[3]));
                                    myTrade.NumberOrderParent = valuesMyTrade[4];

                                    if (MyTradeEvent != null)
                                    {
                                        MyTradeEvent(myTrade);
                                    }
                                }
                            }
                            else if (mes.Contains("\"ws\"")) // snapshot of portfolios / снимок портфелей
                            {
                                lock (_senderLocker)
                                {
                                    var res = Walets.FromJson(mes)[2].AllWallets;

                                    if (NewPortfolio != null)
                                    {
                                        NewPortfolio(res);
                                    }
                                }
                            }
                            else if (mes.Contains("\"wu\"")) // portfolio update / обновление портфеля
                            {
                                lock (_senderLocker)
                                {
                                    var res = WalletUpdate.FromJson(mes)[2].UpdatedWallet; ;

                                    if (UpdatePortfolio != null)
                                    {
                                        UpdatePortfolio(res);
                                    }
                                }
                            }
                            else if (mes.Contains("[0,\"ps\",[")) // shapshot of position / снимок позиций
                            {
                                
                            }
                            else if (mes.Contains("\"pn\"")|| mes.Contains("\"pu\"")|| mes.Contains("\"pc\"")) // снимок позиций
                            {

                            }
                            else if (mes.Contains("\"event\":\"subscribed\"") && mes.Contains("\"chanId\""))
                            {
                                lock (_senderLocker)
                                {
                                    // inform about successful subscription / информируем об успешной подписке
                                    var info = JsonConvert.DeserializeAnonymousType(mes, new SubscriptionInformation());

                                    if (info.channel == "trades")
                                    {
                                        _subscribedTradesSecurity.Add(info.pair, info.chanId);
                                    }

                                    if (info.channel == "book")
                                    {
                                        _subscribedBooksSecurity.Add(info.pair, info.chanId);
                                    }

                                    string msgInfo =
                                        string.Format(
                                            "Инструмент {0} успешно подписан на канал : {1}, Id канала = {2}", info.pair,
                                            info.channel, info.chanId);

                                    SendLogMessage(msgInfo, LogMessageType.System);

                                }
                            }
                            else if (mes.Contains("\"tu\"")) // new tick / новый тик
                            {
                                lock (_senderLocker)
                                {
                                    var bitfinexTick = UpdateDataBitfinex.FromJson(mes);

                                    // find the security that owns this snapshot / находим бумагу которой принадлежит этот снимок
                                    var namePair =
                                        _subscribedTradesSecurity.FirstOrDefault(
                                            dic => dic.Value == Convert.ToInt32(bitfinexTick[0].Double));

                                    if (NewTradesEvent != null)
                                    {
                                        NewTradesEvent(bitfinexTick, namePair.Key);
                                    }
                                }
                            }
                            else if (mes.Contains("[["))
                            {
                                lock (_senderLocker)
                                {
                                    var countParams = NumberParametersSnapshot(mes);

                                    if (countParams == 3)
                                    {
                                        // process a shapshot of depth / обрабатываем снимок стакана

                                        var orderBook = BitfinexSnapshotParser.FromJson(mes);

                                        // find the security that owns this snapshot / находим бумагу которой принадлежит этот снимок
                                        var namePair =
                                            _subscribedBooksSecurity.FirstOrDefault(
                                                dic => dic.Value == Convert.ToInt32(orderBook[0].IdChanel));

                                        if (NewMarketDepth != null)
                                        {
                                            NewMarketDepth(orderBook, namePair.Key);
                                        }
                                    }
                                }
                            }
                            else if (mes.Contains("hb"))
                            {
                                // heartbeat message came / пришло сообщение серцебиения
                            }
                            else if (!mes.Contains("[[") && !mes.Contains("\"te\"") && !mes.Contains("\"ws\""))
                            {
                                lock (_senderLocker)
                                {
                                    var bitfinexChangeOrderBook = UpdateDataBitfinex.FromJson(mes);

                                    // find the security that owns this snapshot / находим бумагу которой принадлежит этот снимок
                                    var namePair =
                                        _subscribedBooksSecurity.FirstOrDefault(
                                            dic => dic.Value == Convert.ToInt32(bitfinexChangeOrderBook[0].Double));

                                    if (UpdateMarketDepth != null)
                                    {
                                        UpdateMarketDepth(bitfinexChangeOrderBook, namePair.Key);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }

                catch (Exception exception)
                {
                    SendLogMessage(exception.Message, LogMessageType.Connect);
                }                
            }
        }

        /// <summary>
        /// parse data 
        /// распарсить данные
        /// </summary>
        private string[] ParseData(string msg)
        {
            var iterFirst = msg.Substring(9);

            int index = iterFirst.Length - 2;

            var iterSecond = iterFirst.Remove(index).Replace("\"", "");

            string[] values = iterSecond.Split(',');

            return values;
        }

        /// <summary>
        /// find the number of parameters in the subarray to determine which channel the message belongs to
        /// находит кол-во параметров в подмассиве чтобы определить к какому каналу принадлежит сообщение
        /// </summary>
        /// <param name="msg">string for parsing / строка которую нужно распарсить </param>
        /// <returns>parameter count, if it's 3, then it's a depth snapshot, if 4 - trade snapshot / кол-во параметров, если 3 значит это снимок стакана, если 4 значит снимок трейдов</returns>
        private int NumberParametersSnapshot(string msg)
        {
            // Find the index of the beginning of the first subarray by searching for double square opening brackets / Находим индекс начала первого подмассива поиском двойных квадратных открывающихся скобок
            int indexStart = msg.IndexOf("[[", StringComparison.Ordinal);

            // find the index of the end of the subarray / находим индекс конца подмассива
            int infexEnd = msg.IndexOf("]", StringComparison.Ordinal);

            // cut the first subarray / вырезаем первый подмассив
            var str = msg.Substring(indexStart, infexEnd-indexStart);

            // split it by commas / делим его по запятым
            var res = str.Split(new char[] { ',' });
            
            // send count of parameters / отправляем кол-во параметров
            int countParams = res.Length;

            return countParams;
        }

        #endregion

        #region outgoing messages / исходящие события

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

        /// <summary>
        /// new portfolios
        /// новые портфели
        /// </summary>
        public event Action<List<List<WaletWalet>>> NewPortfolio;

        /// <summary>
        /// portfolios updated
        /// обновились портфели
        /// </summary>
        public event Action<List<WalletUpdateWalletUpdate>> UpdatePortfolio;

        /// <summary>
        /// depth updated
        /// обновился стакан
        /// </summary>
        public event Action<List<DataObject>, string> NewMarketDepth;

        /// <summary>
        /// depth updated
        /// обновился стакан
        /// </summary>
        public event Action<List<ChangedElement>,string> UpdateMarketDepth;

        /// <summary>
        /// ticks updated
        /// обновились тики
        /// </summary>
        public event Action<List<ChangedElement>, string> NewTradesEvent;

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

        #endregion

    }


}
