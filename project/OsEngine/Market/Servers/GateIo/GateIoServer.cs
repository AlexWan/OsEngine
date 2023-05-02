using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.GateIo
{
    class GateIoServer : AServer
    {
        public GateIoServer()
        {
            ServerRealization = new GateIoServerRealization();
            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
        }

        /// <summary>
        /// instrument history query
        /// запрос истории по инструменту
        /// </summary>
        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            return ((GateIoServerRealization)ServerRealization).GetCandleHistory(nameSec, tf);
        }
    }

    public sealed class GateIoServerRealization : IServerRealization
    {
        private string _publicKey;
        private string _secretKey;

        private WsSource _wsSource;

        private const string WsUri = "wss://api.gateio.ws/ws/v4/";

        private readonly ConcurrentQueue<string> _queueMessagesReceivedFromExchange = new ConcurrentQueue<string>();

        private CancellationTokenSource _cancelTokenSource;

        /// <summary>
        /// словарь таймфреймов, поддерживаемых этой биржей
        /// </summary>
        private readonly Dictionary<int, string> _supportedIntervals;

        public ServerType ServerType
        {
            get { return ServerType.GateIo; }
        }

        public ServerConnectStatus ServerStatus { get; set; }
        public List<IServerParameter> ServerParameters { get; set; }
        public DateTime ServerTime { get; set; }

        public GateIoServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
            //_securitiesCreator = new GateSecurityCreator();
            //_portfolioCreator = new GatePortfolioCreator(PortfolioNumber);
            //_tradesCreator = new GateTradesCreator();
            //_marketDepthCreator = new GateMarketDepthCreator();
            //_orderCreator = new GateOrderCreator(PortfolioNumber);

            _supportedIntervals = CreateIntervalDictionary();
        }

        private Dictionary<int, string> CreateIntervalDictionary()
        {
            var dictionary = new Dictionary<int, string>();

            dictionary.Add(1, "60");
            dictionary.Add(3, "180");
            dictionary.Add(5, "300");
            dictionary.Add(15, "900");
            dictionary.Add(30, "1800");
            dictionary.Add(60, "3600");
            dictionary.Add(120, "7200");
            dictionary.Add(240, "14400");
            dictionary.Add(360, "21600");
            dictionary.Add(720, "43200");
            dictionary.Add(1440, "86400");

            return dictionary;
        }

        public void Connect()
        {
            try
            {
                Initialize();
            }
            catch (Exception e)
            {
                SendLogMessage("Gate io connect error: " + e.Message, LogMessageType.Error);
            }
        }

        private void Initialize()
        {

            _cancelTokenSource = new CancellationTokenSource();

            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

            if (string.IsNullOrEmpty(_publicKey) || string.IsNullOrEmpty(_secretKey))
            {
                throw new ArgumentException("Invalid key, connection terminated!");
            }

            StartMessageReader();

            _wsSource = new WsSource(WsUri);
            _wsSource.MessageEvent += WsSourceOnMessageEvent;
            _wsSource.Start();


            ServerStatus = ServerConnectStatus.Connect;
        }

        private void StartMessageReader()
        {
            Task.Run(() => MessageReader(_cancelTokenSource.Token), _cancelTokenSource.Token);
        }

        private void MessageReader(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!_queueMessagesReceivedFromExchange.IsEmpty)
                    {
                        string mes;

                        if (_queueMessagesReceivedFromExchange.TryDequeue(out mes))
                        {
                            ResponceWebsocketMessage<object> responceWebsocketMessage = JsonConvert.DeserializeAnonymousType(mes, new ResponceWebsocketMessage<object>());


                            if (responceWebsocketMessage.channel.Equals("spot.usertrades") &&
                                responceWebsocketMessage.Event.Equals("update"))
                            {

                                ResponceWebsocketMessage<List<MessageUserTrade>> responceDepths = JsonConvert.DeserializeAnonymousType(mes, new ResponceWebsocketMessage<List<MessageUserTrade>>());

                                for (int i = 0; i < responceDepths.result.Count; i++)
                                {
                                    var security = responceDepths.result[i].currency_pair;

                                    var time = Convert.ToInt64(responceDepths.result[i].create_time);

                                    var newTrade = new MyTrade();

                                    newTrade.Time = TimeManager.GetDateTimeFromTimeStampSeconds(time);
                                    newTrade.SecurityNameCode = security;
                                    newTrade.NumberOrderParent = responceDepths.result[i].order_id;
                                    newTrade.Price = responceDepths.result[i].price.ToDecimal();
                                    newTrade.NumberTrade = responceDepths.result[i].id;
                                    newTrade.Side = responceDepths.result[i].side.Equals("sell") ? Side.Sell : Side.Buy;
                                    newTrade.Volume = responceDepths.result[i].amount.ToDecimal();
                                    MyTradeEvent(newTrade);
                                }
                                continue;
                            }
                            else if (responceWebsocketMessage.channel.Equals("spot.order_book") &&
                                responceWebsocketMessage.Event.Equals("update"))
                            {
                                ResponceWebsocketMessage<MessageDepths> responceDepths = JsonConvert.DeserializeAnonymousType(mes, new ResponceWebsocketMessage<MessageDepths>());


                                try
                                {
                                    MarketDepth _depths = new MarketDepth();
                                    _depths.SecurityNameCode = responceDepths.result.s;

                                    List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                                    List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                                    for (int i = 0; i < responceDepths.result.asks.Count; i++)
                                    {
                                        ascs.Add(new MarketDepthLevel()
                                        {
                                            Ask = responceDepths.result.asks[i][1].ToDecimal(),
                                            Price = responceDepths.result.asks[i][0].ToDecimal()
                                        });
                                    }

                                    for (int i = 0; i < responceDepths.result.bids.Count; i++)
                                    {
                                        bids.Add(new MarketDepthLevel()
                                        {
                                            Bid = responceDepths.result.bids[i][1].ToDecimal(),
                                            Price = responceDepths.result.bids[i][0].ToDecimal()
                                        });
                                    }

                                    _depths.Asks = ascs;
                                    _depths.Bids = bids;

                                    _depths.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responceDepths.result.t));

                                    MarketDepthEvent(_depths);
                                    continue;
                                }
                                catch (Exception error)
                                {
                                    SendLogMessage($"{error.Message} { error.StackTrace}", LogMessageType.Error);
                                }

                            }
                            else if (responceWebsocketMessage.channel.Equals("spot.trades") &&
                                responceWebsocketMessage.Event.Equals("update"))
                            {

                                ResponceWebsocketMessage<MessagePublicTrades> responceTrades = JsonConvert.DeserializeAnonymousType(mes, new ResponceWebsocketMessage<MessagePublicTrades>());

                                Trade trade = new Trade();
                                trade.SecurityNameCode = responceTrades.result.currency_pair;

                                trade.Price = Convert.ToDecimal(responceTrades.result.price.Replace('.', ','));
                                trade.Id = responceTrades.result.id;
                                trade.Time = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(responceTrades.result.create_time));
                                trade.Volume = Convert.ToDecimal(responceTrades.result.amount.Replace('.', ','));
                                trade.Side = responceTrades.result.side.Equals("sell") ? Side.Sell : Side.Buy;

                                NewTradesEvent(trade);

                                continue;

                            }
                            else if (responceWebsocketMessage.channel.Equals("spot.orders") &&
                                responceWebsocketMessage.Event.Equals("update"))
                            {

                                ResponceWebsocketMessage<List<MessageUserOrder>> responceDepths = JsonConvert.DeserializeAnonymousType(mes, new ResponceWebsocketMessage<List<MessageUserOrder>>());

                                for (int i = 0; i < responceDepths.result.Count; i++)
                                {
                                    Order newOrder = new Order();
                                    newOrder.SecurityNameCode = responceDepths.result[i].currency_pair;
                                    newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(responceDepths.result[i].create_time));

                                    OrderStateType orderState = OrderStateType.None;

                                    if (responceDepths.result[i].Event.Equals("put"))
                                    {
                                        orderState = OrderStateType.Activ;
                                    }
                                    else if (responceDepths.result[i].Event.Equals("update"))
                                    {
                                        orderState = OrderStateType.Patrial;
                                    }
                                    else
                                    {
                                        if (responceDepths.result[i].finish_as.Equals("cancelled"))
                                        {
                                            orderState = OrderStateType.Cancel;
                                        }
                                        else
                                        {
                                            orderState = OrderStateType.Done;
                                        }
                                    }
                                    newOrder.NumberUser = Convert.ToInt32(responceDepths.result[i].text.Replace("t-", ""));
                                    newOrder.NumberMarket = responceDepths.result[i].id.ToString();
                                    newOrder.Side = responceDepths.result[i].side.Equals("long") ? Side.Buy : Side.Sell;
                                    newOrder.State = orderState;
                                    newOrder.Volume = responceDepths.result[i].amount.Replace('.', ',').ToDecimal();
                                    newOrder.Price = responceDepths.result[i].price.Replace('.', ',').ToDecimal();
                                    newOrder.ServerType = ServerType.GateIo;
                                    newOrder.PortfolioNumber = "GateIoWallet";

                                    MyOrderEvent(newOrder);
                                }
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(20);
                    }
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                catch (Exception exception)
                {
                    SendLogMessage("MessageReader error: " + exception, LogMessageType.Error);
                }
            }
        }

        private void WsSourceOnMessageEvent(WsMessageType msgType, string data)
        {
            switch (msgType)
            {
                case WsMessageType.Opened:
                    ConnectEvent();
                    break;
                case WsMessageType.Closed:
                    DisconnectEvent();
                    break;
                case WsMessageType.StringData:
                    _queueMessagesReceivedFromExchange.Enqueue(data);
                    break;
                case WsMessageType.Error:
                    SendLogMessage(data, LogMessageType.Error);
                    break;
                default:
                    throw new NotSupportedException(data);
            }
        }

        public void Dispose()
        {
            try
            {
                if (_wsSource != null)
                {
                    UnInitialize();
                }

                if (_cancelTokenSource != null && !_cancelTokenSource.IsCancellationRequested)
                {
                    _cancelTokenSource.Cancel();
                }
            }
            catch (Exception e)
            {
                SendLogMessage("Zb dispose error: " + e, LogMessageType.Error);
            }
        }

        private void UnInitialize()
        {
            _wsSource.Dispose();
            _wsSource.MessageEvent -= WsSourceOnMessageEvent;
            _wsSource = null;
        }

        public void GetPortfolios()
        {
            try
            {
                string key = _publicKey;
                string secret = _secretKey;
                string host = "https://api.gateio.ws";
                string prefix = "/api/v4";
                string method = "GET";
                string url = "/spot/accounts";
                string query_param = "";
                string body_param = "";
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                using (var sha512 = SHA512.Create())
                {
                    byte[] body_bytes = Encoding.UTF8.GetBytes(body_param);
                    byte[] body_hash = sha512.ComputeHash(body_bytes);
                    string body_hash_string = BitConverter.ToString(body_hash).Replace("-", "").ToLowerInvariant();

                    string sign_string = $"{method}\n{prefix}{url}\n{query_param}\n{body_hash_string}\n{timestamp}";

                    using (var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret)))
                    {
                        byte[] sign_bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(sign_string));
                        string sign = BitConverter.ToString(sign_bytes).Replace("-", "").ToLowerInvariant();

                        string full_url = $"{host}{prefix}{url}";

                        using (var client = new HttpClient())
                        {
                            client.DefaultRequestHeaders.TryAddWithoutValidation("Timestamp", timestamp.ToString());
                            client.DefaultRequestHeaders.TryAddWithoutValidation("KEY", key);
                            client.DefaultRequestHeaders.TryAddWithoutValidation("SIGN", sign);

                            HttpResponseMessage response = client.GetAsync(full_url).Result;
                            string response_content = response.Content.ReadAsStringAsync().Result;

                            List<GetCurrencyVolumeResponce> getCurrencyVolumeResponce = JsonConvert.DeserializeAnonymousType<List<GetCurrencyVolumeResponce>>(response_content, new List<GetCurrencyVolumeResponce>());

                            UpdatePotfolio(getCurrencyVolumeResponce);

                        }
                    }
                }
            }
            catch (WebException ex)
            {
                //HttpWebResponse httpResponse = (HttpWebResponse)ex.Response;
                if (ex.Response != null)
                {
                    using (Stream stream = ex.Response.GetResponseStream())
                    {
                        StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                        var error = reader.ReadToEnd();
                    }
                }
            }

        }

        private void UpdatePotfolio(List<GetCurrencyVolumeResponce> getCurrencyVolumeResponce)
        {
            try
            {
                Portfolio myPortfolio = null;


                if (myPortfolio == null)
                {
                    Portfolio newPortf = new Portfolio();
                    newPortf.Number = "GateIO";
                    newPortf.ValueBegin = 1;
                    newPortf.ValueCurrent = 1;
                    myPortfolio = newPortf;
                }

                if (getCurrencyVolumeResponce.Count == 0 ||
                    getCurrencyVolumeResponce == null)
                {
                    return;
                }

                for (int i = 0; i < getCurrencyVolumeResponce.Count; i++)
                {
                    PositionOnBoard newPortf = new PositionOnBoard();
                    newPortf.SecurityNameCode = getCurrencyVolumeResponce[i].currency;
                    newPortf.ValueBegin = getCurrencyVolumeResponce[i].available.Replace(".", ",").ToDecimal();
                    newPortf.ValueCurrent = getCurrencyVolumeResponce[i].available.Replace(".", ",").ToDecimal();
                    newPortf.ValueBlocked = getCurrencyVolumeResponce[i].locked.Replace(".", ",").ToDecimal();
                    myPortfolio.SetNewPosition(newPortf);
                }

                PortfolioEvent(new List<Portfolio> { myPortfolio });

            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} { error.StackTrace}", LogMessageType.Error);
            }
        }

        public void GetSecurities()
        {
            HttpClient httpClient = new HttpClient();
            var responce = httpClient.GetAsync("https://api.gateio.ws/api/v4/spot/currency_pairs").Result;
            string json = responce.Content.ReadAsStringAsync().Result;

            var currencyPairs = JsonConvert.DeserializeAnonymousType<List<CurrencyPair>>(json, new List<CurrencyPair>());

            List<Security> securities = new List<Security>();

            for (int i = 0; i < currencyPairs.Count; i++)
            {
                Security security = new Security();
                security.Name = currencyPairs[i].id;
                security.NameFull = currencyPairs[i].id;
                security.NameClass = currencyPairs[i].quote;
                security.NameId = currencyPairs[i].id;
                security.SecurityType = SecurityType.CurrencyPair;
                security.Lot = 1;
                security.PriceStep = currencyPairs[i].min_base_amount.ToDecimal();
                security.Decimals = Convert.ToInt32(currencyPairs[i].precision);
                security.DecimalsVolume = Convert.ToInt32(currencyPairs[i].amount_precision);
                security.PriceStepCost = security.PriceStep;
                securities.Add(security);
            }


            SecurityEvent(securities);
        }

        public void Subscrible(Security security)
        {
            SubscribeMarketDepth(security.Name);
            SubscribeTrades(security.Name);
            SubscribeOrders(security.Name);
            SubscribeUserTrades(security.Name);
        }

        private void SubscribeMarketDepth(string security)
        {
            var message = new
            {
                time = TimeManager.GetUnixTimeStampSeconds(),
                channel = "spot.order_book",
                @event = "subscribe",
                payload = new string[] { security, "20", "100ms" }
            };

            _wsSource?.SendMessage(JsonConvert.SerializeObject(message));
        }

        private void SubscribeTrades(string security)
        {
            var message = new
            {
                time = TimeManager.GetUnixTimeStampSeconds(),
                channel = "spot.trades",
                @event = "subscribe",
                payload = new string[] { security }
            };

            _wsSource?.SendMessage(JsonConvert.SerializeObject(message));
        }

        private void SubscribeOrders(string security)
        {
            string channel = "spot.orders";
            string eventName = "subscribe";
            long timestamp = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            // GateAPIv4 key pair
            string apiKey = _publicKey;
            string apiSecret = _secretKey;

            string s = string.Format("channel={0}&event={1}&time={2}", channel, eventName, timestamp);
            byte[] keyBytes = Encoding.UTF8.GetBytes(apiSecret);
            byte[] messageBytes = Encoding.UTF8.GetBytes(s);
            byte[] hashBytes;

            using (var hash = new HMACSHA512(keyBytes))
            {
                hashBytes = hash.ComputeHash(messageBytes);
            }

            string sign = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

            JObject authObject = new JObject {
            { "method", "api_key" },
            { "KEY", apiKey },
            { "SIGN", sign }
        };

            JObject payloadObject = new JObject {
            { "id", timestamp * 1000000 },
            { "time", timestamp },
            { "channel", channel },
            { "event", eventName },
            { "payload", new JArray { security } },
            { "auth", authObject }
        };

            string jsonRequest = JsonConvert.SerializeObject(payloadObject);
            //Console.WriteLine(jsonRequest);


            _wsSource?.SendMessage(jsonRequest);
        }

        private void SubscribeUserTrades(string security)
        {
            string channel = "spot.usertrades";
            string eventName = "subscribe";
            long timestamp = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            // GateAPIv4 key pair
            string apiKey = _publicKey;
            string apiSecret = _secretKey;

            string s = string.Format("channel={0}&event={1}&time={2}", channel, eventName, timestamp);
            byte[] keyBytes = Encoding.UTF8.GetBytes(apiSecret);
            byte[] messageBytes = Encoding.UTF8.GetBytes(s);
            byte[] hashBytes;

            using (var hash = new HMACSHA512(keyBytes))
            {
                hashBytes = hash.ComputeHash(messageBytes);
            }

            string sign = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

            JObject authObject = new JObject {
            { "method", "api_key" },
            { "KEY", apiKey },
            { "SIGN", sign }
        };

            JObject payloadObject = new JObject {
            { "id", timestamp * 1000000 },
            { "time", timestamp },
            { "channel", channel },
            { "event", eventName },
            { "payload", new JArray { security } },
            { "auth", authObject }
        };

            string jsonRequest = JsonConvert.SerializeObject(payloadObject);
            //Console.WriteLine(jsonRequest);


            _wsSource?.SendMessage(jsonRequest);
        }

        public void SendOrder(Order order)
        {

            string side = order.Side == Side.Buy ? "buy" : "sell";
            string secName = order.SecurityNameCode;
            string price = order.Price.ToString().Replace(",", ".");
            string volume = order.Volume.ToString().Replace(",", ".");

            string key = _publicKey;
            string secret = _secretKey;
            string host = "https://api.gateio.ws";
            string prefix = "/api/v4";
            string method = "POST";
            string url = "/spot/orders";
            string query_param = "";
            string body_param;
            if (order.TypeOrder == OrderPriceType.Market)
            {
                body_param = $"{{\"text\":\"t-{order.NumberUser}\",\"currency_pair\":\"{secName}\",\"type\":\"market\",\"account\":\"spot\",\"side\":\"{side}\",\"iceberg\":\"0\",\"amount\":\"{volume}\",\"time_in_force\":\"fok\"}}";
            }
            else
            {
                body_param = $"{{\"text\":\"t-{order.NumberUser}\",\"currency_pair\":\"{secName}\",\"type\":\"limit\",\"account\":\"spot\",\"side\":\"{side}\",\"iceberg\":\"0\",\"amount\":\"{volume}\",\"price\":\"{price}\",\"time_in_force\":\"gtc\"}}";
            }
            

            string timestamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body_param);
            byte[] hashBytes;

            using (var sha512 = SHA512.Create())
            {
                hashBytes = sha512.ComputeHash(bodyBytes);
            }

            string body_hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

            string sign_string = $"{method}\n{prefix}{url}\n{query_param}\n{body_hash}\n{timestamp}";
            string sign;

            using (var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret)))
            {
                byte[] signBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(sign_string));
                sign = BitConverter.ToString(signBytes).Replace("-", "").ToLower();
            }

            string full_url = $"{host}{prefix}{url}";
            var client = new HttpClient();
            var content = new StringContent(body_param, Encoding.UTF8, "application/json");

            client.DefaultRequestHeaders.Add("KEY", key);
            client.DefaultRequestHeaders.Add("SIGN", sign);
            client.DefaultRequestHeaders.Add("Timestamp", timestamp);

            var response = client.PostAsync(full_url, content).Result;
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (response.StatusCode != HttpStatusCode.Created)
            {
                SendLogMessage(responseString, LogMessageType.Error);
            }
        }

        public void CancelOrder(Order order)
        {
            string key = _publicKey;
            string secret = _secretKey;
            string host = "https://api.gateio.ws";
            string prefix = "/api/v4";
            string method = "DELETE";
            string url = $"/spot/orders/{order.NumberMarket}";
            string queryParam = $"currency_pair={order.SecurityNameCode}";
            string bodyParam = "";
            long timestamp = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            string bodyHash = GetHash(bodyParam);
            string signString = $"{method}\n{prefix}{url}\n{queryParam}\n{bodyHash}\n{timestamp}";
            string sign = GetHashHMAC(signString, secret);
            string fullUrl = $"{host}{prefix}{url}?{queryParam}";

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Timestamp", timestamp.ToString());
                httpClient.DefaultRequestHeaders.Add("KEY", key);
                httpClient.DefaultRequestHeaders.Add("SIGN", sign);

                HttpResponseMessage response = httpClient.DeleteAsync(fullUrl).Result;
                string responseBody = response.Content.ReadAsStringAsync().Result;

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage(responseBody, LogMessageType.Error);
                }
            }
        }

        private string GetHash(string input)
        {
            using (var sha512 = SHA512.Create())
            {
                var inputBytes = Encoding.ASCII.GetBytes(input);
                var hashBytes = sha512.ComputeHash(inputBytes);
                var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                return hash;
            }
        }

        private string GetHashHMAC(string input, string key)
        {
            using (var hmacsha512 = new HMACSHA512(Encoding.ASCII.GetBytes(key)))
            {
                var inputBytes = Encoding.ASCII.GetBytes(input);
                var hashBytes = hmacsha512.ComputeHash(inputBytes);
                var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                return hash;
            }
        }

        public static string SingRestData(string secretKey, string signatureString)
        {
            var enc = Encoding.UTF8;
            HMACSHA512 hmac = new HMACSHA512(enc.GetBytes(secretKey));
            hmac.Initialize();

            byte[] buffer = enc.GetBytes(signatureString);

            return BitConverter.ToString(hmac.ComputeHash(buffer)).Replace("-", "").ToLower();

        }

        public List<Candle> GetCandleHistory(string security, TimeSpan interval)
        {
            int oldInterval = Convert.ToInt32(interval.TotalMinutes);

            var needIntervalForQuery =
                CandlesCreator.DetermineAppropriateIntervalForRequest(oldInterval, _supportedIntervals, out var needInterval);

            string intervalstr = CreateIntervalString(interval);

            var jsonCandles = RequestCandlesFromExchange(intervalstr, security);

            var oldCandles = CreateCandlesFromJson(jsonCandles);

            if (oldInterval == needInterval)
            {
                return oldCandles;
            }

            var newCandles = CandlesCreator.CreateCandlesRequiredInterval(needInterval, oldInterval, oldCandles);

            return newCandles;
        }

        private string CreateIntervalString(TimeSpan interval)
        {
            if (interval.Seconds > 0)
            {
                return $"{interval.Seconds}s";
            }
            if (interval.Minutes > 0)
            {
                return $"{interval.Minutes}m";
            }
            if (interval.Hours > 0)
            {
                return $"{interval.Hours}h";
            }
            if (interval.Days > 0)
            {
                return $"{interval.Days}d";
            }

            return "1m";
        }

        private string RequestCandlesFromExchange(string needIntervalForQuery, string security)
        {
            var endPoint = $"https://api.gateio.ws/api/v4/spot/candlesticks?currency_pair={security}&interval={needIntervalForQuery}";
            HttpClient httpClient = new HttpClient();
            var q = httpClient.GetAsync(endPoint).Result;
            var json = q.Content.ReadAsStringAsync().Result;
            return json;
        }

        private List<Candle> CreateCandlesFromJson(string jsonCandles)
        {
            var candles = new List<Candle>();

            List<string[]> js = JsonConvert.DeserializeAnonymousType<List<string[]>>(jsonCandles, new List<string[]>());

            for (int i = 0; i < js.Count; i++)
            {
                var candle = new Candle();

                candle.TimeStart = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(js[i][0]));
                candle.Open = Convert.ToDecimal(js[i][5].Replace(".", ","));
                candle.High = Convert.ToDecimal(js[i][3].Replace(".", ","));
                candle.Low = Convert.ToDecimal(js[i][4].Replace(".", ","));
                candle.Close = Convert.ToDecimal(js[i][2].Replace(".", ","));
                candle.Volume = Convert.ToDecimal(js[i][1].Replace(".", ","));

                candles.Add(candle);
            }



            return candles;
        }


        public event Action<Order> MyOrderEvent;
        public event Action<MyTrade> MyTradeEvent;
        public event Action<List<Portfolio>> PortfolioEvent;
        public event Action<List<Security>> SecurityEvent;
        public event Action<MarketDepth> MarketDepthEvent;
        public event Action<Trade> NewTradesEvent;
        public event Action ConnectEvent;
        public event Action DisconnectEvent;
        public event Action<string, LogMessageType> LogMessageEvent;

        private void SendLogMessage(string message, LogMessageType logMessageType)
        {
            LogMessageEvent(message, logMessageType);
        }

        public void CancelAllOrders()
        {
            
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public void GetOrdersState(List<Order> orders)
        {
            
        }

        public void ResearchTradesToOrders(List<Order> orders)
        {
            
        }
    }

    public class CurrencyPair
    {
        public string id;
        public string Base;
        public string quote;
        public string fee;
        public string min_base_amount;
        public string min_quote_amount;
        public string amount_precision;
        public string precision;
        public string trade_status;
        public string sell_start;
        public string buy_start;
    }

    public class GetCurrencyVolumeResponce
    {
        public string currency;
        public string available;
        public string locked;
    }

    public class ResponceWebsocketMessage<T>
    {
        public string time;
        public string time_ms;
        public string channel;
        public string Event;
        public T result;
    }

    public class MessageDepths
    {
        public long t;
        public long lastUpdateId;
        public string s;
        public List<string[]> bids;
        public List<string[]> asks;
    }

    public class MessagePublicTrades
    {
        public string id;
        public string create_time;
        public string create_time_ms;
        public string side;
        public string currency_pair;
        public string amount;
        public string price;
    }

    public class MessageUserTrade
    {
        public string id;
        public string user_id;
        public string order_id;
        public string currency_pair;
        public string create_time;
        public string create_time_ms;
        public string side;
        public string amount;
        public string role;
        public string price;
        public string fee;
        public string fee_currency;
        public string point_fee;
        public string gt_fee;
        public string text;
        public string amend_text;
        public string biz_info;
    }

    public class MessageUserOrder
    {
        public string id;
        public string text;
        public string create_time;
        public string update_time;
        public string currency_pair;
        public string type;
        public string account;
        public string side;
        public string amount;
        public string price;
        public string time_in_force;
        public string left;
        public string filled_total;
        public string avg_deal_price;
        public string fee;
        public string fee_currency;
        public string point_fee;
        public string gt_fee;
        public string rebated_fee;
        public string rebated_fee_currency;
        public string create_time_ms;
        public string update_time_ms;
        public string user;
        public string Event;
        public string stp_id;
        public string stp_act;
        public string finish_as;
        public string biz_info;
        public string amend_text;
    }
}
