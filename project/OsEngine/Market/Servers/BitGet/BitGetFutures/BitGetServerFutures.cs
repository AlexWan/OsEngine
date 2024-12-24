using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.BitGet.BitGetFutures.Entity;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using WebSocketSharp;

namespace OsEngine.Market.Servers.BitGet.BitGetFutures
{
    public class BitGetServerFutures : AServer
    {
        public BitGetServerFutures()
        {

            BitGetServerRealization realization = new BitGetServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamPassphrase, "");
            CreateParameterEnum("Hedge Mode", "On", new List<string> { "On", "Off" });
            CreateParameterEnum("Margin Mode", "Crossed", new List<string> { "Crossed", "Isolated" });
            CreateParameterBoolean("Demo Trading", false);
        }
    }

    public class BitGetServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public BitGetServerRealization()
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

        public void Connect()
        {
            PublicKey = ((ServerParameterString)ServerParameters[0]).Value;
            SeckretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
            Passphrase = ((ServerParameterPassword)ServerParameters[2]).Value;

            if (string.IsNullOrEmpty(PublicKey) ||
                string.IsNullOrEmpty(SeckretKey) ||
                string.IsNullOrEmpty(Passphrase))
            {
                SendLogMessage("Can`t run Bitget Futures connector. No keys or passphrase",
                    LogMessageType.Error);
                return;
            }

            if (((ServerParameterBool)ServerParameters[5]).Value == true)
            {
                _listCoin = new List<string>() { "SUSDT-FUTURES", "SCOIN-FUTURES", "SUSDC-FUTURES" };
            }
            else
            {
                _listCoin = new List<string>() { "USDT-FUTURES", "COIN-FUTURES", "USDC-FUTURES" };
            }

            if (((ServerParameterEnum)ServerParameters[3]).Value == "On")
            {
                _hedgeMode = true;
            }
            else
            {
                _hedgeMode = false;
            }

            SetPositionMode();

            if (((ServerParameterEnum)ServerParameters[4]).Value == "Crossed")
            {
                _marginMode = "crossed";
            }
            else
            {
                _marginMode = "isolated";
            }

            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Ssl3
                | SecurityProtocolType.Tls11
                | SecurityProtocolType.Tls12
                | SecurityProtocolType.Tls13
                | SecurityProtocolType.Tls;

            try
            {
                string requestStr = "/api/v2/public/time";
                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                IRestResponse response = new RestClient(BaseUrl).Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    TimeToSendPingPublic = DateTime.Now;
                    TimeToSendPingPrivate = DateTime.Now;
                    FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
                    FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();
                    CreateWebSocketConnection();
                    _lastConnectionStartTime = DateTime.Now;
                }
                else
                {
                    SendLogMessage("Connection can be open. BitGet. Error request", LogMessageType.Error);

                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                SendLogMessage("Connection can be open. BitGet. Error request", LogMessageType.Error);

                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
        }

        public void Dispose()
        {
            try
            {
                UnsubscribeFromAllWebSockets();
                _subscribledSecutiries.Clear();
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

        public ServerType ServerType
        {
            get { return ServerType.BitGetFutures; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public DateTime ServerTime { get; set; }

        private DateTime _lastConnectionStartTime = DateTime.MinValue;

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string BaseUrl = "https://api.bitget.com";

        private string PublicKey;

        private string SeckretKey;

        private string Passphrase;

        private int _limitCandlesData = 200;

        private int _limitCandlesTrader = 1000;

        private List<string> _listCoin;

        private bool _hedgeMode;

        private string _marginMode = "crossed";

        private Dictionary<string, List<string>> _allPositions = new Dictionary<string, List<string>>();

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            for (int indCoin = 0; indCoin < _listCoin.Count; indCoin++)
            {
                try
                {
                    string requestStr = $"/api/v2/mix/market/contracts?productType={_listCoin[indCoin]}";
                    RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                    IRestResponse response = new RestClient(BaseUrl).Execute(requestRest);

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        SendLogMessage($"Http State Code: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                        continue;
                    }

                    ResponseRestMessage<List<RestMessageSymbol>> symbols = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<RestMessageSymbol>>());

                    List<Security> securities = new List<Security>();

                    if (symbols.data.Count == 0)
                    {
                        continue;
                    }

                    for (int i = 0; i < symbols.data.Count; i++)
                    {
                        RestMessageSymbol item = symbols.data[i];

                        int decimals = Convert.ToInt32(item.pricePlace);
                        decimal priceStep = (GetPriceStep(Convert.ToInt32(item.pricePlace), Convert.ToInt32(item.priceEndStep))).ToDecimal();

                        if (item.symbolStatus.Equals("normal"))
                        {
                            Security newSecurity = new Security();

                            newSecurity.Exchange = ServerType.BitGetFutures.ToString();
                            newSecurity.DecimalsVolume = Convert.ToInt32(item.volumePlace);
                            newSecurity.Lot = GetVolumeStep(newSecurity.DecimalsVolume);
                            newSecurity.Name = item.symbol;
                            newSecurity.NameFull = item.symbol;
                            newSecurity.NameClass = _listCoin[indCoin];
                            newSecurity.NameId = item.symbol;
                            newSecurity.SecurityType = SecurityType.Futures;
                            newSecurity.Decimals = decimals;
                            newSecurity.PriceStep = priceStep;
                            newSecurity.PriceStepCost = priceStep;
                            newSecurity.State = SecurityStateType.Activ;

                            securities.Add(newSecurity);
                        }
                    }
                    SecurityEvent(securities);
                }

                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }

        private decimal GetVolumeStep(int ScalePrice)
        {
            if (ScalePrice == 0)
            {
                return 1;
            }
            string priceStep = "0,";
            for (int i = 0; i < ScalePrice - 1; i++)
            {
                priceStep += "0";
            }

            priceStep += "1";

            return priceStep.ToDecimal();
        }

        private string GetPriceStep(int PricePlace, int PriceEndStep)
        {
            if (PricePlace == 0)
            {
                return Convert.ToString(PriceEndStep);
            }

            string res = String.Empty;

            for (int i = 0; i < PricePlace; i++)
            {
                if (i == 0)
                {
                    res += "0,";
                }
                else
                {
                    res += "0";
                }
            }

            return res + PriceEndStep;
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        public void GetPortfolios()
        {
        }

        private bool _portfolioIsStarted = false;

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

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

        private List<Candle> GetCandleData(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime, bool isOsData)
        {
            if (!CheckTime(startTime, endTime, actualTime))
            {
                return null;
            }

            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

            if (!CheckTf(tfTotalMinutes))
            {
                return null;
            }

            int limitCandles = _limitCandlesTrader;

            if (isOsData)
            {
                limitCandles = _limitCandlesData;
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

                List<Candle> candles = RequestCandleHistory(security, interval, from, to, isOsData, limitCandles);

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

        private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (startTime >= endTime ||
                startTime >= DateTime.Now ||
                actualTime > endTime ||
                actualTime > DateTime.Now ||
                endTime < DateTime.UtcNow.AddYears(-20))
            {
                return false;
            }
            return true;
        }

        private bool CheckTf(int timeFrameMinutes)
        {
            if (timeFrameMinutes == 1 ||
                timeFrameMinutes == 3 ||
                timeFrameMinutes == 5 ||
                timeFrameMinutes == 15 ||
                timeFrameMinutes == 30 ||
                timeFrameMinutes == 60 ||
                timeFrameMinutes == 240)
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
            else
            {
                return $"{tf.Hours}H";
            }
        }

        private readonly RateGate _rgCandleData = new RateGate(1, TimeSpan.FromMilliseconds(100));

        private List<Candle> RequestCandleHistory(Security security, string interval, long startTime, long endTime, bool isOsData, int limitCandles)
        {
            _rgCandleData.WaitToProceed(100);

            string stringUrl = "/api/v2/mix/market/candles";

            if (isOsData)
            {
                stringUrl = "/api/v2/mix/market/history-candles";
            }

            try
            {
                string requestStr = $"{stringUrl}?symbol={security.Name}&productType={security.NameClass.ToLower()}&" +
                    $"startTime={startTime}&granularity={interval}&limit={limitCandles}&endTime={endTime}";
                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                IRestResponse response = new RestClient(BaseUrl).Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return ConvertCandles(response.Content);
                }
                else
                {
                    SendLogMessage($"Http State Code: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
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

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        #endregion

        #region 6 WebSocket creation

        private string _webSocketUrlPublic = "wss://ws.bitget.com/v2/ws/public";

        private string _webSocketUrlPrivate = "wss://ws.bitget.com/v2/ws/private";

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
                _webSocketPublic.EmitOnPing = true;
                _webSocketPublic.SslConfiguration.EnabledSslProtocols
                    = System.Security.Authentication.SslProtocols.Ssl3
                    | System.Security.Authentication.SslProtocols.Tls11
                    | System.Security.Authentication.SslProtocols.Tls12
                    | System.Security.Authentication.SslProtocols.Tls13
                    | System.Security.Authentication.SslProtocols.Tls;
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
                _webSocketPrivate.EmitOnPing = true;
                _webSocketPrivate.SslConfiguration.EnabledSslProtocols
                    = System.Security.Authentication.SslProtocols.Ssl3
                   | System.Security.Authentication.SslProtocols.Tls11
                   | System.Security.Authentication.SslProtocols.Tls12
                   | System.Security.Authentication.SslProtocols.Tls13
                   | System.Security.Authentication.SslProtocols.Tls;
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

        private string _socketActivateLocker = "socketAcvateLocker";

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

        private void CreateAuthMessageWebSocekt()
        {
            try
            {
                string TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                string Sign = GenerateSignature(TimeStamp, "GET", "/user/verify", null, null, SeckretKey);

                RequestWebsocketAuth requestWebsocketAuth = new RequestWebsocketAuth();

                requestWebsocketAuth.op = "login";
                requestWebsocketAuth.args = new List<AuthItem>();
                requestWebsocketAuth.args.Add(new AuthItem());
                requestWebsocketAuth.args[0].apiKey = PublicKey;
                requestWebsocketAuth.args[0].passphrase = Passphrase;
                requestWebsocketAuth.args[0].timestamp = TimeStamp;
                requestWebsocketAuth.args[0].sign = Sign;

                string AuthJson = JsonConvert.SerializeObject(requestWebsocketAuth);

                _webSocketPrivate.Send(AuthJson);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocketPublic_Opened(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("Bitget WebSocket Public connection open", LogMessageType.System);
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
                    SendLogMessage("Connection Closed by BitGet. WebSocket Public Closed Event", LogMessageType.System);
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
                SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPrivate_Opened(object sender, EventArgs e)
        {
            try
            {
                CreateAuthMessageWebSocekt();
                SendLogMessage("Bitget WebSocket Private connection open", LogMessageType.System);
                _privateSocketOpen = true;
                CheckSocketsActivate();
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
                SendLogMessage("Connection Closed by BitGet. WebSocket Private Closed Event", LogMessageType.System);
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
                    SubscriblePrivate();
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
                SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 WebSocket check alive

        private DateTime TimeToSendPingPublic = DateTime.Now;
        private DateTime TimeToSendPingPrivate = DateTime.Now;

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
                        (_webSocketPublic.ReadyState == WebSocketState.Open ||
                        _webSocketPublic.ReadyState == WebSocketState.Connecting)
                        )
                    {
                        if (TimeToSendPingPublic.AddSeconds(25) < DateTime.Now)
                        {
                            _webSocketPublic.Send("ping");
                            TimeToSendPingPublic = DateTime.Now;
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
                        (_webSocketPrivate.ReadyState == WebSocketState.Open ||
                        _webSocketPrivate.ReadyState == WebSocketState.Connecting)
                        )
                    {
                        if (TimeToSendPingPrivate.AddSeconds(25) < DateTime.Now)
                        {
                            _webSocketPrivate.Send("ping");
                            TimeToSendPingPrivate = DateTime.Now;
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

        #region 9 Security subscrible

        private RateGate _rateGateSubscrible = new RateGate(1, TimeSpan.FromMilliseconds(350));

        private List<Security> _subscribledSecutiries = new List<Security>();

        public void Subscrible(Security security)
        {
            try
            {
                _rateGateSubscrible.WaitToProceed();
                CreateSubscribleSecurityMessageWebSocket(security);

            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void CreateSubscribleSecurityMessageWebSocket(Security security)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (_subscribledSecutiries != null)
                {
                    for (int i = 0; i < _subscribledSecutiries.Count; i++)
                    {
                        if (_subscribledSecutiries[i].Name.Equals(security.Name))
                        {
                            return;
                        }
                    }
                }

                _subscribledSecutiries.Add(security);

                _webSocketPublic.Send($"{{\"op\": \"subscribe\",\"args\": [{{\"instType\": \"{security.NameClass}\",\"channel\": \"books15\",\"instId\": \"{security.Name}\"}}]}}");
                _webSocketPublic.Send($"{{\"op\": \"subscribe\",\"args\": [{{ \"instType\": \"{security.NameClass}\",\"channel\": \"trade\",\"instId\": \"{security.Name}\"}}]}}");
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private void SubscriblePrivate()
        {
            try
            {
                for (int i = 0; i < _listCoin.Count; i++)
                {
                    _webSocketPrivate.Send($"{{\"op\": \"subscribe\",\"args\": [{{\"instType\": \"{_listCoin[i]}\",\"channel\": \"account\",\"coin\": \"default\"}}]}}");
                    _webSocketPrivate.Send($"{{\"op\": \"subscribe\",\"args\": [{{\"instType\": \"{_listCoin[i]}\",\"channel\": \"positions\",\"coin\": \"default\"}}]}}");
                    _webSocketPrivate.Send($"{{\"op\": \"subscribe\",\"args\": [{{\"instType\": \"{_listCoin[i]}\",\"channel\": \"orders\"}}]}}");
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Error);
            }
        }

        private void UnsubscribeFromAllWebSockets()
        {
            if (_webSocketPublic != null)
            {
                try
                {
                    if (_subscribledSecutiries != null)
                    {
                        for (int i = 0; i < _subscribledSecutiries.Count; i++)
                        {
                            _webSocketPublic.Send($"{{\"op\": \"unsubscribe\",\"args\": [{{\"instType\": \"{_subscribledSecutiries[i].NameClass}\",\"channel\": \"books15\",\"instId\": \"{_subscribledSecutiries[i].Name}\"}}]}}");
                            _webSocketPublic.Send($"{{\"op\": \"unsubscribe\",\"args\": [{{\"instType\": \"{_subscribledSecutiries[i].NameClass}\",\"channel\": \"trade\",\"instId\": \"{_subscribledSecutiries[i].Name}\"}}]}}");
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }

            if (_webSocketPrivate != null)
            {
                try
                {
                    for (int i = 0; i < _listCoin.Count; i++)
                    {
                        _webSocketPrivate.Send($"{{\"op\": \"unsubscribe\",\"args\": [{{\"instType\": \"{_listCoin[i]}\",\"channel\": \"account\",\"coin\": \"default\"}}]}}");
                        _webSocketPrivate.Send($"{{\"op\": \"unsubscribe\",\"args\": [{{\"instType\": \"{_listCoin[i]}\",\"channel\": \"positions\",\"coin\": \"default\"}}]}}");
                        _webSocketPrivate.Send($"{{\"op\": \"unsubscribe\",\"args\": [{{\"instType\": \"{_listCoin[i]}\",\"channel\": \"orders\",\"coin\": \"default\"}}]}}");
                    }
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

                    ResponseWebSocketMessageSubscrible SubscribleState = null;

                    try
                    {
                        SubscribleState = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageSubscrible());
                    }
                    catch (Exception error)
                    {
                        SendLogMessage("Error in message reader: " + error.ToString(), LogMessageType.Error);
                        SendLogMessage("message str: \n" + message, LogMessageType.Error);
                        continue;
                    }

                    if (SubscribleState.code != null)
                    {
                        if (SubscribleState.code.Equals("0") == false)
                        {
                            SendLogMessage("WebSocket listener error", LogMessageType.Error);
                            SendLogMessage(SubscribleState.code + "\n" +
                                SubscribleState.msg, LogMessageType.Error);

                            if (_lastConnectionStartTime.AddMinutes(5) > DateTime.Now)
                            { // если на старте вёб-сокета проблемы, то надо его перезапускать
                                ServerStatus = ServerConnectStatus.Disconnect;
                                DisconnectEvent();
                            }
                        }

                        continue;
                    }
                    else
                    {
                        ResponseWebSocketMessageAction<object> action = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<object>());

                        if (action.arg != null)
                        {
                            if (action.arg.channel.Equals("books15"))
                            {
                                UpdateDepth(message);
                                continue;
                            }
                            if (action.arg.channel.Equals("trade"))
                            {
                                UpdateTrade(message);
                                continue;
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

                    ResponseWebSocketMessageSubscrible SubscribleState = null;

                    try
                    {
                        SubscribleState = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageSubscrible());
                    }
                    catch (Exception error)
                    {
                        SendLogMessage("Error in message reader: " + error.ToString(), LogMessageType.Error);
                        SendLogMessage("message str: \n" + message, LogMessageType.Error);
                        continue;
                    }

                    if (SubscribleState.code != null)
                    {
                        if (SubscribleState.code.Equals("0") == false)
                        {
                            SendLogMessage("WebSocket listener error", LogMessageType.Error);
                            SendLogMessage(SubscribleState.code + "\n" +
                                SubscribleState.msg, LogMessageType.Error);

                            if (_lastConnectionStartTime.AddMinutes(5) > DateTime.Now)
                            { // если на старте вёб-сокета проблемы, то надо его перезапускать
                                ServerStatus = ServerConnectStatus.Disconnect;
                                DisconnectEvent();
                            }
                        }

                        continue;
                    }
                    else
                    {
                        ResponseWebSocketMessageAction<object> action = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<object>());

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
            ResponseWebSocketMessageAction<List<ResponseMessagePositions>> positions = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<List<ResponseMessagePositions>>());

            if (positions.data == null)
            {
                return;
            }

            Portfolio portfolio = new Portfolio();
            portfolio.Number = "BitGetFutures";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            if (positions != null)
            {
                if (positions.data.Count > 0)
                {
                    for (int i = 0; i < positions.data.Count; i++)
                    {
                        PositionOnBoard pos = new PositionOnBoard();
                        pos.PortfolioName = "BitGetFutures";
                        pos.SecurityNameCode = positions.data[i].instId;

                        if (positions.data[i].posMode == "hedge_mode")
                        {
                            if(positions.data[i].holdSide == "long")
                            {
                                pos.SecurityNameCode = positions.data[i].instId + "_" + "LONG";
                            }
                            if (positions.data[i].holdSide == "short")
                            {
                                pos.SecurityNameCode = positions.data[i].instId + "_" + "SHORT";
                            }
                        }

                        if (positions.data[i].holdSide == "long")
                        {
                            pos.ValueCurrent = positions.data[i].available.ToDecimal();
                            pos.ValueBlocked = positions.data[i].frozen.ToDecimal();
                        }
                        else if (positions.data[i].holdSide == "short")
                        {
                            pos.ValueCurrent = positions.data[i].available.ToDecimal() * -1;
                            pos.ValueBlocked = positions.data[i].frozen.ToDecimal();
                        }

                        if (_portfolioIsStarted == false)
                        {
                            pos.ValueBegin = pos.ValueCurrent;
                        }

                        portfolio.SetNewPosition(pos);

                        if (!_allPositions.ContainsKey(positions.arg.instType))
                        {
                            _allPositions.Add(positions.arg.instType, new List<string>());
                        }

                        if (!_allPositions[positions.arg.instType].Contains(pos.SecurityNameCode))
                        {
                            _allPositions[positions.arg.instType].Add(pos.SecurityNameCode);
                        }
                    }
                }

                if (_allPositions.ContainsKey(positions.arg.instType))
                {
                    if (_allPositions[positions.arg.instType].Count > 0)
                    {
                        for (int indAllPos = 0; indAllPos < _allPositions[positions.arg.instType].Count; indAllPos++)
                        {
                            bool isInData = false;

                            if (positions.data.Count > 0)
                            {
                                for (int indData = 0; indData < positions.data.Count; indData++)
                                {
                                    if (positions.data[indData].posMode == "hedge_mode")
                                    {
                                        if (_allPositions[positions.arg.instType][indAllPos] == positions.data[indData].instId + "_" + positions.data[indData].holdSide.ToUpper())
                                        {
                                            isInData = true;
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        if (_allPositions[positions.arg.instType][indAllPos] == positions.data[indData].instId)
                                        {
                                            isInData = true;
                                            break;
                                        }
                                    }
                                }
                            }

                            if (!isInData)
                            {
                                PositionOnBoard pos = new PositionOnBoard();
                                pos.PortfolioName = "BitGetFutures";
                                pos.SecurityNameCode = _allPositions[positions.arg.instType][indAllPos];
                                pos.ValueCurrent = 0;
                                pos.ValueBlocked = 0;

                                portfolio.SetNewPosition(pos);

                                _allPositions[positions.arg.instType].RemoveAt(indAllPos);
                                indAllPos--;
                            }
                        }
                    }
                }
            }
            else
            {
                SendLogMessage("BITGET ERROR. NO POSITIONS IN REQUEST.", LogMessageType.Error);
            }
            _portfolioIsStarted = true;

            PortfolioEvent(new List<Portfolio> { portfolio });
        }

        private void UpdateAccount(string message)
        {
            try
            {
                ResponseWebSocketMessageAction<List<ResponseWebSocketAccount>> assets = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<List<ResponseWebSocketAccount>>());

                if (assets.data == null ||
                    assets.data.Count == 0)
                {
                    return;
                }

                Portfolio portfolio = new Portfolio();
                portfolio.Number = "BitGetFutures";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;


                for (int i = 0; i < assets.data.Count; i++)
                {
                    PositionOnBoard pos = new PositionOnBoard();

                    pos.PortfolioName = "BitGetFutures";
                    pos.SecurityNameCode = assets.data[i].marginCoin;
                    pos.ValueBlocked = assets.data[i].frozen.ToDecimal();
                    pos.ValueCurrent = assets.data[i].available.ToDecimal();

                    portfolio.SetNewPosition(pos);
                }

                PortfolioEvent(new List<Portfolio> { portfolio });
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
                ResponseWebSocketMessageAction<List<ResponseWebSocketOrder>> order = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<List<ResponseWebSocketOrder>>());

                if (order.data == null ||
                    order.data.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < order.data.Count; i++)
                {
                    ResponseWebSocketOrder item = order.data[i];

                    if (string.IsNullOrEmpty(item.orderId))
                    {
                        continue;
                    }

                    OrderStateType stateType = GetOrderState(item.status);

                    if (item.orderType.Equals("market") &&
                        stateType != OrderStateType.Done &&
                        stateType != OrderStateType.Partial)
                    {
                        continue;
                    }



                    Order newOrder = new Order();
                    newOrder.SecurityNameCode = item.instId;
                    newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.cTime));
                    int.TryParse(item.clientOId, out newOrder.NumberUser);
                    newOrder.NumberMarket = item.orderId.ToString();
                    newOrder.Side = GetSide(item.tradeSide, item.side);
                    newOrder.State = stateType;
                    newOrder.Volume = item.size.ToDecimal();
                    newOrder.Price = item.price.ToDecimal();
                    newOrder.ServerType = ServerType.BitGetFutures;
                    newOrder.PortfolioNumber = "BitGetFutures";
                    newOrder.SecurityClassCode = order.arg.instType.ToString();



                    if (item.orderType.Equals("market"))
                    {
                        newOrder.TypeOrder = OrderPriceType.Market;
                    }
                    else
                    {
                        newOrder.TypeOrder = OrderPriceType.Limit;
                    }

                    if (stateType == OrderStateType.Partial)
                    {
                        MyOrderEvent(newOrder);

                        MyTrade myTrade = new MyTrade();
                        myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.fillTime));
                        myTrade.NumberOrderParent = item.orderId.ToString();
                        myTrade.NumberTrade = item.tradeId;
                        myTrade.Volume = item.baseVolume.ToDecimal();
                        myTrade.Price = item.fillPrice.ToDecimal();
                        myTrade.SecurityNameCode = item.instId;
                        myTrade.Side = GetSide(item.tradeSide, item.side);

                        MyTradeEvent(myTrade);

                        return;
                    }
                    else if (stateType == OrderStateType.Done)
                    {
                        MyOrderEvent(newOrder);

                        MyTrade myTrade = new MyTrade();
                        myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.fillTime));
                        myTrade.NumberOrderParent = item.orderId.ToString();
                        myTrade.NumberTrade = item.tradeId;
                        myTrade.Volume = item.baseVolume.ToDecimal();

                        if (myTrade.Volume > 0)
                        {
                            myTrade.Price = item.fillPrice.ToDecimal();
                            myTrade.SecurityNameCode = item.instId;
                            myTrade.Side = GetSide(item.tradeSide, item.side);

                            MyTradeEvent(myTrade);
                        }

                        return;
                    }
                    else
                    {
                        MyOrderEvent(newOrder);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private Side GetSide(string tradeSide, string side)
        {
            if (tradeSide == "close")
            {
                return side == "buy" ? Side.Sell : Side.Buy;
            }
            return side == "buy" ? Side.Buy : Side.Sell;
        }

        private void UpdateTrade(string message)
        {
            try
            {
                ResponseWebSocketMessageAction<List<ResponseWebsocketTrade>> responseTrade = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<List<ResponseWebsocketTrade>>());

                if (responseTrade == null)
                {
                    return;
                }

                if (responseTrade.data == null)
                {
                    return;
                }

                if (responseTrade.data[0] == null)
                {
                    return;
                }

                Trade trade = new Trade();
                trade.SecurityNameCode = responseTrade.arg.instId;
                trade.Price = responseTrade.data[0].price.ToDecimal();
                trade.Id = responseTrade.data[0].tradeId;

                if (trade.Id == null)
                {
                    return;
                }

                trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseTrade.data[0].ts));
                trade.Volume = responseTrade.data[0].size.ToDecimal();
                trade.Side = responseTrade.data[0].side.Equals("buy") ? Side.Buy : Side.Sell;

                NewTradesEvent(trade);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private void UpdateDepth(string message)
        {
            try
            {
                ResponseWebSocketMessageAction<List<ResponseWebSocketDepthItem>> responseDepth = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<List<ResponseWebSocketDepthItem>>());

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
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private DateTime _lastTimeMd;

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        #endregion

        #region 11 Trade

        private RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(200));

        private RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(200));

        public void SendOrder(Order order)
        {
            try
            {
                string trSide = "open";
                string posSide;

                if (_hedgeMode)
                {
                    if (order.PositionConditionType == OrderPositionConditionType.Close)
                    {
                        trSide = "close";
                        posSide = order.Side == Side.Buy ? "sell" : "buy";
                    }
                    else
                    {
                        trSide = "open";
                        posSide = order.Side == Side.Buy ? "buy" : "sell";
                    }
                }
                else
                {
                    posSide = order.Side == Side.Buy ? "buy" : "sell";
                }

                _rateGateSendOrder.WaitToProceed();

                Dictionary<string, dynamic> jsonContent = new Dictionary<string, dynamic>();

                jsonContent.Add("symbol", order.SecurityNameCode);
                jsonContent.Add("productType", order.SecurityClassCode.ToLower());
                jsonContent.Add("marginMode", _marginMode);
                jsonContent.Add("marginCoin", order.SecurityClassCode.Split('-')[0]);
                jsonContent.Add("side", posSide);
                jsonContent.Add("orderType", order.TypeOrder.ToString().ToLower());
                jsonContent.Add("price", order.Price.ToString().Replace(",", "."));
                jsonContent.Add("size", order.Volume.ToString().Replace(",", "."));
                jsonContent.Add("clientOid", order.NumberUser);

                if (_hedgeMode)
                {
                    jsonContent.Add("tradeSide", trSide);
                }

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                HttpResponseMessage responseMessage = CreatePrivateQueryOrders("/api/v2/mix/order/place-order", Method.POST.ToString(), null, jsonRequest);
                string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

                ResponseRestMessage<object> stateResponse = JsonConvert.DeserializeAnonymousType(JsonResponse, new ResponseRestMessage<object>());

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    if (stateResponse.code.Equals("00000") == true)
                    {
                        // ignore
                    }
                    else
                    {
                        CreateOrderFail(order);
                        SendLogMessage($"Code: {stateResponse.code}\n"
                            + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    CreateOrderFail(order);
                    SendLogMessage($"Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                    if (stateResponse != null && stateResponse.code != null)
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n"
                            + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        public void CancelOrder(Order order)
        {
            try
            {
                _rateGateCancelOrder.WaitToProceed();

                Dictionary<string, string> jsonContent = new Dictionary<string, string>();

                jsonContent.Add("symbol", order.SecurityNameCode);
                jsonContent.Add("productType", order.SecurityClassCode.ToLower());
                jsonContent.Add("orderId", order.NumberMarket);

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                HttpResponseMessage response = CreatePrivateQueryOrders("/api/v2/mix/order/cancel-order", Method.POST.ToString(), null, jsonRequest);
                string JsonResponse = response.Content.ReadAsStringAsync().Result;

                ResponseRestMessage<object> stateResponse = JsonConvert.DeserializeAnonymousType(JsonResponse, new ResponseRestMessage<object>());

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    if (stateResponse.code.Equals("00000") == true)
                    {
                        // ignore
                    }
                    else
                    {
                        CreateOrderFail(order);
                        SendLogMessage($"Code: {stateResponse.code}\n"
                            + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    CreateOrderFail(order);
                    SendLogMessage($"Http State Code: {response.StatusCode}", LogMessageType.Error);

                    if (stateResponse != null && stateResponse.code != null)
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n"
                            + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
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
            try
            {
                string path = "/api/v2/mix/order/detail?symbol=" + order.SecurityNameCode + "&productType=" + order.SecurityClassCode + "&clientOid=" + order.NumberUser;

                IRestResponse responseMessage = CreatePrivateQuery(path, Method.GET, null, null);
                string json = responseMessage.Content;

                ResponseRestMessage<DataOrderStatus> stateResponse = JsonConvert.DeserializeAnonymousType(json, new ResponseRestMessage<DataOrderStatus>());

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    if (stateResponse.code.Equals("00000") == true)
                    {
                        Order newOrder = new Order();

                        OrderStateType stateType = GetOrderState(stateResponse.data.state);

                        newOrder.SecurityNameCode = stateResponse.data.symbol;
                        newOrder.SecurityClassCode = stateResponse.data.marginCoin + "-FUTURES";
                        newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(stateResponse.data.cTime));
                        int.TryParse(stateResponse.data.clientOid, out newOrder.NumberUser);
                        newOrder.NumberMarket = stateResponse.data.orderId.ToString();
                        newOrder.Side = stateResponse.data.side == "buy" ? Side.Buy : Side.Sell;
                        newOrder.State = stateType;
                        newOrder.Volume = stateResponse.data.size.ToDecimal();
                        newOrder.Price = stateResponse.data.price.ToDecimal();
                        newOrder.ServerType = ServerType.BitGetFutures;
                        newOrder.PortfolioNumber = "BitGetFutures";
                        newOrder.TypeOrder = stateResponse.data.orderType == "limit" ? OrderPriceType.Limit : OrderPriceType.Market;

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
                    else
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n"
                            , LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private void FindMyTradesToOrder(Order order)
        {
            try
            {
                string path = $"/api/v2/mix/order/fills?symbol={order.SecurityNameCode}&productType={order.SecurityClassCode}";

                IRestResponse responseMessage = CreatePrivateQuery(path, Method.GET, null, null);
                string json = responseMessage.Content;

                RestMyTradesResponce stateResponse = JsonConvert.DeserializeAnonymousType(json, new RestMyTradesResponce());

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    if (stateResponse.code.Equals("00000") == true)
                    {
                        for (int i = 0; i < stateResponse.data.fillList.Count; i++)
                        {
                            FillList item = stateResponse.data.fillList[i];

                            MyTrade myTrade = new MyTrade();
                            myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.cTime));
                            myTrade.NumberOrderParent = item.orderId.ToString();
                            myTrade.NumberTrade = item.tradeId;
                            myTrade.Volume = item.baseVolume.ToDecimal();
                            myTrade.Price = item.price.ToDecimal();
                            myTrade.SecurityNameCode = item.symbol.ToUpper();
                            myTrade.Side = item.side == "buy" ? Side.Buy : Side.Sell;

                            MyTradeEvent(myTrade);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n"
                            + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.Message, LogMessageType.Error);
            }
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            try
            {
                _rateGateCancelOrder.WaitToProceed();

                Dictionary<string, string> jsonContent = new Dictionary<string, string>();

                jsonContent.Add("symbol", security.Name);
                jsonContent.Add("productType", security.NameClass);

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                CreatePrivateQueryOrders("/api/v2/mix/order/cancel-all-orders", Method.POST.ToString(), null, jsonRequest);
            }
            catch (Exception e)
            {
                SendLogMessage(e.Message, LogMessageType.Error);
            }
        }

        public void CancelAllOrders()
        {
            try
            {
                _rateGateCancelOrder.WaitToProceed();

                for (int i = 0; i < _listCoin.Count; i++)
                {
                    Dictionary<string, string> jsonContent = new Dictionary<string, string>();
                    jsonContent.Add("productType", _listCoin[i]);

                    string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                    CreatePrivateQueryOrders("/api/v2/mix/order/cancel-all-orders", Method.POST.ToString(), null, jsonRequest);
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.Message, LogMessageType.Error);
            }
        }

        public List<Order> GetAllOpenOrders()
        {
            try
            {
                for (int i = 0; i < _listCoin.Count; i++)
                {
                    IRestResponse responseMessage = CreatePrivateQuery($"/api/v2/mix/order/orders-pending?productType={_listCoin[i]}", Method.GET, null, null);
                    string json = responseMessage.Content;

                    ResponseRestMessage<RestMessageOrders> stateResponse = JsonConvert.DeserializeAnonymousType(json, new ResponseRestMessage<RestMessageOrders>());

                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        if (stateResponse.code.Equals("00000") == true)
                        {
                            if (stateResponse.data.entrustedList == null)
                            {
                                return null;
                            }

                            List<Order> orders = new List<Order>();

                            for (int ind = 0; ind < stateResponse.data.entrustedList.Count; ind++)
                            {
                                Order curOder = ConvertRestToOrder(stateResponse.data.entrustedList[ind]);
                                orders.Add(curOder);
                            }

                            return orders;
                        }
                        else
                        {
                            SendLogMessage($"Code: {stateResponse.code}\n"
                                + $"Message: {stateResponse.msg}", LogMessageType.Error);
                        }
                    }
                }
                return null;
            }
            catch (Exception e)
            {
                SendLogMessage(e.Message, LogMessageType.Error);
                return null;
            }
        }

        private Order ConvertRestToOrder(EntrustedList item)
        {
            Order newOrder = new Order();

            OrderStateType stateType = GetOrderState(item.status);

            newOrder.SecurityNameCode = item.symbol;
            newOrder.SecurityClassCode = item.marginCoin + "-FUTURES";
            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.cTime));
            int.TryParse(item.clientOid, out newOrder.NumberUser);
            newOrder.NumberMarket = item.orderId.ToString();
            newOrder.Side = item.side == "buy" ? Side.Buy : Side.Sell;
            newOrder.State = stateType;
            newOrder.Volume = item.size.ToDecimal();
            newOrder.Price = item.price.ToDecimal();
            newOrder.ServerType = ServerType.BitGetFutures;
            newOrder.PortfolioNumber = "BitGetFutures";
            newOrder.TypeOrder = item.orderType == "limit" ? OrderPriceType.Limit : OrderPriceType.Market;

            return newOrder;
        }

        private OrderStateType GetOrderState(string orderStateResponse)
        {
            OrderStateType stateType;

            switch (orderStateResponse)
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

        private void CreateOrderFail(Order order)
        {
            order.State = OrderStateType.Fail;

            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        #endregion

        #region 12 Queries

        private IRestResponse CreatePrivateQuery(string path, Method method, string queryString, string body)
        {
            try
            {
                RestRequest requestRest = new RestRequest(path, method);

                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string signature = GenerateSignature(timestamp, method.ToString(), path, queryString, body, SeckretKey);

                requestRest.AddHeader("ACCESS-KEY", PublicKey);
                requestRest.AddHeader("ACCESS-SIGN", signature);
                requestRest.AddHeader("ACCESS-TIMESTAMP", timestamp);
                requestRest.AddHeader("ACCESS-PASSPHRASE", Passphrase);
                requestRest.AddHeader("X-CHANNEL-API-CODE", "6yq7w");

                RestClient client = new RestClient(BaseUrl);

                IRestResponse response = client.Execute(requestRest);

                return response;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        private HttpResponseMessage CreatePrivateQueryOrders(string path, string method, string queryString, string body)
        {
            try
            {
                HttpClient _httpClient = new HttpClient();

                string requestPath = path;
                string url = $"{BaseUrl}{requestPath}";
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string signature = GenerateSignature(timestamp, method, requestPath, queryString, body, SeckretKey);

                _httpClient.DefaultRequestHeaders.Add("ACCESS-KEY", PublicKey);
                _httpClient.DefaultRequestHeaders.Add("ACCESS-SIGN", signature);
                _httpClient.DefaultRequestHeaders.Add("ACCESS-TIMESTAMP", timestamp);
                _httpClient.DefaultRequestHeaders.Add("ACCESS-PASSPHRASE", Passphrase);
                _httpClient.DefaultRequestHeaders.Add("X-CHANNEL-API-CODE", "6yq7w");

                if (method.Equals("POST"))
                {
                    return _httpClient.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json")).Result;
                }
                else
                {
                    return _httpClient.GetAsync(url).Result;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        private string GenerateSignature(string timestamp, string method, string requestPath, string queryString, string body, string secretKey)
        {
            method = method.ToUpper();
            body = string.IsNullOrEmpty(body) ? string.Empty : body;
            queryString = string.IsNullOrEmpty(queryString) ? string.Empty : "?" + queryString;

            string preHash = timestamp + method + requestPath + queryString + body;

            byte[] secretKeyBytes = Encoding.UTF8.GetBytes(secretKey);
            using (HMACSHA256 hmac = new HMACSHA256(secretKeyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(preHash));
                return Convert.ToBase64String(hashBytes);
            }
        }

        private void SetPositionMode()
        {
            try
            {
                for (int i = 0; i < _listCoin.Count; i++)
                {
                    Dictionary<string, string> jsonContent = new Dictionary<string, string>();

                    jsonContent.Add("posMode", _hedgeMode == true ? "hedge_mode" : "one_way_mode");
                    jsonContent.Add("productType", _listCoin[i]);

                    string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                    HttpResponseMessage responseMessage = CreatePrivateQueryOrders("/api/v2/mix/account/set-position-mode", Method.POST.ToString(), null, jsonRequest);
                    string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

                    ResponseRestMessage<object> stateResponse = JsonConvert.DeserializeAnonymousType(JsonResponse, new ResponseRestMessage<object>());

                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        if (stateResponse.code.Equals("00000") == true)
                        {
                            // ignore
                        }
                        else
                        {
                            SendLogMessage($"SetPositionMode - Code: {stateResponse.code}\n"
                                + $"Message: {stateResponse.msg}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage($"SetPositionMode - Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                        if (stateResponse != null && stateResponse.code != null)
                        {
                            SendLogMessage($"SetPositionMode - Code: {stateResponse.code}\n"
                                + $"Message: {stateResponse.msg}", LogMessageType.Error);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.Message, LogMessageType.Error);
            }
        }

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