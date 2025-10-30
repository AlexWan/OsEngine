/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Entity.WebSocketOsEngine;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.AscendexSpot.Entity;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;


namespace OsEngine.Market.Servers.AscendexSpot
{
    public class AscendexSpotServer : AServer
    {
        public AscendexSpotServer(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            AscendexSpotServerRealization realization = new AscendexSpotServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
        }
    }

    public class AscendexSpotServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public AscendexSpotServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread threadForPublicMessages = new Thread(PublicMessageReader);
            threadForPublicMessages.IsBackground = true;
            threadForPublicMessages.Name = "PublicMessageReaderAscendexSpot";
            threadForPublicMessages.Start();

            Thread threadForPrivateMessages = new Thread(PrivateMessageReader);
            threadForPrivateMessages.IsBackground = true;
            threadForPrivateMessages.Name = "PrivateMessageReaderAscendexSpot";
            threadForPrivateMessages.Start();

            Thread threadCheckAliveWebSocket = new Thread(CheckAliveWebSocket);
            threadCheckAliveWebSocket.IsBackground = true;
            threadCheckAliveWebSocket.Name = "CheckAliveWebSocket";
            threadCheckAliveWebSocket.Start();
        }

        private WebProxy _myProxy;

        public void Connect(WebProxy proxy = null)
        {
            LoadOrderTrackers();

            _myProxy = proxy;

            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

            if (string.IsNullOrEmpty(_publicKey)
                || string.IsNullOrEmpty(_secretKey))
            {
                SendLogMessage("Error:Invalid public or secret key.", LogMessageType.Error);
                return;
            }

            try
            {
                _rateGateConnect.WaitToProceed();

                IRestResponse response = CreatePrivateQuery("/api/pro/v1/info", "info", null, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    AscendexSpotApiKeyInfoResponse result = JsonConvert.DeserializeObject<AscendexSpotApiKeyInfoResponse>(response.Content);

                    if (result != null && result.code == "0")
                    {
                        _accountGroup = result.data.accountGroup;
                        CreatePublicWebSocketConnect();
                        CreatePrivateWebSocketConnect();

                        SendLogMessage("Start AscendexSpot Connection", LogMessageType.System);
                    }
                    else
                    {
                        SendLogMessage("Status: Maintenance mode", LogMessageType.System);
                        Disconnect();
                    }
                }
                else
                {
                    SendLogMessage($"No connection to AscendexSpot server. Code:{response.StatusCode}, Error:{response.Content}", LogMessageType.Error);
                    Disconnect();
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in Connect: {exception}", LogMessageType.Error);
                Disconnect();
            }
        }

        public void Dispose()
        {
            try
            {
                UnsubscribeFromAllWebSockets();
                DeleteWebSocketConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in Dispose: {exception}", LogMessageType.Error);
            }

            FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
            FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();
            _subscribedSecurities.Clear();
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

        public DateTime ServerTime { get; set; }

        private RateGate _rateGateConnect = new RateGate(1, TimeSpan.FromMicroseconds(300));

        public ServerType ServerType
        {
            get { return ServerType.AscendexSpot; }
        }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        public ServerConnectStatus ServerStatus { get; set; }

        private string _publicKey = "";

        private string _secretKey = "";

        private string _accountGroup = "";

        private string _baseUrl = "https://ascendex.com";

        private string _accountCategory = "cash";

        private string _portfolioName = "AscendEXSpotPortfolio";

        #endregion

        #region 3 Securities

        private RateGate _rateGateSecurity = new RateGate(1, TimeSpan.FromMilliseconds(210));

        public void GetSecurities()
        {
            try
            {
                _rateGateSecurity.WaitToProceed();

                string _apiPath = $"api/pro/v1/{_accountCategory}/products";

                IRestResponse response = CreatePublicQuery(_apiPath, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    AscendexSpotSecurityResponse securityList = JsonConvert.DeserializeObject<AscendexSpotSecurityResponse>(response.Content);

                    if (securityList == null)
                    {
                        SendLogMessage("GetSecurities> Deserialization resulted in null", LogMessageType.Error);
                        return;
                    }

                    if (securityList.code == "0")
                    {
                        if (securityList.data.Count > 0)
                        {
                            SendLogMessage("Securities loaded. Count: " + securityList.data.Count, LogMessageType.System);
                        }

                        List<Security> securities = new List<Security>();

                        for (int i = 0; i < securityList.data.Count; i++)
                        {
                            string symbol = securityList.data[i].symbol;
                            string domain = securityList.data[i].domain;
                            string statusCode = securityList.data[i].statusCode;

                            if (symbol.Contains("$") ||
                                domain.Contains("LeveragedETF") ||
                                statusCode != "Normal")
                            {
                                continue;
                            }

                            Security newSecurity = new Security();

                            newSecurity.Exchange = ServerType.AscendexSpot.ToString();
                            newSecurity.Name = symbol;
                            newSecurity.NameFull = symbol;
                            newSecurity.NameClass = GetNameClass(symbol);
                            newSecurity.NameId = symbol + securityList.data[i].tradingStartTime;
                            newSecurity.SecurityType = SecurityType.CurrencyPair;
                            newSecurity.Lot = 1;
                            newSecurity.State = SecurityStateType.Activ;
                            newSecurity.PriceStep = securityList.data[i].tickSize.ToDecimal();
                            newSecurity.Decimals = Convert.ToInt32(securityList.data[i].priceScale);
                            newSecurity.PriceStepCost = newSecurity.PriceStep;
                            newSecurity.DecimalsVolume = Convert.ToInt32(securityList.data[i].qtyScale);
                            newSecurity.MinTradeAmount = securityList.data[i].minNotional.ToDecimal();
                            newSecurity.MinTradeAmountType = MinTradeAmountType.C_Currency;
                            newSecurity.VolumeStep = newSecurity.DecimalsVolume.GetValueByDecimals();

                            securities.Add(newSecurity);
                        }

                        SecurityEvent?.Invoke(securities);
                    }
                    else
                    {
                        SendLogMessage($"Securities error. {response.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Securities request error. Code:{response.StatusCode}, Error:{response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in GetSecurities: {exception}", LogMessageType.Error);
            }
        }

        private string GetNameClass(string security)
        {
            if (security.EndsWith("USD"))
            {
                return "USD";
            }
            else if (security.EndsWith("USDT"))
            {
                return "USDT";
            }
            else if (security.EndsWith("BTC"))
            {
                return "BTC";
            }

            return "CurrencyPair";
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private RateGate _rateGatePortfolio = new RateGate(1, TimeSpan.FromMilliseconds(200));

        public void GetPortfolios()
        {
            try
            {
                _rateGatePortfolio.WaitToProceed();

                string fullPath = $"/{_accountGroup}/api/pro/v1/{_accountCategory}/balance";

                IRestResponse response = CreatePrivateQuery(fullPath, "balance", null, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BalanceResponse wallets = JsonConvert.DeserializeObject<BalanceResponse>(response.Content);

                    if (wallets.code == "0")
                    {
                        Portfolio portfolio = new Portfolio();

                        portfolio.Number = _portfolioName;
                        portfolio.ValueBegin = 1;
                        portfolio.ValueCurrent = 1;

                        for (int i = 0; i < wallets.data.Count; i++)
                        {
                            PositionOnBoard position = new PositionOnBoard();

                            position.PortfolioName = _portfolioName;
                            position.SecurityNameCode = wallets.data[i].asset;
                            position.ValueBegin = wallets.data[i].totalBalance.ToDecimal();
                            position.ValueCurrent = wallets.data[i].availableBalance.ToDecimal();
                            position.ValueBlocked = position.ValueBegin - position.ValueCurrent;

                            portfolio.SetNewPosition(position);
                        }

                        PortfolioEvent(new List<Portfolio> { portfolio });
                    }
                    else
                    {
                        SendLogMessage($"Portfolio error. {response.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Portfolio request error. Code:{response.StatusCode}, Error:{response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in CreateQueryPortfolio: {exception.ToString()}", LogMessageType.Error);
            }
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
            endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
            actualTime = DateTime.SpecifyKind(actualTime, DateTimeKind.Utc);

            if (startTime != actualTime)
            {
                startTime = actualTime;
            }

            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

            if (!CheckTf(tfTotalMinutes))
            {
                return null;
            }

            if (endTime > DateTime.UtcNow)
            {
                endTime = DateTime.UtcNow;
            }

            if (!CheckTime(startTime, endTime, actualTime))
            {
                return null;
            }

            int countNeedToLoad = GetCountCandlesFromPeriod(startTime, endTime, timeFrameBuilder.TimeFrameTimeSpan);

            if (countNeedToLoad <= 0)
            {
                return null;
            }

            List<Candle> candles = GetCandleHistory(security.NameFull, timeFrameBuilder.TimeFrameTimeSpan, true, countNeedToLoad, endTime);

            if (candles == null || candles.Count == 0)
            {
                return null;
            }

            return candles;
        }

        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf, bool isOsData, int countToLoad, DateTime timeEnd)
        {
            string timeFrame = GetInterval(tf);

            int limit = 480;

            List<Candle> allCandles = new List<Candle>();

            HashSet<DateTime> uniqueTimes = new HashSet<DateTime>();

            int candlesLoaded = 0;

            DateTime periodEnd = timeEnd;

            DateTime periodStart = timeEnd.AddMinutes(-countToLoad * tf.TotalMinutes);

            while (candlesLoaded < countToLoad)
            {
                int candlesToLoad = Math.Min(limit, countToLoad - candlesLoaded);

                List<Candle> rangeCandles = CreateQueryCandles(nameSec, timeFrame, periodEnd, candlesToLoad);

                if (rangeCandles == null || rangeCandles.Count == 0)
                {
                    break;
                }

                for (int i = 0; i < rangeCandles.Count; i++)
                {
                    if (uniqueTimes.Add(rangeCandles[i].TimeStart))
                    {
                        allCandles.Add(rangeCandles[i]);
                    }
                }

                periodEnd = rangeCandles[0].TimeStart;

                if (periodEnd <= periodStart)
                {
                    break;
                }

                candlesLoaded += rangeCandles.Count;
            }

            for (int i = allCandles.Count - 1; i >= 0; i--)
            {
                if (allCandles[i].TimeStart < periodStart
                    || allCandles[i].TimeStart > timeEnd)
                {
                    allCandles.RemoveAt(i);
                }
            }

            if (allCandles.Count == 0)
            {
                return null;
            }

            allCandles.Sort((a, b) => a.TimeStart.CompareTo(b.TimeStart));

            return allCandles;
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
            DateTime timeEnd = DateTime.UtcNow;
            DateTime timeStart = timeEnd.AddMinutes(-tfTotalMinutes * candleCount);

            return GetCandleDataToSecurity(security, timeFrameBuilder, timeStart, timeEnd, timeStart);
        }

        private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (startTime >= endTime ||
                startTime > DateTime.UtcNow ||
                actualTime > endTime ||
                actualTime > DateTime.UtcNow)
            {
                SendLogMessage("Error: The date is incorrect", LogMessageType.User);
                return false;
            }

            return true;
        }

        private bool CheckTf(int timeFrameMinutes)
        {
            if (timeFrameMinutes == 1 ||
                timeFrameMinutes == 5 ||
                timeFrameMinutes == 15 ||
                timeFrameMinutes == 30 ||
                timeFrameMinutes == 60 ||
                timeFrameMinutes == 120 ||
                timeFrameMinutes == 240 ||
                timeFrameMinutes == 1440)
            {
                return true;
            }

            return false;
        }

        private string GetInterval(TimeSpan tf)
        {
            if (tf.Days > 0)
            {
                return $"{tf.Days}d";
            }
            else if (tf.TotalMinutes > 0)
            {
                return (tf.TotalMinutes).ToString();
            }
            else
            {
                SendLogMessage($"Error: The timeframe is incorrect.Received: {tf}", LogMessageType.User);
                return null;
            }
        }

        private int GetCountCandlesFromPeriod(DateTime startTime, DateTime endTime, TimeSpan tf)
        {
            if (tf.TotalMinutes <= 0)
            {
                SendLogMessage($"Invalid timeframe: {tf}", LogMessageType.Error);
                return 0;
            }

            double totalMinutes = (endTime - startTime).TotalMinutes;

            return Convert.ToInt32(totalMinutes / tf.TotalMinutes);
        }

        private RateGate _rateGateCandleHistory = new RateGate(1, TimeSpan.FromMilliseconds(200));

        private List<Candle> CreateQueryCandles(string symbol, string interval, DateTime endTime, int limit)
        {
            _rateGateCandleHistory.WaitToProceed();

            try
            {
                long endDate = TimeManager.GetTimeStampMilliSecondsToDateTime(endTime);

                string _apiPath = $"/api/pro/v1/barhist?symbol={symbol}&interval={interval}&n={limit}&to={endDate}";

                IRestResponse response = CreatePublicQuery(_apiPath, Method.GET);

                if (response == null)
                {
                    SendLogMessage($"Failed to query candles. Code: {response?.StatusCode}, Error: {response?.Content}", LogMessageType.Error);
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    AscendexSpotCandleResponse json = JsonConvert.DeserializeObject<AscendexSpotCandleResponse>(response.Content);

                    if (json == null || json.code != "0")
                    {
                        SendLogMessage($"Candle response invalid or empty. Symbol={symbol}, Interval={interval}", LogMessageType.Error);
                        return null;
                    }

                    List<Candle> candles = new List<Candle>();

                    for (int i = 0; i < json.data.Count; i++)
                    {
                        AscendexSpotCandleData candleData = json.data[i].data;

                        if (string.IsNullOrWhiteSpace(candleData.o)
                            || string.IsNullOrWhiteSpace(candleData.c)
                            || string.IsNullOrWhiteSpace(candleData.h)
                            || string.IsNullOrWhiteSpace(candleData.l)
                            || string.IsNullOrWhiteSpace(candleData.v))
                        {
                            continue;
                        }

                        decimal open = candleData.o.ToDecimal();
                        decimal close = candleData.c.ToDecimal();
                        decimal high = candleData.h.ToDecimal();
                        decimal low = candleData.l.ToDecimal();
                        decimal volume = candleData.v.ToDecimal();

                        if (open == 0
                            || close == 0
                            || high == 0
                            || low == 0
                            || volume == 0)
                        {
                            continue;
                        }

                        Candle newCandle = new Candle();

                        newCandle.State = CandleState.Finished;
                        newCandle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(candleData.ts));
                        newCandle.Open = open;
                        newCandle.Close = close;
                        newCandle.High = high;
                        newCandle.Low = low;
                        newCandle.Volume = volume;

                        candles.Add(newCandle);
                    }

                    if (candles.Count == 0)
                    {
                        SendLogMessage($"No valid candles returned. Symbol={symbol}, Interval={interval}", LogMessageType.System);
                        return null;
                    }

                    return candles;
                }
                else
                {
                    SendLogMessage($"Failed to query candles. Code: {response.StatusCode}, Error: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in CreateQueryCandles: {exception.ToString()}", LogMessageType.Error);
            }

            return null;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        #endregion

        #region 6 WebSocket creation

        private List<WebSocket> _webSocketPublic = new List<WebSocket>();

        private WebSocket _webSocketPrivate;

        private string _webSocketUrl = "wss://ascendex.com";

        private void CreatePublicWebSocketConnect()
        {
            try
            {
                if (FIFOListWebSocketPublicMessage == null)
                {
                    FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
                }

                _webSocketPublic.Add(CreateNewPublicSocket());
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in CreatePublicWebSocketConnect: {exception.ToString()}", LogMessageType.Error);
            }
        }

        private WebSocket CreateNewPublicSocket()
        {
            try
            {
                WebSocket webSocketPublicNew = new WebSocket($"{_webSocketUrl}/{_accountGroup}/api/pro/v1/stream");

                if (_myProxy != null)
                {
                    webSocketPublicNew.SetProxy(_myProxy);
                }

                webSocketPublicNew.EmitOnPing = false;
                webSocketPublicNew.OnOpen += WebSocketPublicNew_OnOpen;
                webSocketPublicNew.OnClose += WebSocketPublicNew_OnClose;
                webSocketPublicNew.OnMessage += WebSocketPublicNew_OnMessage;
                webSocketPublicNew.OnError += WebSocketPublicNew_OnError;
                webSocketPublicNew.ConnectAsync();

                return webSocketPublicNew;
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in CreateNewPublicSocket: {exception.ToString()}", LogMessageType.Error);
                return null;
            }
        }

        private void CreatePrivateWebSocketConnect()
        {
            try
            {
                if (_webSocketPrivate != null)
                {
                    return;
                }

                _webSocketPrivate = new WebSocket($"{_webSocketUrl}/{_accountGroup}/api/pro/v1/stream");

                if (_myProxy != null)
                {
                    _webSocketPrivate.SetProxy(_myProxy);
                }

                _webSocketPrivate.EmitOnPing = false;
                _webSocketPrivate.OnOpen += _webSocketPrivate_OnOpen;
                _webSocketPrivate.OnClose += _webSocketPrivate_OnClose;
                _webSocketPrivate.OnMessage += _webSocketPrivate_OnMessage;
                _webSocketPrivate.OnError += _webSocketPrivate_OnError;

                _webSocketPrivate.ConnectAsync();
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in CreatePrivateWebSocketConnect: {exception.ToString()}", LogMessageType.Error);
            }
        }

        private void DeleteWebSocketConnection()
        {
            if (_webSocketPublic != null)
            {
                try
                {
                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublicNew = _webSocketPublic[i];

                        webSocketPublicNew.OnOpen -= WebSocketPublicNew_OnOpen;
                        webSocketPublicNew.OnClose -= WebSocketPublicNew_OnClose;
                        webSocketPublicNew.OnMessage -= WebSocketPublicNew_OnMessage;
                        webSocketPublicNew.OnError -= WebSocketPublicNew_OnError;

                        if (webSocketPublicNew.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublicNew.CloseAsync();
                        }

                        webSocketPublicNew = null;
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
                    _webSocketPrivate.OnClose -= _webSocketPrivate_OnClose;
                    _webSocketPrivate.OnMessage -= _webSocketPrivate_OnMessage;
                    _webSocketPrivate.OnError -= _webSocketPrivate_OnError;
                    _webSocketPrivate.CloseAsync();
                }
                catch
                {
                    // ignore
                }

                _webSocketPrivate = null;
            }
        }

        private readonly object _socketActivateLocker = new object();

        private void CheckSocketsActivate()
        {
            try
            {
                lock (_socketActivateLocker)
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
            catch (Exception exception)
            {
                SendLogMessage($"Exception in CheckSocketsActivate: {exception.ToString()}", LogMessageType.Error);
            }
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocketPublicNew_OnOpen(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    CheckSocketsActivate();
                    SendLogMessage("AscendexSpot Public WebSocket  connection open", LogMessageType.System);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in WebSocketPublicNew_OnOpen: {exception}", LogMessageType.Error);
            }
        }

        private void WebSocketPublicNew_OnClose(object sender, CloseEventArgs e)
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

        private void WebSocketPublicNew_OnMessage(object sender, MessageEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect ||
                    e == null ||
                    string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                if (FIFOListWebSocketPublicMessage == null)
                {
                    SendLogMessage("FIFOListWebSocketPublicMessage is null", LogMessageType.Error);
                    return;
                }

                if (e.IsText && e.Data.Contains("\"m\":\"connected\""))
                {
                    if (e.Data.Contains("\"type\":\"unauth\""))
                    {
                        SendLogMessage("WebSocket MarketDepth opened", LogMessageType.System);
                    }

                    return;
                }

                if (e.IsText && e.Data.Contains("\"m\":\"ping\""))
                {
                    WebSocket socket = sender as WebSocket;

                    if (socket != null && socket.ReadyState == WebSocketState.Open)
                    {
                        socket.SendAsync("{\"op\":\"pong\"}");
                    }

                    return;
                }

                if (e.Data.Contains("\"m\":\"error\""))
                {
                    SendLogMessage("Received WebSocket error message: " + e.Data, LogMessageType.Error);
                    return;
                }

                FIFOListWebSocketPublicMessage.Enqueue(e.Data);
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in WebSocketPublicNew_OnMessage: {exception}", LogMessageType.Error);
            }
        }

        private void WebSocketPublicNew_OnError(object sender, OsEngine.Entity.WebSocketOsEngine.ErrorEventArgs e)
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
            catch (Exception exception)
            {
                SendLogMessage($"Exception in WebSocketPublicNew_OnError: {exception}", LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnOpen(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    GenerateAuthenticate();
                    CheckSocketsActivate();
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in _webSocketPrivate_OnOpen: {exception}", LogMessageType.Error);
            }
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

        private void _webSocketPrivate_OnMessage(object sender, MessageEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect ||
                    e == null ||
                    string.IsNullOrEmpty(e.Data) ||
                    FIFOListWebSocketPrivateMessage == null)
                {
                    return;
                }

                if (e.IsText && e.Data.Contains("\"m\":\"ping\""))
                {
                    WebSocket socket = sender as WebSocket;

                    if (socket != null && socket.ReadyState == WebSocketState.Open)
                    {
                        socket.SendAsync("{\"op\":\"pong\"}");
                    }

                    return;
                }

                if (e.IsText && e.Data.Contains("\"m\":\"auth\""))
                {
                    if (e.Data.Contains("\"code\":0"))
                    {
                        SubscriblePrivate();
                        SendLogMessage("Authorization to private channels", LogMessageType.System);
                    }
                    else
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                        SendLogMessage($"WebSocket private channel error {e.Data}", LogMessageType.Error);
                    }

                    return;
                }

                FIFOListWebSocketPrivateMessage.Enqueue(e.Data);
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in _webSocketPrivate_OnMessage: {exception}", LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnError(object sender, OsEngine.Entity.WebSocketOsEngine.ErrorEventArgs e)
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

                    if (message.Contains("The remote party closed the WebSocket AscendexSpot connection"))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in _webSocketPrivate_OnError: {exception}", LogMessageType.Error);
            }
        }

        #endregion

        #region 8 WebSocket check alive

        private void CheckAliveWebSocket()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(14000);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        continue;
                    }

                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        if (webSocketPublic != null
                            && webSocketPublic.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublic.SendAsync("{\"op\":\"ping\"}");
                        }
                        else
                        {
                            Disconnect();
                        }
                    }

                    if (_webSocketPrivate != null
                        && (_webSocketPrivate.ReadyState == WebSocketState.Open
                    || _webSocketPrivate.ReadyState == WebSocketState.Connecting))
                    {
                        _webSocketPrivate.SendAsync("{\"op\":\"ping\"}");
                    }
                    else
                    {
                        Disconnect();
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage($"Exception in  CheckAliveWebSocket: {exception.ToString()}", LogMessageType.Error);
                }
            }
        }

        #endregion

        #region 9  WebSocket security subscribe

        private RateGate _rateGateSubscribed = new RateGate(1, TimeSpan.FromMilliseconds(300));

        List<string> _subscribedSecurities = new List<string>();

        public void Subscribe(Security security)
        {
            try
            {
                _rateGateSubscribed.WaitToProceed();

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    if (_subscribedSecurities.Contains(security.Name))
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
                    webSocketPublic.SendAsync($"{{\"op\":\"req\",\"action\":\"depth-snapshot\",\"args\":{{\"symbol\":\"{security.Name}\"}}}}");
                    webSocketPublic.SendAsync($"{{\"op\":\"sub\",\"ch\":\"depth:{security.Name}\"}}");
                    webSocketPublic.SendAsync($"{{\"op\":\"sub\",\"ch\":\"trades:{security.Name}\"}}");
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in Subscrible: {exception}", LogMessageType.Error);
            }
        }

        private void SubscriblePrivate()
        {
            try
            {
                _webSocketPrivate.SendAsync("{\"op\":\"sub\",\"ch\":\"order:cash\"}");
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Error);
            }
        }

        private void UnsubscribeFromAllWebSockets()
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                for (int i = 0; i < _webSocketPublic.Count; i++)
                {
                    WebSocket webSocketPublic = _webSocketPublic[i];

                    if (webSocketPublic != null && webSocketPublic.ReadyState == WebSocketState.Open)
                    {
                        try
                        {
                            if (_subscribedSecurities != null && _subscribedSecurities.Count > 0)
                            {
                                for (int j = 0; j < _subscribedSecurities.Count; j++)
                                {
                                    string symbol = _subscribedSecurities[j];

                                    webSocketPublic.SendAsync($"{{\"op\":\"unsub\",\"ch\":\"depth:{symbol}\"}}");
                                    webSocketPublic.SendAsync($"{{\"op\":\"unsub\",\"ch\":\"trades:{symbol}\"}}");
                                }
                            }
                        }
                        catch (Exception exception)
                        {
                            SendLogMessage($"PublicSocket> Exception in UnsubscribeFromAllWebSockets: {exception}", LogMessageType.Error);
                        }
                    }
                }

                if (_webSocketPrivate != null && _webSocketPrivate.ReadyState == WebSocketState.Open)
                {
                    try
                    {
                        _webSocketPrivate.SendAsync("{\"op\":\"unsub\",\"ch\":\"order:cash\"}");
                    }
                    catch (Exception exception)
                    {
                        SendLogMessage($"Unsubscribe error on private socket: {exception.Message}", LogMessageType.Error);
                    }
                }

                _subscribedSecurities.Clear();

                SendLogMessage("All subscriptions have been successfully removed", LogMessageType.System);
            }
            catch (Exception exception)
            {
                SendLogMessage($"PrivateSocket> Exception in UnsubscribeFromAllWebSockets: {exception}", LogMessageType.Error);
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        #endregion

        #region 10 WebSocket parsing the messages

        private ConcurrentQueue<string> FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

        private void PublicMessageReader()
        {
            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    if (FIFOListWebSocketPublicMessage.IsEmpty)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    FIFOListWebSocketPublicMessage.TryDequeue(out string message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Contains("\"m\":\"pong\""))
                    {
                        continue;
                    }

                    if (message.Contains("\"m\":\"error\""))
                    {
                        SendLogMessage($"Error  websocketPublic -{message}", LogMessageType.Error);
                        continue;
                    }

                    if (message.Contains("\"m\":\"depth-snapshot\""))
                    {
                        SnapshotDepth(message);
                        continue;
                    }

                    if (message.Contains("\"m\":\"depth\""))
                    {
                        UpdateDepth(message);
                        continue;
                    }

                    if (message.Contains("\"m\":\"trades\""))
                    {
                        UpdateTrade(message);
                        continue;
                    }
                }
                catch (Exception exception)
                {
                    Thread.Sleep(5000);
                    SendLogMessage($"Exception in PublicMessageReader: {exception.ToString()}", LogMessageType.Error);
                }
            }
        }

        private void PrivateMessageReader()
        {
            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    if (FIFOListWebSocketPrivateMessage.IsEmpty)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    FIFOListWebSocketPrivateMessage.TryDequeue(out string message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Contains("\"m\":\"pong\""))
                    {
                        continue;
                    }

                    if (message.Contains("\"m\":\"error\""))
                    {
                        SendLogMessage($"Error webSocketPrivate {message}", LogMessageType.Error);
                        continue;
                    }

                    if (message.Contains("\"m\":\"order\""))
                    {
                        UpdateOrder(message);
                        continue;
                    }

                    if (message.Contains("\"m\":\"balance\""))
                    {
                        UpdateBalance(message);
                        continue;
                    }
                }
                catch (Exception exception)
                {
                    Thread.Sleep(5000);
                    SendLogMessage($"Exception in PrivateMessageReader: {exception.ToString()}", LogMessageType.Error);
                }
            }
        }

        private List<MarketDepth> _allDepths = new List<MarketDepth>();

        private bool _snapshotInitialized = false;

        private long _lastSeqNum = -1;

        private DateTime _lastTimeMd = DateTime.MinValue;

        private void SnapshotDepth(string message)
        {
            try
            {
                AscendexSpotDepthResponse snapshot = JsonConvert.DeserializeObject<AscendexSpotDepthResponse>(message);

                if (snapshot == null || snapshot.data == null)
                {
                    return;
                }

                _lastSeqNum = Convert.ToInt64(snapshot.data.seqnum);
                _snapshotInitialized = true;

                MarketDepth newDepth = new MarketDepth();

                newDepth.SecurityNameCode = snapshot.symbol;
                newDepth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(snapshot.data.ts));

                for (int i = 0; i < snapshot.data.bids.Count && i < 25; i++)
                {
                    List<string> level = snapshot.data.bids[i];

                    newDepth.Bids.Add(new MarketDepthLevel
                    {
                        Price = level[0].ToDouble(),
                        Bid = level[1].ToDouble()
                    });
                }

                for (int i = 0; i < snapshot.data.asks.Count && i < 25; i++)
                {
                    List<string> level = snapshot.data.asks[i];

                    newDepth.Asks.Add(new MarketDepthLevel
                    {
                        Price = level[0].ToDouble(),
                        Ask = level[1].ToDouble()
                    });
                }

                if (newDepth.Time <= _lastTimeMd)
                {
                    _lastTimeMd = _lastTimeMd.AddTicks(1);
                    newDepth.Time = _lastTimeMd;
                }
                else
                {
                    _lastTimeMd = newDepth.Time;
                }

                _allDepths.RemoveAll(d => d.SecurityNameCode == newDepth.SecurityNameCode);

                _allDepths.Add(newDepth);

                if (newDepth.Bids.Count == 0 || newDepth.Asks.Count == 0)
                {
                    return;
                }

                MarketDepthEvent?.Invoke(newDepth.GetCopy());
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in SnapshotDepth: {exception.ToString()}", LogMessageType.Error);
            }
        }

        private void UpdateDepth(string json)
        {
            try
            {
                AscendexSpotDepthResponse update = JsonConvert.DeserializeObject<AscendexSpotDepthResponse>(json);

                MarketDepth depth = _allDepths.Find(d => d.SecurityNameCode == update.symbol);

                if (depth == null)
                {
                    return;
                }

                if (update?.data == null || update.symbol != depth.SecurityNameCode)
                {
                    return;
                }

                if (!_snapshotInitialized)
                {
                    return;
                }

                if (_lastSeqNum != -1 && Convert.ToInt64(update.data.seqnum) != _lastSeqNum + 1)
                {
                    _snapshotInitialized = false;
                    _lastSeqNum = -1;

                    RequestSnapshot(depth.SecurityNameCode);
                    return;
                }

                _lastSeqNum = Convert.ToInt64(update.data.seqnum);

                depth.Time = DateTime.UtcNow;

                if (depth.Time <= _lastTimeMd)
                {
                    _lastTimeMd = _lastTimeMd.AddTicks(1);
                    depth.Time = _lastTimeMd;
                }
                else
                {
                    _lastTimeMd = depth.Time;
                }

                _lastTimeMd = depth.Time;

                ApplyLevels(update.data.bids, depth.Bids, isBid: true);
                ApplyLevels(update.data.asks, depth.Asks, isBid: false);

                depth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(update.data.ts));

                List<MarketDepthLevel> topBids = new List<MarketDepthLevel>();

                for (int i = 0; i < depth.Bids.Count && i < 25; i++)
                {
                    topBids.Add(depth.Bids[i]);
                }

                depth.Bids = topBids;

                List<MarketDepthLevel> topAsks = new List<MarketDepthLevel>();

                for (int i = 0; i < depth.Asks.Count && i < 25; i++)
                {
                    topAsks.Add(depth.Asks[i]);
                }

                depth.Asks = topAsks;

                if (depth.Bids.Count == 0 || depth.Asks.Count == 0)
                {
                    return;
                }

                MarketDepthEvent?.Invoke(depth.GetCopy());
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in UpdateDepth: {exception.ToString()}", LogMessageType.Error);
            }
        }

        private void RequestSnapshot(string symbol)
        {
            WebSocket webSocketPublicMarketDepths = _webSocketPublic[_webSocketPublic.Count - 1];

            if (webSocketPublicMarketDepths.ReadyState == WebSocketState.Open)
            {
                webSocketPublicMarketDepths.SendAsync($"{{\"op\":\"req\",\"action\":\"depth-snapshot\",\"args\":{{\"symbol\":\"{symbol}\"}}}}");
            }
        }

        private void ApplyLevels(List<List<string>> updates, List<MarketDepthLevel> levels, bool isBid)
        {
            for (int i = 0; i < updates.Count; i++)
            {
                double price = updates[i][0].ToDouble();
                double size = updates[i][1].ToDouble();

                MarketDepthLevel existing = levels.Find(x => x.Price == price);

                if (size == 0)
                {
                    if (existing != null)
                    {
                        levels.Remove(existing);
                    }
                }
                else
                {
                    if (existing != null)
                    {
                        if (isBid)
                        {
                            existing.Bid = size;
                        }
                        else
                        {
                            existing.Ask = size;
                        }
                    }
                    else
                    {
                        MarketDepthLevel level = new MarketDepthLevel { Price = price };

                        if (isBid)
                        {
                            level.Bid = size;
                        }
                        else
                        {
                            level.Ask = size;
                        }

                        levels.Add(level);
                    }
                }
            }

            if (isBid)
            {
                levels.Sort((a, b) => b.Price.CompareTo(a.Price));
            }
            else
            {
                levels.Sort((a, b) => a.Price.CompareTo(b.Price));
            }
        }

        private void UpdateTrade(string message)
        {
            try
            {
                AscendexSpotPublicTradesResponse response = JsonConvert.DeserializeObject<AscendexSpotPublicTradesResponse>(message);

                if (response == null || response.data == null)
                {
                    SendLogMessage("UpdateTrade> Received empty  json", LogMessageType.Error);
                    return;
                }

                for (int i = 0; i < response.data.Count; i++)
                {
                    AscendexSpotPublicTradeItem json = response.data[i];

                    Trade newTrade = new Trade();

                    newTrade.SecurityNameCode = response.symbol;
                    newTrade.Id = json.seqnum;
                    newTrade.Price = json.p.ToDecimal();
                    newTrade.Volume = json.q.ToDecimal();
                    newTrade.Side = (json.bm == "true") ? Side.Sell : Side.Buy; // Ascendex: bm = "true" → buyer is maker → инициатор продажи
                    newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(json.ts));

                    NewTradesEvent?.Invoke(newTrade);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in UpdateTrade: {exception.ToString()}", LogMessageType.Error);
            }
        }

        private void UpdateOrder(string message)
        {
            try
            {
                WebSocketMessage<OrderDataWebsocket> orderMessage = JsonConvert.DeserializeObject<WebSocketMessage<OrderDataWebsocket>>(message);

                if (orderMessage == null
                    || orderMessage.m != "order"
                    || orderMessage.data == null)
                {
                    SendLogMessage("UpdateOrder> Received empty json", LogMessageType.Error);
                    return;
                }

                Order updateOrder = new Order();

                OrderDataWebsocket data = orderMessage.data;

                if (data.ot == "Market"
                    && data.st == "New")
                {
                    return;
                }

                updateOrder.SecurityNameCode = data.s;
                updateOrder.SecurityClassCode = GetNameClass(data.s);
                updateOrder.State = GetOrderState(data.st);
                updateOrder.NumberMarket = data.orderId;
                updateOrder.NumberUser = GetUserOrderNumber(data.orderId);
                updateOrder.Side = data.sd == "Buy" ? Side.Buy : Side.Sell;
                updateOrder.TypeOrder = (data.ot == "Limit") ? OrderPriceType.Limit : OrderPriceType.Market;
                updateOrder.Price = (data.p).ToDecimal();
                updateOrder.Volume = (data.q).ToDecimal();
                updateOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(data.t));
                updateOrder.ServerType = ServerType.AscendexSpot;
                updateOrder.PortfolioNumber = _portfolioName;

                if (orderMessage.data.st == "PartiallyFilled" || orderMessage.data.st == "Filled")
                {
                    UpdateMyTrade(data);
                }

                UpdatePortfolioFromOrder(data);


                SendLogMessage($"Order update: {updateOrder.State}, ID: {updateOrder.NumberMarket}, User: {updateOrder.NumberUser}", LogMessageType.System);

                MyOrderEvent?.Invoke(updateOrder);
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in UpdateOrder: {exception.ToString()}", LogMessageType.Error);
            }
        }

        private void UpdateMyTrade(OrderDataWebsocket data)
        {
            try
            {
                if (string.IsNullOrEmpty(data.q))
                {
                    SendLogMessage("UpdateMyTrade> Trade skipped due to missing data", LogMessageType.Error);
                    return;
                }

                MyTrade myTrade = new MyTrade();

                myTrade.NumberOrderParent = data.orderId;
                myTrade.Side = data.sd == "Buy" ? Side.Buy : Side.Sell;
                myTrade.SecurityNameCode = data.s;
                myTrade.Price = data.ap.ToDecimal();

                string commissionSecName = data.fa;

                if (myTrade.SecurityNameCode.StartsWith(commissionSecName))
                {
                    myTrade.Volume = data.q.ToDecimal() - data.cf.ToDecimal();
                }
                else
                {
                    myTrade.Volume = data.q.ToDecimal();
                }

                myTrade.NumberTrade = data.sn;
                myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(data.t));

                MyTradeEvent?.Invoke(myTrade);
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in UpdateMyTrade: {exception.ToString()}", LogMessageType.Error);
            }
        }

        private void UpdatePortfolioFromOrder(OrderDataWebsocket data)
        {
            try
            {
                if (data == null
                    || string.IsNullOrEmpty(data.s))
                {
                    return;
                }

                Portfolio portfolio = new Portfolio();

                portfolio = new Portfolio();
                portfolio.Number = _portfolioName;
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;
                portfolio.ServerType = ServerType.AscendexSpot;

                string[] parts = data.s.Split('/');
                if (parts.Length == 2)
                {
                    string baseAsset = parts[0];
                    string quoteAsset = parts[1];

                    PositionOnBoard basePos = new PositionOnBoard();
                    basePos.PortfolioName = portfolio.Number;
                    basePos.SecurityNameCode = baseAsset;
                    //basePos.ValueBegin = data.btb.ToDecimal();
                    basePos.ValueCurrent = data.bab.ToDecimal();
                    //basePos.ValueBlocked = basePos.ValueBegin - basePos.ValueCurrent;

                    portfolio.SetNewPosition(basePos);

                    PositionOnBoard quotePos = new PositionOnBoard();
                    quotePos.PortfolioName = portfolio.Number;
                    quotePos.SecurityNameCode = quoteAsset;
                    quotePos.ValueBegin = data.qtb.ToDecimal();
                    quotePos.ValueCurrent = data.qab.ToDecimal();
                    quotePos.ValueBlocked = quotePos.ValueBegin - quotePos.ValueCurrent;

                    portfolio.SetNewPosition(quotePos);
                }

                PortfolioEvent(new List<Portfolio> { portfolio });
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in UpdatePortfolioFromOrder: {exception.ToString()}", LogMessageType.Error);
            }
        }

        private void UpdateBalance(string message)
        {
            try
            {
                WebSocketMessage<BalanceSocket> response = JsonConvert.DeserializeObject<WebSocketMessage<BalanceSocket>>(message);

                if (response == null)
                {
                    return;
                }

                Portfolio portfolio = new Portfolio();

                portfolio = new Portfolio();
                portfolio.Number = _portfolioName;
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;
                portfolio.ServerType = ServerType.AscendexSpot;

                PositionOnBoard position = new PositionOnBoard();

                position.PortfolioName = _portfolioName;
                position.SecurityNameCode = response.data.a;
                position.ValueCurrent = response.data.ab.ToDecimal();

                portfolio.SetNewPosition(position);
                PortfolioEvent(new List<Portfolio> { portfolio });
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in UpdateBalance: {exception.ToString()}", LogMessageType.Error);
            }
        }

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        #endregion

        #region 11 Trade

        private RateGate _rateGateOrder = new RateGate(1, TimeSpan.FromMilliseconds(100));

        private Dictionary<int, string> _orderTrackerDict = new Dictionary<int, string>();

        private Dictionary<string, int> _marketToUserDict = new Dictionary<string, int>();

        private string GetMarketOrderId(int userOrderNumber)
        {
            if (_orderTrackerDict.Count == 0 || _orderTrackerDict.Count == 0)
            {
                LoadOrderTrackers();
            }

            if (_orderTrackerDict.ContainsKey(userOrderNumber))
            {
                return _orderTrackerDict[userOrderNumber];
            }

            return null;
        }

        private int GetUserOrderNumber(string marketOrderId)
        {
            if (_marketToUserDict == null || _marketToUserDict.Count == 0)
            {
                LoadOrderTrackers();
            }

            if (_marketToUserDict.ContainsKey(marketOrderId))
            {
                return _marketToUserDict[marketOrderId];
            }

            return 0;
        }

        private string GetEngineDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            DirectoryInfo dir = new DirectoryInfo(baseDir);
            while (dir != null)
            {
                string enginePath = Path.Combine(dir.FullName, "Engine");
                if (Directory.Exists(enginePath))
                {
                    return enginePath;
                }

                dir = dir.Parent;
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Engine");
        }

        private void LoadOrderTrackers()
        {
            try
            {
                string engineDir = GetEngineDirectory();

                string marketToUserPath = Path.Combine(engineDir, "marketToUserDict.json");
                string orderTrackerPath = Path.Combine(engineDir, "orderTrackerDict.json");

                if (File.Exists(marketToUserPath))
                {
                    string marketToUserJson = File.ReadAllText(marketToUserPath);
                    _marketToUserDict = JsonConvert.DeserializeObject<Dictionary<string, int>>(marketToUserJson);
                }

                if (File.Exists(orderTrackerPath))
                {
                    string orderTrackerJson = File.ReadAllText(orderTrackerPath);
                    _orderTrackerDict = JsonConvert.DeserializeObject<Dictionary<int, string>>(orderTrackerJson);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in LoadOrderTrackers: {exception}", LogMessageType.Error);
            }
        }

        private void SaveOrderTrackers()
        {
            try
            {
                if (_orderTrackerDict == null || _marketToUserDict == null)
                {
                    return;
                }

                string engineDir = GetEngineDirectory();

                string orderTrackerJson = JsonConvert.SerializeObject(_orderTrackerDict, Formatting.Indented);
                File.WriteAllText(Path.Combine(engineDir, "orderTrackerDict.json"), orderTrackerJson);

                string marketToUserJson = JsonConvert.SerializeObject(_marketToUserDict, Formatting.Indented);
                File.WriteAllText(Path.Combine(engineDir, "marketToUserDict.json"), marketToUserJson);
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in SaveOrderTrackers: {exception}", LogMessageType.Error);
            }
        }

        public void SendOrder(Order order)
        {
            _rateGateOrder.WaitToProceed();

            try
            {
                long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string secName = order.SecurityNameCode;
                string orderSide = order.Side == Side.Buy ? "Buy" : "Sell";
                string typeOrder = order.TypeOrder == OrderPriceType.Limit ? "Limit" : "Market";
                string numberUser = order.NumberUser.ToString();
                string price = order.Price.ToString(CultureInfo.InvariantCulture);
                string volume = order.Volume.ToString(CultureInfo.InvariantCulture);
                string body;

                if (typeOrder == "Limit")
                {
                    body = $"{{" +
                                  $"\"id\": \"{numberUser}\", " +
                                  $"\"time\": {time}, " +
                                  $"\"symbol\": \"{secName}\", " +
                                  $"\"orderPrice\": \"{price}\", " +
                                  $"\"orderQty\": \"{volume}\", " +
                                  $"\"orderType\": \"{typeOrder}\", " +
                                  $"\"side\": \"{orderSide}\"" +
                                  $"}}";
                }
                else
                {
                    body = $"{{" +
                                  $"\"id\": \"{numberUser}\", " +
                                  $"\"time\": {time}, " +
                                  $"\"symbol\": \"{secName}\", " +
                                  $"\"orderQty\": \"{volume}\", " +
                                  $"\"orderType\": \"{typeOrder}\", " +
                                  $"\"side\": \"{orderSide}\"" +
                                  $"}}";
                }

                string fullPath = $"/{_accountGroup}/api/pro/v1/{_accountCategory}/order";
                string prehashPath = "order";

                IRestResponse response = CreatePrivateQuery(fullPath, prehashPath, body, Method.POST);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    AscendexSpotOrderResponse responseOrder = JsonConvert.DeserializeObject<AscendexSpotOrderResponse>(response.Content);

                    if (responseOrder.code == "0")
                    {
                        order.NumberMarket = responseOrder.data.info.orderId;
                        order.State = OrderStateType.Active;

                        if (order.NumberUser != 0 && order.NumberMarket != "0")
                        {
                            if (!_orderTrackerDict.ContainsKey(order.NumberUser))
                            {
                                _orderTrackerDict.Add(order.NumberUser, order.NumberMarket);
                            }

                            if (!_marketToUserDict.ContainsKey(order.NumberMarket))
                            {
                                _marketToUserDict.Add(order.NumberMarket, order.NumberUser);
                            }
                        }

                        SaveOrderTrackers();

                        SendLogMessage($"Order sent successfully: Symbol={order.SecurityNameCode}, UserNum={order.NumberUser}, MarketNum={order.NumberMarket}", LogMessageType.Trade);

                        MyOrderEvent?.Invoke(order);

                    }
                    else
                    {
                        SendLogMessage($"Order Fail. {response.Content}", LogMessageType.Error);
                        CreateOrderFail(order);
                    }
                }
                else
                {
                    SendLogMessage("Order Fail. Status: " + response.StatusCode + "  " + order.SecurityNameCode + ", " + response.Content, LogMessageType.Error);
                    CreateOrderFail(order);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in SendOrder: {exception}", LogMessageType.Error);
            }
        }

        private void CreateOrderFail(Order order)
        {
            order.State = OrderStateType.Fail;

            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        public void CancelAllOrders()
        {
            try
            {
                _rateGateOrder.WaitToProceed();

                string path = $"/{_accountGroup}/api/pro/v1/{_accountCategory}/order/all";
                string prehashPath = "order/all";

                IRestResponse response = CreatePrivateQuery(path, prehashPath, null, Method.DELETE);

                if (response == null)
                {
                    SendLogMessage("CancelAllOrders> Response is null", LogMessageType.Error);
                    return;
                }

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    AscendexSpotCancelOrderResponse cancelResult = JsonConvert.DeserializeObject<AscendexSpotCancelOrderResponse>(response.Content);

                    if (cancelResult != null && cancelResult.code == "0")
                    {
                        SendLogMessage($"All active orders cancelled", LogMessageType.Trade);
                    }
                    else
                    {
                        SendLogMessage($"Error: code={cancelResult.code}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Error Order canceled {response.StatusCode}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in CancelAllOrders: {exception}", LogMessageType.Error);
            }
        }

        public bool CancelOrder(Order order)
        {
            try
            {
                _rateGateOrder.WaitToProceed();

                string path = $"/{_accountGroup}/api/pro/v1/{_accountCategory}/order";
                string prehashPath = "order";

                long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                string secName = order.SecurityNameCode;
                string orderId = order.NumberMarket.ToString();

                string body = $"{{\"orderId\":\"{orderId}\",\"symbol\":\"{secName}\",\"time\":{time}}}";

                IRestResponse response = CreatePrivateQuery(path, prehashPath, body, Method.DELETE);

                if (response == null)
                {
                    SendLogMessage("CancelOrder> Response is null", LogMessageType.Error);
                    return false;
                }

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    AscendexSpotCancelOrderResponse cancelResult = JsonConvert.DeserializeObject<AscendexSpotCancelOrderResponse>(response.Content);

                    if (cancelResult.code == "0")
                    {
                        return true;
                    }
                    else
                    {
                        OrderStateType state = GetOrderStatus(order);

                        if (state == OrderStateType.None)
                        {
                            SendLogMessage($" Cancel error: code={response.StatusCode},message {response.Content} ", LogMessageType.Error);
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    OrderStateType state = GetOrderStatus(order);

                    if (state == OrderStateType.None)
                    {
                        SendLogMessage($" Error Order cancellation: {response.Content}", LogMessageType.Error);
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in CancelOrder: {exception}", LogMessageType.Error);
                return false;
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            try
            {
                _rateGateOrder.WaitToProceed();

                string path = $"/{_accountGroup}/api/pro/v1/{_accountCategory}/order/all";
                string prehashPath = "order/all";
                string body = $"{{\"symbol\":\"{security.Name}\"}}";

                IRestResponse response = CreatePrivateQuery(path, prehashPath, body, Method.DELETE);

                if (response == null)
                {
                    return;
                }

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    AscendexSpotCancelOrderResponse cancelResult = JsonConvert.DeserializeObject<AscendexSpotCancelOrderResponse>(response.Content);

                    if (cancelResult != null && cancelResult.code == "0")
                    {
                        SendLogMessage($" Orders cancelled: {cancelResult.data.info.orderId} |Status: {cancelResult.data.status}", LogMessageType.Trade);
                    }
                    else
                    {
                        SendLogMessage($" CancelOrder error: {response.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($" Error: {response.StatusCode} : {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in  CancelAllOrdersToSecurity: {exception}", LogMessageType.Error);
            }
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
        }

        public List<Order> GetAllOpenOrders()
        {
            try
            {
                _rateGateOrder.WaitToProceed();

                List<Order> orders = new List<Order>();

                string path = $"/{_accountGroup}/api/pro/v1/{_accountCategory}/order/open";
                string prehashPath = "order/open";

                IRestResponse response = CreatePrivateQuery(path, prehashPath, null, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    AscendexSpotOpenOrdersResponse result = JsonConvert.DeserializeObject<AscendexSpotOpenOrdersResponse>(response.Content);

                    if (result != null && result.code == "0")
                    {
                        if (result.data.Count == 0)
                        {
                            return new List<Order>();
                        }

                        for (int i = 0; i < result.data.Count; i++)
                        {
                            AscendexSpotOrderInfo order = result.data[i];

                            Order activeOrder = new Order();

                            activeOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.lastExecTime));
                            activeOrder.ServerType = ServerType.AscendexSpot;
                            activeOrder.SecurityNameCode = order.symbol;
                            activeOrder.SecurityClassCode = GetNameClass(order.symbol);
                            activeOrder.NumberMarket = order.orderId;
                            activeOrder.NumberUser = GetUserOrderNumber(order.orderId);
                            activeOrder.Side = order.side == "Buy" ? Side.Buy : Side.Sell;
                            activeOrder.State = GetOrderState(order.status);
                            activeOrder.TypeOrder = order.orderType == "Limit" ? OrderPriceType.Limit : OrderPriceType.Market;
                            activeOrder.Volume = (order.orderQty).ToDecimal();
                            activeOrder.Price = order.price.ToDecimal();
                            activeOrder.PortfolioNumber = _portfolioName;

                            orders.Add(activeOrder);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Get all open orders. Error: {response.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Get all open orders request error:{response.Content}", LogMessageType.Error);
                }

                return orders;
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in GetAllOpenOrders: {exception}", LogMessageType.Error);
                return new List<Order>();
            }
        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllOpenOrders();

            if (orders == null || orders.Count == 0)
            {
                return;
            }

            for (int i = 0; i < orders.Count; i++)
            {
                MyOrderEvent?.Invoke(orders[i]);
            }
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            try
            {
                if (order == null)
                {
                    SendLogMessage("GetOrderStatus > Order is null", LogMessageType.Error);
                    return OrderStateType.None; 
                }

                if (string.IsNullOrWhiteSpace(order.NumberMarket))
                {
                    order.NumberMarket = GetMarketOrderId(order.NumberUser);

                    if (string.IsNullOrWhiteSpace(order.NumberMarket))
                    {
                        SendLogMessage($"GetOrderStatus > Cannot resolve marketOrderId for userOrder: {order.NumberUser}", LogMessageType.Error);
                        return OrderStateType.None; 
                    }
                }

                Order orderOnMarket = GetOrderStatusById(order.NumberMarket);

                if (orderOnMarket == null || string.IsNullOrWhiteSpace(orderOnMarket.NumberMarket))
                {
                    SendLogMessage($"GetOrderStatus > Order not found: {order.NumberMarket}", LogMessageType.Error);
                    return OrderStateType.None;
                }

                MyOrderEvent?.Invoke(orderOnMarket);
                return orderOnMarket.State;
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in GetOrderStatus: {exception}", LogMessageType.Error);
                return OrderStateType.None;
            }
        }

        public Order GetOrderStatusById(string NumberMarket)
        {
            try
            {
                _rateGateOrder.WaitToProceed();

                Order order = new Order();

                if (NumberMarket == null)
                {
                    SendLogMessage("GetOrderStatus> Order is null or empty", LogMessageType.Error);
                    return new Order();
                }

                string path = $"/{_accountGroup}/api/pro/v1/{_accountCategory}/order/status?orderId={NumberMarket}";
                string prehashPath = "order/status";

                IRestResponse response = CreatePrivateQuery(path, prehashPath, null, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    AscendexQueryOrderResponse responseOrder = JsonConvert.DeserializeObject<AscendexQueryOrderResponse>(response.Content);

                    if (responseOrder.code == "0")
                    {
                        AscendexSpotOrderInfo orderData = responseOrder.data;

                        order.SecurityNameCode = orderData.symbol;
                        order.SecurityClassCode = GetNameClass(orderData.symbol);
                        order.NumberMarket = orderData.orderId;

                        order.NumberUser = GetUserOrderNumber(orderData.orderId);
                        order.Price = orderData.price.ToDecimal();
                        order.PortfolioNumber = _portfolioName;
                        order.Side = orderData.side == "Buy" ? Side.Buy : Side.Sell;
                        order.TypeOrder = orderData.orderType == "Limit" ? OrderPriceType.Limit : OrderPriceType.Market;
                        order.Volume = orderData.orderQty.ToDecimal();
                        order.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(orderData.lastExecTime));
                        order.State = GetOrderState(orderData.status);

                        if (orderData.status == "Filled" || orderData.status == "PartiallyFilled")
                        {
                            MyTrade myTrade = new MyTrade();

                            myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(orderData.lastExecTime));
                            myTrade.SecurityNameCode = orderData.symbol;
                            myTrade.Price = orderData.price.ToDecimal();
                            myTrade.NumberTrade = orderData.seqNum;
                            myTrade.NumberOrderParent = orderData.orderId;
                            myTrade.Volume = orderData.orderQty.ToDecimal();
                            myTrade.Side = orderData.side == "Buy" ? Side.Buy : Side.Sell;

                            MyTradeEvent?.Invoke(myTrade);
                        }

                        return order;
                    }
                    else
                    {
                        SendLogMessage($"Get order status. Error:{response.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage("Get order status request error " + response.StatusCode + "  " + response.Content, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in GetOrderStatusById: {exception}", LogMessageType.Error);

            }

            return null;
        }

        private OrderStateType GetOrderState(string orderStateResponse)
        {
            if (orderStateResponse.StartsWith("New")
                || orderStateResponse.StartsWith("Ack")
                || orderStateResponse.StartsWith("ACCEPT"))
            {
                return OrderStateType.Active;
            }
            else if (orderStateResponse.StartsWith("Filled")
                || orderStateResponse.StartsWith("Done")
                || orderStateResponse.StartsWith("DONE"))
            {
                return OrderStateType.Done;
            }
            else if (orderStateResponse.StartsWith("PartiallyFilled"))
            {
                return OrderStateType.Partial;
            }
            else if (orderStateResponse.StartsWith("Rejected"))
            {
                return OrderStateType.Fail;
            }
            else if (orderStateResponse.StartsWith("Canceled"))
            {
                return OrderStateType.Cancel;
            }

            return OrderStateType.None;
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            return null;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            return null;
        }

        #endregion

        #region 12 Queries

        private IRestResponse CreatePublicQuery(string path, Method method)
        {
            try
            {
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest(path, method);

                IRestResponse response = client.Execute(request);

                return response;
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in CreatePublicQuery: {exception}", LogMessageType.Error);
                return null;
            }
        }

        private IRestResponse CreatePrivateQuery(string fullPath, string prehashPath, object body = null, Method method = Method.GET)
        {
            try
            {
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                string message = $"{timestamp}+{prehashPath}";

                string signature = GenerateSignature(message, _secretKey);

                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest(fullPath, method);

                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("x-auth-key", _publicKey);
                request.AddHeader("x-auth-timestamp", timestamp.ToString());
                request.AddHeader("x-auth-signature", signature);

                if (body != null)
                {
                    request.AddParameter("application/json", body, ParameterType.RequestBody);
                }

                return client.Execute(request);
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in CreatePrivateQuery: {exception}", LogMessageType.Error);
                return null;
            }
        }

        private void GenerateAuthenticate()
        {
            try
            {
                string idGuid = Guid.NewGuid().ToString("N");
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string message = $"{timestamp}+stream";
                string signature = GenerateSignature(message, _secretKey);

                var authMsg = new
                {
                    op = "auth",
                    id = idGuid,
                    t = timestamp,
                    key = _publicKey,
                    sig = signature
                };

                string jsonAuthMsg = JsonConvert.SerializeObject(authMsg);
                _webSocketPrivate.SendAsync(jsonAuthMsg);
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in  GenerateAuthenticate: {exception}", LogMessageType.Error);
            }
        }

        private string GenerateSignature(string message, string secret)
        {
            try
            {
                using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
                {
                    byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                    return Convert.ToBase64String(hash);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in  GenerateAuthenticate: {exception}", LogMessageType.Error);
                return null;
            }
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