/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Entity.WebSocketOsEngine;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.BingX.BingXSpot.Entity;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace OsEngine.Market.Servers.BinGxSpot
{
    public class BingXServerSpot : AServer
    {
        public BingXServerSpot(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            BingXServerSpotRealization realization = new BingXServerSpotRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterBoolean("Extended Data", false);

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
            ServerParameters[2].Comment = OsLocalization.Market.Label269;
        }
    }

    public class BingXServerSpotRealization : IServerRealization
    {

        #region 1 Constructor, Status, Connection

        public BingXServerSpotRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread keepalive = new Thread(RequestListenKey);
            keepalive.CurrentCulture = new CultureInfo("ru-RU");
            keepalive.IsBackground = true;
            keepalive.Start();

            Thread thread = new Thread(GetUpdatePortfolio);
            thread.IsBackground = true;
            thread.Name = "ThreadBingXSpotPortfolios";
            thread.Start();

            Thread threadReader = new Thread(MessageReaderPrivate);
            threadReader.IsBackground = true;
            threadReader.Name = "MessageReaderBingXSpot";
            threadReader.Start();

            Thread messageReaderPublic = new Thread(MessageReaderPublic);
            messageReaderPublic.IsBackground = true;
            messageReaderPublic.Name = "MessageReaderBingXSpot";
            messageReaderPublic.Start();
        }

        public DateTime ServerTime { get; set; }

        private WebProxy _myProxy;

        public void Connect(WebProxy proxy = null)
        {
            try
            {
                _myProxy = proxy;
                _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
                _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

                if (string.IsNullOrEmpty(_publicKey) ||
                string.IsNullOrEmpty(_secretKey))
                {
                    SendLogMessage("Can`t run BingX Spot connector. No keys",
                        LogMessageType.Error);
                    return;
                }

                if (((ServerParameterBool)ServerParameters[2]).Value == true)
                {
                    _extendedMarketData = true;
                }
                else
                {
                    _extendedMarketData = false;
                }

                RestRequest requestRest = new RestRequest("/openApi/swap/v2/server/time", Method.GET);
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse responseMessage = client.Execute(requestRest);

                if (responseMessage.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"The server is not available. No internet", LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
                else
                {
                    try
                    {
                        FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
                        FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();
                        CreatePrivateWebSocketConnect();
                        CheckSocketsActivate();
                    }
                    catch (Exception ex)
                    {
                        SendLogMessage(ex.ToString(), LogMessageType.Error);
                        SendLogMessage("The connection cannot be opened. BingX. Error Request", LogMessageType.Error);
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                SendLogMessage("The connection cannot be opened. BingXFutures. Error Request", LogMessageType.Error);
            }
        }

        public void Dispose()
        {
            try
            {
                UnsubscribeFromAllWebSockets();
                _subscribedSecutiries.Clear();
                DeleteWebSocketConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            FIFOListWebSocketPublicMessage = null;
            FIFOListWebSocketPrivateMessage = null;

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
            get { return ServerType.BingXSpot; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private RateGate _rateGate = new RateGate(3, TimeSpan.FromMilliseconds(700));

        private string _publicKey;

        private string _secretKey;

        private bool _extendedMarketData;

        #endregion

        #region 3 Securities

        List<Security> _securities = new List<Security>();

        public void GetSecurities()
        {
            _rateGate.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest("/openApi/spot/v1/common/symbols", Method.GET);

                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string parameters = $"timestamp={timeStamp}";
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                ResponseSpotBingX<SymbolArray> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseSpotBingX<SymbolArray>());

                List<SymbolData> currencyPairs = new List<SymbolData>();

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    for (int i = 0; i < response.data.symbols.Count; i++)
                    {
                        if (response.data.symbols[i].symbol.Contains("#")) // remove NFT from the list of available
                        {
                            continue;
                        }
                        currencyPairs.Add(response.data.symbols[i]);
                    }
                    UpdateSecurity(currencyPairs);
                }
                else
                {
                    SendLogMessage($"Http State Code: {json.StatusCode}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSecurity(List<SymbolData> currencyPairs)
        {
            for (int i = 0; i < currencyPairs.Count; i++)
            {
                SymbolData current = currencyPairs[i];

                if (current.status == "1")
                {
                    Security security = new Security();
                    security.Lot = 1;
                    security.MinTradeAmount = current.minNotional.ToDecimal();
                    security.Name = current.symbol;
                    security.NameFull = current.symbol;
                    security.NameClass = NameClass(current.symbol);
                    security.NameId = security.Name;
                    security.Exchange = nameof(ServerType.BingXSpot);
                    security.State = SecurityStateType.Activ;
                    security.PriceStep = current.tickSize.ToDecimal();
                    security.PriceStepCost = security.PriceStep;
                    security.SecurityType = SecurityType.CurrencyPair;
                    security.Decimals = current.tickSize.DecimalsCount();
                    security.DecimalsVolume = current.stepSize.DecimalsCount();
                    security.MinTradeAmountType = MinTradeAmountType.C_Currency;
                    security.VolumeStep = current.stepSize.ToDecimal();

                    _securities.Add(security);
                }
            }

            SecurityEvent(_securities);
        }

        private string NameClass(string character)
        {
            string[] parts = character.Split('-');
            string nameClass = parts[1];
            return nameClass;
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        public void GetPortfolios()
        {
            CreateQueryPortfolio();
        }

        private void GetUpdatePortfolio()
        {
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
                    CreateQueryPortfolio();
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                }
            }
        }

        private void CreateQueryPortfolio()
        {
            _rateGate.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest("/openApi/spot/v1/account/balance", Method.GET);

                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string parameters = $"timestamp={timeStamp}";
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                ResponseSpotBingX<BalanceArray> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseSpotBingX<BalanceArray>());

                List<BalanceData> assets = new List<BalanceData>();

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    if (response.code == "0")
                    {
                        for (int i = 0; i < response.data.balances.Count; i++)
                        {
                            assets.Add(response.data.balances[i]);
                        }
                        UpdatePortfolio(assets);
                    }
                    else
                    {
                        ResponseErrorCode responseError = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseErrorCode());
                        SendLogMessage($"Http State Code: {responseError.code} - message: {responseError.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Http State Code: {json.StatusCode}", LogMessageType.Error);

                    if (assets != null && response != null)
                    {
                        SendLogMessage($"Code: {response.code}\n"
                            + $"Message: {response.msg}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdatePortfolio(List<BalanceData> assets)
        {
            try
            {
                Portfolio portfolio = new Portfolio();

                portfolio.Number = "BingXSpot";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;

                if (assets == null || assets.Count == 0)
                {
                    PortfolioEvent(new List<Portfolio> { portfolio });
                    return;
                }

                for (int i = 0; i < assets.Count; i++)
                {
                    PositionOnBoard newPortf = new PositionOnBoard();
                    newPortf.SecurityNameCode = assets[i].asset;
                    newPortf.ValueBegin = assets[i].free.ToDecimal();
                    newPortf.ValueCurrent = assets[i].free.ToDecimal();
                    newPortf.ValueBlocked = assets[i].locked.ToDecimal();
                    newPortf.PortfolioName = "BingXSpot";
                    portfolio.SetNewPosition(newPortf);
                }

                PortfolioEvent(new List<Portfolio> { portfolio });

            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            string tf = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);
            return RequestCandleHistory(security.Name, tf);
        }

        private List<Candle> RequestCandleHistory(string nameSec, string tameFrame, long limit = 500, long fromTimeStamp = 0, long toTimeStamp = 0)
        {
            _rateGate.WaitToProceed();

            try
            {
                string endPoint = "/openApi/spot/v2/market/kline";
                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                string parameters = "";
                if (fromTimeStamp != 0 && toTimeStamp != 0)
                {
                    parameters = $"symbol={nameSec}&interval={tameFrame}&startTime={fromTimeStamp}&endTime={toTimeStamp}&limit={limit}&timestamp={timeStamp}";
                }
                else
                {
                    parameters = $"symbol={nameSec}&interval={tameFrame}&limit={limit}&timestamp={timeStamp}";
                }

                string sign = CalculateHmacSha256(parameters);
                string requestUri = $"{endPoint}?{parameters}&signature={sign}";

                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest(requestUri, Method.GET);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    CandlestickChartData response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new CandlestickChartData());

                    if (response.code == "0")
                    {
                        return ConvertCandles(response.data);
                    }
                    else
                    {
                        ResponseErrorCode responseError = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseErrorCode());
                        SendLogMessage($"Http State Code: {responseError.code} - message: {responseError.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Http State Code: {responseMessage.StatusCode} - {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private List<Candle> ConvertCandles(List<string[]> rawList)
        {
            try
            {
                List<Candle> candles = new List<Candle>();

                for (int i = 0; i < rawList.Count; i++)
                {
                    string[] current = rawList[i];

                    Candle candle = new Candle();

                    candle.State = CandleState.Finished;
                    candle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(current[0]));
                    candle.Volume = current[7].ToDecimal();
                    candle.Close = current[4].ToDecimal();
                    candle.High = current[2].ToDecimal();
                    candle.Low = current[3].ToDecimal();
                    candle.Open = current[1].ToDecimal();

                    candles.Add(candle);
                }
                candles.Reverse();

                return candles;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private string GetInterval(TimeSpan timeFrame)
        {
            if (timeFrame.Minutes != 0)
            {
                return $"{timeFrame.Minutes}m";
            }
            else if (timeFrame.Hours != 0)
            {
                return $"{timeFrame.Hours}h";
            }
            else
            {
                return $"{timeFrame.Days}d";
            }
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
            endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
            actualTime = DateTime.SpecifyKind(actualTime, DateTimeKind.Utc);

            if (!CheckTime(startTime, endTime, actualTime))
            {
                return null;
            }

            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

            if (!CheckTf(tfTotalMinutes))
            {
                return null;
            }

            List<Candle> allCandles = new List<Candle>();

            string interval = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);

            int timeRange = 0;
            if (interval == "1m")
            {
                timeRange = tfTotalMinutes * 10000; // for 1m limit 10000 candles
            }
            else
            {
                timeRange = tfTotalMinutes * 20000; // for other TF 20000
            }

            DateTime maxStartTime = DateTime.UtcNow.AddMinutes(-timeRange);
            DateTime startTimeData = startTime;

            if (maxStartTime > startTime)
            {
                SendLogMessage($"Maximum number of candles for TF exceeded {interval}", LogMessageType.Error);
                return null;
            }

            DateTime partEndTime = startTimeData.AddMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * 720);

            do
            {
                List<Candle> candles = new List<Candle>();

                long from = TimeManager.GetTimeStampMilliSecondsToDateTime(startTimeData);
                long to = TimeManager.GetTimeStampMilliSecondsToDateTime(partEndTime);

                candles = RequestCandleHistory(security.Name, interval, 720, from, to);

                if (candles == null || candles.Count == 0)
                {
                    break;
                }

                Candle last = candles[candles.Count - 1];

                if (last.TimeStart >= endTime)
                {
                    for (int i = 0; i < candles.Count; i++)
                    {
                        if (candles[i].TimeStart <= endTime)
                        {
                            allCandles.Add(candles[i]);
                        }
                    }
                    break;
                }

                allCandles.AddRange(candles);

                startTimeData = partEndTime;
                partEndTime = startTimeData.AddMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * 720);

                if (startTimeData >= DateTime.UtcNow)
                {
                    break;
                }

                if (partEndTime > DateTime.UtcNow)
                {
                    partEndTime = DateTime.UtcNow;
                }
            }
            while (true);

            return allCandles;
        }

        private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (startTime >= endTime ||
                startTime >= DateTime.UtcNow ||
                actualTime > endTime ||
                actualTime > DateTime.UtcNow)
            {
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
                timeFrameMinutes == 240)
            {
                return true;
            }

            return false;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        #endregion

        #region 6 WebSocket creation

        private List<WebSocket> _webSocketPublic = new List<WebSocket>();

        private WebSocket _webSocketPrivate;

        private const string _webSocketUrl = "wss://open-api-ws.bingx.com/market";

        private string _listenKey = "";

        private WebSocket CreateNewPublicSocket()
        {
            try
            {
                _listenKey = CreateListenKey();

                if (_listenKey == null)
                {
                    SendLogMessage("Authorization error. Listen key is note created", LogMessageType.Error);
                    return null;
                }

                string urlStr = $"{_webSocketUrl}?listenKey={_listenKey}";

                WebSocket webSocketPublicNew = new WebSocket(urlStr);

                if (_myProxy != null)
                {
                    webSocketPublicNew.SetProxy(_myProxy);
                }

                webSocketPublicNew.EmitOnPing = true;
                webSocketPublicNew.OnOpen += WebSocketPublicNew_OnOpen;
                webSocketPublicNew.OnClose += WebSocketPublicNew_OnClose;
                webSocketPublicNew.OnMessage += WebSocketPublicNew_OnMessage;
                webSocketPublicNew.OnError += WebSocketPublicNew_OnError;
                webSocketPublicNew.ConnectAsync();

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
            if (_webSocketPrivate != null)
            {
                return;
            }

            _listenKey = CreateListenKey();

            if (_listenKey == null)
            {
                SendLogMessage("Autorization error. Listen key is note created", LogMessageType.Error);
                return;
            }

            string urlStr = $"{_webSocketUrl}?listenKey={_listenKey}";

            _webSocketPrivate = new WebSocket(urlStr);

            if (_myProxy != null)
            {
                _webSocketPrivate.SetProxy(_myProxy);
            }

            _webSocketPrivate.EmitOnPing = true;
            _webSocketPrivate.OnOpen += _webSocketPrivate_OnOpen;
            _webSocketPrivate.OnClose += _webSocketPrivate_OnClose;
            _webSocketPrivate.OnMessage += _webSocketPrivate_OnMessage;
            _webSocketPrivate.OnError += _webSocketPrivate_OnError;
            _webSocketPrivate.ConnectAsync();
        }

        private void DeleteWebSocketConnection()
        {
            if (_webSocketPublic != null)
            {
                try
                {
                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        webSocketPublic.OnOpen -= WebSocketPublicNew_OnOpen;
                        webSocketPublic.OnClose -= WebSocketPublicNew_OnClose;
                        webSocketPublic.OnMessage -= WebSocketPublicNew_OnMessage;
                        webSocketPublic.OnError -= WebSocketPublicNew_OnError;

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

        private string _socketActivateLocker = "socketActivateLocker";

        private void CheckSocketsActivate()
        {
            try
            {
                lock (_socketActivateLocker)
                {
                    if (_webSocketPrivate == null
                       || _webSocketPrivate?.ReadyState != WebSocketState.Open)
                    {
                        Disconnect();
                        return;
                    }

                    if (_subscribedSecutiries.Count > 0)
                    {
                        if (_webSocketPublic.Count == 0
                            || _webSocketPublic == null)
                        {
                            //Disconnect();
                            return;
                        }

                        WebSocket webSocketPublic = _webSocketPublic[0];

                        if (webSocketPublic == null
                            || webSocketPublic?.ReadyState != WebSocketState.Open)
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
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        #endregion

        #region 7 WebSocket events

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
            catch (Exception ex)
            {
                SendLogMessage("Data socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublicNew_OnMessage(object sender, MessageEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e == null)
                {
                    return;
                }

                if (e.RawData == null
                    || e.RawData.Length == 0)
                {
                    return;
                }

                if (FIFOListWebSocketPublicMessage == null)
                {
                    return;
                }

                if (e.IsBinary)
                {
                    string item = Decompress(e.RawData);

                    if (item.Contains("ping")) // send immediately upon receipt. 
                    {
                        for (int i = 0; i < _webSocketPublic.Count; i++)
                        {
                            _webSocketPublic[i].SendAsync("pong");
                        }

                        return;
                    }

                    FIFOListWebSocketPublicMessage.Enqueue(item);
                }

                if (e.IsText)
                {
                    FIFOListWebSocketPublicMessage.Enqueue(e.Data);
                }

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
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

        private void WebSocketPublicNew_OnOpen(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("BingXSpot WebSocket Public connection open", LogMessageType.System);
                    CheckSocketsActivate();
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

        private void _webSocketPrivate_OnOpen(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("BingXSpot WebSocket Private connection open", LogMessageType.System);
                    CheckSocketsActivate();
                    _webSocketPrivate.SendAsync($"{{\"id\":\"{GenerateNewId()}\", \"reqType\": \"sub\", \"dataType\": \"spot.executionReport\"}}"); // changing orders
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
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
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    return;
                }

                if (e == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(e.ToString()))
                {
                    return;
                }

                if (FIFOListWebSocketPrivateMessage == null)
                {
                    return;
                }

                if (e.IsBinary)
                {
                    string item = Decompress(e.RawData);

                    if (item.Contains("ping")) // send immediately upon receipt. 
                    {
                        _webSocketPrivate.SendAsync("pong");
                        return;
                    }

                    FIFOListWebSocketPrivateMessage.Enqueue(item);
                }

                if (e.IsText)
                {
                    FIFOListWebSocketPrivateMessage.Enqueue(e.Data);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                SendLogMessage($"WebSocket Private Error message read. Error: {error}", LogMessageType.Error);
            }
        }

        #endregion

        #region 8 Security subscribe

        private List<string> _subscribedSecutiries = new List<string>();

        public void Subscribe(Security security)
        {
            try
            {
                CreateSubscribeSecurityMessageWebSocket(security);
                Thread.Sleep(100);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void CreateSubscribeSecurityMessageWebSocket(Security security)
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            for (int i = 0; i < _subscribedSecutiries.Count; i++)
            {
                if (_subscribedSecutiries[i].Equals(security.Name))
                {
                    return;
                }
            }

            if (_webSocketPublic.Count == 0)
            {
                WebSocket socket = CreateNewPublicSocket();

                if (socket == null)
                {
                    return;
                }

                DateTime timeEnd = DateTime.Now.AddSeconds(10);
                while (socket.ReadyState != WebSocketState.Open)
                {
                    Thread.Sleep(1000);

                    if (timeEnd < DateTime.Now)
                    {
                        break;
                    }
                }

                if (socket.ReadyState == WebSocketState.Open)
                {
                    _webSocketPublic.Add(socket);
                }
            }

            if (_webSocketPublic.Count == 0)
            {
                return;
            }

            _subscribedSecutiries.Add(security.Name);

            WebSocket webSocketPublic = _webSocketPublic[_webSocketPublic.Count - 1];

            if (webSocketPublic.ReadyState == WebSocketState.Open
                && _subscribedSecutiries.Count != 0
                && _subscribedSecutiries.Count % 60 == 0)
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
                webSocketPublic.SendAsync($"{{\"id\": \"{GenerateNewId()}\", \"reqType\": \"sub\", \"dataType\": \"{security.Name}@trade\"}}");
                webSocketPublic.SendAsync($"{{ \"id\":\"{GenerateNewId()}\", \"reqType\": \"sub\", \"dataType\": \"{security.Name}@depth20\" }}");

                if (_extendedMarketData)
                {
                    webSocketPublic.SendAsync($"{{ \"id\":\"{GenerateNewId()}\", \"reqType\": \"sub\", \"dataType\": \"{security.Name}@ticker\" }}");
                }
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
                                if (_subscribedSecutiries != null)
                                {
                                    for (int i2 = 0; i2 < _subscribedSecutiries.Count; i2++)
                                    {
                                        string name = _subscribedSecutiries[i2];

                                        webSocketPublic.SendAsync($"{{\"id\": \"{GenerateNewId()}\", \"reqType\": \"unsub\", \"dataType\": \"{name}@trade\"}}");
                                        webSocketPublic.SendAsync($"{{ \"id\":\"{GenerateNewId()}\", \"reqType\": \"unsub\", \"dataType\": \"{name}@depth20\" }}");

                                        if (_extendedMarketData)
                                        {
                                            webSocketPublic.SendAsync($"{{ \"id\":\"{GenerateNewId()}\", \"reqType\": \"unsub\", \"dataType\": \"{name}@ticker\" }}");
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
                    _webSocketPrivate.SendAsync($"{{\"id\":\"{GenerateNewId()}\", \"reqType\": \"unsub\", \"dataType\": \"spot.executionReport\"}}");
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

        public event Action<News> NewsEvent { add { } remove { } }

        #endregion

        #region 9 WebSocket parsing the messages

        private ConcurrentQueue<string> FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        private void MessageReaderPublic()
        {
            Thread.Sleep(5000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (FIFOListWebSocketPublicMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (FIFOListWebSocketPublicMessage.TryDequeue(out string message))
                    {
                        if (message.Contains("@depth20"))
                        {
                            UpdateDepth(message);
                            continue;
                        }
                        else if (message.Contains("@trade"))
                        {
                            UpdateTrade(message);
                            continue;
                        }
                        else if (message.Contains("@ticker"))
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

        private void MessageReaderPrivate()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (FIFOListWebSocketPrivateMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (FIFOListWebSocketPrivateMessage.TryDequeue(out string message))
                    {
                        if (message.Contains("spot.executionReport"))
                        {
                            UpdateOrder(message);
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

        private void UpdateOrder(string message)
        {
            try
            {
                ResponseWebSocketBingXMessage<SubscriptionOrderUpdateData> responseOrder =
                    JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketBingXMessage<SubscriptionOrderUpdateData>());

                Order newOrder = new Order();

                OrderStateType orderState = GetOrderState(responseOrder.data.X);

                try
                {
                    newOrder.NumberUser = Convert.ToInt32(responseOrder.data.C);
                }
                catch
                {
                    // ignore
                }

                newOrder.NumberMarket = responseOrder.data.i.ToString();
                newOrder.SecurityNameCode = responseOrder.data.s;
                newOrder.SecurityClassCode = responseOrder.data.s.Split('-')[1];
                newOrder.PortfolioNumber = "BingXSpot";
                newOrder.Side = responseOrder.data.S.Equals("BUY") ? Side.Buy : Side.Sell;
                newOrder.Price = responseOrder.data.p.Replace('.', ',').ToDecimal();
                newOrder.Volume = responseOrder.data.q.Replace('.', ',').ToDecimal();
                newOrder.State = orderState;
                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseOrder.data.E));
                newOrder.TypeOrder = responseOrder.data.o.Equals("MARKET") ? OrderPriceType.Market : OrderPriceType.Limit;
                newOrder.ServerType = ServerType.BingXSpot;

                MyOrderEvent(newOrder);

                if (orderState == OrderStateType.Done
                    || orderState == OrderStateType.Partial)
                {
                    UpdateMyTrade(message);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private OrderStateType GetOrderState(string orderStateResponse)
        {
            OrderStateType orderState;

            switch (orderStateResponse)
            {
                case "FILLED":
                    orderState = OrderStateType.Done;
                    break;
                case "PENDING":
                    orderState = OrderStateType.Active;
                    break;
                case "PARTIALLY_FILLED":
                    orderState = OrderStateType.Partial;
                    break;
                case "CANCELED":
                    orderState = OrderStateType.Cancel;
                    break;
                case "FAILED":
                    orderState = OrderStateType.Fail;
                    break;
                case "NEW":
                    orderState = OrderStateType.Active;
                    break;
                default:
                    orderState = OrderStateType.None;
                    break;
            }

            return orderState;
        }

        private void UpdateMyTrade(string message)
        {
            try
            {
                ResponseWebSocketBingXMessage<SubscriptionOrderUpdateData> responseOrder =
                    JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketBingXMessage<SubscriptionOrderUpdateData>());

                MyTrade newTrade = new MyTrade();

                newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseOrder.data.T));
                newTrade.SecurityNameCode = responseOrder.data.s;
                newTrade.NumberOrderParent = responseOrder.data.i;
                newTrade.Price = responseOrder.data.L.ToDecimal();
                newTrade.NumberTrade = responseOrder.data.t;
                newTrade.Side = responseOrder.data.S.Equals("BUY") ? Side.Buy : Side.Sell;

                string commissionSecName = responseOrder.data.N;

                if (newTrade.SecurityNameCode.StartsWith(commissionSecName))
                {
                    newTrade.Volume = responseOrder.data.l.ToDecimal() + responseOrder.data.n.ToDecimal();
                    int decimalVolum = GetDecimalsVolume(responseOrder.data.s);
                    if (decimalVolum > 0)
                    {
                        newTrade.Volume = Math.Floor(newTrade.Volume * (decimal)Math.Pow(10, decimalVolum)) / (decimal)Math.Pow(10, decimalVolum);
                    }
                }
                else
                {
                    newTrade.Volume = responseOrder.data.l.ToDecimal();
                }

                MyTradeEvent(newTrade);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private int GetDecimalsVolume(string security)
        {
            for (int i = 0; i < _securities.Count; i++)
            {
                if (security == _securities[i].Name)
                {
                    return _securities[i].DecimalsVolume;
                }
            }

            return 0;
        }

        private void UpdateTrade(string message)
        {
            try
            {
                ResponseWebSocketBingXMessage<ResponseWebSocketTrade> responseTrades =
                    JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketBingXMessage<ResponseWebSocketTrade>());

                Trade trade = new Trade();
                trade.SecurityNameCode = responseTrades.data.s;

                trade.Price = responseTrades.data.p.Replace('.', ',').ToDecimal();
                trade.Id = responseTrades.data.t;
                trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseTrades.data.T));
                trade.Volume = responseTrades.data.q.Replace('.', ',').ToDecimal();
                if (responseTrades.data.m == "true")
                    trade.Side = Side.Sell;
                else trade.Side = Side.Buy;

                NewTradesEvent(trade);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private readonly Dictionary<string, MarketDepth> _allDepths = new Dictionary<string, MarketDepth>();

        private void UpdateDepth(string message)
        {
            try
            {
                ResponseWebSocketBingXMessage<MarketDepthDataMessage> responseDepths =
                    JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketBingXMessage<MarketDepthDataMessage>());
                MarketDepth depth = new MarketDepth();

                List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                depth.SecurityNameCode = responseDepths.dataType.Split('@')[0];

                for (int i = 0; i < responseDepths.data.asks.Count; i++)
                {
                    MarketDepthLevel level = new MarketDepthLevel()
                    {
                        Price = responseDepths.data.asks[i][0].ToDouble(),
                        Ask = responseDepths.data.asks[i][1].ToDouble()
                    };

                    ascs.Insert(0, level);
                }

                for (int i = 0; i < responseDepths.data.bids.Count; i++)
                {
                    bids.Add(new MarketDepthLevel()
                    {
                        Price = responseDepths.data.bids[i][0].ToDouble(),
                        Bid = responseDepths.data.bids[i][1].ToDouble()
                    });
                }

                depth.Asks = ascs;
                depth.Bids = bids;

                depth.Time = DateTime.UtcNow;

                if (depth.Time <= _lastMdTime)
                {
                    depth.Time = _lastMdTime.AddTicks(1);
                }

                _lastMdTime = depth.Time;

                _allDepths[depth.SecurityNameCode] = depth;

                MarketDepthEvent(depth);
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        DateTime _lastMdTime;

        private void UpdateTicker(string message)
        {
            try
            {
                ResponseWebSocketBingXMessage<TickerItem> responseTicker =
                    JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketBingXMessage<TickerItem>());

                if (responseTicker.code == "0")
                {
                    SecurityVolumes volume = new SecurityVolumes();

                    volume.SecurityNameCode = responseTicker.data.s;
                    volume.Volume24h = responseTicker.data.v.ToDecimal();
                    volume.Volume24hUSDT = responseTicker.data.q.ToDecimal();
                    volume.TimeUpdate = TimeManager.GetDateTimeFromTimeStamp((long)responseTicker.data.E.ToDecimal());

                    Volume24hUpdateEvent?.Invoke(volume);
                }
                else
                {
                    SendLogMessage($"UpdateTicker> WebSocketPublic Code: {responseTicker.code} - message: {responseTicker.dataType}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent;

        #endregion

        #region 10 Trade

        public void SendOrder(Order order)
        {
            try
            {
                _rateGate.WaitToProceed();

                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest("/openApi/spot/v1/trade/order", Method.POST);

                string secName = order.SecurityNameCode;
                string side = order.Side == Side.Buy ? "BUY" : "SELL";
                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string quantity = order.Volume.ToString().Replace(",", ".");
                string typeOrder = "";
                string parameters = "";
                string price = "";

                if (order.TypeOrder == OrderPriceType.Market)
                {
                    typeOrder = "MARKET";
                    parameters = $"timestamp={timeStamp}&symbol={secName}&side={side}&type={typeOrder}&quantity={quantity}&newClientOrderId={order.NumberUser}";
                }
                else if (order.TypeOrder == OrderPriceType.Limit)
                {
                    typeOrder = "LIMIT";
                    price = order.Price.ToString().Replace(",", ".");
                    parameters = $"timestamp={timeStamp}&symbol={secName}&side={side}&type={typeOrder}&quantity={quantity}&price={price}&newClientOrderId={order.NumberUser}";
                }
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("symbol", secName);
                request.AddParameter("side", side);
                request.AddParameter("type", typeOrder);
                request.AddParameter("quantity", quantity);

                if (typeOrder == "LIMIT")
                    request.AddParameter("price", price);

                request.AddParameter("newClientOrderId", order.NumberUser);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseSpotBingX<ResponseCreateOrder> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseSpotBingX<ResponseCreateOrder>());
                    if (response.code == "0")
                    {
                        order.State = OrderStateType.Active;
                        order.NumberMarket = response.data.orderId;
                    }
                    else
                    {
                        CreateOrderFail(order);
                        ResponseErrorCode responseError = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseErrorCode());

                        SendLogMessage($"Order execution error: code - {responseError.code} | message - {responseError.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    CreateOrderFail(order);
                    SendLogMessage($"Http State Code: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                }

                MyOrderEvent.Invoke(order);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Error);
            }
        }

        public void CancelAllOrders()
        {

        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            try
            {
                _rateGate.WaitToProceed();

                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest("/openApi/spot/v1/trade/cancelOpenOrders", Method.POST);

                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string symbol = security.Name;
                string parameters = $"timestamp={timeStamp}&symbol={symbol}";
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("symbol", symbol);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseSpotBingX<ResponseCreateOrder> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseSpotBingX<ResponseCreateOrder>());
                    if (response.code == "0")
                    {

                    }
                    else
                    {
                        ResponseErrorCode responseError = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseErrorCode());
                        SendLogMessage($"CancelAllOrdersToSecurity> Order cancel error: code - {responseError.code} | message - {responseError.msg}", LogMessageType.Trade);
                    }
                }
                else
                {
                    SendLogMessage($"Http State Code: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Error);
            }
        }

        public bool CancelOrder(Order order)
        {
            try
            {
                _rateGate.WaitToProceed();

                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest("/openApi/spot/v1/trade/cancel", Method.POST);

                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string symbol = order.SecurityNameCode.ToString();
                string orderId = order.NumberMarket.ToString();
                string parameters = $"timestamp={timeStamp}&symbol={symbol}&orderId={orderId}";
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("symbol", symbol);
                request.AddParameter("orderId", orderId);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseSpotBingX<ResponseCreateOrder> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseSpotBingX<ResponseCreateOrder>());
                    if (response.code == "0")
                    {
                        return true;
                    }
                    else
                    {
                        OrderStateType state = GetOrderStatus(order);

                        if (state == OrderStateType.None)
                        {
                            ResponseErrorCode responseError = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseErrorCode());
                            SendLogMessage($"Order cancel error: code - {responseError.code} | message - {responseError.msg}", LogMessageType.Trade);
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
                        SendLogMessage($"Http State Code: {json.StatusCode} - {json.Content}", LogMessageType.Error);
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
                SendLogMessage(exception.Message, LogMessageType.Error);
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

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllOpenOrders();

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

        private List<Order> GetAllOpenOrders()
        {
            _rateGate.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest("/openApi/spot/v1/trade/openOrders", Method.GET);

                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string parameters = $"timestamp={timeStamp}";
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseSpotBingX<OrderArray> responseOrders = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseSpotBingX<OrderArray>());

                    if (responseOrders.code == "0")
                    {
                        List<Order> orders = new List<Order>();

                        for (int i = 0; i < responseOrders.data.orders.Count; i++)
                        {
                            ResponseGetOrder itemOrders = responseOrders.data.orders[i];

                            Order newOrder = new Order();

                            OrderStateType orderState = GetOrderState(itemOrders.status);

                            try
                            {
                                newOrder.NumberUser = Convert.ToInt32(itemOrders.clientOrderID);
                            }
                            catch
                            {
                                // ignore
                            }

                            newOrder.NumberMarket = itemOrders.orderId.ToString();
                            newOrder.SecurityNameCode = itemOrders.symbol;
                            newOrder.SecurityClassCode = itemOrders.symbol.Split('-')[1];
                            newOrder.PortfolioNumber = "BingXSpot";
                            newOrder.Side = itemOrders.side.Equals("BUY") ? Side.Buy : Side.Sell;
                            newOrder.Price = itemOrders.price.Replace('.', ',').ToDecimal();
                            newOrder.Volume = itemOrders.origQty.Replace('.', ',').ToDecimal();
                            newOrder.State = orderState;
                            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(itemOrders.updateTime));
                            newOrder.TypeOrder = itemOrders.type.Equals("MARKET") ? OrderPriceType.Market : OrderPriceType.Limit;
                            newOrder.ServerType = ServerType.BingXSpot;

                            orders.Add(newOrder);
                        }

                        return orders;
                    }
                    else
                    {
                        SendLogMessage($"Get all open orders request error: {responseOrders.code} - {responseOrders.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Get all open orders request error: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                }
                return null;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Error);
                return null;
            }
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            List<Order> orders = GetAllOpenOrders();

            if (orders == null
                || orders.Count == 0)
            {
                GetOrderFromExchange(order.SecurityNameCode, order.NumberUser.ToString());
                return OrderStateType.None;
            }

            Order orderOnMarket = null;

            for (int i = 0; i < orders.Count; i++)
            {
                Order curOder = orders[i];

                if (order.NumberUser != 0
                    && curOder.NumberUser != 0
                    && curOder.NumberUser == order.NumberUser)
                {
                    orderOnMarket = curOder;
                    break;
                }

                if (string.IsNullOrEmpty(order.NumberMarket) == false
                    && order.NumberMarket == curOder.NumberMarket)
                {
                    orderOnMarket = curOder;
                    break;
                }
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

            return orderOnMarket.State;
        }

        private void GetOrderFromExchange(string securityNameCode, string numberUser)
        {
            _rateGate.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest("/openApi/spot/v1/trade/historyOrders", Method.GET);

                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string parameters = $"timestamp={timeStamp}&symbol={securityNameCode}";
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("symbol", securityNameCode);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseSpotBingX<OrderArray> responseOrders = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseSpotBingX<OrderArray>());

                    if (responseOrders.code == "0")
                    {
                        List<Order> orders = new List<Order>();

                        for (int i = 0; i < responseOrders.data.orders.Count; i++)
                        {
                            ResponseGetOrder itemOrders = responseOrders.data.orders[i];

                            if (itemOrders.clientOrderID == numberUser)
                            {
                                Order newOrder = new Order();

                                OrderStateType orderState = GetOrderState(itemOrders.status);

                                try
                                {
                                    newOrder.NumberUser = Convert.ToInt32(itemOrders.clientOrderID);
                                }
                                catch
                                {
                                    // ignore
                                }

                                newOrder.NumberMarket = itemOrders.orderId.ToString();
                                newOrder.SecurityNameCode = itemOrders.symbol;
                                newOrder.SecurityClassCode = itemOrders.symbol.Split('-')[1];
                                newOrder.PortfolioNumber = "BingXSpot";
                                newOrder.Side = itemOrders.side.Equals("BUY") ? Side.Buy : Side.Sell;
                                newOrder.Price = itemOrders.price.Replace('.', ',').ToDecimal();
                                newOrder.Volume = itemOrders.origQty.Replace('.', ',').ToDecimal();
                                newOrder.State = orderState;
                                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(itemOrders.updateTime));
                                newOrder.TypeOrder = itemOrders.type.Equals("MARKET") ? OrderPriceType.Market : OrderPriceType.Limit;
                                newOrder.ServerType = ServerType.BingXSpot;

                                if (newOrder != null
                                   && MyOrderEvent != null)
                                {
                                    MyOrderEvent(newOrder);
                                }

                                if (newOrder.State == OrderStateType.Done ||
                                    newOrder.State == OrderStateType.Partial)
                                {
                                    FindMyTradesToOrder(newOrder);
                                }
                            }
                        }
                    }
                    else
                    {
                        SendLogMessage($"GetOrderFromExchange> Get status Order. Code: {responseOrders.code} - {responseOrders.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"GetOrderFromExchange> Get status Order. Error: {json.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void FindMyTradesToOrder(Order itemOrders)
        {
            try
            {
                _rateGate.WaitToProceed();

                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest("/openApi/spot/v1/trade/myTrades", Method.GET);

                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string parameters = $"timestamp={timeStamp}&symbol={itemOrders.SecurityNameCode}&orderId={itemOrders.NumberMarket}";
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("symbol", itemOrders.SecurityNameCode);
                request.AddParameter("orderId", itemOrders.NumberMarket);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseSpotBingX<TradeArray> responseTrade = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseSpotBingX<TradeArray>());

                    if (responseTrade.code == "0")
                    {
                        for (int i = 0; i < responseTrade.data.fills.Count; i++)
                        {
                            ResponseTrade itemTrades = responseTrade.data.fills[i];

                            MyTrade newTrade = new MyTrade();

                            newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(itemTrades.time));
                            newTrade.SecurityNameCode = itemTrades.symbol;
                            newTrade.NumberOrderParent = itemTrades.orderId;
                            newTrade.Price = itemTrades.price.ToDecimal();
                            newTrade.NumberTrade = itemTrades.id;

                            if (itemTrades.isBuyer == "true")
                            {
                                newTrade.Side = Side.Buy;
                            }
                            else
                            {
                                newTrade.Side = Side.Sell;
                            }

                            string commissionSecName = itemTrades.commissionAsset;

                            if (newTrade.SecurityNameCode.StartsWith(commissionSecName))
                            {
                                newTrade.Volume = itemTrades.qty.ToDecimal() + itemTrades.commission.ToDecimal();
                                int decimalVolum = GetDecimalsVolume(itemTrades.symbol);
                                if (decimalVolum > 0)
                                {
                                    newTrade.Volume = Math.Floor(newTrade.Volume * (decimal)Math.Pow(10, decimalVolum)) / (decimal)Math.Pow(10, decimalVolum);
                                }
                            }
                            else
                            {
                                newTrade.Volume = itemTrades.qty.ToDecimal();
                            }

                            MyTradeEvent(newTrade);
                        }
                    }
                    else
                    {
                        SendLogMessage($"GetOrderFromExchange> Get status Order. Code: {responseTrade.code} - {responseTrade.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"FindMyTradesToOrder> Http State Code: {json.Content}", LogMessageType.Error);
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.Message, LogMessageType.Error);
            }
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

        #region 11 Queries

        private const string _baseUrl = "https://open-api.bingx.com";

        private string CreateListenKey()
        {
            _rateGate.WaitToProceed();

            try
            {
                string endpoint = "/openApi/user/auth/userDataStream";

                RestRequest request = new RestRequest(endpoint, Method.POST);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                string response = client.Execute(request).Content;

                string responseStr = JsonConvert.DeserializeAnonymousType(response, new ListenKeyBingX()).listenKey;

                _timeLastUpdateListenKey = DateTime.Now;

                return responseStr;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private DateTime _timeLastUpdateListenKey = DateTime.MinValue;

        private void RequestListenKey()
        {
            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(5000);
                    continue;
                }

                if (_timeLastUpdateListenKey.AddMinutes(30) > DateTime.Now)
                {
                    Thread.Sleep(10000);
                    continue;
                }

                try
                {
                    if (_listenKey == "")
                    {
                        continue;
                    }

                    _rateGate.WaitToProceed();

                    string endpoint = "/openApi/user/auth/userDataStream";

                    RestClient client = new RestClient(_baseUrl);

                    if (_myProxy != null)
                    {
                        client.Proxy = _myProxy;
                    }

                    RestRequest request = new RestRequest(endpoint, Method.PUT);

                    request.AddQueryParameter("listenKey", _listenKey);

                    IRestResponse response = client.Execute(request);

                    _timeLastUpdateListenKey = DateTime.Now;
                }
                catch (Exception exception)
                {
                    SendLogMessage($"Request Listen Key Error. Error: {exception}", LogMessageType.Error);
                }
            }
        }

        #endregion

        #region 12 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            if (LogMessageEvent != null)
                LogMessageEvent(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

        #region 13 Helpers

        private string Decompress(byte[] data)
        {
            try
            {
                using (var compressedStream = new System.IO.MemoryStream(data))
                {
                    using (var decompressor = new GZipStream(compressedStream, CompressionMode.Decompress))
                    {
                        using (var resultStream = new System.IO.MemoryStream())
                        {
                            decompressor.CopyTo(resultStream);

                            return Encoding.UTF8.GetString(resultStream.ToArray());
                        }
                    }
                }
            }
            catch
            {
                SendLogMessage("Decompress error", LogMessageType.Error);
                return null;
            }
        }

        private string GenerateNewId()
        {
            return Guid.NewGuid().ToString();
        }

        private string CalculateHmacSha256(string parametrs)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(_secretKey);
            byte[] inputBytes = Encoding.UTF8.GetBytes(parametrs);
            using (HMACSHA256 hmac = new HMACSHA256(keyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(inputBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        #endregion
    }
}