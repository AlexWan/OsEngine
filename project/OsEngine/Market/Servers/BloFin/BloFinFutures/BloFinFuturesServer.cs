/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.BloFin.Entity;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using OsEngine.Entity.WebSocketOsEngine;


namespace OsEngine.Market.Servers.BloFin
{
    public class BloFinFuturesServer : AServer
    {
        public BloFinFuturesServer(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            BloFinFuturesServerRealization realization = new BloFinFuturesServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamPassword, "");
            CreateParameterBoolean("Hedge Mode", true);
            ServerParameters[3].ValueChange += BloFinFuturesServer_ValueChange;
            CreateParameterEnum("Margin Mode", "Cross", new List<string> { "Cross", "Isolated" });

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
            ServerParameters[2].Comment = OsLocalization.Market.Label271;
            ServerParameters[3].Comment = OsLocalization.Market.Label250;
            ServerParameters[4].Comment = OsLocalization.Market.Label249;
        }

        private void BloFinFuturesServer_ValueChange()
        {
            ((BloFinFuturesServerRealization)ServerRealization).HedgeMode = ((ServerParameterBool)ServerParameters[3]).Value;
        }
    }

    public class BloFinFuturesServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public BloFinFuturesServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread threadCheckAliveWebSocket = new Thread(CheckAliveWebSocket);
            threadCheckAliveWebSocket.IsBackground = true;
            threadCheckAliveWebSocket.Name = "CheckAliveWebSocketBloFinFutures";
            threadCheckAliveWebSocket.Start();

            Thread threadMessageReaderPublic = new Thread(MessageReaderPublic);
            threadMessageReaderPublic.IsBackground = true;
            threadMessageReaderPublic.Name = "MessageReaderPublic";
            threadMessageReaderPublic.Start();

            Thread threadMessageReaderPrivate = new Thread(MessageReaderPrivate);
            threadMessageReaderPrivate.IsBackground = true;
            threadMessageReaderPrivate.Name = "MessageReaderPrivate";
            threadMessageReaderPrivate.Start();
        }

        public void Connect(WebProxy proxy = null)
        {
            try
            {
                _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
                _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
                _passphrase = ((ServerParameterPassword)ServerParameters[2]).Value;
                HedgeMode = ((ServerParameterBool)ServerParameters[3]).Value;

                if (string.IsNullOrEmpty(_publicKey) ||
                    string.IsNullOrEmpty(_secretKey) ||
                    string.IsNullOrEmpty(_passphrase))
                {
                    SendLogMessage("Can`t run BloFinFutures connector. No keys or passphrase",
                        LogMessageType.Error);
                    return;
                }

                if (((ServerParameterEnum)ServerParameters[4]).Value == "Cross")
                {
                    _marginMode = "cross";
                }
                else
                {
                    _marginMode = "isolated";
                }

                if (!CheckApiKeyInformation())
                {
                    Disconnect();
                    return;
                }

                CreateWebSocketConnection();
                CheckActivationSockets();
                SetMarginMode();
                //SetPositionMode();
            }
            catch (Exception ex)
            {
                SendLogMessage($"Can`t run BloFin connector. No internet connection. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
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
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }

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

        private void SetMarginMode()
        {
            try
            {
                _rateGate.WaitToProceed();

                Dictionary<string, string> mode = new Dictionary<string, string>();
                mode["marginMode"] = _marginMode;

                string jsonRequest = JsonConvert.SerializeObject(mode);
                string path = $"/api/v1/account/set-margin-mode";

                IRestResponse response = CreatePrivateQuery(path, Method.POST, jsonRequest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<RestMessageMarginMode> modeResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<RestMessageMarginMode>());

                    if (modeResponse.code == "0")
                    {
                        SendLogMessage($"Margin Mode: {modeResponse.data.marginMode}", LogMessageType.System);
                    }
                    else
                    {
                        SendLogMessage($"Margin Mode error. Code: {modeResponse.code} || msg: {modeResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Margin Mode error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"SetMarginMode: {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void SetPositionMode()
        {
            _rateGate.WaitToProceed();

            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                Dictionary<string, string> mode = new Dictionary<string, string>();
                mode["positionMode"] = "net_mode";

                if (_hedgeMode)
                {
                    mode["positionMode"] = "long_short_mode";
                }

                string jsonRequest = JsonConvert.SerializeObject(mode);
                string path = $"/api/v1/account/set-position-mode";

                IRestResponse response = CreatePrivateQuery(path, Method.POST, jsonRequest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<RestMessagePositionMode> modeResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<RestMessagePositionMode>());

                    if (modeResponse.code == "0")
                    {
                        SendLogMessage($"Position Mode: {modeResponse.data.positionMode}", LogMessageType.System);
                    }
                }
                else
                {
                    SendLogMessage($"Position Mode error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"SetPositionMode: {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public DateTime ServerTime { get; set; }

        public ServerType ServerType
        {
            get { return ServerType.BloFinFutures; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion 1

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string _publicKey;

        private string _secretKey;

        private string _passphrase;

        private string _baseUrl = "https://openapi.blofin.com";

        private string _webSocketUrlPublic = "wss://openapi.blofin.com/ws/public";

        private string _webSocketUrlPrivate = "wss://openapi.blofin.com/ws/private";

        //private string _baseUrlDemo = "https://demo-trading-openapi.blofin.com";

        //private string _webSocketUrlPublicDemo = "wss://demo-trading-openapi.blofin.com/ws/public";

        //private string _webSocketUrlPrivateDemo = "wss://demo-trading-openapi.blofin.com/ws/private";

        private bool _hedgeMode;

        public bool HedgeMode
        {
            get { return _hedgeMode; }
            set
            {
                if (value == _hedgeMode)
                {
                    return;
                }
                _hedgeMode = value;

                SetPositionMode();
            }
        }

        private string _marginMode;

        private RateGate _rateGate = new RateGate(1, TimeSpan.FromMilliseconds(210));

        #endregion 2

        #region 3 Securities

        private List<Security> _securities = new List<Security>();

        public void GetSecurities()
        {
            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            try
            {
                _rateGate.WaitToProceed();

                string requestStr = $"/api/v1/market/instruments";
                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                IRestResponse response = new RestClient(_baseUrl).Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<RestMessageInstruments>> symbols = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<RestMessageInstruments>>());

                    if (symbols.code == "0")
                    {
                        if (symbols.data.Count == 0)
                        {
                            return;
                        }

                        for (int i = 0; i < symbols.data.Count; i++)
                        {
                            RestMessageInstruments item = symbols.data[i];

                            Security newSecurity = new Security();

                            newSecurity.Exchange = ServerType.BloFinFutures.ToString();
                            newSecurity.Lot = 1;
                            newSecurity.Name = item.instId;
                            newSecurity.NameFull = item.instId;
                            newSecurity.NameClass = item.quoteCurrency;
                            newSecurity.NameId = item.instId + "_" + item.contractValue;
                            newSecurity.SecurityType = SecurityType.Futures;
                            newSecurity.Decimals = item.tickSize.DecimalsCount(); ;
                            newSecurity.PriceStep = item.tickSize.ToDecimal();
                            newSecurity.PriceStepCost = newSecurity.PriceStep;
                            newSecurity.State = SecurityStateType.Activ;
                            newSecurity.MinTradeAmountType = MinTradeAmountType.Contract;
                            newSecurity.MinTradeAmount = item.minSize.ToDecimal() * item.contractValue.ToDecimal();
                            newSecurity.DecimalsVolume = (item.minSize.ToDecimal() * item.contractValue.ToDecimal()).ToString().DecimalsCount();
                            newSecurity.VolumeStep = item.minSize.ToDecimal();

                            _securities.Add(newSecurity);
                        }

                        SecurityEvent(_securities);
                    }
                    else
                    {
                        SendLogMessage($"Securities error. Code:{symbols.code} || msg: {symbols.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Securities error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion 3

        #region 4 Portfolios

        public List<Portfolio> Portfolios;

        private bool _portfolioIsStarted = true;

        public void GetPortfolios()
        {

        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion 4

        #region 5 Data

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return GetCandleData(security, timeFrameBuilder, startTime, endTime, actualTime, true);
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
            DateTime endTime = DateTime.UtcNow;
            DateTime startTime = endTime.AddMinutes(-tfTotalMinutes * candleCount);

            return GetCandleData(security, timeFrameBuilder, startTime, endTime, endTime, false);
        }

        private List<Candle> GetCandleData(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime, bool isOsData)
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

            int limitCandles = 1000;

            if (isOsData)
            {
                limitCandles = 1440;
            }

            TimeSpan span = endTime - startTime;

            if (limitCandles > span.TotalMinutes / tfTotalMinutes)
            {
                limitCandles = (int)Math.Round(span.TotalMinutes / tfTotalMinutes, MidpointRounding.AwayFromZero);
            }

            List<Candle> allCandles = new List<Candle>();

            DateTime startTimeData = startTime;
            DateTime endTimeData = startTimeData.AddMinutes(tfTotalMinutes * limitCandles);

            do
            {
                long from = TimeManager.GetTimeStampMilliSecondsToDateTime(startTimeData);
                long to = TimeManager.GetTimeStampMilliSecondsToDateTime(endTimeData);

                string interval = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);

                List<Candle> candles = RequestCandleHistory(security, interval, from, to, limitCandles);

                if (candles == null || candles.Count == 0)
                {
                    break;
                }

                Candle last = candles[candles.Count - 1];

                if (allCandles.Count > 0)
                {
                    if (allCandles[allCandles.Count - 1].TimeStart == candles[0].TimeStart)
                    {
                        candles.RemoveAt(0);
                    }
                }

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

                startTimeData = endTimeData;
                endTimeData = startTimeData.AddMinutes(tfTotalMinutes * limitCandles);

                if (startTimeData >= endTime)
                {
                    break;
                }

                if (endTimeData > endTime)
                {
                    endTimeData = endTime;
                }

                span = endTimeData - startTimeData;

                if (limitCandles > span.TotalMinutes / tfTotalMinutes)
                {
                    limitCandles = (int)Math.Round(span.TotalMinutes / tfTotalMinutes, MidpointRounding.AwayFromZero);
                }

            } while (true);

            return allCandles;
        }

        private List<Candle> RequestCandleHistory(Security security, string interval, long startTime, long endTime, int limitCandles)
        {
            _rateGate.WaitToProceed();

            try
            {
                string path = $"/api/v1/market/candles";
                string requestStr = $"{path}?instId={security.Name}&bar={interval}&limit={limitCandles}";

                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                IRestResponse response = new RestClient(_baseUrl).Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return ConvertCandles(response.Content);
                }
                else
                {
                    SendLogMessage($"Http State Code: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }

            return null;
        }

        private List<Candle> ConvertCandles(string json)
        {
            RestMessageCandle symbols = JsonConvert.DeserializeObject<RestMessageCandle>(json);

            List<Candle> candles = new List<Candle>();

            if (symbols.data.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < symbols.data.Count; i++)
            {
                if (CheckCandlesToZeroData(symbols.data[i]))
                {
                    continue;
                }

                Candle candle = new Candle();

                candle.State = CandleState.Finished;
                candle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(long.Parse(symbols.data[i][0]));
                candle.Volume = symbols.data[i][5].ToDecimal();
                candle.Close = symbols.data[i][4].ToDecimal();
                candle.High = symbols.data[i][2].ToDecimal();
                candle.Low = symbols.data[i][3].ToDecimal();
                candle.Open = symbols.data[i][1].ToDecimal();

                candles.Add(candle);
            }

            candles.Reverse();

            return candles;
        }

        private bool CheckCandlesToZeroData(List<string> item)
        {
            if (item[1].ToDecimal() == 0 ||
                item[2].ToDecimal() == 0 ||
                item[3].ToDecimal() == 0 ||
                item[4].ToDecimal() == 0)
            {
                return true;
            }

            return false;
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
                || timeFrameMinutes == 3
                || timeFrameMinutes == 5
                || timeFrameMinutes == 15
                || timeFrameMinutes == 30
                || timeFrameMinutes == 60
                || timeFrameMinutes == 120
                || timeFrameMinutes == 240
                || timeFrameMinutes == 1440)
            {
                return true;
            }

            return false;
        }

        private string GetInterval(TimeSpan tf)
        {
            if (tf.Minutes != 0)
            {
                return $"{tf.Minutes}m";
            }
            else if (tf.Hours != 0)
            {
                return $"{tf.Hours}H";
            }
            else if (tf.Days != 0)
            {
                return $"{tf.Days}D";
            }

            return String.Empty;
        }

        #endregion 5

        #region 6 WebSocket creation

        private WebSocket _webSocketPrivate;

        private WebSocket _webSocketPublic;

        private void CreateWebSocketConnection()
        {
            _webSocketPrivate = new WebSocket(_webSocketUrlPrivate);

            /*_webSocketPrivate.SslConfiguration.EnabledSslProtocols
                = System.Security.Authentication.SslProtocols.Tls12;*/

            _webSocketPrivate.EmitOnPing = true;
            _webSocketPrivate.OnOpen += _webSocketPrivate_OnOpen;
            _webSocketPrivate.OnMessage += _webSocketPrivate_OnMessage;
            _webSocketPrivate.OnError += _webSocketPrivate_OnError;
            _webSocketPrivate.OnClose += _webSocketPrivate_OnClose;
            _webSocketPrivate.ConnectAsync();

            _webSocketPublic = new WebSocket(_webSocketUrlPublic);
            /*_webSocketPublic.SslConfiguration.EnabledSslProtocols
               = System.Security.Authentication.SslProtocols.Tls12;*/
            _webSocketPublic.EmitOnPing = true;

            _webSocketPublic.OnOpen += _webSocketPublic_OnOpen;
            _webSocketPublic.OnMessage += _webSocketPublic_OnMessage;
            _webSocketPublic.OnError += _webSocketPublic_OnError;
            _webSocketPublic.OnClose += _webSocketPublic_OnClose;
            _webSocketPublic.ConnectAsync();
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

                    SetPositionMode();
                }
            }
        }

        private void DeleteWebSocketConnection()
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

        private void CreateAuthMessageWebSockets()
        {
            try
            {
                string path = $"/users/self/verify";
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string nonce = Guid.NewGuid().ToString();
                string signature = GenerateSignature(timestamp, Method.GET.ToString(), path, null, nonce);

                _webSocketPrivate?.SendAsync($"{{\"op\":\"login\",\"args\":[{{\"apiKey\":\"{_publicKey}\",\"passphrase\":\"{_passphrase}\",\"timestamp\":\"{timestamp}\",\"sign\":\"{signature}\",\"nonce\":\"{nonce}\"}}]}}");
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        #endregion

        #region 7 WebSocket events

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
                if (e == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                if (e.Data.Contains("pong"))
                { // pong message
                    return;
                }

                if (e.Data.Contains("login"))
                {
                    SubscribePrivate();
                    return;
                }

                if (FIFOListWebSocketPrivateMessage == null)
                {
                    return;
                }

                FIFOListWebSocketPrivateMessage.Enqueue(e.Data);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnOpen(object sender, EventArgs e)
        {
            try
            {
                CreateAuthMessageWebSockets();
                CheckActivationSockets();
                SendLogMessage("BloFinFutures WebSocket Private connection open", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

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
                if (e == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                if (e.Data.Contains("pong"))
                { // pong message
                    return;
                }

                if (FIFOListWebSocketPublicMessage == null)
                {
                    return;
                }

                FIFOListWebSocketPublicMessage.Enqueue(e.Data);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void _webSocketPublic_OnOpen(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    CheckActivationSockets();
                    SendLogMessage("BloFinFutures WebSocket Public connection open", LogMessageType.System);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        #endregion 7

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
                        _webSocketPrivate.SendAsync($"ping");
                    }
                    else
                    {
                        Disconnect();
                    }

                    if (_webSocketPublic != null && _webSocketPublic.ReadyState == WebSocketState.Open ||
                        _webSocketPublic.ReadyState == WebSocketState.Connecting)
                    {
                        _webSocketPublic.SendAsync($"ping");
                    }
                    else
                    {
                        Disconnect();
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        #endregion 8

        #region 9 Security subscribe

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(1000));

        private List<Security> _subscribedSecutiries = new List<Security>();

        public void Subscribe(Security security)
        {
            try
            {
                _rateGateSubscribe.WaitToProceed();

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (_subscribedSecutiries != null)
                {
                    for (int i = 0; i < _subscribedSecutiries.Count; i++)
                    {
                        if (_subscribedSecutiries[i].Name.Equals(security.Name))
                        {
                            return;
                        }
                    }
                }

                _subscribedSecutiries.Add(security);

                _webSocketPublic?.SendAsync($"{{\"op\":\"subscribe\",\"args\":[{{\"channel\":\"books5\",\"instId\":\"{security.Name}\"}}]}}");
                _webSocketPublic?.SendAsync($"{{\"op\":\"subscribe\",\"args\":[{{ \"channel\":\"trades\",\"instId\":\"{security.Name}\"}}]}}");
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void SubscribePrivate()
        {
            try
            {
                _webSocketPrivate?.SendAsync("{\"op\":\"subscribe\",\"args\":[{\"channel\":\"orders\"}]}");
                _webSocketPrivate?.SendAsync("{\"op\":\"subscribe\",\"args\":[{\"channel\":\"positions\"}]}");
                _webSocketPrivate?.SendAsync("{\"op\":\"subscribe\",\"args\":[{\"channel\":\"account\"}]}");
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UnsubscribeFromAllWebSockets()
        {
            if (_webSocketPublic != null
                && _webSocketPublic.ReadyState == WebSocketState.Open)
            {
                try
                {
                    for (int i = 0; i < _subscribedSecutiries.Count; i++)
                    {
                        Security security = _subscribedSecutiries[i];

                        _webSocketPublic.SendAsync($"{{\"op\":\"unsubscribe\",\"args\":[{{\"channel\":\"books5\",\"instId\":\"{security.Name}\"}}]}}");
                        _webSocketPublic.SendAsync($"{{\"op\":\"unsubscribe\",\"args\":[{{ \"channel\":\"trades\",\"instId\":\"{security.Name}\"}}]}}");
                    }
                }
                catch
                {
                    // ignore
                }
            }

            if (_webSocketPrivate != null
                && _webSocketPrivate.ReadyState == WebSocketState.Open)
            {
                try
                {
                    _webSocketPrivate?.SendAsync("{\"op\":\"unsubscribe\",\"args\":[{\"channel\":\"orders\"}]}");
                    _webSocketPrivate?.SendAsync("{\"op\":\"unsubscribe\",\"args\":[{\"channel\":\"positions\"}]}");
                    _webSocketPrivate?.SendAsync("{\"op\":\"unsubscribe\",\"args\":[{\"channel\":\"account\"}]}");
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

        #endregion 9

        #region 10 WebSocket parsing the messages

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

                    string message = null;

                    FIFOListWebSocketPublicMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    ResponseWebSocketMessage<object> action = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<object>());

                    if (action.arg != null)
                    {
                        if (action.arg.channel.Equals("books5"))
                        {
                            UpdateMarketDepth(message);
                            continue;
                        }

                        if (action.arg.channel.Equals("trades"))
                        {
                            UpdateTrades(message);
                            continue;
                        }
                    }
                    else
                    {
                        if (action.Event != null && action.Event.Equals("error"))
                        {
                            SendLogMessage("[WS Public] Got error msg: " + action.msg, LogMessageType.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                    Thread.Sleep(3000);
                }
            }
        }

        private void MessageReaderPrivate()
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

                    if (FIFOListWebSocketPrivateMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message = null;

                    FIFOListWebSocketPrivateMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    ResponseWebSocketMessage<object> action = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<object>());

                    if (action.arg != null)
                    {
                        if (action.arg.channel.Equals("account"))
                        {
                            UpdateAccount(message);
                            continue;
                        }
                        if (action.arg.channel.Equals("positions"))
                        {
                            UpdatePositions(message);
                            continue;
                        }
                        if (action.arg.channel.Equals("orders"))
                        {
                            UpdateOrder(message);
                            continue;
                        }
                    }
                    else
                    {
                        if (action.Event != null && action.Event.Equals("error"))
                        {
                            SendLogMessage("[WS Private] Got error msg: " + action.msg, LogMessageType.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                    Thread.Sleep(3000);
                }
            }
        }

        private void UpdateMarketDepth(string message)
        {
            try
            {
                ResponseWebSocketMessage<ResponseWebSocketDepth> responseDepth = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<ResponseWebSocketDepth>());

                if (responseDepth.data == null)
                {
                    return;
                }

                if (responseDepth.data.asks.Count == 0 && responseDepth.data.bids.Count == 0)
                {
                    return;
                }

                MarketDepth marketDepth = new MarketDepth();

                List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                marketDepth.SecurityNameCode = responseDepth.arg.instId;

                for (int i = 0; i < responseDepth.data.asks.Count; i++)
                {
                    double ask = responseDepth.data.asks[i][1].ToString().ToDouble();
                    double price = responseDepth.data.asks[i][0].ToString().ToDouble();

                    if (ask == 0 ||
                        price == 0)
                    {
                        continue;
                    }

                    MarketDepthLevel level = new MarketDepthLevel();
                    level.Ask = ask;
                    level.Price = price;
                    ascs.Add(level);
                }

                for (int i = 0; i < responseDepth.data.bids.Count; i++)
                {
                    double bid = responseDepth.data.bids[i][1].ToString().ToDouble();
                    double price = responseDepth.data.bids[i][0].ToString().ToDouble();

                    if (bid == 0 ||
                        price == 0)
                    {
                        continue;
                    }

                    MarketDepthLevel level = new MarketDepthLevel();
                    level.Bid = bid;
                    level.Price = price;
                    bids.Add(level);
                }

                marketDepth.Asks = ascs;
                marketDepth.Bids = bids;

                marketDepth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseDepth.data.ts));

                if (marketDepth.Time <= _lastTimeMd)
                {
                    marketDepth.Time = _lastTimeMd.AddTicks(1);
                }

                _lastTimeMd = marketDepth.Time;

                MarketDepthEvent(marketDepth);

            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private DateTime _lastTimeMd;

        private void UpdateTrades(string message)
        {
            try
            {
                ResponseWebSocketMessage<List<ResponseWebSocketTrades>> tradeRespone = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<List<ResponseWebSocketTrades>>());

                if (tradeRespone.data == null)
                {
                    return;
                }

                Trade trade = new Trade();
                trade.SecurityNameCode = tradeRespone.data[0].instId;

                if (trade.SecurityNameCode != tradeRespone.data[0].instId)
                {
                    return;
                }

                trade.Price = tradeRespone.data[0].price.ToDecimal();
                trade.Id = tradeRespone.data[0].tradeId;
                trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(tradeRespone.data[0].ts));
                trade.Volume = tradeRespone.data[0].size.ToDecimal();

                if (tradeRespone.data[0].side.Equals("buy"))
                {
                    trade.Side = Side.Buy;
                }
                if (tradeRespone.data[0].side.Equals("sell"))
                {
                    trade.Side = Side.Sell;
                }

                NewTradesEvent?.Invoke(trade);
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
                ResponseWebSocketMessage<List<ResponseWebSocketOrder>> orderResponse = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<List<ResponseWebSocketOrder>>());

                if (orderResponse.data == null || orderResponse.data.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < orderResponse.data.Count; i++)
                {
                    Order newOrder = null;

                    if ((orderResponse.data[i].orderType.Equals("limit") ||
                    orderResponse.data[i].orderType.Equals("market")))
                    {
                        newOrder = OrderUpdate(orderResponse.data[i]);
                    }

                    if (newOrder == null)
                    {
                        continue;
                    }

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(newOrder);
                    }

                    if (newOrder.State == OrderStateType.Partial ||
                        newOrder.State == OrderStateType.Done)
                    {

                        FindMyTradesToOrder(newOrder.SecurityNameCode, newOrder.NumberMarket);

                        //ResponseWebSocketOrder item = orderResponse.data[i];

                        //MyTrade myTrade = new MyTrade();

                        //myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.updateTime));
                        //myTrade.NumberOrderParent = item.orderId.ToString();
                        //myTrade.NumberTrade = TimeManager.GetTimeStampMilliSecondsToDateTime(DateTime.Now).ToString();
                        //myTrade.Price = item.filledAmount.ToDecimal();
                        //myTrade.Volume = item.filledSize.ToDecimal();
                        //myTrade.SecurityNameCode = item.instId;
                        //myTrade.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;

                        //MyTradeEvent(myTrade);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private Order OrderUpdate(ResponseWebSocketOrder OrderResponse)
        {
            ResponseWebSocketOrder item = OrderResponse;

            Order newOrder = new Order();

            newOrder.State = GetOrderState(item.state);

            if (item.orderType.Equals("market")
                && newOrder.State != OrderStateType.Done
                && newOrder.State != OrderStateType.Partial)
            {
                return null;
            }

            newOrder.SecurityNameCode = item.instId;
            newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createTime));
            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.updateTime));

            if (newOrder.State == OrderStateType.Done)
            {
                newOrder.TimeDone = newOrder.TimeCallBack;
            }
            else if (newOrder.State == OrderStateType.Cancel)
            {
                newOrder.TimeCancel = newOrder.TimeCallBack;
            }

            int.TryParse(item.clientOrderId, out newOrder.NumberUser);

            newOrder.NumberMarket = item.orderId.ToString();
            newOrder.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;
            newOrder.Volume = item.size.ToDecimal() * GetVolume(newOrder.SecurityNameCode);
            newOrder.PortfolioNumber = "BloFinFutures";
            newOrder.Price = item.price.ToDecimal();

            if (item.orderType == "market")
            {
                newOrder.TypeOrder = OrderPriceType.Market;
            }
            else
            {
                newOrder.TypeOrder = OrderPriceType.Limit;
            }

            newOrder.ServerType = ServerType.BloFinFutures;

            return newOrder;
        }

        private void UpdatePositions(string message)
        {
            try
            {
                ResponseWebSocketMessage<List<ResponseWebSocketPosition>> positionsResponse = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<List<ResponseWebSocketPosition>>());

                if (positionsResponse == null
                    || positionsResponse.data == null
                    || Portfolios == null)
                {
                    return;
                }

                Portfolio portfolio = Portfolios[0];

                if (portfolio == null)
                {
                    return;
                }

                List<ResponseWebSocketPosition> positionsOnBoard = positionsResponse.data;

                if (positionsOnBoard.Count > 0)
                {
                    for (int i = 0; i < positionsOnBoard.Count; i++)
                    {
                        ResponseWebSocketPosition item = positionsOnBoard[i];

                        PositionOnBoard pos = new PositionOnBoard();

                        pos.PortfolioName = "BloFinFutures";

                        if (item.positionSide.Contains("long"))
                        {
                            pos.SecurityNameCode = item.instId + "_LONG";
                        }
                        else if (item.positionSide.Contains("short"))
                        {
                            pos.SecurityNameCode = item.instId + "_SHORT";
                        }
                        else
                        {
                            pos.SecurityNameCode = item.instId;
                        }

                        pos.ValueCurrent = Math.Round(item.positions.ToDecimal() * GetVolume(item.instId), 5);
                        pos.ValueBlocked = 0;
                        pos.UnrealizedPnl = Math.Round(item.unrealizedPnl.ToDecimal(), 5);

                        portfolio.SetNewPosition(pos);
                    }
                }

                List<PositionOnBoard> positionInPortfolio = portfolio.GetPositionOnBoard();

                for (int j = 0; j < positionInPortfolio.Count; j++)
                {
                    if (positionInPortfolio[j].SecurityNameCode == "USDT")
                    {
                        continue;
                    }

                    bool isInArray = false;

                    for (int i = 0; i < positionsOnBoard.Count; i++)
                    {
                        ResponseWebSocketPosition item = positionsOnBoard[i];

                        string curNameSec = item.instId;

                        if (item.positionSide.Contains("long"))
                        {
                            curNameSec = item.instId + "_LONG";
                        }
                        else if (item.positionSide.Contains("short"))
                        {
                            curNameSec = item.instId + "_SHORT";
                        }

                        if (curNameSec == positionInPortfolio[j].SecurityNameCode)
                        {
                            isInArray = true;
                            break;
                        }
                    }

                    if (isInArray == false)
                    {
                        positionInPortfolio[j].ValueCurrent = 0;
                    }
                }

                PortfolioEvent(Portfolios);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateAccount(string message)
        {
            try
            {
                ResponseWebSocketMessage<ResponseWebSocketAccount> assets = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<ResponseWebSocketAccount>());

                if (assets.data == null ||
                    assets.data.details.Count == 0)
                {
                    return;
                }

                if (Portfolios == null)
                {
                    Portfolios = new List<Portfolio>();

                    Portfolio portfolioInitial = new Portfolio();
                    portfolioInitial.Number = "BloFinFutures";
                    portfolioInitial.ValueBegin = 1;
                    portfolioInitial.ValueCurrent = 1;
                    portfolioInitial.ValueBlocked = 0;

                    Portfolios.Add(portfolioInitial);

                    PortfolioEvent(Portfolios);
                }

                Portfolio portfolio = Portfolios[0];

                if (_portfolioIsStarted)
                {
                    portfolio.ValueBegin = Math.Round(assets.data.totalEquity.ToDecimal(), 5);
                }

                portfolio.ValueCurrent = Math.Round(assets.data.totalEquity.ToDecimal(), 5);

                if (portfolio.ValueCurrent == 0)
                {
                    portfolio.ValueBegin = 1;
                    portfolio.ValueCurrent = 1;
                }

                for (int i = 0; i < assets.data.details.Count; i++)
                {
                    ResponseWebSockeDetail asset = assets.data.details[i];

                    PositionOnBoard portf = new PositionOnBoard();

                    portf.SecurityNameCode = asset.currency;

                    if (_portfolioIsStarted)
                    {
                        portf.ValueBegin = Math.Round(asset.balance.ToDecimal(), 5);
                        _portfolioIsStarted = false;
                    }

                    portf.ValueCurrent = Math.Round(asset.equity.ToDecimal(), 5);
                    portf.ValueBlocked = Math.Round(asset.frozen.ToDecimal(), 5);
                    portf.UnrealizedPnl = Math.Round(asset.isolatedUnrealizedPnl.ToDecimal(), 5);
                    portf.PortfolioName = "BloFinFutures";
                    portfolio.SetNewPosition(portf);
                }

                PortfolioEvent(Portfolios);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private OrderStateType GetOrderState(string state)
        {
            OrderStateType stateType;

            switch (state)
            {
                case ("live"):
                    stateType = OrderStateType.Active;
                    break;
                case ("partially_filled"):
                    stateType = OrderStateType.Partial;
                    break;
                case ("filled"):
                    stateType = OrderStateType.Done;
                    break;
                case ("canceled"):
                    stateType = OrderStateType.Cancel;
                    break;
                default:
                    stateType = OrderStateType.None;
                    break;
            }

            return stateType;
        }

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        #endregion 10

        #region 11 Trade

        private RateGate _rateGateTrading = new RateGate(1, TimeSpan.FromMilliseconds(350));

        public void SendOrder(Order order)
        {
            try
            {
                _rateGateTrading.WaitToProceed();

                string posSide = "net";

                if (_hedgeMode)
                {
                    posSide = order.Side == Side.Buy ? "long" : "short";

                    if (order.PositionConditionType == OrderPositionConditionType.Close)
                    {
                        posSide = order.Side == Side.Buy ? "short" : "long";
                    }
                }

                decimal volume = order.Volume / GetVolume(order.SecurityNameCode);

                Dictionary<string, string> orderRequest = new Dictionary<string, string>();

                orderRequest.Add("instId", order.SecurityNameCode);
                orderRequest.Add("marginMode", _marginMode);
                orderRequest.Add("positionSide", posSide);
                orderRequest.Add("side", order.Side == Side.Buy ? "buy" : "sell");
                orderRequest.Add("orderType", order.TypeOrder.ToString().ToLower());
                orderRequest.Add("price", order.Price.ToString().Replace(",", "."));
                orderRequest.Add("size", volume.ToString().Replace(",", "."));
                orderRequest.Add("clientOrderId", order.NumberUser.ToString());
                orderRequest.Add("brokerId", "0f43c3141c50b7e3");

                string jsonRequest = JsonConvert.SerializeObject(orderRequest);
                string path = $"/api/v1/trade/order";

                IRestResponse response = CreatePrivateQuery(path, Method.POST, jsonRequest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<RestMessageSendOrder>> orderResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<RestMessageSendOrder>>());

                    if (orderResponse.code == "0")
                    {
                        order.State = OrderStateType.Active;
                        order.NumberMarket = orderResponse.data[0].orderId;
                        MyOrderEvent.Invoke(order);
                    }
                    else
                    {
                        CreateOrderFail(order);

                        if (orderResponse != null
                            && orderResponse.data != null)
                        {
                            SendLogMessage($"Send Order error. Code: {orderResponse.data[0].code} || msg: {orderResponse.data[0].msg}", LogMessageType.Error);
                        }
                        else
                        {
                            SendLogMessage($"Send Order error. Code: {orderResponse.code} || msg: {orderResponse.msg}", LogMessageType.Error);
                        }
                    }
                }
                else
                {
                    CreateOrderFail(order);
                    SendLogMessage($"Send Order error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Send Order - {ex.Message}, {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private decimal GetVolume(string securityName)
        {
            decimal minVolume = 1;

            for (int i = 0; i < _securities.Count; i++)
            {
                if (_securities[i].Name == securityName)
                {
                    minVolume = _securities[i].NameId.Split('_')[1].ToDecimal();
                }
            }

            if (minVolume <= 0)
            {
                return 1;
            }

            return minVolume;
        }

        private void CreateOrderFail(Order order)
        {
            order.State = OrderStateType.Fail;

            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        public bool CancelOrder(Order order)
        {
            try
            {
                _rateGateTrading.WaitToProceed();

                Dictionary<string, string> orderRequest = new Dictionary<string, string>();
                orderRequest.Add("instId", order.SecurityNameCode);

                if (order.NumberMarket != null)
                {
                    orderRequest.Add("orderId", order.NumberMarket);
                }
                else
                {
                    orderRequest.Add("clientOrderId", order.NumberUser.ToString());
                }

                string jsonRequest = JsonConvert.SerializeObject(orderRequest);
                string path = $"/api/v1/trade/cancel-order";

                IRestResponse response = CreatePrivateQuery(path, Method.POST, jsonRequest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<RestMessageSendOrder>> orderResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<RestMessageSendOrder>>());

                    if (orderResponse.code == "0")
                    {
                        // Ignore
                        return true;
                    }
                    else
                    {
                        OrderStateType state = GetOrderStatus(order);

                        if (state == OrderStateType.None)
                        {
                            if (orderResponse != null
                                && orderResponse.data != null)
                            {
                                SendLogMessage($"Cancel Order error. Code: {orderResponse.data[0].code} || msg: {orderResponse.data[0].msg}", LogMessageType.Error);
                            }
                            else
                            {
                                SendLogMessage($"Cancel Order error. Code: {orderResponse.code} || msg: {orderResponse.msg}", LogMessageType.Error);
                            }
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
                        SendLogMessage($"Cancel Order error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
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
                SendLogMessage($"Cancel Order - {ex.Message}, {ex.StackTrace}", LogMessageType.Error);
            }
            return false;
        }

        public void CancelAllOrders()
        {
            List<Order> orders = GetAllOpenOrders();

            if (orders == null)
            {
                return;
            }

            for (int i = 0; i < orders.Count; i++)
            {
                CancelOrder(orders[i]);
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {

        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllOpenOrders();

            if (orders == null)
            {
                return;
            }

            for (int i = 0; i < orders.Count; i++)
            {
                if (MyOrderEvent != null)
                {
                    MyOrderEvent(orders[i]);
                }
            }
        }

        private List<Order> GetAllOpenOrders()
        {
            try
            {
                _rateGateTrading.WaitToProceed();

                string path = $"/api/v1/trade/orders-pending";

                IRestResponse response = CreatePrivateQuery(path, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<RestMessageOrder>> orderResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<RestMessageOrder>>());

                    if (orderResponse.code == "0")
                    {
                        List<Order> orders = new List<Order>();

                        for (int i = 0; i < orderResponse.data.Count; i++)
                        {
                            RestMessageOrder item = orderResponse.data[i];

                            Order newOrder = new Order();

                            OrderStateType stateType = GetOrderState(item.state);

                            newOrder.SecurityNameCode = item.instId;
                            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createTime));
                            int.TryParse(item.clientOrderId, out newOrder.NumberUser);
                            newOrder.NumberMarket = item.orderId.ToString();
                            newOrder.Side = item.side == "buy" ? Side.Buy : Side.Sell;
                            newOrder.State = stateType;
                            newOrder.Volume = item.size.ToDecimal() * GetVolume(newOrder.SecurityNameCode);
                            newOrder.Price = item.price.ToDecimal();
                            newOrder.ServerType = ServerType.BloFinFutures;
                            newOrder.PortfolioNumber = "BloFinFutures";
                            newOrder.TypeOrder = item.orderType == "limit" ? OrderPriceType.Limit : OrderPriceType.Market;

                            orders.Add(newOrder);
                        }

                        return orders;
                    }
                    else
                    {
                        SendLogMessage($"Get All Open Orders error. Code: {orderResponse.code} || msg: {orderResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Get All Open Orders error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
                return null;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Get All Open Orders - {ex.Message}, {ex.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            List<Order> orderFromExchange = GetAllOpenOrders();

            if (orderFromExchange == null
                || orderFromExchange.Count == 0)
            {
                orderFromExchange = GetOrderHistory(order.SecurityNameCode);
            }

            if (orderFromExchange == null
               || orderFromExchange.Count == 0)
            {
                return OrderStateType.None;
            }

            Order orderOnMarket = null;

            for (int i = 0; i < orderFromExchange.Count; i++)
            {
                Order curOder = orderFromExchange[i];

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

            if (orderOnMarket.State == OrderStateType.Done
                || orderOnMarket.State == OrderStateType.Partial)
            {
                FindMyTradesToOrder(order.SecurityNameCode, order.NumberMarket);
            }

            return orderOnMarket.State;
        }

        private List<Order> GetOrderHistory(string securityName)
        {
            try
            {
                _rateGateTrading.WaitToProceed();

                string path = $"/api/v1/trade/orders-history";
                string requestStr = $"{path}?instId={securityName}&stater=filled";

                IRestResponse response = CreatePrivateQuery(requestStr, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<RestMessageOrder>> orderResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<RestMessageOrder>>());

                    if (orderResponse.code == "0")
                    {
                        List<Order> orders = new List<Order>();

                        for (int i = 0; i < orderResponse.data.Count; i++)
                        {
                            RestMessageOrder item = orderResponse.data[i];

                            Order newOrder = new Order();

                            OrderStateType stateType = GetOrderState(item.state);

                            newOrder.SecurityNameCode = item.instId;
                            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createTime));
                            int.TryParse(item.clientOrderId, out newOrder.NumberUser);
                            newOrder.NumberMarket = item.orderId.ToString();
                            newOrder.Side = item.side == "buy" ? Side.Buy : Side.Sell;
                            newOrder.State = stateType;
                            newOrder.Volume = item.size.ToDecimal() * GetVolume(newOrder.SecurityNameCode);
                            newOrder.Price = item.price.ToDecimal();
                            newOrder.ServerType = ServerType.BloFinFutures;
                            newOrder.PortfolioNumber = "BloFinFutures";
                            newOrder.TypeOrder = item.orderType == "limit" ? OrderPriceType.Limit : OrderPriceType.Market;

                            orders.Add(newOrder);
                        }

                        return orders;
                    }
                    else
                    {
                        SendLogMessage($"Get Order History error. Code: {orderResponse.code} || msg: {orderResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Get Order History error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }

                return null;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Get Order History - {ex.Message}, {ex.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        private void FindMyTradesToOrder(string securityName, string numberMarket)
        {
            try
            {
                _rateGateTrading.WaitToProceed();

                string path = $"/api/v1/trade/fills-history";
                string requestStr = $"{path}?instId={securityName}&orderId={numberMarket}";

                IRestResponse response = CreatePrivateQuery(requestStr, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<RestMessageTrade>> tradeResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<RestMessageTrade>>());

                    if (tradeResponse.code == "0")
                    {
                        for (int i = 0; i < tradeResponse.data.Count; i++)
                        {
                            RestMessageTrade item = tradeResponse.data[i];

                            if (item.orderId != numberMarket)
                            {
                                continue;
                            }

                            MyTrade newTrade = new MyTrade();

                            newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts));
                            newTrade.SecurityNameCode = item.instId;
                            newTrade.NumberOrderParent = item.orderId;
                            newTrade.Price = item.fillPrice.ToDecimal();
                            newTrade.NumberTrade = item.tradeId;
                            newTrade.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;
                            newTrade.Volume = item.fillSize.ToDecimal() * GetVolume(item.instId);

                            MyTradeEvent(newTrade);
                        }
                    }
                    else
                    {
                        SendLogMessage($"My Trades error. Code: {tradeResponse.code} || msg: {tradeResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"My Trades error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"My Trades - {ex.Message}, {ex.StackTrace}", LogMessageType.Error);
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

        #endregion 11

        #region 12 Query

        private IRestResponse CreatePrivateQuery(string path, Method method, string body = "")
        {
            try
            {
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string nonce = Guid.NewGuid().ToString();

                string signature = GenerateSignature(timestamp, method.ToString(), path, body, nonce);

                RestRequest requestRest = new RestRequest(path, method);
                requestRest.AddHeader("ACCESS-KEY", _publicKey);
                requestRest.AddHeader("ACCESS-SIGN", signature);
                requestRest.AddHeader("ACCESS-TIMESTAMP", timestamp);
                requestRest.AddHeader("ACCESS-NONCE", nonce);
                requestRest.AddHeader("ACCESS-PASSPHRASE", _passphrase);
                requestRest.AddParameter("application/json", body, ParameterType.RequestBody);

                IRestResponse response = new RestClient(_baseUrl).Execute(requestRest);

                return response;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        public bool CheckApiKeyInformation()
        {
            try
            {
                _rateGate.WaitToProceed();

                string path = $"/api/v1/user/query-apikey";

                IRestResponse response = CreatePrivateQuery(path, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<APIKeyInfoData> keyInfoResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<APIKeyInfoData>());

                    if (keyInfoResponse.code == "0")
                    {
                        return true;
                    }
                    else
                    {
                        SendLogMessage($"ApiKey Information error. Code:{keyInfoResponse.code} || msg: {keyInfoResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"ApiKey Information error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }

                return false;
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return false;
            }
        }

        private string GenerateSignature(string timestamp, string method, string path, string body, string nonce)
        {
            string prehashString = $"{path}{method}{timestamp}{nonce}{body}";

            byte[] encodedString = Encoding.UTF8.GetBytes(prehashString);

            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey)))
            {
                byte[] signatureBytes = hmac.ComputeHash(encodedString);

                StringBuilder hexdigest = new StringBuilder(signatureBytes.Length * 2);

                foreach (byte b in signatureBytes)
                {
                    hexdigest.AppendFormat("{0:x2}", b);
                }

                byte[] hexdigestToBytes = Encoding.UTF8.GetBytes(hexdigest.ToString());

                string base64Encoded = Convert.ToBase64String(hexdigestToBytes);
                return base64Encoded;
            }
        }

        #endregion 12

        #region 13 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, messageType);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion 13
    }
}
