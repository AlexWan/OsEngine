using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.GateIo.EntityCreators;
using OsEngine.Market.Services;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

            WaitTimeAfterFirstStart = 10;
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

    public sealed class GateIoServerRealization : AServerRealization
    {
        private string _publicKey;
        private string _secretKey;

        private RestChannel _restChannel;
        private WsSource _wsSource;
        
        private const string WsUri = "wss://ws.gate.io/v3/";

        private const string PublicRestUri = "https://data.gateio.co/api2/1/";
        private const string PrivateRestUri = "https://api.gateio.co/api2/1/private/";

        private readonly ConcurrentQueue<string> _queueMessagesReceivedFromExchange = new ConcurrentQueue<string>();

        private CancellationTokenSource _cancelTokenSource;

        /// <summary>
        /// словарь таймфреймов, поддерживаемых этой биржей
        /// </summary>
        private readonly Dictionary<int, string> _supportedIntervals;

        public override ServerType ServerType { get { return ServerType.GateIo; } }

        private readonly GateSecurityCreator _securitiesCreator;
        private readonly GatePortfolioCreator _portfolioCreator;
        private readonly GateTradesCreator _tradesCreator;
        private readonly GateMarketDepthCreator _marketDepthCreator;
        private readonly GateOrderCreator _orderCreator;

        private JsonArray _securitiesForSubscribeMarketDepth;
        private JsonArray _securitiesForSubscribe;

        private const string PortfolioNumber = "GateIoWallet";

        public GateIoServerRealization()
        {
            _securitiesCreator = new GateSecurityCreator();
            _portfolioCreator = new GatePortfolioCreator(PortfolioNumber);
            _tradesCreator = new GateTradesCreator();
            _marketDepthCreator = new GateMarketDepthCreator();
            _orderCreator = new GateOrderCreator(PortfolioNumber);

            _supportedIntervals = CreateIntervalDictionary();
        }

        private void OrderCreatorOnNewMyTrade(MyTrade myTrade)
        {
            OnMyTradeEvent(myTrade);
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

        public override void Connect()
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
            _securitiesForSubscribeMarketDepth = new JsonArray();
            _securitiesForSubscribe = new JsonArray();

            _orderCreator.NewMyTrade += OrderCreatorOnNewMyTrade;

            _cancelTokenSource = new CancellationTokenSource();

            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

            if (string.IsNullOrEmpty(_publicKey) || string.IsNullOrEmpty(_secretKey))
            {
                throw new ArgumentException("Invalid key, connection terminated!");
            }

            StartMessageReader();

            _restChannel = new RestChannel();

            _wsSource = new WsSource(WsUri);
            _wsSource.MessageEvent += WsSourceOnMessageEvent;
            _wsSource.Start();
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
                            if (mes.StartsWith("{\"error"))
                            {
                                continue;
                            }

                            var jt = JToken.Parse(mes);

                            var channel = jt.SelectToken("method").ToString();

                            if (channel == "trades.update")
                            {
                                foreach (var trade in _tradesCreator.Create(mes))
                                {
                                    OnTradeEvent(trade);
                                }
                            }
                            else if (channel == "depth.update")
                            {
                                OnMarketDepthEvent(_marketDepthCreator.Create(mes));
                            }
                            else if (channel == "balance.update")
                            {
                                OnPortfolioEvent(_portfolioCreator.UpdatePortfolio(mes));
                            }
                            else if (channel == "order.update")
                            {
                                OnOrderEvent(_orderCreator.Create(mes));
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
                    OnConnectEvent();
                    SendWsSignMessage();
                    StartPingSender();
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

        private void SendWsSignMessage()
        {
            JsonObject jsonContent = new JsonObject();

            jsonContent.Add("id", 777126565235);
            jsonContent.Add("method", "server.sign");

            var nonce = TimeManager.GetUnixTimeStampMilliseconds();

            var sing = SingData(_secretKey, nonce.ToString());

            jsonContent.Add("params", new JsonArray{_publicKey, sing, nonce});

            _wsSource?.SendMessage(jsonContent.ToString());
        }

        private void StartPingSender()
        {
            Task.Run(() => PingSender(_cancelTokenSource.Token), _cancelTokenSource.Token);
        }

        private async void PingSender(CancellationToken token)
        {
            //server.ping
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(50000, token);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        continue;
                    }
                    SendPing();
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

        private void SendPing()
        {
            JsonObject jsonContent = new JsonObject();

            jsonContent.Add("id", TimeManager.GetUnixTimeStampSeconds());
            jsonContent.Add("method", "server.ping");
            jsonContent.Add("params", new JsonArray());

            _wsSource?.SendMessage(jsonContent.ToString());
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
            }
            catch (Exception e)
            {
                SendLogMessage("Zb dispose error: " + e, LogMessageType.Error);
            }
        }

        private void UnInitialize()
        {
            _orderCreator.NewMyTrade -= OrderCreatorOnNewMyTrade;

            _restChannel = null;

            _wsSource.Dispose();
            _wsSource.MessageEvent -= WsSourceOnMessageEvent;
            _wsSource = null;
        }

        public override void GetPortfolios()
        {
            JsonObject jsonContent = new JsonObject();

            jsonContent.Add("id", TimeManager.GetUnixTimeStampSeconds());
            jsonContent.Add("method", "balance.subscribe");
            jsonContent.Add("params", new JsonArray());
            
            _wsSource.SendMessage(jsonContent.ToString());

            var headers = new Dictionary<string, string>();

            headers.Add("Key", _publicKey);
            headers.Add("Sign", SingRestData(_secretKey, ""));

            var result = _restChannel.SendPostQuery(PrivateRestUri, "fundingbalances", new byte[0], headers);

            OnPortfolioEvent(_portfolioCreator.CreatePortfolio(result));
        }

        public override void GetSecurities()
        {
            var securitiesJson = _restChannel.SendGetQuery(PublicRestUri, "marketinfo");

            OnSecurityEvent(_securitiesCreator.Create(securitiesJson));
        }

        public override void Subscrible(Security security)
        {
            SubscribeMarketDepth(security.Name);
            SubscribeTrades(security.Name);
            SubscribeOrders(security.Name);
        }

        private void SubscribeMarketDepth(string security)
        {
            _securitiesForSubscribeMarketDepth.Add(new JsonArray { security, 30, "0.00000001" });

            JsonObject jsonContent = new JsonObject();

            jsonContent.Add("id", TimeManager.GetUnixTimeStampSeconds());
            jsonContent.Add("method", "depth.subscribe");
            jsonContent.Add("params", _securitiesForSubscribeMarketDepth);

            _wsSource?.SendMessage(jsonContent.ToString());
        }

        private void SubscribeTrades(string security)
        {
            _securitiesForSubscribe.Add(security);

            JsonObject jsonContent = new JsonObject();

            jsonContent.Add("id", TimeManager.GetUnixTimeStampSeconds());
            jsonContent.Add("method", "trades.subscribe");
            jsonContent.Add("params", _securitiesForSubscribe);

            _wsSource?.SendMessage(jsonContent.ToString());
        }

        private void SubscribeOrders(string security)
        {
            _securitiesForSubscribe.Add(security);

            JsonObject jsonContent = new JsonObject();

            jsonContent.Add("id", TimeManager.GetUnixTimeStampSeconds());
            jsonContent.Add("method", "order.subscribe");
            jsonContent.Add("params", new JsonArray());

            _wsSource?.SendMessage(jsonContent.ToString());
        }

        public override void SendOrder(Order order)
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendFormat("{0}={1}", "currencyPair", order.SecurityNameCode.ToLower());
            builder.Append("&");
            builder.AppendFormat("{0}={1}", "rate", order.Price);
            builder.Append("&");
            builder.AppendFormat("{0}={1}", "amount", order.Volume);
            builder.Append("&");
            builder.AppendFormat("{0}={1}", "text", "t-" + order.NumberUser);

            var headers = new Dictionary<string, string>();

            headers.Add("Key", _publicKey);
            headers.Add("Sign", SingRestData(_secretKey, builder.ToString()));

            var data = Encoding.UTF8.GetBytes(builder.ToString());

            var endPoint = order.Side == Side.Buy ? "buy" : "sell";

            var result = _restChannel.SendPostQuery(PrivateRestUri, endPoint, data, headers);

            var jt = JToken.Parse(result);
            var responseData = jt["result"].ToString();

            if (responseData == "false")
            {
                order.State = OrderStateType.Fail;
                OnOrderEvent(order);
                SendLogMessage("Error placing order: you have insufficient funds", LogMessageType.Trade);
            }
        }

        public override void CancelOrder(Order order)
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendFormat("{0}={1}", "orderNumber", order.NumberMarket);
            builder.Append("&");
            builder.AppendFormat("{0}={1}", "currencyPair", order.SecurityNameCode.ToLower());
            builder.Append("&");
            builder.AppendFormat("{0}={1}", "text", "t-" + order.NumberUser);

            var headers = new Dictionary<string, string>();

            headers.Add("Key", _publicKey);
            headers.Add("Sign", SingRestData(_secretKey, builder.ToString()));

            var data = Encoding.UTF8.GetBytes(builder.ToString());

            var endPoint = "cancelOrder";

            var result = _restChannel.SendPostQuery(PrivateRestUri, endPoint, data, headers);

            var jt = JToken.Parse(result);
            var responseData = jt["result"].ToString();

            if (responseData == "false")
            {
                SendLogMessage(jt["message"].ToString(), LogMessageType.Error);
            }
        }

        private string SingData(string secretKey, string data)
        {
            byte[] keyInBytes = Encoding.UTF8.GetBytes(secretKey);
            byte[] dataInBytes = Encoding.UTF8.GetBytes(data);

            byte[] computedHash;

            using (var hash = new HMACSHA512(keyInBytes))
            {
                computedHash = hash.ComputeHash(dataInBytes);
            }
            
            var result = Convert.ToBase64String(computedHash);

            return result;
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

            var jsonCandles = RequestCandlesFromExchange(needIntervalForQuery, security);

            var oldCandles = CreateCandlesFromJson(jsonCandles);

            if (oldInterval == needInterval)
            {
                return oldCandles;
            }

            var newCandles = CandlesCreator.CreateCandlesRequiredInterval(needInterval, oldInterval, oldCandles);

            return newCandles;
        }

        private string RequestCandlesFromExchange(string needIntervalForQuery, string security)
        {
            //https://data.gateio.co/api2/1/candlestick2/eth_usdt?group_sec=300&range_hour=1000

            var endPoint = "candlestick2/" + security.ToLower() + "?group_sec=" + needIntervalForQuery + "&range_hour=1000";

            var jsonCandles = _restChannel.SendGetQuery(PublicRestUri, endPoint);
            
            return jsonCandles;
        }

        private List<Candle> CreateCandlesFromJson(string jsonCandles)
        {
            //{"elapsed":"11ms","result":"true","data":[["1569432300000","0","8379.11","8379.11","8379.11","8379.11"],
            //["1569432360000","0.467191","8375.87","8375.87","8375.19","8375.19"]]}

            var candles = new List<Candle>();

            var js = JToken.Parse(jsonCandles);

            foreach (var jtCandle in js["data"])
            {
                var candle = new Candle();

                candle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(jtCandle[0].Value<long>());
                candle.Open = jtCandle[5].Value<decimal>();
                candle.High = jtCandle[3].Value<decimal>();
                candle.Low = jtCandle[4].Value<decimal>();
                candle.Close = jtCandle[2].Value<decimal>();
                candle.Volume = jtCandle[1].Value<decimal>();

                candles.Add(candle);
            }

            return candles;
        }


    }
}
