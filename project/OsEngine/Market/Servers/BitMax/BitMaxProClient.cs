using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using WebSocket4Net;

namespace OsEngine.Market.Servers.BitMax
{
    public class BitMaxProClient
    {
        public BitMaxProClient(string pubKey, string secKey)
        {
            _apiKey = pubKey;
            _secretKey = secKey;
        }

        private readonly string _apiKey;
        private readonly string _secretKey;
        //private readonly bool _isMargin;

        private readonly string _baseUrl = "https://bitmax.io/";

        /// <summary>
        /// connecto to the exchange
        /// установить соединение с биржей 
        /// </summary>
        public void Connect()
        {
            if (string.IsNullOrEmpty(_apiKey) ||
                string.IsNullOrEmpty(_secretKey))
            {
                return;
            }

            // check server availability for HTTP communication with it / проверяем доступность сервера для HTTP общения с ним
            Uri uri = new Uri(_baseUrl + "api/pro/v1/assets");
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
                HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            }
            catch (Exception)
            {
                SendLogMessage("Server is not available. No internet available. ", LogMessageType.Error);
                return;
            }

            _accountGroup = GetAccountGroup().Account.AccountGroup;

            IsConnected = true;

            if (Connected != null)
            {
                Connected();
            }

            Thread converter = new Thread(Converter);
            converter.CurrentCulture = new CultureInfo("ru-RU");
            converter.IsBackground = true;
            converter.Start();

            Thread publicConverter = new Thread(PublicDataConverter);
            publicConverter.CurrentCulture = new CultureInfo("ru-RU");
            publicConverter.IsBackground = true;
            publicConverter.Start();

            CreateDataStream();
            Thread.Sleep(1000);
            CreateUserDataStream();
        }

        /// <summary>
        /// shows whether connection works
        /// работает ли соединение
        /// </summary>
        public bool IsConnected;

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
                _wsClient.Opened -= StreamConnect;
                _wsClient.Closed -= Disconnect;
                _wsClient.Error -= WsError;
                _wsClient.MessageReceived -= GetRes;
                _wsClient.Close();
                _wsClient.Dispose();
            }

            if (_privateStream != null)
            {
                _privateStream.Opened -= Connect;
                _privateStream.Closed -= Disconnect;
                _privateStream.Error -= WsError;
                _privateStream.MessageReceived -= GetResUserData;
                _privateStream.Close();
                _privateStream.Dispose();
            }

            IsConnected = false;
            _isDisposed = true;
        }

        private int _accountGroup;

        /// <summary>
        /// получить групу, к которой принадлежит аккаунт
        /// </summary>
        private AccountGroup GetAccountGroup()
        {
            var userInfoMsg = GetData("info", true);

            return JsonConvert.DeserializeAnonymousType(userInfoMsg, new AccountGroup());
        }

        public void GetSecurities()
        {
            try
            {
                var result = GetData("products");
                var securities = JsonConvert.DeserializeAnonymousType(result, new RootProducts());

                UpdateSecurities?.Invoke(securities);
            }
            catch (Exception e)
            {
                SendLogMessage("An error occurred while trying to get securities " + e.Message, LogMessageType.Error);
            }
        }

        public void GetPortfolios()
        {
            try
            {
                var result = GetData( "margin/balance" , true, _accountGroup.ToString());

                var portfolios = JsonConvert.DeserializeAnonymousType(result, new Wallets());

                NewPortfoliosEvent?.Invoke(portfolios);

                result = GetData("cash/balance", true, _accountGroup.ToString());

                portfolios = JsonConvert.DeserializeAnonymousType(result, new Wallets());

                NewSpotPortfoliosEvent?.Invoke(portfolios);
            }
            catch (Exception e)
            {
                SendLogMessage("An error occurred while trying to retrieve a portfolio. " + e.Message, LogMessageType.Error);
            }
        }

        public string GetRefPrices()
        {
            try
            {
                var result = GetData("margin/ref-price");
                return result;
            }
            catch (Exception e)
            {
                SendLogMessage("An error occurred while trying to retrieve a ref-price. " + e.Message, LogMessageType.Error);
                return null;
            }
        }


        string _candlesEndPoint = "barhist";

        public CandlesInfo GetCandles(string security, string timeFrame, long startTimeStamp, long endTimeStamp)
        {
            try
            {
                string candlesQuery = $"?symbol={security}&from={startTimeStamp}&to={endTimeStamp}&interval={timeFrame}";

                var res = GetData(_candlesEndPoint + candlesQuery);

                return JsonConvert.DeserializeObject<CandlesInfo>(res);
            }
            catch (Exception e)
            {
                SendLogMessage("Error getting candles.  " + e.Message, LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// execute order
        /// исполнить ордер
        /// </summary>
        public OrderPlaceResponse SendOrder(Order order, string needId)
        {
            var time = TimeManager.GetUnixTimeStampMilliseconds();

            JsonObject jsonContent = new JsonObject();
            jsonContent.Add("id", needId);
            jsonContent.Add("time", time);
            jsonContent.Add("symbol", order.SecurityNameCode.Replace('-', '/'));
            jsonContent.Add("orderQty", order.Volume.ToString(CultureInfo.InvariantCulture)
                .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, "."));

            if (order.TypeOrder == OrderPriceType.Limit)
            {
                jsonContent.Add("orderPrice", order.Price.ToString(CultureInfo.InvariantCulture)
                    .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, "."));
            }
            string orderType = order.TypeOrder == OrderPriceType.Limit ? "limit" :
                order.TypeOrder == OrderPriceType.Market ? "market" : "none";

            jsonContent.Add("orderType", orderType);
            jsonContent.Add("side", order.Side == Side.Buy ? "buy" : "sell");

            string endPoint = order.PortfolioNumber == "BitMaxMargin" ? "margin/order" : "cash/order";

            var res = GetData(endPoint, true, _accountGroup.ToString(), jsonContent.ToString(), needId, time.ToString(), Method.POST);

            if (res == null)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<OrderPlaceResponse>(res);
        }

        public OrdersHistoryResult CheckOrderState(Order order)
        {
            //GET <account-group>/api/pro/v1/{account-category}/order/hist/current
            var time = TimeManager.GetUnixTimeStampMilliseconds();

            JsonObject jsonContent = new JsonObject();
            jsonContent.Add("n", 10);
            jsonContent.Add("symbol", order.SecurityNameCode.Replace('-', '/'));


            string endPoint = order.PortfolioNumber == "BitMaxMargin" ? "margin/order/hist/current" : "cash/order/hist/current";

            var res = GetData(endPoint, true, _accountGroup.ToString(), null, null, time.ToString(), need: true);

            return JsonConvert.DeserializeObject<OrdersHistoryResult>(res);
        }

        /// <summary>
        /// cancel order
        /// отменить ордер
        /// </summary>
        public OrderPlaceResponse CancelOrder(Order order, string needId)
        {
            var time = TimeManager.GetUnixTimeStampMilliseconds();

            JsonObject jsonContent = new JsonObject();
            jsonContent.Add("id", needId);
            jsonContent.Add("time", time);
            jsonContent.Add("symbol", order.SecurityNameCode);
            jsonContent.Add("orderId", order.NumberMarket);

            string endPoint = order.PortfolioNumber == "BitMaxMargin" ? "margin/order" : "cash/order";

            var res = GetData(endPoint, true, _accountGroup.ToString(), jsonContent.ToString(), needId, time.ToString(), Method.DELETE);

            return JsonConvert.DeserializeObject<OrderPlaceResponse>(res);
        }

        /// <summary>
        /// request order status
        /// запросить статус ордера
        /// </summary>
        /// <param name="clientOrderId"></param>
        /// <returns></returns>
        public OrderState GetOrderState(string clientOrderId)
        {
            var timeStamp = TimeManager.GetUnixTimeStampMilliseconds();
            var result = GetData("order/" + clientOrderId, true, _accountGroup.ToString(), time: timeStamp.ToString());
            return JsonConvert.DeserializeObject<OrderState>(result);
        }

        private readonly object _queryLocker = new object();

        /// <summary>
        /// request data from the server
        /// запросить данные с сервера
        /// </summary>
        /// <param name="apiPath">end point / конечная точка</param>
        /// <param name="auth">flag, signed request or not / флаг, подписанный запрос или нет</param>
        /// <param name="accGroup">account group / група аккаунта</param>
        /// <param name="jsonContent">content sent to server / контент, отправляемый серверу</param>
        /// <param name="orderId">order identifier / идентификатор ордера</param>
        /// <param name="time">timestamp / временная метка</param>
        /// <param name="method">request method / метод запроса</param>
        /// <returns>server response / ответ от сервера</returns>
        private string GetData(string apiPath, bool auth = false, string accGroup = null, string jsonContent = null,
            string orderId = null, string time = null, Method method = Method.GET, bool need = false)
        {
            lock (_queryLocker)
            {
                try
                {
                    Uri uri;

                    HttpWebRequest httpWebRequest;

                    if (!auth)
                    {
                        uri = new Uri(_baseUrl + "api/pro/v1/" + apiPath);

                        httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
                    }
                    else
                    {
                        if (accGroup == null)
                        {
                            uri = new Uri(_baseUrl + "api/pro/v1/" + apiPath);
                        }
                        else
                        {
                            var str = _baseUrl + accGroup + "/" + "api/pro/v1/" + apiPath;

                            if (need)
                            {
                                str += "?n=10&executedOnly=True";
                            }

                            uri = new Uri(str);
                        }

                        string timestamp;

                        if (time == null)
                        {
                            timestamp = TimeManager.GetUnixTimeStampMilliseconds().ToString();
                        }
                        else
                        {
                            timestamp = time;
                        }

                        string signatureMsg;

                        if (orderId == null)
                        {
                            signatureMsg = timestamp + "+" + apiPath;
                        }
                        else
                        {
                            //signatureMsg = timestamp + "+" + apiPath + "+" + orderId;
                            signatureMsg = timestamp + "+" + "order";
                        }

                        if (signatureMsg.EndsWith("cash/balance"))
                        {
                            signatureMsg = signatureMsg.Replace("cash/", "");
                            //signatureMsg = signatureMsg.Remove(signatureMsg.Length - 11, 4);
                        }

                        if (signatureMsg.EndsWith("margin/balance"))
                        {
                            signatureMsg = signatureMsg.Replace("margin/", "");
                            //signatureMsg = signatureMsg.Remove(signatureMsg.Length - 13, 6);
                        }


                        var codedSignature = CreateSignature(signatureMsg);

                        httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);

                        httpWebRequest.Headers.Add("x-auth-key", _apiKey);
                        httpWebRequest.Headers.Add("x-auth-signature", codedSignature);
                        httpWebRequest.Headers.Add("x-auth-timestamp", timestamp.ToString());

                        if (orderId != null)
                        {
                            httpWebRequest.Headers.Add("x-auth-coid", orderId);
                        }
                    }

                    httpWebRequest.Method = method.ToString();

                    if (jsonContent != null)
                    {
                        var data = Encoding.UTF8.GetBytes(jsonContent);

                        httpWebRequest.ContentType = "application/json";

                        httpWebRequest.ContentLength = data.Length;

                        using (Stream requestStream = httpWebRequest.GetRequestStream())
                        {
                            requestStream.Write(data, 0, data.Length);
                        }
                    }

                    HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();

                    string responseMsg;

                    using (var stream = httpWebResponse.GetResponseStream())
                    {
                        using (StreamReader reader = new StreamReader(stream ?? throw new InvalidOperationException()))
                        {
                            responseMsg = reader.ReadToEnd();
                        }
                    }

                    httpWebResponse.Close();

                    return responseMsg;
                }
                catch (InvalidOperationException invalidOperationException)
                {
                    SendLogMessage("Failed to get stream to read response from server..   " + invalidOperationException.Message, LogMessageType.Error);
                    return null;
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.Message, LogMessageType.Error);
                    return null;
                }
            }
        }

        #region Data streams

        /// <summary>
        /// subscribe this security to get depths and trades
        /// подписать данную бумагу на получение стаканов и трейдов
        /// </summary>
        public void SubscribeTradesAndDepths(string security)
        {
            var subMsg = SubscribeMsg.Replace("message",$"depth:{security}");
            _wsClient?.Send(subMsg);

            subMsg = SubscribeMsg.Replace("message", $"trades:{security}");
            _wsClient?.Send(subMsg);
        }

        private WebSocket _wsClient;

        private string _wsUri = "wss://bitmax.io/";

        /// <summary>
        /// create data stream
        /// создать поток данных
        /// </summary>
        private void CreateDataStream()
        {
            _wsClient = new WebSocket(_wsUri + "api/pro/v1/stream");

            _wsClient.Opened += StreamConnect;

            _wsClient.Closed += Disconnect;

            _wsClient.Error += WsError;

            _wsClient.MessageReceived += GetRes;

            _wsClient.Open();
        }

        /// <summary>
        /// ws-connection is opened
        /// соединение по ws открыто
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StreamConnect(object sender, EventArgs e)
        {
            SendLogMessage("Public data channel open: " + e, LogMessageType.Connect);
        }

        /// <summary>
        /// error from ws4net
        /// ошибка из ws4net
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WsError(object sender, EventArgs e)
        {
            SendLogMessage("Error from ws4net : " + e, LogMessageType.Error);
        }

        private WebSocket _privateStream;

        /// <summary>
        /// create data stream
        /// создать поток данных
        /// </summary>
        private void CreateUserDataStream()
        {
            //var timeStamp = TimeManager.GetUnixTimeStampMilliseconds();
            //var msg = $"{timeStamp}+api/pro/v1/stream";

            //List<KeyValuePair<string, string>> headers = new List<KeyValuePair<string, string>>();

            //headers.Add(new KeyValuePair<string, string>("x-auth-key", _apiKey));
            //headers.Add(new KeyValuePair<string, string>("x-auth-signature", CreateSignature(msg)));
            //headers.Add(new KeyValuePair<string, string>("x-auth-timestamp", timeStamp.ToString()));

            //_privateStream = new WebSocket(_wsUri + _accountGroup + "/api/pro/v1/stream", customHeaderItems: headers);

            _privateStream = new WebSocket(_wsUri + _accountGroup + "/api/pro/v1/stream");

            _privateStream.Opened += Connect;

            _privateStream.Closed += Disconnect;

            _privateStream.Error += WsPrivateError;

            _privateStream.MessageReceived += GetResUserData;

            _privateStream.Open();

        }

        private void WsPrivateError(object sender, EventArgs e)
        {
            SendLogMessage("Error from private ws4net : " + e, LogMessageType.Error);
        }

        private string _privateSubscribeMsg = "{ \"op\": \"sub\", \"ch\":\"order:*\" }";

        /// <summary>
        /// ws-connection is opened
        /// соединение по ws открыто
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Connect(object sender, EventArgs e)
        {
            //{"op":"auth", "t": 1596191045220, "key": "8NGs9CWYwP9XZq3aIsl1EGtgOg6XMy1H", "sig": "n6GO+C3LxF5PspGVp+VV4QuBRJUX6GSAJBEbbrCHgU0="}
            var timeStamp = TimeManager.GetUnixTimeStampMilliseconds();

            var msg = $"{timeStamp}+stream";

            var auth = $"{{\"op\":\"auth\", \"t\": {timeStamp}, \"key\": \"{_apiKey}\", \"sig\": \"{CreateSignature(msg)}\"}}";

            _privateStream.Send(auth);
            
            var subStr = _privateSubscribeMsg.Replace("*", "margin");

            _privateStream.Send(subStr);

            subStr = _privateSubscribeMsg.Replace("*", "cash");

            _privateStream.Send(subStr);
        }

        private const string SubscribeMsg = "{\"op\":\"sub\" ,\"ch\":\"message\",  }";

        /// <summary>
        /// ws-connection is closed
        /// соединение по ws закрыто
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Disconnect(object sender, EventArgs e)
        {
            if (IsConnected)
            {
                IsConnected = false;

                if (Disconnected != null)
                {
                    Disconnected();
                }
            }
        }

        /// <summary>
        /// queue of new messages from the exchange server
        /// очередь новых сообщений, пришедших с сервера биржи
        /// </summary>
        private readonly ConcurrentQueue<string> _newMessage = new ConcurrentQueue<string>();
        
        /// <summary>
        /// takes messages that came through ws and puts them in a general queue
        /// берет пришедшие через ws сообщения и кладет их в общую очередь
        /// </summary>        
        private void GetRes(object sender, MessageReceivedEventArgs e)
        {
            if (_isDisposed)
            { 
                return;
            }
            _newMessage.Enqueue(e.Message);
        }

        /// <summary>
        /// takes messages that came through ws and puts them in a general queue
        /// берет пришедшие через ws сообщения и кладет их в общую очередь
        /// </summary>        
        private void GetResUserData(object sender, MessageReceivedEventArgs e)
        {
            if (_isDisposed)
            {
                return;
            }
            _newPrivateMessage.Enqueue(e.Message);
        }

        private BitMaxMarketDepthCreator _depthCreator = new BitMaxMarketDepthCreator();

        private readonly ConcurrentQueue<string> _newPrivateMessage = new ConcurrentQueue<string>();
        
        /// <summary>
        /// takes messages from the general queue, converts them to C # classes and sends them to up
        /// берет сообщения из общей очереди, конвертирует их в классы C# и отправляет на верх
        /// </summary>
        public void Converter()
        {
            while (true)
            {
                try
                {
                    if (!_newPrivateMessage.IsEmpty)
                    {
                        string mes;

                        if (_newPrivateMessage.TryDequeue(out mes))
                        {
                            if (mes.StartsWith("{\"m\"" + ":" + "\"ping\""))
                            {
                                _privateStream.Send("{ \"op\": \"pong\" }");
                                continue;
                            }
                            if (mes.StartsWith("{\"m\"" + ":" + "\"order\""))
                            {
                                var order = JsonConvert.DeserializeAnonymousType(mes, new OrderState());

                                if (MyOrderEvent != null)
                                {
                                    MyOrderEvent(order);
                                }
                                continue;
                            }
                            if (mes.StartsWith("{\"m\"" + ":" + "\"disconnected\""))
                            {
                                Disconnected?.Invoke();
                            }
                            if (mes.Contains("error"))
                            {
                                SendLogMessage(mes, LogMessageType.Error);
                            }
                        }
                    }
                    else
                    {
                        if (_isDisposed)
                        {
                            return;
                        }
                        Thread.Sleep(1);
                    }
                }

                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }

        public void PublicDataConverter()
        {
            while (true)
            {
                try
                {
                    if (!_newMessage.IsEmpty)
                    {
                        string mes;

                        if (_newMessage.TryDequeue(out mes))
                        {
                            if (mes.StartsWith("{\"m\"" + ":" + "\"ping\""))
                            {
                                _wsClient.Send("{ \"op\": \"pong\" }");
                                continue;
                            }
                            if (mes.StartsWith("{\"m\"" + ":" + "\"depth\""))
                            {
                                if (UpdateMarketDepth != null)
                                {
                                    UpdateMarketDepth(_depthCreator.Create(mes));
                                }
                                continue;
                            }
                            if (mes.StartsWith("{\"m\"" + ":" + "\"trades\""))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new TradeInfo());

                                if (NewTradesEvent != null)
                                {
                                    NewTradesEvent(quotes);
                                }
                                continue;
                            }
                            if (mes.StartsWith("{\"m\":\"summary\""))
                            {
                                continue;
                            }
                            if (mes.StartsWith("{\"m\":\"bar\""))
                            {
                                continue;
                            }
                            if (mes.Contains("error"))
                            {
                                SendLogMessage(mes, LogMessageType.Error);
                            }
                        }
                    }
                    else
                    {
                        if (_isDisposed)
                        {
                            return;
                        }
                        Thread.Sleep(1);
                    }
                }

                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }
        #endregion

        #region outgoing events / исходящие события

        /// <summary>
        /// my new orders
        /// новые мои ордера
        /// </summary>
        public event Action<OrderState> MyOrderEvent;

        /// <summary>
        /// new portfolios
        /// новые портфели
        /// </summary>
        public event Action<Wallets> NewPortfoliosEvent;

        /// <summary>
        /// new portfolios
        /// новые портфели
        /// </summary>
        public event Action<Wallets> NewSpotPortfoliosEvent;

        /// <summary>
        /// new security in the system
        /// новые бумаги в системе
        /// </summary>
        public event Action<RootProducts> UpdateSecurities;

        /// <summary>
        /// depth updated
        /// обновился стакан
        /// </summary>
        public event Action<MarketDepth> UpdateMarketDepth;

        /// <summary>
        /// ticks updated
        /// обновились тики
        /// </summary>
        public event Action<TradeInfo> NewTradesEvent;

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

        #region log messages / сообщения для лога

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

        private string CreateSignature(string message)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var keyBytes = Encoding.UTF8.GetBytes(_secretKey);
            byte[] computedHash;
            using (var hash = new HMACSHA256(keyBytes))
            {
                computedHash = hash.ComputeHash(messageBytes);
            }
            return Convert.ToBase64String(computedHash);
        }
    }
}
