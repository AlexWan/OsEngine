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
using OsEngine.Market.Servers.OKX.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;


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
            CreateParameterBoolean("Hedge Mode", true);
            ServerParameters[3].ValueChange += OkxServer_ValueChange;
            CreateParameterEnum("Margin Mode", "Cross", new List<string> { "Cross", "Isolated" });
            CreateParameterBoolean("Use Options", false);
            CreateParameterBoolean("Demo Mode", false);
            CreateParameterBoolean("Extended Data", false);

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
            ServerParameters[2].Comment = OsLocalization.Market.Label271;
            ServerParameters[3].Comment = OsLocalization.Market.Label250;
            ServerParameters[4].Comment = OsLocalization.Market.Label249;
            ServerParameters[5].Comment = OsLocalization.Market.Label253;
            ServerParameters[6].Comment = OsLocalization.Market.Label268;
            ServerParameters[7].Comment = OsLocalization.Market.Label252;
        }

        private void OkxServer_ValueChange()
        {
            ((OkxServerRealization)ServerRealization).HedgeMode = ((ServerParameterBool)ServerParameters[3]).Value;
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

        private WebProxy _myProxy;

        public void Connect(WebProxy proxy)
        {
            _myProxy = proxy;

            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
            _password = ((ServerParameterPassword)ServerParameters[2]).Value;
            HedgeMode = ((ServerParameterBool)ServerParameters[3]).Value;

            if (((ServerParameterEnum)ServerParameters[4]).Value == "Cross")
            {
                _marginMode = "cross";
            }
            else
            {
                _marginMode = "isolated";
            }

            _useOptions = ((ServerParameterBool)ServerParameters[5]).Value;

            if (((ServerParameterBool)ServerParameters[6]).Value == false)
            {
                _demoMode = false;
            }
            else
            {
                _demoMode = true;
            }

            if (((ServerParameterBool)ServerParameters[7]).Value == true)
            {
                _extendedMarketData = true;
            }
            else
            {
                _extendedMarketData = false;
            }

            try
            {
                RestRequest requestRest = new RestRequest("/api/v5/public/time", Method.GET);
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

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
                CreatePublicWebSocketConnect();
                CreatePrivateWebSocketConnect();
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
            get { return ServerType.OKX; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public DateTime ServerTime { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

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

        private bool _useOptions;

        private bool _demoMode;

        private bool _extendedMarketData;

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

                SecurityResponse securityResponseFuturesContracts = GetFuturesContractsSecurities();
                if (securityResponseFuturesContracts != null && securityResponseFuturesContracts.data != null)
                {
                    securityResponseFutures.data.AddRange(securityResponseFuturesContracts.data);
                }

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
                RestRequest requestRest = new RestRequest("/api/v5/public/instruments?instType=SWAP", Method.GET);
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"GetFuturesSecurities - {response.Content}", LogMessageType.Error);
                }

                SecurityResponse securityResponse = JsonConvert.DeserializeAnonymousType(response.Content, new SecurityResponse());

                return securityResponse;
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        private SecurityResponse GetFuturesContractsSecurities()
        {
            try
            {
                RestRequest requestRest = new RestRequest("/api/v5/public/instruments?instType=FUTURES", Method.GET);
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"GetFuturesContractsSecurities - {response.Content}", LogMessageType.Error);
                }

                SecurityResponse securityResponse = JsonConvert.DeserializeAnonymousType(response.Content, new SecurityResponse());

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
                RestRequest requestRest = new RestRequest("/api/v5/public/underlying?instType=OPTION", Method.GET);
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"GetOptionSecurities - {response.Content}", LogMessageType.Error);
                }

                SecurityUnderlyingResponse baseSecuritiesResponse = JsonConvert.DeserializeAnonymousType(response.Content, new SecurityUnderlyingResponse());

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

                    RestRequest requestRest = new RestRequest("/api/v5/public/instruments?instType=OPTION&uly=" + baseSecurity, Method.GET);
                    RestClient client = new RestClient(_baseUrl);

                    if (_myProxy != null)
                    {
                        client.Proxy = _myProxy;
                    }

                    IRestResponse response = client.Execute(requestRest);

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        SendLogMessage($"GetOptionSecurities - {response.Content}", LogMessageType.Error);
                    }

                    SecurityResponse securityResponse = JsonConvert.DeserializeAnonymousType(response.Content, new SecurityResponse());

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
                RestRequest requestRest = new RestRequest("/api/v5/public/instruments?instType=SPOT", Method.GET);
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"GetSpotSecurities - {response.Content}", LogMessageType.Error);
                }

                SecurityResponse securityResponse = JsonConvert.DeserializeAnonymousType(response.Content, new SecurityResponse());

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

                if (item.instType.Equals("SWAP") || item.instType.Equals("FUTURES"))
                {
                    securityType = SecurityType.Futures;
                }
                else if (item.instType.Equals("OPTION"))
                {
                    securityType = SecurityType.Option;
                }

                security.Name = item.instId;
                security.NameFull = item.instId;
                security.NameId = item.instId;

                if (item.lotSz == string.Empty)
                {
                    continue;
                }

                security.Lot = 1;
                string volStep = item.minSz.Replace(',', '.');

                if (volStep != null
                        && volStep.Length > 0 &&
                        volStep.Split('.').Length > 1)
                {
                    security.DecimalsVolume = volStep.Split('.')[1].Length;
                }

                security.MinTradeAmountType = MinTradeAmountType.Contract;
                security.MinTradeAmount = item.minSz.ToDecimal();
                security.VolumeStep = item.minSz.ToDecimal();

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
                    else if (item.instId.Contains("-USDT-"))
                    {
                        security.NameClass = "Futures_USDT";
                    }
                    else
                    {
                        security.NameClass = "SWAP_" + item.settleCcy;
                    }

                    security.NameId = item.instId + "_" + item.ctVal.ToDecimal();
                    security.MinTradeAmount = item.minSz.ToDecimal() * item.ctVal.ToDecimal();
                    security.VolumeStep = item.lotSz.ToDecimal() * item.ctVal.ToDecimal();
                    security.DecimalsVolume = security.MinTradeAmount.ToString().DecimalsCount();
                    security.UnderlyingAsset = item.uly;

                    if (item.expTime != "")
                        security.Expiration = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.expTime));
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

                    //security.Lot = item.ctVal.ToDecimal();
                    security.Expiration = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.expTime));
                    security.OptionType = item.optType == "P" ? OptionType.Put : OptionType.Call;
                    security.Strike = item.stk.ToDecimal();

                    string baseName = item.uly + "T"; // example: BTC-USD -> BTC-USDT

                    // 1. Find all futures that are true quarterly futures (expire on last Friday of Mar, Jun, Sep, Dec)
                    var quarterlyFutures = _securities
                        .Where(s => s.SecurityType == SecurityType.Futures &&
                                    s.Name.StartsWith(baseName) &&
                                    s.Expiration != DateTime.MinValue &&
                                    (s.Expiration.Month == 3 || s.Expiration.Month == 6 || s.Expiration.Month == 9 || s.Expiration.Month == 12) &&
                                    s.Expiration.DayOfWeek == DayOfWeek.Friday &&
                       s.Expiration.AddDays(7).Month != s.Expiration.Month)
                        .ToList();

                    if (quarterlyFutures.Any())
                    {
                        // 2. Find the first quarterly future that expires AFTER the option expires.
                        var nextFuture = quarterlyFutures
                            .Where(f => f.Expiration >= security.Expiration)
                            .OrderBy(f => f.Expiration)
                            .FirstOrDefault();

                        if (nextFuture != null)
                        {
                            security.UnderlyingAsset = nextFuture.Name;
                        }
                        else
                        {
                            // 3. Fallback: If no future expires after the option, take the one with the latest expiration date available.
                            var latestFuture = quarterlyFutures
                                .OrderByDescending(f => f.Expiration)
                                .FirstOrDefault();

                            if (latestFuture != null)
                            {
                                security.UnderlyingAsset = latestFuture.Name;
                            }
                            else
                            {
                                security.UnderlyingAsset = item.uly;
                            }
                        }
                    }
                    else
                    {
                        // 4. Fallback: No quarterly futures found at all for this underlying.
                        security.UnderlyingAsset = item.uly;
                    }
                }

                security.Exchange = ServerType.OKX.ToString();
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
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
            DateTime endTime = DateTime.UtcNow;
            DateTime startTime = endTime.AddMinutes(-tfTotalMinutes * candleCount);

            return GetCandleData(security, timeFrameBuilder, startTime, endTime, endTime, false);
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return GetCandleData(security, timeFrameBuilder, startTime, endTime, actualTime, true);
        }

        public List<Candle> GetCandleData(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime, bool isOsData)
        {
            startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
            endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
            actualTime = DateTime.SpecifyKind(actualTime, DateTimeKind.Utc);

            if (startTime < DateTime.UtcNow.AddMonths(-3))
            {
                SendLogMessage("History more than 3 months is not supported by Api", LogMessageType.Error);
                return null;
            }

            if (!CheckTime(startTime, endTime, actualTime))
            {
                return null;
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

            int CountCandlesNeedToLoad = GetCountCandlesFromTimeInterval(startTime, endTime, timeFrameBuilder.TimeFrameTimeSpan);

            List<Candle> candles = GetCandleDataHistory(security.Name, timeFrameBuilder.TimeFrameTimeSpan, CountCandlesNeedToLoad, TimeManager.GetTimeStampMilliSecondsToDateTime(endTime), isOsData);

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

        public List<Candle> GetCandleDataHistory(string nameSec, TimeSpan tf, int NumberCandlesToLoad, long DataEnd, bool isOsData)
        {
            CandlesResponse securityResponse = GetResponseDataCandles(nameSec, tf, NumberCandlesToLoad, DataEnd, isOsData);

            List<Candle> candles = new List<Candle>();

            ConvertCandles(securityResponse, candles);

            candles.Reverse();

            return candles;
        }

        private CandlesResponse GetResponseDataCandles(string nameSec, TimeSpan tf, int NumberCandlesToLoad, long DataEnd, bool isOsData)
        {
            _rateGateCandles.WaitToProceed();

            try
            {
                string bar = GetStringBar(tf);

                CandlesResponse candlesResponse = new CandlesResponse();
                candlesResponse.data = new List<List<string>>();

                do
                {
                    _rateGateCandles.WaitToProceed();

                    int limit = NumberCandlesToLoad;

                    if (NumberCandlesToLoad > 300)
                    {
                        limit = 300;
                    }

                    string after = $"&after={Convert.ToString(DataEnd)}";

                    if (candlesResponse.data.Count != 0)
                    {
                        after = $"&after={candlesResponse.data[candlesResponse.data.Count - 1][0]}";
                    }

                    string url = _baseUrl + $"/api/v5/market/candles?instId={nameSec}&bar={bar}&limit={limit}" + after;

                    if (isOsData)
                    {
                        url = _baseUrl + $"/api/v5/market/candles?instId={nameSec}&bar={bar}&limit={limit}" + after;
                    }

                    RestClient client = new RestClient(url);
                    RestRequest request = new RestRequest(Method.GET);
                    IRestResponse Response = client.Execute(request);

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
                    return $"{tf.Days}Dutc";
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
            startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
            endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
            actualTime = DateTime.SpecifyKind(actualTime, DateTimeKind.Utc);

            if (startTime < DateTime.UtcNow.AddMonths(-3))
            {
                SendLogMessage("History more than 3 months is not supported by Api", LogMessageType.Error);
                return null;
            }

            if (!CheckTime(startTime, endTime, actualTime))
            {
                return null;
            }

            List<Trade> trades = new List<Trade>();

            List<Trade> newTrades = GetTickHistoryToSecurity(security.Name, endTime);

            if (newTrades == null ||
                    newTrades.Count == 0)
            {
                return null;
            }

            trades.AddRange(newTrades);
            DateTime timeEnd = DateTime.SpecifyKind(trades[0].Time, DateTimeKind.Utc);

            while (timeEnd > startTime)
            {
                newTrades = GetTickHistoryToSecurity(security.Name, timeEnd);

                if (newTrades != null && trades.Count != 0 && newTrades.Count != 0)
                {
                    for (int j = 0; j < trades.Count; j++)
                    {
                        for (int i = 0; i < newTrades.Count; i++)
                        {
                            if (trades[j].Id == newTrades[i].Id)
                            {
                                newTrades.RemoveAt(i);
                                i--;
                            }
                        }
                    }
                }

                if (newTrades.Count == 0)
                {
                    break;
                }

                trades.InsertRange(0, newTrades);
                timeEnd = DateTime.SpecifyKind(trades[0].Time, DateTimeKind.Utc);
            }

            if (trades.Count == 0)
            {
                return null;
            }

            for (int i = trades.Count - 1; i >= 0; i--)
            {
                if (DateTime.SpecifyKind(trades[i].Time, DateTimeKind.Utc) <= endTime)
                {
                    break;
                }
                else
                {
                    trades.RemoveAt(i);
                }
            }

            return trades;
        }

        private List<Trade> GetTickHistoryToSecurity(string securityName, DateTime endTime)
        {
            _rateGateCandles.WaitToProceed();

            try
            {
                List<Trade> trades = new List<Trade>();

                long timeEnd = TimeManager.GetTimeStampMilliSecondsToDateTime(endTime);

                string url = _baseUrl + $"/api/v5/market/history-trades?instId={securityName}&type=2&after={timeEnd}&limit=100";

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    TradesDataResponse tradesResponse = JsonConvert.DeserializeAnonymousType(response.Content, new TradesDataResponse());

                    if (tradesResponse.code == "0")
                    {
                        for (int i = 0; i < tradesResponse.data.Count; i++)
                        {
                            TradeData item = tradesResponse.data[i];

                            Trade trade = new Trade();
                            trade.SecurityNameCode = item.instId;
                            trade.Id = item.tradeId;
                            trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts));
                            trade.Price = item.px.ToDecimal();
                            trade.Volume = item.sz.ToDecimal(); //For spot trading, the unit is base currency
                                                                //For FUTURES / SWAP / OPTION, the unit is contract.

                            trade.Side = item.side == "Sell" ? Side.Sell : Side.Buy;
                            trades.Add(trade);
                        }

                        trades.Reverse();
                        return trades;
                    }
                    else
                    {
                        SendLogMessage($"Trades request error: {tradesResponse.code} - {tradesResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Trades request error: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception error)
            {
                SendLogMessage($"Trades request error: {error.Message} {error.StackTrace}", LogMessageType.Error);
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
                    webSocketPublicNew.SetProxy(_myProxy);
                }

                webSocketPublicNew.EmitOnPing = true;
                webSocketPublicNew.OnOpen += WebSocketPublic_Opened;
                webSocketPublicNew.OnClose += WebSocketPublic_Closed;
                webSocketPublicNew.OnMessage += WebSocketPublic_MessageReceived;
                webSocketPublicNew.OnError += WebSocketPublic_Error;
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
                    _webSocketPrivate.SetProxy(_myProxy);
                }


                _webSocketPrivate.EmitOnPing = true;
                _webSocketPrivate.OnOpen += WebSocketPrivate_Opened;
                _webSocketPrivate.OnClose += WebSocketPrivate_Closed;
                _webSocketPrivate.OnMessage += WebSocketPrivate_MessageReceived;
                _webSocketPrivate.OnError += WebSocketPrivate_Error;
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
                        if (ConnectEvent != null)
                        {
                            ConnectEvent();
                        }

                        SetPositionMode();
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
                _webSocketPrivate.SendAsync(Encryptor.MakeAuthRequest(_publicKey, _secretKey, _password));
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private void SetPositionMode()
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            Dictionary<string, string> dict = new Dictionary<string, string>();

            dict["posMode"] = "net_mode";

            if (HedgeMode)
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
            else if (message.msg == "API key doesn't exist")
            {
                SendLogMessage($"PushPositionMode - {contentStr}", LogMessageType.Error);
                Disconnect();
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

        private void WebSocketPublic_Closed(object sender, CloseEventArgs e)
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

        private void WebSocketPublic_Error(object sender, ErrorEventArgs e)
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

        private void WebSocketPrivate_Opened(object sender, EventArgs e)
        {
            try
            {
                CreateAuthMessageWebSockets();
                SendLogMessage("OKX WebSocket Private connection open", LogMessageType.System);
                CheckSocketsActivate();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPrivate_Closed(object sender, CloseEventArgs e)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    string message = this.GetType().Name + OsLocalization.Market.Message101 + "\n";
                    message += OsLocalization.Market.Message102;
                    message += $"Server: {e.Code} {e.Reason}";

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

                if (e.Data.Contains("error"))
                {
                    SendLogMessage("Error received from server: "+ e.Data.ToString(), LogMessageType.Error);
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

        private void WebSocketPrivate_Error(object sender, ErrorEventArgs e)
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
                            && webSocketPublic?.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublic.SendAsync("ping");
                        }
                        else
                        {
                            Disconnect();
                        }
                    }

                    if (_webSocketPrivate != null &&
                    (_webSocketPrivate.ReadyState == WebSocketState.Open))
                    {
                        _webSocketPrivate.SendAsync("ping");
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

        public void Subscribe(Security security)
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
                    webSocketPublic.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"channel\": \"books5\",\"instId\": \"{security.Name}\"}}]}}");
                    webSocketPublic.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"channel\": \"trades\",\"instId\": \"{security.Name}\"}}]}}");

                    if (_extendedMarketData)
                    {
                        webSocketPublic.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"channel\": \"tickers\",\"instId\": \"{security.Name}\"}}]}}");

                        if (security.Name.Contains("SWAP"))
                        {
                            webSocketPublic.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"channel\": \"open-interest\",\"instId\": \"{security.Name}\"}}]}}");
                            webSocketPublic.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"channel\": \"funding-rate\",\"instId\": \"{security.Name}\"}}]}}");
                            GetFundingHistory(security.Name);
                        }
                    }
                }

                if (_useOptions && security.SecurityType == SecurityType.Option)
                {
                    _subscribedSecurities.Add(securityName, true);

                    _rateGateSubscribe.WaitToProceed();

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

        private RateGate _rateGateFundingHistory = new RateGate(10, TimeSpan.FromMilliseconds(2000));

        private void GetFundingHistory(string securityName)
        {
            _rateGateFundingHistory.WaitToProceed();

            try
            {
                string url = _baseUrl + $"/api/v5/public/funding-rate-history?instId={securityName}";

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<FundingItemHistory>> responseFunding = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<FundingItemHistory>>());

                    if (responseFunding.code == "0")
                    {
                        FundingItemHistory item = responseFunding.data[0];

                        Funding data = new Funding();

                        data.SecurityNameCode = item.instId;
                        data.PreviousFundingTime = TimeManager.GetDateTimeFromTimeStamp((long)item.fundingTime.ToDecimal());

                        FundingUpdateEvent?.Invoke(data);
                    }
                    else
                    {
                        SendLogMessage($"GetFundingHistory error. Code:{responseFunding.code} || msg: {responseFunding.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"GetFundingHistory error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception error)
            {
                SendLogMessage($"GetFundingHistory error. {error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        public void SubscribeOptionSummary(string securityName, WebSocket webSocketPublic)
        {
            RequestSubscribe<SubscribeArgsOption> requestTrade = new RequestSubscribe<SubscribeArgsOption>();
            requestTrade.args = new List<SubscribeArgsOption>() { new SubscribeArgsOption() };
            requestTrade.args[0].channel = "opt-summary";
            requestTrade.args[0].instFamily = securityName; //"BTC-USD"

            string json = JsonConvert.SerializeObject(requestTrade);
            webSocketPublic.SendAsync(json);
        }

        public void SubscribeMarkPrice(string name, WebSocket webSocketPublic)
        {
            RequestSubscribe<SubscribeArgs> requestTrade = new RequestSubscribe<SubscribeArgs>();
            requestTrade.args = new List<SubscribeArgs>() { new SubscribeArgs() };
            requestTrade.args[0].channel = "mark-price";
            requestTrade.args[0].instId = name; //"LTC-USD-SWAP"

            string json = JsonConvert.SerializeObject(requestTrade);
            webSocketPublic.SendAsync(json);
        }

        private void SubscribePrivate()
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                _webSocketPrivate.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"channel\": \"account\"}}]}}");
                _webSocketPrivate.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"channel\": \"positions\",\"instType\": \"ANY\"}}]}}");
                _webSocketPrivate.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"channel\": \"orders\",\"instType\": \"ANY\"}}]}}");
                //_webSocketPrivate.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"channel\": \"fills\"}}]}}");
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
                                    webSocketPublic.SendAsync($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"books5\",\"instId\": \"{name}\"}}]}}");
                                    webSocketPublic.SendAsync($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"trade\",\"instId\": \"{name}\"}}]}}");

                                    if (_extendedMarketData)
                                    {
                                        webSocketPublic.SendAsync($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"tickers\",\"instId\": \"{name}\"}}]}}");

                                        if (name.Contains("SWAP"))
                                        {
                                            webSocketPublic.SendAsync($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"open-interest\",\"instId\": \"{name}\"}}]}}");
                                            webSocketPublic.SendAsync($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"funding-rate\",\"instId\": \"{name}\"}}]}}");
                                        }
                                    }

                                    if (item.Value)
                                    {
                                        //option
                                        webSocketPublic.SendAsync($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"mark-price\",\"instId\": \"{name}\"}}]}}");
                                    }
                                }
                            }

                            if (_baseOptionSerurities != null)
                            {
                                foreach (string name in _baseOptionSerurities)
                                {
                                    webSocketPublic.SendAsync($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"opt-summary\",\"instFamily\": \"{name}\"}}]}}");
                                    webSocketPublic.SendAsync($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"mark-price\",\"instId\": \"{name}-SWAP\"}}]}}");
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
                    _webSocketPrivate.SendAsync($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"account\"}}]}}");
                    _webSocketPrivate.SendAsync($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"positions\",\"instType\": \"ANY\"}}]}}");
                    _webSocketPrivate.SendAsync($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"orders\",\"instType\": \"ANY\"}}]}}");
                    //_webSocketPrivate.SendAsync($"{{\"op\": \"unsubscribe\",\"args\": [{{\"channel\": \"fills\"}}]}}");
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
                        //SendLogMessage("[WS Public] Got subscribe msg: " + action.msg, LogMessageType.System);
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

                        if (action.arg.channel.Equals("funding-rate"))
                        {
                            UpdateFundingRate(message);
                            continue;
                        }

                        if (action.arg.channel.Equals("tickers"))
                        {
                            UpdateTickers(message);
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
                                    pos.ValueCurrent = Math.Round(GetAvailPos(item.pos) * GetVolume(item.instId), 6);
                                    pos.ValueBlocked = 0;
                                    pos.UnrealizedPnl = Math.Round(GetAvailPos(item.upl), 6);
                                }
                                else if (item.posSide.Contains("short"))
                                {
                                    pos.SecurityNameCode = item.instId + "_SHORT";
                                    pos.ValueCurrent = -Math.Round(GetAvailPos(item.pos) * GetVolume(item.instId), 6);
                                    pos.ValueBlocked = 0;
                                    pos.UnrealizedPnl = Math.Round(GetAvailPos(item.upl), 6);
                                }
                                else if (item.posSide.Contains("net"))
                                {
                                    pos.SecurityNameCode = item.instId;
                                    pos.ValueCurrent = Math.Round(GetAvailPos(item.pos) * GetVolume(item.instId), 6);
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

                if (assets.data == null ||
                    assets.data.Count == 0)
                {
                    return;
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
                    double ask = responseDepth.data[0].asks[i][1].ToString().ToDouble();
                    double price = responseDepth.data[0].asks[i][0].ToString().ToDouble();

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
                    double bid = responseDepth.data[0].bids[i][1].ToString().ToDouble();
                    double price = responseDepth.data[0].bids[i][0].ToString().ToDouble();

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

                if (_extendedMarketData && trade.SecurityNameCode.Contains("SWAP"))
                {
                    trade.OpenInterest = GetOpenInterest(trade.SecurityNameCode);
                }

                NewTradesEvent?.Invoke(trade);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private decimal GetOpenInterest(string securityNameCode)
        {
            if (_additionalOptionData == null
                || _additionalOptionData.Count == 0)
            {
                return 0;
            }

            foreach (var optionData in _additionalOptionData)
            {
                if (optionData.Key == securityNameCode)
                {
                    return optionData.Value.OpenInterest.ToDecimal();
                }
            }

            return 0;
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

                    if (newOrder.State == OrderStateType.Partial
                        /*|| newOrder.State == OrderStateType.Done*/)
                    {
                        ResponseWsOrders item = OrderResponse.data[i];

                        MyTrade myTrade = new MyTrade();

                        myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.cTime));
                        myTrade.SecurityNameCode = item.instId;
                        myTrade.NumberOrderParent = item.ordId.ToString();
                        myTrade.NumberTrade = item.tradeId.ToString();

                        if (item.instId.Contains("SWAP"))
                        {
                            if (string.IsNullOrEmpty(item.fee))
                            {
                                myTrade.Volume = item.fillSz.ToDecimal() * GetVolume(item.instId);
                            }
                            else
                            {// there is a commission
                                if (item.instId.StartsWith(item.feeCcy))
                                { // the commission is taken in the traded currency, not in the exchange currency
                                    myTrade.Volume = item.fillSz.ToDecimal() * GetVolume(item.instId) + item.fee.ToDecimal();
                                }
                                else
                                {
                                    myTrade.Volume = item.fillSz.ToDecimal() * GetVolume(item.instId);
                                }
                            }
                        }
                        else
                        {
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
                        }

                        if (!item.fillPx.Equals(String.Empty))
                        {
                            myTrade.Price = item.fillPx.ToDecimal();
                        }

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
            newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.cTime));
            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.uTime));

            if (newOrder.State == OrderStateType.Done)
            {
                newOrder.TimeDone = newOrder.TimeCallBack;
            }
            else if (newOrder.State == OrderStateType.Cancel)
            {
                newOrder.TimeCancel = newOrder.TimeCallBack;
            }

            try
            {
                newOrder.NumberUser = Convert.ToInt32(item.clOrdId);
            }
            catch
            {

            }

            newOrder.NumberMarket = item.ordId.ToString();
            newOrder.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;

            if (item.instId.Contains("SWAP"))
            {
                newOrder.Volume = item.sz.ToDecimal() * GetVolume(item.instId);
            }
            else
            {
                newOrder.Volume = item.sz.ToDecimal();
            }

            newOrder.PortfolioNumber = "OKX";

            if (string.IsNullOrEmpty(item.px) == false
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

        private ConcurrentDictionary<string, AdditionalOptionData> _additionalOptionData = new ConcurrentDictionary<string, AdditionalOptionData>();

        private ConcurrentDictionary<string, string> _underlyingPrice = new ConcurrentDictionary<string, string>();

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

        private void UpdateFundingRate(string message)
        {
            try
            {
                ResponseWsMessageAction<List<FundingItem>> response = JsonConvert.DeserializeAnonymousType(message, new ResponseWsMessageAction<List<FundingItem>>());

                if (response.data == null || response.data.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < response.data.Count; i++)
                {
                    FundingItem item = response.data[i];

                    Funding funding = new Funding();

                    funding.SecurityNameCode = item.instId;
                    funding.CurrentValue = item.fundingRate.ToDecimal() * 100;
                    funding.NextFundingTime = TimeManager.GetDateTimeFromTimeStamp((long)item.fundingTime.ToDecimal());
                    funding.TimeUpdate = TimeManager.GetDateTimeFromTimeStamp((long)item.ts.ToDecimal());
                    funding.MinFundingRate = item.minFundingRate.ToDecimal();
                    funding.MaxFundingRate = item.maxFundingRate.ToDecimal();
                    TimeSpan data = TimeManager.GetDateTimeFromTimeStamp((long)item.nextFundingTime.ToDecimal()) - TimeManager.GetDateTimeFromTimeStamp((long)item.fundingTime.ToDecimal());
                    funding.FundingIntervalHours = int.Parse(data.Hours.ToString());

                    FundingUpdateEvent?.Invoke(funding);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                Thread.Sleep(5000);
            }
        }

        private void UpdateTickers(string message)
        {
            try
            {
                ResponseWsMessageAction<List<TickerItem>> response = JsonConvert.DeserializeAnonymousType(message, new ResponseWsMessageAction<List<TickerItem>>());

                if (response.data == null || response.data.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < response.data.Count; i++)
                {
                    TickerItem item = response.data[i];

                    SecurityVolumes volume = new SecurityVolumes();

                    volume.SecurityNameCode = item.instId;
                    volume.Volume24h = item.vol24h.ToDecimal();
                    volume.Volume24hUSDT = item.volCcy24h.ToDecimal();
                    volume.TimeUpdate = TimeManager.GetDateTimeFromTimeStamp((long)item.ts.ToDecimal());

                    Volume24hUpdateEvent?.Invoke(volume);
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

        public event Action<Funding> FundingUpdateEvent;

        public event Action<SecurityVolumes> Volume24hUpdateEvent;

        #endregion

        #region 11 Trade

        private RateGate _rateGateOrder = new RateGate(3, TimeSpan.FromMilliseconds(80));

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
                orderRequest.Add("ordType", order.TypeOrder.ToString().ToLower());

                if (order.TypeOrder == OrderPriceType.Limit)
                {
                    orderRequest.Add("px", order.Price.ToString().Replace(",", "."));
                    orderRequest.Add("sz", order.Volume.ToString().Replace(",", "."));
                }
                else if (order.TypeOrder == OrderPriceType.Market)
                {
                    orderRequest.Add("tgtCcy", "base_ccy");
                    orderRequest.Add("sz", order.Volume.ToString().Replace(",", "."));
                }

                orderRequest.Add("tag", "5faf8b0e85c1BCDE");

                string json = JsonConvert.SerializeObject(orderRequest);

                string url = $"{_baseUrl}/api/v5/trade/order";

                HttpClient responseMessage = new HttpClient(new HttpInterceptor(_publicKey, _secretKey, _password, json, _demoMode, _myProxy));
                HttpResponseMessage res = responseMessage.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json")).Result;
                string contentStr = res.Content.ReadAsStringAsync().Result;

                ResponseRestMessage<List<RestMessageSendOrder>> message = JsonConvert.DeserializeAnonymousType(contentStr, new ResponseRestMessage<List<RestMessageSendOrder>>());

                if (res.StatusCode == HttpStatusCode.OK)
                {
                    if (message.code.Equals("1"))
                    {
                        CreateOrderFail(order);
                        SendLogMessage($"SendOrderSpot - {message.data[0].sMsg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Spot Order Fail. Status: {res.StatusCode} || {contentStr}", LogMessageType.Error);
                    CreateOrderFail(order);
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

                if (HedgeMode)
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

                decimal volume = order.Volume / GetVolume(order.SecurityNameCode);
                orderRequest.Add("sz", volume.ToString().Replace(",", "."));
                orderRequest.Add("posSide", posSide);
                orderRequest.Add("tag", "5faf8b0e85c1BCDE");

                string json = JsonConvert.SerializeObject(orderRequest);

                string url = $"{_baseUrl}/api/v5/trade/order";

                HttpClient responseMessage = new HttpClient(new HttpInterceptor(_publicKey, _secretKey, _password, json, _demoMode, _myProxy));
                HttpResponseMessage res = responseMessage.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json")).Result;
                string contentStr = res.Content.ReadAsStringAsync().Result;

                if (res.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<RestMessageSendOrder>> message = JsonConvert.DeserializeAnonymousType(contentStr, new ResponseRestMessage<List<RestMessageSendOrder>>());

                    if (message.code.Equals("1"))
                    {
                        CreateOrderFail(order);
                        SendLogMessage($"SendOrderSwap - {message.data[0].sMsg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Swap Order Fail. Status: {res.StatusCode} || {contentStr}", LogMessageType.Error);
                    CreateOrderFail(order);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"SendOrderSwap - {ex.Message}", LogMessageType.Error);
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

                if (res.StatusCode == HttpStatusCode.OK)
                {
                    if (message.code.Equals("0"))
                    {
                        return true;
                    }
                    else
                    {
                        OrderStateType state = GetOrderStatus(order);

                        if (state == OrderStateType.None)
                        {
                            SendLogMessage($"Cancel Order Error. {order.NumberUser} || {contentStr}.", LogMessageType.Error);
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
                        SendLogMessage($"Cancel order failed. Status: {res.StatusCode} || {contentStr}", LogMessageType.Error);
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
                SendLogMessage($"CancelOrder - {ex.Message}", LogMessageType.Error);
            }
            return false;
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
        }

        public void CancelAllOrders()
        {
            List<Order> orders = GetAllActivOrdersArray(100);

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
            List<Order> orders = GetAllActivOrdersArray(100);

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

        private List<Order> GetAllActivOrdersArray(int maxCountByCategory)
        {
            List<Order> ordersOpenAll = new List<Order>();

            List<Order> orders = new List<Order>();

            GetAllOpenOrders(orders, 100);

            if (orders != null
                && orders.Count > 0)
            {
                ordersOpenAll.AddRange(orders);
            }

            return ordersOpenAll;
        }

        private List<Order> _activeOrdersCash = new List<Order>();
        private List<Order> _historicalOrdersCash = new List<Order>();
        private DateTime _timeOrdersCashCreate;

        public OrderStateType GetOrderStatus(Order order)
        {
            try
            {
                if (_timeOrdersCashCreate.AddSeconds(2) < DateTime.Now)
                {
                    // We update order arrays once every two seconds.
                    // We are creating a cache for mass requesting statuses on reconnection.
                    _historicalOrdersCash = GetHistoricalOrders(0, 100);
                    _activeOrdersCash = GetActiveOrders(0, 100);
                    _timeOrdersCashCreate = DateTime.Now;
                }

                Order myOrder = null;

                for (int i = 0; _historicalOrdersCash != null && i < _historicalOrdersCash.Count; i++)
                {
                    if (_historicalOrdersCash[i].NumberUser == order.NumberUser)
                    {
                        myOrder = _historicalOrdersCash[i];
                        break;
                    }
                }

                if (myOrder == null)
                {
                    for (int i = 0; _activeOrdersCash != null && i < _activeOrdersCash.Count; i++)
                    {
                        if (_activeOrdersCash[i].NumberUser == order.NumberUser)
                        {
                            myOrder = _activeOrdersCash[i];
                            break;
                        }
                    }
                }

                if (myOrder == null)
                {
                    return OrderStateType.None;
                }

                MyOrderEvent?.Invoke(myOrder);

                // check trades

                if (myOrder.State == OrderStateType.Partial
                    || myOrder.State == OrderStateType.Done)
                {
                    List<MyTrade> tradesInOrder = GetMyTradesBySecurity(myOrder, 1);

                    for (int i2 = 0; tradesInOrder != null && i2 < tradesInOrder.Count; i2++)
                    {
                        MyTradeEvent(tradesInOrder[i2]);
                    }
                }

                return myOrder.State;
            }
            catch (Exception ex)
            {
                SendLogMessage($"GetOrderStatus>. Order error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }

            return OrderStateType.None;
        }

        private void GetAllOpenOrders(List<Order> array, int maxCount)
        {
            _rateGateOrder.WaitToProceed();

            try
            {
                string url = $"{_baseUrl}/api/v5/trade/orders-pending";
                HttpResponseMessage res = GetPrivateRequest(url);
                string contentStr = res.Content.ReadAsStringAsync().Result;

                if (res.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<ResponseWsOrders>> OrderResponse = JsonConvert.DeserializeAnonymousType(contentStr, new ResponseRestMessage<List<ResponseWsOrders>>());

                    if (OrderResponse.code.Equals("0"))
                    {
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

                        if (orders.Count > 0)
                        {
                            array.AddRange(orders);

                            if (array.Count > maxCount)
                            {
                                while (array.Count > maxCount)
                                {
                                    array.RemoveAt(array.Count - 1);
                                }
                                return;
                            }
                            else if (array.Count < 100)
                            {
                                return;
                            }
                        }
                        else
                        {
                            return;
                        }

                        return;
                    }
                    else
                    {
                        SendLogMessage($"Get all open orders failed: {OrderResponse.code} || msg: {OrderResponse.msg}", LogMessageType.Error);
                        return;
                    }
                }
                else
                {
                    SendLogMessage($"Get all open orders request error {res.StatusCode} || {contentStr}", LogMessageType.Error);
                    return;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"GetActivOrders - {ex.Message}", LogMessageType.Error);
                return;
            }
        }

        private RateGate _rateGateGenerateToTrade = new RateGate(1, TimeSpan.FromMilliseconds(200));

        private List<MyTrade> GetMyTradesBySecurity(Order order, int CountOfCalls)
        {
            try
            {
                _rateGateGenerateToTrade.WaitToProceed();

                string TypeInstr = order.SecurityNameCode.EndsWith("SWAP") ? "SWAP" : "SPOT";

                string url = $"{_baseUrl}/api/v5/trade/fills-history?ordId={order.NumberMarket}&instId={order.SecurityNameCode}&instType={TypeInstr}";

                HttpResponseMessage res = GetPrivateRequest(url);

                string contentStr = res.Content.ReadAsStringAsync().Result;

                if (res.StatusCode == HttpStatusCode.OK)
                {
                    TradeDetailsResponse quotes = JsonConvert.DeserializeAnonymousType(contentStr, new TradeDetailsResponse());

                    if (quotes.code.Equals("0"))
                    {
                        List<MyTrade> myTrades = new List<MyTrade>();

                        for (int i = 0; i < quotes.data.Count; i++)
                        {
                            TradeDetailsObject item = quotes.data[i];

                            MyTrade myTrade = new MyTrade();

                            myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts));
                            myTrade.SecurityNameCode = item.instId;
                            myTrade.NumberOrderParent = item.ordId.ToString();
                            myTrade.NumberTrade = item.tradeId.ToString();

                            if (item.instId.Contains("SWAP"))
                            {
                                if (string.IsNullOrEmpty(item.fee))
                                {
                                    myTrade.Volume = item.fillSz.ToDecimal() * GetVolume(item.instId);
                                }
                                else
                                {// there is a commission
                                    if (item.instId.StartsWith(item.feeCcy))
                                    { // the commission is taken in the traded currency, not in the exchange currency
                                        myTrade.Volume = item.fillSz.ToDecimal() * GetVolume(item.instId) + item.fee.ToDecimal();
                                    }
                                    else
                                    {
                                        myTrade.Volume = item.fillSz.ToDecimal() * GetVolume(item.instId);
                                    }
                                }
                            }
                            else
                            {
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
                            }

                            if (!item.fillPx.Equals(String.Empty))
                            {
                                myTrade.Price = item.fillPx.ToDecimal();
                            }

                            myTrade.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;

                            myTrades.Add(myTrade);
                        }

                        return myTrades;
                    }
                    else
                    {
                        SendLogMessage($"Get my trades by security error: {quotes.code} || {quotes.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Get my trades by security error: {res.StatusCode} || {contentStr}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"GenerateTradesToOrder - {ex.Message}", LogMessageType.Error);

            }
            return null;
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            int countToMethod = startIndex + count;

            List<Order> result = GetAllActivOrdersArray(countToMethod);

            List<Order> resultExit = new List<Order>();

            if (result != null
                && startIndex < result.Count)
            {
                if (startIndex + count < result.Count)
                {
                    resultExit = result.GetRange(startIndex, count);
                }
                else
                {
                    resultExit = result.GetRange(startIndex, result.Count - startIndex);
                }
            }

            return resultExit;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            int countToMethod = startIndex + count;

            List<Order> result = GetAllHistoricalOrdersArray(countToMethod);

            List<Order> resultExit = new List<Order>();

            if (result != null
                && startIndex < result.Count)
            {
                if (startIndex + count < result.Count)
                {
                    resultExit = result.GetRange(startIndex, count);
                }
                else
                {
                    resultExit = result.GetRange(startIndex, result.Count - startIndex);
                }
            }

            return resultExit;
        }

        private List<Order> GetAllHistoricalOrdersArray(int maxCountByCategory)
        {
            List<Order> ordersOpenAll = new List<Order>();

            List<Order> orders = new List<Order>();

            for (int i = 0; i < _instType.Count; i++)
            {
                GetAllHistoricalOrders(orders, 100, _instType[i]);
            }

            if (orders != null
                && orders.Count > 0)
            {
                ordersOpenAll.AddRange(orders);
            }

            return ordersOpenAll;
        }

        private List<string> _instType = new List<string>() { "SWAP", "SPOT" };

        private void GetAllHistoricalOrders(List<Order> array, int maxCount, string instType)
        {
            _rateGateOrder.WaitToProceed();

            try
            {
                string url = $"{_baseUrl}/api/v5/trade/orders-history?instType={instType}&limit=50";
                HttpResponseMessage res = GetPrivateRequest(url);
                string contentStr = res.Content.ReadAsStringAsync().Result;

                if (res.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<ResponseWsOrders>> OrderResponse = JsonConvert.DeserializeAnonymousType(contentStr, new ResponseRestMessage<List<ResponseWsOrders>>());

                    if (OrderResponse.code.Equals("0"))
                    {
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

                        if (orders.Count > 0)
                        {
                            array.AddRange(orders);

                            if (array.Count > maxCount)
                            {
                                while (array.Count > maxCount)
                                {
                                    array.RemoveAt(array.Count - 1);
                                }
                                return;
                            }
                            else if (array.Count < 100)
                            {
                                return;
                            }
                        }
                        else
                        {
                            return;
                        }

                        return;
                    }
                    else
                    {
                        SendLogMessage($"Get all historical orders request error. {OrderResponse.code} || {OrderResponse.msg}", LogMessageType.Error);
                        return;
                    }

                }
                else
                {
                    SendLogMessage($"Get all historical orders request error. Code: {res.StatusCode} || {contentStr}", LogMessageType.Error);
                    return;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"GetAllHistoricalOrders - {ex.Message}", LogMessageType.Error);
                return;
            }
        }

        #endregion

        #region 12 Queries

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