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
            realization.UseFullMarketDepth = this._needToUseFullMarketDepth;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamPassword, "");
            CreateParameterBoolean("Hedge Mode", true);
            ServerParameters[3].ValueChange += OkxServer_ValueChange;
            CreateParameterEnum("Margin Mode", "Cross", new List<string> { "Cross", "Isolated" });
            CreateParameterBoolean("Use Options", false);
            CreateParameterBoolean("Demo Mode", false);
            CreateParameterBoolean("Extended Data", false);
            CreateParameterEnum("Market Depth level", "5", new List<string> { "5", "400" });

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
            ServerParameters[2].Comment = OsLocalization.Market.Label271;
            ServerParameters[3].Comment = OsLocalization.Market.Label250;
            ServerParameters[4].Comment = OsLocalization.Market.Label249;
            ServerParameters[5].Comment = OsLocalization.Market.Label253;
            ServerParameters[6].Comment = OsLocalization.Market.Label268;
            ServerParameters[7].Comment = OsLocalization.Market.Label252;
            ServerParameters[8].Comment = OsLocalization.Market.Label375;
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
            threadMessageReaderPublic.Name = "MessageReaderPublic";
            threadMessageReaderPublic.IsBackground = true;
            threadMessageReaderPublic.Start();

            Thread threadMessageReaderPrivate = new Thread(MessageReaderPrivate);
            threadMessageReaderPrivate.Name = "MessageReaderPrivate";
            threadMessageReaderPrivate.IsBackground = true;
            threadMessageReaderPrivate.Start();

            Thread thread = new Thread(CheckAliveWebSocket);
            thread.Name = "CheckAliveWebSocket";
            thread.IsBackground = true;
            thread.Start();

            Thread threadMessageReaderMarketDepthSpot = new Thread(ThreadMessageReaderMarketDepthSpot);
            threadMessageReaderMarketDepthSpot.Name = "ThreadOkxMessageReaderMarketDepthSpot";
            threadMessageReaderMarketDepthSpot.IsBackground = true;
            threadMessageReaderMarketDepthSpot.Start();

            Thread threadMessageReaderMarketDepthSwap = new Thread(ThreadMessageReaderMarketDepthSwap);
            threadMessageReaderMarketDepthSwap.Name = "ThreadOkxMessageReaderMarketDepthSwap";
            threadMessageReaderMarketDepthSwap.IsBackground = true;
            threadMessageReaderMarketDepthSwap.Start();

            Thread threadMessageReaderMarketDepthFutures = new Thread(ThreadMessageReaderMarketDepthFutures);
            threadMessageReaderMarketDepthFutures.Name = "ThreadOkxMessageReaderMarketDepthFutures";
            threadMessageReaderMarketDepthFutures.IsBackground = true;
            threadMessageReaderMarketDepthFutures.Start();

            Thread threadMessageReaderMarketDepthOption = new Thread(ThreadMessageReaderMarketDepthOption);
            threadMessageReaderMarketDepthOption.Name = "ThreadOkxMessageReaderMarketDepthOption";
            threadMessageReaderMarketDepthOption.IsBackground = true;
            threadMessageReaderMarketDepthOption.Start();

            Thread threadMessageReaderTradesSpot = new Thread(ThreadMessageReaderTradesSpot);
            threadMessageReaderTradesSpot.Name = "ThreadOkxMessageReaderTradesSpot";
            threadMessageReaderTradesSpot.IsBackground = true;
            threadMessageReaderTradesSpot.Start();

            Thread threadMessageReaderTradesSwap = new Thread(ThreadMessageReaderTradesSwap);
            threadMessageReaderTradesSwap.Name = "ThreadOkxMessageReaderTradesSwap";
            threadMessageReaderTradesSwap.IsBackground = true;
            threadMessageReaderTradesSwap.Start();

            Thread threadMessageReaderTradesFutures = new Thread(ThreadMessageReaderTradesFutures);
            threadMessageReaderTradesFutures.Name = "ThreadOkxMessageReaderTradesFutures";
            threadMessageReaderTradesFutures.IsBackground = true;
            threadMessageReaderTradesFutures.Start();

            Thread threadMessageReaderTradesOption = new Thread(ThreadMessageReaderTradesOption);
            threadMessageReaderTradesOption.Name = "ThreadOkxMessageReaderTradesOption";
            threadMessageReaderTradesOption.IsBackground = true;
            threadMessageReaderTradesOption.Start();
        }

        private WebProxy _myProxy;

        public void Connect(WebProxy proxy)
        {
            _myProxy = proxy;
            _socketReconnectAllowed = true;

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

            if (((ServerParameterEnum)ServerParameters[8]).Value == "400")
            {
                _marketDepthChannel = "books";
            }
            else
            {
                _marketDepthChannel = "books5";
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
                    exception.ToString() +
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
                    exception.ToString() +
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
                _orderBooks.Clear();
                _booksSocketBySecurity.Clear();
                DeleteWebSocketConnection();

                if (_httpClient != null)
                {
                    _httpClient.Dispose();
                    _httpClient = null;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            _fIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
            _fIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();
            _queueMessageMarketDepthSpot = new ConcurrentQueue<string>();
            _queueMessageMarketDepthSwap = new ConcurrentQueue<string>();
            _queueMessageMarketDepthFutures = new ConcurrentQueue<string>();
            _queueMessageMarketDepthOption = new ConcurrentQueue<string>();
            _queueMessageTradesSpot = new ConcurrentQueue<string>();
            _queueMessageTradesSwap = new ConcurrentQueue<string>();
            _queueMessageTradesFutures = new ConcurrentQueue<string>();
            _queueMessageTradesOption = new ConcurrentQueue<string>();

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

        public bool IsCompletelyDeleted { get; set; }

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
                SecurityResponse securityResponseFutures = GetSwapSecurities();
                SecurityResponse securityResponseSpot = GetSpotSecurities();

                if (securityResponseFutures == null || securityResponseFutures.data == null
                    || securityResponseSpot == null || securityResponseSpot.data == null)
                {
                    SendLogMessage("Securities loading error: swap or spot instruments request failed. Reconnect to retry.", LogMessageType.Error);
                    return;
                }

                securityResponseFutures.data.AddRange(securityResponseSpot.data);

                SecurityResponse securityResponseFuturesContracts = GetFuturesContractsSecurities();
                if (securityResponseFuturesContracts != null && securityResponseFuturesContracts.data != null)
                {
                    securityResponseFutures.data.AddRange(securityResponseFuturesContracts.data);
                }

                if (_useOptions)
                {
                    _baseOptionSerurities = GetOptionBaseSecurities();

                    if (_baseOptionSerurities == null)
                    {
                        SendLogMessage("Securities loading error: option underlying request failed. Reconnect to retry.", LogMessageType.Error);
                        return;
                    }

                    SecurityResponse securityResponseOptions = GetOptionSecurities(_baseOptionSerurities);

                    if (securityResponseOptions == null || securityResponseOptions.data == null)
                    {
                        SendLogMessage("Securities loading error: option instruments request failed. Reconnect to retry.", LogMessageType.Error);
                        return;
                    }

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

        private SecurityResponse GetSwapSecurities()
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
                    SendLogMessage($"GetSwapSecurities error. Status: {response.StatusCode}. {response.ErrorMessage} {response.Content}", LogMessageType.Error);
                    return null;
                }

                if (string.IsNullOrEmpty(response.Content))
                {
                    SendLogMessage("GetSwapSecurities error. Empty response", LogMessageType.Error);
                    return null;
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
                    SendLogMessage($"GetFuturesContractsSecurities error. Status: {response.StatusCode}. {response.ErrorMessage} {response.Content}", LogMessageType.Error);
                    return null;
                }

                if (string.IsNullOrEmpty(response.Content))
                {
                    SendLogMessage("GetFuturesContractsSecurities error. Empty response", LogMessageType.Error);
                    return null;
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
                    SendLogMessage($"GetOptionBaseSecurities error. Status: {response.StatusCode}. {response.ErrorMessage} {response.Content}", LogMessageType.Error);
                    return null;
                }

                if (string.IsNullOrEmpty(response.Content))
                {
                    SendLogMessage("GetOptionBaseSecurities error. Empty response", LogMessageType.Error);
                    return null;
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
                        SendLogMessage($"GetOptionSecurities error. {baseSecurity} Status: {response.StatusCode}. {response.ErrorMessage} {response.Content}", LogMessageType.Error);
                        continue;
                    }

                    if (string.IsNullOrEmpty(response.Content))
                    {
                        SendLogMessage($"GetOptionSecurities error. {baseSecurity} Empty response", LogMessageType.Error);
                        continue;
                    }

                    SecurityResponse securityResponse = JsonConvert.DeserializeAnonymousType(response.Content, new SecurityResponse());

                    if (securityResponse == null || securityResponse.data == null)
                    {
                        SendLogMessage($"GetOptionSecurities - no data for {baseSecurity}", LogMessageType.Error);
                        continue;
                    }

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
                    SendLogMessage($"GetSpotSecurities error. Status: {response.StatusCode}. {response.ErrorMessage} {response.Content}", LogMessageType.Error);
                    return null;
                }

                if (string.IsNullOrEmpty(response.Content))
                {
                    SendLogMessage("GetSpotSecurities error. Empty response", LogMessageType.Error);
                    return null;
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

        private Dictionary<string, Security> _securitiesDict = new Dictionary<string, Security>();

        private void UpdatePairs(SecurityResponse securityResponse)
        {
            if (_securitiesDict == null)
            {
                _securitiesDict = new Dictionary<string, Security>();
            }

            List<Security> securities = new List<Security>();

            for (int i = 0; i < securityResponse.data.Count; i++)
            {
                SecurityResponseItem item = securityResponse.data[i];

                Security security = new Security();

                SecurityType securityType = SecurityType.CurrencyPair;

                if (item.instType.Equals("SWAP")
                    || item.instType.Equals("FUTURES"))
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

                security.MinTradeAmountType = MinTradeAmountType.Contract;

                if (securityType == SecurityType.CurrencyPair)
                {
                    security.NameClass = "SPOT_" + item.quoteCcy;
                }

                if (securityType == SecurityType.Futures)
                {
                    if (item.ctType == "linear")
                    {
                        security.NameClass = $"Linear_{item.instType}_{item.settleCcy}";
                    }
                    else if (item.ctType == "inverse")
                    {
                        security.NameClass = $"Inverse_{item.instType}_{item.ctValCcy}";
                    }

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

                    if (string.IsNullOrEmpty(item.expTime) == false)
                    {
                        security.Expiration = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.expTime));
                    }
                    security.OptionType = item.optType == "P" ? OptionType.Put : OptionType.Call;
                    security.Strike = item.stk.ToDecimal();

                    string baseName = item.uly + "T"; // example: BTC-USD -> BTC-USDT

                    // 1. Find all futures that are true quarterly futures (expire on last Friday of Mar, Jun, Sep, Dec)
                    var quarterlyFutures = securities
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

                ApplyInstrumentParams(security, item);

                security.State = SecurityStateType.Activ;
                securities.Add(security);
            }

            if (securities.Count > 0)
            {
                securities = securities.OrderBy(s => s.Name).ToList();
            }

            foreach (Security sec in securities)
            {
                _securitiesDict[sec.Name] = sec;
            }

            if (SecurityEvent != null)
            {
                SecurityEvent(securities);
            }
        }

        // trading parameter mapping shared by UpdatePairs and the instruments channel push handler.
        // only the fields that affect order sizing and rounding are refreshed on pushes
        private void ApplyInstrumentParams(Security security, SecurityResponseItem item)
        {
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

            if (security.SecurityType == SecurityType.Futures)
            {
                security.NameId = item.instId + "_" + item.ctVal.ToDecimal();
                security.MinTradeAmount = item.minSz.ToDecimal() * item.ctVal.ToDecimal();
                security.VolumeStep = item.lotSz.ToDecimal() * item.ctVal.ToDecimal();
                security.DecimalsVolume = security.MinTradeAmount.ToString().DecimalsCount();
            }
            else
            {
                // spot/options: lotSz is the rounding step for sz, minSz is the minimum order size (floor check only)
                security.MinTradeAmount = item.minSz.ToDecimal();
                security.VolumeStep = item.lotSz.ToDecimal();

                string volStep = item.lotSz.Replace(',', '.');

                if (volStep != null
                    && volStep.Length > 0
                    && volStep.Split('.').Length > 1)
                {
                    security.DecimalsVolume = volStep.Split('.')[1].Length;
                }
            }
        }

        // instruments channel push: trading parameters (tickSz/minSz/lotSz/ctVal) change over time,
        // the cached securities are updated in place so order rounding uses fresh steps
        private void UpdateInstrumentCache(string message)
        {
            try
            {
                ResponseWsMessageAction<List<SecurityResponseItem>> response =
                    JsonConvert.DeserializeAnonymousType(message, new ResponseWsMessageAction<List<SecurityResponseItem>>());

                if (response.data == null
                    || response.data.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < response.data.Count; i++)
                {
                    SecurityResponseItem item = response.data[i];

                    // preopen placeholder instruments may come without instId
                    if (string.IsNullOrEmpty(item.instId))
                    {
                        continue;
                    }

                    Security sec;
                    if (_securitiesDict == null
                        || _securitiesDict.TryGetValue(item.instId, out sec) == false
                        || sec == null)
                    {
                        continue;
                    }

                    decimal oldPriceStep = sec.PriceStep;
                    decimal oldVolumeStep = sec.VolumeStep;

                    ApplyInstrumentParams(sec, item);

                    if (sec.PriceStep != oldPriceStep
                        || sec.VolumeStep != oldVolumeStep)
                    {
                        SendLogMessage($"Instrument params updated for {item.instId}: price step {oldPriceStep} -> {sec.PriceStep}, volume step {oldVolumeStep} -> {sec.VolumeStep}, state {item.state}", LogMessageType.System);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
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

        // history-candles endpoint: 20 requests per 2 seconds
        public RateGate _rateGateCandlesHistory = new RateGate(1, TimeSpan.FromMilliseconds(200));

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

            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
            int CountCandlesNeedToLoad = GetCountCandlesFromTimeInterval(startTime, endTime, timeFrameBuilder.TimeFrameTimeSpan);

            if (!CheckTime(startTime, endTime, actualTime))
            {
                return null;
            }

            if (!CheckTf(tfTotalMinutes))
            {
                return null;
            }

            if (endTime > DateTime.UtcNow)
            {
                endTime = DateTime.UtcNow;
            }

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

            if (securityResponse == null)
            {
                return candles;
            }

            ConvertCandles(securityResponse, candles);

            candles.Reverse();

            return candles;
        }

        private CandlesResponse GetResponseDataCandles(string nameSec, TimeSpan tf, int NumberCandlesToLoad, long DataEnd, bool isOsData)
        {
            try
            {
                string bar = GetStringBar(tf);

                long timeFrameMs = (long)tf.TotalMilliseconds;

                // window upper bound, moves back by arithmetic on each iteration (deterministic pagination).
                // it is never read from the response, so an empty or short page can not stall the loop
                long after = DataEnd;

                // market/candles keeps only the latest 1440 entries, older bars are served by market/history-candles
                long historyBorder = TimeManager.GetTimeStampMilliSecondsToDateTime(DateTime.UtcNow) - 1439 * timeFrameMs;

                CandlesResponse candlesResponse = new CandlesResponse();
                candlesResponse.data = new List<List<string>>();

                do
                {
                    int limit = NumberCandlesToLoad;

                    if (NumberCandlesToLoad > 300)
                    {
                        limit = 300;
                    }

                    bool useHistoryCandles = after < historyBorder;

                    if (useHistoryCandles)
                    {
                        _rateGateCandlesHistory.WaitToProceed();
                    }
                    else
                    {
                        _rateGateCandles.WaitToProceed();
                    }

                    string endpoint = useHistoryCandles ? "history-candles" : "candles";

                    string url = _baseUrl + $"/api/v5/market/{endpoint}?instId={nameSec}&bar={bar}&limit={limit}&after={after}";

                    RestClient client = new RestClient(url);
                    RestRequest request = new RestRequest(Method.GET);
                    IRestResponse Response = client.Execute(request);

                    if (Response.StatusCode == HttpStatusCode.OK)
                    {
                        CandlesResponse page = JsonConvert.DeserializeAnonymousType(Response.Content, new CandlesResponse());

                        if (page == null || page.data == null)
                        {
                            break;
                        }

                        candlesResponse.data.AddRange(page.data);
                    }
                    else
                    {
                        SendLogMessage($"GetResponseDataCandles - {Response.Content}", LogMessageType.Error);
                    }

                    // move the window back by limit bars, even if the page came back empty or short
                    after -= limit * timeFrameMs;
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
            return null;

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

                            trade.Side = item.side == "sell" ? Side.Sell : Side.Buy;
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

        private List<OkxSocketWrapper> _webSocketPublic = new List<OkxSocketWrapper>();

        // seamless reconnect of public sockets: a dead socket is reconnected and resubscribed
        // on its own instead of restarting the whole connector (TInvest pattern)
        private bool _socketReconnectAllowed = true;

        private WebSocket _webSocketPrivate;

        private void CreatePublicWebSocketConnect()
        {
            try
            {
                if (_fIFOListWebSocketPublicMessage == null)
                {
                    _fIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
                }

                OkxSocketWrapper firstWrapper = new OkxSocketWrapper();
                firstWrapper.Socket = CreateNewPublicSocket();
                _webSocketPublic.Add(firstWrapper);
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
                        WebSocket webSocketPublic = _webSocketPublic[i].Socket;

                        if (webSocketPublic == null)
                        {
                            continue;
                        }

                        DetachPublicSocketEvents(webSocketPublic);

                        if (webSocketPublic.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublic.CloseAsync();
                        }

                        _webSocketPublic[i].Socket = null;
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
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
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
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

                    WebSocket webSocketPublic = _webSocketPublic[0].Socket;

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
                SendLogMessage(ex.ToString(), LogMessageType.Error);
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
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void SetPositionMode()
        {
            // The caller may be the UI thread (server parameter change).
            // Blocking HTTP (.Result) with async continuations on the UI
            // synchronization context deadlocks the terminal
            System.Threading.Tasks.Task.Run(() => SetPositionModeThread());
        }

        private void SetPositionModeThread()
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            string targetMode = HedgeMode ? "long_short_mode" : "net_mode";

            try
            {
                // OKX rejects set-position-mode on an account with open orders, positions
                // or bots — even when the requested mode is already active.
                // Ask the current mode first and push only when it really has to change
                string currentMode = GetCurrentPositionMode();

                if (currentMode == targetMode)
                {
                    return;
                }

                Dictionary<string, string> dict = new Dictionary<string, string>();

                dict["posMode"] = targetMode;

                string res = PushPositionMode(dict);
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        private string GetCurrentPositionMode()
        {
            _rateGatePositionMode.WaitToProceed();

            string url = $"{_baseUrl}/api/v5/account/config";

            HttpResponseMessage res = GetPrivateRequest(url);
            string contentStr = res.Content.ReadAsStringAsync().Result;

            if (res.StatusCode != HttpStatusCode.OK)
            {
                SendLogMessage($"Get account config request error {res.StatusCode} || {contentStr}", LogMessageType.Error);
                return null;
            }

            ResponseRestMessage<List<AccountConfigData>> message = JsonConvert.DeserializeAnonymousType(contentStr, new ResponseRestMessage<List<AccountConfigData>>());

            if (message == null
                || string.Equals(message.code, "0") == false
                || message.data == null
                || message.data.Count == 0)
            {
                string errorText = message != null
                    && string.IsNullOrEmpty(message.msg) == false
                        ? message.msg
                        : contentStr;

                SendLogMessage($"Get account config failed: {errorText}", LogMessageType.Error);
                return null;
            }

            return message.data[0].posMode;
        }

        private string PushPositionMode(Dictionary<string, string> requestParams)
        {
            _rateGatePositionMode.WaitToProceed();

            string url = $"{_baseUrl}{"/api/v5/account/set-position-mode"}";
            string bodyStr = JsonConvert.SerializeObject(requestParams);

            HttpResponseMessage res = SendPrivatePost(url, bodyStr);
            string contentStr = res.Content.ReadAsStringAsync().Result;

            ResponseRestMessage<List<RestMessageSendOrder>> message = JsonConvert.DeserializeAnonymousType(contentStr, new ResponseRestMessage<List<RestMessageSendOrder>>());

            // OKX returns code "0" on success. Errors come with codes like "59000" etc.
            if (message == null
                || message.code.Equals("0") == false)
            {
                string errorText = message != null
                    && string.IsNullOrEmpty(message.msg) == false
                        ? message.msg
                        : contentStr;

                SendLogMessage($"PushPositionMode - {errorText}", LogMessageType.Error);

                if (errorText.Contains("API key doesn't exist"))
                {
                    Disconnect();
                }
            }

            return contentStr;
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocketPublic_Opened(object sender, EventArgs e)
        {
            try
            {
                // instrument parameter updates (lotSz/minSz/tickSz/ctVal changes) arrive through the
                // instruments channel; subscribe it once per connection on the first public socket
                if (sender is WebSocket openedSocket
                    && _webSocketPublic.Count > 0
                    && ReferenceEquals(openedSocket, _webSocketPublic[0].Socket))
                {
                    SubscribeInstrumentsChannel(openedSocket);
                }

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

        private void SubscribeInstrumentsChannel(WebSocket webSocketPublic)
        {
            try
            {
                if (webSocketPublic == null
                    || webSocketPublic.ReadyState != WebSocketState.Open)
                {
                    return;
                }

                // one frame with all types: OKX limits subscribe/unsubscribe/login requests to 480 per connection per hour
                RequestSubscribe<SubscribeArgsAccount> request = new RequestSubscribe<SubscribeArgsAccount>();
                request.args = new List<SubscribeArgsAccount>()
                {
                    new SubscribeArgsAccount() { channel = "instruments", instType = "SPOT" },
                    new SubscribeArgsAccount() { channel = "instruments", instType = "SWAP" },
                    new SubscribeArgsAccount() { channel = "instruments", instType = "FUTURES" },
                    new SubscribeArgsAccount() { channel = "instruments", instType = "OPTION" },
                };

                webSocketPublic.SendAsync(JsonConvert.SerializeObject(request));
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
                if (ServerStatus == ServerConnectStatus.Disconnect
                    || _socketReconnectAllowed == false)
                {
                    return;
                }

                OkxSocketWrapper wrapper = FindPublicSocketWrapper(sender as WebSocket);

                if (wrapper == null)
                {
                    // the socket has just been replaced by a seamless reconnect: the old instance is no longer tracked
                    return;
                }

                // only the failed socket is reconnected (see CheckAliveWebSocket), the connector stays connected:
                // no candle reload, the other sockets keep streaming
                SendLogMessage($"OKX WebSocket Public connection closed (code {e.Code}). The socket will be reconnected.", LogMessageType.System);

                wrapper.Socket = null;
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

                if (_fIFOListWebSocketPublicMessage == null)
                {
                    return;
                }

                _fIFOListWebSocketPublicMessage.Enqueue(e.Data);
                _eventPublicMessage.Set();
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

                // wire format from OKX is compact json: {"event":"login","code":"0",...}
                // errors are logged by the MessageReaderPrivate thread
                if (e.Data.Contains("\"event\":\"login\"")
                    && e.Data.Contains("\"code\":\"0\""))
                {
                    SubscribePrivate();
                }

                if (_fIFOListWebSocketPrivateMessage == null)
                {
                    return;
                }

                _fIFOListWebSocketPrivateMessage.Enqueue(e.Data);
                _eventPrivateMessage.Set();
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

                    if (IsCompletelyDeleted == true)
                    {
                        return;
                    }

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        continue;
                    }

                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        OkxSocketWrapper wrapper = _webSocketPublic[i];

                        if (wrapper?.Socket != null
                            && wrapper.Socket.ReadyState == WebSocketState.Open)
                        {
                            wrapper.Socket.SendAsync("ping");
                        }
                        else
                        {
                            // reconnect only the dead socket instead of the whole connector.
                            // Mass failures (OKX maintenance, error 64008) hit all sockets at once:
                            // the reconnects go one by one with a pause, OKX allows
                            // no more than 3 new connections per second per IP
                            ReconnectPublicSocket(wrapper);
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

        private OkxSocketWrapper FindPublicSocketWrapper(WebSocket socket)
        {
            if (socket == null)
            {
                return null;
            }

            for (int i = 0; i < _webSocketPublic.Count; i++)
            {
                if (ReferenceEquals(_webSocketPublic[i].Socket, socket))
                {
                    return _webSocketPublic[i];
                }
            }

            return null;
        }

        private void ReconnectPublicSocket(OkxSocketWrapper wrapper)
        {
            try
            {
                if (wrapper == null
                    || ServerStatus == ServerConnectStatus.Disconnect
                    || _socketReconnectAllowed == false)
                {
                    return;
                }

                if (wrapper.LastReconnectTime != DateTime.MinValue
                    && wrapper.LastReconnectTime.AddSeconds(30) > DateTime.Now)
                {
                    // throttled: the same socket is not reconnected more often than once in 30 seconds
                    return;
                }

                wrapper.LastReconnectTime = DateTime.Now;
                wrapper.ReconnectAttempts++;

                SendLogMessage($"OKX WebSocket Public reconnect attempt {wrapper.ReconnectAttempts}/3", LogMessageType.System);

                // pause between sockets: mass failures reconnect one by one,
                // OKX allows no more than 3 new connections per second per IP
                Thread.Sleep(500);

                WebSocket oldSocket = wrapper.Socket;

                // the wrapper gets the new socket before waiting for open:
                // the Opened hook (instruments channel on the first socket) and the Closed lookup must see the final state
                WebSocket newSocket = CreateNewPublicSocket();
                wrapper.Socket = newSocket;

                RebindBooksSockets(oldSocket, newSocket);

                DateTime timeEnd = DateTime.Now.AddSeconds(10);
                while (newSocket.ReadyState != WebSocketState.Open)
                {
                    Thread.Sleep(1000);

                    if (timeEnd < DateTime.Now)
                    {
                        break;
                    }
                }

                if (newSocket.ReadyState != WebSocketState.Open)
                {
                    if (wrapper.ReconnectAttempts >= 3)
                    {
                        SendLogMessage("OKX WebSocket Public reconnect failed after maximum attempts. Restarting the connector.", LogMessageType.Error);
                        Disconnect();
                    }
                    return;
                }

                if (oldSocket != null)
                {
                    DetachPublicSocketEvents(oldSocket);

                    if (oldSocket.ReadyState == WebSocketState.Open)
                    {
                        oldSocket.CloseAsync();
                    }
                }

                // one batched frame with everything this socket was subscribed to:
                // a new connection has a fresh 480 requests/hour budget, no gate is needed
                List<Dictionary<string, string>> argsCopy;

                lock (wrapper.Subscriptions)
                {
                    argsCopy = new List<Dictionary<string, string>>(wrapper.Subscriptions);
                }

                if (argsCopy.Count > 0)
                {
                    Dictionary<string, object> subscribeRequest = new Dictionary<string, object>();
                    subscribeRequest.Add("op", "subscribe");
                    subscribeRequest.Add("args", argsCopy);

                    newSocket.SendAsync(JsonConvert.SerializeObject(subscribeRequest));
                }

                wrapper.ReconnectAttempts = 0;
                SendLogMessage($"OKX WebSocket Public reconnected, {argsCopy.Count} subscriptions restored", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void RebindBooksSockets(WebSocket oldSocket, WebSocket newSocket)
        {
            if (oldSocket == null)
            {
                return;
            }

            foreach (var pair in _booksSocketBySecurity)
            {
                if (ReferenceEquals(pair.Value, oldSocket))
                {
                    _booksSocketBySecurity[pair.Key] = newSocket;
                }
            }
        }

        private void DetachPublicSocketEvents(WebSocket webSocketPublic)
        {
            webSocketPublic.OnOpen -= WebSocketPublic_Opened;
            webSocketPublic.OnClose -= WebSocketPublic_Closed;
            webSocketPublic.OnMessage -= WebSocketPublic_MessageReceived;
            webSocketPublic.OnError -= WebSocketPublic_Error;
        }

        #endregion

        #region 9 Security subscribe

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(450));

        //mapping: secutity name -> option (true or false)
        private ConcurrentDictionary<string, bool> _subscribedSecurities = new ConcurrentDictionary<string, bool>();

        private string _marketDepthChannel = "books5";

        public ServerParameterBool UseFullMarketDepth;

        // incremental books (400 levels) state: full book + seqId chain per security
        private ConcurrentDictionary<string, OrderBookKeeper> _orderBooks = new ConcurrentDictionary<string, OrderBookKeeper>();

        // the public socket carrying the books subscription of a security:
        // needed to resubscribe the instrument on its own connection after a seqId gap
        private ConcurrentDictionary<string, WebSocket> _booksSocketBySecurity = new ConcurrentDictionary<string, WebSocket>();

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

                OkxSocketWrapper wrapper = _webSocketPublic[_webSocketPublic.Count - 1];
                WebSocket webSocketPublic = wrapper.Socket;

                if (webSocketPublic != null
                    && webSocketPublic.ReadyState == WebSocketState.Open
                    && _subscribedSecurities.Count != 0
                    && _subscribedSecurities.Count % 50 == 0)
                {
                    // creating a new socket
                    OkxSocketWrapper newWrapper = new OkxSocketWrapper();
                    newWrapper.Socket = CreateNewPublicSocket();

                    DateTime timeEnd = DateTime.Now.AddSeconds(10);
                    while (newWrapper.Socket.ReadyState != WebSocketState.Open)
                    {
                        Thread.Sleep(1000);

                        if (timeEnd < DateTime.Now)
                        {
                            break;
                        }
                    }

                    if (newWrapper.Socket.ReadyState == WebSocketState.Open)
                    {
                        _webSocketPublic.Add(newWrapper);
                        wrapper = newWrapper;
                        webSocketPublic = newWrapper.Socket;
                    }
                }

                if (webSocketPublic != null)
                {
                    List<SubscribeArgs> subscribeArgs = new List<SubscribeArgs>();

                    subscribeArgs.Add(new SubscribeArgs() { channel = _marketDepthChannel, instId = security.Name });

                    if (_useOptions && security.SecurityType == SecurityType.Option)
                    {
                        // OKX pushes option ticks only through the separate option-trades channel, instType is required there
                        subscribeArgs.Add(new SubscribeArgs() { channel = "option-trades", instType = "OPTION", instId = security.Name });
                    }
                    else
                    {
                        subscribeArgs.Add(new SubscribeArgs() { channel = "trades", instId = security.Name });
                    }

                    if (_extendedMarketData)
                    {
                        subscribeArgs.Add(new SubscribeArgs() { channel = "tickers", instId = security.Name });

                        if (security.Name.Contains("SWAP"))
                        {
                            subscribeArgs.Add(new SubscribeArgs() { channel = "open-interest", instId = security.Name });
                            subscribeArgs.Add(new SubscribeArgs() { channel = "funding-rate", instId = security.Name });
                        }
                    }

                    // the frame goes through the wrapper: the args are remembered and restored
                    // in one batch if this socket is ever reconnected seamlessly
                    List<Dictionary<string, string>> frameArgs = new List<Dictionary<string, string>>();

                    for (int i = 0; i < subscribeArgs.Count; i++)
                    {
                        Dictionary<string, string> arg = new Dictionary<string, string>();
                        arg.Add("channel", subscribeArgs[i].channel);
                        arg.Add("instId", subscribeArgs[i].instId);

                        if (string.IsNullOrEmpty(subscribeArgs[i].instType) == false)
                        {
                            arg.Add("instType", subscribeArgs[i].instType);
                        }

                        frameArgs.Add(arg);
                    }

                    SendSubscribeFrame(wrapper, frameArgs);

                    _booksSocketBySecurity[securityName] = webSocketPublic;

                    if (_extendedMarketData
                        && security.Name.Contains("SWAP"))
                    {
                        GetFundingHistory(security.Name);
                    }
                }

                if (_useOptions && security.SecurityType == SecurityType.Option)
                {
                    _subscribedSecurities.TryAdd(securityName, true);

                    _rateGateSubscribe.WaitToProceed();

                    SubscribeMarkPrice(security.Name, wrapper);

                    securityName = securityName.Substring(0, 7);

                    string key = securityName + "-OPTION";
                    if (!_subscribedSecurities.ContainsKey(key))
                    {
                        SubscribeOptionSummary(securityName, wrapper);
                        //for underlying price
                        SubscribeMarkPrice(securityName + "-SWAP", wrapper);

                        _subscribedSecurities.TryAdd(key, false);
                    }
                }
                else
                {
                    _subscribedSecurities.TryAdd(securityName, false);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
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
                        if (responseFunding.data != null
                            && responseFunding.data.Count > 0)
                        {
                            FundingItemHistory item = responseFunding.data[0];

                            Funding data = new Funding();

                            data.SecurityNameCode = item.instId;
                            data.PreviousFundingTime = TimeManager.GetDateTimeFromTimeStamp((long)item.fundingTime.ToDecimal());

                            FundingUpdateEvent?.Invoke(data);
                        }
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

        // sends a subscribe frame and remembers the args on the wrapper:
        // a seamless reconnect restores everything from the remembered list
        private void SendSubscribeFrame(OkxSocketWrapper wrapper, List<Dictionary<string, string>> args)
        {
            if (wrapper == null
                || args == null
                || args.Count == 0)
            {
                return;
            }

            if (wrapper.Socket != null
                && wrapper.Socket.ReadyState == WebSocketState.Open)
            {
                Dictionary<string, object> subscribeRequest = new Dictionary<string, object>();
                subscribeRequest.Add("op", "subscribe");
                subscribeRequest.Add("args", args);

                wrapper.Socket.SendAsync(JsonConvert.SerializeObject(subscribeRequest));
            }

            lock (wrapper.Subscriptions)
            {
                wrapper.Subscriptions.AddRange(args);
            }
        }

        public void SubscribeOptionSummary(string securityName, OkxSocketWrapper wrapper)
        {
            List<Dictionary<string, string>> args = new List<Dictionary<string, string>>()
            {
                new Dictionary<string, string>() { { "channel", "opt-summary" }, { "instFamily", securityName } } //"BTC-USD"
            };

            SendSubscribeFrame(wrapper, args);
        }

        public void SubscribeMarkPrice(string name, OkxSocketWrapper wrapper)
        {
            List<Dictionary<string, string>> args = new List<Dictionary<string, string>>()
            {
                new Dictionary<string, string>() { { "channel", "mark-price" }, { "instId", name } } //"LTC-USD-SWAP"
            };

            SendSubscribeFrame(wrapper, args);
        }

        private void SubscribePrivate()
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                List<Dictionary<string, string>> privateSubscribeArgs = new List<Dictionary<string, string>>();

                privateSubscribeArgs.Add(new Dictionary<string, string>() { { "channel", "account" } });
                privateSubscribeArgs.Add(new Dictionary<string, string>() { { "channel", "positions" }, { "instType", "ANY" } });
                privateSubscribeArgs.Add(new Dictionary<string, string>() { { "channel", "orders" }, { "instType", "ANY" } });
                //privateSubscribeArgs.Add(new Dictionary<string, string>() { { "channel", "fills" } });

                // one frame with all channels: OKX limits subscribe/unsubscribe/login requests to 480 per connection per hour
                Dictionary<string, object> privateSubscribeRequest = new Dictionary<string, object>();
                privateSubscribeRequest.Add("op", "subscribe");
                privateSubscribeRequest.Add("args", privateSubscribeArgs);

                _webSocketPrivate.SendAsync(JsonConvert.SerializeObject(privateSubscribeRequest));
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
                // deliberate teardown: Closed events of the closing sockets must not trigger reconnects
                _socketReconnectAllowed = false;

                if (_webSocketPublic != null
                    && _webSocketPublic.Count != 0)
                {
                    // channels from all securities are batched into one unsubscribe frame per socket:
                    // OKX limits subscribe/unsubscribe/login requests to 480 per connection per hour
                    List<Dictionary<string, string>> unsubscribeArgs = new List<Dictionary<string, string>>();

                    if (_subscribedSecurities != null)
                    {
                        foreach (var item in _subscribedSecurities)
                        {
                            string name = item.Key;

                            // "XXX-OPTION" keys are option families, not instruments — nothing to unsubscribe there
                            if (name.EndsWith("-OPTION"))
                            {
                                continue;
                            }

                            unsubscribeArgs.Add(new Dictionary<string, string>() { { "channel", _marketDepthChannel }, { "instId", name } });

                            if (item.Value)
                            {
                                // option: ticks were subscribed through the option-trades channel
                                unsubscribeArgs.Add(new Dictionary<string, string>() { { "channel", "option-trades" }, { "instType", "OPTION" }, { "instId", name } });
                            }
                            else
                            {
                                unsubscribeArgs.Add(new Dictionary<string, string>() { { "channel", "trades" }, { "instId", name } });
                            }

                            if (_extendedMarketData)
                            {
                                unsubscribeArgs.Add(new Dictionary<string, string>() { { "channel", "tickers" }, { "instId", name } });

                                if (name.Contains("SWAP"))
                                {
                                    unsubscribeArgs.Add(new Dictionary<string, string>() { { "channel", "open-interest" }, { "instId", name } });
                                    unsubscribeArgs.Add(new Dictionary<string, string>() { { "channel", "funding-rate" }, { "instId", name } });
                                }
                            }

                            if (item.Value)
                            {
                                //option
                                unsubscribeArgs.Add(new Dictionary<string, string>() { { "channel", "mark-price" }, { "instId", name } });
                            }
                        }
                    }

                    if (_baseOptionSerurities != null)
                    {
                        foreach (string name in _baseOptionSerurities)
                        {
                            unsubscribeArgs.Add(new Dictionary<string, string>() { { "channel", "opt-summary" }, { "instFamily", name } });
                            unsubscribeArgs.Add(new Dictionary<string, string>() { { "channel", "mark-price" }, { "instId", name + "-SWAP" } });
                        }
                    }

                    // the instruments channel is subscribed per instrument type, not per security
                    unsubscribeArgs.Add(new Dictionary<string, string>() { { "channel", "instruments" }, { "instType", "SPOT" } });
                    unsubscribeArgs.Add(new Dictionary<string, string>() { { "channel", "instruments" }, { "instType", "SWAP" } });
                    unsubscribeArgs.Add(new Dictionary<string, string>() { { "channel", "instruments" }, { "instType", "FUTURES" } });
                    unsubscribeArgs.Add(new Dictionary<string, string>() { { "channel", "instruments" }, { "instType", "OPTION" } });

                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i].Socket;

                        try
                        {
                            if (webSocketPublic != null && webSocketPublic?.ReadyState == WebSocketState.Open
                                && unsubscribeArgs.Count != 0)
                            {
                                Dictionary<string, object> unsubscribeRequest = new Dictionary<string, object>();
                                unsubscribeRequest.Add("op", "unsubscribe");
                                unsubscribeRequest.Add("args", unsubscribeArgs);

                                webSocketPublic.SendAsync(JsonConvert.SerializeObject(unsubscribeRequest));
                            }
                        }
                        catch (Exception ex)
                        {
                            SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }


            if (_webSocketPrivate != null
                && _webSocketPrivate.ReadyState == WebSocketState.Open)
            {
                try
                {
                    List<Dictionary<string, string>> privateUnsubscribeArgs = new List<Dictionary<string, string>>();

                    privateUnsubscribeArgs.Add(new Dictionary<string, string>() { { "channel", "account" } });
                    privateUnsubscribeArgs.Add(new Dictionary<string, string>() { { "channel", "positions" }, { "instType", "ANY" } });
                    privateUnsubscribeArgs.Add(new Dictionary<string, string>() { { "channel", "orders" }, { "instType", "ANY" } });
                    //privateUnsubscribeArgs.Add(new Dictionary<string, string>() { { "channel", "fills" } });

                    // one frame with all channels: OKX limits subscribe/unsubscribe/login requests to 480 per connection per hour
                    Dictionary<string, object> privateUnsubscribeRequest = new Dictionary<string, object>();
                    privateUnsubscribeRequest.Add("op", "unsubscribe");
                    privateUnsubscribeRequest.Add("args", privateUnsubscribeArgs);

                    _webSocketPrivate.SendAsync(JsonConvert.SerializeObject(privateUnsubscribeRequest));
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
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

        private ConcurrentQueue<string> _fIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _fIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _queueMessageMarketDepthSpot = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _queueMessageMarketDepthSwap = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _queueMessageMarketDepthFutures = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _queueMessageMarketDepthOption = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _queueMessageTradesSpot = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _queueMessageTradesSwap = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _queueMessageTradesFutures = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _queueMessageTradesOption = new ConcurrentQueue<string>();

        // doorbells for the readers: Set on enqueue, the readers block on WaitOne
        // instead of spinning with Sleep(1)
        private readonly AutoResetEvent _eventPublicMessage = new AutoResetEvent(false);
        private readonly AutoResetEvent _eventPrivateMessage = new AutoResetEvent(false);
        private readonly AutoResetEvent _eventDepthSpot = new AutoResetEvent(false);
        private readonly AutoResetEvent _eventDepthSwap = new AutoResetEvent(false);
        private readonly AutoResetEvent _eventDepthFutures = new AutoResetEvent(false);
        private readonly AutoResetEvent _eventDepthOption = new AutoResetEvent(false);
        private readonly AutoResetEvent _eventTradesSpot = new AutoResetEvent(false);
        private readonly AutoResetEvent _eventTradesSwap = new AutoResetEvent(false);
        private readonly AutoResetEvent _eventTradesFutures = new AutoResetEvent(false);
        private readonly AutoResetEvent _eventTradesOption = new AutoResetEvent(false);

        private void MessageReaderPublic()
        {
            while (true)
            {
                try
                {
                    if (_fIFOListWebSocketPublicMessage.IsEmpty)
                    {
                        if (IsCompletelyDeleted == true)
                        {
                            return;
                        }

                        _eventPublicMessage.WaitOne(1000);
                    }
                    else
                    {
                        string message = null;

                        _fIFOListWebSocketPublicMessage.TryDequeue(out message);

                        if (message == null)
                        {
                            continue;
                        }

                        ResponseWsMessageHeader action = JsonConvert.DeserializeObject<ResponseWsMessageHeader>(message);

                        if (action.@event != null && action.@event.Contains("subscribe"))
                        {
                            //ignore
                            //SendLogMessage("[WS Public] Got subscribe msg: " + action.msg, LogMessageType.System);
                        }
                        else if (action.arg != null)
                        {
                            if (action.arg.channel.Equals("books5")
                                || action.arg.channel.Equals("books"))
                            {
                                if (action.arg.instId.EndsWith("SWAP"))
                                {
                                    _queueMessageMarketDepthSwap.Enqueue(message);
                                    _eventDepthSwap.Set();
                                }
                                else if (action.arg.instId.EndsWith("-C")
                                    || action.arg.instId.EndsWith("-P"))
                                {
                                    _queueMessageMarketDepthOption.Enqueue(message);
                                    _eventDepthOption.Set();
                                }
                                else
                                {
                                    bool endsWithDigit = Char.IsDigit(action.arg.instId[action.arg.instId.Length - 1]);

                                    if (endsWithDigit)
                                    {
                                        _queueMessageMarketDepthFutures.Enqueue(message);
                                        _eventDepthFutures.Set();
                                    }
                                    else
                                    {
                                        _queueMessageMarketDepthSpot.Enqueue(message);
                                        _eventDepthSpot.Set();
                                    }
                                }

                                continue;
                            }

                            if (action.arg.channel.Equals("instruments"))
                            {
                                UpdateInstrumentCache(message);
                                continue;
                            }

                            if (action.arg.channel.Equals("trades")
                                || action.arg.channel.Equals("option-trades"))
                            {
                                if (action.arg.instId.EndsWith("SWAP"))
                                {
                                    _queueMessageTradesSwap.Enqueue(message);
                                    _eventTradesSwap.Set();
                                }
                                else if (action.arg.instId.EndsWith("-C")
                                    || action.arg.instId.EndsWith("-P"))
                                {
                                    _queueMessageTradesOption.Enqueue(message);
                                    _eventTradesOption.Set();
                                }
                                else
                                {
                                    bool endsWithDigit = Char.IsDigit(action.arg.instId[action.arg.instId.Length - 1]);

                                    if (endsWithDigit)
                                    {
                                        _queueMessageTradesFutures.Enqueue(message);
                                        _eventTradesFutures.Set();
                                    }
                                    else
                                    {
                                        _queueMessageTradesSpot.Enqueue(message);
                                        _eventTradesSpot.Set();
                                    }
                                }

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
                                if (action.code == "60014")
                                {
                                    // OKX limits subscribe/unsubscribe/login requests to 480 per connection per hour
                                    SendLogMessage("[WS Public] OKX request limit exceeded: 480 subscribe/unsubscribe/login requests per connection per hour. " + action.msg, LogMessageType.Error);
                                }
                                else
                                {
                                    SendLogMessage("[WS Public] Got error msg: " + action.msg, LogMessageType.Error);
                                }
                            }
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
            while (true)
            {
                try
                {
                    if (_fIFOListWebSocketPrivateMessage.IsEmpty)
                    {
                        if (IsCompletelyDeleted == true)
                        {
                            return;
                        }

                        _eventPrivateMessage.WaitOne(1000);
                    }
                    else
                    {
                        string message = null;

                        _fIFOListWebSocketPrivateMessage.TryDequeue(out message);

                        if (message == null)
                        {
                            continue;
                        }

                        ResponseWsMessageHeader action = JsonConvert.DeserializeObject<ResponseWsMessageHeader>(message);

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
                            if (action.@event != null && action.@event.Equals("error"))
                            {
                                if (action.code == "60014")
                                {
                                    // OKX limits subscribe/unsubscribe/login requests to 480 per connection per hour
                                    SendLogMessage("[WS Private] OKX request limit exceeded: 480 subscribe/unsubscribe/login requests per connection per hour. " + action.msg, LogMessageType.Error);
                                }
                                else
                                {
                                    SendLogMessage("[WS Private] Got error msg: " + action.msg, LogMessageType.Error);
                                }
                            }
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

                            if (item.instId.Contains("SWAP")
                                || item.instType == "FUTURES")
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

                PortfolioEvent?.Invoke(Portfolios);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
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

                PortfolioEvent?.Invoke(Portfolios);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ThreadMessageReaderTradesOption()
        {
            while (true)
            {
                try
                {
                    if (_queueMessageTradesOption.IsEmpty)
                    {
                        if (IsCompletelyDeleted == true)
                        {
                            return;
                        }

                        _eventTradesOption.WaitOne(1000);
                    }
                    else
                    {
                        string message;

                        if (_queueMessageTradesOption.TryDequeue(out message))
                        {
                            UpdateTrades(message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        private void ThreadMessageReaderTradesFutures()
        {
            while (true)
            {
                try
                {
                    if (_queueMessageTradesFutures.IsEmpty)
                    {
                        if (IsCompletelyDeleted == true)
                        {
                            return;
                        }

                        _eventTradesFutures.WaitOne(1000);
                    }
                    else
                    {
                        string message;

                        if (_queueMessageTradesFutures.TryDequeue(out message))
                        {
                            UpdateTrades(message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        private void ThreadMessageReaderTradesSwap()
        {
            while (true)
            {
                try
                {
                    if (_queueMessageTradesSwap.IsEmpty)
                    {
                        if (IsCompletelyDeleted == true)
                        {
                            return;
                        }

                        _eventTradesSwap.WaitOne(1000);
                    }
                    else
                    {
                        string message;

                        if (_queueMessageTradesSwap.TryDequeue(out message))
                        {
                            UpdateTrades(message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        private void ThreadMessageReaderTradesSpot()
        {
            while (true)
            {
                try
                {
                    if (_queueMessageTradesSpot.IsEmpty)
                    {
                        if (IsCompletelyDeleted == true)
                        {
                            return;
                        }

                        _eventTradesSpot.WaitOne(1000);
                    }
                    else
                    {
                        string message;

                        if (_queueMessageTradesSpot.TryDequeue(out message))
                        {
                            UpdateTrades(message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        private void ThreadMessageReaderMarketDepthOption()
        {
            while (true)
            {
                try
                {
                    if (_queueMessageMarketDepthOption.IsEmpty)
                    {
                        if (IsCompletelyDeleted == true)
                        {
                            return;
                        }

                        _eventDepthOption.WaitOne(1000);
                    }
                    else
                    {
                        string message;

                        if (_queueMessageMarketDepthOption.TryDequeue(out message))
                        {
                            UpdateMarketDepth(message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        private void ThreadMessageReaderMarketDepthFutures()
        {
            while (true)
            {
                try
                {
                    if (_queueMessageMarketDepthFutures.IsEmpty)
                    {
                        if (IsCompletelyDeleted == true)
                        {
                            return;
                        }

                        _eventDepthFutures.WaitOne(1000);
                    }
                    else
                    {
                        string message;

                        if (_queueMessageMarketDepthFutures.TryDequeue(out message))
                        {
                            UpdateMarketDepth(message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        private void ThreadMessageReaderMarketDepthSwap()
        {
            while (true)
            {
                try
                {
                    if (_queueMessageMarketDepthSwap.IsEmpty)
                    {
                        if (IsCompletelyDeleted == true)
                        {
                            return;
                        }

                        _eventDepthSwap.WaitOne(1000);
                    }
                    else
                    {
                        string message;

                        if (_queueMessageMarketDepthSwap.TryDequeue(out message))
                        {
                            UpdateMarketDepth(message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        private void ThreadMessageReaderMarketDepthSpot()
        {
            while (true)
            {
                try
                {
                    if (_queueMessageMarketDepthSpot.IsEmpty)
                    {
                        if (IsCompletelyDeleted == true)
                        {
                            return;
                        }

                        _eventDepthSpot.WaitOne(1000);
                    }
                    else
                    {
                        string message;

                        if (_queueMessageMarketDepthSpot.TryDequeue(out message))
                        {
                            UpdateMarketDepth(message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        // last depth time by security: the depth queues work in parallel,
        // a single shared field would couple timestamps across instruments
        private ConcurrentDictionary<string, DateTime> _lastTimeMdBySecurity = new ConcurrentDictionary<string, DateTime>();

        private void UpdateMarketDepth(string message)
        {
            try
            {
                ResponseWsMessageHeader header = JsonConvert.DeserializeObject<ResponseWsMessageHeader>(message);

                if (header.arg != null
                    && header.arg.channel.Equals("books"))
                {
                    UpdateMarketDepthBooks(message);
                    return;
                }

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

                DateTime lastTimeMd;
                _lastTimeMdBySecurity.TryGetValue(marketDepth.SecurityNameCode, out lastTimeMd);

                if (marketDepth.Time <= lastTimeMd)
                {
                    marketDepth.Time = lastTimeMd.AddTicks(1);
                }

                _lastTimeMdBySecurity[marketDepth.SecurityNameCode] = marketDepth.Time;

                MarketDepthEvent?.Invoke(marketDepth);

            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateMarketDepthBooks(string message)
        {
            ResponseWsMessageAction<List<ResponseWsDepthItem>> response =
                JsonConvert.DeserializeAnonymousType(message, new ResponseWsMessageAction<List<ResponseWsDepthItem>>());

            if (response.data == null
                || response.data.Count == 0)
            {
                return;
            }

            string securityName = response.arg.instId;

            OrderBookKeeper keeper;
            if (!_orderBooks.TryGetValue(securityName, out keeper))
            {
                keeper = new OrderBookKeeper();
                _orderBooks[securityName] = keeper;
            }

            ResponseWsDepthItem item = response.data[0];
            long seqId = string.IsNullOrEmpty(item.seqId) ? 0 : Convert.ToInt64(item.seqId);
            long prevSeqId = string.IsNullOrEmpty(item.prevSeqId) ? 0 : Convert.ToInt64(item.prevSeqId);

            bool applied;

            if (response.action == "snapshot"
                || !keeper.HasSnapshot)
            {
                keeper.ApplySnapshot(item.bids, item.asks, seqId);
                applied = true;
            }
            else
            {
                applied = keeper.ApplyUpdate(item.bids, item.asks, seqId, prevSeqId);
            }

            if (!applied)
            {
                // seqId gap: the local book is inconsistent, resync from scratch
                SendLogMessage($"[WS Public] books seqId gap on {securityName} (prevSeqId {prevSeqId}, last {keeper.SeqId}). Resubscribing.", LogMessageType.System);
                ResubscribeBooks(securityName);
                return;
            }

            MarketDepth marketDepth = new MarketDepth();
            marketDepth.SecurityNameCode = securityName;

            List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
            List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

            // with "Use full market depth" off the system consumes only the best bid/ask
            int maxLevels = UseFullMarketDepth != null
                && UseFullMarketDepth.Value == false
                ? 1
                : int.MaxValue;

            keeper.CopyTopLevels(bids, ascs, maxLevels);

            marketDepth.Asks = ascs;
            marketDepth.Bids = bids;

            marketDepth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts));

            DateTime lastTimeMd;
            _lastTimeMdBySecurity.TryGetValue(securityName, out lastTimeMd);

            if (marketDepth.Time <= lastTimeMd)
            {
                marketDepth.Time = lastTimeMd.AddTicks(1);
            }

            _lastTimeMdBySecurity[securityName] = marketDepth.Time;

            MarketDepthEvent?.Invoke(marketDepth);
        }

        private void ResubscribeBooks(string securityName)
        {
            WebSocket socket;
            if (!_booksSocketBySecurity.TryGetValue(securityName, out socket)
                || socket == null
                || socket.ReadyState != WebSocketState.Open)
            {
                return;
            }

            _rateGateSubscribe.WaitToProceed();

            Dictionary<string, object> unsubscribeRequest = new Dictionary<string, object>();
            unsubscribeRequest.Add("op", "unsubscribe");
            unsubscribeRequest.Add("args", new List<Dictionary<string, string>>()
            {
                new Dictionary<string, string>() { { "channel", _marketDepthChannel }, { "instId", securityName } }
            });
            socket.SendAsync(JsonConvert.SerializeObject(unsubscribeRequest));

            _rateGateSubscribe.WaitToProceed();

            RequestSubscribe<SubscribeArgs> subscribeRequest = new RequestSubscribe<SubscribeArgs>();
            subscribeRequest.args = new List<SubscribeArgs>()
            {
                new SubscribeArgs() { channel = _marketDepthChannel, instId = securityName }
            };
            socket.SendAsync(JsonConvert.SerializeObject(subscribeRequest));

            OrderBookKeeper keeper;
            if (_orderBooks.TryGetValue(securityName, out keeper))
            {
                keeper.Reset();
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

                // one update may aggregate multiple trades, so we go through the entire array
                for (int i = 0; i < tradeRespone.data.Count; i++)
                {
                    ResponseWsTrade item = tradeRespone.data[i];

                    Trade trade = new Trade();
                    trade.SecurityNameCode = item.instId;
                    trade.Price = item.px.ToDecimal();
                    trade.Id = item.tradeId;
                    trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts));
                    trade.Volume = item.sz.ToDecimal();

                    if (item.side.Equals("buy"))
                    {
                        trade.Side = Side.Buy;
                    }

                    if (item.side.Equals("sell"))
                    {
                        trade.Side = Side.Sell;
                    }

                    if (_extendedMarketData && trade.SecurityNameCode.Contains("SWAP"))
                    {
                        trade.OpenInterest = GetOpenInterest(trade.SecurityNameCode);
                    }

                    NewTradesEvent?.Invoke(trade);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private decimal GetOpenInterest(string securityNameCode)
        {
            AdditionalOptionData optionData;

            if (_additionalOptionData != null
                && _additionalOptionData.TryGetValue(securityNameCode, out optionData))
            {
                return optionData.OpenInterest.ToDecimal();
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
                        || newOrder.State == OrderStateType.Done)
                    {
                        ResponseWsOrders item = OrderResponse.data[i];

                        // the orders channel carries only the LAST fill (tradeId, fillSz, fillPx)
                        // and may push the same update repeatedly.
                        // MyTrade is emitted only when tradeId is new for this order
                        if (string.IsNullOrEmpty(item.tradeId) == false
                            && CheckTradeIsNew(item.ordId, item.tradeId))
                        {
                            MyTrade myTrade = new MyTrade();

                            string timeStamp = string.IsNullOrEmpty(item.fillTime) ? item.uTime : item.fillTime;
                            myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(timeStamp));
                            myTrade.SecurityNameCode = item.instId;
                            myTrade.NumberOrderParent = item.ordId.ToString();
                            myTrade.NumberTrade = item.tradeId;
                            myTrade.Volume = item.fillSz.ToDecimal();

                            if (item.instId.Contains("SWAP")
                                || item.instType == "FUTURES")
                            {
                                myTrade.Volume = item.fillSz.ToDecimal() * GetVolume(item.instId);
                            }
                            else if (string.IsNullOrEmpty(item.fee) == false
                                && item.instId.StartsWith(item.feeCcy))
                            {
                                // the commission is taken in the traded currency, not in the exchange currency
                                myTrade.Volume = item.fillSz.ToDecimal() + item.fee.ToDecimal();
                            }

                            if (string.IsNullOrEmpty(item.fillPx) == false)
                            {
                                myTrade.Price = item.fillPx.ToDecimal();
                            }

                            myTrade.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;

                            MyTradeEvent?.Invoke(myTrade);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        // last tradeId by order id, for emitting MyTrade only on a new fill
        // accessed only from the single MessageReaderPrivate thread, no lock needed
        private Dictionary<string, string> _lastTradeByOrder = new Dictionary<string, string>();

        private bool CheckTradeIsNew(string ordId, string tradeId)
        {
            if (_lastTradeByOrder.TryGetValue(ordId, out string lastTradeId)
                && lastTradeId == tradeId)
            {
                return false;
            }

            _lastTradeByOrder[ordId] = tradeId;

            // protection against unbounded growth: one entry per order
            if (_lastTradeByOrder.Count > 5000)
            {
                _lastTradeByOrder.Clear();
            }

            return true;
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
                // clOrdId can be empty or non-numeric for orders placed outside the terminal
            }

            newOrder.NumberMarket = item.ordId.ToString();
            newOrder.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;

            if (item.instId.Contains("SWAP")
                || item.instType == "FUTURES")
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

        // docs: 60 requests per 2 seconds for place order, cancel order and orders pending
        private RateGate _rateGatePlaceOrder = new RateGate(1, TimeSpan.FromMilliseconds(35));

        private RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(35));

        private RateGate _rateGateOrdersPending = new RateGate(1, TimeSpan.FromMilliseconds(35));

        // docs: 40 requests per 2 seconds for orders history
        private RateGate _rateGateOrdersHistory = new RateGate(1, TimeSpan.FromMilliseconds(55));

        // docs: 20 requests per 2 seconds for set position mode
        private RateGate _rateGatePositionMode = new RateGate(1, TimeSpan.FromMilliseconds(100));

        public void SendOrder(Order order)
        {
            _rateGatePlaceOrder.WaitToProceed();

            if (order.SecurityNameCode.Contains("SWAP")
                || order.SecurityClassCode.Contains("FUTURES")
                || order.SecurityClassCode.Contains("OPTION"))
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

                decimal priceStep;
                decimal volumeStep;
                decimal minTradeAmount;
                GetOrderSteps(order.SecurityNameCode, out priceStep, out volumeStep, out minTradeAmount);

                decimal orderVolume = TruncateToStep(order.Volume, volumeStep);

                if (orderVolume <= 0
                    || (minTradeAmount > 0 && orderVolume < minTradeAmount))
                {
                    SendLogMessage($"SendOrderSpot - order size {order.Volume} ({order.SecurityNameCode}) is below the instrument min size {minTradeAmount}. Order rejected.", LogMessageType.Error);
                    CreateOrderFail(order);
                    return;
                }

                if (order.TypeOrder == OrderPriceType.Limit)
                {
                    orderRequest.Add("px", TruncateToStep(order.Price, priceStep).ToString().Replace(",", "."));
                    orderRequest.Add("sz", orderVolume.ToString().Replace(",", "."));
                }
                else if (order.TypeOrder == OrderPriceType.Market)
                {
                    orderRequest.Add("tgtCcy", "base_ccy");
                    orderRequest.Add("sz", orderVolume.ToString().Replace(",", "."));
                }

                orderRequest.Add("tag", "5faf8b0e85c1BCDE");

                string json = JsonConvert.SerializeObject(orderRequest);

                string url = $"{_baseUrl}/api/v5/trade/order";

                HttpResponseMessage res = SendPrivatePost(url, json);
                string contentStr = res.Content.ReadAsStringAsync().Result;

                ResponseRestMessage<List<RestMessageSendOrder>> message = JsonConvert.DeserializeAnonymousType(contentStr, new ResponseRestMessage<List<RestMessageSendOrder>>());

                if (res.StatusCode == HttpStatusCode.OK)
                {
                    // OKX returns code "0" on success. Errors come with codes like "51008", "51001" etc.
                    if (message == null
                        || message.code.Equals("0") == false
                        || message.data == null
                        || message.data.Count == 0
                        || message.data[0].sCode.Equals("0") == false)
                    {
                        CreateOrderFail(order);

                        string errorText = message != null
                            && message.data != null
                            && message.data.Count > 0
                            && string.IsNullOrEmpty(message.data[0].sMsg) == false
                                ? message.data[0].sMsg
                                : contentStr;

                        SendLogMessage($"SendOrderSpot - {errorText}", LogMessageType.Error);
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
                SendLogMessage($"SendOrderSpot - {ex.ToString()}", LogMessageType.Error);
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

                decimal priceStep;
                decimal volumeStep;
                decimal minTradeAmount;
                GetOrderSteps(order.SecurityNameCode, out priceStep, out volumeStep, out minTradeAmount);

                if (order.TypeOrder == OrderPriceType.Limit)
                {
                    orderRequest.Add("px", TruncateToStep(order.Price, priceStep).ToString().Replace(",", "."));
                }

                decimal orderVolume = TruncateToStep(order.Volume, volumeStep);

                if (orderVolume <= 0
                    || (minTradeAmount > 0 && orderVolume < minTradeAmount))
                {
                    SendLogMessage($"SendOrderSwap - order size {order.Volume} ({order.SecurityNameCode}) is below the instrument min size {minTradeAmount}. Order rejected.", LogMessageType.Error);
                    CreateOrderFail(order);
                    return;
                }

                // options trade in contracts; futures/swaps convert base volume to contracts via ctVal
                decimal volume = order.SecurityClassCode.Contains("OPTION")
                    ? orderVolume
                    : orderVolume / GetVolume(order.SecurityNameCode);

                orderRequest.Add("sz", volume.ToString().Replace(",", "."));
                orderRequest.Add("posSide", posSide);
                orderRequest.Add("tag", "5faf8b0e85c1BCDE");

                string json = JsonConvert.SerializeObject(orderRequest);

                string url = $"{_baseUrl}/api/v5/trade/order";

                HttpResponseMessage res = SendPrivatePost(url, json);
                string contentStr = res.Content.ReadAsStringAsync().Result;

                if (res.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<RestMessageSendOrder>> message = JsonConvert.DeserializeAnonymousType(contentStr, new ResponseRestMessage<List<RestMessageSendOrder>>());

                    // OKX returns code "0" on success. Errors come with codes like "51008", "51001" etc.
                    if (message == null
                        || message.code.Equals("0") == false
                        || message.data == null
                        || message.data.Count == 0
                        || message.data[0].sCode.Equals("0") == false)
                    {
                        CreateOrderFail(order);

                        string errorText = message != null
                            && message.data != null
                            && message.data.Count > 0
                            && string.IsNullOrEmpty(message.data[0].sMsg) == false
                                ? message.data[0].sMsg
                                : contentStr;

                        SendLogMessage($"SendOrderSwap - {errorText}", LogMessageType.Error);
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
                SendLogMessage($"SendOrderSwap - {ex.ToString()}", LogMessageType.Error);
            }
        }

        // securities already logged for the volume fallback: the log goes out only once per instrument
        private HashSet<string> _volumeFallbackLogged = new HashSet<string>();

        // rounds the value down to a multiple of the instrument step.
        // OKX rejects orders whose price or size is not aligned to tickSz / lotSz.
        // decimal arithmetic is base-10, so already aligned values pass through unchanged
        private decimal TruncateToStep(decimal value, decimal step)
        {
            if (step <= 0)
            {
                return value;
            }

            return Math.Truncate(value / step) * step;
        }

        private void GetOrderSteps(string securityName, out decimal priceStep, out decimal volumeStep, out decimal minTradeAmount)
        {
            priceStep = 0;
            volumeStep = 0;
            minTradeAmount = 0;

            Security sec;
            if (_securitiesDict.TryGetValue(securityName, out sec))
            {
                priceStep = sec.PriceStep;
                volumeStep = sec.VolumeStep;
                minTradeAmount = sec.MinTradeAmount;
            }
        }

        private decimal GetVolume(string securityName)
        {
            decimal minVolume = 1;

            if (_securitiesDict.TryGetValue(securityName, out Security sec))
            {
                minVolume = sec.NameId.Split('_')[1].ToDecimal();
            }
            else
            {
                lock (_volumeFallbackLogged)
                {
                    if (_volumeFallbackLogged.Add(securityName))
                    {
                        SendLogMessage($"GetVolume: {securityName} is not in the securities dictionary. " +
                            "Volumes are counted with a contract value of 1. Reload securities to fix.", LogMessageType.Error);
                    }
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
            _rateGateCancelOrder.WaitToProceed();

            try
            {
                Dictionary<string, dynamic> orderRequest = new Dictionary<string, dynamic>();

                orderRequest.Add("instId", order.SecurityNameCode);
                orderRequest.Add("ordId", order.NumberMarket);

                string json = JsonConvert.SerializeObject(orderRequest);

                string url = $"{_baseUrl}/api/v5/trade/cancel-order";

                HttpResponseMessage res = SendPrivatePost(url, json);
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
                SendLogMessage($"CancelOrder - {ex.ToString()}", LogMessageType.Error);
            }
            return false;
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
        }

        public void CancelAllOrders()
        {
            List<Order> orders = GetAllActivOrdersArray(1000);

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
            List<Order> orders = GetAllActivOrdersArray(1000);

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

            GetAllOpenOrders(orders, maxCountByCategory);

            if (orders != null
                && orders.Count > 0)
            {
                ordersOpenAll.AddRange(orders);
            }

            ordersOpenAll = ordersOpenAll.OrderByDescending(order => order.TimeCreate).ToList();

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
                    List<MyTrade> tradesInOrder = GetMyTradesBySecurity(myOrder);

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
            try
            {
                string after = "";

                while (true)
                {
                    _rateGateOrdersPending.WaitToProceed();

                    string url = $"{_baseUrl}/api/v5/trade/orders-pending?limit=100";

                    if (string.IsNullOrEmpty(after) == false)
                    {
                        url += $"&after={after}";
                    }

                    HttpResponseMessage res = GetPrivateRequest(url);
                    string contentStr = res.Content.ReadAsStringAsync().Result;

                    if (res.StatusCode != HttpStatusCode.OK)
                    {
                        SendLogMessage($"Get all open orders request error {res.StatusCode} || {contentStr}", LogMessageType.Error);
                        return;
                    }

                    ResponseRestMessage<List<ResponseWsOrders>> OrderResponse = JsonConvert.DeserializeAnonymousType(contentStr, new ResponseRestMessage<List<ResponseWsOrders>>());

                    if (OrderResponse.code.Equals("0") == false)
                    {
                        SendLogMessage($"Get all open orders failed: {OrderResponse.code} || msg: {OrderResponse.msg}", LogMessageType.Error);
                        return;
                    }

                    if (OrderResponse.data == null
                        || OrderResponse.data.Count == 0)
                    {
                        return;
                    }

                    for (int i = 0; i < OrderResponse.data.Count; i++)
                    {
                        if (OrderResponse.data[i].ordType.Equals("limit") ||
                            OrderResponse.data[i].ordType.Equals("market"))
                        {
                            Order newOrder = OrderUpdate(OrderResponse.data[i]);

                            if (newOrder != null)
                            {
                                array.Add(newOrder);
                            }
                        }

                        if (array.Count >= maxCount)
                        {
                            while (array.Count > maxCount)
                            {
                                array.RemoveAt(array.Count - 1);
                            }
                            return;
                        }
                    }

                    if (OrderResponse.data.Count < 100)
                    {
                        // short page: the last one
                        return;
                    }

                    string newAfter = OrderResponse.data[OrderResponse.data.Count - 1].ordId;

                    if (newAfter == after)
                    {
                        // the exchange returned the same page again: stop to avoid an infinite loop
                        return;
                    }

                    after = newAfter;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"GetActivOrders - {ex.ToString()}", LogMessageType.Error);
            }
        }

        private RateGate _rateGateGenerateToTrade = new RateGate(1, TimeSpan.FromMilliseconds(200));

        private List<MyTrade> GetMyTradesBySecurity(Order order)
        {
            try
            {
                _rateGateGenerateToTrade.WaitToProceed();

                string TypeInstr = "SPOT";

                if (order.SecurityNameCode.EndsWith("SWAP"))
                {
                    TypeInstr = "SWAP";
                }
                else if (order.SecurityNameCode.EndsWith("-C")
                    || order.SecurityNameCode.EndsWith("-P"))
                {
                    // option instId, e.g. BTC-USD-241227-30000-C
                    TypeInstr = "OPTION";
                }
                else if (order.SecurityNameCode.Count(c => c == '-') == 2)
                {
                    TypeInstr = "FUTURES";
                }

                string url = $"{_baseUrl}/api/v5/trade/fills-history?ordId={order.NumberMarket}&instId={order.SecurityNameCode}&instType={TypeInstr}";

                HttpResponseMessage res = GetPrivateRequest(url);

                string contentStr = res.Content.ReadAsStringAsync().Result;

                if (res.StatusCode == HttpStatusCode.OK)
                {
                    TradeDetailsResponse quotes = JsonConvert.DeserializeAnonymousType(contentStr, new TradeDetailsResponse());

                    if (quotes == null
                        || string.IsNullOrEmpty(quotes.code))
                    {
                        SendLogMessage($"Get my trades by security error: unexpected response || {contentStr}", LogMessageType.Error);
                        return null;
                    }

                    if (quotes.code.Equals("0"))
                    {
                        List<MyTrade> myTrades = new List<MyTrade>();

                        if (quotes.data == null)
                        {
                            return myTrades;
                        }

                        for (int i = 0; i < quotes.data.Count; i++)
                        {
                            TradeDetailsObject item = quotes.data[i];

                            MyTrade myTrade = new MyTrade();

                            myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts));
                            myTrade.SecurityNameCode = item.instId;
                            myTrade.NumberOrderParent = item.ordId.ToString();
                            myTrade.NumberTrade = item.tradeId.ToString();

                            if (item.instId.Contains("SWAP")
                                || item.instType == "FUTURES")
                            {
                                myTrade.Volume = item.fillSz.ToDecimal() * GetVolume(item.instId);
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
                SendLogMessage($"GenerateTradesToOrder - {ex.ToString()}", LogMessageType.Error);

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

            List<Order> swapOrders = new List<Order>();
            GetAllHistoricalOrders(swapOrders, maxCountByCategory, "SWAP");

            if (swapOrders != null
                && swapOrders.Count > 0)
            {
                ordersOpenAll.AddRange(swapOrders);
            }

            List<Order> spotOrders = new List<Order>();
            GetAllHistoricalOrders(spotOrders, maxCountByCategory, "SPOT");

            if (spotOrders != null
                && spotOrders.Count > 0)
            {
                ordersOpenAll.AddRange(spotOrders);
            }

            List<Order> futuresOrders = new List<Order>();
            GetAllHistoricalOrders(futuresOrders, maxCountByCategory, "FUTURES");

            if (futuresOrders != null
                && futuresOrders.Count > 0)
            {
                ordersOpenAll.AddRange(futuresOrders);
            }

            if (_useOptions)
            {
                List<Order> optionOrders = new List<Order>();
                GetAllHistoricalOrders(optionOrders, maxCountByCategory, "OPTION");

                if (optionOrders != null
                    && optionOrders.Count > 0)
                {
                    ordersOpenAll.AddRange(optionOrders);
                }
            }

            ordersOpenAll = ordersOpenAll.OrderByDescending(order => order.TimeCreate).ToList();

            return ordersOpenAll;
        }

        private void GetAllHistoricalOrders(List<Order> array, int maxCount, string instType)
        {
            try
            {
                string after = "";

                while (true)
                {
                    _rateGateOrdersHistory.WaitToProceed();

                    string url = $"{_baseUrl}/api/v5/trade/orders-history?instType={instType}&limit=100";

                    if (string.IsNullOrEmpty(after) == false)
                    {
                        url += $"&after={after}";
                    }

                    HttpResponseMessage res = GetPrivateRequest(url);
                    string contentStr = res.Content.ReadAsStringAsync().Result;

                    if (res.StatusCode != HttpStatusCode.OK)
                    {
                        SendLogMessage($"Get all historical orders request error. Code: {res.StatusCode} || {contentStr}", LogMessageType.Error);
                        return;
                    }

                    ResponseRestMessage<List<ResponseWsOrders>> OrderResponse = JsonConvert.DeserializeAnonymousType(contentStr, new ResponseRestMessage<List<ResponseWsOrders>>());

                    if (OrderResponse.code.Equals("0") == false)
                    {
                        SendLogMessage($"Get all historical orders request error. {OrderResponse.code} || {OrderResponse.msg}", LogMessageType.Error);
                        return;
                    }

                    if (OrderResponse.data == null
                        || OrderResponse.data.Count == 0)
                    {
                        return;
                    }

                    for (int i = 0; i < OrderResponse.data.Count; i++)
                    {
                        if (OrderResponse.data[i].ordType.Equals("limit") ||
                            OrderResponse.data[i].ordType.Equals("market"))
                        {
                            Order newOrder = OrderUpdate(OrderResponse.data[i]);

                            if (newOrder != null)
                            {
                                array.Add(newOrder);
                            }
                        }

                        if (array.Count >= maxCount)
                        {
                            while (array.Count > maxCount)
                            {
                                array.RemoveAt(array.Count - 1);
                            }
                            return;
                        }
                    }

                    if (OrderResponse.data.Count < 100)
                    {
                        // short page: the last one
                        return;
                    }

                    string newAfter = OrderResponse.data[OrderResponse.data.Count - 1].ordId;

                    if (newAfter == after)
                    {
                        // the exchange returned the same page again: stop to avoid an infinite loop
                        return;
                    }

                    after = newAfter;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"GetAllHistoricalOrders - {ex.ToString()}", LogMessageType.Error);
            }
        }

        #endregion

        #region 12 Queries

        private HttpClient _httpClient;

        // one client per realization instance: HttpClient is thread-safe and pools connections.
        // credentials and proxy are fixed for the realization lifetime
        private HttpClient GetHttpClient()
        {
            if (_httpClient == null)
            {
                _httpClient = new HttpClient(new HttpInterceptor(_publicKey, _secretKey, _password, _demoMode, _myProxy));
            }

            return _httpClient;
        }

        private HttpResponseMessage SendPrivatePost(string url, string json)
        {
            HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            return GetHttpClient().SendAsync(httpRequest).Result;
        }

        public HttpResponseMessage GetPrivateRequest(string url)
        {
            return GetHttpClient().GetAsync(url).Result;
        }

        public void SetLeverage(Security security, decimal leverage) { }

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