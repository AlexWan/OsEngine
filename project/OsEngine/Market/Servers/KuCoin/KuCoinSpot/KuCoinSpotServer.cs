using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Entity.WebSocketOsEngine;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.KuCoin.KuCoinSpot.Json;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;


namespace OsEngine.Market.Servers.KuCoin.KuCoinSpot
{
    public class KuCoinSpotServer : AServer
    {
        public KuCoinSpotServer(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            KuCoinSpotServerRealization realization = new KuCoinSpotServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterPassphrase, "");
            CreateParameterBoolean("Extended Data", false);
        }
    }

    public class KuCoinSpotServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public KuCoinSpotServerRealization()
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
            threadCheckAliveWebSocket.Name = "CheckAliveWebSocketKuCoinSpot";
            threadCheckAliveWebSocket.Start();
        }

        public DateTime ServerTime { get; set; }

        public void Connect(WebProxy proxy)
        {
            PublicKey = ((ServerParameterString)ServerParameters[0]).Value;
            SecretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
            Passphrase = ((ServerParameterPassword)ServerParameters[2]).Value;

            if (string.IsNullOrEmpty(PublicKey) ||
               string.IsNullOrEmpty(SecretKey) ||
               string.IsNullOrEmpty(Passphrase))
            {
                SendLogMessage("Can`t run KuCoin Spot connector. No keys or passphrase",
                    LogMessageType.Error);
                return;
            }

            if (((ServerParameterBool)ServerParameters[3]).Value == true)
            {
                _extendedMarketData = true;
            }
            else
            {
                _extendedMarketData = false;
            }

            try
            {
                RestRequest requestRest = new RestRequest("/api/v1/timestamp", Method.GET);
                IRestResponse responseMessage = new RestClient(BaseUrl).Execute(requestRest);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {

                    _webSocketPublicMessages = new ConcurrentQueue<string>();
                    _webSocketPrivateMessages = new ConcurrentQueue<string>();
                    CreatePublicWebSocketConnect();
                    CreatePrivateWebSocketConnect();
                    CheckActivationSockets();
                }
                else
                {
                    SendLogMessage("Connection cannot be open. KuCoinSpot. Error request", LogMessageType.Error);
                    Disconnect();
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                SendLogMessage("Connection cannot be open. KuCoinSpot. Error request", LogMessageType.Error);
                Disconnect();
            }
        }

        public void Dispose()
        {
            try
            {
                unsubscribeFromAllWebSockets();
                _subscribedSecurities.Clear();
                DeleteWebsocketConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

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

        public ServerType ServerType
        {
            get { return ServerType.KuCoinSpot; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string PublicKey;

        private string SecretKey;

        private string Passphrase;

        private bool _extendedMarketData;

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            try
            {
                HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(BaseUrl + "/api/v2/symbols").Result;
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

                if (item.enableTrading.Equals("true"))
                {
                    Security newSecurity = new Security();

                    newSecurity.Exchange = ServerType.KuCoinSpot.ToString();
                    newSecurity.Lot = 1;
                    newSecurity.Name = item.symbol;
                    newSecurity.NameFull = item.symbol;
                    newSecurity.NameClass = item.symbol.Split('-')[1];
                    newSecurity.NameId = item.symbol;
                    newSecurity.SecurityType = SecurityType.CurrencyPair;

                    if (string.IsNullOrEmpty(item.priceIncrement) == false)
                    {
                        newSecurity.Decimals = item.priceIncrement.DecimalsCount();
                    }

                    if (string.IsNullOrEmpty(item.baseIncrement) == false)
                    {
                        newSecurity.DecimalsVolume = item.baseIncrement.DecimalsCount();
                        newSecurity.VolumeStep = item.baseIncrement.ToDecimal();
                    }

                    newSecurity.PriceStep = item.priceIncrement.ToDecimal();
                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                    newSecurity.State = SecurityStateType.Activ;
                    newSecurity.MinTradeAmountType = MinTradeAmountType.Contract;
                    newSecurity.MinTradeAmount = item.baseMinSize.ToDecimal();

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
            CreateQueryPortfolio();
            _portfolioIsStarted = true;
        }

        private bool _portfolioIsStarted = false;

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
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
            DateTime timeEnd = DateTime.UtcNow;
            DateTime timeStart = timeEnd.AddMinutes(-tfTotalMinutes * candleCount);

            return GetCandleDataToSecurity(security, timeFrameBuilder, timeStart, timeEnd, timeStart);
        }

        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf, bool IsOsData, int CountToLoad, DateTime timeEnd)
        {
            int needToLoadCandles = CountToLoad;

            List<Candle> candles = new List<Candle>();
            DateTime fromTime = timeEnd - TimeSpan.FromMinutes(tf.TotalMinutes * CountToLoad);

            do
            {
                int limit = needToLoadCandles;
                // KuCoin limitation: For each query, the system would return at most 1500 pieces of data. To obtain more data, please page the data by time.
                if (needToLoadCandles > 1500)
                {
                    limit = 1500;
                }

                List<Candle> rangeCandles = new List<Candle>();

                rangeCandles = CreateQueryCandles(nameSec, GetStringInterval(tf), fromTime, timeEnd);

                if (rangeCandles == null
                    || rangeCandles.Count == 0)
                {
                    return null; // no data
                }

                rangeCandles.Reverse();

                candles.InsertRange(0, rangeCandles);

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

            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

            if (!CheckTf(tfTotalMinutes))
            {
                return null;
            }

            int countNeedToLoad = GetCountCandlesFromSliceTime(startTime, endTime, timeFrameBuilder.TimeFrameTimeSpan);
            return GetCandleHistory(security.NameFull, timeFrameBuilder.TimeFrameTimeSpan, true, countNeedToLoad, endTime);
        }

        private bool CheckTf(int timeFrameMinutes)
        {
            if (timeFrameMinutes == 1 ||
                timeFrameMinutes == 5 ||
                timeFrameMinutes == 15 ||
                timeFrameMinutes == 30 ||
                timeFrameMinutes == 60 ||
                timeFrameMinutes == 120 ||
                timeFrameMinutes == 240)
            {
                return true;
            }

            return false;
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
            // Type of candlestick patterns: 1min, 3min, 5min, 15min, 30min, 1hour, 2hour, 4hour
            if (tf.Minutes != 0)
            {
                return $"{tf.Minutes}min";
            }
            else
            {
                return $"{tf.Hours}hour";
            }
        }

        #endregion

        #region 6 WebSocket creation

        private WebSocket _webSocketPrivate;

        private List<WebSocket> _webSocketPublic = new List<WebSocket>();

        private string _webSocketPrivateUrl = "wss://ws-api-spot.kucoin.com/?token=xxx&[connectId=xxxxx]";

        private string _webSocketPublicUrl = "wss://ws-api-spot.kucoin.com/?token=xxx&[connectId=xxxxx]";

        private string _socketLocker = "webSocketLockerKuCoin";

        private void CreatePublicWebSocketConnect()
        {
            try
            {
                if (_webSocketPublicMessages == null)
                {
                    _webSocketPublicMessages = new ConcurrentQueue<string>();
                }

                _webSocketPublic.Add(CreateNewPublicSocket());
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private WebSocket CreateNewPublicSocket()
        {
            try
            {
                HttpResponseMessage responseMessage = _httpPublicClient.PostAsync(BaseUrl + "/api/v1/bullet-public", new StringContent(String.Empty, Encoding.UTF8, "application/json")).Result;
                string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;
                ResponsePrivateWebSocketConnection wsResponse = JsonConvert.DeserializeAnonymousType(JsonResponse, new ResponsePrivateWebSocketConnection());

                // set dynamic server address ws
                _webSocketPublicUrl = wsResponse.data.instanceServers[0].endpoint + "?token=" + wsResponse.data.token;

                WebSocket webSocketPublicNew = new WebSocket(_webSocketPublicUrl);

                webSocketPublicNew.EmitOnPing = true;
                webSocketPublicNew.OnOpen += _webSocketPublic_OnOpen;
                webSocketPublicNew.OnMessage += _webSocketPublic_OnMessage;
                webSocketPublicNew.OnError += _webSocketPublic_OnError;
                webSocketPublicNew.OnClose += _webSocketPublic_OnClose;
                webSocketPublicNew.Connect();

                return webSocketPublicNew;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private void CreatePrivateWebSocketConnect()
        {
            // 1. get websocket address
            HttpResponseMessage responseMessage = CreatePrivateQuery("/api/v1/bullet-private", "POST", null, String.Empty);

            if (responseMessage.IsSuccessStatusCode == false)
            {
                SendLogMessage("KuCoin keys are wrong. Message from server: " + responseMessage.Content.ReadAsStringAsync().Result, LogMessageType.Error);
                return;
            }

            string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

            ResponsePrivateWebSocketConnection wsResponse = JsonConvert.DeserializeAnonymousType(JsonResponse, new ResponsePrivateWebSocketConnection());

            // set dynamic server address ws
            _webSocketPrivateUrl = wsResponse.data.instanceServers[0].endpoint + "?token=" + wsResponse.data.token;
            _webSocketPrivate = new WebSocket(_webSocketPrivateUrl);
            _webSocketPrivate.EmitOnPing = true;
            _webSocketPrivate.OnOpen += _webSocketPrivate_OnOpen;
            _webSocketPrivate.OnMessage += _webSocketPrivate_OnMessage;
            _webSocketPrivate.OnError += _webSocketPrivate_OnError;
            _webSocketPrivate.OnClose += _webSocketPrivate_OnClose;
            _webSocketPrivate.Connect();
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

                if (_webSocketPublic.Count == 0)
                {
                    Disconnect();
                    return;
                }

                WebSocket webSocketPublic = _webSocketPublic[0];

                if (webSocketPublic == null
                    || webSocketPublic?.ReadyState != WebSocketState.Open)
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
                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        webSocketPublic.OnOpen -= _webSocketPublic_OnOpen;
                        webSocketPublic.OnClose -= _webSocketPublic_OnClose;
                        webSocketPublic.OnMessage -= _webSocketPublic_OnMessage;
                        webSocketPublic.OnError -= _webSocketPublic_OnError;

                        if (webSocketPublic.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublic.CloseAsync();
                        }

                        webSocketPublic = null;
                    }
                }
                catch
                {
                    // ignore
                }

                _webSocketPublic.Clear();
            }

            if (_webSocketPrivate != null)
            {
                try
                {
                    _webSocketPrivate.OnOpen -= _webSocketPrivate_OnOpen;
                    _webSocketPrivate.OnMessage -= _webSocketPrivate_OnMessage;
                    _webSocketPrivate.OnError -= _webSocketPrivate_OnError;
                    _webSocketPrivate.OnClose -= _webSocketPrivate_OnClose; ;
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
            try
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    string message = this.GetType().Name + OsLocalization.Market.Message101 + "\n";
                    message += OsLocalization.Market.Message102;

                    SendLogMessage(message, LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPublic_OnError(object sender, ErrorEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e.Exception != null)
                {
                    string message = e.Exception.ToString();

                    if (message.Contains("The remote party closed the WebSocket connection"))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Data socket error" + ex.ToString(), LogMessageType.Error);
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
            try
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    string message = this.GetType().Name + OsLocalization.Market.Message101 + "\n";
                    message += OsLocalization.Market.Message102;

                    SendLogMessage(message, LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnError(object sender, ErrorEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e.Exception != null)
                {
                    string message = e.Exception.ToString();

                    if (message.Contains("The remote party closed the WebSocket connection"))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Data socket error" + ex.ToString(), LogMessageType.Error);
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
            _webSocketPrivate.Send($"{{\"type\": \"subscribe\", \"privateChannel\": \"true\", \"topic\": \"/spotMarket/tradeOrdersV2\"}}"); // changing orders
            _webSocketPrivate.Send($"{{\"type\": \"subscribe\", \"privateChannel\": \"true\", \"topic\": \"/account/balance\"}}"); // portfolio change
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
                        Disconnect();
                    }

                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        if (webSocketPublic != null && webSocketPublic?.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublic.Send($"{{\"type\": \"ping\"}}");
                        }
                        else
                        {
                            Disconnect();
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

        private RateGate rateGateSubscribed = new RateGate(1, TimeSpan.FromMilliseconds(220));

        private List<string> _subscribedSecurities = new List<string>();

        public void Subscribe(Security security)
        {
            try
            {
                rateGateSubscribed.WaitToProceed();

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                CreateSubscribedSecurityMessageWebSocket(security);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void CreateSubscribedSecurityMessageWebSocket(Security security)
        {

            for (int i = 0; i < _subscribedSecurities.Count; i++)
            {
                if (_subscribedSecurities[i].Equals(security.Name))
                {
                    return;
                }
            }

            _subscribedSecurities.Add(security.Name);

            if (_webSocketPublic.Count == 0)
            {
                return;
            }

            WebSocket webSocketPublic = _webSocketPublic[_webSocketPublic.Count - 1];

            if (webSocketPublic.ReadyState == WebSocketState.Open
                && _subscribedSecurities.Count != 0
                && _subscribedSecurities.Count % 70 == 0)
            {
                // creating a new socket
                WebSocket newSocket = CreateNewPublicSocket();

                DateTime timeEnd = DateTime.Now.AddSeconds(10);

                while (newSocket.ReadyState != WebSocketState.Open)
                {
                    Thread.Sleep(1000);

                    if (timeEnd < DateTime.Now)
                    {
                        break;
                    }
                }

                if (newSocket.ReadyState == WebSocketState.Open)
                {
                    _webSocketPublic.Add(newSocket);
                    webSocketPublic = newSocket;
                }
            }

            if (webSocketPublic != null)
            {
                lock (_socketLocker)
                {
                    //Push frequency: once every 100ms
                    webSocketPublic.Send($"{{\"type\": \"subscribe\",\"topic\": \"/market/match:{security.Name}\"}}");
                    //Push frequency: once every 100ms
                    webSocketPublic.Send($"{{\"type\": \"subscribe\",\"topic\": \"/spotMarket/level2Depth5:{security.Name}\"}}");

                    if (_extendedMarketData)
                    {
                        webSocketPublic.Send($"{{\"type\": \"subscribe\",\"topic\": \"/market/snapshot:{security.Name}\"}}");
                    }
                }
            }
        }

        private void unsubscribeFromAllWebSockets()
        {
            try
            {
                if (_webSocketPublic.Count != 0
                    && _webSocketPublic != null)
                {
                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        try
                        {
                            if (webSocketPublic != null && webSocketPublic?.ReadyState == WebSocketState.Open)
                            {
                                if (_subscribedSecurities != null)
                                {
                                    for (int i2 = 0; i2 < _subscribedSecurities.Count; i2++)
                                    {
                                        string securityName = _subscribedSecurities[i2];

                                        webSocketPublic.Send($"{{\"type\": \"unsubscribe\",\"topic\": \"/market/ticker:{securityName}\"}}");
                                        webSocketPublic.Send($"{{\"type\": \"unsubscribe\",\"topic\": \"/spotMarket/level2Depth5:{securityName}\"}}");

                                        if (_extendedMarketData)
                                        {
                                            webSocketPublic.Send($"{{\"type\": \"unsubscribe\",\"topic\": \"/market/snapshot:{securityName}\"}}");
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }

            if (_webSocketPrivate != null
              && _webSocketPrivate.ReadyState == WebSocketState.Open)
            {
                try
                {
                    _webSocketPrivate.Send($"{{\"type\": \"unsubscribe\", \"privateChannel\": \"true\", \"topic\": \"/spotMarket/tradeOrdersV2\"}}"); //  changing orders
                    _webSocketPrivate.Send($"{{\"type\": \"unsubscribe\", \"privateChannel\": \"true\", \"topic\": \"/account/balance\"}}"); // portfolio change
                }
                catch
                {
                    // ignore
                }
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
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        Thread.Sleep(1000);
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

                        if (action.subject.Equals("trade.l3match"))
                        {
                            UpdateTrade(message);
                            continue;
                        }

                        if (action.subject.Equals("trade.snapshot"))
                        {
                            UpdateTicker(message);
                            continue;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Thread.Sleep(2000);
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
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        Thread.Sleep(1000);
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

                        if (action.subject.Equals("account.balance"))
                        {
                            UpdatePortfolio(message);
                            continue;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Thread.Sleep(2000);
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }

        private void UpdateTrade(string message)
        {
            try
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
                trade.SecurityNameCode = responseTrade.topic.Split(':')[1];
                trade.Price = responseTrade.data.price.ToDecimal();
                trade.Id = responseTrade.data.sequence;
                trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseTrade.data.time) / 1000000);
                trade.Volume = responseTrade.data.size.ToDecimal();

                if (responseTrade.data.side == "sell")
                {
                    trade.Side = Side.Sell;
                }
                else //(responseTrade.data.side == "buy")
                {
                    trade.Side = Side.Buy;
                }

                NewTradesEvent(trade);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateTicker(string message)
        {
            try
            {
                ResponseWebSocketMessageAction<TickerData> responseTicker = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<TickerData>());

                if (responseTicker == null
                    || responseTicker.data == null
                    || responseTicker.data.data == null)
                {
                    return;
                }

                TickerItem item = responseTicker.data.data;

                SecurityVolumes volume = new SecurityVolumes();

                volume.SecurityNameCode = item.symbol;
                volume.Volume24h = item.marketChange24h.vol.ToDecimal();
                volume.Volume24hUSDT = item.marketChange24h.volValue.ToDecimal();
                volume.TimeUpdate = TimeManager.GetDateTimeFromTimeStamp((long)item.datetime.ToDecimal());

                Volume24hUpdateEvent?.Invoke(volume);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateDepth(string message)
        {
            try
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
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateMyTrade(string json)
        {
            try
            {
                ResponseMessageRest<ResponseMyTrades> responseMyTrades = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<ResponseMyTrades>());

                for (int i = 0; i < responseMyTrades.data.items.Count; i++)
                {
                    ResponseMyTrade responseT = responseMyTrades.data.items[i];

                    MyTrade myTrade = new MyTrade();

                    myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseT.createdAt));
                    myTrade.NumberOrderParent = responseT.orderId;
                    myTrade.NumberTrade = responseT.tradeId;
                    myTrade.Price = responseT.price.ToDecimal();
                    myTrade.SecurityNameCode = responseT.symbol;
                    myTrade.Side = responseT.side.Equals("buy") ? Side.Buy : Side.Sell;

                    string commissionSecName = responseT.feeCurrency;

                    if (myTrade.SecurityNameCode.StartsWith(commissionSecName))
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
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdatePortfolio(string message)
        {
            if (_portfolioIsStarted == false)
            {
                return;
            }

            try
            {
                ResponseWebSocketMessageAction<ResponseWebSocketPortfolio> Portfolio = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketPortfolio>());

                Portfolio portfolio = new Portfolio();
                portfolio.Number = "KuCoinSpot";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;

                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = "KuCoinSpot";
                pos.SecurityNameCode = Portfolio.data.currency;
                pos.ValueBlocked = Portfolio.data.hold.ToDecimal();
                pos.ValueCurrent = Portfolio.data.available.ToDecimal();

                portfolio.SetNewPosition(pos);
                PortfolioEvent(new List<Portfolio> { portfolio });
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateOrder(string message)
        {
            try
            {
                ResponseWebSocketMessageAction<ResponseWebSocketOrder> Order = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketOrder>());

                if (Order.data == null)
                {
                    return;
                }

                ResponseWebSocketOrder item = Order.data;

                OrderStateType stateType = GetOrderState(item.status, item.type);

                if (item.orderType.Equals("market") && stateType == OrderStateType.Active)
                {
                    return;
                }

                Order newOrder = new Order();
                newOrder.SecurityNameCode = item.symbol;
                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts) / 1000000); //from nanoseconds to ms

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

                newOrder.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;
                newOrder.State = stateType;

                if (item.size != null)
                {
                    newOrder.Volume = item.size.Replace('.', ',').ToDecimal();
                }
                else if (item.originSize != null)
                {
                    newOrder.Volume = item.originSize.Replace('.', ',').ToDecimal();
                }
                else
                {
                    newOrder.Volume = item.filledSize.Replace('.', ',').ToDecimal();
                }

                newOrder.Price = item.price != null ? item.price.Replace('.', ',').ToDecimal() : 0;

                newOrder.ServerType = ServerType.KuCoinSpot;
                newOrder.PortfolioNumber = "KuCoinSpot";

                if (stateType == OrderStateType.Done ||
                        stateType == OrderStateType.Partial)
                {
                    CreateQueryMyTrade(newOrder.SecurityNameCode, newOrder.NumberMarket,
                            Convert.ToInt64(item.ts) / 1000000);
                }

                MyOrderEvent(newOrder);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private OrderStateType GetOrderState(string orderStatusResponse, string orderTypeResponse)
        {
            OrderStateType stateType;

            switch (orderStatusResponse)
            {
                case ("new"):
                case ("update"):
                case ("open"):
                    stateType = OrderStateType.Active;
                    break;

                case ("match"):
                    //     if (orderFilledSize.ToDecimal() == orderOriginSize.ToDecimal())
                    //       stateType = OrderStateType.Done;
                    // else
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

        public event Action<Funding> FundingUpdateEvent;

        public event Action<SecurityVolumes> Volume24hUpdateEvent;

        #endregion

        #region 11 Trade

        private RateGate rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        private RateGate rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        public void SendOrder(Order order)
        {
            try
            {
                // https://www.kucoin.com/docs/rest/spot-trading/orders/place-order
                rateGateSendOrder.WaitToProceed();

                SendOrderRequestData data = new SendOrderRequestData();
                data.clientOid = order.NumberUser.ToString();
                data.symbol = order.SecurityNameCode;
                data.side = order.Side.ToString().ToLower();
                data.type = order.TypeOrder.ToString().ToLower();
                data.price = order.TypeOrder == OrderPriceType.Market ? null : order.Price.ToString().Replace(",", ".");
                data.size = order.Volume.ToString().Replace(",", ".");

                JsonSerializerSettings dataSerializerSettings = new JsonSerializerSettings();
                dataSerializerSettings.NullValueHandling = NullValueHandling.Ignore;// if it's a market order, then we ignore the price parameter

                string jsonRequest = JsonConvert.SerializeObject(data, dataSerializerSettings);

                // for the test you can use  "/api/v1/orders/test"
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
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        public void CancelAllOrders()
        {

        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            try
            {
                rateGateCancelOrder.WaitToProceed();

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
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public bool CancelOrder(Order order)
        {
            try
            {
                rateGateCancelOrder.WaitToProceed();

                HttpResponseMessage responseMessage = CreatePrivateQuery("/api/v1/orders/" + order.NumberMarket, "DELETE", null, null);
                string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;
                ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(JsonResponse, new ResponseMessageRest<object>());

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    if (stateResponse.code.Equals("200000") == true)
                    {
                        return true;
                        // ignore
                    }
                    else
                    {
                        GetOrderStatus(order);
                        SendLogMessage($"Code: {stateResponse.code}\n"
                            + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    GetOrderStatus(order);
                    SendLogMessage($"CancelOrder> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                    if (stateResponse != null && stateResponse.code != null)
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n"
                            + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }

            return false;
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
            List<Order> orders = GetAllOrdersFromExchange();

            for (int i = 0; orders != null && i < orders.Count; i++)
            {
                if (orders[i] == null)
                {
                    continue;
                }

                if (orders[i].State != OrderStateType.Active
                    && orders[i].State != OrderStateType.Partial
                    && orders[i].State != OrderStateType.Pending)
                {
                    continue;
                }

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(orders[i]);
                }
            }
        }

        private List<Order> GetAllOrdersFromExchange()
        {
            rateGateSendOrder.WaitToProceed();

            try
            {
                HttpResponseMessage responseMessage = CreatePrivateQuery("/api/v1/orders", "GET", "status=active", null);
                string json = responseMessage.Content.ReadAsStringAsync().Result;
                ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<object>());

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    if (stateResponse.code == "200000")
                    {
                        ResponseMessageRest<ResponseAllOrders> order = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<ResponseAllOrders>());

                        List<Order> orders = new List<Order>();

                        for (int i = 0; i < order.data.items.Count; i++)
                        {
                            if (order.data.items[i].isActive == "false")
                            {
                                continue;
                            }

                            Order newOrder = new Order();

                            newOrder.SecurityNameCode = order.data.items[i].symbol;
                            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.data.items[i].createdAt));
                            newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.data.items[i].createdAt));
                            newOrder.ServerType = ServerType.KuCoinSpot;

                            try
                            {
                                newOrder.NumberUser = Convert.ToInt32(order.data.items[i].clientOid);
                            }
                            catch
                            {

                            }

                            newOrder.NumberMarket = order.data.items[i].id;
                            newOrder.Side = order.data.items[i].side.Equals("buy") ? Side.Buy : Side.Sell;

                            if (order.data.items[i].type == "market")
                            {
                                newOrder.TypeOrder = OrderPriceType.Market;
                            }
                            if (order.data.items[i].type == "limit")
                            {
                                newOrder.TypeOrder = OrderPriceType.Limit;
                            }

                            newOrder.State = OrderStateType.Active;
                            newOrder.Volume = order.data.items[i].size.Replace('.', ',').ToDecimal();
                            newOrder.Price = order.data.items[i].price != null ? order.data.items[i].price.Replace('.', ',').ToDecimal() : 0;
                            newOrder.PortfolioNumber = "KuCoinSpot";

                            orders.Add(newOrder);
                        }
                        return orders;
                    }
                    else
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n" + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
            return null;
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            Order orderFromExchange = GetOrderFromExchange(order.SecurityNameCode, order.NumberMarket, order.NumberUser);

            if (orderFromExchange == null)
            {
                return OrderStateType.None;
            }

            Order orderOnMarket = null;

            if (order.NumberUser != 0
                && orderFromExchange.NumberUser != 0
                && orderFromExchange.NumberUser == order.NumberUser)
            {
                orderOnMarket = orderFromExchange;
            }

            if (string.IsNullOrEmpty(order.NumberMarket) == false
                && order.NumberMarket == orderFromExchange.NumberMarket)
            {
                orderOnMarket = orderFromExchange;
            }

            if (orderOnMarket == null)
            {
                return OrderStateType.None;
            }

            if (orderOnMarket != null &&
                MyOrderEvent != null)
            {
                MyOrderEvent(orderOnMarket);
            }

            if (orderOnMarket.State == OrderStateType.Done
                || orderOnMarket.State == OrderStateType.Partial)
            {
                CreateQueryMyTrade(order.SecurityNameCode, order.NumberMarket, Convert.ToInt64(order.TimeDone));
            }

            return orderOnMarket.State;
        }

        private Order GetOrderFromExchange(string securityNameCode, string numberMarket, int numberUser)
        {
            rateGateSendOrder.WaitToProceed();

            try
            {
                string path = null;

                if (numberMarket != null)
                {
                    path = $"/api/v1/orders/{numberMarket}";
                }
                else
                {
                    path = $"/api/v1/order/client-order/{numberUser}";
                }

                if (path == null)
                {
                    return null;
                }

                HttpResponseMessage responseMessage = CreatePrivateQuery(path, "GET", null, null);
                string json = responseMessage.Content.ReadAsStringAsync().Result;
                ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<object>());

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    if (stateResponse.code == "200000")
                    {
                        ResponseMessageRest<ResponseOrder> order = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<ResponseOrder>());

                        Order newOrder = new Order();

                        if (order.data != null)
                        {
                            newOrder.SecurityNameCode = order.data.symbol;
                            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.data.createdAt));
                            newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.data.createdAt));
                            newOrder.ServerType = ServerType.KuCoinSpot;

                            try
                            {
                                newOrder.NumberUser = Convert.ToInt32(order.data.clientOid);
                            }
                            catch
                            {

                            }

                            newOrder.NumberMarket = order.data.id;
                            newOrder.Side = order.data.side.Equals("buy") ? Side.Buy : Side.Sell;

                            if (order.data.type == "market")
                            {
                                newOrder.TypeOrder = OrderPriceType.Market;
                            }
                            if (order.data.type == "limit")
                            {
                                newOrder.TypeOrder = OrderPriceType.Limit;
                            }

                            newOrder.State = OrderStateType.Active;
                            newOrder.Volume = order.data.size.Replace('.', ',').ToDecimal();
                            newOrder.Price = order.data.price != null ? order.data.price.Replace('.', ',').ToDecimal() : 0;
                            newOrder.PortfolioNumber = "KuCoinSpot";
                        }

                        return newOrder;
                    }
                    else
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n" + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
            return null;
        }

        #endregion

        #region 12 Queries

        private string BaseUrl = "https://api.kucoin.com";

        private RateGate rateGateGetMyTradeState = new RateGate(1, TimeSpan.FromMilliseconds(200));

        HttpClient _httpPublicClient = new HttpClient();

        private void CreateQueryPortfolio()
        {
            try
            {
                HttpResponseMessage responseMessage = CreatePrivateQuery("/api/v1/accounts", "GET", "type=trade", null);
                string json = responseMessage.Content.ReadAsStringAsync().Result;

                ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<object>());

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    if (stateResponse.code == "200000")
                    {
                        UpdatePortfolioREST(json);
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

        private void UpdatePortfolioREST(string json)
        {
            ResponseMessageRest<List<ResponseAsset>> assets = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<List<ResponseAsset>>());

            Portfolio portfolio = new Portfolio();

            portfolio.Number = "KuCoinSpot";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            List<PositionOnBoard> alreadySendPositions = new List<PositionOnBoard>();

            for (int i = 0; i < assets.data.Count; i++)
            {
                ResponseAsset item = assets.data[i];
                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = "KuCoinSpot";
                pos.SecurityNameCode = item.currency;
                pos.ValueBlocked = item.holds.ToDecimal();
                pos.ValueCurrent = item.available.ToDecimal();
                pos.ValueBegin = item.balance.ToDecimal();

                bool canSend = true;

                for (int j = 0; j < alreadySendPositions.Count; j++)
                {
                    if (alreadySendPositions[j].SecurityNameCode == pos.SecurityNameCode
                        && pos.ValueCurrent == 0)
                    {
                        canSend = false;
                        break;
                    }
                }

                if (canSend)
                {
                    portfolio.SetNewPosition(pos);
                    alreadySendPositions.Add(pos);
                }
            }

            PortfolioEvent(new List<Portfolio> { portfolio });
        }

        private void CreateQueryMyTrade(string nameSec, string OrdId, long ts)
        {
            Thread.Sleep(2000);
            rateGateGetMyTradeState.WaitToProceed();
            //long tsStart = ts - 20000;
            //long tsEnd = TimeManager.GetUnixTimeStampMilliseconds();

            HttpResponseMessage responseMessage = CreatePrivateQuery(
                "/api/v1/fills",
                "GET",
                "symbol=" + nameSec + "&orderId=" + OrdId + "&tradeType=TRADE",//"&startAt="+tsStart.ToString()+"&endAt="+tsEnd.ToString(),
                null);
            string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

            ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(JsonResponse, new ResponseMessageRest<object>());

            if (responseMessage.StatusCode == HttpStatusCode.OK)
            {
                if (stateResponse.code.Equals("200000") == true)
                {
                    ResponseMessageRest<ResponseMyTrades> responseMyTrades = JsonConvert.DeserializeAnonymousType(JsonResponse, new ResponseMessageRest<ResponseMyTrades>());
                    if (responseMyTrades.data.totalNum.Equals("0"))
                    {
                        CreateQueryMyTrade(nameSec, OrdId, ts);
                        return;
                    }

                    UpdateMyTrade(JsonResponse);
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
            _rateGateCandleHistory.WaitToProceed(100);

            // /api/v1/market/candles?type=1min&symbol=BTC-USDT&startAt=1566703297&endAt=1566789757
            HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(BaseUrl + $"/api/v1/market/candles?symbol={nameSec}&type={stringInterval}&startAt={TimeManager.GetTimeStampSecondsToDateTime(timeFrom)}&endAt={TimeManager.GetTimeStampSecondsToDateTime(timeTo)}").Result;
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

                        Candle newCandle = new Candle();

                        newCandle.Open = item[1].ToDecimal();
                        newCandle.Close = item[2].ToDecimal();
                        newCandle.High = item[3].ToDecimal();
                        newCandle.Low = item[4].ToDecimal();
                        newCandle.Volume = item[5].ToDecimal();
                        newCandle.State = CandleState.Finished;
                        newCandle.TimeStart = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(item[0]));
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
                SendLogMessage($"State Code: {responseMessage.StatusCode}", LogMessageType.Error);
                return null;
            }
        }

        private HttpResponseMessage CreatePrivateQuery(string path, string method, string queryString, string body)
        {
            string requestPath = path;
            string url = $"{BaseUrl}{requestPath}";
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string signature = GenerateSignature(timestamp, method, requestPath, queryString, body, SecretKey);

            HttpClient httpClient = new HttpClient();

            httpClient.DefaultRequestHeaders.Add("KC-API-KEY", PublicKey);
            httpClient.DefaultRequestHeaders.Add("KC-API-SIGN", signature);
            httpClient.DefaultRequestHeaders.Add("KC-API-TIMESTAMP", timestamp);
            httpClient.DefaultRequestHeaders.Add("KC-API-PASSPHRASE", SignHMACSHA256(Passphrase, SecretKey));
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

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}
