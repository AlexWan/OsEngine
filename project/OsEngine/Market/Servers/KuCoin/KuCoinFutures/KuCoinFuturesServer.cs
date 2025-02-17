using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.KuCoin.KuCoinFutures.Json;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using WebSocketSharp;

namespace OsEngine.Market.Servers.KuCoin.KuCoinFutures
{
    public class KuCoinFuturesServer : AServer
    {
        public KuCoinFuturesServer()
        {

            KuCoinFuturesServerRealization realization = new KuCoinFuturesServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamPassphrase, "");
        }
    }

    public class KuCoinFuturesServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public KuCoinFuturesServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread threadForPublicMessages = new Thread(PublicMessageReader);
            threadForPublicMessages.IsBackground = true;
            threadForPublicMessages.Name = "PublicMessageReaderKuCoin";
            threadForPublicMessages.Start();

            Thread threadForPrivateMessages = new Thread(PrivateMessageReader);
            threadForPrivateMessages.IsBackground = true;
            threadForPrivateMessages.Name = "PrivateMessageReaderKuCoin";
            threadForPrivateMessages.Start();

            Thread threadCheckAliveWebSocket = new Thread(CheckAliveWebSocket);
            threadCheckAliveWebSocket.IsBackground = true;
            threadCheckAliveWebSocket.Name = "CheckAliveWebSocketKuCoinFutures";
            threadCheckAliveWebSocket.Start();
        }

        public DateTime ServerTime { get; set; }

        public void Connect()
        {
            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
            _passphrase = ((ServerParameterPassword)ServerParameters[2]).Value;

            HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(_baseUrl + "/api/v1/timestamp").Result;

            string json = responseMessage.Content.ReadAsStringAsync().Result;

            if (responseMessage.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    _webSocketPublicMessages = new ConcurrentQueue<string>();
                    _webSocketPrivateMessages = new ConcurrentQueue<string>();               
                    CreateWebSocketConnection();
                    CheckActivationSockets();
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    SendLogMessage("Connection cannot be open. KuCoinFutures. Error request", LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            else
            {
                SendLogMessage("Connection cannot be open. KuCoinFutures. Error request", LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public void Dispose()
        {
            try
            {
                UnsubscribeFromAllWebSockets();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            try
            {
                DeleteWebsocketConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            _subscribedSecurities.Clear();

            _webSocketPublicMessages = new ConcurrentQueue<string>();
            _webSocketPrivateMessages = new ConcurrentQueue<string>();

            Disconnect();
        }

        public void Disconnect()
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void UnsubscribeFromAllWebSockets()
        {
            if (_webSocketPublic == null 
                || _webSocketPrivate == null)
            {
                return;
            }

            for (int i = 0; i < _subscribedSecurities.Count; i++)
            {
                string securityName = _subscribedSecurities[i];

                _webSocketPublic.Send($"{{\"type\": \"unsubscribe\",\"topic\": \"/contractMarket/tickerV2:{securityName}\"}}"); // transactions
                _webSocketPublic.Send($"{{\"type\": \"unsubscribe\",\"topic\": \"/contractMarket/level2Depth5:{securityName}\"}}"); // marketDepth
                _webSocketPrivate.Send($"{{\"type\": \"unsubscribe\", \"privateChannel\": \"true\", \"topic\": \"/contract/position:{securityName}\"}}"); // change of positions
            }
            
            _webSocketPrivate.Send($"{{\"type\": \"unsubscribe\", \"privateChannel\": \"true\", \"topic\": \"/contractMarket/tradeOrders\"}}"); // changing orders
            _webSocketPrivate.Send($"{{\"type\": \"unsubscribe\", \"privateChannel\": \"true\", \"topic\": \"/contractAccount/wallet\"}}"); // portfolio change
        }

        public ServerType ServerType
        {
            get { return ServerType.KuCoinFutures; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string _publicKey;

        private string _secretKey;

        private string _passphrase;

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            try
            {
                // https://www.kucoin.com/docs/rest/futures-trading/market-data/get-symbols-list
                HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(_baseUrl + "/api/v1/contracts/active").Result;

                string json = responseMessage.Content.ReadAsStringAsync().Result;

                ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<object>());

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    if (stateResponse.code.Equals("200000") == true)
                    {
                        UpdateSecurity(json);
                    }
                    else
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n"
                            + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"GetSecurities> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                    if (stateResponse != null && stateResponse.code != null)
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n"
                            + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSecurity(string json)
        {
            ResponseMessageRest<List<ResponseSymbol>> symbols = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<List<ResponseSymbol>>());

            List<Security> securities = new List<Security>();

            for (int i = 0; i < symbols.data.Count; i++)
            {
                ResponseSymbol item = symbols.data[i];

                if (item.status.Equals("Open"))
                {
                    Security newSecurity = new Security();

                    newSecurity.Exchange = ServerType.KuCoinFutures.ToString();
                    newSecurity.State = SecurityStateType.Activ;
                    newSecurity.Name = item.symbol;
                    newSecurity.NameFull = item.symbol;
                    newSecurity.NameClass = item.rootSymbol;
                    newSecurity.NameId = item.symbol;
                    newSecurity.SecurityType = SecurityType.Futures;

                    newSecurity.PriceStep = item.tickSize.ToDecimal();
                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                    newSecurity.Lot = item.lotSize.ToDecimal();
                    
                    newSecurity.Decimals = item.tickSize.DecimalsCount();
                    newSecurity.DecimalsVolume = item.lotSize.DecimalsCount();

                    securities.Add(newSecurity);
                }
            }

            SecurityEvent(securities);
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        public void GetPortfolios()
        {
            CreateQueryPortfolio(true, "USDT");
            CreateQueryPortfolio(true, "USD");
            CreateQueryPortfolio(true, "XBT");
            CreateQueryPositions(true);
        }
        
        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        private readonly RateGate _rateGateCandleHistory = new RateGate(700, TimeSpan.FromSeconds(30));

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            DateTime timeStart = DateTime.UtcNow - TimeSpan.FromMinutes(timeFrameBuilder.TimeFrameTimeSpan.Minutes * candleCount);
            DateTime timeEnd = DateTime.UtcNow;

            return GetCandleDataToSecurity(security, timeFrameBuilder, timeStart, timeEnd, timeStart);
        }

        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf, bool IsOsData, int CountToLoad, DateTime timeEnd)
        {
            // From technical support chat: Right now the kucoin servers are only returning 24 hours for the lower timeframes and no more than 30 days for higher timeframes. Hopefully their new API fixes this, but no word yet when this will be.

            int needToLoadCandles = CountToLoad;

            List<Candle> candles = new List<Candle>();

            DateTime fromTime = timeEnd - TimeSpan.FromMinutes(tf.TotalMinutes*CountToLoad);
            
            const int KuCoinFuturesDataLimit = 200; // KuCoin limitation: For each query, the system would return at most 200 pieces of data. To obtain more data, please page the data by time.
            do
            {
                int limit = needToLoadCandles;

                if (needToLoadCandles > KuCoinFuturesDataLimit) 
                {
                    limit = KuCoinFuturesDataLimit;
                    
                }
                else
                {
                    limit = needToLoadCandles;
                }

                List<Candle> rangeCandles = new List<Candle>();

                DateTime slidingFrom = timeEnd - TimeSpan.FromMinutes(tf.TotalMinutes * limit);
                rangeCandles = CreateQueryCandles(nameSec, GetStringInterval(tf), slidingFrom, timeEnd);

                if (rangeCandles == null)
                    return null; // no data

                candles.InsertRange(0, rangeCandles);

                if (rangeCandles.Count < KuCoinFuturesDataLimit) // hard limit
                {
                    if ((candles.Count > rangeCandles.Count) && (candles[rangeCandles.Count].TimeStart == candles[rangeCandles.Count - 1].TimeStart))
                    { // HACK: exchange returns one element twice when data in the past ends
                        candles.RemoveAt(rangeCandles.Count);
                    }

                    // this happens when the server does not provide new data further into the past
                    return candles;
                }

                if (candles.Count != 0)
                {
                    timeEnd = candles[0].TimeStart;
                }
                
                needToLoadCandles -= limit;
            } while (needToLoadCandles > 0);

            
            return candles;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (startTime != actualTime)
            {
                startTime = actualTime;
            }

            int countNeedToLoad = GetCountCandlesFromSliceTime(startTime, endTime, timeFrameBuilder.TimeFrameTimeSpan);

            return GetCandleHistory(security.NameFull, timeFrameBuilder.TimeFrameTimeSpan, true, countNeedToLoad, endTime);
        }
        
        private int GetCountCandlesFromSliceTime(DateTime startTime, DateTime endTime, TimeSpan tf)
        {
            if (tf.Hours != 0)
            {
                TimeSpan TimeSlice = endTime - startTime;

                return Convert.ToInt32(TimeSlice.TotalHours / tf.TotalHours);
            }
            else
            {
                TimeSpan TimeSlice = endTime - startTime;
                return Convert.ToInt32(TimeSlice.TotalMinutes / tf.Minutes);
            }
        }

        private string GetStringInterval(TimeSpan tf)
        {
            // The granularity (granularity parameter of K-line) represents the number of minutes, the available granularity scope is: 1,5,15,30,60,120,240,480,720,1440,10080. Requests beyond the above range will be rejected.
            if (tf.Minutes != 0)
            {
                return $"{tf.Minutes}";
            }
            else
            {
                return $"{tf.TotalMinutes}";
            }
        }

        #endregion

        #region 6 WebSocket creation

        private WebSocket _webSocketPrivate;

        private WebSocket _webSocketPublic;

        private string _webSocketPrivateUrl = "wss://ws-api-futures.kucoin.com/?token=xxx&[connectId=xxxxx]";

        private string _webSocketPublicUrl = "wss://ws-api-futures.kucoin.com/?token=xxx&[connectId=xxxxx]";

        private void CreateWebSocketConnection()
        {
            // 1. get websocket address
            HttpResponseMessage responseMessage = CreatePrivateQuery("/api/v1/bullet-private", "POST", null, String.Empty);

            if(responseMessage.IsSuccessStatusCode == false)
            {
                SendLogMessage("KuCoin keys are wrong. Message from server: " + responseMessage.Content.ReadAsStringAsync().Result, LogMessageType.Error);
                return;
            }

            string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

            ResponsePrivateWebSocketConnection wsResponse = JsonConvert.DeserializeAnonymousType(JsonResponse, new ResponsePrivateWebSocketConnection());

            // set dynamic server address ws
            _webSocketPrivateUrl = wsResponse.data.instanceServers[0].endpoint + "?token=" + wsResponse.data.token;

            _webSocketPrivate = new WebSocket(_webSocketPrivateUrl);
            _webSocketPrivate.SslConfiguration.EnabledSslProtocols
                = System.Security.Authentication.SslProtocols.Ssl3
                | System.Security.Authentication.SslProtocols.Tls11
                | System.Security.Authentication.SslProtocols.None
                | System.Security.Authentication.SslProtocols.Tls12
                | System.Security.Authentication.SslProtocols.Tls13
                | System.Security.Authentication.SslProtocols.Tls;
            _webSocketPrivate.EmitOnPing = true;

            _webSocketPrivate.OnOpen += _webSocketPrivate_OnOpen;
            _webSocketPrivate.OnMessage += _webSocketPrivate_OnMessage;
            _webSocketPrivate.OnError += _webSocketPrivate_OnError;
            _webSocketPrivate.OnClose += _webSocketPrivate_OnClose;
            _webSocketPrivate.Connect();

            responseMessage = _httpPublicClient.PostAsync(_baseUrl + "/api/v1/bullet-public", new StringContent(String.Empty, Encoding.UTF8, "application/json")).Result;
            JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;
            wsResponse = JsonConvert.DeserializeAnonymousType(JsonResponse, new ResponsePrivateWebSocketConnection());

            // set dynamic server address ws
            _webSocketPublicUrl = wsResponse.data.instanceServers[0].endpoint + "?token=" + wsResponse.data.token;

            _webSocketPublic = new WebSocket(_webSocketPublicUrl);
            _webSocketPublic.SslConfiguration.EnabledSslProtocols
               = System.Security.Authentication.SslProtocols.Ssl3
               | System.Security.Authentication.SslProtocols.Tls11
               | System.Security.Authentication.SslProtocols.None
               | System.Security.Authentication.SslProtocols.Tls12
               | System.Security.Authentication.SslProtocols.Tls13
               | System.Security.Authentication.SslProtocols.Tls;
            _webSocketPublic.EmitOnPing = true;

            _webSocketPublic.OnOpen += _webSocketPublic_OnOpen;
            _webSocketPublic.OnMessage += _webSocketPublic_OnMessage;
            _webSocketPublic.OnError += _webSocketPublic_OnError;
            _webSocketPublic.OnClose += _webSocketPublic_OnClose;
            _webSocketPublic.Connect();
        }

        private string _lockerCheckActivateionSockets = "lockerCheckActivateionSocketsKuCoinFutures";

        private void CheckActivationSockets()
        {
            lock (_lockerCheckActivateionSockets)
            {
                if (_webSocketPrivate == null
                    || _webSocketPrivate.ReadyState != WebSocketState.Open)
                {
                    Disconnect();
                    return;
                }
                if (_webSocketPublic == null
                    || _webSocketPublic.ReadyState != WebSocketState.Open)
                {
                    Disconnect();
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Connect;

                    if (ConnectEvent != null)
                    {
                        ConnectEvent();
                    }
                }
            }
        }

        private void DeleteWebsocketConnection()
        {
            if (_webSocketPublic != null)
            {
                try
                {
                    _webSocketPublic.OnOpen -= _webSocketPublic_OnOpen;
                    _webSocketPublic.OnMessage -= _webSocketPublic_OnMessage;
                    _webSocketPublic.OnError -= _webSocketPublic_OnError;
                    _webSocketPublic.OnClose -= _webSocketPublic_OnClose;
                    _webSocketPublic.CloseAsync();
                }
                catch
                {
                    // ignore
                }

                _webSocketPublic = null;
            }

            if (_webSocketPrivate != null)
            {
                try
                {
                    _webSocketPrivate.OnOpen -= _webSocketPrivate_OnOpen;
                    _webSocketPrivate.OnMessage -= _webSocketPrivate_OnMessage;
                    _webSocketPrivate.OnError -= _webSocketPrivate_OnError;
                    _webSocketPrivate.OnClose -= _webSocketPrivate_OnClose;
                    _webSocketPrivate.CloseAsync();
                }
                catch
                {
                    // ignore
                }

                _webSocketPrivate = null;
            }
        }

        #endregion

        #region 7 WebSocket events

        private void _webSocketPublic_OnClose(object sender, CloseEventArgs e)
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                SendLogMessage("Connection Closed by KuCoin. WebSocket Public Closed Event", LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void _webSocketPublic_OnError(object sender, ErrorEventArgs e)
        {
            WebSocketSharp.ErrorEventArgs error = e;

            if (error.Exception != null)
            {
                SendLogMessage(error.Exception.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPublic_OnMessage(object sender, MessageEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e == null
                    || e.Data == null
                    || e.Data.Length == 0)
                {
                    return;
                }

                if (_webSocketPublicMessages == null)
                {
                    return;
                }

                _webSocketPublicMessages.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPublic_OnOpen(object sender, EventArgs e)
        {
            SendLogMessage("Connection to public data is Open", LogMessageType.System);

            CheckActivationSockets();
        }

        private void _webSocketPrivate_OnClose(object sender, CloseEventArgs e)
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                SendLogMessage("Connection Closed by KuCoin. WebSocket Private Closed Event", LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void _webSocketPrivate_OnError(object sender, ErrorEventArgs e)
        {
            WebSocketSharp.ErrorEventArgs error = e;

            if (error.Exception != null)
            {
                SendLogMessage(error.Exception.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnMessage(object sender, MessageEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e == null
                    || e.Data == null
                    || e.Data.Length == 0)
                {
                    return;
                }

                if (_webSocketPrivateMessages == null)
                {
                    return;
                }

                _webSocketPrivateMessages.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnOpen(object sender, EventArgs e)
        {
            SendLogMessage("Connection to private data is Open", LogMessageType.System);

            CheckActivationSockets();

            // We immediately subscribe to changes in orders and portfolio
            _webSocketPrivate.Send($"{{\"type\": \"subscribe\", \"privateChannel\": \"true\", \"topic\": \"/contractMarket/tradeOrders\"}}"); // changing orders
            _webSocketPrivate.Send($"{{\"type\": \"subscribe\", \"privateChannel\": \"true\", \"topic\": \"/contractAccount/wallet\"}}"); // portfolio change
        }

        #endregion

        #region 8 WebSocket check alive

        private void CheckAliveWebSocket()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(10000);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_webSocketPrivate != null && _webSocketPrivate.ReadyState == WebSocketState.Open ||
                        _webSocketPrivate.ReadyState == WebSocketState.Connecting)
                    {
                        _webSocketPrivate.Send($"{{\"type\": \"ping\"}}");
                    }
                    else
                    {
                        if (ServerStatus != ServerConnectStatus.Disconnect)
                        {
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                        }
                    }

                    if (_webSocketPublic != null && _webSocketPublic.ReadyState == WebSocketState.Open ||
                        _webSocketPublic.ReadyState == WebSocketState.Connecting)
                    {
                        _webSocketPublic.Send($"{{\"type\": \"ping\"}}");
                    }
                    else
                    {
                        if (ServerStatus != ServerConnectStatus.Disconnect)
                        {
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        #endregion

        #region 9 Security Subscribed

        // https://www.kucoin.com/docs/basic-info/request-rate-limit/rest-api
        private RateGate _rateGateSubscribed = new RateGate(1, TimeSpan.FromMilliseconds(220));

        public void Subscrible(Security security)
        {
            try
            {
                _rateGateSubscribed.WaitToProceed();

                CreateSubscribedSecurityMessageWebSocket(security);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent;

        #endregion

        #region 10 WebSocket parsing the messages

        private ConcurrentQueue<string> _webSocketPublicMessages = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _webSocketPrivateMessages = new ConcurrentQueue<string>();

        private void PublicMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    if (_webSocketPublicMessages.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    _webSocketPublicMessages.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Equals("pong"))
                    {
                        continue;
                    }

                    ResponseWebSocketMessageAction<object> action = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<object>());

                    if (action.subject != null && action.type != "welcome")
                    {
                        if (action.subject.Equals("level2"))
                        {
                            UpdateDepth(message);
                            continue;
                        }
                        else if (action.subject.Equals("ticker"))
                        {
                            UpdateTrade(message);
                            continue;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }

        private void PrivateMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    if (_webSocketPrivateMessages.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    _webSocketPrivateMessages.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Equals("pong"))
                    {
                        continue;
                    }

                    ResponseWebSocketMessageAction<object> action = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<object>());

                    if (action.subject != null && action.type != "welcome")
                    {
                        if (action.subject.Equals("orderChange"))
                        {
                            UpdateOrder(message);
                            continue;
                        }

                        if (action.subject.Equals("position.change"))
                        {
                            UpdatePosition(message);
                            continue;
                        }

                        if (action.subject.Equals("availableBalance.change"))
                        {
                            UpdatePortfolio(message);
                            continue;
                        }
                    }

                }
                catch (Exception exception)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }

        private void UpdateTrade(string message)
        {
            ResponseWebSocketMessageAction<ResponseWebSocketMessageTrade> responseTrade = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketMessageTrade>());

            if (responseTrade == null)
            {
                return;
            }

            if (responseTrade.data == null)
            {
                return;
            }
            
            Trade trade = new Trade();
            trade.SecurityNameCode = responseTrade.data.symbol;
            trade.Price = responseTrade.data.price.ToDecimal();
            trade.Id = responseTrade.data.tradeId;
            trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseTrade.data.ts) / 1000000); // from nanoseconds to ms))
            trade.Volume = responseTrade.data.size.ToDecimal();

            if(responseTrade.data.side == "sell")
            {
                trade.Side = Side.Sell;
            }
            else //(responseTrade.data.side == "buy")
            {
                trade.Side = Side.Buy;
            }

            NewTradesEvent(trade);
        }

        private void UpdateDepth(string message)
        {
            ResponseWebSocketMessageAction<ResponseWebSocketDepthItem> responseDepth = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketDepthItem>());

            if (responseDepth.data == null)
            {
                return;
            }

            MarketDepth marketDepth = new MarketDepth();

            List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
            List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

            marketDepth.SecurityNameCode = responseDepth.topic.Split(':')[1];

            for (int i = 0; i < responseDepth.data.asks.Count; i++)
            {
                MarketDepthLevel newMDLevel = new MarketDepthLevel();
                newMDLevel.Ask = responseDepth.data.asks[i][1].ToDecimal();
                newMDLevel.Price = responseDepth.data.asks[i][0].ToDecimal();
                ascs.Add(newMDLevel);
            }

            for (int i = 0; i < responseDepth.data.bids.Count; i++)
            {
                MarketDepthLevel newMDLevel = new MarketDepthLevel();
                newMDLevel.Bid = responseDepth.data.bids[i][1].ToDecimal();
                newMDLevel.Price = responseDepth.data.bids[i][0].ToDecimal();

                bids.Add(newMDLevel);
            }

            marketDepth.Asks = ascs;
            marketDepth.Bids = bids;

            marketDepth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseDepth.data.timestamp));


            MarketDepthEvent(marketDepth);
        }

        private void UpdateMytrade(string json)
        {
            ResponseMessageRest<ResponseMyTrades> responseMyTrades = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<ResponseMyTrades>());


            for (int i = 0; i < responseMyTrades.data.items.Count; i++)
            {
                ResponseMyTrade responseT = responseMyTrades.data.items[i];

                MyTrade myTrade = new MyTrade();

                myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseT.tradeTime) / 1000000); //from nanoseconds to ms
                myTrade.NumberOrderParent = responseT.orderId;
                myTrade.NumberTrade = responseT.tradeId;
                myTrade.Price = responseT.price.ToDecimal();
                myTrade.SecurityNameCode = responseT.symbol;
                myTrade.Side = responseT.side.Equals("buy") ? Side.Buy : Side.Sell;
                
                string comissionSecName = responseT.feeCurrency;

                if (myTrade.SecurityNameCode.StartsWith(comissionSecName))
                {
                    myTrade.Volume = responseT.size.ToDecimal() + responseT.fee.ToDecimal();
                }
                else
                {
                    myTrade.Volume = responseT.size.ToDecimal();
                }
                
                MyTradeEvent(myTrade);
            }

        }

        private void UpdatePortfolio(string message)
        {
            ResponseWebSocketMessageAction<ResponseWebSocketPortfolio> Portfolio = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketPortfolio>());

            Portfolio portfolio = new Portfolio();
            portfolio.Number = "KuCoinFutures";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            PositionOnBoard pos = new PositionOnBoard();

            pos.PortfolioName = "KuCoinFutures";
            pos.SecurityNameCode = Portfolio.data.currency;
            pos.ValueBlocked = Portfolio.data.holdBalance.ToDecimal();
            pos.ValueCurrent = Portfolio.data.availableBalance.ToDecimal();

            portfolio.SetNewPosition(pos);
            PortfolioEvent(new List<Portfolio> { portfolio });
        }

        private void UpdatePosition(string message)
        {
            ResponseWebSocketMessageAction<ResponseWebSocketPosition> posResponse = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketPosition>());

            Portfolio portfolio = new Portfolio();
            portfolio.Number = "KuCoinFutures";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            ResponseWebSocketPosition data = posResponse.data;

            PositionOnBoard pos = new PositionOnBoard();

            pos.PortfolioName = "KuCoinFutures";
            pos.SecurityNameCode = data.symbol;
            pos.ValueBlocked = data.maintMargin.ToDecimal();
            pos.ValueCurrent = data.currentQty.ToDecimal();
            
            portfolio.SetNewPosition(pos);
            PortfolioEvent(new List<Portfolio> { portfolio });
        }

        private void UpdateOrder(string message)
        {
            ResponseWebSocketMessageAction<ResponseWebSocketOrder> Order = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketOrder>());

            if (Order.data == null)
            {
                return;
            }
            
            ResponseWebSocketOrder item = Order.data;

            OrderStateType stateType = GetOrderState(item.status, item.type);

            if (item.orderType != null && item.orderType.Equals("market") && stateType == OrderStateType.Active)
            {
               return;
            }

            Order newOrder = new Order();
            newOrder.SecurityNameCode = item.symbol;
            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts)/1000000); //from nanoseconds to ms

            if (item.clientOid != null)
            {
                try
                {
                        newOrder.NumberUser = Convert.ToInt32(item.clientOid);
                }
                catch
                {
                    SendLogMessage("Strange order num: " + item.clientOid, LogMessageType.Error);
                    return;
                }
            }

            newOrder.NumberMarket = item.orderId;

            OrderPriceType.TryParse(item.orderType, true, out newOrder.TypeOrder);
            Side.TryParse(item.side, true, out newOrder.Side);

            newOrder.State = stateType;
            newOrder.Volume = item.size == null ? item.filledSize.Replace('.', ',').ToDecimal() : item.size.Replace('.', ',').ToDecimal();
            newOrder.Price = item.price != null ? item.price.Replace('.', ',').ToDecimal() : 0;

            newOrder.ServerType = ServerType.KuCoinFutures;
            newOrder.PortfolioNumber = "KuCoinFutures";

            if (stateType == OrderStateType.Done || (stateType == OrderStateType.Partial && item.size != item.filledSize))
            {
                // как только приходит ордер исполненный или частично исполненный триггер на запрос моего трейда по имени бумаги
                CreateQueryMyTrade(newOrder.SecurityNameCode, newOrder.NumberMarket, Convert.ToInt64(item.ts) / 1000000);
            }
            
            MyOrderEvent(newOrder);
            
        }

        private OrderStateType GetOrderState(string orderStatusResponse, string orderTypeResponse)
        {
            OrderStateType stateType;

            switch (orderStatusResponse)
            {
                case ("open"):
                    stateType = OrderStateType.Active;
                    break;

                case ("match"):
                    stateType = OrderStateType.Partial;
                    break;

                case ("done"):
                    if (orderTypeResponse == "canceled")
                        stateType = OrderStateType.Cancel;
                    else //(orderTypeResponse == "filled")
                        stateType = OrderStateType.Done;
                    break;
                    
                default:
                    stateType = OrderStateType.None;
                    break;
            }

            return stateType;
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;

        #endregion

        #region 11 Trade

        private RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        private RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        public void SendOrder(Order order)
        {
            // https://www.kucoin.com/docs/rest/futures-trading/orders/place-order
            _rateGateSendOrder.WaitToProceed();

            SendOrderRequestData data = new SendOrderRequestData();
            data.clientOid = order.NumberUser.ToString();
            data.symbol = order.SecurityNameCode;
            data.side = order.Side.ToString().ToLower();
            data.type = order.TypeOrder.ToString().ToLower();
            data.price = order.TypeOrder == OrderPriceType.Market ? null : order.Price.ToString().Replace(",", ".");
            data.size = order.Volume.ToString().Replace(",", ".");
            data.leverage = "10";

            JsonSerializerSettings dataSerializerSettings = new JsonSerializerSettings();
            dataSerializerSettings.NullValueHandling = NullValueHandling.Ignore;// если маркет-ордер, то игнорим параметр цены

            string jsonRequest = JsonConvert.SerializeObject(data, dataSerializerSettings);

            // для теста можно использовать  "/api/v1/orders/test"
            HttpResponseMessage responseMessage = CreatePrivateQuery("/api/v1/orders", "POST", null, jsonRequest);
            string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;
            ResponseMessageRest<ResponsePlaceOrder> stateResponse = JsonConvert.DeserializeAnonymousType(JsonResponse, new ResponseMessageRest<ResponsePlaceOrder>());

            if (responseMessage.StatusCode == HttpStatusCode.OK)
            {
                if (stateResponse.code.Equals("200000") == true)
                {
                    SendLogMessage($"Order num {order.NumberUser} on exchange.", LogMessageType.Trade);
                    order.State = OrderStateType.Active;
                    order.NumberMarket = stateResponse.data.orderId;

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }
                }
                else
                {
                    CreateOrderFail(order);
                    SendLogMessage($"Code: {stateResponse.code}\n"
                        + $"Message: {stateResponse.msg}", LogMessageType.Error);
                }
            }
            else
            {
                CreateOrderFail(order);
                SendLogMessage($"SendOrder> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                if (stateResponse != null && stateResponse.code != null)
                {
                    SendLogMessage($"Code: {stateResponse.code}\n"
                        + $"Message: {stateResponse.msg}", LogMessageType.Error);
                }
            }

        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        public void GetOrdersState(List<Order> orders)
        {

        }

        public void CancelAllOrders()
        {

        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            _rateGateCancelOrder.WaitToProceed();

            CancelAllOrdersRequestData data = new CancelAllOrdersRequestData();
            data.symbol = security.Name;

            string jsonRequest = JsonConvert.SerializeObject(data);

            HttpResponseMessage responseMessage = CreatePrivateQuery("/api/v1/orders", "DELETE", null, jsonRequest);
            string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;
            ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(JsonResponse, new ResponseMessageRest<object>());

            if (responseMessage.StatusCode == HttpStatusCode.OK)
            {
                if (stateResponse.code.Equals("200000") == true)
                {
                    // ignore
                }
                else
                {
                    SendLogMessage($"Code: {stateResponse.code}\n"
                        + $"Message: {stateResponse.msg}", LogMessageType.Error);
                }
            }
            else
            {
                SendLogMessage($"CancelAllOrdersToSecurity> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                if (stateResponse != null && stateResponse.code != null)
                {
                    SendLogMessage($"Code: {stateResponse.code}\n"
                        + $"Message: {stateResponse.msg}", LogMessageType.Error);
                }
            }
        }

        public void CancelOrder(Order order)
        {
            _rateGateCancelOrder.WaitToProceed();

            
            HttpResponseMessage responseMessage = CreatePrivateQuery("/api/v1/orders/" + order.NumberMarket, "DELETE", null, null);
            string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;
            ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(JsonResponse, new ResponseMessageRest<object>());

            if (responseMessage.StatusCode == HttpStatusCode.OK)
            {
                if (stateResponse.code.Equals("200000") == true)
                {
                    // ignore
                }
                else
                {
                    CreateOrderFail(order);
                    SendLogMessage($"Code: {stateResponse.code}\n"
                        + $"Message: {stateResponse.msg}", LogMessageType.Error);
                }
            }
            else
            {
                CreateOrderFail(order);
                SendLogMessage($"CancelOrder> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                if (stateResponse != null && stateResponse.code != null)
                {
                    SendLogMessage($"Code: {stateResponse.code}\n"
                        + $"Message: {stateResponse.msg}", LogMessageType.Error);
                }
            }
        }

        public void ResearchTradesToOrders(List<Order> orders)
        {

        }

        private void CreateOrderFail(Order order)
        {
            order.State = OrderStateType.Fail;

            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        public void GetAllActivOrders()
        {

        }

        public void GetOrderStatus(Order order)
        {

        }

        #endregion

        #region 12 Queries

        private string _baseUrl = "https://api-futures.kucoin.com";

        private RateGate _rateGateGetMyTradeState = new RateGate(1, TimeSpan.FromMilliseconds(200));

        HttpClient _httpPublicClient = new HttpClient();

        private List<string> _subscribedSecurities = new List<string>();

        private void CreateSubscribedSecurityMessageWebSocket(Security security)
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            for (int i = 0; i < _subscribedSecurities.Count; i++)
            {
                if (_subscribedSecurities[i].Equals(security.Name))
                {
                    return;
                }
            }

            _subscribedSecurities.Add(security.Name);

             _webSocketPublic.Send($"{{\"type\": \"subscribe\",\"topic\": \"/contractMarket/ticker:{security.Name}\"}}"); // сделки

             _webSocketPublic.Send($"{{\"type\": \"subscribe\",\"topic\": \"/contractMarket/level2Depth5:{security.Name}\"}}"); // стаканы 5+5 https://www.kucoin.com/docs/websocket/futures-trading/public-channels/level2-5-best-ask-bid-orders
             
            _webSocketPrivate.Send($"{{\"type\": \"subscribe\", \"privateChannel\": \"true\", \"topic\": \"/contract/position:{security.Name}\"}}"); // изменение позиций https://www.kucoin.com/docs/websocket/futures-trading/private-channels/position-change-events
        }

        private void CreateQueryPortfolio(bool IsUpdateValueBegin, string currency = "USDT")
        {
            try
            {
                HttpResponseMessage responseMessage = CreatePrivateQuery("/api/v1/account-overview", "GET", "currency="+currency, null);
                string json = responseMessage.Content.ReadAsStringAsync().Result;

                ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<object>());

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    if (stateResponse.code == "200000")
                    {
                        UpdatePortfolioREST(json, IsUpdateValueBegin);
                    }
                    else
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n" + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }

                }
                else
                {
                    SendLogMessage($"CreateQueryPortfolio> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                    if (stateResponse != null && stateResponse.code != null)
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n" + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdatePortfolioREST(string json, bool IsUpdateValueBegin)
        {
            ResponseMessageRest<ResponseAsset> asset = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<ResponseAsset>());

            Portfolio portfolio = new Portfolio();
            portfolio.Number = "KuCoinFutures";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            
            ResponseAsset item = asset.data;

            PositionOnBoard pos = new PositionOnBoard();

            pos.PortfolioName = "KuCoinFutures";
            pos.SecurityNameCode = item.currency;
            pos.ValueBlocked = item.unrealisedPNL.ToDecimal();
            pos.ValueCurrent = item.accountEquity.ToDecimal();

            if (IsUpdateValueBegin)
            {
                pos.ValueBegin = item.accountEquity.ToDecimal();
            }

            portfolio.SetNewPosition(pos);
            

            PortfolioEvent(new List<Portfolio> { portfolio });
        }

        private void CreateQueryPositions(bool IsUpdateValueBegin)
        {
            try
            {
                HttpResponseMessage responseMessage = CreatePrivateQuery("/api/v1/positions", "GET", null, null);
                string json = responseMessage.Content.ReadAsStringAsync().Result;

                ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<object>());

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    if (stateResponse.code == "200000")
                    {
                        UpdatePositionsREST(json, IsUpdateValueBegin);
                    } else
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n" + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                    
                }
                else
                {
                    SendLogMessage($"CreateQueryPositions> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                    if (stateResponse != null && stateResponse.code != null)
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n" + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdatePositionsREST(string json, bool IsUpdateValueBegin)
        {
            ResponseMessageRest<List<ResponsePosition>> assets = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<List<ResponsePosition>>());

            Portfolio portfolio = new Portfolio();
            portfolio.Number = "KuCoinFutures";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            for (int i = 0; i < assets.data.Count; i++)
            {
                ResponsePosition item = assets.data[i];
                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = "KuCoinFutures";
                pos.SecurityNameCode = item.symbol;
                pos.ValueBlocked = item.maintMargin.ToDecimal(); //????
                pos.ValueCurrent = item.currentCost.ToDecimal();

                if (IsUpdateValueBegin)
                {
                    pos.ValueBegin = item.currentCost.ToDecimal();
                }

                portfolio.SetNewPosition(pos);
            }

            PortfolioEvent(new List<Portfolio> { portfolio });
        }

        private void CreateQueryMyTrade(string nameSec, string OrdId, long ts)
        {
            Thread.Sleep(2000);
            _rateGateGetMyTradeState.WaitToProceed();
            
            HttpResponseMessage responseMessage = CreatePrivateQuery(
                "/api/v1/fills",
                "GET",
                "symbol=" + nameSec + "&orderId=" + OrdId,
                null);
            string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

            ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(JsonResponse, new ResponseMessageRest<object>());
            

            if (responseMessage.StatusCode == HttpStatusCode.OK)
            {
                if (stateResponse.code.Equals("200000") == true)
                {
                    UpdateMytrade(JsonResponse);
                }
                else
                {
                    SendLogMessage($"Code: {stateResponse.code}\n"
                        + $"Message: {stateResponse.msg}", LogMessageType.Error);
                }
            }
            else
            {
                SendLogMessage($"CreateQueryMyTrade> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                if (stateResponse != null && stateResponse.code != null)
                {
                    SendLogMessage($"Code: {stateResponse.code}\n"
                        + $"Message: {stateResponse.msg}", LogMessageType.Error);
                }
            }

        }

        private List<Candle> CreateQueryCandles(string nameSec, string stringInterval, DateTime timeFrom, DateTime timeTo)
        {
            _rateGateCandleHistory.WaitToProceed();

            long from = TimeManager.GetTimeStampMilliSecondsToDateTime(timeFrom);
            long to = TimeManager.GetTimeStampMilliSecondsToDateTime(timeTo);
            HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(_baseUrl + $"/api/v1/kline/query?symbol={nameSec}&granularity={stringInterval}&from={from}&to={to}").Result;
            string content = responseMessage.Content.ReadAsStringAsync().Result;


            if (responseMessage.StatusCode == HttpStatusCode.OK)
            {
                ResponseMessageRest<List<List<string>>> symbols = JsonConvert.DeserializeAnonymousType(content, new ResponseMessageRest<List<List<string>>>());

                if (symbols.code.Equals("200000") == true)
                {
                    List<Candle> candles = new List<Candle>();

                    for (int i = 0; i < symbols.data.Count; i++)
                    {
                        List<string> item = symbols.data[i];

                        /* Пример возвращаемого значения свечи https://www.kucoin.com/docs/rest/futures-trading/market-data/get-klines
                         * [
                                1575331200000, //Time
                                7495.01, //Entry price
                                8309.67, //Highest price
                                7250, //Lowest price
                                7463.55, //Close price
                                0 //Trading volume
                              ],
                         *
                         */

                        Candle newCandle = new Candle();

                        newCandle.Open = item[1].ToDecimal();
                        newCandle.Close = item[4].ToDecimal();
                        newCandle.High = item[2].ToDecimal();
                        newCandle.Low = item[3].ToDecimal();
                        newCandle.Volume = item[5].ToDecimal();
                        newCandle.State = CandleState.Finished;
                        newCandle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item[0]));
                        candles.Add(newCandle);
                    }

                    return candles;
                }
                else
                {
                    SendLogMessage($"Code: {symbols.code}\n"
                        + $"Message: {symbols.msg}", LogMessageType.Error);
                    return null;
                }
            }
            else
            {
                SendLogMessage($"CreateQueryCandles> State Code: {responseMessage.StatusCode}", LogMessageType.Error);
                return null;
            }
        }

        private HttpResponseMessage CreatePrivateQuery(string path, string method, string queryString, string body)
        {
            string requestPath = path;
            string url = $"{_baseUrl}{requestPath}";
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string signature = GenerateSignature(timestamp, method, requestPath, queryString, body, _secretKey);

            HttpClient httpClient = new HttpClient();

            httpClient.DefaultRequestHeaders.Add("KC-API-KEY", _publicKey);
            httpClient.DefaultRequestHeaders.Add("KC-API-SIGN", signature);
            httpClient.DefaultRequestHeaders.Add("KC-API-TIMESTAMP", timestamp);
            httpClient.DefaultRequestHeaders.Add("KC-API-PASSPHRASE", SignHMACSHA256(_passphrase, _secretKey));
            httpClient.DefaultRequestHeaders.Add("KC-API-KEY-VERSION", "2");

            if (method.Equals("POST"))
            {
                return httpClient.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json")).Result;
            } 
            else if (method.Equals("DELETE"))
            {
                HttpRequestMessage request = new HttpRequestMessage
                {
                    Method = HttpMethod.Delete,
                    RequestUri = new Uri(url),
                    Content = body != null ? new StringContent(body, Encoding.UTF8, "application/json") : null
                };

                return httpClient.SendAsync(request).Result;
            }
            else
            {
                return httpClient.GetAsync(url + "?" + queryString).Result;
            }
        }

        private string SignHMACSHA256(string data, string secretKey)
        {
            byte[] secretKeyBytes = Encoding.UTF8.GetBytes(secretKey);
            using (HMACSHA256 hmac = new HMACSHA256(secretKeyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
                return Convert.ToBase64String(hashBytes);
            }
        }

        private string GenerateSignature(string timestamp, string method, string requestPath, string queryString, string body, string secretKey)
        {
            method = method.ToUpper();
            body = string.IsNullOrEmpty(body) ? string.Empty : body;
            queryString = string.IsNullOrEmpty(queryString) ? string.Empty : "?" + queryString;

            string preHash = timestamp + method + Uri.UnescapeDataString(requestPath + queryString) + body;

            return SignHMACSHA256(preHash, secretKey);
        }

        #endregion

        #region 13 Log

        public event Action<string, LogMessageType> LogMessageEvent;

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);
        }

        #endregion
    }
}