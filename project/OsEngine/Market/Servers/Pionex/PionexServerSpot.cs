/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Entity.WebSocketOsEngine;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.Pionex.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;


namespace OsEngine.Market.Servers.Pionex
{
    public class PionexServerSpot : AServer
    {
        public PionexServerSpot()
        {
            PionexServerRealization realization = new PionexServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
        }
    }

    public class PionexServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public PionexServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread threadForPublicMessages = new Thread(PublicMessageReader);
            threadForPublicMessages.IsBackground = true;
            threadForPublicMessages.Name = "PublicMessageReaderPionex";
            threadForPublicMessages.Start();

            Thread threadForPrivateMessages = new Thread(PrivateMessageReader);
            threadForPrivateMessages.IsBackground = true;
            threadForPrivateMessages.Name = "PrivateMessageReaderPionex";
            threadForPrivateMessages.Start();
        }

        public DateTime ServerTime { get; set; }

        public void Connect(WebProxy proxy)
        {
            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

            if (string.IsNullOrEmpty(_publicKey) ||
                string.IsNullOrEmpty(_secretKey))
            {
                SendLogMessage("Can`t run Bitget Spot connector. No keys",
                    LogMessageType.Error);
                return;
            }

            try
            {
                RestRequest requestRest = new RestRequest(_prefix + "common/symbols?symbol=BTC_USDT", Method.GET);
                RestClient client = new RestClient(_baseUrl);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    CreatePublicWebSocketConnect();
                    CreatePrivateWebSocketConnect();
                }
                else
                {
                    SendLogMessage("Connection cannot be open. Pionex Spot. Error request", LogMessageType.Error);
                    Disconnect();
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                SendLogMessage("Connection can be open. Pionex Spot. Error request", LogMessageType.Error);
                Disconnect();
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
                DeleteWebSocketConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            _subscribedSecurities.Clear();

            FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
            FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

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
            get { return ServerType.PionexSpot; }
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

        private string _baseUrl = "https://api.pionex.com";

        private string _prefix = "/api/v1/";

        private string _pathUrl = string.Empty;

        #endregion

        #region 3 Securities

        RateGate _rateGateGetSec = new RateGate(1, TimeSpan.FromMilliseconds(200));

        private List<Security> _securities;

        public void GetSecurities()
        {
            _rateGateGetSec.WaitToProceed();

            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            try
            {
                RestRequest requestRest = new RestRequest(_prefix + "common/symbols", Method.GET);
                RestClient client = new RestClient(_baseUrl);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<ResponseSymbols> symbols = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseMessageRest<ResponseSymbols>());

                    if (symbols.result.Equals("true") == true)
                    {
                        for (int i = 0; i < symbols.data.symbols.Count; i++)
                        {
                            Symbol item = symbols.data.symbols[i];

                            if (item.enable != "true")
                            {
                                continue;
                            }

                            //bool candlesOnSecurities = CandlesOnSecurities(item.symbol);

                            //if (!candlesOnSecurities)
                            //{
                            //    continue;
                            //}

                            Security newSecurity = new Security();

                            newSecurity.Exchange = ServerType.PionexSpot.ToString();
                            newSecurity.Lot = 1;
                            newSecurity.Name = item.symbol;
                            newSecurity.NameFull = item.symbol;
                            newSecurity.NameClass = item.symbol.Split('_')[1];
                            newSecurity.NameId = item.symbol;
                            newSecurity.SecurityType = SecurityType.CurrencyPair;
                            newSecurity.MinTradeAmount = item.minAmount.ToDecimal();
                            newSecurity.Decimals = Convert.ToInt32(item.quotePrecision);
                            newSecurity.PriceStep = newSecurity.Decimals.GetValueByDecimals();
                            newSecurity.PriceStepCost = newSecurity.PriceStep;
                            newSecurity.DecimalsVolume = Convert.ToInt32(item.basePrecision);
                            newSecurity.State = SecurityStateType.Activ;
                            newSecurity.MinTradeAmountType = MinTradeAmountType.C_Currency;
                            newSecurity.VolumeStep = item.minTradeSize.ToDecimal();

                            _securities.Add(newSecurity);
                        }

                        SecurityEvent(_securities);
                    }
                    else
                    {
                        SendLogMessage($"Securities request error {symbols.result}\n"
                            + $"Message: {symbols.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Securities request error. Code: {response.StatusCode} || {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private RateGate _rateGateCandlesOnSecurities = new RateGate(1, TimeSpan.FromMilliseconds(200));

        private bool CandlesOnSecurities(string name)
        {
            _rateGateCandlesOnSecurities.WaitToProceed();

            try
            {
                DateTime EndTime = DateTime.UtcNow.AddSeconds(-10);
                string endTimeMs = new DateTimeOffset(EndTime).ToUnixTimeMilliseconds().ToString();
                string endPoint = _prefix + "market/klines?symbol=" + name;

                endPoint += "&interval=15M";
                endPoint += "&endTime=" + endTimeMs;
                endPoint += "&limit=5";

                RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                RestClient client = new RestClient(_baseUrl);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<ResponseCandles> responce = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseMessageRest<ResponseCandles>());

                    if (responce.result == "true")
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return false;
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        public void GetPortfolios()
        {
            _rateGate.WaitToProceed();

            try
            {
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                _pathUrl = "account/balances";

                string _signature = GenerateSignature("GET", _pathUrl, timestamp, null, null);

                IRestResponse json = CreatePrivateRequest(_signature, _pathUrl, Method.GET, timestamp, null, null);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<ResponseBalance> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseMessageRest<ResponseBalance>());

                    if (response.result == "true")
                    {
                        Portfolio portfolio = new Portfolio();
                        portfolio.Number = "PionexSpotPortfolio";
                        portfolio.ValueBegin = 1;
                        portfolio.ValueCurrent = 1;

                        for (int i = 0; i < response.data.balances.Count; i++)
                        {
                            PositionOnBoard newPortf = new PositionOnBoard();
                            newPortf.SecurityNameCode = response.data.balances[i].coin;
                            newPortf.ValueBegin = response.data.balances[i].free.ToDecimal();
                            newPortf.ValueCurrent = response.data.balances[i].free.ToDecimal();
                            newPortf.ValueBlocked = response.data.balances[i].frozen.ToDecimal();
                            newPortf.PortfolioName = "PionexSpotPortfolio";
                            portfolio.SetNewPosition(newPortf);
                        }

                        PortfolioEvent(new List<Portfolio> { portfolio });
                    }
                    else
                    {
                        SendLogMessage($"Portfolio error. Code: {response.code} - message: {response.message}", LogMessageType.Error);
                        Disconnect();
                    }
                }
                else
                {
                    SendLogMessage($"Portfolio error. {json.StatusCode}", LogMessageType.Error);
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
            DateTime endTime = DateTime.UtcNow;
            DateTime startTime = endTime.AddMinutes(-tfTotalMinutes * candleCount);

            return GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, startTime);
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            _rateGate.WaitToProceed();

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

            try
            {
                string interval = string.Empty;
                string limit = /*candleCount <= 500 ? candleCount.ToString() :*/ "500";

                switch (timeFrameBuilder.TimeFrame)
                {
                    case TimeFrame.Min1:
                        interval = "1M";
                        break;
                    case TimeFrame.Min5:
                        interval = "5M";
                        break;
                    case TimeFrame.Min15:
                        interval = "15M";
                        break;
                    case TimeFrame.Min30:
                        interval = "30M";
                        break;
                    case TimeFrame.Hour1:
                        interval = "60M";
                        break;
                    case TimeFrame.Hour4:
                        interval = "4H";
                        break;
                    case TimeFrame.Day:
                        interval = "1D";
                        break;
                    default:
                        SendLogMessage("Incorrect timeframe", LogMessageType.Error);
                        return null;
                }

                DateTime EndTime = DateTime.UtcNow.AddSeconds(-10);
                string endTimeMs = new DateTimeOffset(EndTime).ToUnixTimeMilliseconds().ToString();
                string endPoint = _prefix + "market/klines?symbol=" + security.Name;

                endPoint += "&interval=" + interval;
                endPoint += "&endTime=" + endTimeMs;
                endPoint += "&limit=" + limit;

                RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                RestClient client = new RestClient(_baseUrl);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<ResponseCandles> responseCandles = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseMessageRest<ResponseCandles>());

                    if (responseCandles.result == "true")
                    {
                        List<Candle> candles = new List<Candle>();

                        for (int i = responseCandles.data.klines.Count - 1; i >= 0; i--)
                        {
                            Candle newCandle = new Candle();

                            newCandle.Open = responseCandles.data.klines[i].open.ToDecimal();
                            newCandle.Close = responseCandles.data.klines[i].close.ToDecimal();
                            newCandle.High = responseCandles.data.klines[i].high.ToDecimal();
                            newCandle.Low = responseCandles.data.klines[i].low.ToDecimal();
                            newCandle.Volume = responseCandles.data.klines[i].volume.ToDecimal();
                            newCandle.State = CandleState.Finished;
                            newCandle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseCandles.data.klines[i].time));
                            candles.Add(newCandle);
                        }

                        return candles;
                    }
                    else
                    {
                        SendLogMessage($"Candles request error: {responseCandles.code} - message: {responseCandles.message}", LogMessageType.Error);
                        return null;
                    }
                }
                else
                {
                    SendLogMessage($"Candles request error: {response.StatusCode} || {response.StatusCode}", LogMessageType.Error);
                    return null;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return null;
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
            if (timeFrameMinutes == 1
                || timeFrameMinutes == 5
                || timeFrameMinutes == 15
                || timeFrameMinutes == 30
                || timeFrameMinutes == 60
                || timeFrameMinutes == 240
                || timeFrameMinutes == 1440)
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

        private string _webSocketPublicUrl = "wss://ws.pionex.com/wsPub";

        private string _webSocketPrivateUrl = "wss://ws.pionex.com/ws";

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
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private WebSocket CreateNewPublicSocket()
        {
            try
            {
                WebSocket webSocketPublicNew = new WebSocket(_webSocketPublicUrl);

                //if (_myProxy != null)
                //{
                //    webSocketPublicNew.SetProxy(_myProxy);
                //}

                webSocketPublicNew.EmitOnPing = true;
                webSocketPublicNew.OnOpen += WebSocketPublicNew_OnOpen;
                webSocketPublicNew.OnMessage += WebSocketPublicNew_OnMessage;
                webSocketPublicNew.OnError += WebSocketPublicNew_OnError;
                webSocketPublicNew.OnClose += WebSocketPublicNew_OnClose;
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
            try
            {
                if (_webSocketPrivate != null)
                {
                    return;
                }

                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                string preHash = "/ws?key=" + _publicKey + "&timestamp=" + timestamp + "websocket_auth";

                string signature = SHA256HexHashString(_secretKey, preHash);

                string privateURL = $"{_webSocketPrivateUrl}?key={_publicKey}&timestamp={timestamp}&signature={signature}";

                _webSocketPrivate = new WebSocket(privateURL);

                //if (_myProxy != null)
                //{
                //    _webSocketPrivate.SetProxy(_myProxy);
                //}

                _webSocketPrivate.EmitOnPing = true;
                _webSocketPrivate.OnOpen += _webSocketPrivate_OnOpen;
                _webSocketPrivate.OnClose += _webSocketPrivate_OnClose;
                _webSocketPrivate.OnMessage += _webSocketPrivate_OnMessage;
                _webSocketPrivate.OnError += _webSocketPrivate_OnError;
                _webSocketPrivate.ConnectAsync();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
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

        private string _socketActivateLocker = "socketAcvateLocker";

        private void CheckSocketsActivate()
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

        #endregion

        #region 7 WebSocket events

        private void WebSocketPublicNew_OnClose(object arg1, CloseEventArgs e)
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

        private void WebSocketPublicNew_OnError(object arg1, ErrorEventArgs e)
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

        private void WebSocketPublicNew_OnMessage(object arg1, MessageEventArgs e)
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

                if (string.IsNullOrEmpty(e.ToString()))
                {
                    return;
                }

                if (FIFOListWebSocketPublicMessage == null)
                {
                    return;
                }

                if (e.IsBinary)
                {
                    FIFOListWebSocketPublicMessage.Enqueue(Encoding.UTF8.GetString(e.RawData));
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

        private void WebSocketPublicNew_OnOpen(object arg1, EventArgs arg2)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    CheckSocketsActivate();
                    SendLogMessage("PionexSpot WebSocket Public connection open", LogMessageType.System);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnError(object arg1, ErrorEventArgs e)
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
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnMessage(object arg1, MessageEventArgs e)
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
                    FIFOListWebSocketPrivateMessage.Enqueue(Encoding.UTF8.GetString(e.RawData));
                }

                if (e.IsText)
                {
                    FIFOListWebSocketPrivateMessage.Enqueue(e.Data);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnClose(object arg1, CloseEventArgs arg2)
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

        private void _webSocketPrivate_OnOpen(object arg1, EventArgs arg2)
        {
            try
            {
                CheckSocketsActivate();
                SendLogMessage("BitMartSpot WebSocket Private connection open", LogMessageType.System);

                _webSocketPrivate.SendAsync("{\"op\": \"SUBSCRIBE\", \"topic\": \"BALANCE\"}");
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 Security Subscribed

        private List<string> _subscribedSecurities = new List<string>();

        private RateGate _rateGateSubscribed = new RateGate(1, TimeSpan.FromMilliseconds(500));

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
                    && _subscribedSecurities.Count % 50 == 0)
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
                    webSocketPublic.SendAsync($"{{\"op\": \"SUBSCRIBE\", \"topic\": \"TRADE\", \"symbol\": \"{security.Name}\"}}");
                    webSocketPublic.SendAsync($"{{\"op\": \"SUBSCRIBE\",  \"topic\":  \"DEPTH\",  \"symbol\": \"{security.Name}\", \"limit\":  10 }}");
                }

                if (_webSocketPrivate != null)
                {
                    _webSocketPrivate.SendAsync($"{{\"op\": \"SUBSCRIBE\", \"topic\": \"ORDER\", \"symbol\": \"{security.Name}\"}}");
                    _webSocketPrivate.SendAsync($"{{\"op\": \"SUBSCRIBE\",  \"topic\":  \"FILL\",  \"symbol\": \"{security.Name}\"}}");
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
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
                                    for (int j = 0; j < _subscribedSecurities.Count; j++)
                                    {
                                        string securityName = _subscribedSecurities[j];

                                        webSocketPublic.SendAsync($"{{\"op\": \"UNSUBSCRIBED\", \"topic\": \"TRADE\", \"symbol\": {securityName}\"}}");
                                        webSocketPublic.SendAsync($"{{\"op\": \"UNSUBSCRIBED\",  \"topic\":  \"DEPTH\",  \"symbol\": {securityName}\", \"limit\":  10 }}");
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
                    if (_subscribedSecurities != null)
                    {
                        for (int j = 0; j < _subscribedSecurities.Count; j++)
                        {
                            string securityName = _subscribedSecurities[j];

                            _webSocketPrivate.SendAsync($"{{\"op\": \"UNSUBSCRIBE\", \"topic\": \"ORDER\", \"symbol\": \"{securityName}\"}}"); // myorders
                            _webSocketPrivate.SendAsync($"{{\"op\": \"UNSUBSCRIBE\",  \"topic\":  \"FILL\",  \"symbol\": \"{securityName}\"}}"); // mytrades
                        }
                    }

                    _webSocketPrivate.SendAsync("{\"op\": \"UNSUBSCRIBE\", \"topic\": \"BALANCE\"}");
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

                    if (FIFOListWebSocketPublicMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    FIFOListWebSocketPublicMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Contains("PING"))
                    {
                        for (int i = 0; i < _webSocketPublic.Count; i++)
                        {
                            WebSocket webSocketPublic = _webSocketPublic[i];

                            if (webSocketPublic != null
                                && webSocketPublic?.ReadyState == WebSocketState.Open)
                            {
                                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                                string pong = $"{{\"op\": \"PONG\", \"timestamp\": {timeStamp}}}";
                                webSocketPublic.SendAsync(pong);
                            }
                            else
                            {
                                Disconnect();
                            }
                        }

                        continue;
                    }

                    if (message.Contains("CLOSE"))
                    {
                        SendLogMessage("WebSocket Public CLOSE " + message, LogMessageType.Error);
                    }

                    ResponseWebSocketMessage<object> stream = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<object>());

                    if (stream.topic != null && stream.data != null)
                    {
                        if (stream.topic.Equals("DEPTH"))
                        {
                            UpdateDepth(message);
                            continue;
                        }

                        if (stream.topic.Equals("TRADE"))
                        {
                            UpdateTrade(message);
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
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    if (FIFOListWebSocketPrivateMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    FIFOListWebSocketPrivateMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Contains("PING"))
                    {
                        string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                        string pong = $"{{\"op\": \"PONG\", \"timestamp\": {timeStamp}}}";
                        _webSocketPrivate.SendAsync(pong);
                        continue;
                    }

                    ResponseWebSocketMessage<object> stream = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<object>());

                    if (stream.topic != null && stream.data != null)
                    {
                        if (stream.topic.Equals("ORDER"))
                        {
                            UpdateOrder(message);
                            continue;
                        }

                        if (stream.topic.Equals("FILL"))
                        {
                            UpdateMyTrade(message);
                            continue;
                        }

                        if (stream.topic.Equals("BALANCE"))
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
                ResponseWebSocketMessage<List<TradeElements>> responseTrade = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<List<TradeElements>>());

                if (responseTrade == null)
                {
                    return;
                }

                if (responseTrade.data == null)
                {
                    return;
                }

                Trade trade = new Trade();

                trade.SecurityNameCode = responseTrade.data[0].symbol;
                trade.Price = responseTrade.data[0].price.ToDecimal();
                trade.Id = responseTrade.data[0].tradeId;
                trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseTrade.data[0].timestamp));
                trade.Volume = responseTrade.data[0].size.ToDecimal();
                trade.Side = responseTrade.data[0].side.Equals("BUY") ? Side.Buy : Side.Sell;

                NewTradesEvent(trade);
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
                ResponseWebSocketMessage<ResponseWebSocketDepthItem> responseDepth = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<ResponseWebSocketDepthItem>());

                if (responseDepth.data == null)
                {
                    return;
                }

                MarketDepth marketDepth = new MarketDepth();

                List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                marketDepth.SecurityNameCode = responseDepth.symbol;

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
                marketDepth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseDepth.timestamp));

                if (marketDepth.Asks.Count == 0 ||
                    marketDepth.Bids.Count == 0)
                {
                    return;
                }

                MarketDepthEvent(marketDepth);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdatePortfolio(string message)
        {
            try
            {
                ResponseWebSocketMessage<ResponseWSBalance> responce = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<ResponseWSBalance>());

                Portfolio portfolio = new Portfolio();
                portfolio.Number = "PionexSpotPortfolio";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;

                for (int i = 0; i < responce.data.balances.Count; i++)
                {
                    PositionOnBoard newPortf = new PositionOnBoard();
                    newPortf.SecurityNameCode = responce.data.balances[i].coin;
                    newPortf.ValueCurrent = responce.data.balances[i].free.ToDecimal();
                    newPortf.ValueBlocked = responce.data.balances[i].frozen.ToDecimal();
                    newPortf.PortfolioName = "PionexSpotPortfolio";
                    portfolio.SetNewPosition(newPortf);
                }

                PortfolioEvent(new List<Portfolio> { portfolio });
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateMyTrade(string message)
        {
            try
            {
                ResponseWebSocketMessage<MyTrades> responseTrades = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<MyTrades>());

                MyTrades item = responseTrades.data;

                long time = Convert.ToInt64(item.timestamp);

                MyTrade newTrade = new MyTrade();

                newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(time);
                newTrade.SecurityNameCode = item.symbol;
                newTrade.NumberOrderParent = item.orderId;
                newTrade.Price = item.price.ToDecimal();
                newTrade.NumberTrade = item.id;
                newTrade.Side = item.side.Equals("SELL") ? Side.Sell : Side.Buy;

                string commissionSecName = item.feeCoin;

                if (newTrade.SecurityNameCode.StartsWith(commissionSecName))
                {
                    newTrade.Volume = item.size.ToDecimal() - item.fee.ToDecimal();

                    int decimalVolum = GetVolumeDecimals(newTrade.SecurityNameCode);

                    if (decimalVolum > 0)
                    {
                        newTrade.Volume = Math.Floor(newTrade.Volume * (decimal)Math.Pow(10, decimalVolum)) / (decimal)Math.Pow(10, decimalVolum);
                    }
                }
                else
                {
                    newTrade.Volume = item.size.ToDecimal();
                }

                MyTradeEvent(newTrade);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private int GetVolumeDecimals(string security)
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

        private void UpdateOrder(string message)
        {
            try
            {
                ResponseWebSocketMessage<MyOrders> responseOrders = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<MyOrders>());

                if (responseOrders.data == null)
                {
                    return;
                }

                MyOrders item = responseOrders.data;

                OrderStateType stateType = GetOrderState(item.status, item.filledSize);

                if (item.type.Equals("MARKET") && stateType == OrderStateType.Active)
                {
                    return;
                }

                Order newOrder = new Order();

                newOrder.SecurityNameCode = item.symbol;
                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createTime));

                if (string.IsNullOrEmpty(item.clientOrderId))
                {
                    return;
                }

                newOrder.NumberUser = Convert.ToInt32(item.clientOrderId);
                newOrder.NumberMarket = item.orderId;
                newOrder.Side = item.side.Equals("BUY") ? Side.Buy : Side.Sell;
                newOrder.State = stateType;
                newOrder.TypeOrder = item.type.Equals("MARKET") ? OrderPriceType.Market : OrderPriceType.Limit;
                newOrder.Volume = item.status.Equals("OPEN") ? item.size.ToDecimal() : item.filledSize.ToDecimal();
                newOrder.Price = item.price.ToDecimal();
                newOrder.ServerType = ServerType.PionexSpot;
                newOrder.PortfolioNumber = "PionexSpotPortfolio";

                MyOrderEvent(newOrder);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private OrderStateType GetOrderState(string status, string filledSize)
        {
            OrderStateType stateType;

            if (status == "OPEN" && filledSize == "0")
            {
                stateType = OrderStateType.Active;
            }
            else if (status == "OPEN" && filledSize != "0")
            {
                stateType = OrderStateType.Partial;
            }
            else if (status == "CLOSED" && filledSize != "0")
            {
                stateType = OrderStateType.Done;
            }
            else if (status == "CLOSED" && filledSize == "0")
            {
                stateType = OrderStateType.Cancel;
            }
            else
            {
                stateType = OrderStateType.None;
            }

            return stateType;
        }

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        #endregion

        #region 10 Trade

        public void SendOrder(Order order)
        {
            _rateGate.WaitToProceed();

            try
            {
                SendNewOrder data = new SendNewOrder();
                data.clientOrderId = order.NumberUser.ToString();
                data.symbol = order.SecurityNameCode;
                data.side = order.Side.ToString().ToUpper();
                data.type = order.TypeOrder.ToString().ToUpper();
                data.price = order.TypeOrder == OrderPriceType.Market ? null : order.Price.ToString().Replace(",", ".");
                data.size = order.Volume.ToString().Replace(",", ".");
                data.amount = (order.Volume * order.Price).ToString().Replace(",", "."); // для BUY MARKET ORDER указывается размер в USDT не меньше 10
                data.IOC = false;

                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                _pathUrl = "trade/order";

                JsonSerializerSettings dataSerializerSettings = new JsonSerializerSettings();

                dataSerializerSettings.NullValueHandling = NullValueHandling.Ignore;// если LIMIT-ордер, то игнорим параметр amount

                string body = JsonConvert.SerializeObject(data, dataSerializerSettings);

                string _signature = GenerateSignature("POST", _pathUrl, timestamp, body, null);

                IRestResponse json = CreatePrivateRequest(_signature, _pathUrl, Method.POST, timestamp, body, null);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<ResponseCreateOrder> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseMessageRest<ResponseCreateOrder>());
                    if (response.result == "true")
                    {
                        //
                    }
                    else
                    {
                        CreateOrderFail(order);
                        SendLogMessage($"Order Fail. Code: {response.code}\nMessage: {response.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    CreateOrderFail(order);
                    SendLogMessage($"Order Fail. Status:: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
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

        public void CancelAllOrdersToSecurity(Security security)
        {
            _rateGate.WaitToProceed();

            try
            {
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                _pathUrl = "trade/allOrders";

                string body = $"{{\"symbol\":\"{security.Name}\"}}";

                string signature = GenerateSignature("DELETE", _pathUrl, timestamp, body, null);

                IRestResponse json = CreatePrivateRequest(signature, _pathUrl, Method.DELETE, timestamp, body, null);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<object> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseMessageRest<object>());

                    if (response.result == "true")
                    {
                        SendLogMessage($"Orders canceled", LogMessageType.Trade);
                    }
                    else
                    {
                        SendLogMessage($"Orders cancel error: code - {response.code} | message - {response.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Http State Code: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public bool CancelOrder(Order order)
        {
            _rateGate.WaitToProceed();

            try
            {
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                _pathUrl = "trade/order";

                string body = $"{{ \"symbol\":\"{order.SecurityNameCode}\",\"orderId\":{order.NumberMarket}}}";

                string _signature = GenerateSignature("DELETE", _pathUrl, timestamp, body, null);

                IRestResponse json = CreatePrivateRequest(_signature, _pathUrl, Method.DELETE, timestamp, body, null);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<object> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseMessageRest<object>());

                    if (response.result == "true")
                    {
                        SendLogMessage($"The order has been cancelled", LogMessageType.Trade);
                        return true;
                    }
                    else
                    {
                        OrderStateType state = GetOrderStatus(order);

                        if (state == OrderStateType.None)
                        {
                            SendLogMessage($"Order cancellation error: code - {response.code} | message - {response.message}", LogMessageType.Error);
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
                        SendLogMessage($"Order cancellation error: {json.StatusCode} - {json.Content}", LogMessageType.Error);

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
                return false;
            }
        }

        public void CancelAllOrders()
        {

        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        public void GetAllActivOrders()
        {
            List<Order> ordersOnBoard = GetAllOpenOrders();

            if (ordersOnBoard == null)
            {
                return;
            }

            for (int i = 0; i < ordersOnBoard.Count; i++)
            {
                if (MyOrderEvent != null)
                {
                    MyOrderEvent(ordersOnBoard[i]);
                }
            }
        }

        private List<Order> GetAllOpenOrders()
        {
            _rateGate.WaitToProceed();

            try
            {
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                _pathUrl = "trade/openOrders";

                List<Order> orders = new List<Order>();

                string _signature = GenerateSignature("GET", _pathUrl, timestamp, null, null);

                IRestResponse json = CreatePrivateRequest(_signature, _pathUrl, Method.GET, timestamp, null, null);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<OrderData> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseMessageRest<OrderData>());

                    if (response.result == "true")
                    {
                        for (int j = 0; j < response.data.orders.Count; j++)
                        {
                            GetOrder item = response.data.orders[j];

                            OrderStateType stateType = GetOrderState(item.status, item.filledSize);

                            if (item.type.Equals("MARKET") && stateType == OrderStateType.Active)
                            {
                                return null;
                            }

                            Order newOrder = new Order();

                            newOrder.SecurityNameCode = item.symbol;
                            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createTime));

                            if (string.IsNullOrEmpty(item.clientOrderId))
                            {
                                return null;
                            }

                            newOrder.NumberUser = Convert.ToInt32(item.clientOrderId);
                            newOrder.NumberMarket = item.orderId;
                            newOrder.Side = item.side.Equals("BUY") ? Side.Buy : Side.Sell;
                            newOrder.State = stateType;
                            newOrder.TypeOrder = item.type.Equals("MARKET") ? OrderPriceType.Market : OrderPriceType.Limit;
                            newOrder.Volume = item.status.Equals("OPEN") ? item.size.ToDecimal() : item.filledSize.ToDecimal();
                            newOrder.Price = item.price.ToDecimal();
                            newOrder.ServerType = ServerType.PionexSpot;
                            newOrder.PortfolioNumber = "PionexSpotPortfolio";

                            orders.Add(newOrder);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Get open orders error: code - {response.code} | message - {response.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Get open orders error: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                }

                return orders;
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            Order myOrder = GetOrderFromExchange(order.SecurityNameCode, order.NumberUser.ToString());

            if (myOrder == null)
            {
                return OrderStateType.None;
            }

            MyOrderEvent?.Invoke(myOrder);

            if (myOrder.State == OrderStateType.Done || myOrder.State == OrderStateType.Partial)
            {
                GetTradesForOrder(myOrder.NumberMarket);
            }

            return myOrder.State;
        }

        private Order GetOrderFromExchange(string nameSecurity, string userOrderId)
        {
            _rateGate.WaitToProceed();

            try
            {
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                _pathUrl = "trade/orderByClientOrderId";

                SortedDictionary<string, string> parameters = new SortedDictionary<string, string>
                {
                    { "clientOrderId", userOrderId }
                };

                string _signature = GenerateSignature("GET", _pathUrl, timestamp, null, parameters);

                IRestResponse json = CreatePrivateRequest(_signature, _pathUrl, Method.GET, timestamp, null, parameters);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<GetOrder> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseMessageRest<GetOrder>());

                    if (response.result == "true")
                    {
                        GetOrder item = response.data;

                        if (item.clientOrderId != userOrderId)
                        {
                            return null;
                        }

                        OrderStateType stateType = GetOrderState(item.status, item.filledSize);

                        if (item.type.Equals("MARKET") && stateType == OrderStateType.Active)
                        {
                            return null;
                        }

                        Order newOrder = new Order();

                        newOrder.SecurityNameCode = item.symbol;
                        newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createTime));

                        if (string.IsNullOrEmpty(item.clientOrderId))
                        {
                            return null;
                        }

                        newOrder.NumberUser = Convert.ToInt32(item.clientOrderId);
                        newOrder.NumberMarket = item.orderId;
                        newOrder.Side = item.side.Equals("BUY") ? Side.Buy : Side.Sell;
                        newOrder.State = stateType;
                        newOrder.TypeOrder = item.type.Equals("MARKET") ? OrderPriceType.Market : OrderPriceType.Limit;
                        newOrder.Volume = item.status.Equals("OPEN") ? item.size.ToDecimal() : item.filledSize.ToDecimal();
                        newOrder.Price = item.price.ToDecimal();
                        newOrder.ServerType = ServerType.PionexSpot;
                        newOrder.PortfolioNumber = "PionexSpotPortfolio";

                        return newOrder;
                    }
                    else
                    {
                        SendLogMessage($"Get orders error: code - {response.code} | message - {response.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Get orders error: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                }

                return null;
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        private List<MyTrade> GetTradesForOrder(string orderId)
        {
            _rateGate.WaitToProceed();

            try
            {
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                _pathUrl = "trade/fillsByOrderId";

                SortedDictionary<string, string> parameters = new SortedDictionary<string, string>
                {
                    { "orderId", orderId }
                };

                string _signature = GenerateSignature("GET", _pathUrl, timestamp, null, parameters);

                IRestResponse json = CreatePrivateRequest(_signature, _pathUrl, Method.GET, timestamp, null, parameters);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<FillData> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseMessageRest<FillData>());

                    if (response.result == "true")
                    {
                        for (int j = 0; j < response.data.fills.Count; j++)
                        {
                            FillItem item = response.data.fills[j];

                            long time = Convert.ToInt64(item.timestamp);

                            MyTrade newTrade = new MyTrade();

                            newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(time);
                            newTrade.SecurityNameCode = item.symbol;
                            newTrade.NumberOrderParent = item.orderId;
                            newTrade.Price = item.price.ToDecimal();
                            newTrade.NumberTrade = item.id;
                            newTrade.Side = item.side.Equals("SELL") ? Side.Sell : Side.Buy;

                            string commissionSecName = item.feeCoin;

                            if (newTrade.SecurityNameCode.StartsWith(commissionSecName))
                            {
                                newTrade.Volume = item.size.ToDecimal() - item.fee.ToDecimal();

                                int decimalVolum = GetVolumeDecimals(newTrade.SecurityNameCode);

                                if (decimalVolum > 0)
                                {
                                    newTrade.Volume = Math.Floor(newTrade.Volume * (decimal)Math.Pow(10, decimalVolum)) / (decimal)Math.Pow(10, decimalVolum);
                                }
                            }
                            else
                            {
                                newTrade.Volume = item.size.ToDecimal();
                            }

                            MyTradeEvent(newTrade);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Order trade request error: code - {response.code} | message - {response.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Order trade error: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                }

                return null;
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return null;
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

        private RateGate _rateGate = new RateGate(8, TimeSpan.FromMilliseconds(1000));

        private string GenerateSignature(string method, string path, string timestamp, string body, SortedDictionary<string, string> param)
        {
            try
            {
                method = method.ToUpper();

                path = string.IsNullOrEmpty(path) ? string.Empty : path + "?";

                body = string.IsNullOrEmpty(body) ? string.Empty : body;

                string preHash = string.Empty;

                if (method == "GET")
                {
                    preHash = method + _prefix + path + BuildParams(param) + "timestamp=" + timestamp;
                }
                if (method == "POST" || method == "DELETE")
                {
                    preHash = method + _prefix + path + "timestamp=" + timestamp + body;
                }

                return SHA256HexHashString(_secretKey, preHash);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        private string SHA256HexHashString(string key, string message)
        {
            try
            {
                string hashString;

                using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
                {
                    byte[] b = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));

                    hashString = ToHex(b, false);
                }

                return hashString;
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        private string ToHex(byte[] bytes, bool upperCase)
        {
            try
            {
                StringBuilder result = new StringBuilder(bytes.Length * 2);

                for (int i = 0; i < bytes.Length; i++)
                {
                    result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        public string BuildParams(SortedDictionary<string, string> _params)
        {
            if (_params == null)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();

            SortedDictionary<string, string>.Enumerator enumerator = _params.GetEnumerator();

            while (enumerator.MoveNext())
            {
                string key = enumerator.Current.Key;
                string value = enumerator.Current.Value;

                sb.Append('&');
                sb.Append(key).Append('=').Append(value);
            }
            return sb.ToString().Substring(1) + "&";
        }

        private IRestResponse CreatePrivateRequest(string signature, string pathUrl, Method method, string timestamp, string body, SortedDictionary<string, string> _params)
        {
            try
            {
                RestClient client = new RestClient(_baseUrl);

                RestRequest request = new RestRequest(_prefix + pathUrl, method);

                request.AddHeader("PIONEX-KEY", _publicKey);
                request.AddHeader("PIONEX-SIGNATURE", signature);
                request.AddQueryParameter("timestamp", timestamp);

                if (_params != null && body == null)
                {
                    SortedDictionary<string, string>.Enumerator enumerator = _params.GetEnumerator();

                    while (enumerator.MoveNext())
                    {
                        string key = enumerator.Current.Key;
                        string value = enumerator.Current.Value;
                        request.AddQueryParameter(key, value);
                    }
                }

                if (method == Method.POST || method == Method.DELETE)
                {
                    request.AddHeader("Content-Type", "application/json");
                }
                if (body != null)
                {
                    request.AddParameter("application/json", body, ParameterType.RequestBody);
                }

                return client.Execute(request);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        public void SetLeverage(Security security, decimal leverage) { }

        #endregion

        #region 12 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}