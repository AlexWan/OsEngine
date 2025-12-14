/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

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

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
            ServerParameters[2].Comment = OsLocalization.Market.Label271;
            ServerParameters[3].Comment = OsLocalization.Market.Label269;
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

        public void Connect(WebProxy proxy)
        {
            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
            _passphrase = ((ServerParameterPassword)ServerParameters[2]).Value;

            if (string.IsNullOrEmpty(_publicKey) ||
               string.IsNullOrEmpty(_secretKey) ||
               string.IsNullOrEmpty(_passphrase))
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
                IRestResponse responseMessage = new RestClient(_baseUrl).Execute(requestRest);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {

                    _webSocketPublicMessages = new ConcurrentQueue<string>();
                    _webSocketPrivateMessages = new ConcurrentQueue<string>();
                    CreatePublicWebSocketConnect();
                    CreatePrivateWebSocketConnect();
                    // CheckActivationSockets();
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

        public DateTime ServerTime { get; set; }

        public ServerType ServerType
        {
            get { return ServerType.KuCoinSpot; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string _publicKey;

        private string _secretKey;

        private string _passphrase;

        private string _baseUrl = "https://api.kucoin.com";

        private bool _extendedMarketData;

        #endregion

        #region 3 Securities

        private RateGate _rateGateSecurity = new RateGate(1, TimeSpan.FromMilliseconds(60));

        public void GetSecurities()
        {
            _rateGateSecurity.WaitToProceed();

            try
            {
                string requestStr = $"/api/v2/symbols";
                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                IRestResponse responseMessage = new RestClient(_baseUrl).Execute(requestRest);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<List<ResponseSymbol>> symbols = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<List<ResponseSymbol>>());

                    if (symbols.code.Equals("200000") == true)
                    {
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
                    else
                    {
                        SendLogMessage($"Securities error: {symbols.code} || Message: {symbols.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Securities request error: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Securities request error: {ex.Message} {ex.StackTrace}" + ex.ToString(), LogMessageType.Error);
            }
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        public void GetPortfolios()
        {
            CreatePortfolio();
            _portfolioIsStarted = true;
        }

        private bool _portfolioIsStarted = false;

        private RateGate _ratePortfolios = new RateGate(1, TimeSpan.FromMilliseconds(45));

        private void CreatePortfolio()
        {
            _ratePortfolios.WaitToProceed();

            try
            {
                string path = $"/api/v1/accounts";
                string requestStr = $"{path}?type=trade";

                IRestResponse responseMessage = CreatePrivateQuery(requestStr, Method.GET, null);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<List<ResponseAsset>> assets = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<List<ResponseAsset>>());

                    if (assets.code == "200000")
                    {


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
                    else
                    {
                        SendLogMessage($"Portfolio error: {assets.code}\n" + $"Message: {assets.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Portfolio error. Code: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
            DateTime timeEnd = DateTime.UtcNow;
            DateTime timeStart = timeEnd.AddMinutes(-tfTotalMinutes * candleCount);

            return GetCandleDataToSecurity(security, timeFrameBuilder, timeStart, timeEnd, timeStart);
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

        private readonly RateGate _rateGateCandleHistory = new RateGate(1, TimeSpan.FromMilliseconds(50));

        private List<Candle> CreateQueryCandles(string nameSec, string stringInterval, DateTime timeFrom, DateTime timeTo)
        {
            _rateGateCandleHistory.WaitToProceed();

            try
            {
                string requestStr = $"/api/v1/market/candles?symbol={nameSec}&type={stringInterval}&startAt={TimeManager.GetTimeStampSecondsToDateTime(timeFrom)}&endAt={TimeManager.GetTimeStampSecondsToDateTime(timeTo)}";

                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                IRestResponse responseMessage = new RestClient(_baseUrl).Execute(requestRest);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<List<List<string>>> symbols = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<List<List<string>>>());

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
                        SendLogMessage($"CreateQueryCandles> Code: {symbols.code} || Message: {symbols.msg}", LogMessageType.Error);
                        return null;
                    }
                }
                else
                {
                    if (responseMessage.Content.StartsWith("<!DOCTYPE"))
                    {

                    }
                    else
                    {
                        SendLogMessage($"CreateQueryCandles> State Code: {responseMessage.StatusCode} || {responseMessage.Content}", LogMessageType.Error);
                    }

                    return null;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
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

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
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

        private RateGate _rateGateToken = new RateGate(1, TimeSpan.FromMilliseconds(80));

        private WebSocket CreateNewPublicSocket()
        {
            _rateGateToken.WaitToProceed();

            try
            {
                IRestResponse responseMessage = CreatePrivateQuery("/api/v1/bullet-public", Method.POST, String.Empty);

                if (responseMessage.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage("KuCoinSpot public keys are wrong. Message from server: " + responseMessage.Content, LogMessageType.Error);
                    return null;
                }

                ResponsePrivateWebSocketConnection wsResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponsePrivateWebSocketConnection());

                // set dynamic server address ws
                _webSocketPublicUrl = wsResponse.data.instanceServers[0].endpoint + "?token=" + wsResponse.data.token;

                WebSocket webSocketPublicNew = new WebSocket(_webSocketPublicUrl);

                webSocketPublicNew.EmitOnPing = true;
                webSocketPublicNew.OnOpen += _webSocketPublic_OnOpen;
                webSocketPublicNew.OnMessage += _webSocketPublic_OnMessage;
                webSocketPublicNew.OnError += _webSocketPublic_OnError;
                webSocketPublicNew.OnClose += _webSocketPublic_OnClose;
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
            _rateGateToken.WaitToProceed();

            try
            {
                // 1. get websocket address
                IRestResponse responseMessage = CreatePrivateQuery("/api/v1/bullet-private", Method.POST, String.Empty);

                if (responseMessage.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage("KuCoinSpot keys are wrong. Message from server: " + responseMessage.Content, LogMessageType.Error);
                    return;
                }

                ResponsePrivateWebSocketConnection wsResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponsePrivateWebSocketConnection());

                // set dynamic server address ws
                _webSocketPrivateUrl = wsResponse.data.instanceServers[0].endpoint + "?token=" + wsResponse.data.token;
                _webSocketPrivate = new WebSocket(_webSocketPrivateUrl);
                _webSocketPrivate.EmitOnPing = true;
                _webSocketPrivate.OnOpen += _webSocketPrivate_OnOpen;
                _webSocketPrivate.OnMessage += _webSocketPrivate_OnMessage;
                _webSocketPrivate.OnError += _webSocketPrivate_OnError;
                _webSocketPrivate.OnClose += _webSocketPrivate_OnClose;
                _webSocketPrivate.ConnectAsync();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
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

            _webSocketPrivate.SendAsync($"{{\"type\": \"subscribe\", \"privateChannel\": \"true\", \"topic\": \"/spotMarket/tradeOrdersV2\"}}");
            _webSocketPrivate.SendAsync($"{{\"type\": \"subscribe\", \"privateChannel\": \"true\", \"topic\": \"/account/balance\"}}");
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
                        _webSocketPrivate.SendAsync($"{{\"type\": \"ping\"}}");
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
                            webSocketPublic.SendAsync($"{{\"type\": \"ping\"}}");
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

        private RateGate rateGateSubscribed = new RateGate(1, TimeSpan.FromMilliseconds(300));

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
                lock (_socketLocker)
                {
                    webSocketPublic.SendAsync($"{{\"type\": \"subscribe\",\"topic\": \"/market/match:{security.Name}\"}}");
                    webSocketPublic.SendAsync($"{{\"type\": \"subscribe\",\"topic\": \"/spotMarket/level2Depth5:{security.Name}\"}}");

                    if (_extendedMarketData)
                    {
                        webSocketPublic.SendAsync($"{{\"type\": \"subscribe\",\"topic\": \"/market/snapshot:{security.Name}\"}}");
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

                                        webSocketPublic.SendAsync($"{{\"type\": \"unsubscribe\",\"topic\": \"/market/ticker:{securityName}\"}}");
                                        webSocketPublic.SendAsync($"{{\"type\": \"unsubscribe\",\"topic\": \"/spotMarket/level2Depth5:{securityName}\"}}");

                                        if (_extendedMarketData)
                                        {
                                            webSocketPublic.SendAsync($"{{\"type\": \"unsubscribe\",\"topic\": \"/market/snapshot:{securityName}\"}}");
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
                    _webSocketPrivate.SendAsync($"{{\"type\": \"unsubscribe\", \"privateChannel\": \"true\", \"topic\": \"/spotMarket/tradeOrdersV2\"}}");
                    _webSocketPrivate.SendAsync($"{{\"type\": \"unsubscribe\", \"privateChannel\": \"true\", \"topic\": \"/account/balance\"}}");
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
                trade.Id = responseTrade.data.tradeId;
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
                    newMDLevel.Ask = responseDepth.data.asks[i][1].ToDouble();
                    newMDLevel.Price = responseDepth.data.asks[i][0].ToDouble();
                    ascs.Add(newMDLevel);
                }

                for (int i = 0; i < responseDepth.data.bids.Count; i++)
                {
                    MarketDepthLevel newMDLevel = new MarketDepthLevel();
                    newMDLevel.Bid = responseDepth.data.bids[i][1].ToDouble();
                    newMDLevel.Price = responseDepth.data.bids[i][0].ToDouble();

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

                if (stateType == OrderStateType.Partial)
                {
                    MyTrade myTrade = new MyTrade();

                    myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts) / 1000000); //from nanoseconds to ms
                    myTrade.NumberOrderParent = item.orderId;
                    myTrade.NumberTrade = item.tradeId;
                    myTrade.Price = item.matchPrice.ToDecimal();
                    myTrade.SecurityNameCode = item.symbol;
                    myTrade.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;
                    myTrade.Volume = item.matchSize.ToDecimal();

                    MyTradeEvent(myTrade);
                }

                MyOrderEvent(newOrder);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private OrderStateType GetOrderState(string status, string type)
        {
            if (type == "open"
                || type == "received"
                || type == "update")
            {
                return OrderStateType.Active;
            }
            else if (type == "match")
            {
                return OrderStateType.Partial;
            }
            else if (type == "filled")
            {
                return OrderStateType.Done;
            }
            else if (type == "canceled")
            {
                return OrderStateType.Cancel;
            }

            return OrderStateType.None;
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent;

        #endregion

        #region 11 Trade

        private RateGate _rateGateOrder = new RateGate(1, TimeSpan.FromMilliseconds(10));

        private RateGate _rateGateGetOrders = new RateGate(1, TimeSpan.FromMilliseconds(17));

        public void SendOrder(Order order)
        {
            _rateGateOrder.WaitToProceed();

            try
            {
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
                IRestResponse responseMessage = CreatePrivateQueryOrders("/api/v1/hf/orders", Method.POST, jsonRequest);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<ResponsePlaceOrder> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<ResponsePlaceOrder>());

                    if (stateResponse.code.Equals("200000") == true)
                    {
                        //SendLogMessage($"Order num {order.NumberUser} on exchange.", LogMessageType.Trade);
                        //order.State = OrderStateType.Active;
                        //order.NumberMarket = stateResponse.data.orderId;

                        //if (MyOrderEvent != null)
                        //{
                        //    MyOrderEvent(order);
                        //}
                    }
                    else
                    {
                        CreateOrderFail(order);
                        SendLogMessage($"Order Fail: {stateResponse.code} Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    CreateOrderFail(order);
                    SendLogMessage($"Order Fail. Status: {responseMessage.StatusCode} || {responseMessage.Content}", LogMessageType.Error);
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
            _rateGateGetOrders.WaitToProceed();

            try
            {
                CancelAllOrdersRequestData data = new CancelAllOrdersRequestData();
                data.symbol = security.Name;

                string jsonRequest = JsonConvert.SerializeObject(data);

                IRestResponse responseMessage = CreatePrivateQueryOrders("/api/v1/hf/orders", Method.DELETE, jsonRequest);
                ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<object>());

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
                    SendLogMessage($"CancelAllOrdersToSecurity>  Code: {responseMessage.StatusCode}", LogMessageType.Error);

                    if (stateResponse != null && stateResponse.code != null)
                    {
                        SendLogMessage($"CancelAllOrdersToSecurity> Code: {stateResponse.code}\n"
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
            _rateGateOrder.WaitToProceed();

            try
            {
                string path = $"/api/v1/hf/orders/{order.NumberMarket}";
                string requestStr = $"{path}?symbol={order.SecurityNameCode}";

                IRestResponse responseMessage = CreatePrivateQueryOrders(requestStr, Method.DELETE, null);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<object>());

                    if (stateResponse.code.Equals("200000") == true)
                    {
                        return true;
                    }
                    else
                    {
                        OrderStateType state = GetOrderStatus(order);

                        if (state == OrderStateType.None)
                        {
                            SendLogMessage($"Cancel order failed: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
                            return false;
                        }
                        else
                        {
                            if (responseMessage.Content.Contains("The order cannot be canceled"))
                            {
                                // 
                            }
                            else
                            {
                                return true;
                            }
                        }
                    }
                }
                else
                {
                    OrderStateType state = GetOrderStatus(order);

                    if (state == OrderStateType.None)
                    {
                        SendLogMessage("Cancel order failed. Status: " + responseMessage.StatusCode + "  " + order.SecurityNameCode + ", " + responseMessage.Content, LogMessageType.Error);
                        return false;
                    }
                    else
                    {
                        return true;
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
            _rateGateGetOrders.WaitToProceed();

            try
            {
                List<string> activeOrdersSimbolsArray = GetActiveOrderSymbols();

                List<Order> orders = new List<Order>();

                for (int j = 0; j < activeOrdersSimbolsArray.Count; j++)
                {
                    string path = $"/api/v1/hf/orders/active/page";
                    string requestStr = $"{path}?symbol={activeOrdersSimbolsArray[j]}";

                    IRestResponse responseMessage = CreatePrivateQueryOrders(requestStr, Method.GET, null);

                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        ResponseMessageRest<ResponseAllOrders> order = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<ResponseAllOrders>());

                        if (order.code == "200000")
                        {
                            for (int i = 0; i < order.data.items.Count; i++)
                            {
                                if (order.data.items[i].active == "false")
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
                        }
                        else
                        {
                            SendLogMessage($"Get all open orders error. Code: {order.code} || Message: {order.msg}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Get all open orders error: {responseMessage.StatusCode} || Message: {responseMessage.Content}", LogMessageType.Error);
                    }
                }

                return orders;
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }

            return null;
        }

        private List<string> GetActiveOrderSymbols()
        {
            _rateGateGetOrders.WaitToProceed();

            try
            {
                string path = $"/api/v1/hf/orders/active/symbols";

                IRestResponse responseMessage = CreatePrivateQueryOrders(path, Method.GET, null);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<ActiveOrderSymbols> order = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<ActiveOrderSymbols>());

                    if (order.code == "200000")
                    {
                        List<string> activeOrdersSimbols = new List<string>();

                        for (int i = 0; i < order.data.symbols.Count; i++)
                        {
                            activeOrdersSimbols.Add(order.data.symbols[i]);
                        }

                        return activeOrdersSimbols;
                    }
                    else
                    {
                        SendLogMessage($"Get active order symbols error. Code: {order.code} || Message: {order.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Get active order symbols error. {responseMessage.StatusCode} || Message: {responseMessage.Content}", LogMessageType.Error);
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
                GetMyTradesBySecurity(order.SecurityNameCode, order.NumberMarket);
            }

            return orderOnMarket.State;
        }

        private Order GetOrderFromExchange(string securityNameCode, string numberMarket, int numberUser)
        {
            _rateGateGetOrders.WaitToProceed();

            try
            {
                string path = null;

                if (numberMarket != null
                    && numberMarket != "")
                {
                    path = $"/api/v1/hf/orders/{numberMarket}?symbol={securityNameCode}";
                }
                else
                {
                    path = $"/api/v1/hf/orders/client-order/{numberUser}?symbol={securityNameCode}";
                }

                if (path == null)
                {
                    return null;
                }

                IRestResponse responseMessage = CreatePrivateQueryOrders(path, Method.GET, null);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<ResponseOrder> order = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<ResponseOrder>());

                    if (order.code == "200000")
                    {
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

                            newOrder.State = order.data.active == "true" ? OrderStateType.Active : OrderStateType.Done;
                            newOrder.Volume = order.data.size.Replace('.', ',').ToDecimal();
                            newOrder.Price = order.data.price != null ? order.data.price.Replace('.', ',').ToDecimal() : 0;
                            newOrder.PortfolioNumber = "KuCoinSpot";
                        }

                        return newOrder;
                    }
                    else
                    {
                        SendLogMessage($"Get order from exchange error. Code: {order.code} || Message: {order.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Get order from exchange error: {responseMessage.StatusCode} || Message: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
            return null;
        }

        private void GetMyTradesBySecurity(string nameSec, string OrdId)
        {
            _rateGateGetOrders.WaitToProceed();

            try
            {
                string path = $"/api/v1/hf/fills";
                string requestStr = $"{path}?symbol={nameSec}&orderId={OrdId}";

                IRestResponse responseMessage = CreatePrivateQueryOrders(requestStr, Method.GET, null);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<ResponseMyTrades> responseMyTrades = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<ResponseMyTrades>());

                    if (responseMyTrades.code.Equals("200000") == true)
                    {
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
                    else
                    {
                        SendLogMessage($"Get my trades by security error. Code: {responseMyTrades.code} || Message: {responseMyTrades.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Get my trades by security error: {responseMessage.StatusCode} || {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
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

        #region 12 Queries

        private IRestResponse CreatePrivateQueryOrders(string path, Method method, string body)
        {
            try
            {
                string requestPath = path;
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string signature = GenerateSignature(timestamp, method.ToString(), path, body, _secretKey);
                string signaturePartner = GenerateSignaturePartner(timestamp);

                RestRequest requestRest = new RestRequest(path, method);
                requestRest.AddHeader("KC-API-KEY", _publicKey);
                requestRest.AddHeader("KC-API-SIGN", signature);
                requestRest.AddHeader("KC-API-TIMESTAMP", timestamp);
                requestRest.AddHeader("KC-API-PASSPHRASE", SignHMACSHA256(_passphrase, _secretKey));
                requestRest.AddHeader("KC-API-PARTNER", "VANTECHNOLOGIES");
                requestRest.AddHeader("KC-API-PARTNER-SIGN", signaturePartner);
                requestRest.AddHeader("KC-BROKER-NAME", "VANTECHNOLOGIES");
                requestRest.AddHeader("KC-API-PARTNER-VERIFY", "true");
                requestRest.AddHeader("KC-API-KEY-VERSION", "2");

                if (body != null)
                {
                    requestRest.AddParameter("application/json", body, ParameterType.RequestBody);
                }

                IRestResponse response = new RestClient(_baseUrl).Execute(requestRest);

                return response;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        private IRestResponse CreatePrivateQuery(string path, Method method, string body)
        {
            try
            {
                string requestPath = path;
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string signature = GenerateSignature(timestamp, method.ToString(), path, body, _secretKey);

                RestRequest requestRest = new RestRequest(path, method);
                requestRest.AddHeader("KC-API-KEY", _publicKey);
                requestRest.AddHeader("KC-API-SIGN", signature);
                requestRest.AddHeader("KC-API-TIMESTAMP", timestamp);
                requestRest.AddHeader("KC-API-PASSPHRASE", SignHMACSHA256(_passphrase, _secretKey));
                requestRest.AddHeader("KC-API-KEY-VERSION", "2");

                if (body != null)
                {
                    requestRest.AddParameter("application/json", body, ParameterType.RequestBody);
                }

                IRestResponse response = new RestClient(_baseUrl).Execute(requestRest);

                return response;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
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

        private string GenerateSignature(string timestamp, string method, string requestPath, string body, string secretKey)
        {
            method = method.ToUpper();
            body = string.IsNullOrEmpty(body) ? string.Empty : body;
            string preHash = timestamp + method + Uri.UnescapeDataString(requestPath) + body;

            return SignHMACSHA256(preHash, secretKey);
        }

        private string GenerateSignaturePartner(string timestamp)
        {
            string preHash = timestamp + "VANTECHNOLOGIES" + _publicKey;

            return SignHMACSHA256(preHash, "3efbc50d-16ef-45c3-a524-36d7ede4fa1a");
        }

        public void SetLeverage(Security security, decimal leverage) { }

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
