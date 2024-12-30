using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.OKX.Entity;
using OsEngine.Market.Servers.Entity;
using WebSocketSharp;
using System.Net.Http;
using OsEngine.Language;

namespace OsEngine.Market.Servers.OKX
{
    public class OkxServer : AServer
    {
        public OkxServer()
        {
            OkxServerRealization realization = new OkxServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamPassword, "");
            CreateParameterEnum("Hedge Mode", "On", new List<string> { "On", "Off" });
            CreateParameterEnum("Margin Mode", "Cross", new List<string> { "Cross", "Isolated"});
        }
    }

    public class OkxServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public OkxServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread threadMessageReaderPublic = new Thread(MessageReaderPublic);
            threadMessageReaderPublic.IsBackground = true;
            threadMessageReaderPublic.Name = "MessageReaderPublic";
            threadMessageReaderPublic.Start();

            Thread threadMessageReaderPrivate = new Thread(MessageReaderPrivate);
            threadMessageReaderPrivate.IsBackground = true;
            threadMessageReaderPrivate.Name = "MessageReaderPrivate";
            threadMessageReaderPrivate.Start();

            Thread thread = new Thread(CheckAliveWebSocket);
            thread.Name = "CheckAliveWebSocket";
            thread.Start();
        }

        public ServerType ServerType
        {
            get { return ServerType.OKX; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public DateTime ServerTime { get; set; }

        public void Connect()
        {
            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
            _password = ((ServerParameterPassword)ServerParameters[2]).Value;

            if (((ServerParameterEnum)ServerParameters[3]).Value == "On")
            {
                _hedgeMode = true;
            }
            else
            {
                _hedgeMode = false;
            }

            if (((ServerParameterEnum)ServerParameters[4]).Value == "Cross")
            {
                _marginMode = "cross";
            }
            else
            {
                _marginMode = "isolated";
            }            

            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls |
                    SecurityProtocolType.Tls11 |
                    SecurityProtocolType.Tls12 |
                    SecurityProtocolType.Tls13 |
                    SecurityProtocolType.Ssl3 |
                    SecurityProtocolType.SystemDefault;

                HttpResponseMessage response = _httpClient.GetAsync(_baseUrl + "/api/v5/public/time").Result;

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"/api/v5/public/time - Server is not available or there is no internet. \n" +
                         " \n You may have forgotten to turn on the VPN", LogMessageType.Error);
                    return;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"/api/v5/public/time - Server is not available or there is no internet. \n" +
                    exception.Message +
                    " \n You may have forgotten to turn on the VPN", LogMessageType.Error);
                return;
            }

            try
            {
                SetPositionMode();
                FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
                FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();
                CreateWebSocketConnection();
            }
            catch(Exception exception) 
            {
                SendLogMessage($"/api/v5/public/time - Server is not available or there is no internet. \n" +
                    exception.Message +
                      " \n You may have forgotten to turn on the VPN", LogMessageType.Error);
                return;
            }
        }

        public void Dispose()
        {
            try
            {
                UnsubscribeFromAllWebSockets();
                _subscribedSecurities.Clear();
                DeleteWebSocketConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
                        
            FIFOListWebSocketPublicMessage = null;
            FIFOListWebSocketPrivateMessage = null;

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string _publicKey;

        private string _secretKey;

        private string _password;

        private string _baseUrl = "https://www.okx.com";

        private string _webSocketUrlPublic = "wss://ws.okx.com:8443/ws/v5/public";

        private string _webSocketUrlPrivate = "wss://ws.okx.com:8443/ws/v5/private";
       
        private bool _hedgeMode;

        private string _marginMode;

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            try
            {
                SecurityResponse securityResponseFutures = GetFuturesSecurities();
                SecurityResponse securityResponseSpot = GetSpotSecurities();
                securityResponseFutures.data.AddRange(securityResponseSpot.data);
                UpdatePairs(securityResponseFutures);
            }
            catch (Exception error)
            {
                if (error.Message.Equals("Unexpected character encountered while parsing value: <. Path '', line 0, position 0."))
                {
                    SendLogMessage("service is unavailable", LogMessageType.Error);
                    return;
                }
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        private SecurityResponse GetFuturesSecurities()
        {
            HttpResponseMessage response = _httpClient.GetAsync(_baseUrl + "/api/v5/public/instruments?instType=SWAP").Result;

            string json = response.Content.ReadAsStringAsync().Result;

            if (response.StatusCode != HttpStatusCode.OK)
            {
                SendLogMessage($"GetFuturesSecurities - {json}", LogMessageType.Error);
            }

            SecurityResponse securityResponse = JsonConvert.DeserializeAnonymousType(json, new SecurityResponse());

            return securityResponse;
        }

        private SecurityResponse GetSpotSecurities()
        {
            HttpResponseMessage response = _httpClient.GetAsync(_baseUrl + "/api/v5/public/instruments?instType=SPOT").Result;
            string json = response.Content.ReadAsStringAsync().Result;

            if (response.StatusCode != HttpStatusCode.OK)
            {
                SendLogMessage($"GetSpotSecurities - {json}", LogMessageType.Error);
            }

            SecurityResponse securityResponse = JsonConvert.DeserializeAnonymousType(json, new SecurityResponse());

            return securityResponse;
        }

        private List<Security> _securities = new List<Security>();

        private void UpdatePairs(SecurityResponse securityResponse)
        {
            for (int i = 0; i < securityResponse.data.Count; i++)
            {
                SecurityResponseItem item = securityResponse.data[i];

                Security security = new Security();

                SecurityType securityType = SecurityType.CurrencyPair;

                if (item.instType.Equals("SWAP"))
                {
                    securityType = SecurityType.Futures;
                }

                security.Lot = item.minSz.ToDecimal();

                string volStep = item.minSz.Replace(',', '.');

                if (volStep != null
                        && volStep.Length > 0 &&
                        volStep.Split('.').Length > 1)
                {
                    security.DecimalsVolume = volStep.Split('.')[1].Length;
                }

                if (securityType == SecurityType.CurrencyPair)
                {
                    security.Name = item.instId;
                    security.NameFull = item.instId;
                    security.NameClass = "SPOT_" + item.quoteCcy;
                }
                if (securityType == SecurityType.Futures)
                {
                    security.Name = item.instId;
                    security.NameFull = item.instId;

                    if (item.instId.Contains("-USD-"))
                    {
                        security.NameClass = "SWAP_USD";
                    }
                    else
                    {
                        security.NameClass = "SWAP_" + item.settleCcy;
                    }
                }

                security.Exchange = ServerType.OKX.ToString();

                security.NameId = item.instId;
                security.SecurityType = securityType;

                security.PriceStep = item.tickSz.ToDecimal();
                security.PriceStepCost = security.PriceStep;


                if (security.PriceStep < 1)
                {
                    string prStep = security.PriceStep.ToString(CultureInfo.InvariantCulture);
                    security.Decimals = Convert.ToString(prStep).Split('.')[1].Split('1')[0].Length + 1;
                }
                else
                {
                    security.Decimals = 0;
                }

                security.State = SecurityStateType.Activ;
                _securities.Add(security);
            }

            if (SecurityEvent != null)
            {
                SecurityEvent(_securities);
            }
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private bool _portfolioIsStarted = true;
       
        public void GetPortfolios()
        {
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public RateGate _rateGateCandles = new RateGate(1, TimeSpan.FromMilliseconds(200));

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
           return GetLastCandleHistoryRecursive(security, timeFrameBuilder, candleCount, 1);
        }

        public List<Candle> GetLastCandleHistoryRecursive(
            Security security, TimeFrameBuilder timeFrameBuilder, int candleCount, int countOfCalls)
        {
            try
            {
                _rateGateCandles.WaitToProceed();

                CandlesResponse securityResponse = GetResponseCandles(security.Name, timeFrameBuilder.TimeFrameTimeSpan);

                if (securityResponse == null)
                {
                    securityResponse = GetResponseCandles(security.Name, timeFrameBuilder.TimeFrameTimeSpan);
                }

                if (securityResponse == null)
                {
                    return null;
                }

                List<Candle> candles = new List<Candle>();

                ConvertCandles(securityResponse, candles);

                if (candles == null ||
                   candles.Count == 0)
                {
                    return null;
                }

                candles.Reverse();

                if (candles != null && candles.Count != 0)
                {
                    for (int i = 0; i < candles.Count; i++)
                    {
                        candles[i].State = CandleState.Finished;
                    }
                    candles[candles.Count - 1].State = CandleState.Started;
                }

                return candles;
            }
            catch
            {

            }

            if(countOfCalls < 5)
            {
                countOfCalls++;
                return GetLastCandleHistoryRecursive(security, timeFrameBuilder, candleCount, countOfCalls);
            }

            return null;
        }

        private CandlesResponse GetResponseCandles(string nameSec, TimeSpan tf)
        {

            int NumberCandlesToLoad = GetCountCandlesToLoad();

            string bar = GetStringBar(tf);

            CandlesResponse candlesResponse = new CandlesResponse();
            candlesResponse.data = new List<List<string>>();

            do
            {
                int limit = NumberCandlesToLoad;
                if (NumberCandlesToLoad > 100)
                {
                    limit = 100;
                }

                string after = String.Empty;

                if (candlesResponse.data.Count != 0)
                {
                    after = $"&after={candlesResponse.data[candlesResponse.data.Count - 1][0]}";
                }

                string url = _baseUrl + $"/api/v5/market/history-candles?instId={nameSec}&bar={bar}&limit={limit}" + after;

                HttpResponseMessage Response = _httpClient.GetAsync(url).Result;
                string json = Response.Content.ReadAsStringAsync().Result;
                candlesResponse.data.AddRange(JsonConvert.DeserializeAnonymousType(json, new CandlesResponse()).data);

                if (Response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"GetResponseCandles - {json}", LogMessageType.Error);
                }

                NumberCandlesToLoad -= limit;

            } while (NumberCandlesToLoad > 0);

            return candlesResponse;
        }

        private int GetCountCandlesToLoad()
        {
            AServer server = (AServer)ServerMaster.GetServers().Find(server => server.ServerType == ServerType.OKX);

            for (int i = 0; i < server.ServerParameters.Count; i++)
            {
                if (server.ServerParameters[i].Name.Equals("Candles to load"))
                {
                    ServerParameterInt Param = (ServerParameterInt)server.ServerParameters[i];
                    return Param.Value;
                }
            }

            return 100;
        }

        private void ConvertCandles(CandlesResponse candlesResponse, List<Candle> candles)
        {
            for (int j = 0; j < candlesResponse.data.Count; j++)
            {
                Candle candle = new Candle();
                try
                {
                    candle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(candlesResponse.data[j][0]));

                    candle.Open = candlesResponse.data[j][1].ToDecimal();
                    candle.High = candlesResponse.data[j][2].ToDecimal();
                    candle.Low = candlesResponse.data[j][3].ToDecimal();
                    candle.Close = candlesResponse.data[j][4].ToDecimal();
                    candle.Volume = candlesResponse.data[j][5].ToDecimal();
                    string VolCcy = candlesResponse.data[j][6];

                    candles.Add(candle);
                }
                catch (Exception error)
                {
                    SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
                }
            }
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if(timeFrameBuilder.TimeFrame == TimeFrame.Min1
                || timeFrameBuilder.TimeFrame == TimeFrame.Min2
                || timeFrameBuilder.TimeFrame == TimeFrame.Min10)
            {
                return null;
            }

            if(actualTime > endTime)
            {
                return null;
            }

            if(startTime > endTime)
            {
                return null;
            }

            if (endTime > DateTime.Now)
            {
                endTime = DateTime.Now;
            }

            int CountCandlesNeedToLoad = GetCountCandlesFromTimeInterval(startTime, endTime, timeFrameBuilder.TimeFrameTimeSpan);

            List<Candle> candles = GetCandleDataHistory(security.Name, timeFrameBuilder.TimeFrameTimeSpan, CountCandlesNeedToLoad, TimeManager.GetTimeStampMilliSecondsToDateTime(endTime));

            for (int i = 0; i < candles.Count; i++)
            {
                if (candles[i].TimeStart > endTime)
                {
                    candles.RemoveAt(i);
                    i--;
                }
            }

            for (int i = 1; i < candles.Count; i++)
            {
                if (candles[i - 1].TimeStart == candles[i].TimeStart)
                {
                    candles.RemoveAt(i);
                    i--;
                }
            }

            return candles;
        }

        private int GetCountCandlesFromTimeInterval(DateTime startTime, DateTime endTime, TimeSpan timeFrameSpan)
        {
            TimeSpan timeSpanInterval = endTime - startTime;

            if (timeFrameSpan.Hours != 0)
            {
                return Convert.ToInt32(timeSpanInterval.TotalHours / timeFrameSpan.Hours);
            }
            else if (timeFrameSpan.Days != 0)
            {
                return Convert.ToInt32(timeSpanInterval.TotalDays / timeFrameSpan.Days);
            }
            else
            {
                return Convert.ToInt32(timeSpanInterval.TotalMinutes / timeFrameSpan.Minutes);
            }
        }

        public List<Candle> GetCandleDataHistory(string nameSec, TimeSpan tf, int NumberCandlesToLoad, long DataEnd)
        {
            CandlesResponse securityResponse = GetResponseDataCandles(nameSec, tf, NumberCandlesToLoad, DataEnd);

            List<Candle> candles = new List<Candle>();

            ConvertCandles(securityResponse, candles);

            candles.Reverse();

            return candles;
        }

        private CandlesResponse GetResponseDataCandles(string nameSec, TimeSpan tf, int NumberCandlesToLoad, long DataEnd)
        {
            string bar = GetStringBar(tf);

            CandlesResponse candlesResponse = new CandlesResponse();
            candlesResponse.data = new List<List<string>>();

            do
            {
                int limit = NumberCandlesToLoad;
                if (NumberCandlesToLoad > 100)
                {
                    limit = 100;
                }

                string after = $"&after={Convert.ToString(DataEnd)}";

                if (candlesResponse.data.Count != 0)
                {
                    after = $"&after={candlesResponse.data[candlesResponse.data.Count - 1][0]}";
                }


                string url = _baseUrl + $"/api/v5/market/history-candles?instId={nameSec}&bar={bar}&limit={limit}" + after;

                HttpResponseMessage Response = _httpClient.GetAsync(url).Result;
                string json = Response.Content.ReadAsStringAsync().Result;
                candlesResponse.data.AddRange(JsonConvert.DeserializeAnonymousType(json, new CandlesResponse()).data);

                if (Response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"GetResponseDataCandles - {json}", LogMessageType.Error);
                }

                NumberCandlesToLoad -= limit;

            } while (NumberCandlesToLoad > 0);

            return candlesResponse;
        }

        private string GetStringBar(TimeSpan tf)
        {
            try
            {
                if (tf.Hours != 0)
                {
                    return $"{tf.Hours}H";
                }
                if (tf.Minutes != 0)
                {
                    return $"{tf.Minutes}m";
                }
                if (tf.Days != 0)
                {
                    return $"{tf.Days}D";
                }
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }

            return String.Empty;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        #endregion

        #region 6 WebSocket creation

        private WebSocket _webSocketPublic;

        private WebSocket _webSocketPrivate;

        private bool _publicSocketOpen = false;

        private bool _privateSocketOpen = false;
               
        private void CreateWebSocketConnection()
        {
            try
            {
                _publicSocketOpen = false;
                _privateSocketOpen = false;

                if (_webSocketPublic != null)
                {
                    return;
                }

                _webSocketPublic = new WebSocket(_webSocketUrlPublic);
                _webSocketPublic.SslConfiguration.EnabledSslProtocols
                    = System.Security.Authentication.SslProtocols.Ssl3
                    | System.Security.Authentication.SslProtocols.Tls11
                    | System.Security.Authentication.SslProtocols.None
                    | System.Security.Authentication.SslProtocols.Tls12
                    | System.Security.Authentication.SslProtocols.Tls13
                    | System.Security.Authentication.SslProtocols.Tls;
                _webSocketPublic.EmitOnPing = true;
                _webSocketPublic.OnOpen += WebSocketPublic_Opened;
                _webSocketPublic.OnClose += WebSocketPublic_Closed;
                _webSocketPublic.OnMessage += WebSocketPublic_MessageReceived;
                _webSocketPublic.OnError += WebSocketPublic_Error;
                _webSocketPublic.Connect();

                if (_webSocketPrivate != null)
                {
                    return;
                }

                _webSocketPrivate = new WebSocket(_webSocketUrlPrivate);
                _webSocketPrivate.SslConfiguration.EnabledSslProtocols
                    = System.Security.Authentication.SslProtocols.Ssl3
                   | System.Security.Authentication.SslProtocols.Tls11
                   | System.Security.Authentication.SslProtocols.None
                   | System.Security.Authentication.SslProtocols.Tls12
                   | System.Security.Authentication.SslProtocols.Tls13
                   | System.Security.Authentication.SslProtocols.Tls;
                _webSocketPrivate.EmitOnPing = true;
                _webSocketPrivate.OnOpen += WebSocketPrivate_Opened;
                _webSocketPrivate.OnClose += WebSocketPrivate_Closed;
                _webSocketPrivate.OnMessage += WebSocketPrivate_MessageReceived;
                _webSocketPrivate.OnError += WebSocketPrivate_Error;
                _webSocketPrivate.Connect();
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
                    _webSocketPublic.OnOpen -= WebSocketPublic_Opened;
                    _webSocketPublic.OnClose -= WebSocketPublic_Closed;
                    _webSocketPublic.OnMessage -= WebSocketPublic_MessageReceived;
                    _webSocketPublic.OnError -= WebSocketPublic_Error;
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
                    _webSocketPrivate.OnOpen -= WebSocketPrivate_Opened;
                    _webSocketPrivate.OnClose -= WebSocketPrivate_Closed;
                    _webSocketPrivate.OnMessage -= WebSocketPrivate_MessageReceived;
                    _webSocketPrivate.OnError -= WebSocketPrivate_Error;
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
            lock (_socketActivateLocker)
            {
                if (_publicSocketOpen
                    && _privateSocketOpen
                    && ServerStatus == ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Connect;

                    if (ConnectEvent != null)
                    {
                        ConnectEvent();
                    }
                }
            }
        }

        private void CreateAuthMessageWebSockets()
        { 
            try
            {
                _webSocketPrivate.Send(Encryptor.MakeAuthRequest(_publicKey, _secretKey, _password));                         
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private void SetPositionMode()
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();

            dict["posMode"] = "net_mode";

            if (_hedgeMode)
            {
                dict["posMode"] = "long_short_mode";
            }

            try
            {
                string res = PushPositionMode(dict);
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        private string PushPositionMode(Dictionary<string, string> requestParams)
        {
            string url = $"{_baseUrl}{"/api/v5/account/set-position-mode"}";
            string bodyStr = JsonConvert.SerializeObject(requestParams);
            HttpClient client = new HttpClient(new HttpInterceptor(_publicKey, _secretKey, _password, bodyStr));
           
            HttpResponseMessage res = client.PostAsync(url, new StringContent(bodyStr, Encoding.UTF8, "application/json")).Result;
            string contentStr = res.Content.ReadAsStringAsync().Result;

            ResponseRestMessage<List<RestMessageSendOrder>> message = JsonConvert.DeserializeAnonymousType(contentStr, new ResponseRestMessage<List<RestMessageSendOrder>>());

            if (message.code.Equals("1"))
            {
                SendLogMessage($"PushPositionMode - {message.data[0].sMsg}", LogMessageType.Error);
            }

            return contentStr;           
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocketPublic_Opened(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("OKX WebSocket Public connection open", LogMessageType.System);
                    _publicSocketOpen = true;
                    CheckSocketsActivate();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublic_Closed(object sender, EventArgs e)
        {
            try
            {
                if (DisconnectEvent != null
                 & ServerStatus != ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("Connection Closed by OKX. WebSocket Public Closed Event", LogMessageType.System);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublic_MessageReceived(object sender, MessageEventArgs e)
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
                if (e.Data.Length == 4)
                { // pong message
                    return;
                }

                if (FIFOListWebSocketPublicMessage == null)
                {
                    return;
                }

                FIFOListWebSocketPublicMessage.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublic_Error(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            if (e.Exception != null)
            {
                string exception = e.Exception.ToString();

                if (exception.Contains("0x80004005")
                    || exception.Contains("no address was supplied"))
                {
                    return;
                }

                SendLogMessage(exception, LogMessageType.Error);
            }
        }

        private void WebSocketPrivate_Opened(object sender, EventArgs e)
        {
            try
            {                
                SendLogMessage("OKX WebSocket Private connection open", LogMessageType.System);
                _privateSocketOpen = true;
                CheckSocketsActivate();
                CreateAuthMessageWebSockets();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPrivate_Closed(object sender, EventArgs e)
        {
            if (DisconnectEvent != null
                && ServerStatus != ServerConnectStatus.Disconnect)
            {
                SendLogMessage("Connection Closed by OKX. WebSocket Private Closed Event", LogMessageType.System);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void WebSocketPrivate_MessageReceived(object sender, MessageEventArgs e)
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
                if (e.Data.Length == 4)
                { // pong message
                    return;
                }

                if (e.Data.Contains("login"))
                {
                    SubscribePrivate();
                }

                if (FIFOListWebSocketPrivateMessage == null)
                {
                    return;
                }

                FIFOListWebSocketPrivateMessage.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPrivate_Error(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            if (e.Exception != null)
            {
                string exception = e.Exception.ToString();

                if(exception.Contains("0x80004005") 
                    || exception.Contains("no address was supplied"))
                {
                    return;
                }

                SendLogMessage(exception, LogMessageType.Error);
            }
        }
     
        #endregion

        #region 8 WebSocket check alive

        private DateTime _timeToSendPingPublic = DateTime.Now;
        private DateTime _timeToSendPingPrivate = DateTime.Now;

        private void CheckAliveWebSocket()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(1000);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        continue;
                    }
                                       
                    if (_webSocketPublic != null &&
                    (_webSocketPublic.ReadyState == WebSocketState.Open)
                    )
                    {
                        if (_timeToSendPingPublic.AddSeconds(25) < DateTime.Now)
                        {
                            _webSocketPublic.Send("ping");
                            _timeToSendPingPublic = DateTime.Now;
                        }
                    }
                    else
                    {
                        if (ServerStatus != ServerConnectStatus.Disconnect)
                        {
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                        }
                    }
                   
                    if (_webSocketPrivate != null &&
                    (_webSocketPrivate.ReadyState == WebSocketState.Open)
                    )
                    {
                        if (_timeToSendPingPrivate.AddSeconds(25) < DateTime.Now)
                        {
                            _webSocketPrivate.Send("ping");
                            _timeToSendPingPrivate = DateTime.Now;
                        }
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
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(3000);
                }
            }
        }

        #endregion

        #region 9 Security subscribe

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(350));

        private List<Security> _subscribedSecurities = new List<Security>();

        public void Subscrible(Security security)
        {
            try
            {
                _rateGateSubscribe.WaitToProceed();
                CreateSubscribeSecurityMessageWebSocket(security);

            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void CreateSubscribeSecurityMessageWebSocket(Security security)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    if (_subscribedSecurities[i].Name == security.Name
                        && _subscribedSecurities[i].NameClass == security.NameClass)
                    {
                        return;
                    }
                }

                _subscribedSecurities.Add(security);

                SubscribeTrades(security);
                SubscribeDepths(security);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        public void SubscribeTrades(Security security)
        {           
            RequestSubscribe<SubscribeArgs> requestTrade = new RequestSubscribe<SubscribeArgs>();
            requestTrade.args = new List<SubscribeArgs>() { new SubscribeArgs() };
            requestTrade.args[0].channel = "trades";
            requestTrade.args[0].instId = security.Name;

            string json = JsonConvert.SerializeObject(requestTrade);

            _webSocketPublic.Send(json);                      
        }

        public void SubscribeDepths(Security security)
        {
            RequestSubscribe<SubscribeArgs> requestTrade = new RequestSubscribe<SubscribeArgs>();
            requestTrade.args = new List<SubscribeArgs>() { new SubscribeArgs() };
            requestTrade.args[0].channel = "books5";
            requestTrade.args[0].instId = security.Name;

            string json = JsonConvert.SerializeObject(requestTrade);

            _webSocketPublic.Send(json);           
        }

        private void SubscribePrivate()
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                _webSocketPrivate.Send($"{{\"op\": \"subscribe\",\"args\": [{{\"channel\": \"account\"}}]}}");
                _webSocketPrivate.Send($"{{\"op\": \"subscribe\",\"args\": [{{\"channel\": \"positions\",\"instType\": \"ANY\"}}]}}");
                _webSocketPrivate.Send($"{{\"op\": \"subscribe\",\"args\": [{{\"channel\": \"orders\",\"instType\": \"ANY\"}}]}}");                              
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Error);
            }
        }

        private void UnsubscribeFromAllWebSockets()
        {
            if (_webSocketPublic != null
                && _webSocketPublic.ReadyState == WebSocketState.Open)
            {
                try
                {
                    if (_subscribedSecurities != null)
                    {
                        for (int i = 0; i < _subscribedSecurities.Count; i++)
                        {
                            _webSocketPublic.Send($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"books5\",\"instId\": \"{_subscribedSecurities[i].Name}\"}}]}}");
                            _webSocketPublic.Send($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"trade\",\"instId\": \"{_subscribedSecurities[i].Name}\"}}]}}");                                                      
                        }
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
                    _webSocketPrivate.Send($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"account\"}}]}}");
                    _webSocketPrivate.Send($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"positions\",\"instType\": \"ANY\"}}]}}");
                    _webSocketPrivate.Send($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"orders\",\"instType\": \"ANY\"}}]}}");                                       
                }
                catch
                {
                    // ignore
                }
            }
        }

        #endregion

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
                                        
                    ResponseWsMessageAction<object> action = JsonConvert.DeserializeAnonymousType(message, new ResponseWsMessageAction<object>());

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
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
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
                                        
                    ResponseWsMessageAction<object> action = JsonConvert.DeserializeAnonymousType(message, new ResponseWsMessageAction<object>());

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
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(3000);
                }
            }
        }

        private void UpdatePositions(string message)
        {
            try
            {
                ResponseWsMessageAction<List<ResponseMessagePositions>> positions = JsonConvert.DeserializeAnonymousType(message, new ResponseWsMessageAction<List<ResponseMessagePositions>>());

                if (positions.data == null)
                {
                    return;
                }

                Portfolio portfolio = new Portfolio();
                portfolio.Number = "OKX";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;

                if (positions != null)
                {
                    if (positions.data.Count > 0)
                    {
                        for (int i = 0; i < positions.data.Count; i++)
                        {
                            PositionOnBoard pos = new PositionOnBoard();

                            ResponseMessagePositions item = positions.data[i];

                            pos.PortfolioName = "OKX";

                            if (item.instId.Contains("SWAP"))
                            {
                                if (item.posSide.Contains("long"))
                                {
                                    pos.SecurityNameCode = item.instId + "_LONG";
                                    pos.ValueCurrent = GetAvailPos(item.availPos);
                                    pos.ValueBlocked = 0;
                                }
                                else if (item.posSide.Contains("short"))
                                {
                                    pos.SecurityNameCode = item.instId + "_SHORT";
                                    pos.ValueCurrent = -GetAvailPos(item.availPos);
                                    pos.ValueBlocked = 0;
                                }
                                else if (item.posSide.Contains("net"))
                                {
                                    pos.SecurityNameCode = item.instId;
                                    pos.ValueCurrent = GetAvailPos(item.pos);
                                    pos.ValueBlocked = 0;
                                }
                            }
                            else
                            {
                                pos.SecurityNameCode = item.instId;
                                pos.ValueCurrent = GetAvailPos(item.pos);
                                pos.ValueBlocked = 0;
                            }

                            portfolio.SetNewPosition(pos);
                        }                      
                    }
                }
                else
                {
                    SendLogMessage("OKX ERROR. NO POSITIONS IN REQUEST.", LogMessageType.Error);
                }
                _portfolioIsStarted = true;

                PortfolioEvent(new List<Portfolio> { portfolio });
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private decimal GetAvailPos(string availPos)
        {
            if (availPos.Equals(String.Empty))
            {
                return 0;
            }
            return availPos.ToDecimal();
        }

        private void UpdateAccount(string message)
        {
            try
            {
                ResponseWsMessageAction<List<ResponseWsAccount>> assets = JsonConvert.DeserializeAnonymousType(message, new ResponseWsMessageAction<List<ResponseWsAccount>>());

                if (assets.data == null ||
                    assets.data.Count == 0)
                {
                    return;
                }

                Portfolio portfolio = new Portfolio();
                portfolio.Number = "OKX";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;

                for (int i = 0; i < assets.data[0].details.Count; i++)
                {
                    PositionOnBoard pos = new PositionOnBoard();

                    PortfolioDetails item = assets.data[0].details[i];

                    pos.PortfolioName = "OKX";
                    pos.SecurityNameCode = item.ccy;                    
                    pos.ValueCurrent = item.availBal.ToDecimal();
                    pos.ValueBlocked = item.frozenBal.ToDecimal();

                    if (_portfolioIsStarted)
                    {
                        pos.ValueBegin = item.availBal.ToDecimal();
                        _portfolioIsStarted = false;
                    }   
                    portfolio.SetNewPosition(pos);
                }

                PortfolioEvent(new List<Portfolio> { portfolio });
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private DateTime _lastTimeMd;

        private void UpdateMarketDepth(string message)
        {
            try
            {
                ResponseWsMessageAction<List<ResponseWsDepthItem>> responseDepth = JsonConvert.DeserializeAnonymousType(message, new ResponseWsMessageAction<List<ResponseWsDepthItem>>());

                if (responseDepth.data == null)
                {
                    return;
                }

                if (responseDepth.data[0].asks.Count == 0 && responseDepth.data[0].bids.Count == 0)
                {
                    return;
                }

                MarketDepth marketDepth = new MarketDepth();

                List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                marketDepth.SecurityNameCode = responseDepth.arg.instId;

                for (int i = 0; i < responseDepth.data[0].asks.Count; i++)
                {
                    decimal ask = responseDepth.data[0].asks[i][1].ToString().ToDecimal();
                    decimal price = responseDepth.data[0].asks[i][0].ToString().ToDecimal();

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

                for (int i = 0; i < responseDepth.data[0].bids.Count; i++)
                {
                    decimal bid = responseDepth.data[0].bids[i][1].ToString().ToDecimal();
                    decimal price = responseDepth.data[0].bids[i][0].ToString().ToDecimal();

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

                marketDepth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseDepth.data[0].ts));

                if (marketDepth.Time < _lastTimeMd)
                {
                    marketDepth.Time = _lastTimeMd;
                }
                else if (marketDepth.Time == _lastTimeMd)
                {
                    _lastTimeMd = DateTime.FromBinary(_lastTimeMd.Ticks + 1);
                    marketDepth.Time = _lastTimeMd;
                }

                _lastTimeMd = marketDepth.Time;

                MarketDepthEvent(marketDepth);

            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateTrades(string message)
        {
            try
            {
                ResponseWsMessageAction<List<ResponseWsTrade>> tradeRespone = JsonConvert.DeserializeAnonymousType(message, new ResponseWsMessageAction<List<ResponseWsTrade>>());

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

                trade.Price = tradeRespone.data[0].px.ToDecimal();
                trade.Id = tradeRespone.data[0].tradeId;
                trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(tradeRespone.data[0].ts));
                trade.Volume = tradeRespone.data[0].sz.ToDecimal();

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
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private void UpdateOrder(string message)
        {
            try
            {
                ResponseWsMessageAction<List<ResponseWsOrders>> OrderResponse = JsonConvert.DeserializeAnonymousType(message, new ResponseWsMessageAction<List<ResponseWsOrders>>());

                if (OrderResponse.data == null || OrderResponse.data.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < OrderResponse.data.Count; i++)
                {
                    Order newOrder = null;

                    if ((OrderResponse.data[i].ordType.Equals("limit") ||
                    OrderResponse.data[i].ordType.Equals("market")))
                    {
                        newOrder = OrderUpdate(OrderResponse.data[i]);
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
                        Thread.Sleep(500);
                        List<MyTrade> tradesInOrder = GenerateTradesToOrder(newOrder, 1);

                        for (int i2 = 0; tradesInOrder != null && i2 < tradesInOrder.Count; i2++)
                        {
                            MyTradeEvent(tradesInOrder[i2]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private Order OrderUpdate(ResponseWsOrders OrderResponse)
        {
            ResponseWsOrders item = OrderResponse;

            Order newOrder = new Order();

            newOrder.State = GetOrderState(item.state);
            newOrder.SecurityNameCode = item.instId;
            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.cTime));

            if (newOrder.State == OrderStateType.Done)
            {
                newOrder.TimeDone = newOrder.TimeCallBack;
            }
            else if (newOrder.State == OrderStateType.Cancel)
            {
                newOrder.TimeCancel = newOrder.TimeCallBack;
            }

            int.TryParse(item.clOrdId, out newOrder.NumberUser);
                 
            newOrder.NumberMarket = item.ordId.ToString();
            newOrder.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;
            newOrder.Volume = item.sz.ToDecimal();
            newOrder.PortfolioNumber = "OKX";

            if (string.IsNullOrEmpty(item.avgPx) == false
                && item.avgPx != "0")
            {
                newOrder.Price = item.avgPx.ToDecimal();
            }
            else if (string.IsNullOrEmpty(item.px) == false
                && item.px != "0")
            {
                newOrder.Price = item.px.ToDecimal();
            }

            if (item.ordType == "market")
            {
                newOrder.TypeOrder = OrderPriceType.Market;
            }
            else
            {
                newOrder.TypeOrder = OrderPriceType.Limit;
            }

            newOrder.ServerType = ServerType.OKX;

            return newOrder;
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

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        #endregion

        #region 11 Trade

        public void SendOrder(Order order)
        {           
            if (order.SecurityNameCode.Contains("SWAP"))
            {
                SendOrderSwap(order);
            }
            else
            {
                SendOrderSpot(order);
            }                              
        }

        private void SendOrderSpot(Order order)
        {
            try
            {
                Dictionary<string, dynamic> orderRequest = new Dictionary<string, dynamic>();

                orderRequest.Add("instId", order.SecurityNameCode);
                orderRequest.Add("tdMode", "cash");
                orderRequest.Add("clOrdId", order.NumberUser.ToString());
                orderRequest.Add("side", order.Side == Side.Buy ? "buy" : "sell");
                orderRequest.Add("ordType", "limit");
                orderRequest.Add("px", order.Price.ToString().Replace(",", "."));
                orderRequest.Add("sz", order.Volume.ToString().Replace(",", "."));
                orderRequest.Add("tag", "5faf8b0e85c1BCDE");

                string json = JsonConvert.SerializeObject(orderRequest);

                string url = $"{_baseUrl}/api/v5/trade/order";

                HttpClient responseMessage = new HttpClient(new HttpInterceptor(_publicKey, _secretKey, _password, json));
                HttpResponseMessage res = responseMessage.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json")).Result;
                string contentStr = res.Content.ReadAsStringAsync().Result;

                ResponseRestMessage<List<RestMessageSendOrder>> message = JsonConvert.DeserializeAnonymousType(contentStr, new ResponseRestMessage<List<RestMessageSendOrder>>());

                if (message.code.Equals("1"))
                {
                    CreateOrderFail(order);
                    SendLogMessage($"SendOrderSpot - {message.data[0].sMsg}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"SendOrderSpot - {ex.Message}", LogMessageType.Error);
            }
        }

        private void SendOrderSwap(Order order)
        {
            try
            {
                string posSide = "net";

                if (_hedgeMode)
                {
                    posSide = order.Side == Side.Buy ? "long" : "short";

                    if (order.PositionConditionType == OrderPositionConditionType.Close)
                    {
                        posSide = order.Side == Side.Buy ? "short" : "long";
                    }
                }                

                Dictionary<string, dynamic> orderRequest = new Dictionary<string, dynamic>();

                orderRequest.Add("instId", order.SecurityNameCode);
                orderRequest.Add("tdMode", _marginMode);
                orderRequest.Add("clOrdId", order.NumberUser.ToString());
                orderRequest.Add("side", order.Side == Side.Buy ? "buy" : "sell");
                orderRequest.Add("ordType", order.TypeOrder.ToString().ToLower());
                orderRequest.Add("px", order.Price.ToString().Replace(",", "."));
                orderRequest.Add("sz", order.Volume.ToString().Replace(",", "."));
                orderRequest.Add("posSide", posSide);
                orderRequest.Add("tag", "5faf8b0e85c1BCDE");

                string json = JsonConvert.SerializeObject(orderRequest);

                string url = $"{_baseUrl}/api/v5/trade/order";

                HttpClient responseMessage = new HttpClient(new HttpInterceptor(_publicKey, _secretKey, _password, json));
                HttpResponseMessage res = responseMessage.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json")).Result;
                string contentStr = res.Content.ReadAsStringAsync().Result;

                ResponseRestMessage<List<RestMessageSendOrder>> message = JsonConvert.DeserializeAnonymousType(contentStr, new ResponseRestMessage<List<RestMessageSendOrder>>());

                if (message.code.Equals("1"))
                {
                    CreateOrderFail(order);
                    SendLogMessage($"SendOrderSwap - {message.data[0].sMsg}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"SendOrderSwap - {ex.Message}", LogMessageType.Error);
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

        public void CancelOrder(Order order)
        {
            try
            {
                Dictionary<string, dynamic> orderRequest = new Dictionary<string, dynamic>();

                orderRequest.Add("instId", order.SecurityNameCode);
                orderRequest.Add("ordId", order.NumberMarket);

                string json = JsonConvert.SerializeObject(orderRequest);

                string url = $"{_baseUrl}/api/v5/trade/cancel-order";

                HttpClient responseMessage = new HttpClient(new HttpInterceptor(_publicKey, _secretKey, _password, json));
                HttpResponseMessage res = responseMessage.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json")).Result;
                string contentStr = res.Content.ReadAsStringAsync().Result;

                ResponseRestMessage<List<RestMessageSendOrder>> message = JsonConvert.DeserializeAnonymousType(contentStr, new ResponseRestMessage<List<RestMessageSendOrder>>());

                if (message.code.Equals("1"))
                {
                    CreateOrderFail(order);
                    SendLogMessage($"CancelOrder - {message.data[0].sMsg}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"CancelOrder - {ex.Message}", LogMessageType.Error);
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
        }

        public void CancelAllOrders()
        {
            List<Order> orders = GetActiveOrders();

            if (orders == null)
            {
                return;
            }

            for (int i = 0; i < orders.Count; i++)
            {
                CancelOrder(orders[i]);
            }
        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetActiveOrders();

            if(orders == null)
            {
                return;
            }

            for(int i = 0;i < orders.Count;i++)
            {
                if(MyOrderEvent != null)
                {
                    MyOrderEvent(orders[i]);
                }
            }
        }

        public void GetOrderStatus(Order order)
        {
            string url;

            if(string.IsNullOrEmpty(order.NumberMarket))
            {
                url =
                    $"{_baseUrl}/api/v5/trade/order"
                    + $"?clOrdId={order.NumberUser}&"
                    + $"instId={order.SecurityNameCode}";
            }
            else
            {
                url =
                    $"{_baseUrl}/api/v5/trade/order"
                    + $"?ordId={order.NumberMarket}&"
                    + $"clOrdId={order.NumberUser}&"
                    + $"instId={order.SecurityNameCode}";
            }

            HttpResponseMessage res = GetPrivateRequest(url);
            string contentStr = res.Content.ReadAsStringAsync().Result;

            if (res.StatusCode != HttpStatusCode.OK)
            {
                SendLogMessage($"GetOrderStatus - {contentStr}", LogMessageType.Error);
                return;
            }
            UpdateOrder(contentStr);
        }

        private List<Order> GetActiveOrders()
        {
            try
            {
                string url = $"{_baseUrl}/api/v5/trade/orders-pending";
                HttpResponseMessage res = GetPrivateRequest(url);
                string contentStr = res.Content.ReadAsStringAsync().Result;

                if (res.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"GetActivOrders - {contentStr}", LogMessageType.Error);
                    return null;
                }

                ResponseWsMessageAction<List<ResponseWsOrders>> OrderResponse = JsonConvert.DeserializeAnonymousType(contentStr, new ResponseWsMessageAction<List<ResponseWsOrders>>());

                List<Order> orders = new List<Order>();

                for (int i = 0; i < OrderResponse.data.Count; i++)
                {
                    Order newOrder = null;

                    if ((OrderResponse.data[i].ordType.Equals("limit") ||
                        OrderResponse.data[i].ordType.Equals("market")))
                    {
                        newOrder = OrderUpdate(OrderResponse.data[i]);
                    }

                    if (newOrder == null)
                    {
                        continue;
                    }

                    orders.Add(newOrder);
                }
                return orders;
            }
            catch (Exception ex)
            {
                SendLogMessage($"GetActivOrders - {ex.Message}", LogMessageType.Error);
                return null;
            }
        }

        private RateGate _rateGateGenerateToTrade = new RateGate(1, TimeSpan.FromMilliseconds(300));

        private List<MyTrade> GenerateTradesToOrder(Order order, int CountOfCalls)
        {
            try
            {

                _rateGateGenerateToTrade.WaitToProceed();

                List<MyTrade> myTrades = new List<MyTrade>();

                if (CountOfCalls >= 8)
                {
                    SendLogMessage($"Trade is not found to order: {order.NumberUser}", LogMessageType.Error);
                    return myTrades;
                }

                string TypeInstr = order.SecurityNameCode.EndsWith("SWAP") ? "SWAP" : "SPOT";

                string url = $"{_baseUrl}/api/v5/trade/fills-history?ordId={order.NumberMarket}&instId={order.SecurityNameCode}&instType={TypeInstr}";

                HttpResponseMessage res = GetPrivateRequest(url);

                string contentStr = res.Content.ReadAsStringAsync().Result;

                if (res.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"GenerateTradesToOrder - {contentStr}", LogMessageType.Error);
                }

                TradeDetailsResponse quotes = JsonConvert.DeserializeAnonymousType(contentStr, new TradeDetailsResponse());

                if (quotes == null ||
                    quotes.data == null ||
                    quotes.data.Count == 0)
                {
                    Thread.Sleep(500 * CountOfCalls);

                    CountOfCalls++;

                    return GenerateTradesToOrder(order, CountOfCalls);
                }

                CreateListTrades(myTrades, quotes);

                return myTrades;
            }
            catch (Exception ex)
            {
                SendLogMessage($"GenerateTradesToOrder - {ex.Message}", LogMessageType.Error);
                return null;
            }
        }

        private void CreateListTrades(List<MyTrade> myTrades, TradeDetailsResponse quotes)
        {
            for (int i = 0; i < quotes.data.Count; i++)
            {
                TradeDetailsObject item = quotes.data[i];

                MyTrade myTrade = new MyTrade();

                myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts));
                myTrade.NumberOrderParent = item.ordId.ToString();
                myTrade.NumberTrade = item.tradeId.ToString();

                if (string.IsNullOrEmpty(item.fee))
                {
                    myTrade.Volume = item.fillSz.ToDecimal();
                }
                else
                {// комиссия есть

                    if (item.instId.StartsWith(item.feeCcy))
                    { // комиссия взята в торгуемой валюте, а не в валюте биржи
                        myTrade.Volume = item.fillSz.ToDecimal() + item.fee.ToDecimal();
                    }
                    else
                    {
                        myTrade.Volume = item.fillSz.ToDecimal();
                    }
                }

                if (!item.fillPx.Equals(String.Empty))
                {
                    myTrade.Price = item.fillPx.ToDecimal();
                }

                myTrade.SecurityNameCode = item.instId;
                myTrade.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;

                myTrades.Add(myTrade);
            }
        }

        public void GetOrdersState(List<Order> orders)
        {
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
        }

        #endregion

        #region 12 Queries

        HttpClient _httpClient = new HttpClient();

        public HttpResponseMessage GetPrivateRequest(string url)
        {
            HttpClient _client = new HttpClient(new HttpInterceptor(_publicKey, _secretKey, _password, null)); 
            return _client.GetAsync(url).Result;            
        }

        #endregion

        #region 13 Log

        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}