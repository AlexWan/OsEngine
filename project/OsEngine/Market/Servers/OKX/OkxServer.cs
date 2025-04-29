﻿using System;
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
using RestSharp;

namespace OsEngine.Market.Servers.OKX
{
    public class OkxServer : AServer
    {
        public OkxServer(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            OkxServerRealization realization = new OkxServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamPassword, "");
            CreateParameterEnum("Hedge Mode", "On", new List<string> { "On", "Off" });
            CreateParameterEnum("Margin Mode", "Cross", new List<string> { "Cross", "Isolated" });            
            CreateParameterBoolean(OsLocalization.Market.UseOptions, false);
            CreateParameterEnum("Demo Mode", "Off", new List<string> { "Off", "On" });
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

        private WebProxy _myProxy;

        public void Connect(WebProxy proxy)
        {
            _myProxy = proxy;

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

            _useOptions = ((ServerParameterBool)ServerParameters[5]).Value;

            if (((ServerParameterEnum)ServerParameters[6]).Value == "Off")
            {
                _demoMode = false;
            }
            else
            {
                _demoMode = true;
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
                CreatePublicWebSocketConnect();
                CreatePrivateWebSocketConnect();
                CheckSocketsActivate();
            }
            catch (Exception exception)
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

        private string _webSocketUrlPublicDemo = "wss://wspap.okx.com:8443/ws/v5/public";

        private string _webSocketUrlPrivateDemo = "wss://wspap.okx.com:8443/ws/v5/private";

        private bool _hedgeMode;

        private string _marginMode;

        private bool _useOptions;

        private bool _demoMode;

        #endregion

        #region 3 Securities

        private List<string> _baseOptionSerurities = null;

        public void GetSecurities()
        {
            try
            {
                SecurityResponse securityResponseFutures = GetFuturesSecurities();
                SecurityResponse securityResponseSpot = GetSpotSecurities();
                securityResponseFutures.data.AddRange(securityResponseSpot.data);

                if (_useOptions)
                {
                    _baseOptionSerurities = GetOptionBaseSecurities();
                    SecurityResponse securityResponseOptions = GetOptionSecurities(_baseOptionSerurities);

                    securityResponseFutures.data.AddRange(securityResponseOptions.data);
                }

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
            try
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
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        private List<string> GetOptionBaseSecurities()
        {
            try
            {
                //get list of possible options
                HttpResponseMessage response = _httpClient.GetAsync(_baseUrl + "/api/v5/public/underlying?instType=OPTION").Result;

                string json = response.Content.ReadAsStringAsync().Result;

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"GetOptionSecurities - {json}", LogMessageType.Error);
                }

                SecurityUnderlyingResponse baseSecuritiesResponse = JsonConvert.DeserializeAnonymousType(json, new SecurityUnderlyingResponse());

                if (baseSecuritiesResponse == null ||
                    baseSecuritiesResponse.data == null ||
                    baseSecuritiesResponse.data.Count == 0)
                {
                    SendLogMessage($"GetOptionSecurities - Empty underlying", LogMessageType.Error);
                    return null;
                }

                var baseSecurities = baseSecuritiesResponse.data[0];

                return baseSecurities;
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        private SecurityResponse GetOptionSecurities(List<string> baseSecurities)
        {
            try
            {
                SecurityResponse ret = null;

                for (int k = 0; k < baseSecurities.Count; k++)
                {
                    string baseSecurity = baseSecurities[k];

                    HttpResponseMessage response = _httpClient.GetAsync(_baseUrl + "/api/v5/public/instruments?instType=OPTION&uly=" + baseSecurity).Result;

                    string json = response.Content.ReadAsStringAsync().Result;

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        SendLogMessage($"GetOptionSecurities - {json}", LogMessageType.Error);
                    }

                    SecurityResponse securityResponse = JsonConvert.DeserializeAnonymousType(json, new SecurityResponse());

                    if (ret == null)
                    {
                        ret = securityResponse;
                    }
                    else
                    {
                        ret.data.AddRange(securityResponse.data);
                    }
                }

                return ret;
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        private SecurityResponse GetSpotSecurities()
        {
            try
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
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
                return null;
            }
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
                else if (item.instType.Equals("OPTION"))
                {
                    securityType = SecurityType.Option;
                }

                security.Lot = item.lotSz.ToDecimal();

                string volStep = item.minSz.Replace(',', '.');

                if (volStep != null
                        && volStep.Length > 0 &&
                        volStep.Split('.').Length > 1)
                {
                    security.DecimalsVolume = volStep.Split('.')[1].Length;
                }

                security.MinTradeAmountType = MinTradeAmountType.Contract;
                security.MinTradeAmount = item.minSz.ToDecimal();
                security.VolumeStep = item.lotSz.ToDecimal();
                security.Name = item.instId;
                security.NameFull = item.instId;

                if (securityType == SecurityType.CurrencyPair)
                {
                    security.NameClass = "SPOT_" + item.quoteCcy;
                }

                if (securityType == SecurityType.Futures)
                {
                    if (item.instId.Contains("-USD-"))
                    {
                        security.NameClass = "SWAP_USD";
                    }
                    else
                    {
                        security.NameClass = "SWAP_" + item.settleCcy;
                    }

                    security.Lot = item.ctVal.ToDecimal();
                }

                if (securityType == SecurityType.Option)
                {
                    if (item.quoteCcy == "")
                    {
                        security.NameClass = "OPTION_USD";
                    }
                    else
                    {
                        security.NameClass = "OPTION_" + item.quoteCcy;
                    }

                    security.Lot = item.ctVal.ToDecimal();
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

        public List<Portfolio> Portfolios;

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

            if (countOfCalls < 5)
            {
                countOfCalls++;
                return GetLastCandleHistoryRecursive(security, timeFrameBuilder, candleCount, countOfCalls);
            }

            return null;
        }

        private CandlesResponse GetResponseCandles(string nameSec, TimeSpan tf)
        {
            try
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
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
                return null;
            }
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
            startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
            endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
            actualTime = DateTime.SpecifyKind(actualTime, DateTimeKind.Utc);

            if (timeFrameBuilder.TimeFrame == TimeFrame.Min1
                || timeFrameBuilder.TimeFrame == TimeFrame.Min2
                || timeFrameBuilder.TimeFrame == TimeFrame.Min10)
            {
                return null;
            }

            if (actualTime > endTime)
            {
                return null;
            }

            if (startTime > endTime)
            {
                return null;
            }

            if (endTime > DateTime.UtcNow)
            {
                endTime = DateTime.UtcNow;
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
            _rateGateCandles.WaitToProceed();

            try
            {
                string bar = GetStringBar(tf);

                CandlesResponse candlesResponse = new CandlesResponse();
                candlesResponse.data = new List<List<string>>();

                //Thread.Sleep(1000);

                do
                {
                    _rateGateCandles.WaitToProceed();

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

                    RestClient client = new RestClient(url);
                    RestRequest request = new RestRequest(Method.GET);
                    IRestResponse Response = client.Execute(request);

                    //HttpResponseMessage Response = _httpClient.GetAsync(url).Result;
                    //string json = Response.Content.ReadAsStringAsync().Result;

                    if (Response.StatusCode == HttpStatusCode.OK)
                    {
                        candlesResponse.data.AddRange(JsonConvert.DeserializeAnonymousType(Response.Content, new CandlesResponse()).data);
                    }
                    else
                    {
                        SendLogMessage($"GetResponseDataCandles - {Response.Content}", LogMessageType.Error);
                    }

                    NumberCandlesToLoad -= limit;

                } while (NumberCandlesToLoad > 0);

                return candlesResponse;
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }

            return null;
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

        private List<WebSocket> _webSocketPublic = new List<WebSocket>();

        private WebSocket _webSocketPrivate;

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
                WebSocket webSocketPublicNew = null;

                if (_demoMode)
                {
                    webSocketPublicNew = new WebSocket(_webSocketUrlPublicDemo);
                }
                else
                {
                    webSocketPublicNew = new WebSocket(_webSocketUrlPublic);
                }

                if (_myProxy != null)
                {
                    NetworkCredential credential = (NetworkCredential)_myProxy.Credentials;
                    webSocketPublicNew.SetProxy(_myProxy.Address.ToString(), credential.UserName, credential.Password);
                }

                webSocketPublicNew.SslConfiguration.EnabledSslProtocols
                    = System.Security.Authentication.SslProtocols.Ssl3
                    | System.Security.Authentication.SslProtocols.Tls11
                    | System.Security.Authentication.SslProtocols.None
                    | System.Security.Authentication.SslProtocols.Tls12
                    | System.Security.Authentication.SslProtocols.Tls13
                    | System.Security.Authentication.SslProtocols.Tls;
                webSocketPublicNew.EmitOnPing = true;
                webSocketPublicNew.OnOpen += WebSocketPublic_Opened;
                webSocketPublicNew.OnClose += WebSocketPublic_Closed;
                webSocketPublicNew.OnMessage += WebSocketPublic_MessageReceived;
                webSocketPublicNew.OnError += WebSocketPublic_Error;
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
            try
            {
                if (_webSocketPrivate != null)
                {
                    return;
                }

                if (_demoMode)
                {
                    _webSocketPrivate = new WebSocket(_webSocketUrlPrivateDemo);
                }
                else
                {
                    _webSocketPrivate = new WebSocket(_webSocketUrlPrivate);
                }

                if (_myProxy != null)
                {
                    NetworkCredential credential = (NetworkCredential)_myProxy.Credentials;
                    _webSocketPrivate.SetProxy(_myProxy.Address.ToString(), credential.UserName, credential.Password);
                }

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
                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        webSocketPublic.OnOpen -= WebSocketPublic_Opened;
                        webSocketPublic.OnClose -= WebSocketPublic_Closed;
                        webSocketPublic.OnMessage -= WebSocketPublic_MessageReceived;
                        webSocketPublic.OnError -= WebSocketPublic_Error;

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
            HttpClient client = new HttpClient(new HttpInterceptor(_publicKey, _secretKey, _password, bodyStr, _demoMode, _myProxy));

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

                if (exception.Contains("0x80004005")
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

                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];
                        if (webSocketPublic != null && webSocketPublic?.ReadyState == WebSocketState.Open)
                        {
                            if (_timeToSendPingPublic.AddSeconds(25) < DateTime.Now)
                            {
                                webSocketPublic.Send("ping");
                                _timeToSendPingPublic = DateTime.Now;
                            }
                        }
                        else
                        {
                            Disconnect();
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
                        Disconnect();
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

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(450));

        //mapping: secutity name -> option (true or false)
        private Dictionary<string, bool> _subscribedSecurities = new Dictionary<string, bool>();

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

                string securityName = security.Name;

                if (_subscribedSecurities.ContainsKey(securityName))
                {
                    return;
                }

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
                    SubscribeTrades(security, webSocketPublic);
                    SubscribeDepths(security, webSocketPublic);

                }

                if (_useOptions && security.SecurityType == SecurityType.Option)
                {
                    _subscribedSecurities.Add(securityName, true);

                    _rateGateSubscribe.WaitToProceed();

                    SubscribeOpenInterest(security.Name, webSocketPublic);
                    SubscribeMarkPrice(security.Name, webSocketPublic);

                    securityName = securityName.Substring(0, 7);

                    string key = securityName + "-OPTION";
                    if (!_subscribedSecurities.ContainsKey(key))
                    {
                        SubscribeOptionSummary(securityName, webSocketPublic);
                        //for underlying price
                        SubscribeMarkPrice(securityName + "-SWAP", webSocketPublic);

                        _subscribedSecurities.Add(key, false);
                    }
                }
                else
                {
                    _subscribedSecurities.Add(securityName, false);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        public void SubscribeTrades(Security security, WebSocket webSocketPublic)
        {
            RequestSubscribe<SubscribeArgs> requestTrade = new RequestSubscribe<SubscribeArgs>();
            requestTrade.args = new List<SubscribeArgs>() { new SubscribeArgs() };
            requestTrade.args[0].channel = "trades";
            requestTrade.args[0].instId = security.Name;

            string json = JsonConvert.SerializeObject(requestTrade);

            webSocketPublic.Send(json);
        }

        public void SubscribeDepths(Security security, WebSocket webSocketPublic)
        {
            RequestSubscribe<SubscribeArgs> requestTrade = new RequestSubscribe<SubscribeArgs>();
            requestTrade.args = new List<SubscribeArgs>() { new SubscribeArgs() };
            requestTrade.args[0].channel = "books5";
            requestTrade.args[0].instId = security.Name;

            // webSocketPublic.Send($"{{\"op\": \"subscribe\",\"args\": [{{\"channel\": \"books5\",\"instId\": \"{security.Name}\"}}]}}");
            string json = JsonConvert.SerializeObject(requestTrade);
            webSocketPublic.Send(json);
        }

        public void SubscribeOptionSummary(string securityName, WebSocket webSocketPublic)
        {
            RequestSubscribe<SubscribeArgsOption> requestTrade = new RequestSubscribe<SubscribeArgsOption>();
            requestTrade.args = new List<SubscribeArgsOption>() { new SubscribeArgsOption() };
            requestTrade.args[0].channel = "opt-summary";
            requestTrade.args[0].instFamily = securityName; //"BTC-USD"

            string json = JsonConvert.SerializeObject(requestTrade);
            webSocketPublic.Send(json);
        }

        public void SubscribeOpenInterest(string name, WebSocket webSocketPublic)
        {
            RequestSubscribe<SubscribeArgs> requestTrade = new RequestSubscribe<SubscribeArgs>();
            requestTrade.args = new List<SubscribeArgs>() { new SubscribeArgs() };
            requestTrade.args[0].channel = "open-interest";
            requestTrade.args[0].instId = name; //"LTC-USD-SWAP"

            string json = JsonConvert.SerializeObject(requestTrade);

            webSocketPublic.Send(json);
        }

        public void SubscribeMarkPrice(string name, WebSocket webSocketPublic)
        {
            RequestSubscribe<SubscribeArgs> requestTrade = new RequestSubscribe<SubscribeArgs>();
            requestTrade.args = new List<SubscribeArgs>() { new SubscribeArgs() };
            requestTrade.args[0].channel = "mark-price";
            requestTrade.args[0].instId = name; //"LTC-USD-SWAP"

            string json = JsonConvert.SerializeObject(requestTrade);
            webSocketPublic.Send(json);
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
            try
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
                                foreach (var item in _subscribedSecurities)
                                {
                                    string name = item.Key;
                                    webSocketPublic.Send($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"books5\",\"instId\": \"{name}\"}}]}}");
                                    webSocketPublic.Send($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"trade\",\"instId\": \"{name}\"}}]}}");

                                    if (item.Value)
                                    {
                                        //option
                                        webSocketPublic.Send($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"open-interest\",\"instId\": \"{name}\"}}]}}");
                                        webSocketPublic.Send($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"mark-price\",\"instId\": \"{name}\"}}]}}");
                                    }
                                }
                            }

                            if (_baseOptionSerurities != null)
                            {
                                foreach (string name in _baseOptionSerurities)
                                {
                                    webSocketPublic.Send($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"opt-summary\",\"instFamily\": \"{name}\"}}]}}");
                                    webSocketPublic.Send($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"mark-price\",\"instId\": \"{name}-SWAP\"}}]}}");
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
            catch
            {
                // ignore
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

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent;

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

                    if (action.@event != null && action.@event.Contains("subscribe"))
                    {
                        //ignore
                        SendLogMessage("[WS Public] Got subscribe msg: " + action.msg, LogMessageType.System);
                    }
                    else if (action.arg != null)
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

                        if (action.arg.channel.Equals("opt-summary"))
                        {
                            UpdateOptionSummary(message);
                            continue;
                        }

                        if (action.arg.channel.Equals("open-interest"))
                        {
                            UpdateOpenInterest(message);
                            continue;
                        }

                        if (action.arg.channel.Equals("mark-price"))
                        {
                            UpdateMarkPrice(message);
                            continue;
                        }
                    }
                    else
                    {
                        if (action.@event != null && action.@event.Equals("error"))
                        {
                            SendLogMessage("[WS Public] Got error msg: " + action.msg, LogMessageType.Error);
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

                if (positions.data == null || Portfolios == null)
                {
                    return;
                }

                Portfolio portfolio = Portfolios[0];

                if (portfolio == null)
                {
                    return;
                }

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
                                    pos.ValueCurrent = Math.Round(GetAvailPos(item.pos), 6);
                                    pos.ValueBlocked = 0;
                                    pos.UnrealizedPnl = Math.Round(GetAvailPos(item.upl), 6);
                                }
                                else if (item.posSide.Contains("short"))
                                {
                                    pos.SecurityNameCode = item.instId + "_SHORT";
                                    pos.ValueCurrent = -Math.Round(GetAvailPos(item.pos), 6);
                                    pos.ValueBlocked = 0;
                                    pos.UnrealizedPnl = Math.Round(GetAvailPos(item.upl), 6);
                                }
                                else if (item.posSide.Contains("net"))
                                {
                                    pos.SecurityNameCode = item.instId;
                                    pos.ValueCurrent = Math.Round(GetAvailPos(item.pos), 6);
                                    pos.ValueBlocked = 0;
                                    pos.UnrealizedPnl = Math.Round(GetAvailPos(item.upl), 6);
                                }
                            }
                            else
                            {
                                pos.SecurityNameCode = item.instId;
                                pos.ValueCurrent = Math.Round(GetAvailPos(item.pos), 6);
                                pos.ValueBlocked = 0;
                                pos.UnrealizedPnl = Math.Round(GetAvailPos(item.upl), 6);
                            }

                            portfolio.SetNewPosition(pos);
                        }
                    }
                }
                else
                {
                    SendLogMessage("OKX ERROR. NO POSITIONS IN REQUEST.", LogMessageType.Error);
                }
                // _portfolioIsStarted = true;

                PortfolioEvent(Portfolios);
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

                if (Portfolios == null)
                {
                    Portfolios = new List<Portfolio>();

                    Portfolio portfolioInitial = new Portfolio();
                    portfolioInitial.Number = "OKX";
                    portfolioInitial.ValueBegin = 1;
                    portfolioInitial.ValueCurrent = 1;
                    portfolioInitial.ValueBlocked = 0;

                    Portfolios.Add(portfolioInitial);

                    PortfolioEvent(Portfolios);
                }

                Portfolio portfolio = Portfolios[0];
                portfolio.Number = "OKX";

                if (_portfolioIsStarted)
                {
                    portfolio.ValueBegin = Math.Round(assets.data[0].totalEq.ToDecimal(), 4);
                    _portfolioIsStarted = false;
                }

                portfolio.ValueCurrent = Math.Round(assets.data[0].totalEq.ToDecimal(), 4);

                for (int i = 0; i < assets.data[0].details.Count; i++)
                {
                    PositionOnBoard pos = new PositionOnBoard();

                    PortfolioDetails item = assets.data[0].details[i];

                    pos.PortfolioName = "OKX";
                    pos.SecurityNameCode = item.ccy;
                    pos.ValueCurrent = Math.Round(item.availBal.ToDecimal(), 6);
                    pos.ValueBlocked = Math.Round(item.frozenBal.ToDecimal(), 6);
                    pos.UnrealizedPnl = Math.Round(GetAvailPos(item.upl), 6);

                    if (item.ccy == "USDT")
                    {
                        portfolio.UnrealizedPnl = Math.Round(GetAvailPos(item.upl), 6);
                    }

                    pos.ValueBegin = Math.Round(item.eq.ToDecimal(), 6);
                    portfolio.SetNewPosition(pos);
                }

                PortfolioEvent(Portfolios);
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

                if (marketDepth.Time <= _lastTimeMd)
                {
                    marketDepth.Time = _lastTimeMd.AddTicks(1);
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
                        ResponseWsOrders item = OrderResponse.data[i];

                        MyTrade myTrade = new MyTrade();

                        myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.cTime));
                        myTrade.NumberOrderParent = item.ordId.ToString();
                        myTrade.NumberTrade = item.tradeId.ToString();

                        if (string.IsNullOrEmpty(item.fee))
                        {
                            myTrade.Volume = item.fillSz.ToDecimal();
                        }
                        else
                        {// there is a commission
                            if (item.instId.StartsWith(item.feeCcy))
                            { // the commission is taken in the traded currency, not in the exchange currency
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

                        MyTradeEvent(myTrade);
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

        private void UpdateOptionSummary(string message)
        {
            try
            {
                ResponseWsMessageAction<List<ResponseWsGreeks>> response = JsonConvert.DeserializeAnonymousType(message, new ResponseWsMessageAction<List<ResponseWsGreeks>>());

                if (response.data == null || response.data.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < response.data.Count; i++)
                {
                    ResponseWsGreeks greeks = response.data[i];

                    OptionMarketDataForConnector data = new OptionMarketDataForConnector();

                    data.SecurityName = greeks.instId;
                    data.UnderlyingAsset = greeks.uly;

                    data.Delta = greeks.delta;
                    data.Gamma = greeks.gamma;
                    data.Vega = greeks.vega;
                    data.Theta = greeks.theta;
                    data.TimeCreate = greeks.ts;
                    data.BidIV = greeks.bidVol;
                    data.AskIV = greeks.askVol;
                    data.MarkIV = greeks.markVol;

                    AdditionalOptionData additionalData;
                    if (_additionalOptionData.TryGetValue(greeks.instId, out additionalData))
                    {
                        data.OpenInterest = additionalData.OpenInterest;
                        data.MarkPrice = additionalData.MarkPrice;
                    }

                    string uprice;
                    if (_underlyingPrice.TryGetValue(greeks.uly, out uprice))
                    {
                        data.UnderlyingPrice = uprice;
                    }

                    //absend
                    //data.Rho = greeks.rho;

                    AdditionalMarketDataEvent(data);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                Thread.Sleep(5000);
            }
        }

        class AdditionalOptionData
        {
            public string MarkPrice;
            public string OpenInterest;
        }

        private ConcurrentDictionary<string, AdditionalOptionData> _additionalOptionData =
            new ConcurrentDictionary<string, AdditionalOptionData>();

        private ConcurrentDictionary<string, string> _underlyingPrice =
            new ConcurrentDictionary<string, string>();

        private void UpdateOpenInterest(string message)
        {
            try
            {
                ResponseWsMessageAction<List<ResponseWsOpenInterest>> response = JsonConvert.DeserializeAnonymousType(message, new ResponseWsMessageAction<List<ResponseWsOpenInterest>>());

                if (response.data == null || response.data.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < response.data.Count; i++)
                {
                    ResponseWsOpenInterest data = response.data[i];

                    if (!_additionalOptionData.ContainsKey(data.instId))
                    {
                        _additionalOptionData.TryAdd(data.instId, new AdditionalOptionData());
                    }

                    _additionalOptionData[data.instId].OpenInterest = data.oi;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                Thread.Sleep(5000);
            }
        }

        private void UpdateMarkPrice(string message)
        {
            try
            {
                ResponseWsMessageAction<List<ResponseWsMarkPrice>> response = JsonConvert.DeserializeAnonymousType(message, new ResponseWsMessageAction<List<ResponseWsMarkPrice>>());

                if (response.data == null || response.data.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < response.data.Count; i++)
                {
                    ResponseWsMarkPrice data = response.data[i];

                    if (data == null)
                    {
                        continue;
                    }

                    string name = data.instId;

                    if (data.instId.Contains("-SWAP"))
                    {
                        name = name.Replace("-SWAP", "");
                        _underlyingPrice[name] = data.markPx;
                    }
                    else
                    {
                        if (!_additionalOptionData.ContainsKey(data.instId))
                        {
                            _additionalOptionData.TryAdd(data.instId, new AdditionalOptionData());
                        }

                        _additionalOptionData[data.instId].MarkPrice = data.markPx;
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                Thread.Sleep(5000);
            }
        }


        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;

        #endregion

        #region 11 Trade

        private RateGate _rateGateOrder = new RateGate(3, TimeSpan.FromMilliseconds(100));

        public void SendOrder(Order order)
        {
            _rateGateOrder.WaitToProceed();

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

                HttpClient responseMessage = new HttpClient(new HttpInterceptor(_publicKey, _secretKey, _password, json, _demoMode, _myProxy));
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
                else
                {
                    //posSide = order.Side == Side.Buy ? "long" : "short";
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

                HttpClient responseMessage = new HttpClient(new HttpInterceptor(_publicKey, _secretKey, _password, json, _demoMode, _myProxy));
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
            _rateGateOrder.WaitToProceed();

            try
            {
                Dictionary<string, dynamic> orderRequest = new Dictionary<string, dynamic>();

                orderRequest.Add("instId", order.SecurityNameCode);
                orderRequest.Add("ordId", order.NumberMarket);

                string json = JsonConvert.SerializeObject(orderRequest);

                string url = $"{_baseUrl}/api/v5/trade/cancel-order";

                HttpClient responseMessage = new HttpClient(new HttpInterceptor(_publicKey, _secretKey, _password, json, _demoMode, _myProxy));
                HttpResponseMessage res = responseMessage.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json")).Result;
                string contentStr = res.Content.ReadAsStringAsync().Result;

                ResponseRestMessage<List<RestMessageSendOrder>> message = JsonConvert.DeserializeAnonymousType(contentStr, new ResponseRestMessage<List<RestMessageSendOrder>>());

                if (message.code.Equals("1"))
                {
                    GetOrderStatus(order);
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

        public void GetOrderStatus(Order order)
        {
            _rateGateOrder.WaitToProceed();

            try
            {
                string url;

                if (string.IsNullOrEmpty(order.NumberMarket))
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

                if (res.StatusCode == HttpStatusCode.OK)
                {
                    ResponseWsMessageAction<List<ResponseWsOrders>> OrderResponse = JsonConvert.DeserializeAnonymousType(contentStr, new ResponseWsMessageAction<List<ResponseWsOrders>>());

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
                else
                {
                    SendLogMessage($"GetOrderStatus - {contentStr}", LogMessageType.Error);
                    return;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"GetOrderStatus - {ex.Message}", LogMessageType.Error);
            }
        }

        private List<Order> GetActiveOrders()
        {
            _rateGateOrder.WaitToProceed();

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

        private RateGate _rateGateGenerateToTrade = new RateGate(1, TimeSpan.FromMilliseconds(200));

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
                {// there is a commission
                    if (item.instId.StartsWith(item.feeCcy))
                    { // the commission is taken in the traded currency, not in the exchange currency
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
            HttpClient _client = new HttpClient(new HttpInterceptor(_publicKey, _secretKey, _password, null, _demoMode, _myProxy));
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