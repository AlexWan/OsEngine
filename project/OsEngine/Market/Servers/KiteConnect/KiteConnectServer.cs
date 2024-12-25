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
using WebSocketSharp;


namespace OsEngine.Market.Servers.KiteConnect
{
    public class KiteConnectServer : AServer
    {
        public KiteConnectServer()
        {
            KiteServerRealization realization = new KiteServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
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
            //Thread worker = new Thread(CheckAliveWebSocket);
            //worker.Name = "CheckAliveWebSocketKite";
            //worker.Start();

            Thread thread = new Thread(GetUpdatePortfolio);
            thread.Name = "UpdatePortfolioKiteConnect";
            thread.Start();

            Thread converter = new Thread(MessageReader);
            converter.Name = "MessageReaderKiteConnect";
            converter.Start();

        }

        public void Connect()
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

                    if (errorUser.error_type == "TokenException")
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
                        SendLogMessage($"User profile request error. Status: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                    }
                }

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
                        _publicToken = responseUserAuthentication.data.public_token;
                        ((ServerParameterPassword)ServerParameters[3]).Value = _accessToken;
                    }
                    else
                    {
                        SendLogMessage($"Access Token error type: {responseUserAuthentication.error_type}, {responseUserAuthentication.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Authentication request error. Status: {response.StatusCode}, {response.Content}", LogMessageType.Error);
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

            _securities.Clear();
            _myPortfolios.Clear();

            DeleteWebSocketConnection();

            SendLogMessage("Connection Closed by KiteConnect. WebSocket Data Closed Event", LogMessageType.System);

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }
        private void unsubscribeFromAllWebSockets()
        {
            if (_webSocket == null)
            {
                return;
            }

            for (int i = 0; i < _securities.Count; i++)
            {
                string instrumentToken = _securities[i].NameId.Split('_')[0];
                _webSocket?.Send("{\"a\":\"unsubscribe\",\"v\":[" + instrumentToken + "]}");
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

        #endregion 1

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private static string _apiKey;

        private string _secretKey;

        private string _baseUrl = "https://api.kite.trade";

        private string _login = "https://kite.zerodha.com/connect/login";

        private string _webSocketUrl = "wss://ws.kite.trade";

        private string _accessToken;

        private string _requestToken;

        private string _publicToken;

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
                        newSecurity.Name = security[2];
                        newSecurity.NameFull = $"{security[11]}_{security[9]}_{security[2]}";
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
                            //newSecurity.Expiration = security[5].ToDecimal();
                        }

                        newSecurity.DecimalsVolume = Convert.ToInt32(security[8]);
                        newSecurity.Lot = security[8].ToDecimal();
                        newSecurity.PriceStep = security[7].ToDecimal();
                        newSecurity.Decimals = security[7].DecimalsCount();
                        newSecurity.PriceStepCost = newSecurity.PriceStep;
                        newSecurity.State = SecurityStateType.Activ;
                        //newSecurity.MinTradeAmount = responseOrder.minov.ToDecimal();

                        _securities.Add(newSecurity);
                    }
                }
                else
                {
                    SendLogMessage($"Securities request error. Status: {response.StatusCode}, {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Securities request error: " + exception.ToString(), LogMessageType.Error);
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
                            Portfolio myPortfolio = new Portfolio();
                            myPortfolio.Number = "EquityPortfolio";
                            myPortfolio.ValueBegin = responsePortfolio.data.equity.available.opening_balance.ToDecimal();
                            myPortfolio.ValueCurrent = responsePortfolio.data.equity.available.live_balance.ToDecimal();
                            myPortfolio.ValueBlocked = responsePortfolio.data.equity.utilised.debits.ToDecimal();
                            _myPortfolios.Add(myPortfolio);
                        }
                        if (Convert.ToBoolean(responsePortfolio.data.commodity.enabled) == true)
                        {
                            Portfolio myPortfolio = new Portfolio();
                            myPortfolio.Number = "CommodityPortfolio";
                            myPortfolio.ValueBegin = responsePortfolio.data.equity.available.opening_balance.ToDecimal();
                            myPortfolio.ValueCurrent = responsePortfolio.data.equity.available.live_balance.ToDecimal();
                            myPortfolio.ValueBlocked = responsePortfolio.data.equity.utilised.debits.ToDecimal();
                            _myPortfolios.Add(myPortfolio);
                        }

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
                            if (Convert.ToBoolean(responsePortfolio.data.equity.enabled) == true)
                            {
                                _myPortfolios[i].Number = "EquityPortfolio";
                                _myPortfolios[i].ValueBegin = responsePortfolio.data.equity.available.opening_balance.ToDecimal();
                                _myPortfolios[i].ValueCurrent = responsePortfolio.data.equity.available.live_balance.ToDecimal();
                                _myPortfolios[i].ValueBlocked = responsePortfolio.data.equity.utilised.debits.ToDecimal();
                            }
                            if (Convert.ToBoolean(responsePortfolio.data.commodity.enabled) == true)
                            {
                                _myPortfolios[i].Number = "CommodityPortfolio";
                                _myPortfolios[i].ValueBegin = responsePortfolio.data.equity.available.opening_balance.ToDecimal();
                                _myPortfolios[i].ValueCurrent = responsePortfolio.data.equity.available.live_balance.ToDecimal();
                                _myPortfolios[i].ValueBlocked = responsePortfolio.data.equity.utilised.debits.ToDecimal();
                            }
                        }

                        if (PortfolioEvent != null)
                        {
                            PortfolioEvent(_myPortfolios);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Update Portfolio error type: {responsePortfolio.error_type}, {responsePortfolio.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Update Portfolio request error. Status: {response.StatusCode}, {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Update Portfolio request error " + exception.ToString(), LogMessageType.Error);
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

                        for (int i = 0; i < _myPortfolios.Count; i++)
                        {
                            if (_myPortfolios[i].Number == "EquityPortfolio")
                            {
                                portf = _myPortfolios[i];

                            }
                            else if (_myPortfolios[i].Number == "CommodityPortfolio")
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
                            portf.SetNewPosition(newPos);
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

                    if (responsePositions.status == "success")
                    {
                        Portfolio portf = null;

                        for (int i = 0; i < _myPortfolios.Count; i++)
                        {
                            if (_myPortfolios[i].Number == "EquityPortfolio")
                            {
                                portf = _myPortfolios[i];

                            }
                            else if (_myPortfolios[i].Number == "CommodityPortfolio")
                            {
                                portf = _myPortfolios[i];
                            }
                        }

                        if (portf == null)
                        {
                            return;
                        }

                        for (int i = 0; i < responsePositions.data.net.Count; i++)
                        {
                            PositionOnBoard newPos = new PositionOnBoard();
                            newPos.PortfolioName = portf.Number;
                            newPos.ValueCurrent = responsePositions.data.net[i].quantity.ToDecimal();
                            newPos.SecurityNameCode = responsePositions.data.net[i].tradingsymbol;
                            portf.SetNewPosition(newPos);
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
                    SendLogMessage($"Positions request error. Status: {response.StatusCode}, {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Positions request error " + exception.ToString(), LogMessageType.Error);
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

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
            DateTime endTime = DateTime.Now;
            DateTime startTime = endTime.AddMinutes(-tfTotalMinutes * candleCount);

            return GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, endTime);
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (!_useSubscribeToHistoricalData)
            {
                return null;
            }

            _rateGateHistoricalCandle.WaitToProceed();

            try
            {
                string end = endTime.ToString("yyyy-MM-dd HH:mm:ss");
                string start = startTime.ToString("yyyy-MM-dd HH:mm:ss");

                string tf = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);
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

                            Candle newCandle = new Candle();
                            newCandle.Open = curCandle[1].ToDecimal();
                            newCandle.High = curCandle[2].ToDecimal();
                            newCandle.Low = curCandle[3].ToDecimal();
                            newCandle.Close = curCandle[4].ToDecimal();
                            newCandle.Volume = curCandle[5].ToDecimal();
                            newCandle.TimeStart = Convert.ToDateTime(curCandle[0]);

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
                    SendLogMessage($"Last Candle request error. Status: {response.StatusCode}, {response.Content}", LogMessageType.Error);
                }
                return null;
            }
            catch (Exception exception)
            {
                SendLogMessage("Last Candle request error " + exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private string GetInterval(TimeSpan timeFrame)
        {
            switch (timeFrame.TotalMinutes)
            {
                case 1:
                    return "minute";
                case 3:
                    return "3minute";
                case 5:
                    return "5minute";
                case 15:
                    return "15minute";
                case 30:
                    return "30minute";
                case 60:
                    return "60minute";
                case 1440:
                    return "day";
                default:
                    return null;
            }
        }

        #endregion 5

        #region 6 WebSocket creation

        private WebSocket _webSocket;

        private void CreateWebSocketConnection()
        {
            try
            {
                string fullUrlWebSoket = $"{_webSocketUrl}/?api_key={_apiKey}&access_token={_accessToken}";

                if (_webSocket != null)
                {
                    return;
                }

                _webSocket = new WebSocket(fullUrlWebSoket);
                _webSocket.EmitOnPing = true;
                _webSocket.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.None;
                _webSocket.OnOpen += _webSocket_OnOpen;
                _webSocket.OnMessage += _webSocket_OnMessage;
                _webSocket.OnError += _webSocket_OnError;
                _webSocket.OnClose += _webSocket_OnClose;

                _webSocket.Connect();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void DeleteWebSocketConnection()
        {
            if (_webSocket != null)
            {
                try
                {
                    _webSocket.OnOpen -= _webSocket_OnOpen;
                    _webSocket.OnMessage -= _webSocket_OnMessage;
                    _webSocket.OnError -= _webSocket_OnError;
                    _webSocket.OnClose -= _webSocket_OnClose;
                }
                catch
                {
                    // ignore
                }
                _webSocket = null;
            }
        }

        #endregion 6

        #region 7 WebSocket events

        private void _webSocket_OnClose(object sender, CloseEventArgs e)
        {
            if (ServerStatus == ServerConnectStatus.Connect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;

                SendLogMessage("Websocket lost connection: " + e.ToString(), LogMessageType.Error);

                if (DisconnectEvent != null)
                {
                    DisconnectEvent();
                }
            }
        }

        private void _webSocket_OnError(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            SendLogMessage("Error websocket :" + e.ToString(), LogMessageType.Error);
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
            if (ServerStatus != ServerConnectStatus.Connect)
            {
                SendLogMessage("Websockets activate. Connection status", LogMessageType.System);

                ServerStatus = ServerConnectStatus.Connect;

                if (ConnectEvent != null)
                {
                    ConnectEvent();
                }
            }
        }

        #endregion 7

        #region 8 WebSocket check alive

        private DateTime _timeLastSendPing = DateTime.Now;

        private void CheckAliveWebSocket()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(3000);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        _timeLastSendPing = DateTime.Now;
                        Thread.Sleep(1000);
                        continue;
                    }

                    //if (_webSocket != null && _webSocket.State == WebSocketState.Open ||
                    //    _webSocket.State == WebSocketState.Connecting)
                    //{
                    //    if (_timeLastSendPing.AddSeconds(25) < DateTime.Now)
                    //    {
                    //        _webSocket.Send("ping");
                    //        _timeLastSendPing = DateTime.Now;
                    //    }
                    //}
                    //else
                    //{
                    //    if (ServerStatus != ServerConnectStatus.Disconnect)
                    //    {
                    //        ServerStatus = ServerConnectStatus.Disconnect;
                    //        DisconnectEvent();
                    //    }
                    //}
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        #endregion 8

        #region 9 WebSocket Security subscrible

        List<Security> _subscribledSecurities = new List<Security>();

        public void Subscrible(Security security)
        {
            try
            {
                _rateGateAllOthers.WaitToProceed();

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                for (int i = 0; i < _subscribledSecurities.Count; i++)
                {
                    if (_subscribledSecurities[i].Equals(security.Name))
                    {
                        return;
                    }
                }

                _subscribledSecurities.Add(security);

                string instrumentToken = security.NameId.Split('_')[0];

                _webSocket?.Send("{\"a\":\"subscribe\",\"v\":[" + instrumentToken + "]}");
                _webSocket?.Send("{\"a\":\"mode\",\"v\":[\"" + "full" + "\", [" + instrumentToken + "]]}");
            }
            catch (Exception exception)
            {
                Thread.Sleep(5000);
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        #endregion 9

        #region 10 WebSocket parsing the messages

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

                List<Order> newOrders = new List<Order>();

                OrderStateType stateType = GetOrderState(responseOrder.data.status);

                Order newOrder = new Order();
                newOrder.SecurityNameCode = responseOrder.data.tradingsymbol;
                newOrder.TimeCallBack = Convert.ToDateTime(responseOrder.data.order_timestamp);
                newOrder.TimeCreate = Convert.ToDateTime(responseOrder.data.order_timestamp);
                newOrder.NumberUser = Convert.ToInt32(responseOrder.data.tag);
                newOrder.NumberMarket = responseOrder.data.order_id.ToString();
                newOrder.Side = responseOrder.data.transaction_type.Equals("BUY") ? Side.Buy : Side.Sell;
                newOrder.State = stateType;
                newOrder.Volume = responseOrder.data.quantity.ToDecimal();
                newOrder.Price = responseOrder.data.price.ToDecimal();
                newOrder.ServerType = ServerType.KiteConnect;
                newOrder.PortfolioNumber = "EquityPortfolio";
                newOrder.SecurityClassCode = responseOrder.data.tradingsymbol;
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

            trade.Time = Timestamp;

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
            
            depth.Time = Timestamp;

            Bids = new DepthItem[5];
            for (int i = 0; i < 5; i++)
            {
                MarketDepthLevel newBid = new MarketDepthLevel();
                newBid.Bid = ReadInt(b, ref offset);
                newBid.Price = ReadInt(b, ref offset) / divisor;
                newBid.Id = ReadShort(b, ref offset);
                depth.Bids.Add(newBid);
                offset += 2;

            }

            Offers = new DepthItem[5];
            for (int i = 0; i < 5; i++)
            {
                MarketDepthLevel newAsk = new MarketDepthLevel();
                newAsk.Ask = ReadInt(b, ref offset);
                newAsk.Price = ReadInt(b, ref offset) / divisor;
                newAsk.Id = ReadShort(b, ref offset);
                depth.Asks.Add(newAsk);
                offset += 2;
            }

            if (MarketDepthEvent != null)
            {
                MarketDepthEvent(depth);
            }


            //if (quotes.side == "buy")
            //{
            //    trade.Side = Side.Buy;
            //}
            //else
            //{
            //    trade.Side = Side.Sell;
            //}

        }

        private Security GetNameSecurity(string instrumentToken)
        {
            string nameSecurity = null;

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

        #endregion 10

        #region 11 Trade

        public void SendOrder(Order order)
        {
            _rateGateOrder.WaitToProceed();

            try
            {
                string path = $"/orders/regular";

                string exchange = GetExchange(order.SecurityClassCode.Split('_')[0]);

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(path, Method.POST);
                request.AddHeader("Authorization", "token " + _apiKey + ":" + _accessToken);
                request.AddHeader("X-Kite-Version", "3");
                request.AddParameter("tradingsymbol", order.SecurityNameCode);
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
                request.AddParameter("tag", order.NumberUser.ToString());

                IRestResponse response = client.Execute(request);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    SendLogMessage("Order Fail. Status: "
                        + response.StatusCode + "  " + order.SecurityNameCode + ", " + response.Content, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order send error " + exception.ToString(), LogMessageType.Error);
            }
        }

        public void CancelOrder(Order order)
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
                    SendLogMessage("Cancel order Fail. Status: "
                         + response.StatusCode + "  " + order.SecurityNameCode + ", " + response.Content, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Cancel order error " + exception.ToString(), LogMessageType.Error);
            }
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
                request.AddParameter("quantity", order.Volume.ToString());
                request.AddParameter("price", newPrice.ToString());
                request.AddParameter("validity", "DAY");


                IRestResponse response = client.Execute(request);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    SendLogMessage("Order Fail. Status: "
                         + response.StatusCode + "  " + order.SecurityNameCode + ", " + response.Content, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order send error " + exception.ToString(), LogMessageType.Error);
            }
        }

        public void CancelAllOrders()
        {

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

        public void GetOrderStatus(Order order)
        {
            Order myOrder = GetOrderFromExchange(order.NumberMarket);

            if (myOrder == null)
            {
                return;
            }

            MyOrderEvent?.Invoke(myOrder);

            if (myOrder.State == OrderStateType.Done || myOrder.State == OrderStateType.Partial)
            {
                FindMyTradesToOrder(myOrder);
            }
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

                            if (responseOrder.status != "Open")
                            {
                                continue;
                            }

                            OrderStateType stateType = GetOrderState(responseOrder.data[i].status);

                            Order newOrder = new Order();
                            newOrder.SecurityNameCode = responseOrder.data[i].tradingsymbol;
                            newOrder.TimeCallBack = Convert.ToDateTime(responseOrder.data[i].order_timestamp);
                            newOrder.TimeCreate = Convert.ToDateTime(responseOrder.data[i].order_timestamp);
                            newOrder.NumberUser = Convert.ToInt32(responseOrder.data[i].user_id);
                            newOrder.NumberMarket = responseOrder.data[i].order_id.ToString();
                            newOrder.Side = responseOrder.data[i].transaction_type.Equals("BUY") ? Side.Buy : Side.Sell;
                            newOrder.State = stateType;
                            newOrder.Volume = responseOrder.data[i].quantity.ToDecimal();
                            newOrder.Price = responseOrder.data[i].price.ToDecimal();
                            newOrder.ServerType = ServerType.KiteConnect;
                            newOrder.PortfolioNumber = "EquityPortfolio";
                            newOrder.SecurityClassCode = responseOrder.data[i].tradingsymbol;
                            newOrder.TypeOrder = responseOrder.data[i].order_type == "LIMIT"
                                    ? OrderPriceType.Limit
                                    : OrderPriceType.Market;

                            orders.Add(newOrder);
                        }

                        return orders;
                    }
                }
                else
                {
                    SendLogMessage($"Get all orders request error: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                }
                return null;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Error);
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
                            myTrade.Time = Convert.ToDateTime(responceMyTrade.data[i].fill_timestamp);
                            myTrade.NumberOrderParent = responceMyTrade.data[i].order_id;
                            myTrade.NumberTrade = responceMyTrade.data[i].trade_id;
                            myTrade.Price = responceMyTrade.data[i].average_price.ToDecimal();
                            myTrade.SecurityNameCode = responceMyTrade.data[i].tradingsymbol;
                            myTrade.Side = responceMyTrade.data[i].transaction_type == "BUY" ? Side.Buy : Side.Sell;
                            myTrade.Volume = responceMyTrade.data[i].quantity.ToDecimal();

                            MyTradeEvent(myTrade);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Get myTrade error type: {responceMyTrade.error_type}, {responceMyTrade.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Get orders myTrade error: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Error);
            }
        }

        private Order GetOrderFromExchange(string numberMarket)
        {
            _rateGateOrder.WaitToProceed();

            if (string.IsNullOrEmpty(numberMarket))
            {
                SendLogMessage("Order ID is empty", LogMessageType.Connect);
                return null;
            }

            Order newOrder = new Order();

            try
            {
                string path = $"/orders/{numberMarket}";

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
                        List<Order> newOrders = new List<Order>();

                        for (int i = 0; i < responseOrder.data.Count; i++)
                        {
                            if (string.IsNullOrEmpty(responseOrder.data[i].order_id.ToString()))
                            {
                                continue;
                            }

                            OrderStateType stateType = GetOrderState(responseOrder.data[i].status);

                            newOrder.SecurityNameCode = responseOrder.data[i].tradingsymbol;
                            newOrder.TimeCallBack = Convert.ToDateTime(responseOrder.data[i].order_timestamp);
                            newOrder.TimeCreate = Convert.ToDateTime(responseOrder.data[i].order_timestamp);
                            newOrder.NumberUser = Convert.ToInt32(responseOrder.data[i].tag);
                            newOrder.NumberMarket = responseOrder.data[i].order_id.ToString();
                            newOrder.Side = responseOrder.data[i].transaction_type.Equals("BUY") ? Side.Buy : Side.Sell;
                            newOrder.State = stateType;
                            newOrder.Volume = responseOrder.data[i].quantity.ToDecimal();
                            newOrder.Price = responseOrder.data[i].price.ToDecimal();
                            newOrder.ServerType = ServerType.KiteConnect;
                            newOrder.PortfolioNumber = "EquityPortfolio";
                            newOrder.SecurityClassCode = responseOrder.data[i].tradingsymbol;
                            newOrder.TypeOrder = responseOrder.data[i].order_type == "LIMIT"
                                    ? OrderPriceType.Limit
                                    : OrderPriceType.Market;
                        }
                    }
                    else
                    {
                        SendLogMessage($"Get order error type: {responseOrder.error_type}, {responseOrder.message}", LogMessageType.Error);
                        return null;
                    }
                }
                else
                {
                    SendLogMessage($"Get order request error. Status: {response.StatusCode}, {response.Content}", LogMessageType.Error);
                    return null;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Get order request error." + exception.ToString(), LogMessageType.Error);
                return null;
            }

            return newOrder;
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

        #endregion 11

        #region 12 Helpers

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

        #endregion 12

        #region 13 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}
