using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using WebSocket4Net;


namespace OsEngine.Market.Servers.BitMax
{

    public class BitMaxClient
    {
        public BitMaxClient(string pubKey, string secKey, bool isMargin)
        {
            _apiKey = pubKey;
            _secretKey = secKey;
            _isMargin = isMargin;
        }

        private readonly string _apiKey;
        private readonly string _secretKey;
        private readonly bool _isMargin;

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
            Uri uri = new Uri(_baseUrl + "api/v1/assets");
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

            _accountGroup = GetAccountGroup();

            IsConnected = true;

            if (Connected != null)
            {
                Connected();
            }

            Thread converter = new Thread(Converter);
            converter.CurrentCulture = new CultureInfo("ru-RU");
            converter.IsBackground = true;
            converter.Start();

            CreateUserDataStream("ETH-BTC");
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
            foreach (var ws in _wsStreams)
            {
                ws.Opened -= StreamConnect;
                ws.Closed -= Disconnect;
                ws.Error -= WsError;
                ws.MessageReceived -= GetRes;

                ws.Close();
                ws.Dispose();
            }
            _wsStreams.Clear();
            IsConnected = false;
            _isDisposed = true;

            if (Disconnected != null)
            {
                Disconnected();
            }
        }

        private int _accountGroup;

        /// <summary>
        /// получить групу, к которой принадлежит аккаунт
        /// </summary>
        private int GetAccountGroup()
        {
            var userInfoMsg = GetData("user/info", true);

            return JsonConvert.DeserializeAnonymousType(userInfoMsg, new AccGroup()).AccountGroup;
        }

        public void GetSecurities()
        {
            try
            {
                var result = GetData("products");
                var securities = JsonConvert.DeserializeAnonymousType(result, new List<Product>());

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
                var result = GetData("balance", true, _accountGroup.ToString());
                var portfolios = JsonConvert.DeserializeAnonymousType(result, new Accaunt());

                NewPortfoliosEvent?.Invoke(portfolios);
            }
            catch (Exception e)
            {
                SendLogMessage("An error occurred while trying to retrieve a portfolio. " + e.Message, LogMessageType.Error);
            }
        }

        string _candlesEndPoint = "barhist";

        public List<BitMaxCandle> GetCandles(string security, string timeFrame, long startTimeStamp, long endTimeStamp)
        {
            try
            {
                string candlesQuery = $"?symbol={security}&from={startTimeStamp}&to={endTimeStamp}&interval={timeFrame}";

                var res = GetData(_candlesEndPoint + candlesQuery);

                return JsonConvert.DeserializeObject<List<BitMaxCandle>>(res);
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
        /// <param name="order"></param>
        public OrderSendResult SendOrder(Order order, string needId)
        {
            var time = TimeManager.GetUnixTimeStampMilliseconds();

            JsonObject jsonContent = new JsonObject();
            jsonContent.Add("coid", needId);
            jsonContent.Add("time", time);
            jsonContent.Add("symbol", order.SecurityNameCode.Replace('-', '/'));
            jsonContent.Add("orderPrice", order.Price.ToString(CultureInfo.InvariantCulture)
                .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, "."));
            jsonContent.Add("orderQty", order.Volume.ToString(CultureInfo.InvariantCulture)
                .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, "."));
            jsonContent.Add("orderType", order.TypeOrder == OrderPriceType.Limit ? "limit" : "market");
            jsonContent.Add("side", order.Side == Side.Buy ? "buy" : "sell");

            string endPoint = _isMargin ? "margin/order" : "order";

            var res = GetData(endPoint, true, _accountGroup.ToString(), jsonContent.ToString(), needId, time.ToString(), Method.POST);

            return JsonConvert.DeserializeObject<OrderSendResult>(res);
        }

        /// <summary>
        /// cancel order
        /// отменить ордер
        /// </summary>
        public OrderSendResult CancelOrder(Order order, string needId)
        {
            var time = TimeManager.GetUnixTimeStampMilliseconds();

            JsonObject jsonContent = new JsonObject();
            jsonContent.Add("coid", needId);
            jsonContent.Add("time", time);
            jsonContent.Add("symbol", order.SecurityNameCode.Replace('-', '/'));
            jsonContent.Add("origCoid", order.NumberMarket);

            string endPoint = _isMargin ? "margin/order" : "order";

            var res = GetData(endPoint, true, _accountGroup.ToString(), jsonContent.ToString(), needId, time.ToString(), Method.DELETE);

            return JsonConvert.DeserializeObject<OrderSendResult>(res);
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
        private string GetData(string apiPath, bool auth = false, string accGroup = null, string jsonContent = null, string orderId = null, string time = null, Method method = Method.GET)
        {
            lock (_queryLocker)
            {
                try
                {
                    Uri uri;

                    HttpWebRequest httpWebRequest;

                    if (!auth)
                    {
                        uri = new Uri(_baseUrl + "api/v1/" + apiPath);

                        httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
                    }
                    else
                    {
                        if (accGroup == null)
                        {
                            uri = new Uri(_baseUrl + "api/v1/" + apiPath);
                        }
                        else
                        {
                            uri = new Uri(_baseUrl + accGroup + "/" + "api/v1/" + apiPath);
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
                            signatureMsg = timestamp + "+" + apiPath + "+" + orderId;
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
        /// data stream collection
        /// коллекция потоков данных
        /// </summary>
        private List<WebSocket> _wsStreams = new List<WebSocket>();

        private string _wsUri = "wss://bitmax.io/";

        /// <summary>
        /// create data stream
        /// создать поток данных
        /// </summary>
        private void CreateDataStream(string security)
        {

            WebSocket wsClient = new WebSocket(_wsUri + "/api/public/" + security);

            wsClient.Opened += StreamConnect;

            wsClient.Closed += Disconnect;

            wsClient.Error += WsError;

            wsClient.MessageReceived += GetRes;

            wsClient.Open();

            _wsStreams.Add(wsClient);
        }

        private WebSocket _privateStream;

        /// <summary>
        /// create data stream
        /// создать поток данных
        /// </summary>
        private void CreateUserDataStream(string security)
        {
            var timeStamp = TimeManager.GetUnixTimeStampMilliseconds();
            var msg = $"{timeStamp}+api/stream";

            List<KeyValuePair<string, string>> headers = new List<KeyValuePair<string, string>>();

            headers.Add(new KeyValuePair<string, string>("x-auth-key", _apiKey));
            headers.Add(new KeyValuePair<string, string>("x-auth-signature", CreateSignature(msg)));
            headers.Add(new KeyValuePair<string, string>("x-auth-timestamp", timeStamp.ToString()));

            _privateStream = new WebSocket(_wsUri + _accountGroup + "/api/stream/" + security, customHeaderItems: headers);

            _privateStream.Opened += Connect;

            _privateStream.Closed += Disconnect;

            _privateStream.Error += WsError;

            _privateStream.MessageReceived += GetRes;

            _privateStream.Open();

            _wsStreams.Add(_privateStream);
        }

        private const string SubscribeMsg = "{\"messageType\": \"subscribe\" ,\"marketDepthLevel\": 20 }";

        /// <summary>
        /// ws-connection is opened
        /// соединение по ws открыто
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Connect(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// ws-connection is opened
        /// соединение по ws открыто
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StreamConnect(object sender, EventArgs e)
        {
            var stream = (WebSocket)sender;
            var needStream = _wsStreams.Find(ws => ws.Equals(stream));

            if (needStream == null)
            {
                return;
            }

            needStream.Send(SubscribeMsg);
        }

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
        /// error from ws4net
        /// ошибка из ws4net
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WsError(object sender, EventArgs e)
        {
            SendLogMessage("Error from ws4net : " + e, LogMessageType.Error);
        }

        /// <summary>
        /// queue of new messages from the exchange server
        /// очередь новых сообщений, пришедших с сервера биржи
        /// </summary>
        private ConcurrentQueue<string> _newMessage = new ConcurrentQueue<string>();

        private readonly object _resLocker = new object();

        /// <summary>
        /// takes messages that came through ws and puts them in a general queue
        /// берет пришедшие через ws сообщения и кладет их в общую очередь
        /// </summary>        
        private void GetRes(object sender, MessageReceivedEventArgs e)
        {
            lock (_resLocker)
            {
                if (_isDisposed)
                {
                    return;
                }
                _newMessage.Enqueue(e.Message);
            }
        }

        /// <summary>
        /// subscribe this security to get depths and trades
        /// подписать данную бумагу на получение стаканов и трейдов
        /// </summary>
        public void SubscribeTradesAndDepths(string security)
        {
            CreateDataStream(security);
        }

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
                    if (!_newMessage.IsEmpty)
                    {
                        string mes;

                        if (_newMessage.TryDequeue(out mes))
                        {
                            if (mes.StartsWith("{\"m\"" + ":" + "\"depth\""))
                            {
                                var depth = JsonConvert.DeserializeAnonymousType(mes, new Depth());

                                if (UpdateMarketDepth != null)
                                {
                                    UpdateMarketDepth(depth);
                                }
                                continue;
                            }

                            if (mes.StartsWith("{\"m\"" + ":" + "\"marketTrades\""))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new Trades());

                                if (NewTradesEvent != null)
                                {
                                    NewTradesEvent(quotes);
                                }
                                continue;
                            }

                            if (mes.StartsWith("{\"m\"" + ":" + "\"order\""))
                            {
                                var order = JsonConvert.DeserializeAnonymousType(mes, new BitMaxOrder());

                                if (NewTradesEvent != null)
                                {
                                    MyOrderEvent(order);
                                }
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
        public event Action<BitMaxOrder> MyOrderEvent;

        /// <summary>
        /// new portfolios
        /// новые портфели
        /// </summary>
        public event Action<Accaunt> NewPortfoliosEvent;

        /// <summary>
        /// new security in the system
        /// новые бумаги в системе
        /// </summary>
        public event Action<List<Product>> UpdateSecurities;

        /// <summary>
        /// depth updated
        /// обновился стакан
        /// </summary>
        public event Action<Depth> UpdateMarketDepth;

        /// <summary>
        /// ticks updated
        /// обновились тики
        /// </summary>
        public event Action<Trades> NewTradesEvent;

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
