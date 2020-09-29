using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.Huobi.Entities;
using OsEngine.Market.Servers.Huobi.Request;
using OsEngine.Market.Servers.Huobi.Response;
using OsEngine.Market.Services;
using RestSharp;

namespace OsEngine.Market.Servers.Huobi.Spot
{
    public class HuobiSpotServer : AServer
    {
        public HuobiSpotServer()
        {
            HuobiServerRealization realization = new HuobiServerRealization(ServerType.HuobiSpot,
                "api.huobi.pro",
                "/ws/v2");

            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
        }

        /// <summary>
        /// instrument history query
        /// запрос истории по инструменту
        /// </summary>
        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            return ((HuobiServerRealization)ServerRealization).GetCandleHistory(nameSec, tf);
        }
    }

    public class HuobiServerRealization : AServerRealization
    {
        private readonly string _host;
        private readonly string _path;

        private readonly HuobiSpotSecurityCreator _securityCreator;

        /// <summary>
        /// словарь таймфреймов, поддерживаемых этой биржей
        /// </summary>
        private readonly Dictionary<int, string> _supportedIntervals;

        public HuobiServerRealization(ServerType type,
            string host,
            string path)
        {
            _supportedIntervals = CreateIntervalDictionary();

            ServerType = type;
            ServerStatus = ServerConnectStatus.Disconnect;

            _host = host;
            _path = path;

            _securityCreator = new HuobiSpotSecurityCreator();
        }

        //1min, 5min, 15min, 30min, 60min, 4hour, 1day, 1mon, 1week, 1year
        private Dictionary<int, string> CreateIntervalDictionary()
        {
            var dictionary = new Dictionary<int, string>();

            dictionary.Add(1, "1min");
            dictionary.Add(5, "5min");
            dictionary.Add(15, "15min");
            dictionary.Add(30, "30min");
            dictionary.Add(60, "60min");
            dictionary.Add(240, "4hour");
            dictionary.Add(1440, "1day");
            dictionary.Add(43200, "1mon");
            dictionary.Add(525600, "1year");

            return dictionary;
        }

        /// <summary>
        /// server type
        /// тип сервера
        /// </summary>
        public override ServerType ServerType { get; }

        private string _publicKey;
        private string _secretKey;

        private PublicUrlBuilder _urlBuilder;

        private PrivateUrlBuilder _privateUriBuilder;

        private CancellationTokenSource _cancelTokenSource;

        private Signer _signer;

        private WsSource _wsSource;

        private WsSource _marketDataSource;

        private readonly ConcurrentQueue<string> _queueMessagesReceivedFromExchange = new ConcurrentQueue<string>();

        private readonly ConcurrentQueue<string> _queueMarketDataReceivedFromExchange = new ConcurrentQueue<string>();


        public override void Connect()
        {
            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

            _urlBuilder = new PublicUrlBuilder(_host);
            _privateUriBuilder = new PrivateUrlBuilder(_publicKey, _secretKey, _host);

            _cancelTokenSource = new CancellationTokenSource();

            StartMessageReader();

            StartMarketDataReader();

            _signer = new Signer(_secretKey);

            _wsSource = new WsSource("wss://" + _host + _path);
            _wsSource.MessageEvent += WsSourceOnMessageEvent;
            _wsSource.Start();

            _marketDataSource = new WsSource("wss://" + _host + "/ws");
            _marketDataSource.ByteDataEvent += MarketDataSourceOnMessageEvent;
            _marketDataSource.Start();

        }

        private void WsSourceOnMessageEvent(WsMessageType msgType, string data)
        {
            switch (msgType)
            {
                case WsMessageType.Opened:
                    Sign();
                    OnConnectEvent();
                    StartPortfolioRequester();
                    break;
                case WsMessageType.Closed:
                    OnDisconnectEvent();
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

        #region Разбор рыночных данных

        private DateTime _lastTimeUpdateSocket;

        private async void SourceAliveCheckerThread(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(200);
                if (_lastTimeUpdateSocket == DateTime.MinValue)
                {
                    continue;
                }
                
                if (_lastTimeUpdateSocket.AddSeconds(60) < DateTime.Now)
                {
                    SendLogMessage("The websocket is disabled. Restart", LogMessageType.Error);
                    Dispose();
                    OnDisconnectEvent();
                    return;
                }
            }
        }

        private void MarketDataSourceOnMessageEvent(WsMessageType msgType, byte[] data)
        {
            switch (msgType)
            {
                case WsMessageType.Opened:
                    break;
                case WsMessageType.Closed:
                    OnDisconnectEvent();
                    break;
                case WsMessageType.ByteData:
                    string message = GZipDecompresser.Decompress(data);
                    _queueMarketDataReceivedFromExchange.Enqueue(message);
                    break;
                case WsMessageType.Error:
                    break;
                default:
                    throw new NotSupportedException("");
            }
        }

        private void StartMarketDataReader()
        {
            Task.Run(() => MarketDataReader(_cancelTokenSource.Token), _cancelTokenSource.Token);
            Task.Run(() => SourceAliveCheckerThread(_cancelTokenSource.Token), _cancelTokenSource.Token);
        }

        private async void MarketDataReader(CancellationToken token)
        {
            _lastTimeUpdateSocket = DateTime.MinValue;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!_queueMarketDataReceivedFromExchange.IsEmpty)
                    {
                        string mes;

                        if (_queueMarketDataReceivedFromExchange.TryDequeue(out mes))
                        {
                            var pingMessage = JsonConvert.DeserializeObject<MarketDataPingMessage>(mes);

                            if (pingMessage != null && pingMessage.ping != 0)
                            {
                                string pongData = $"{{\"pong\":{pingMessage.ping}}}";
                                _marketDataSource.SendMessage(pongData);
                            }
                            else
                            {
                                if (mes.StartsWith("{\"ch\":\"market."))
                                {
                                    var security = GetSecurityName(mes, out var channel);

                                    if (channel == "trade")
                                    {
                                        _lastTimeUpdateSocket = DateTime.Now;
                                        var response = JsonConvert.DeserializeObject<SubscribeTradeResponse>(mes);

                                        foreach (var trade in CreateTrades(security, response))
                                        {
                                            OnTradeEvent(trade);
                                        }
                                    }
                                    else if (channel == "depth")
                                    {
                                        _lastTimeUpdateSocket = DateTime.Now;
                                        var response = JsonConvert.DeserializeObject<SubscribeDepthResponse>(mes);

                                        OnMarketDepthEvent(CreateMarketDepth(security, response));
                                    }
                                }
                                else if (mes.StartsWith("{\"id\":\"\",\"rep\":\"market."))
                                {
                                    var response = JsonConvert.DeserializeObject<GetCandlestickResponse>(mes);
                                    response.security = response.GetSecurity();

                                    _allCandleSeries.Add(response);
                                }
                                else
                                {
                                    dynamic jt = JToken.Parse(mes);

                                    string ch = jt.rep;

                                    if (!string.IsNullOrEmpty(ch) && ch.Contains("kline"))
                                    {
                                        var response = JsonConvert.DeserializeObject<GetCandlestickResponse>(mes);

                                        response.security = response.GetSecurity();

                                        _allCandleSeries.Add(response);
                                    }
                                    else
                                    {
                                        SendLogMessage(mes, LogMessageType.System);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        await Task.Delay(20);
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

        private string GetSecurityName(string data, out string channel)
        {
            int firstIndex = data.IndexOf(':');
            int secondIndex = data.IndexOf(',');

            var substring = data.Substring(firstIndex, secondIndex - firstIndex);

            var parts = substring.Split('.');

            channel = parts[2];

            var security = parts[1];

            return security;
        }

        private List<Trade> CreateTrades(string securityName, SubscribeTradeResponse response)
        {
            var trades = new List<Trade>();

            var data = response.tick.data;

            for (int i = data.Length - 1; i >= 0; i--)
            {
                var trade = new Trade();
                trade.SecurityNameCode = securityName;
                trade.Id = data[i].tradeid.ToString();
                trade.Side = data[i].direction == "sell" ? Side.Sell : Side.Buy;
                trade.Price = data[i].price;
                trade.Volume = data[i].amount;
                trade.Time = TimeManager.GetDateTimeFromTimeStamp(data[i].ts);

                trades.Add(trade);
            }

            return trades;
        }

        private MarketDepth CreateMarketDepth(string securityName, SubscribeDepthResponse response)
        {
            var newDepth = new MarketDepth();

            newDepth.Time = TimeManager.GetDateTimeFromTimeStamp(response.tick.ts);

            newDepth.SecurityNameCode = securityName;

            var bids = response.tick.bids;

            var asks = response.tick.asks;

            foreach (var bid in bids)
            {
                newDepth.Bids.Add(new MarketDepthLevel()
                {
                    Price = bid[0],
                    Bid = bid[1],
                });
            }

            foreach (var ask in asks)
            {
                newDepth.Asks.Add(new MarketDepthLevel()
                {
                    Price = ask[0],
                    Ask = ask[1],
                });
            }

            return newDepth;
        }

        #endregion

        private void Sign()
        {
            string authRequest = BuildSign(DateTime.UtcNow);
            _wsSource.SendMessage(authRequest);
        }

        private void StartMessageReader()
        {
            Task.Run(() => MessageReader(_cancelTokenSource.Token), _cancelTokenSource.Token);
        }

        private async void MessageReader(CancellationToken token)
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
                            dynamic jt = JToken.Parse(mes);

                            string channel = jt.action;

                            if (string.IsNullOrEmpty(channel))
                            {
                                continue;
                            }

                            if (channel == "ping")
                            {
                                var pingMessage = JsonConvert.DeserializeObject<PingMessage>(mes);

                                if (pingMessage != null && pingMessage.Data != null && pingMessage.Data.TimeStamp != 0)
                                {
                                    long ts = pingMessage.Data.TimeStamp;

                                    string pongData = $"{{\"action\": \"pong\", \"data\": {{\"ts\":{ts} }} }}";
                                    _wsSource.SendMessage(pongData);
                                }
                            }
                            else if (channel == "req")
                            {
                                SendLogMessage("Успешная аутентификация в системе", LogMessageType.Connect);
                            }
                            else if (channel == "push")
                            {
                                string ch = jt.ch;

                                string security = ch.Split('#')[1];

                                var data = jt.data;

                                var eventType = data.eventType;

                                if (ch.StartsWith("orders"))
                                {
                                    if (eventType == "creation")
                                    {
                                        var order = new Order();
                                        order.SecurityNameCode = security;
                                        order.Side = data.type == "buy-limit" ? Side.Buy :
                                            data.type == "sell-limit" ? Side.Sell : Side.None;
                                        order.NumberMarket = data.orderId;
                                        order.NumberUser = data.clientOrderId;
                                        order.PortfolioNumber = _portfolioCurrent;

                                        order.Price = data.orderPrice;
                                        order.Volume = data.orderSize;
                                        order.State = OrderStateType.Activ;
                                        order.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp((long)data.orderCreateTime);
                                        OnOrderEvent(order);
                                    }
                                }
                                else if (ch.StartsWith("trade"))
                                {
                                    OnMyTradeEvent(CreateMyTrade(security, data));
                                }
                            }
                            else
                            {
                                SendLogMessage(mes, LogMessageType.System);
                            }
                        }
                    }
                    else
                    {
                        await Task.Delay(20);
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

        private MyTrade CreateMyTrade(string security, dynamic data)
        {
            var trade = new MyTrade();
            trade.SecurityNameCode = security;
            trade.NumberOrderParent = data.orderId;
            trade.Volume = data.tradeVolume;
            trade.Price = data.tradePrice;
            trade.NumberTrade = data.tradeId;
            trade.Time = TimeManager.GetDateTimeFromTimeStamp((long)data.tradeTime);
            trade.Side = data.orderSide == "buy" ? Side.Buy : Side.Sell;

            return trade;
        }

        public override void Dispose()
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

                _allCandleSeries?.Clear();
            }
            catch (Exception e)
            {
                SendLogMessage("Huobi dispose error: " + e, LogMessageType.Error);
            }
        }

        private void UnInitialize()
        {
            _wsSource.Dispose();
            _wsSource.MessageEvent -= WsSourceOnMessageEvent;
            _wsSource = null;

            _marketDataSource.Dispose();
            _marketDataSource.ByteDataEvent -= MarketDataSourceOnMessageEvent;
            _marketDataSource = null;
        }

        #region Запросы

        public override void GetSecurities()
        {
            string url = _urlBuilder.Build("/v1/common/symbols");

            var httpClient = new HttpClient();

            string response = httpClient.GetStringAsync(url).Result;

            OnSecurityEvent(_securityCreator.Create(response));
        }

        private void StartPortfolioRequester()
        {
            Task.Run(() => PortfolioRequester(_cancelTokenSource.Token), _cancelTokenSource.Token);
        }

        public List<Portfolio> Portfolios;

        private async void PortfolioRequester(CancellationToken token)
        {
            try
            {
                string url = _privateUriBuilder.Build("GET", "/v1/account/accounts");

                var httpClient = new HttpClient();

                string response = httpClient.GetStringAsync(url).Result;

                Portfolios = new List<Portfolio>();

                GetAccountInfoResponse accountInfo = JsonConvert.DeserializeObject<GetAccountInfoResponse>(response);

                foreach (var info in accountInfo.data)
                {
                    var portfolio = new Portfolio();
                    portfolio.Number = info.type + "_" + info.id;
                    portfolio.ValueBegin = 1;
                    portfolio.ValueCurrent = 1;
                    portfolio.ValueBlocked = 1;

                    Portfolios.Add(portfolio);
                }

                OnPortfolioEvent(Portfolios);

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(5000, token);

                        GetPortfolios();
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
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        public override void GetPortfolios()
        {
            if (Portfolios == null)
            {
                return;
            }
            foreach (var portfolio in Portfolios)
            {
                string url = _privateUriBuilder.Build("GET",
                    $"/v1/account/accounts/{portfolio.Number.Split('_')[1]}/balance");

                var httpClient = new HttpClient();

                string response = httpClient.GetStringAsync(url).Result;

                if (response.Contains("Incorrect Access key"))
                {
                    SendLogMessage("Huobi: Incorrect Access API key", LogMessageType.Error);
                    return;
                }

                GetAccountBalanceResponse accountBalance = JsonConvert.DeserializeObject<GetAccountBalanceResponse>(response);

                //portfolio.ClearPositionOnBoard();

                for (int i = 0; accountBalance.data != null && i < accountBalance.data.list.Length; i++)
                {
                    var currentData = accountBalance.data.list[i];

                    if (currentData.balance == 0)
                    {
                        continue;
                    }

                    PositionOnBoard pos = new PositionOnBoard();
                    pos.SecurityNameCode = currentData.currency;
                    pos.PortfolioName = portfolio.Number;
                    pos.ValueCurrent = currentData.balance;
                    pos.ValueBegin = currentData.balance;

                    portfolio.SetNewPosition(pos);
                }
            }

            OnPortfolioEvent(Portfolios);
        }

        private string _portfolioCurrent;

        public override void SendOrder(Order order)
        {
            _portfolioCurrent = order.PortfolioNumber;

            JsonObject jsonContent = new JsonObject();

            var accountData = order.PortfolioNumber.Split('_');

            var source = "spot-api";

            if (accountData[0] == "margin")
            {
                source = "margin-api";
            }
            else if (accountData[0] == "super-margin")
            {
                source = "super-margin-api";
            }

            jsonContent.Add("account-id", accountData[1]);
            jsonContent.Add("symbol", order.SecurityNameCode);
            jsonContent.Add("type", order.Side == Side.Buy ? "buy-limit" : "sell-limit");
            jsonContent.Add("amount", order.Volume);
            jsonContent.Add("price", order.Price);
            jsonContent.Add("source", source);
            jsonContent.Add("client-order-id", order.NumberUser);

            string url = _privateUriBuilder.Build("POST", "/v1/order/orders/place");

            StringContent httpContent = new StringContent(jsonContent.ToString(), Encoding.UTF8, "application/json");

            var httpClient = new HttpClient();

            var response = httpClient.PostAsync(url, httpContent).Result;

            string result = response.Content.ReadAsStringAsync().Result;

            PlaceOrderResponse orderResponse = JsonConvert.DeserializeObject<PlaceOrderResponse>(result);

            if (orderResponse.status == "ok")
            {
                SendLogMessage($"Order num {order.NumberUser} on exchange.", LogMessageType.Trade);
            }
            else
            {
                SendLogMessage($"Order exchange error num {order.NumberUser} : {orderResponse.errorMessage}", LogMessageType.Error);

                order.State = OrderStateType.Fail;

                OnOrderEvent(order);
            }
        }

        public override void CancelOrder(Order order)
        {
            string url = _privateUriBuilder.Build("POST", "/v1/order/orders/submitCancelClientOrder");

            string body = $"{{ \"client-order-id\":\"{order.NumberUser}\" }}";

            StringContent httpContent = new StringContent(body, Encoding.UTF8, "application/json");

            var httpClient = new HttpClient();

            var response = httpClient.PostAsync(url, httpContent).Result;

            string result = response.Content.ReadAsStringAsync().Result;

            CancelOrderByClientResponse cancelResponse = JsonConvert.DeserializeObject<CancelOrderByClientResponse>(result);

            if (cancelResponse.status == "ok")
            {
                SendLogMessage($"Order num {order.NumberUser} canceled.", LogMessageType.Trade);
                order.State = OrderStateType.Cancel;
                OnOrderEvent(order);
            }
            else
            {
                SendLogMessage($"Error on order cancel num {order.NumberUser} : {cancelResponse.errorMessage}", LogMessageType.Error);
            }
        }

        public override void Subscrible(Security security)
        {
            string topic = $"market.{security.Name}.trade.detail";

            string clientId = "";

            _marketDataSource.SendMessage($"{{ \"sub\": \"{topic}\",\"id\": \"{clientId}\" }}");

            topic = $"market.{security.Name}.depth.step0";

            _marketDataSource.SendMessage($"{{ \"sub\": \"{topic}\",\"id\": \"{clientId}\" }}");

            topic = $"orders#{security.Name}";

            _wsSource.SendMessage($"{{\"action\":\"sub\", \"ch\":\"{topic}\", \"cid\": \"{clientId}\" }}");

            topic = $"trade.clearing#{security.Name}";

            _wsSource.SendMessage($"{{\"action\":\"sub\", \"cid\": \"{clientId}\", \"ch\":\"{topic}\" }}");
        }

        public override List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public override void GetOrdersState(List<Order> orders)
        {
            
        }
        
        private readonly List<GetCandlestickResponse> _allCandleSeries = new List<GetCandlestickResponse>();

        public override List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            List<Candle> candles = new List<Candle>();

            int oldInterval = Convert.ToInt32(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes);

            var step = new TimeSpan(0, (int)(oldInterval * 300), 0);

            actualTime = startTime;

            var midTime = actualTime + step;

            while (true)
            {
                if (actualTime >= endTime)
                {
                    break;
                }

                List<Candle> newCandles = GetCandles(oldInterval, security.Name, actualTime, midTime);

                if (candles.Count != 0 && newCandles != null && newCandles.Count != 0)
                {
                    for (int i = 0; i < newCandles.Count; i++)
                    {
                        if (candles[candles.Count - 1].TimeStart >= newCandles[i].TimeStart)
                        {
                            newCandles.RemoveAt(i);
                            i--;
                        }
                    }
                }

                if (newCandles == null)
                {
                    continue;
                }

                if (newCandles.Count == 0)
                {
                    return candles;
                }

                candles.AddRange(newCandles);

                actualTime = candles[candles.Count - 1].TimeStart;
                midTime = actualTime + step;
                Thread.Sleep(1000);
            }

            if (candles.Count == 0)
            {
                return null;
            }

            return candles;
        }

        /// <summary>
        /// request instrument history
        /// запрос истории по инструменту
        /// </summary>
        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            var diff = new TimeSpan(0, (int)(tf.TotalMinutes * 300), 0);

            return GetCandles((int)tf.TotalMinutes, nameSec, DateTime.UtcNow - diff, DateTime.UtcNow);
        }

        private object _locker = new object();

        private List<Candle> GetCandles(int oldInterval, string security, DateTime startTime, DateTime endTime)
        {
            lock (_locker)
            {
                var needIntervalForQuery =
                    CandlesCreator.DetermineAppropriateIntervalForRequest(oldInterval, _supportedIntervals, out var needInterval);

                var clientId = "";

                string topic = $"market.{security}.kline.{needIntervalForQuery}";

                var from = TimeManager.GetTimeStampSecondsToDateTime(startTime);
                var to = TimeManager.GetTimeStampSecondsToDateTime(endTime);

                _marketDataSource.SendMessage($"{{ \"req\": \"{topic}\",\"id\": \"{clientId}\", \"from\":{from}, \"to\":{to} }}");

                var startLoadingTime = DateTime.Now;

                while (startLoadingTime.AddSeconds(40) > DateTime.Now)
                {
                    var candles = _allCandleSeries.Find(s => s.security == security && s.GetTimeFrame() == needIntervalForQuery);

                    if (candles != null)
                    {
                        _allCandleSeries.Remove(candles);

                        var oldCandles = CreateCandlesFromJson(candles);

                        if (oldInterval == needInterval)
                        {
                            return oldCandles;
                        }

                        var newCandles = CandlesCreator.CreateCandlesRequiredInterval(needInterval, oldInterval, oldCandles);

                        return newCandles;
                    }

                    Thread.Sleep(100);
                }

                SendLogMessage(OsLocalization.Market.Message95 + security, LogMessageType.Error);

                return null;
            }
        }

        private List<Candle> CreateCandlesFromJson(GetCandlestickResponse rawCandles)
        {
            var candles = new List<Candle>();

            foreach (var jtCandle in rawCandles.data)
            {
                var candle = new Candle();

                candle.TimeStart = TimeManager.GetDateTimeFromTimeStampSeconds(jtCandle.id);
                candle.Open = jtCandle.open;
                candle.High = jtCandle.high;
                candle.Low = jtCandle.low;
                candle.Close = jtCandle.close;
                candle.Volume = jtCandle.vol;

                candles.Add(candle);
            }

            return candles;
        }

        #endregion

        public string BuildSign(DateTime utcDateTime)
        {
            string strDateTime = utcDateTime.ToString("s");

            var request = new GetRequest()
                .AddParam("accessKey", _publicKey)
                .AddParam("signatureMethod", "HmacSHA256")
                .AddParam("signatureVersion", "2.1")
                .AddParam("timestamp", strDateTime);

            string signature = _signer.Sign("GET", _host, _path, request.BuildParams());

            var auth = new WebSocketAuthenticationRequestV2
            {
                @params = new WebSocketAuthenticationRequestV2.Params
                {
                    accessKey = _publicKey,
                    timestamp = strDateTime,
                    signature = signature
                }
            };

            return auth.ToJson();
        }
    }

    public class WebSocketAuthenticationRequestV2
    {
        public class Params
        {
            public string authType { get { return "api"; } }
            public string accessKey;
            public string signatureMethod { get { return "HmacSHA256"; } }
            public string signatureVersion { get { return "2.1"; } }
            public string timestamp;
            public string signature;
        }

        public string action { get { return "req"; } }
        public string ch { get { return "auth"; } }
        public Params @params;

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
