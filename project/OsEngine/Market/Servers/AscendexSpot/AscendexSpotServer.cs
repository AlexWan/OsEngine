using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.AscendexSpot.Json;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using Candle = OsEngine.Entity.Candle;
using CloseEventArgs = OsEngine.Entity.WebSocketOsEngine.CloseEventArgs;
using ErrorEventArgs = OsEngine.Entity.WebSocketOsEngine.ErrorEventArgs;
using MessageEventArgs = OsEngine.Entity.WebSocketOsEngine.MessageEventArgs;
using Method = RestSharp.Method;
using Order = OsEngine.Entity.Order;
using Security = OsEngine.Entity.Security;
using Side = OsEngine.Entity.Side;
using Trade = OsEngine.Entity.Trade;
using WebSocket = OsEngine.Entity.WebSocketOsEngine.WebSocket;
using WebSocketState = OsEngine.Entity.WebSocketOsEngine.WebSocketState;





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
            try
            {
                LoadOrderTrackers();

                _myProxy = proxy;

                _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
                _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

                if (string.IsNullOrEmpty(_publicKey) || string.IsNullOrEmpty(_secretKey))
                {
                    SendLogMessage("Error:Invalid public or secret key.", LogMessageType.Error);
                    return;
                }

                _rateGateConnect.WaitToProceed();

                string _apiPath = "/api/pro/v2/assets";

                IRestResponse response = CreatePublicQuery(_apiPath, Method.GET);

                if (string.IsNullOrEmpty(response.Content))
                {
                    return;
                }

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    AscendexSpotSecurityResponse result = JsonConvert.DeserializeObject<AscendexSpotSecurityResponse>(response.Content);

                    if (result != null && result.code == "0")
                    {
                        FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
                        FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

                        CreatePublicWebSocketConnect();
                        CreatePrivateWebSocketConnect();
                        CheckSocketsActivate();

                        _accountGroup = GetAccountGroup();

                        SendLogMessage("Start AscendexSpot Connection", LogMessageType.System);
                    }
                    else
                    {
                        SendLogMessage("Status: Maintenance mode", LogMessageType.System);
                    }
                }
                else
                {
                    SendLogMessage($"No connection to AscendexSpot server. Code:{response.StatusCode}, Error:{response.Content}", LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in Connect: {exception}", LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void CheckSocketsActivate()
        {
            try
            {
                lock (_socketActivateLocker)
                {
                    if (_webSocketPrivate == null ||
                        _webSocketPrivate?.ReadyState != WebSocketState.Open)
                    {
                        Disconnect();
                        return;
                    }

                    if (_subscribedSecurities.Count > 0)
                    {
                        if (_webSocketPublic.Count == 0 ||
                            _webSocketPublic == null)
                        {
                            //Disconnect();
                            return;
                        }

                        WebSocket webSocketPublic = _webSocketPublic[0];

                        if (webSocketPublic == null ||
                            webSocketPublic?.ReadyState != WebSocketState.Open)
                        {
                            Disconnect();
                            return;
                        }
                    }

                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        ServerStatus = ServerConnectStatus.Connect;
                        ConnectEvent();
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in CheckSocketsActivate: {exception}", LogMessageType.Error);
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

            FIFOListWebSocketPublicMessage = null;
            FIFOListWebSocketPrivateMessage = null;

            _portfolios.Clear();
            PortfolioEvent?.Invoke(new List<Portfolio>());
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

        private RateGate _rateGateConnect = new RateGate(1, TimeSpan.FromSeconds(5));

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

        private string _portfolioName = "AscendexSpotPortfolio";

        #endregion

        #region 3 Securities

        private RateGate _rateGateSecurity = new RateGate(1, TimeSpan.FromMilliseconds(2100));

        public void GetSecurities()
        {
            try
            {
                _rateGateSecurity.WaitToProceed();

                string _apiPath = $"api/pro/v1/{_accountCategory}/products";

                IRestResponse response = CreatePublicQuery(_apiPath, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string jsonResponse = response.Content;

                    AscendexSpotSecurityResponse securityList = JsonConvert.DeserializeObject<AscendexSpotSecurityResponse>(jsonResponse);

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
                            string price = securityList.data[i].tickSize;
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
                            newSecurity.NameId = symbol;
                            newSecurity.SecurityType = SecurityType.CurrencyPair;
                            newSecurity.Lot = securityList.data[i].lotSize.ToDecimal();
                            newSecurity.State = SecurityStateType.Activ;
                            newSecurity.PriceStep = securityList.data[i].tickSize.ToDecimal();
                            newSecurity.Decimals = price.DecimalsCount() == 0 ? 1 : price.DecimalsCount();
                            newSecurity.PriceStepCost = newSecurity.PriceStep;
                            newSecurity.DecimalsVolume = Convert.ToInt32(securityList.data[i].qtyScale);
                            newSecurity.MinTradeAmount = securityList.data[i].minQty.ToDecimal();
                            newSecurity.MinTradeAmountType = MinTradeAmountType.Contract;
                            newSecurity.VolumeStep = newSecurity.DecimalsVolume.GetValueByDecimals();

                            securities.Add(newSecurity);
                        }

                        SecurityEvent?.Invoke(securities);
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

        private string GetAccountGroup()
        {
            if (!string.IsNullOrEmpty(_accountGroup))
            {
                return _accountGroup;
            }

            try
            {
                string fullPath = $"/api/pro/v1/info";

                string prehashPath = "info";

                IRestResponse response = CreatePrivateQuery(fullPath, prehashPath, null, Method.GET);

                if (response == null || response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"Failed to get account group. Status: {response?.StatusCode}, Content: {response?.Content}", LogMessageType.Error);

                    return string.Empty;
                }

                AscendexSpotApiKeyInfoResponse result = JsonConvert.DeserializeObject<AscendexSpotApiKeyInfoResponse>(response.Content);

                if (result == null)
                {
                    SendLogMessage("GetAccountGroup: Deserialization returned null", LogMessageType.Error);
                    return string.Empty;
                }

                if (result.code == "0" && result.data != null)
                {
                    _accountGroup = result.data.accountGroup;
                    return _accountGroup;
                }
                else
                {
                    SendLogMessage($"Unable to get account group, message={response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in GetAccountGroup: {exception}", LogMessageType.Error);
            }

            return string.Empty;
        }

        private string GetNameClass(string security)
        {
            if (security.EndsWith("USD")) return "USD";
            if (security.EndsWith("USDT")) return "USDT";
            if (security.EndsWith("BTC")) return "BTC";

            return "CurrencyPair";
        }

        #endregion

        #region 4 Portfolios

        private RateGate _rateGatePortfolio = new RateGate(1, TimeSpan.FromMilliseconds(2000));

        private List<Portfolio> _portfolios = new List<Portfolio>();

        public event Action<List<Portfolio>> PortfolioEvent;

        public void GetPortfolios()
        {
            CreateQueryPortfolio();

            if (_portfolios.Count != 0)
            {
                PortfolioEvent?.Invoke(_portfolios);
            }
        }

        private void CreateQueryPortfolio()
        {
            try
            {
                _rateGatePortfolio.WaitToProceed();

                _portfolios.Clear();

                string fullPath = $"/{_accountGroup}/api/pro/v1/{_accountCategory}/balance";
                string prehashPath = "balance";

                IRestResponse response = CreatePrivateQuery(fullPath, prehashPath, null, Method.GET);

                if (response == null || response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"Portfolio request error. Response is null or bad status. Error:{response.Content}", LogMessageType.Error);
                    return;
                }

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Portfolio portfolio = new Portfolio();

                    portfolio.Number = _portfolioName;
                    portfolio.ValueBegin = 1;
                    portfolio.ValueCurrent = 1;

                    AscendexSpotBalanceResponseWebsocket wallets = JsonConvert.DeserializeObject<AscendexSpotBalanceResponseWebsocket>(response.Content);

                    if (wallets == null || wallets.data == null)
                    {
                        SendLogMessage("CreateQueryPortfolio> Deserialization returned null", LogMessageType.Error);
                        return;
                    }

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

                    _portfolios.Add(portfolio);
                }
                else
                {
                    SendLogMessage($"Portfolio request error. Code:{response.StatusCode}, Error:{response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in CreateQueryPortfolio: {exception}", LogMessageType.Error);
            }
        }

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
                if (allCandles[i].TimeStart < periodStart || allCandles[i].TimeStart > timeEnd)
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

        private RateGate _rateGateCandleHistory = new RateGate(1, TimeSpan.FromMilliseconds(2000));

        private List<Candle> CreateQueryCandles(string symbol, string interval, DateTime endTime, int limit)
        {
            _rateGateCandleHistory.WaitToProceed();

            try
            {
                long endDate = TimeManager.GetTimeStampMilliSecondsToDateTime(endTime);

                string _apiPath = $"/api/pro/v1/barhist?symbol={symbol}&interval={interval}&n={limit}&to={endDate}";

                IRestResponse response = CreatePublicQuery(_apiPath, Method.GET);

                if (response == null || response.StatusCode != HttpStatusCode.OK)
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

                        if (string.IsNullOrWhiteSpace(candleData.o) || string.IsNullOrWhiteSpace(candleData.c) ||
                            string.IsNullOrWhiteSpace(candleData.h) || string.IsNullOrWhiteSpace(candleData.l) ||
                            string.IsNullOrWhiteSpace(candleData.v))
                        {
                            continue;
                        }

                        decimal open = candleData.o.ToDecimal();
                        decimal close = candleData.c.ToDecimal();
                        decimal high = candleData.h.ToDecimal();
                        decimal low = candleData.l.ToDecimal();
                        decimal volume = candleData.v.ToDecimal();

                        if (open == 0 || close == 0 || high == 0 || low == 0 || volume == 0)
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
                SendLogMessage($"Exception in CreateQueryCandles: {exception}", LogMessageType.Error);
            }

            return null;
        }

        #endregion

        #region 6 WebSocket creation

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

                    if (message.Contains("\"m\":\"error\""))
                    {
                        SendLogMessage($"Error  websocketPublic -{message}", LogMessageType.Error);
                        continue;
                    }

                    if (message.Contains("\"m\":\"depth-snapshot\""))
                    {
                        SnapshotDepth(message);
                    }
                    else if (message.Contains("\"m\":\"depth\""))
                    {
                        UpdateDepth(message);
                    }
                    else if (message.Contains("\"m\":\"trades\""))
                    {
                        UpdateTrade(message);
                    }
                }
                catch (Exception exception)
                {
                    Thread.Sleep(5000);
                    SendLogMessage($"Exception in PublicMessageReader: {exception}", LogMessageType.Error);
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

                    if (message.Contains("\"m\":\"error\""))
                    {
                        SendLogMessage($"Error webSocketPrivate {message}", LogMessageType.Error);
                        continue;
                    }

                    if (message.Contains("\"op\":\"auth\""))
                    {
                        if (message.Contains("\"code\":0"))
                        {
                            SendLogMessage("Authorization to private channel successful", LogMessageType.System);
                        }
                        else
                        {
                            SendLogMessage($"Authorization failed: {message}", LogMessageType.Error);
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                            break;
                        }

                        continue;
                    }

                    if (message.Contains("\"m\":\"order\""))
                    {
                        WebSocketMessage<AscendexSpotOrderDataWebsocket> orderMessage =
                        JsonConvert.DeserializeObject<WebSocketMessage<AscendexSpotOrderDataWebsocket>>(message);

                        UpdateOrder(orderMessage);
                    }
                }
                catch (Exception exception)
                {
                    Thread.Sleep(5000);
                    SendLogMessage($"Exception in PrivateMessageReader: {exception}", LogMessageType.Error);
                }
            }
        }

        private ConcurrentQueue<string> FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

        private List<WebSocket> _webSocketPublic = new List<WebSocket>();

        private WebSocket _webSocketPrivate;

        private string _webSocketUrl = "wss://ascendex.com/1/api/pro/v1/stream";

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
                SendLogMessage($"Exception in CreatePublicWebSocketConnect: {exception}", LogMessageType.Error);
            }
        }

        private WebSocket CreateNewPublicSocket()
        {
            try
            {
                WebSocket webSocketPublicNew = new WebSocket(_webSocketUrl);

                if (_myProxy != null)
                {
                    webSocketPublicNew.SetProxy(_myProxy);
                }

                webSocketPublicNew.EmitOnPing = false;
                webSocketPublicNew.OnOpen += WebSocketPublicNew_OnOpen;
                webSocketPublicNew.OnClose += WebSocketPublicNew_OnClose;
                webSocketPublicNew.OnMessage += WebSocketPublicNew_OnMessage;
                webSocketPublicNew.OnError += WebSocketPublicNew_OnError;
                webSocketPublicNew.Connect().Wait();

                return webSocketPublicNew;
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in CreateNewPublicSocket: {exception}", LogMessageType.Error);
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

                _webSocketPrivate = new WebSocket(_webSocketUrl);

                if (_myProxy != null)
                {
                    _webSocketPrivate.SetProxy(_myProxy);
                }

                _webSocketPrivate.EmitOnPing = false;
                _webSocketPrivate.OnOpen += _webSocketPrivate_OnOpen;
                _webSocketPrivate.OnClose += _webSocketPrivate_OnClose;
                _webSocketPrivate.OnMessage += _webSocketPrivate_OnMessage;
                _webSocketPrivate.OnError += _webSocketPrivate_OnError;

                _webSocketPrivate.Connect().Wait();
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in CreatePrivateWebSocketConnect: {exception}", LogMessageType.Error);
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
                            webSocketPublicNew.CloseAsync().Wait();
                        }

                        webSocketPublicNew.Dispose();
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

                    if (_webSocketPrivate.ReadyState == WebSocketState.Open)
                    {
                        _webSocketPrivate.CloseAsync().Wait();
                    }

                    _webSocketPrivate.Dispose();
                    _webSocketPrivate = null;
                    _isPrivateSubscribed = false;
                }
                catch
                {
                    // ignore
                }
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
                    CheckActivationSockets();

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
                Disconnect();

                SendLogMessage($"Public MarketDeptns WebSocket closed by AscendexSpot. Code:{e.Code}", LogMessageType.Error);
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in WebSocketPublicNew_OnClose: {exception}", LogMessageType.Error);
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
                        socket.Send("{\"op\":\"pong\"}");
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

        private void WebSocketPublicNew_OnError(object sender, ErrorEventArgs e)
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
            Thread.Sleep(1000);

            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    GenerateAuthenticate();

                    CheckActivationSockets();
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
                Disconnect();

                SendLogMessage($"Connection Closed by AscendexSpot. {e.Code} {e.Reason}. WebSocket Private Closed Event", LogMessageType.Error);
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in _webSocketPrivate_OnClose: {exception}", LogMessageType.Error);
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
                        socket.Send("{\"op\":\"pong\"}");
                    }

                    return;
                }

                if (e.IsText && e.Data.Contains("\"op\":\"auth\""))
                {
                    if (e.Data.Contains("\"code\":0"))
                    {
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

        private readonly object _socketActivateLocker = new object();

        private void CheckActivationSockets()
        {
            lock (_socketActivateLocker)
            {
                try
                {
                    if (_webSocketPublic.Count == 0)
                    {
                        SendLogMessage("CheckActivation: _webSocketPublic is EMPTY", LogMessageType.System);
                        Disconnect();
                        return;
                    }

                    WebSocket webSocketPublic = _webSocketPublic[0];

                    if (webSocketPublic == null)
                    {
                        SendLogMessage("CheckActivation: webSocketPublic is NULL", LogMessageType.System);
                        Disconnect();
                        return;
                    }

                    if (webSocketPublic.ReadyState != WebSocketState.Open)
                    {
                        SendLogMessage("CheckActivation: webSocketPublic not OPEN: " + webSocketPublic.ReadyState, LogMessageType.System);
                        Disconnect();
                        return;
                    }

                    if (_webSocketPrivate == null)
                    {
                        SendLogMessage("CheckActivation: _webSocketPrivate is NULL", LogMessageType.System);
                        Disconnect();
                        return;
                    }

                    if (_webSocketPrivate.ReadyState != WebSocketState.Open)
                    {
                        SendLogMessage("CheckActivation: _webSocketPrivate not OPEN: " + _webSocketPrivate.ReadyState, LogMessageType.System);
                        Disconnect();
                        return;
                    }

                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        ServerStatus = ServerConnectStatus.Connect;
                        ConnectEvent();
                    }

                    SendLogMessage("All sockets activated.", LogMessageType.System);
                }
                catch (Exception exception)
                {
                    SendLogMessage($"Exception in  CheckActivationSockets: {exception}", LogMessageType.Error);
                }
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
                    Thread.Sleep(20000);

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
                            webSocketPublic.Send("{\"op\":\"ping\"}");
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
                        _webSocketPrivate.Send("{\"op\":\"ping\"}");
                    }
                    else
                    {
                        Disconnect();
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage($"Exception in  CheckAliveWebSocket: {exception}", LogMessageType.Error);
                }
            }
        }

        #endregion

        #region 9  WebSocket security subscribe

        private RateGate _rateGateSubscribed = new RateGate(1, TimeSpan.FromMilliseconds(790));

        List<string> _subscribedSecurities = new List<string>();

        private bool _isPrivateSubscribed = false;

        public void Subscrible(Security security)
        {
            try
            {
                _rateGateSubscribed.WaitToProceed();

                CreateSubscribeMessageWebSocket(security);

                Thread.Sleep(100);
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in Subscrible: {exception}", LogMessageType.Error);
            }
        }

        private void CreateSubscribeMessageWebSocket(Security security)
        {
            try
            {
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

                if (_webSocketPublic == null || _webSocketPublic.Count == 0)
                {
                    return;
                }

                WebSocket webSocket = _webSocketPublic[0];

                if (webSocket == null || webSocket.ReadyState != WebSocketState.Open)
                {
                    return;
                }

                webSocket.Send($"{{\"op\":\"req\",\"action\":\"depth-snapshot\",\"args\":{{\"symbol\":\"{security.Name}\"}}}}");
                webSocket.Send($"{{\"op\":\"sub\",\"ch\":\"depth:{security.Name}\"}}");
                webSocket.Send($"{{\"op\":\"sub\",\"ch\":\"trades:{security.Name}\"}}");

                if (_webSocketPrivate != null && _webSocketPrivate.ReadyState == WebSocketState.Open && !_isPrivateSubscribed)
                {

                    _webSocketPrivate.Send("{\"op\":\"sub\",\"ch\":\"order:cash\"}");
                    _isPrivateSubscribed = true;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in CreateSubscribeMessageWebSocket: {exception}", LogMessageType.Error);
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

                                    webSocketPublic.Send($"{{\"op\":\"unsub\",\"ch\":\"depth:{symbol}\"}}");
                                    webSocketPublic.Send($"{{\"op\":\"unsub\",\"ch\":\"trades:{symbol}\"}}");
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
                        _webSocketPrivate.Send("{\"op\":\"unsub\",\"ch\":\"order:cash\"}");
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

        #endregion

        #region 10 WebSocket parsing the messages

        public event Action<List<Security>> SecurityEvent;

        public event Action<News> NewsEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;

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
                    var level = snapshot.data.bids[i];

                    newDepth.Bids.Add(new MarketDepthLevel
                    {
                        Price = level[0].ToDecimal(),
                        Bid = level[1].ToDecimal()
                    });
                }

                for (int i = 0; i < snapshot.data.asks.Count && i < 25; i++)
                {
                    var level = snapshot.data.asks[i];

                    newDepth.Asks.Add(new MarketDepthLevel
                    {
                        Price = level[0].ToDecimal(),
                        Ask = level[1].ToDecimal()
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
                SendLogMessage($"Exception in SnapshotDepth: {exception}", LogMessageType.Error);
            }
        }

        private void UpdateDepth(string json)
        {
            try
            {
                var update = JsonConvert.DeserializeObject<AscendexSpotDepthResponse>(json);

                var depth = _allDepths.Find(d => d.SecurityNameCode == update.symbol);

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
                SendLogMessage($"Exception in UpdateDepth: {exception}", LogMessageType.Error);
            }
        }

        private void RequestSnapshot(string symbol)
        {
            WebSocket webSocketPublicMarketDepths = _webSocketPublic[_webSocketPublic.Count - 1];

            if (webSocketPublicMarketDepths.ReadyState == WebSocketState.Open)
            {
                webSocketPublicMarketDepths.Send($"{{\"op\":\"req\",\"action\":\"depth-snapshot\",\"args\":{{\"symbol\":\"{symbol}\"}}}}");
            }
        }

        private void ApplyLevels(List<List<string>> updates, List<MarketDepthLevel> levels, bool isBid)
        {
            for (int i = 0; i < updates.Count; i++)
            {
                decimal price = updates[i][0].ToDecimal();
                decimal size = updates[i][1].ToDecimal();

                var existing = levels.Find(x => x.Price == price);

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
                        var level = new MarketDepthLevel { Price = price };

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
                SendLogMessage($"Exception in UpdateTrade: {exception}", LogMessageType.Error);
            }
        }

        private void UpdateOrder(WebSocketMessage<AscendexSpotOrderDataWebsocket> json)
        {
            try
            {
                if (json == null || json.m != "order" || json.data == null)
                {
                    SendLogMessage("UpdateOrder> Received empty json", LogMessageType.Error);
                    return;
                }

                Order updateOrder = new Order();

                var data = json.data;

                if (data.orderType == "Market" && data.status == "New")
                {
                    return;
                }

                updateOrder.SecurityNameCode = data.symbol;
                updateOrder.SecurityClassCode = GetNameClass(data.symbol);
                updateOrder.State = GetOrderState(data.status);
                updateOrder.NumberMarket = data.orderId;
                updateOrder.NumberUser = GetUserOrderNumber(data.orderId);
                updateOrder.Side = data.sd == "Buy" ? Side.Buy : Side.Sell;
                updateOrder.TypeOrder = (data.orderType == "Limit") ? OrderPriceType.Limit : OrderPriceType.Market;
                updateOrder.Price = (data.price).ToDecimal();
                updateOrder.Volume = (data.volume).ToDecimal();
                updateOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(data.t));
                updateOrder.ServerType = ServerType.AscendexSpot;
                updateOrder.PortfolioNumber = _portfolioName;
               
                if (json.data.status == "PartiallyFilled" || json.data.status == "Filled")
                {
                    UpdateMyTrade(data);
                    GetPortfolios();
                }
                else
                {
                    UpdatePortfolioFromOrder(data);
                }

                SendLogMessage($"Order update: {updateOrder.State}, ID: {updateOrder.NumberMarket}, User: {updateOrder.NumberUser}", LogMessageType.System);

                MyOrderEvent?.Invoke(updateOrder);
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in UpdateOrder: {exception}", LogMessageType.Error);
            }
        }

        private void UpdateMyTrade(AscendexSpotOrderDataWebsocket data)
        {
            try
            {
                if (string.IsNullOrEmpty(data.quantity))
                {
                    SendLogMessage("UpdateMyTrade> Trade skipped due to missing data", LogMessageType.Error);
                    return;
                }

                MyTrade myTrade = new MyTrade();

                myTrade.NumberOrderParent = data.orderId;
                myTrade.Side = data.sd == "Buy" ? Side.Buy : Side.Sell;
                myTrade.SecurityNameCode = data.symbol;
                myTrade.Price = data.price.ToDecimal();
                myTrade.Volume = data.quantity.ToDecimal();
                myTrade.NumberTrade = data.sn;
                myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(data.t));

                MyTradeEvent?.Invoke(myTrade);
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in UpdateMyTrade: {exception}", LogMessageType.Error);
            }
        }

        private void UpdatePortfolioFromOrder(AscendexSpotOrderDataWebsocket data)
        {
            try
            {
                if (data == null || string.IsNullOrEmpty(data.symbol))
                {
                    return;
                }

                Portfolio portfolio = _portfolios.Find(p => p.Number == _portfolioName);

                if (portfolio == null)
                {
                    portfolio = new Portfolio();
                    portfolio.Number = _portfolioName;
                    portfolio.ValueBegin = 1;
                    portfolio.ValueCurrent = 1;
                    portfolio.ServerType = ServerType.AscendexSpot;

                    _portfolios.Add(portfolio);
                }

                string[] parts = data.symbol.Split('/');
                if (parts.Length == 2)
                {
                    string baseAsset = parts[0];
                    string quoteAsset = parts[1];

                    PositionOnBoard basePos = new PositionOnBoard();
                    basePos.PortfolioName = portfolio.Number;
                    basePos.SecurityNameCode = baseAsset;
                    basePos.ValueBegin = data.btb.ToDecimal();
                    basePos.ValueCurrent = data.bab.ToDecimal();
                    basePos.ValueBlocked = basePos.ValueBegin - basePos.ValueCurrent;

                    portfolio.SetNewPosition(basePos);

                    PositionOnBoard quotePos = new PositionOnBoard();
                    quotePos.PortfolioName = portfolio.Number;
                    quotePos.SecurityNameCode = quoteAsset;
                    quotePos.ValueBegin = data.qtb.ToDecimal();
                    quotePos.ValueCurrent = data.qab.ToDecimal();
                    quotePos.ValueBlocked = quotePos.ValueBegin - quotePos.ValueCurrent;

                    portfolio.SetNewPosition(quotePos);
                }

                PortfolioEvent?.Invoke(_portfolios);
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in UpdatePortfolioFromOrder: {exception}", LogMessageType.Error);
            }
        }

        #endregion

        #region 11 Trade

        private RateGate _rateGateOrder = new RateGate(1, TimeSpan.FromMilliseconds(1000));

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

        private void LoadOrderTrackers()
        {
            try
            {
                if (File.Exists("marketToUserDict.json"))
                {
                    string json = File.ReadAllText("marketToUserDict.json");
                    _marketToUserDict = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);
                }

                if (File.Exists("orderTrackerDict.json"))
                {
                    string json1 = File.ReadAllText("orderTrackerDict.json");
                    _orderTrackerDict = JsonConvert.DeserializeObject<Dictionary<int, string>>(json1);
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

                string json1 = JsonConvert.SerializeObject(_orderTrackerDict, Formatting.Indented);
                File.WriteAllText("orderTrackerDict.json", json1);

                string json2 = JsonConvert.SerializeObject(_marketToUserDict, Formatting.Indented);
                File.WriteAllText("marketToUserDict.json", json2);
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

                IRestResponse request = CreatePrivateQuery(fullPath, prehashPath, body, Method.POST);

                if (request == null || request.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"SendOrder failed:{request.Content}", LogMessageType.Error);
                    return;
                }

                AscendexSpotOrderResponse response = JsonConvert.DeserializeObject<AscendexSpotOrderResponse>(request.Content);

                if (response == null)
                {
                    SendLogMessage($"SendOrder failed: response is null. Content: {request.Content}", LogMessageType.Error);
                    return;
                }

                if (response.code != "0")
                {
                    SendLogMessage($"SendOrder failed: raw response: {request.Content}", LogMessageType.Error);

                    order.State = OrderStateType.Fail;
                    MyOrderEvent?.Invoke(order);
                    return;
                }

                if (response.code == "0")
                {
                    order.NumberMarket = response.data.info.orderId;
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
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in SendOrder: {exception}", LogMessageType.Error);
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

                        GetPortfolios();
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

                    if (cancelResult != null && cancelResult.code == "0")
                    {
                        Order cancelOrder = new Order();

                        cancelOrder.SecurityNameCode = cancelResult.data.info.symbol;
                        cancelOrder.NumberMarket = cancelResult.data.info.orderId;
                        cancelOrder.NumberUser = GetUserOrderNumber(cancelResult.data.info.orderId);
                        cancelOrder.TimeCancel = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(cancelResult.data.info.timestamp));
                        cancelOrder.State = GetOrderState(cancelResult.data.status);

                        SendLogMessage($"Order cancelled: OrderId = {cancelOrder.NumberMarket}, User = {cancelOrder.NumberUser}", LogMessageType.Trade);

                        return true;
                    }
                    else
                    {
                        SendLogMessage($" Cancel error: code={response.StatusCode},message {response.Content} ", LogMessageType.Error);
                        return false;
                    }
                }
                else
                {
                    SendLogMessage($" Error Order cancellation: {response.Content}", LogMessageType.Error);
                    return false;
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

                if (response == null)
                {
                    return new List<Order>();
                }

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
                        SendLogMessage($" GetOrderStatus Error: {response.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($" HTTP Error:{response.Content}", LogMessageType.Error);
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
        OrderStateType IServerRealization.GetOrderStatus(Order order)
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
                    SendLogMessage($"GetOrderStatus > Order not found on exchange: {order.NumberMarket}", LogMessageType.Error);
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

        //public void GetOrderStatus(Order order)
        //{
        //    try
        //    {
        //        if (order == null)
        //        {
        //            SendLogMessage("GetOrderStatus > Order is null", LogMessageType.Error);
        //            return;
        //        }

        //        if (string.IsNullOrWhiteSpace(order.NumberMarket))
        //        {
        //            order.NumberMarket = GetMarketOrderId(order.NumberUser);

        //            if (string.IsNullOrWhiteSpace(order.NumberMarket))
        //            {
        //                SendLogMessage($"GetOrderStatus > Cannot resolve marketOrderId for userOrder: {order.NumberUser}", LogMessageType.Error);
        //                return;
        //            }
        //        }

        //        Order orderOnMarket = GetOrderStatusById(order.NumberMarket);

        //        if (orderOnMarket == null || string.IsNullOrWhiteSpace(orderOnMarket.NumberMarket))
        //        {
        //            SendLogMessage($"GetOrderStatus > Order not found: {order.NumberMarket}", LogMessageType.Error);
        //            return;
        //        }

        //        MyOrderEvent?.Invoke(orderOnMarket);
        //    }
        //    catch (Exception exception)
        //    {
        //        SendLogMessage($"Exception in GetOrderStatus: {exception}", LogMessageType.Error);
        //    }
        //}

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

                IRestResponse request = CreatePrivateQuery(path, prehashPath, null, Method.GET);

                if (request == null)
                {
                    SendLogMessage("GetOrderStatus> Request returned null", LogMessageType.Error);
                    return new Order();
                }

                if (request.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"GetOrderStatusById > HTTP error: {request.Content}", LogMessageType.Error);
                    return null;
                }

                AscendexQueryOrderResponse response =
                 JsonConvert.DeserializeObject<AscendexQueryOrderResponse>(request.Content);

                if (response == null)
                {
                    SendLogMessage($"Error status order: {response.code}, message: {request.Content}", LogMessageType.Error);
                    return new Order();
                }

                if (response.code == "0" && response.data != null)
                {
                    SendLogMessage("Raw JSON response: " + request.Content, LogMessageType.Error);

                    AscendexSpotOrderInfo orderData = response.data;

                    if (orderData != null)
                    {
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
                        SendLogMessage($"byid{order.SecurityNameCode}{order.SecurityClassCode}{order.NumberMarket}{order.State}{order.NumberUser}", LogMessageType.Error);
                    }
                    else
                    {
                        order.State = GetOrderState(orderData.status);
                        SendLogMessage($"HTTP Error:{request.Content}", LogMessageType.Error);
                    }
                }
                MyOrderEvent?.Invoke(order);
                return order;
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in GetOrderStatusById: {exception}", LogMessageType.Error);
                return new Order();
            }
        }

        private OrderStateType GetOrderState(string orderStateResponse)
        {
            if (orderStateResponse.StartsWith("New") || orderStateResponse.StartsWith("Ack") || orderStateResponse.StartsWith("ACCEPT"))
            {
                return OrderStateType.Active;
            }
            else if (orderStateResponse.StartsWith("Filled") || orderStateResponse.StartsWith("Done") || orderStateResponse.StartsWith("DONE"))
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

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<Funding> FundingUpdateEvent;

        public event Action<SecurityVolumes> Volume24hUpdateEvent;

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
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string payload = timestamp + "+stream";
                string signature = GenerateSignature(payload, _secretKey);
                string idGuid = Guid.NewGuid().ToString();

                var auth = new
                {
                    op = "auth",
                    id = "auth-req" + idGuid,
                    t = timestamp,
                    key = _publicKey,
                    sig = signature
                };

                string authJson = JsonConvert.SerializeObject(auth);
                _webSocketPrivate.Send(authJson);

                SendLogMessage("Auth sent: " + authJson, LogMessageType.System);
            }
            catch (Exception exception)
            {
                SendLogMessage($"Exception in  GenerateAuthenticate: {exception}", LogMessageType.Error);
            }
        }

        private static string GenerateSignature(string message, string secret)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(secret);
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);

            using (var hmac = new HMACSHA256(keyBytes))
            {
                byte[] hash = hmac.ComputeHash(messageBytes);
                return Convert.ToBase64String(hash);
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