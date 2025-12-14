using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using OsEngine.Market.Servers.KiteConnect.Json;
using Newtonsoft.Json;
using OsEngine.Entity.WebSocketOsEngine;


namespace OsEngine.Market.Servers.KiteConnect
{
    public class KiteConnectServer : AServer
    {
        public KiteConnectServer()
        {
            KiteServerRealization realization = new KiteServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterString("Request Token", "");
            CreateParameterPassword("Access Token", "");
            CreateParameterBoolean("A subscription for downloading historical data has been issued", false);
            CreateParameterBoolean("Exchange NSE", false);
            CreateParameterBoolean("Exchange BSE", false);
            CreateParameterBoolean("Exchange NFO", false);
            CreateParameterBoolean("Exchange CDS", false);
            CreateParameterBoolean("Exchange BFO", false);
            CreateParameterBoolean("Exchange MCX", false);
            CreateParameterBoolean("Exchange BCD", false);
        }
    }

    public class KiteServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public KiteServerRealization()
        {
            Thread threadUpdatePortfolio = new Thread(GetUpdatePortfolio);
            threadUpdatePortfolio.Name = "UpdatePortfolioKiteConnect";
            threadUpdatePortfolio.Start();

            Thread threadMessageReader = new Thread(MessageReader);
            threadMessageReader.Name = "MessageReaderKiteConnect";
            threadMessageReader.Start();
        }

        public void Connect(WebProxy proxy)
        {
            try
            {
                _apiKey = ((ServerParameterString)ServerParameters[0]).Value;
                _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
                _requestToken = ((ServerParameterString)ServerParameters[2]).Value;
                _accessToken = ((ServerParameterPassword)ServerParameters[3]).Value;
                _useSubscribeToHistoricalData = ((ServerParameterBool)ServerParameters[4]).Value;

                if (string.IsNullOrEmpty(_apiKey) ||
                    string.IsNullOrEmpty(_secretKey))
                {
                    SendLogMessage("Can`t run Kite connector. No keys", LogMessageType.Error);
                    return;
                }

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest("/user/profile", Method.GET);
                request.AddHeader("Authorization", "token " + _apiKey + ":" + _accessToken);
                request.AddHeader("X-Kite-Version", "3");

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    //ResponseRestKite<UserProfile> responseUserProfile = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestKite<UserProfile>());
                }
                else
                {
                    ResponseRestKite<string> errorUser = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestKite<string>());

                    if (errorUser.status == "error")
                    {
                        if (GetCurSessionToken() == false)
                        {
                            SendLogMessage("Authorization Error. Probably an invalid token is specified.",
                            LogMessageType.Error);
                            return;
                        }
                    }
                    else
                    {
                        SendLogMessage($"Error requesting user profile. Status: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                    }
                }

                _listWebSocket = new List<WebSocket>();

                CreateWebSocketConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message.ToString(), LogMessageType.Error);
            }
        }

        private bool GetCurSessionToken()
        {
            try
            {
                string checksum = SHA256Hash(_apiKey + _requestToken + _secretKey);

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest("/session/token", Method.POST);
                request.AddParameter("api_key", _apiKey);
                request.AddParameter("request_token", _requestToken);
                request.AddParameter("checksum", checksum);
                request.AddHeader("Authorization", "token " + _apiKey + ":" + _accessToken);
                request.AddHeader("X-Kite-Version", "3");
                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestKite<UserAuthentication> responseUserAuthentication = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestKite<UserAuthentication>());

                    if (responseUserAuthentication.status == "success")
                    {
                        _accessToken = responseUserAuthentication.data.access_token;
                        ((ServerParameterPassword)ServerParameters[3]).Value = _accessToken;
                    }
                    else
                    {
                        SendLogMessage($"Access token error type: {responseUserAuthentication.error_type}, {responseUserAuthentication.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Authentication request failed. Status: {response.StatusCode}, {response.Content}", LogMessageType.Error);
                }

                return true;
            }
            catch (Exception exception)
            {
                SendLogMessage("Token request error: " + exception.ToString(), LogMessageType.Error);
                return false;
            }
        }

        public void Dispose()
        {
            unsubscribeFromAllWebSockets();

            _myPortfolios.Clear();

            DeleteWebSocketConnection();

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void unsubscribeFromAllWebSockets()
        {
            if (_listWebSocket == null)
            {
                return;
            }

            for (int j = 0; j < _listWebSocket.Count; j++)
            {
                if (_listWebSocket[j] == null)
                {
                    continue;
                }
                if (_listWebSocket[j].ReadyState != WebSocketState.Open)
                {
                    continue;
                }
                for (int i = 0; i < _securities.Count; i++)
                {
                    string instrumentToken = _securities[i].NameId.Split('_')[0];
                    _listWebSocket[j]?.SendAsync("{\"a\":\"unsubscribe\",\"v\":[" + instrumentToken + "]}");
                }
            }            
        }

        public DateTime ServerTime { get; set; }

        public ServerType ServerType
        {
            get { return ServerType.KiteConnect; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion 1

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private static string _apiKey;

        private string _secretKey;

        private string _baseUrl = "https://api.kite.trade";

        private string _webSocketUrl = "wss://ws.kite.trade";

        private string _accessToken;

        private string _requestToken;

        private bool _useSubscribeToHistoricalData = false;

        private bool _useExchangeNSE = false;

        private bool _useExchangeBSE = false;

        private bool _useExchangeNFO = false;

        private bool _useExchangeCDS = false;

        private bool _useExchangeBFO = false;

        private bool _useExchangeMCX = false;

        private bool _useExchangeBCD = false;

        private ConcurrentQueue<string> _fifoListWebSocketMessage = new ConcurrentQueue<string>();

        private RateGate _rateGateQuote = new RateGate(1, TimeSpan.FromMilliseconds(1000));

        private RateGate _rateGateHistoricalCandle = new RateGate(3, TimeSpan.FromMilliseconds(1000));

        private RateGate _rateGateOrder = new RateGate(10, TimeSpan.FromMilliseconds(1000));

        private RateGate _rateGateAllOthers = new RateGate(10, TimeSpan.FromMilliseconds(1000));

        #endregion 2

        #region 3 Securities

        public void GetSecurities()
        {
            _useExchangeNSE = ((ServerParameterBool)ServerParameters[5]).Value;
            _useExchangeBSE = ((ServerParameterBool)ServerParameters[6]).Value;
            _useExchangeNFO = ((ServerParameterBool)ServerParameters[7]).Value;
            _useExchangeCDS = ((ServerParameterBool)ServerParameters[8]).Value;
            _useExchangeBFO = ((ServerParameterBool)ServerParameters[9]).Value;
            _useExchangeMCX = ((ServerParameterBool)ServerParameters[10]).Value;
            _useExchangeBCD = ((ServerParameterBool)ServerParameters[11]).Value;

            if (_useExchangeNSE)
            {
                UpdateSecurity("NSE");
            }

            if (_useExchangeBSE)
            {
                UpdateSecurity("BSE");
            }

            if (_useExchangeNFO)
            {
                UpdateSecurity("NFO");
            }

            if (_useExchangeCDS)
            {
                UpdateSecurity("CDS");
            }

            if (_useExchangeBFO)
            {
                UpdateSecurity("BFO");
            }

            if (_useExchangeMCX)
            {
                UpdateSecurity("MCX");
            }

            if (_useExchangeBCD)
            {
                UpdateSecurity("BCD");
            }

            if (_securities.Count > 0)
            {
                SendLogMessage("Securities loaded. Count: " + _securities.Count, LogMessageType.System);

                if (SecurityEvent != null)
                {
                    SecurityEvent.Invoke(_securities);
                }
            }
        }

        private void UpdateSecurity(string exchange)
        {
            _rateGateAllOthers.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest($"/instruments/{exchange}", Method.GET);
                request.AddHeader("Authorization", "token " + _apiKey + ":" + _accessToken);
                request.AddHeader("X-Kite-Version", "3");
                IRestResponse response = client.Execute(request);

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    List<List<string>> securitiesArea = ParseCSV(response.Content);

                    for (int i = 1; i < securitiesArea.Count - 1; i++)
                    {
                        List<string> security = securitiesArea[i];

                        if (security.Count < 12)
                        {
                            continue;
                        }

                        SecurityType instrumentType = GetSecurityType(security[9]);

                        if (instrumentType == SecurityType.None)
                        {
                            continue;
                        }

                        Security newSecurity = new Security();

                        newSecurity.Exchange = ServerType.KiteConnect.ToString();
                        newSecurity.Name = $"{security[2]}_{security[9]}_{security[11]}";
                        newSecurity.NameFull = security[0];
                        newSecurity.NameClass = $"{security[11]}_{security[9]}";
                        newSecurity.NameId = $"{security[0]}_{security[1]}";
                        newSecurity.SecurityType = instrumentType;

                        if (newSecurity.SecurityType == SecurityType.Option)
                        {
                            newSecurity.Strike = security[6].ToDecimal();
                        }

                        if (newSecurity.SecurityType == SecurityType.Option
                            || newSecurity.SecurityType == SecurityType.Futures)
                        {
                            newSecurity.Expiration = DateTimeOffset.Parse(security[5]).DateTime;
                        }

                        newSecurity.DecimalsVolume = 0;
                        newSecurity.Lot = security[8].ToDecimal();
                        newSecurity.PriceStep = security[7].ToDecimal();

                        if (newSecurity.PriceStep == 0)
                        {
                            continue;
                        }

                        newSecurity.Decimals = security[7].DecimalsCount();
                        newSecurity.PriceStepCost = newSecurity.PriceStep;
                        newSecurity.State = SecurityStateType.Activ;
                        newSecurity.VolumeStep = security[8].ToDecimal();
                        newSecurity.MinTradeAmount = security[8].ToDecimal();
                        newSecurity.MinTradeAmountType = MinTradeAmountType.Contract;

                        _securities.Add(newSecurity);
                    }
                }
                else
                {
                    SendLogMessage($"Error requesting securities. Status: {response.StatusCode}, {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Error requesting securities: " + exception.ToString(), LogMessageType.Error);
            }
        }

        private SecurityType GetSecurityType(string security)
        {
            if (security.StartsWith("FUT"))
            {
                return SecurityType.Futures;
            }
            else if (security.StartsWith("EQ"))
            {
                return SecurityType.Stock;
            }
            else if (security.StartsWith("CE") || security.StartsWith("PE"))
            {
                return SecurityType.Option;
            }

            return SecurityType.None;
        }

        private List<List<string>> ParseCSV(string content)
        {
            string[] lines = content.Split('\n');

            List<List<string>> contents = new List<List<string>>();

            for (int i = 0; i < lines.Length; i++)
            {
                string[] fields = lines[i].Split(new[] { ',' }, StringSplitOptions.None);
                List<string> item = new List<string>(fields);
                contents.Add(item);
            }

            return contents;
        }

        private List<Security> _securities = new List<Security>();

        public event Action<List<Security>> SecurityEvent;

        #endregion 3

        #region 4 Portfolios

        public void GetPortfolios()
        {
            _rateGateAllOthers.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest("/user/margins", Method.GET);
                request.AddHeader("Authorization", "token " + _apiKey + ":" + _accessToken);
                request.AddHeader("X-Kite-Version", "3");
                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestKite<UserMargins> responsePortfolio = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestKite<UserMargins>());

                    if (responsePortfolio.status == "success")
                    {
                        if (Convert.ToBoolean(responsePortfolio.data.equity.enabled) == true)
                        {
                            Portfolio equityPortfolio = new Portfolio();
                            equityPortfolio.Number = "EquityPortfolio";
                            equityPortfolio.ValueBegin = responsePortfolio.data.equity.available.opening_balance.ToDecimal();
                            equityPortfolio.ValueCurrent = responsePortfolio.data.equity.available.live_balance.ToDecimal();
                            equityPortfolio.ValueBlocked = 0;
                            _myPortfolios.Add(equityPortfolio);
                        }
                        if (Convert.ToBoolean(responsePortfolio.data.commodity.enabled) == true)
                        {
                            Portfolio commodityPortfolio = new Portfolio();
                            commodityPortfolio.Number = "CommodityPortfolio";
                            commodityPortfolio.ValueBegin = responsePortfolio.data.equity.available.opening_balance.ToDecimal();
                            commodityPortfolio.ValueCurrent = responsePortfolio.data.equity.available.live_balance.ToDecimal();
                            commodityPortfolio.ValueBlocked = 0;
                            _myPortfolios.Add(commodityPortfolio);
                        }

                        Portfolio holdings = new Portfolio();
                        holdings.Number = "HoldingsPortfolio";
                        holdings.ValueBegin = 0;
                        holdings.ValueCurrent = 0;
                        holdings.ValueBlocked = 0;
                        _myPortfolios.Add(holdings);

                        if (PortfolioEvent != null)
                        {
                            PortfolioEvent(_myPortfolios);
                        }

                        firstPortfolio = true;
                    }
                    else
                    {
                        SendLogMessage($"Portfolio error type: {responsePortfolio.error_type}, {responsePortfolio.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Portfolio request error. Status: {response.StatusCode}, {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Portfolio request error " + exception.ToString(), LogMessageType.Error);
            }
        }

        private void GetUpdatePortfolio()
        {
            while (true)
            {
                Thread.Sleep(5000);

                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    continue;
                }

                UpdatePortfolios();
                UpdateHoldingsPortfolio();
                UpdatePositions();
            }
        }

        private void UpdatePortfolios()
        {
            if (!firstPortfolio)
            {
                return;
            }

            _rateGateAllOthers.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest("/user/margins", Method.GET);
                request.AddHeader("Authorization", "token " + _apiKey + ":" + _accessToken);
                request.AddHeader("X-Kite-Version", "3");
                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestKite<UserMargins> responsePortfolio = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestKite<UserMargins>());

                    if (responsePortfolio.status == "success")
                    {
                        for (int i = 0; i < _myPortfolios.Count; i++)
                        {
                            if (_myPortfolios[i].Number == "EquityPortfolio")
                            {
                                _myPortfolios[i].Number = "EquityPortfolio";
                                _myPortfolios[i].ValueBegin = responsePortfolio.data.equity.available.opening_balance.ToDecimal();
                                _myPortfolios[i].ValueCurrent = responsePortfolio.data.equity.available.live_balance.ToDecimal();
                                _myPortfolios[i].ValueBlocked = 0;
                            }

                            if (_myPortfolios[i].Number == "CommodityPortfolio")
                            {
                                _myPortfolios[i].Number = "CommodityPortfolio";
                                _myPortfolios[i].ValueBegin = responsePortfolio.data.equity.available.opening_balance.ToDecimal();
                                _myPortfolios[i].ValueCurrent = responsePortfolio.data.equity.available.live_balance.ToDecimal();
                                _myPortfolios[i].ValueBlocked = 0;
                            }

                            if (_myPortfolios[i].Number == "HoldingsPortfolio")
                            {
                                _myPortfolios[i].Number = "HoldingsPortfolio";
                                _myPortfolios[i].ValueBegin = 0;
                                _myPortfolios[i].ValueCurrent = 0;
                            }
                        }

                        if (PortfolioEvent != null)
                        {
                            PortfolioEvent(_myPortfolios);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Portfolio update error type: {responsePortfolio.error_type}, {responsePortfolio.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Portfolio update request error. Status: {response.StatusCode}, {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Portfolio update request error " + exception.ToString(), LogMessageType.Error);
            }
        }

        private bool firstPortfolio = false;

        private void UpdateHoldingsPortfolio()
        {
            if (!firstPortfolio)
            {
                return;
            }

            _rateGateAllOthers.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest("/portfolio/holdings", Method.GET);
                request.AddHeader("Authorization", "token " + _apiKey + ":" + _accessToken);
                request.AddHeader("X-Kite-Version", "3");
                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestKite<List<HoldingsPortfolio>> responsePortfolio = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestKite<List<HoldingsPortfolio>>());

                    if (responsePortfolio.data.Count == 0)
                    {
                        return;
                    }

                    if (responsePortfolio.status == "success")
                    {
                        Portfolio portf = null;

                        List<PositionOnBoard> sectionPoses = new List<PositionOnBoard>();

                        for (int i = 0; i < _myPortfolios.Count; i++)
                        {
                            if (_myPortfolios[i].Number == "HoldingsPortfolio")
                            {
                                portf = _myPortfolios[i];
                            }
                        }

                        if (portf == null)
                        {
                            return;
                        }

                        for (int i = 0; i < responsePortfolio.data.Count; i++)
                        {
                            PositionOnBoard newPos = new PositionOnBoard();
                            newPos.PortfolioName = portf.Number;
                            newPos.ValueCurrent = responsePortfolio.data[i].quantity.ToDecimal();
                            newPos.SecurityNameCode = responsePortfolio.data[i].tradingsymbol;
                            sectionPoses.Add(newPos);
                        }

                        for (int i = 0; i < sectionPoses.Count; i++)
                        {
                            portf.SetNewPosition(sectionPoses[i]);
                        }

                        if (PortfolioEvent != null)
                        {
                            PortfolioEvent(_myPortfolios);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Portfolio Holdings error type: {responsePortfolio.error_type}, {responsePortfolio.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Portfolio Holdings request error. Status: {response.StatusCode}, {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Portfolio Holdings request error " + exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdatePositions()
        {
            if (!firstPortfolio)
            {
                return;
            }

            _rateGateAllOthers.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest("/portfolio/positions", Method.GET);

                request.AddHeader("Authorization", "token " + _apiKey + ":" + _accessToken);
                request.AddHeader("X-Kite-Version", "3");
                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestKite<ResponsePositions> responsePositions = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestKite<ResponsePositions>());

                    if (responsePositions.data.day.Count == 0
                        && responsePositions.data.net.Count == 0)
                    {
                        return;
                    }

                    if (responsePositions.status == "success")
                    {
                        Portfolio portf = null;

                        List<PositionOnBoard> sectionPoses = new List<PositionOnBoard>();

                        for (int i = 0; i < _myPortfolios.Count; i++)
                        {
                            if (_myPortfolios[i].Number == "EquityPortfolio")
                            {
                                portf = _myPortfolios[i];

                                for (int j = 0; j < responsePositions.data.net.Count; j++)
                                {
                                    PositionOnBoard newPos = new PositionOnBoard();
                                    newPos.PortfolioName = portf.Number;
                                    newPos.ValueCurrent = responsePositions.data.net[j].quantity.ToDecimal();
                                    newPos.SecurityNameCode = responsePositions.data.net[j].tradingsymbol;
                                    sectionPoses.Add(newPos);
                                }
                            }
                            else if (_myPortfolios[i].Number == "CommodityPortfolio")
                            {
                                portf = _myPortfolios[i];

                                for (int j = 0; j < responsePositions.data.net.Count; j++)
                                {
                                    PositionOnBoard newPos = new PositionOnBoard();
                                    newPos.PortfolioName = portf.Number;
                                    newPos.ValueCurrent = responsePositions.data.net[j].quantity.ToDecimal();
                                    newPos.SecurityNameCode = responsePositions.data.net[j].tradingsymbol;
                                    sectionPoses.Add(newPos);
                                }
                            }
                        }

                        for (int i = 0; i < sectionPoses.Count; i++)
                        {
                            portf.SetNewPosition(sectionPoses[i]);
                        }

                        if (PortfolioEvent != null)
                        {
                            PortfolioEvent(_myPortfolios);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Positions error type: {responsePositions.error_type}, {responsePositions.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Error requesting positions. Status: {response.StatusCode}, {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Error requesting positions " + exception.ToString(), LogMessageType.Error);
            }
        }

        private List<Portfolio> _myPortfolios = new List<Portfolio>();

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion 4

        #region 5 Data

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        private bool candleHistory = false;

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            DateTime endTime = DateTime.UtcNow;

            int candlesInDay = 0;

            if (timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes >= 1)
            {
                candlesInDay = 420 / Convert.ToInt32(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes);
            }
            else
            {
                candlesInDay = 25200 / Convert.ToInt32(timeFrameBuilder.TimeFrameTimeSpan.TotalSeconds);
            }

            if (candlesInDay == 0)
            {
                candlesInDay = 1;
            }

            int daysCount = candleCount / candlesInDay;

            if (daysCount == 0)
            {
                daysCount = 1;
            }

            daysCount++;

            if (daysCount > 5)
            {
                daysCount = daysCount + (daysCount / 5) * 2;
            }

            DateTime startTime = endTime.AddDays(-daysCount);

            if (endTime.DayOfWeek == DayOfWeek.Monday)
            {
                startTime = startTime.AddDays(-2);
            }
            if (endTime.DayOfWeek == DayOfWeek.Tuesday)
            {
                startTime = startTime.AddDays(-1);
            }

            candleHistory = true;

            List<Candle> candles = GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, startTime);

            if (candles == null)
            {
                return null;
            }

            while (candles.Count > candleCount)
            {
                candles.RemoveAt(0);
            }

            return candles;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (!_useSubscribeToHistoricalData)
            {
                return null;
            }

            if (timeFrameBuilder.TimeFrame == TimeFrame.Min2
                || timeFrameBuilder.TimeFrame == TimeFrame.Min10
                || timeFrameBuilder.TimeFrame == TimeFrame.Hour2
                || timeFrameBuilder.TimeFrame == TimeFrame.Hour4)
            {
                return null;
            }

            startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
            endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
            actualTime = DateTime.SpecifyKind(actualTime, DateTimeKind.Utc);

            if (!CheckTime(startTime, endTime, actualTime))
            {
                return null;
            }

            if (candleHistory)
            {
                startTime = ConvertToIST(startTime);
                endTime = ConvertToIST(endTime);
                actualTime = ConvertToIST(actualTime);
            }

            if (startTime != actualTime)
            {
                startTime = actualTime;
            }

            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
            string tf = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);

            List<Candle> allCandles = new List<Candle>();

            double countDays = (endTime - startTime).TotalDays;
            DateTime startTimeData = startTime;
            DateTime endTimeData = endTime;

            if (countDays > 59)
            {
                countDays = 59;
            }

            /*if (timeFrameBuilder.TimeFrame != TimeFrame.Day)
            {
                endTimeData = startTimeData.AddDays(countDays + 1);
            }
            else
            {
                endTimeData = startTimeData.AddDays(countDays);
            }*/

            endTimeData = startTimeData.AddDays(countDays);

            while (countDays > 0)
            {

                List<Candle> candles = RequestCandleHistory(security, tf, startTimeData, endTimeData);

                if (candles == null)
                {
                    return null;
                }

                if (candles.Count == 0)
                {
                    countDays = 0;
                    continue;
                }

                allCandles.AddRange(candles);

                startTimeData = allCandles[allCandles.Count - 1].TimeStart.AddMinutes(tfTotalMinutes);
                countDays = (endTime - startTimeData).TotalDays;

                if (countDays > 59)
                {
                    countDays = 59;
                }

                if (timeFrameBuilder.TimeFrame != TimeFrame.Day)
                {
                    endTimeData = startTimeData.AddDays(countDays);
                }
                else
                {
                    endTimeData = startTimeData.AddDays(countDays);
                }
            }

            while (allCandles != null &&
                allCandles.Count != 0 &&
                allCandles[0].TimeStart < startTime)
            {
                allCandles.RemoveAt(0);
            }

            if (allCandles != null && allCandles.Count > 0)
            {
                for (int i = 1; i < allCandles.Count; i++)
                {
                    if (allCandles[i - 1].TimeStart == allCandles[i].TimeStart)
                    {
                        allCandles.RemoveAt(i);
                        i--;
                    }
                }
            }

            return allCandles;
        }

        private List<Candle> RequestCandleHistory(Security security, string tf, DateTime startTime, DateTime endTime)
        {
            _rateGateHistoricalCandle.WaitToProceed();

            try
            {
                string end = endTime.ToString("yyyy-MM-dd HH:mm:ss");
                string start = startTime.ToString("yyyy-MM-dd HH:mm:ss");

                string instrumentToken = security.NameId.Split('_')[0];
                string path = $"/instruments/historical/{instrumentToken}/{tf}";

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(path, Method.GET);
                request.AddHeader("Authorization", "token " + _apiKey + ":" + _accessToken);
                request.AddHeader("X-Kite-Version", "3");
                request.AddParameter("from", start);
                request.AddParameter("to", end);
                request.AddParameter("interval", tf);
                request.AddParameter("instrument_token", instrumentToken);
                request.AddParameter("continuous", 0);
                request.AddParameter("oi", 1);
                IRestResponse response = client.Execute(request);

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    ResponseRestKite<HistoricalCandles> responseCandles = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestKite<HistoricalCandles>());

                    if (responseCandles.status == "success")
                    {
                        List<Candle> result = new List<Candle>();

                        for (int i = 0; i < responseCandles.data.candles.Count; i++)
                        {
                            string[] curCandle = responseCandles.data.candles[i];

                            if (CheckCandlesToZeroData(curCandle))
                            {
                                continue;
                            }

                            Candle newCandle = new Candle();
                            newCandle.Open = curCandle[1].ToDecimal();
                            newCandle.High = curCandle[2].ToDecimal();
                            newCandle.Low = curCandle[3].ToDecimal();
                            newCandle.Close = curCandle[4].ToDecimal();
                            newCandle.Volume = curCandle[5].ToDecimal();
                            newCandle.TimeStart = DateTimeOffset.Parse(curCandle[0]).DateTime;

                            result.Add(newCandle);
                        }

                        return result;
                    }
                    else
                    {
                        SendLogMessage($"Candle request error type: {responseCandles.error_type}, {responseCandles.message}", LogMessageType.Error);
                        return null;
                    }
                }
                else
                {
                    SendLogMessage($"Candle request error. Status: {response.StatusCode}, {response.Content}", LogMessageType.Error);
                }
                return null;
            }
            catch (Exception exception)
            {
                SendLogMessage("Candle request error " + exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private bool CheckCandlesToZeroData(string[] item)
        {
            if (item[1].ToDecimal() == 0
                || item[2].ToDecimal() == 0
                || item[3].ToDecimal() == 0
                || item[4].ToDecimal() == 0
                || item[5].ToDecimal() == 0)
            {
                return true;
            }
            return false;
        }

        private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (startTime > endTime ||
                startTime >= DateTime.UtcNow ||
                actualTime > endTime ||
                actualTime > DateTime.UtcNow)
            {
                return false;
            }
            return true;
        }

        private string GetInterval(TimeSpan timeFrame)
        {
            switch (timeFrame.TotalMinutes)
            {
                case 1:
                    return "minute";
                case 2:
                    return "2minute";
                case 3:
                    return "3minute";
                case 5:
                    return "5minute";
                case 10:
                    return "10minute";
                case 15:
                    return "15minute";
                case 30:
                    return "30minute";
                case 60:
                    return "60minute";
                case 120:
                    return "2hour";
                case 240:
                    return "4hour";
                case 1440:
                    return "day";
                default:
                    return null;
            }
        }

        #endregion 5

        #region 6 WebSocket creation

        private List<WebSocket> _listWebSocket;

        private WebSocket _webSocket;

        private void CreateWebSocketConnection()
        {
            CreateNewWebSocketConnection();      
        }

        private void CreateNewWebSocketConnection()
        {
            try
            {
                string fullUrlWebSoket = $"{_webSocketUrl}/?api_key={_apiKey}&access_token={_accessToken}";

                _webSocket = new WebSocket(fullUrlWebSoket);

                /*_webSocket.SslConfiguration.EnabledSslProtocols
                = System.Security.Authentication.SslProtocols.None
                | System.Security.Authentication.SslProtocols.Tls12
                | System.Security.Authentication.SslProtocols.Tls13;*/

                _webSocket.EmitOnPing = true;
                _webSocket.OnOpen += _webSocket_OnOpen;
                _webSocket.OnMessage += _webSocket_OnMessage;
                _webSocket.OnError += _webSocket_OnError;
                _webSocket.OnClose += _webSocket_OnClose;

                _webSocket.ConnectAsync();

                _listWebSocket.Add(_webSocket);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void DeleteWebSocketConnection()
        {
            if (_listWebSocket == null)
            {
                return;
            }

            for (int i = 0; i < _listWebSocket.Count; i++)
            {
                if (_listWebSocket[i] != null)
                {
                    try
                    {
                        _listWebSocket[i].OnOpen -= _webSocket_OnOpen;
                        _listWebSocket[i].OnMessage -= _webSocket_OnMessage;
                        _listWebSocket[i].OnError -= _webSocket_OnError;
                        _listWebSocket[i].OnClose -= _webSocket_OnClose;
                        _listWebSocket[i].CloseAsync();
                    }
                    catch
                    {
                        // ignore
                    }
                    _listWebSocket[i] = null;
                }
            }
        }

        #endregion 6

        #region 7 WebSocket events

        private void _webSocket_OnClose(object sender, CloseEventArgs e)
        {
            if (ServerStatus == ServerConnectStatus.Connect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;

                SendLogMessage("Connection Closed by KiteConnect. WebSocket Data Closed Event " + e.Code + " " + e.Reason, LogMessageType.System);

                if (DisconnectEvent != null)
                {
                    DisconnectEvent();
                }
            }
        }

        private void _webSocket_OnError(object sender, ErrorEventArgs e)
        {
            SendLogMessage("Error websocket :" + e.Exception.ToString(), LogMessageType.Error);
        }

        private void _webSocket_OnMessage(object sender, MessageEventArgs e)
        {
            if (ServerStatus != ServerConnectStatus.Connect)
            {
                return;
            }

            if (e.IsBinary)
            {
                DecryptData(e.RawData);
            }
            else if (e.IsText)
            {
                if (_fifoListWebSocketMessage != null)
                {
                    _fifoListWebSocketMessage?.Enqueue(e.Data);
                }
            }
        }

        private void _webSocket_OnOpen(object sender, EventArgs e)
        {
            SendLogMessage("Websockets activate. Connection status", LogMessageType.System);

            if (ServerStatus != ServerConnectStatus.Connect
            && _webSocket != null
            && _webSocket.ReadyState == WebSocketState.Open)
            {
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent();
            }
        }

        #endregion 7

        #region 8 WebSocket Security subscribe

        List<Security> _subscribedSecurities = new List<Security>();

        public void Subscribe(Security security)
        {
            try
            {
                _rateGateAllOthers.WaitToProceed();

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (_listWebSocket.Count == 0)
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

                WebSocket webSocket = _listWebSocket[_listWebSocket.Count - 1];

                if (webSocket.ReadyState == WebSocketState.Open
                    && _subscribedSecurities.Count != 0
                    && _subscribedSecurities.Count % 500 == 0)
                {
                    // creating a new socket
                    CreateNewWebSocketConnection();

                    DateTime timeEnd = DateTime.Now.AddSeconds(10);

                    while (_webSocket.ReadyState != WebSocketState.Open)
                    {
                        Thread.Sleep(1000);

                        if (timeEnd < DateTime.Now)
                        {
                            break;
                        }
                    }

                    if (_webSocket.ReadyState == WebSocketState.Open)
                    {
                        webSocket = _webSocket;
                    }
                }

                _subscribedSecurities.Add(security);

                string instrumentToken = security.NameId.Split('_')[0];

                webSocket?.SendAsync("{\"a\":\"subscribe\",\"v\":[" + instrumentToken + "]}");
                webSocket?.SendAsync("{\"a\":\"mode\",\"v\":[\"" + "full" + "\", [" + instrumentToken + "]]}");

            }
            catch (Exception exception)
            {
                Thread.Sleep(5000);
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        #endregion 8

        #region 9 WebSocket parsing the messages

        private void MessageReader()
        {
            Thread.Sleep(1000);

            try
            {
                while (true)
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(3000);
                        continue;
                    }

                    string message;

                    if (_fifoListWebSocketMessage == null
                        || _fifoListWebSocketMessage.Count == 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    _fifoListWebSocketMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Contains("order"))
                    {
                        UpdateOrder(message);
                    }

                    if (message.Contains("error"))
                    {
                        SendLogMessage(message.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                Thread.Sleep(5000);
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateOrder(string message)
        {
            try
            {
                ResponseWebSocketKiteConnect<OrderData> responseOrder = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketKiteConnect<OrderData>());

                if (responseOrder == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(responseOrder.data.order_id.ToString()))
                {
                    return;
                }

                List<Order> newOrders = new List<Order>();

                if (responseOrder.data.tag == null)
                {
                    return;
                }

                string[] tagSplit = responseOrder.data.tag.Split('_');

                if (tagSplit.Length < 2)
                {
                    return;
                }

                string userNumber = tagSplit[0];
                string nameSecurity = $"{responseOrder.data.tradingsymbol}_{tagSplit[2]}_{tagSplit[3]}";

                OrderStateType stateType = GetOrderState(responseOrder.data.status);

                Order newOrder = new Order();
                newOrder.SecurityNameCode = nameSecurity;
                newOrder.TimeCallBack = DateTimeOffset.Parse(responseOrder.data.order_timestamp).DateTime;
                newOrder.TimeCreate = DateTimeOffset.Parse(responseOrder.data.order_timestamp).DateTime;
                newOrder.NumberUser = Convert.ToInt32(userNumber);
                newOrder.NumberMarket = responseOrder.data.order_id.ToString();
                newOrder.Side = responseOrder.data.transaction_type.Equals("BUY") ? Side.Buy : Side.Sell;
                newOrder.State = stateType;
                newOrder.Volume = responseOrder.data.quantity.ToDecimal();
                newOrder.Price = responseOrder.data.price.ToDecimal();
                newOrder.ServerType = ServerType.KiteConnect;
                newOrder.PortfolioNumber = "EquityPortfolio";
                newOrder.SecurityClassCode = newOrder.SecurityNameCode;
                newOrder.TypeOrder = responseOrder.data.order_type == "LIMIT"
                        ? OrderPriceType.Limit
                        : OrderPriceType.Market;

                MyOrderEvent(newOrder);

                if (stateType == OrderStateType.Done
               || stateType == OrderStateType.Partial)
                {
                    FindMyTradesToOrder(newOrder);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void DecryptData(byte[] Data)
        {
            int Count = Data.Length;

            if (Count == 1)
            {
                // " Heartbeat" Pong;
            }
            else
            {
                int offset = 0;
                ushort count = ReadShort(Data, ref offset);

                for (ushort i = 0; i < count; i++)
                {
                    ushort length = ReadShort(Data, ref offset);

                    if (length == 184) // full with marketdepth and timestamp
                    {
                        UpdateTradeAndMarketDepth(Data, ref offset);
                    }
                }
            }
        }

        private DateTime _lastMdTime = DateTime.MinValue;

        private void UpdateTradeAndMarketDepth(byte[] b, ref int offset)
        {
            uint instrumentToken = ReadInt(b, ref offset);

            decimal divisor = GetDivisor(instrumentToken);

            bool Tradable = (instrumentToken & 0xff) != 9;
            decimal LastPrice = ReadInt(b, ref offset) / divisor;
            uint LastQuantity = ReadInt(b, ref offset);
            decimal AveragePrice = ReadInt(b, ref offset) / divisor;
            uint Volume = ReadInt(b, ref offset);
            uint BuyQuantity = ReadInt(b, ref offset);
            uint SellQuantity = ReadInt(b, ref offset);
            decimal Open = ReadInt(b, ref offset) / divisor;
            decimal High = ReadInt(b, ref offset) / divisor;
            decimal Low = ReadInt(b, ref offset) / divisor;
            decimal Close = ReadInt(b, ref offset) / divisor;

            DateTime LastTradeTime = UnixToDateTime(ReadInt(b, ref offset));
            uint OI = ReadInt(b, ref offset);
            uint OIDayHigh = ReadInt(b, ref offset);
            uint OIDayLow = ReadInt(b, ref offset);
            DateTime Timestamp = UnixToDateTime(ReadInt(b, ref offset));

            DepthItem[] Bids = new DepthItem[5];
            DepthItem[] Offers = new DepthItem[5];

            Trade trade = new Trade();

            Security securityTrade = GetNameSecurity(instrumentToken.ToString());

            if (securityTrade != null)
            {
                trade.SecurityNameCode = securityTrade.Name;
            }

            trade.Id = instrumentToken.ToString();
            trade.Price = LastPrice;
            trade.Volume = Volume;

            if (BuyQuantity > SellQuantity)
            {
                trade.Side = Side.Buy;
            }
            else
            {
                trade.Side = Side.Sell;
            }

            trade.Time = LastTradeTime;

            if (NewTradesEvent != null)
            {
                NewTradesEvent(trade);
            }

            MarketDepth depth = new MarketDepth();

            Security securityDepth = GetNameSecurity(instrumentToken.ToString());

            if (securityDepth != null)
            {
                depth.SecurityNameCode = securityDepth.Name;
            }

            depth.Time = Timestamp.AddMilliseconds(1);

            Bids = new DepthItem[5];
            for (int i = 0; i < 5; i++)
            {
                MarketDepthLevel newBid = new MarketDepthLevel();
                newBid.Bid = ReadInt(b, ref offset);
                newBid.Price = Convert.ToDouble(ReadInt(b, ref offset) / divisor);
                //newBid.Id = ReadShort(b, ref offset);
                depth.Bids.Add(newBid);
                offset += 2;

            }

            Offers = new DepthItem[5];
            for (int i = 0; i < 5; i++)
            {
                MarketDepthLevel newAsk = new MarketDepthLevel();
                newAsk.Ask = ReadInt(b, ref offset);
                newAsk.Price = Convert.ToDouble(ReadInt(b, ref offset) / divisor);
                //newAsk.Id = ReadShort(b, ref offset);
                depth.Asks.Add(newAsk);
                offset += 2;
            }

            if (_lastMdTime != DateTime.MinValue &&
               _lastMdTime >= depth.Time)
            {
                depth.Time = _lastMdTime.AddTicks(1);
            }

            _lastMdTime = depth.Time;

            if (MarketDepthEvent != null)
            {
                MarketDepthEvent(depth);
            }
        }

        private Security GetNameSecurity(string instrumentToken)
        {
            for (int i = 0; i < _securities.Count; i++)
            {
                string tokenSecurity = _securities[i].NameId.Split('_')[0];
                if (tokenSecurity == instrumentToken)
                {
                    return _securities[i];
                }
            }

            return null;
        }

        private ushort ReadShort(byte[] b, ref int offset)
        {
            ushort data = (ushort)(b[offset + 1] + (b[offset] << 8));
            offset += 2;
            return data;
        }

        private UInt32 ReadInt(byte[] b, ref int offset)
        {
            UInt32 data = (UInt32)BitConverter.ToUInt32(new byte[] { b[offset + 3], b[offset + 2], b[offset + 1], b[offset + 0] }, 0);
            offset += 4;
            return data;
        }

        private decimal GetDivisor(uint InstrumentToken)
        {
            uint segment = InstrumentToken & 0xff;
            switch (segment)
            {
                case 3: // CDS
                    return 10000000.0m;
                case 6: // BCD
                    return 10000.0m;
                default:
                    return 100.0m;
            }
        }

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        #endregion 9

        #region 10 Trade

        public void SendOrder(Order order)
        {
            _rateGateOrder.WaitToProceed();

            try
            {
                string path = $"/orders/regular";
                string exchange = GetExchange(order.SecurityClassCode.Split('_')[0]);
                string nameSecurity = order.SecurityNameCode.Split('_')[0];

                string tag = $"{order.NumberUser.ToString()}_{order.SecurityNameCode}";

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(path, Method.POST);
                request.AddHeader("Authorization", "token " + _apiKey + ":" + _accessToken);
                request.AddHeader("X-Kite-Version", "3");
                request.AddParameter("tradingsymbol", nameSecurity);
                request.AddParameter("exchange", exchange);
                request.AddParameter("transaction_type", order.Side.ToString().ToUpper());
                request.AddParameter("order_type", order.TypeOrder.ToString().ToUpper());

                if (order.TypeOrder != OrderPriceType.Market)
                {
                    request.AddParameter("price", order.Price.ToString().Replace(",", "."));
                }

                request.AddParameter("quantity", order.Volume.ToString().Replace(",", "."));
                request.AddParameter("product", "CNC");
                request.AddParameter("validity", "DAY");
                request.AddParameter("tag", tag);

                IRestResponse response = client.Execute(request);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    SendLogMessage("Order failed. Status: "
                         + response.StatusCode + "  " + order.SecurityNameCode + ", " + response.Content, LogMessageType.Error);
                    order.State = OrderStateType.Fail;
                    MyOrderEvent(order);

                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order send error " + exception.ToString(), LogMessageType.Error);
            }
        }

        public bool CancelOrder(Order order)
        {
            _rateGateOrder.WaitToProceed();

            try
            {
                string path = $"/orders/regular/{order.NumberMarket}";

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(path, Method.DELETE);
                request.AddHeader("Authorization", "token " + _apiKey + ":" + _accessToken);
                request.AddHeader("X-Kite-Version", "3");

                IRestResponse response = client.Execute(request);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    SendLogMessage("Cancel order failed. Status: "
                         + response.StatusCode + "  " + order.SecurityNameCode + ", " + response.Content, LogMessageType.Error);
                }
                else
                {
                    return true;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Cancel order failed " + exception.ToString(), LogMessageType.Error);
            }
            return false;
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            if (string.IsNullOrEmpty(order.NumberMarket))
            {
                return;
            }

            _rateGateOrder.WaitToProceed();

            try
            {
                string path = $"/orders/regular/{order.NumberMarket}";

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(path, Method.PUT);
                request.AddHeader("Authorization", "token " + _apiKey + ":" + _accessToken);
                request.AddHeader("X-Kite-Version", "3");
                request.AddParameter("order_type", "LIMIT");
                request.AddParameter("quantity", order.Volume.ToString().Replace(",", "."));
                request.AddParameter("price", newPrice.ToString().Replace(",", "."));
                request.AddParameter("validity", "DAY");

                IRestResponse response = client.Execute(request);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    SendLogMessage("Order change failed Status: "
                         + response.StatusCode + "  " + order.SecurityNameCode + ", " + response.Content, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order change failed " + exception.ToString(), LogMessageType.Error);
            }
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
                Order order = orders[i];

                if (order.State == OrderStateType.Active)
                {
                    CancelOrder(order);
                }
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
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

        public OrderStateType GetOrderStatus(Order order)
        {
            Order myOrder = GetOrderFromExchange(order.NumberUser.ToString());

            if (myOrder == null)
            {
                return OrderStateType.None;
            }

            MyOrderEvent?.Invoke(myOrder);

            if (myOrder.State == OrderStateType.Done || myOrder.State == OrderStateType.Partial)
            {
                FindMyTradesToOrder(myOrder);
            }

            return myOrder.State;
        }

        private List<Order> GetAllOpenOrders()
        {
            _rateGateOrder.WaitToProceed();

            try
            {
                string path = $"/orders";

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(path, Method.GET);
                request.AddHeader("Authorization", "token " + _apiKey + ":" + _accessToken);
                request.AddHeader("X-Kite-Version", "3");

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestKite<List<OrderResponse>> responseOrder = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestKite<List<OrderResponse>>());

                    if (responseOrder.status == "success")
                    {
                        List<Order> orders = new List<Order>();

                        List<Order> newOrders = new List<Order>();

                        for (int i = 0; i < responseOrder.data.Count; i++)
                        {
                            if (responseOrder.data[i].status != "OPEN")
                            {
                                continue;
                            }

                            if (responseOrder.data[i].tag == null)
                            {
                                continue;
                            }

                            string[] tagSplit = responseOrder.data[i].tag.Split('_');

                            if (tagSplit.Length < 2)
                            {
                                continue;
                            }

                            string userNumber = tagSplit[0];
                            string nameSecurity = $"{responseOrder.data[i].tradingsymbol}_{tagSplit[2]}_{tagSplit[3]}";

                            OrderStateType stateType = GetOrderState(responseOrder.data[i].status);

                            Order newOrder = new Order();
                            newOrder.SecurityNameCode = nameSecurity;
                            newOrder.TimeCallBack = DateTimeOffset.Parse(responseOrder.data[i].order_timestamp).DateTime;
                            newOrder.TimeCreate = DateTimeOffset.Parse(responseOrder.data[i].order_timestamp).DateTime;
                            newOrder.NumberUser = Convert.ToInt32(userNumber);
                            newOrder.NumberMarket = responseOrder.data[i].order_id.ToString();
                            newOrder.Side = responseOrder.data[i].transaction_type.Equals("BUY") ? Side.Buy : Side.Sell;
                            newOrder.State = stateType;
                            newOrder.Volume = responseOrder.data[i].quantity.ToDecimal();
                            newOrder.Price = responseOrder.data[i].price.ToDecimal();
                            newOrder.ServerType = ServerType.KiteConnect;
                            newOrder.PortfolioNumber = "EquityPortfolio";
                            newOrder.SecurityClassCode = newOrder.SecurityNameCode;
                            newOrder.TypeOrder = responseOrder.data[i].order_type == "LIMIT"
                                    ? OrderPriceType.Limit
                                    : OrderPriceType.Market;

                            orders.Add(newOrder);
                        }

                        return orders;
                    }
                    else
                    {
                        SendLogMessage($"Active orders error type: {responseOrder.error_type}, {responseOrder.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Error querying active orders. Status: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                }
                return null;
            }
            catch (Exception exception)
            {
                SendLogMessage("Error querying active orders." + exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private void FindMyTradesToOrder(Order order)
        {
            _rateGateOrder.WaitToProceed();

            try
            {
                string path = $"/orders/{order.NumberMarket}/trades";

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(path, Method.GET);
                request.AddHeader("Authorization", "token " + _apiKey + ":" + _accessToken);
                request.AddHeader("X-Kite-Version", "3");
                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestKite<List<TradeResponse>> responceMyTrade = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestKite<List<TradeResponse>>());

                    if (responceMyTrade.status == "success")
                    {
                        for (int i = 0; i < responceMyTrade.data.Count; i++)
                        {
                            MyTrade myTrade = new MyTrade();
                            myTrade.Time = DateTimeOffset.Parse(responceMyTrade.data[i].fill_timestamp).DateTime;
                            myTrade.NumberOrderParent = responceMyTrade.data[i].order_id;
                            myTrade.NumberTrade = responceMyTrade.data[i].trade_id;
                            myTrade.Price = responceMyTrade.data[i].average_price.ToDecimal();

                            if (responceMyTrade.data[i].tradingsymbol == order.SecurityNameCode.Split('_')[0])
                            {
                                myTrade.SecurityNameCode = order.SecurityNameCode;
                            }

                            myTrade.Side = responceMyTrade.data[i].transaction_type == "BUY" ? Side.Buy : Side.Sell;
                            myTrade.Volume = responceMyTrade.data[i].quantity.ToDecimal();

                            MyTradeEvent(myTrade);
                        }
                    }
                    else
                    {
                        SendLogMessage($"myTrade error type: {responceMyTrade.error_type}, {responceMyTrade.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Error getting myTrade. Status: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Got a myTrade request error." + exception.ToString(), LogMessageType.Error);
            }
        }

        private Order GetOrderFromExchange(string numberUser)
        {
            _rateGateOrder.WaitToProceed();

            if (string.IsNullOrEmpty(numberUser))
            {
                SendLogMessage("Order ID is empty", LogMessageType.Connect);
                return null;
            }

            Order newOrder = new Order();

            try
            {
                string path = $"/orders";

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(path, Method.GET);
                request.AddHeader("Authorization", "token " + _apiKey + ":" + _accessToken);
                request.AddHeader("X-Kite-Version", "3");

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestKite<List<OrderResponse>> responseOrder = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestKite<List<OrderResponse>>());

                    if (responseOrder.status == "success")
                    {
                        for (int i = 0; i < responseOrder.data.Count; i++)
                        {
                            if (responseOrder.data[i].tag == null)
                            {
                                continue;
                            }

                            string[] tagSplit = responseOrder.data[i].tag.Split('_');

                            if (tagSplit.Length < 2)
                            {
                                continue;
                            }

                            string userNumber = tagSplit[0];
                            string nameSecurity = $"{responseOrder.data[i].tradingsymbol}_{tagSplit[2]}_{tagSplit[3]}";

                            if (userNumber == numberUser)
                            {
                                OrderStateType stateType = GetOrderState(responseOrder.data[i].status);

                                newOrder.SecurityNameCode = nameSecurity;
                                newOrder.TimeCallBack = DateTimeOffset.Parse(responseOrder.data[i].order_timestamp).DateTime;
                                newOrder.TimeCreate = DateTimeOffset.Parse(responseOrder.data[i].order_timestamp).DateTime;
                                newOrder.NumberUser = Convert.ToInt32(userNumber);
                                newOrder.NumberMarket = responseOrder.data[i].order_id.ToString();
                                newOrder.Side = responseOrder.data[i].transaction_type.Equals("BUY") ? Side.Buy : Side.Sell;
                                newOrder.State = stateType;
                                newOrder.Volume = responseOrder.data[i].quantity.ToDecimal();
                                newOrder.Price = responseOrder.data[i].price.ToDecimal();
                                newOrder.ServerType = ServerType.KiteConnect;
                                newOrder.PortfolioNumber = "EquityPortfolio";
                                newOrder.SecurityClassCode = newOrder.SecurityNameCode;
                                newOrder.TypeOrder = responseOrder.data[i].order_type == "LIMIT"
                                        ? OrderPriceType.Limit
                                        : OrderPriceType.Market;
                            }
                        }

                        return newOrder;
                    }
                    else
                    {
                        SendLogMessage($"Order status error type: {responseOrder.error_type}, {responseOrder.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Request for order status failed. Status: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                }
                return null;
            }
            catch (Exception exception)
            {
                SendLogMessage("Request for order status failed." + exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private string GetExchange(string exchange)
        {
            switch (exchange)
            {
                case "NSE":
                    return "NSE";
                case "BSE":
                    return "BSE";
                case "NFO":
                    return "NFO";
                case "CDS":
                    return "CDS";
                case "BFO":
                    return "BFO";
                case "MCX":
                    return "MCX";
                case "BCD":
                    return "BCD";
                default:
                    return null;
            }
        }

        private OrderStateType GetOrderState(string orderStateResponse)
        {
            OrderStateType stateType;

            switch (orderStateResponse)
            {
                case ("OPEN"):
                    stateType = OrderStateType.Active;
                    break;
                case ("COMPLETE"):
                    stateType = OrderStateType.Done;
                    break;
                case ("UPDATE"):
                    stateType = OrderStateType.Partial;
                    break;
                case ("REJECTED"):
                    stateType = OrderStateType.Fail;
                    break;
                case ("CANCELLED"):
                    stateType = OrderStateType.Cancel;
                    break;
                case ("VALIDATION PENDING"):
                    stateType = OrderStateType.Pending;
                    break;
                case ("OPEN PENDING"):
                    stateType = OrderStateType.Pending;
                    break;
                case ("MODIFY VALIDATION PENDING"):
                    stateType = OrderStateType.Pending;
                    break;
                case ("MODIFY PENDING"):
                    stateType = OrderStateType.Pending;
                    break;
                case ("TRIGGER PENDING"):
                    stateType = OrderStateType.Pending;
                    break;
                case ("CANCEL PENDING"):
                    stateType = OrderStateType.Pending;
                    break;
                default:
                    stateType = OrderStateType.None;
                    break;
            }
            return stateType;
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            return null;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            return null;
        }

        #endregion 10

        #region 11 Helpers

        static DateTime ConvertToIST(DateTime localTime)
        {
            TimeZoneInfo istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            DateTime istTime = TimeZoneInfo.ConvertTimeFromUtc(localTime, istZone);

            return istTime;
        }

        public static DateTime UnixToDateTime(UInt64 unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            DateTime dateTime = new DateTime(1970, 1, 1, 5, 30, 0, 0, DateTimeKind.Unspecified);
            dateTime = dateTime.AddSeconds(unixTimeStamp);
            return dateTime;
        }

        public static string SHA256Hash(string Data)
        {
            char[] inputData = Data.ToCharArray();

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(inputData);
                byte[] hashBytes = sha256.ComputeHash(inputBytes);
                StringBuilder hexHash = new StringBuilder();

                for (int i = 0; i < hashBytes.Length; i++)
                {
                    hexHash.AppendFormat("{0:x2}", hashBytes[i]);
                }

                return hexHash.ToString();
            }
        }

        public void SetLeverage(Security security, decimal leverage) { }

        #endregion 11

        #region 12 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        #endregion 12
    }
}
