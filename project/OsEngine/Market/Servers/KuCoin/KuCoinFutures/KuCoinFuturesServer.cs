﻿using Newtonsoft.Json;
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
        public KuCoinFuturesServer(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            KuCoinFuturesServerRealization realization = new KuCoinFuturesServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterPassphrase, "");
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

            Thread threadGetPortfolios = new Thread(ThreadGetPortfolios);
            threadGetPortfolios.IsBackground = true;
            threadGetPortfolios.Name = "ThreadKuCoinFuturesPortfolios";
            threadGetPortfolios.Start();
        }

        public DateTime ServerTime { get; set; }

        public void Connect(WebProxy proxy)
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
                    CreatePublicWebSocketConnect();
                    CreatePrivateWebSocketConnect();
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

        private List<string> _listCurrency = new List<string>() { "XBT", "ETH", "USDC", "USDT", "SOL", "DOT", "XRP" }; // list of currencies on the exchange

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

                    if (item.isInverse == "true")
                    {
                        newSecurity.NameClass = "Inverse_" + item.quoteCurrency;
                    }
                    else
                    {
                        newSecurity.NameClass = item.quoteCurrency;
                    }

                    newSecurity.NameId = item.symbol;
                    newSecurity.SecurityType = SecurityType.Futures;

                    newSecurity.PriceStep = item.tickSize.ToDecimal();
                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                    newSecurity.Lot = item.lotSize.ToDecimal();

                    newSecurity.Decimals = item.tickSize.DecimalsCount();
                    newSecurity.DecimalsVolume = item.multiplier.DecimalsCount();
                    newSecurity.MinTradeAmountType = MinTradeAmountType.Contract;
                    newSecurity.MinTradeAmount = Math.Abs(item.multiplier.ToDecimal());
                    newSecurity.VolumeStep = Math.Abs(item.multiplier.ToDecimal());

                    securities.Add(newSecurity);
                }
            }

            SecurityEvent(securities);
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        public List<Portfolio> Portfolios;

        private void ThreadGetPortfolios()
        {
            Thread.Sleep(10000);

            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(3000);
                    continue;
                }

                try
                {
                    Thread.Sleep(5000);

                    for (int i = 0; i < _listCurrency.Count; i++)
                    {
                        CreateQueryPortfolio(false, _listCurrency[i]); // create portfolios from a list of currencies
                    }

                    CreateQueryPositions(false);
                    GetUSDTMasterPortfolio(false);
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                }
            }
        }

        public void GetPortfolios()
        {
            if (Portfolios == null)
            {
                Portfolios = new List<Portfolio>();

                Portfolio portfolioInitial = new Portfolio();
                portfolioInitial.Number = "KuCoinFutures";
                portfolioInitial.ValueBegin = 1;
                portfolioInitial.ValueCurrent = 1;
                portfolioInitial.ValueBlocked = 0;

                Portfolios.Add(portfolioInitial);

                PortfolioEvent(Portfolios);
            }

            for (int i = 0; i < _listCurrency.Count; i++)
            {
                CreateQueryPortfolio(true, _listCurrency[i]); // create portfolios from a list of currencies
            }

            CreateQueryPositions(true);
            GetUSDTMasterPortfolio(true);
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
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
            DateTime timeEnd = DateTime.UtcNow;
            DateTime timeStart = timeEnd.AddMinutes(-tfTotalMinutes * candleCount);

            return GetCandleDataToSecurity(security, timeFrameBuilder, timeStart, timeEnd, timeStart);
        }

        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf, bool IsOsData, int CountToLoad, DateTime timeEnd)
        {
            // From technical support chat: Right now the kucoin servers are only returning 24 hours for the lower timeframes and no more than 30 days for higher timeframes. Hopefully their new API fixes this, but no word yet when this will be.

            int needToLoadCandles = CountToLoad;

            List<Candle> candles = new List<Candle>();

            DateTime fromTime = timeEnd - TimeSpan.FromMinutes(tf.TotalMinutes * CountToLoad);

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

        private List<WebSocket> _webSocketPublic = new List<WebSocket>();

        private string _webSocketPrivateUrl = "wss://ws-api-futures.kucoin.com/?token=xxx&[connectId=xxxxx]";

        private string _webSocketPublicUrl = "wss://ws-api-futures.kucoin.com/?token=xxx&[connectId=xxxxx]";

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
                HttpResponseMessage responseMessage = _httpPublicClient.PostAsync(_baseUrl + "/api/v1/bullet-public", new StringContent(String.Empty, Encoding.UTF8, "application/json")).Result;
                string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;
                ResponsePrivateWebSocketConnection wsResponse = JsonConvert.DeserializeAnonymousType(JsonResponse, new ResponsePrivateWebSocketConnection());

                // set dynamic server address ws
                _webSocketPublicUrl = wsResponse.data.instanceServers[0].endpoint + "?token=" + wsResponse.data.token;

                WebSocket webSocketPublicNew = new WebSocket(_webSocketPublicUrl);
                webSocketPublicNew.SslConfiguration.EnabledSslProtocols
                   = System.Security.Authentication.SslProtocols.Tls12;

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
            _webSocketPrivate.SslConfiguration.EnabledSslProtocols
                = System.Security.Authentication.SslProtocols.Tls12;
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

            if (_webSocketPublic.Count == 0)
            {
                return;
            }

            WebSocket webSocketPublic = _webSocketPublic[_webSocketPublic.Count - 1];

            if (webSocketPublic.ReadyState == WebSocketState.Open
                && _subscribedSecurities.Count != 0
                && _subscribedSecurities.Count % 100 == 0)
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
                webSocketPublic.Send($"{{\"type\": \"subscribe\",\"topic\": \"/contractMarket/ticker:{security.Name}\"}}"); // transactions
                webSocketPublic.Send($"{{\"type\": \"subscribe\",\"topic\": \"/contractMarket/level2Depth5:{security.Name}\"}}"); // MarketDepth 5+5 
            }

            if (_webSocketPrivate != null)
            {
                _webSocketPrivate.Send($"{{\"type\": \"subscribe\", \"privateChannel\": \"true\", \"topic\": \"/contract/position:{security.Name}\"}}"); // change of positions
            }
        }

        private void UnsubscribeFromAllWebSockets()
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
                                        string securityName = _subscribedSecurities[i];

                                        webSocketPublic.Send($"{{\"type\": \"unsubscribe\",\"topic\": \"/contractMarket/tickerV2:{securityName}\"}}"); // transactions
                                        webSocketPublic.Send($"{{\"type\": \"unsubscribe\",\"topic\": \"/contractMarket/level2Depth5:{securityName}\"}}"); // marketDepth
                                        _webSocketPrivate.Send($"{{\"type\": \"unsubscribe\", \"privateChannel\": \"true\", \"topic\": \"/contract/position:{securityName}\"}}"); // change of positions
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
                    _webSocketPrivate.Send($"{{\"type\": \"unsubscribe\", \"privateChannel\": \"true\", \"topic\": \"/contractMarket/tradeOrders\"}}"); // changing orders
                    _webSocketPrivate.Send($"{{\"type\": \"unsubscribe\", \"privateChannel\": \"true\", \"topic\": \"/contractAccount/wallet\"}}"); // portfolio change
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

                        if (action.subject.Equals("walletBalance.change"))
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
                myTrade.Volume = responseT.size.ToDecimal();

                MyTradeEvent(myTrade);
            }
        }

        private void UpdatePortfolio(string message)
        {
            ResponseWebSocketMessageAction<ResponseWebSocketPortfolio> Portfolio = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketPortfolio>());

            Portfolio portfolio = Portfolios[0];

            PositionOnBoard pos = new PositionOnBoard();

            pos.PortfolioName = "KuCoinFutures";
            pos.SecurityNameCode = Portfolio.data.currency;
            pos.ValueCurrent = Portfolio.data.walletBalance.ToDecimal();

            portfolio.SetNewPosition(pos);
            PortfolioEvent(Portfolios);
        }

        private void UpdatePosition(string message)
        {
            ResponseWebSocketMessageAction<ResponseWebSocketPosition> posResponse = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketPosition>());

            Portfolio portfolio = Portfolios[0];

            ResponseWebSocketPosition data = posResponse.data;

            PositionOnBoard pos = new PositionOnBoard();

            pos.PortfolioName = "KuCoinFutures";
            pos.SecurityNameCode = data.symbol;
            pos.ValueCurrent = data.currentQty.ToDecimal();

            portfolio.SetNewPosition(pos);
            PortfolioEvent(Portfolios);
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
            Side.TryParse(item.side, true, out newOrder.Side);

            newOrder.State = stateType;
            newOrder.Volume = item.size == null ? item.filledSize.Replace('.', ',').ToDecimal() : item.size.Replace('.', ',').ToDecimal();
            newOrder.Price = item.price != null ? item.price.Replace('.', ',').ToDecimal() : 0;

            newOrder.ServerType = ServerType.KuCoinFutures;
            newOrder.PortfolioNumber = "KuCoinFutures";

            if (stateType == OrderStateType.Done || (stateType == OrderStateType.Partial && item.size != item.filledSize))
            {
                // as soon as an order is executed or partially executed, a trigger is sent to request my trade by the name of the security
                CreateQueryMyTrade(newOrder.SecurityNameCode, newOrder.NumberMarket);
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
            dataSerializerSettings.NullValueHandling = NullValueHandling.Ignore;// if it's a market order, then we ignore the price parameter

            string jsonRequest = JsonConvert.SerializeObject(data, dataSerializerSettings);

            // for the test you can use "/api/v1/orders/test"
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
            _rateGateSendOrder.WaitToProceed();

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
                            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.data.items[i].updatedAt));
                            newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.data.items[i].orderTime) / 1000000); //from nanoseconds to ms
                            newOrder.ServerType = ServerType.KuCoinFutures;

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

                            OrderStateType stateType = GetOrderState(order.data.items[i].status, order.data.items[i].type);
                            newOrder.State = stateType;
                            newOrder.Volume = order.data.items[i].size == null ? order.data.items[i].filledSize.Replace('.', ',').ToDecimal() : order.data.items[i].size.Replace('.', ',').ToDecimal();
                            newOrder.Price = order.data.items[i].price != null ? order.data.items[i].price.Replace('.', ',').ToDecimal() : 0;
                            newOrder.PortfolioNumber = "KuCoinFutures";

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
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
            return null;
        }

        public void GetOrderStatus(Order order)
        {
            Order orderFromExchange = GetOrderFromExchange(order.SecurityNameCode, order.NumberMarket, order.NumberUser);

            if (orderFromExchange == null)
            {
                return;
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
                return;
            }

            if (orderOnMarket != null &&
                MyOrderEvent != null)
            {
                MyOrderEvent(orderOnMarket);
            }

            if (orderOnMarket.State == OrderStateType.Done
                || orderOnMarket.State == OrderStateType.Partial)
            {
                CreateQueryMyTrade(order.SecurityNameCode, order.NumberMarket);
            }
        }

        private Order GetOrderFromExchange(string securityNameCode, string numberMarket, int numberUser)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                string path = null;

                if (numberMarket != null)
                {
                    path = $"/api/v1/orders/{numberMarket}";
                }
                else
                {
                    path = $"/api/v1/orders/{numberUser}";
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
                            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.data.updatedAt));
                            newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.data.orderTime) / 1000000); //from nanoseconds to ms
                            newOrder.ServerType = ServerType.KuCoinFutures;

                            try
                            {
                                newOrder.NumberUser = Convert.ToInt32(order.data.clientOid);
                            }
                            catch
                            {

                            }

                            if (order.data.type == "market")
                            {
                                newOrder.TypeOrder = OrderPriceType.Market;
                            }
                            if (order.data.type == "limit")
                            {
                                newOrder.TypeOrder = OrderPriceType.Limit;
                            }

                            newOrder.NumberMarket = order.data.id;
                            newOrder.Side = order.data.side.Equals("buy") ? Side.Buy : Side.Sell;

                            OrderStateType stateType = GetOrderState(order.data.status, order.data.type);
                            newOrder.State = stateType;

                            if (newOrder.State == OrderStateType.Done)
                            {
                                newOrder.TimeDone = newOrder.TimeCreate;
                                newOrder.TimeCancel = newOrder.TimeCreate;
                            }

                            newOrder.Volume = order.data.size == null ? order.data.filledSize.Replace('.', ',').ToDecimal() : order.data.size.Replace('.', ',').ToDecimal();
                            newOrder.Price = order.data.price != null ? order.data.price.Replace('.', ',').ToDecimal() : 0;
                            newOrder.PortfolioNumber = "KuCoinFutures";
                        }

                        return newOrder;
                    }
                    else
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n" + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
            return null;
        }

        #endregion

        #region 12 Queries

        private string _baseUrl = "https://api-futures.kucoin.com";

        private RateGate _rateGateGetMyTradeState = new RateGate(1, TimeSpan.FromMilliseconds(200));

        HttpClient _httpPublicClient = new HttpClient();

        private void CreateQueryPortfolio(bool IsUpdateValueBegin, string currency = "USDT")
        {
            try
            {
                HttpResponseMessage responseMessage = CreatePrivateQuery("/api/v1/account-overview", "GET", "currency=" + currency, null);
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

            Portfolio portfolio = Portfolios[0];

            ResponseAsset item = asset.data;

            PositionOnBoard pos = new PositionOnBoard();

            pos.PortfolioName = "KuCoinFutures";
            pos.SecurityNameCode = item.currency;
            pos.ValueCurrent = item.accountEquity.ToDecimal();
            pos.ValueBlocked = item.orderMargin.ToDecimal();

            if (IsUpdateValueBegin)
            {
                pos.ValueBegin = item.marginBalance.ToDecimal();
            }

            portfolio.SetNewPosition(pos);
            PortfolioEvent(Portfolios);
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
                    }
                    else
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

            Portfolio portfolio = Portfolios[0];

            for (int i = 0; i < assets.data.Count; i++)
            {
                ResponsePosition item = assets.data[i];
                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = "KuCoinFutures";
                pos.SecurityNameCode = item.symbol;
                pos.UnrealizedPnl = item.unrealisedPnl.ToDecimal();
                pos.ValueCurrent = item.currentQty.ToDecimal();

                if (IsUpdateValueBegin)
                {
                    pos.ValueBegin = item.currentQty.ToDecimal();
                }

                portfolio.SetNewPosition(pos);
            }

            PortfolioEvent(Portfolios);
        }

        private void GetUSDTMasterPortfolio(bool IsUpdateValueBegin)
        {
            Portfolio portfolio = Portfolios[0];

            List<PositionOnBoard> positionOnBoard = Portfolios[0].GetPositionOnBoard();

            decimal positionInUSDT = 0;
            decimal sizeUSDT = 0;

            for (int i = 0; i < positionOnBoard.Count; i++)
            {
                if (positionOnBoard[i].SecurityNameCode == "USDT")
                {
                    sizeUSDT = positionOnBoard[i].ValueCurrent;
                }
                else if (positionOnBoard[i].SecurityNameCode.Contains("USDTM")
                    || positionOnBoard[i].SecurityNameCode.Contains("USDCM")
                    || positionOnBoard[i].SecurityNameCode.Contains("USDM"))
                {
                    //positionInUSDT += GetPriceSecurity(positionOnBoard[i].SecurityNameCode)  * positionOnBoard[i].ValueCurrent;
                }
                else
                {
                    positionInUSDT += GetPriceSecurity(positionOnBoard[i].SecurityNameCode + "USDTM") * positionOnBoard[i].ValueCurrent;
                }
            }

            if (IsUpdateValueBegin)
            {
                portfolio.ValueBegin = Math.Round(sizeUSDT + positionInUSDT, 4);
            }

            portfolio.ValueCurrent = Math.Round(sizeUSDT + positionInUSDT, 4);

            if (portfolio.ValueCurrent == 0)
            {
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;
            }
        }

        private decimal GetPriceSecurity(string security)
        {
            try
            {
                HttpResponseMessage responseMessage = CreatePrivateQuery("/api/v1/ticker", "GET", "symbol=" + security, null);
                string json = responseMessage.Content.ReadAsStringAsync().Result;

                ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<object>());

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    if (stateResponse.code == "200000")
                    {
                        ResponseMessageRest<Ticker> ticker = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<Ticker>());

                        decimal priceSecurity = ticker.data.price.ToDecimal();

                        return priceSecurity;
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

            return 0;
        }

        private void CreateQueryMyTrade(string nameSec, string OrdId)
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