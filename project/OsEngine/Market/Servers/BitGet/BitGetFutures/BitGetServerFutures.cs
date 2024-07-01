using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.BitGet.BitGetFutures.Entity;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.ZB;
using RestSharp;
using SuperSocket.ClientEngine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using WebSocket4Net;

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
        }
    }

    public class BitGetServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public BitGetServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread thread = new Thread(MessageReader);
            thread.Name = "MessageReaderBitGet";
            thread.Start();

            Thread thread2 = new Thread(CheckAliveWebSocket);
            thread2.Name = "CheckAliveWebSocket";
            thread2.Start();

            Thread thread3 = new Thread(UpdatingPortfolio);
            thread3.Name = "UpdatingPortfolio";
            thread3.Start();
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

            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Ssl3
                | SecurityProtocolType.Tls11
                | SecurityProtocolType.Tls;

            string requestStr = "/api/mix/v1/market/contracts?productType=umcbl";
            RestRequest requestRest = new RestRequest(requestStr, Method.GET);
            IRestResponse response = new RestClient(BaseUrl).Execute(requestRest);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    TimeToSendPing = DateTime.Now;
                    TimeToUprdatePortfolio = DateTime.Now;
                    FIFOListWebSocketMessage = new ConcurrentQueue<string>();
                    CreateWebSocketConnection();
                    _lastConnectionStartTime = DateTime.Now;
                }
                catch (Exception exeption)
                {
                    SendLogMessage(exeption.ToString(), LogMessageType.Error);
                    SendLogMessage("Connection can be open. BitGet. Error request", LogMessageType.Error);
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                }
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

        public void Dispose()
        {
            try
            {
                _ordersIsSubscrible = false;
                _portfolioIsStarted = false;
                _subscribledSecutiries.Clear();
                DeleteWebscoektConnection();
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }

            FIFOListWebSocketMessage = new ConcurrentQueue<string>();

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

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            string requestStr = "/api/mix/v1/market/contracts?productType=umcbl";
            RestRequest requestRest = new RestRequest(requestStr, Method.GET);
            var json = new RestClient(BaseUrl).Execute(requestRest).Content;

            ResponseRestMessage<List<RestMessageSymbol>> symbols = JsonConvert.DeserializeAnonymousType(json, new ResponseRestMessage<List<RestMessageSymbol>>());

            List<Security> securities = new List<Security>();

            for (int i = 0; i < symbols.data.Count; i++)
            {
                var item = symbols.data[i];

                var decimals = Convert.ToInt32(item.pricePlace);
                var priceStep = (GetPriceStep(Convert.ToInt32(item.pricePlace), Convert.ToInt32(item.priceEndStep))).ToDecimal();

                if (item.symbolStatus.Equals("normal"))
                {
                    Security newSecurity = new Security();

                    newSecurity.Exchange = ServerType.BitGetFutures.ToString();
                    newSecurity.DecimalsVolume = Convert.ToInt32(item.volumePlace);
                    newSecurity.Lot = GetVolumeStep(newSecurity.DecimalsVolume);
                    newSecurity.Name = item.symbol;
                    newSecurity.NameFull = item.symbol;
                    newSecurity.NameClass = item.quoteCoin;
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

        private DateTime TimeToUprdatePortfolio = DateTime.Now;

        public void GetPortfolios()
        {
            CreateQueryPortfolio();
        }

        private void UpdatingPortfolio()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(1000);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        TimeToUprdatePortfolio = DateTime.Now;
                        continue;
                    }

                    if (TimeToUprdatePortfolio.AddSeconds(50) < DateTime.Now)
                    {
                        TimeToUprdatePortfolio = DateTime.Now;
                        Thread updater = new Thread(CreateQueryPortfolio);
                        updater.Start();
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(3000);
                }
            }
        }

        private RateGate _rateGatePortfolioUpdater = new RateGate(1, TimeSpan.FromMilliseconds(3000));

        private void CreateQueryPortfolio()
        {
            try
            {
                _rateGatePortfolioUpdater.WaitToProceed();

                IRestResponse responseMessage = CreatePrivateQuery("/api/mix/v1/account/accounts?productType=umcbl", Method.GET, null, null);
                string json = responseMessage.Content;

                ResponseRestMessage<object> stateResponse = JsonConvert.DeserializeAnonymousType(json, new ResponseRestMessage<object>());

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    if (stateResponse.code.Equals("00000") == true)
                    {
                        UpdatePorfolio(json);
                    }
                    else
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n"
                            + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                    if (stateResponse != null && stateResponse.code != null)
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n"
                            + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private ResponseRestMessage<List<RestMessagePositions>> CreateQueryPositions()
        {
            try
            {
                IRestResponse responseMessage = CreatePrivateQuery("/api/mix/v1/position/allPosition?productType=umcbl", Method.GET, null, null);
                string json = responseMessage.Content;

                ResponseRestMessage<object> stateResponse = JsonConvert.DeserializeAnonymousType(json, new ResponseRestMessage<object>());

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    if (stateResponse.code.Equals("00000") == true)
                    {
                        ResponseRestMessage<List<RestMessagePositions>> ResponsePositions = JsonConvert.DeserializeAnonymousType(json, new ResponseRestMessage<List<RestMessagePositions>>());

                        return ResponsePositions;
                    }
                    else
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n"
                            + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                    if (stateResponse != null && stateResponse.code != null)
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n"
                            + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private bool _portfolioIsStarted = false;

        private void UpdatePorfolio(string json)
        {
            ResponseRestMessage<List<RestMessageAccount>> assets = JsonConvert.DeserializeAnonymousType(json, new ResponseRestMessage<List<RestMessageAccount>>());
            var Positions = CreateQueryPositions();

            Portfolio portfolio = new Portfolio();
            portfolio.Number = "BitGetFutures";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;


            for (int i = 0; i < assets.data.Count; i++)
            {
                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = "BitGetFutures";
                pos.SecurityNameCode = assets.data[i].marginCoin;
                pos.ValueBlocked = assets.data[i].locked.ToDecimal();

                if (string.IsNullOrEmpty(assets.data[i].unrealizedPL))
                {
                    pos.ValueCurrent = assets.data[i].available.ToDecimal();
                }
                else
                {
                    pos.ValueCurrent = (assets.data[i].available.ToDecimal() + assets.data[i].unrealizedPL.ToDecimal());
                }


                if (_portfolioIsStarted == false)
                {
                    pos.ValueBegin = pos.ValueCurrent;
                }

                portfolio.SetNewPosition(pos);
            }

            if (Positions != null)
            {
                List<PositionOnBoard> currentPositions = new List<PositionOnBoard>();

                for (int i = 0; i < Positions.data.Count; i++)
                {
                    PositionOnBoard pos = new PositionOnBoard();
                    pos.PortfolioName = "BitGetFutures";
                    pos.SecurityNameCode = Positions.data[i].symbol + "_" + Positions.data[i].holdSide;

                    if (Positions.data[i].holdSide == "long")
                    {
                        pos.ValueCurrent = Positions.data[i].total.ToDecimal();
                        pos.ValueBlocked = Positions.data[i].openDelegateCount.ToDecimal();
                    }
                    else if (Positions.data[i].holdSide == "short")
                    {
                        pos.ValueCurrent = Positions.data[i].total.ToDecimal() * -1;
                        pos.ValueBlocked = Positions.data[i].openDelegateCount.ToDecimal() * -1;
                    }
                    else
                    {
                        pos.ValueCurrent = Positions.data[i].total.ToDecimal();
                        pos.ValueBlocked = Positions.data[i].openDelegateCount.ToDecimal();
                    }

                    if (_portfolioIsStarted == false)
                    {
                        pos.ValueBegin = pos.ValueCurrent;
                    }

                    currentPositions.Add(pos);
                }

                // добавляем в общий массив имеющихся позиций, если ещё не добавили
                for (int i = 0; i < currentPositions.Count; i++)
                {
                    PositionOnBoard curPos = currentPositions[i];

                    bool isInArray = false;

                    for (int j = 0; j < _allPositions.Count; j++)
                    {
                        if (curPos.SecurityNameCode == _allPositions[j].SecurityNameCode)
                        {
                            isInArray = true;
                            _allPositions[j] = curPos;
                            break;
                        }
                    }

                    if (isInArray == false)
                    {
                        _allPositions.Add(curPos);
                    }
                }

                // проверяем чтобы все обнулённые позиции были с правильными значениями

                for (int i = 0; i < _allPositions.Count; i++)
                {
                    PositionOnBoard curPos = _allPositions[i];

                    bool isInArray = false;

                    for (int j = 0; j < currentPositions.Count; j++)
                    {
                        if (curPos.SecurityNameCode == currentPositions[j].SecurityNameCode)
                        {
                            isInArray = true;
                            break;
                        }
                    }

                    if (isInArray == false)
                    {
                        curPos.ValueCurrent = 0;
                    }
                }

                for (int i = 0; i < _allPositions.Count; i++)
                {
                    portfolio.SetNewPosition(_allPositions[i]);
                }
            }
            else
            {
                SendLogMessage("BITGET ERROR. NO POSITIONS IN REQUEST. ", LogMessageType.Error);
            }

            _portfolioIsStarted = true;

            PortfolioEvent(new List<Portfolio> { portfolio });
        }

        private List<PositionOnBoard> _allPositions = new List<PositionOnBoard>();

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            return GetCandleHistory(security.Name, 
                timeFrameBuilder.TimeFrameTimeSpan, false, 0, DateTime.Now);
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf, bool IsOsData, int CountToLoad, DateTime timeEnd)
        {

            string stringInterval = GetStringInterval(tf);
            int CountToLoadCandle = GetCountCandlesToLoad();

            List<Candle> candles = new List<Candle>();
            DateTime TimeToRequest = DateTime.UtcNow;

            if (IsOsData == true)
            {
                CountToLoadCandle = CountToLoad;
                TimeToRequest = timeEnd;
            }

            do
            {
                int limit = CountToLoadCandle;
                if (CountToLoadCandle > 1000)
                {
                    limit = 1000;
                }

                List<Candle> rangeCandles = new List<Candle>();

                rangeCandles = CreateQueryCandles(nameSec, stringInterval, limit, TimeToRequest.AddSeconds(10));

                rangeCandles.Reverse();

                candles.AddRange(rangeCandles);

                if (candles.Count != 0)
                {
                    TimeToRequest = candles[candles.Count - 1].TimeStart;
                }

                CountToLoadCandle -= limit;

            } while (CountToLoadCandle > 0);

            candles.Reverse();
            return candles;
        }

        private List<Candle> CreateQueryCandles(string nameSec, string stringInterval, int limit, DateTime timeEndToLoad)
        {
            string requestStr = $"/api/mix/v1/market/candles" + $"?symbol={nameSec}&startTime=0&granularity={stringInterval}&limit={limit}&endTime={TimeManager.GetTimeStampMilliSecondsToDateTime(timeEndToLoad)}";
            RestRequest requestRest = new RestRequest(requestStr, Method.GET);
            IRestResponse response = new RestClient(BaseUrl).Execute(requestRest);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }

            List<string[]> symbols = JsonConvert.DeserializeAnonymousType(response.Content, new List<string[]>());

            List<Candle> candles = new List<Candle>();

            foreach (var item in symbols)
            {
                candles.Add(new Candle()
                {
                    Close = item[4].ToDecimal(),
                    High = item[2].ToDecimal(),
                    Low = item[3].ToDecimal(),
                    Open = item[1].ToDecimal(),
                    Volume = item[5].ToDecimal(),
                    State = CandleState.Finished,
                    TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item[0]))
                });
            }

            return candles;

        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        private int GetCountCandlesToLoad()
        {
            var server = (AServer)ServerMaster.GetServers().Find(server => server.ServerType == ServerType.BitGetFutures);

            for (int i = 0; i < server.ServerParameters.Count; i++)
            {
                if (server.ServerParameters[i].Name.Equals(OsLocalization.Market.ServerParam6))
                {
                    var Param = (ServerParameterInt)server.ServerParameters[i];
                    return Param.Value;
                }
            }

            return 300;
        }

        private string GetStringInterval(TimeSpan tf)
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

        #endregion

        #region 6 WebSocket creation

        private string _socketLocker = "webSocketLockerBitGet";

        private string _webSocketUrl = "wss://ws.bitget.com/mix/v1/stream";

        private WebSocket _webSocket;

        private void CreateWebSocketConnection()
        {
            try
            {
                if (_webSocket != null)
                {
                    return;
                }
                lock (_socketLocker)
                {
                    _webSocket = new WebSocket(_webSocketUrl);
                    _webSocket.EnableAutoSendPing = true;
                    _webSocket.AutoSendPingInterval = 10;
                    _webSocket.Opened += WebSocket_Opened;
                    _webSocket.Closed += WebSocket_Closed;
                    _webSocket.MessageReceived += WebSocket_MessageReceived;
                    _webSocket.Error += WebSocket_Error;
                    _webSocket.Open();
                }
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }
        }

        private void DeleteWebscoektConnection()
        {
            try
            {
                lock (_socketLocker)
                {
                    if (_webSocket != null)
                    {
                        _webSocket.Opened -= WebSocket_Opened;
                        _webSocket.Closed -= WebSocket_Closed;
                        _webSocket.MessageReceived -= WebSocket_MessageReceived;
                        _webSocket.Error -= WebSocket_Error;

                        try
                        {
                            _webSocket.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        try
                        {
                            _webSocket.Dispose();
                        }
                        catch
                        {
                            // ignore
                        }

                        _webSocket = null;
                    }
                }
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }
            finally
            {
                _webSocket = null;
            }
        }

        private void CreateAuthMessageWebSocekt()
        {
            string TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            string Sign = GenerateSignature(TimeStamp, "GET", "/user/verify", null, null, SeckretKey);

            RequestWebsocketAuth requestWebsocketAuth = new RequestWebsocketAuth()
            {
                op = "login",
                args = new List<AuthItem>()
                 {
                      new AuthItem()
                      {
                           apiKey = PublicKey,
                            passphrase = Passphrase,
                             timestamp = TimeStamp,
                             sign = Sign
                      }
                 }
            };

            string AuthJson = JsonConvert.SerializeObject(requestWebsocketAuth);

            lock (_socketLocker)
            {
                _webSocket.Send(AuthJson);
            }
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocket_Opened(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    CreateAuthMessageWebSocekt();
                    SendLogMessage("Bitget webSocket connection open", LogMessageType.System);
                    ServerStatus = ServerConnectStatus.Connect;
                    ConnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocket_Closed(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Connect)
                {
                    SendLogMessage("Connection Closed by BitGet. WebSocket Closed Event", LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocket_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    return;
                }
                if (string.IsNullOrEmpty(e.Message))
                {
                    return;
                }
                if (e.Message.Length == 4)
                { // pong message
                    return;
                }

                if (FIFOListWebSocketMessage == null)
                {
                    return;
                }

                FIFOListWebSocketMessage.Enqueue(e.Message);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocket_Error(object sender, ErrorEventArgs e)
        {
            try
            {
                var error = e;

                if (error.Exception != null)
                {
                    SendLogMessage(error.Exception.ToString(), LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 WebSocket check alive

        private DateTime TimeToSendPing = DateTime.Now;

        private void CheckAliveWebSocket()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(1000);

                    if(ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        continue;
                    }

                    if (_webSocket != null &&
                        (_webSocket.State == WebSocketState.Open ||
                        _webSocket.State == WebSocketState.Connecting)
                        )
                    {
                        if (TimeToSendPing.AddSeconds(30) < DateTime.Now)
                        {
                            lock (_socketLocker)
                            {
                                _webSocket.Send("ping");
                            }

                            TimeToSendPing = DateTime.Now;
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

        private bool _ordersIsSubscrible = false;

        private List<string> _subscribledSecutiries = new List<string>();

        public void Subscrible(Security security)
        {
            try
            {
                _rateGateSubscrible.WaitToProceed();
                CreateSubscribleSecurityMessageWebSocket(security);

            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }
        }

        private void CreateSubscribleSecurityMessageWebSocket(Security security)
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            for (int i = 0; i < _subscribledSecutiries.Count; i++)
            {
                if (_subscribledSecutiries[i].Equals(security.Name))
                {
                    return;
                }
            }

            _subscribledSecutiries.Add(security.Name);

            lock (_socketLocker)
            {
                _webSocket.Send($"{{\"op\": \"subscribe\",\"args\": [{{\"instType\": \"mc\",\"channel\": \"books15\",\"instId\": \"{security.Name.Replace("_UMCBL", "")}\"}}]}}");
                _webSocket.Send($"{{\"op\": \"subscribe\",\"args\": [{{ \"instType\": \"mc\",\"channel\": \"trade\",\"instId\": \"{security.Name.Replace("_UMCBL", "")}\"}}]}}");

                if (_ordersIsSubscrible == false)
                {
                    _ordersIsSubscrible = true;
                    _webSocket.Send($"{{\"op\": \"subscribe\",\"args\": [{{\"channel\": \"orders\",\"instType\": \"UMCBL\",\"instId\": \"default\"}}]}}");
                }
            }
        }

        #endregion

        #region 10 WebSocket parsing the messages

        private ConcurrentQueue<string> FIFOListWebSocketMessage = new ConcurrentQueue<string>();

        private void MessageReader()
        {
            Thread.Sleep(5000);

            while (true)
            {
                try
                {
                    if(ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (FIFOListWebSocketMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message = null;

                    FIFOListWebSocketMessage.TryDequeue(out message);

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
                            if (action.arg.channel.Equals("orders"))
                            {
                                UpdateOrder(message);
                                continue;
                            }
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
                catch (Exception exeption)
                {
                    SendLogMessage(exeption.ToString(), LogMessageType.Error);
                    Thread.Sleep(3000);
                }
            }
        }

        private void UpdateOrder(string message)
        {
            ResponseWebSocketMessageAction<List<ResponseWebSocketOrder>> Order = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<List<ResponseWebSocketOrder>>());

            if (Order.data == null ||
                Order.data.Count == 0)
            {
                return;
            }

            for (int i = 0; i < Order.data.Count; i++)
            {
                var item = Order.data[i];

                OrderStateType stateType = GetOrderState(item.status);

                if (item.ordType.Equals("market") &&
                    stateType != OrderStateType.Done &&
                    stateType != OrderStateType.Patrial)
                {
                    continue;
                }

                Order newOrder = new Order();
                newOrder.SecurityNameCode = item.instId; //.Replace("_SPBL", "")
                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.cTime));

                if (string.IsNullOrEmpty(item.clOrdId) == false)
                {
                    try
                    {
                        newOrder.NumberUser = Convert.ToInt32(item.clOrdId);
                    }
                    catch
                    {
                        // ignore
                    }
                }

                newOrder.NumberMarket = item.ordId.ToString();
                newOrder.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;
                newOrder.State = stateType;
                newOrder.Volume = item.sz.ToDecimal();
                newOrder.Price = item.px.ToDecimal();
                newOrder.ServerType = ServerType.BitGetFutures;
                newOrder.PortfolioNumber = "BitGetFutures";

                if (item.ordType.Equals("market"))
                {
                    newOrder.TypeOrder = OrderPriceType.Market;
                }

                if (stateType == OrderStateType.Patrial)
                {
                    MyOrderEvent(newOrder);

                    MyTrade myTrade = new MyTrade();
                    myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.fillTime));
                    myTrade.NumberOrderParent = item.ordId.ToString();
                    myTrade.NumberTrade = item.tradeId;
                    myTrade.Volume = item.fillSz.ToDecimal();
                    myTrade.Price = item.fillPx.ToDecimal();
                    myTrade.SecurityNameCode = item.instId.ToUpper();
                    myTrade.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;

                    MyTradeEvent(myTrade);

                    if (DateTime.Now.AddSeconds(-45) < TimeToUprdatePortfolio)
                    {
                        TimeToUprdatePortfolio = DateTime.Now.AddSeconds(-45);
                    }

                    return;
                }
                else if (stateType == OrderStateType.Done)
                {
                    MyOrderEvent(newOrder);

                    MyTrade myTrade = new MyTrade();
                    myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.fillTime));
                    myTrade.NumberOrderParent = item.ordId.ToString();
                    myTrade.NumberTrade = item.tradeId;
                    myTrade.Volume = item.fillSz.ToDecimal();

                    if (myTrade.Volume > 0)
                    {
                        myTrade.Price = item.fillPx.ToDecimal();
                        myTrade.SecurityNameCode = item.instId.ToUpper();
                        myTrade.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;
                        MyTradeEvent(myTrade);
                    }

                    if (DateTime.Now.AddSeconds(-45) < TimeToUprdatePortfolio)
                    {
                        TimeToUprdatePortfolio = DateTime.Now.AddSeconds(-45);
                    }

                    return;
                }

                MyOrderEvent(newOrder);
            }
        }

        private List<MyTrade> myTrades = new List<MyTrade>();

        private void UpdateTrade(string message)
        {
            ResponseWebSocketMessageAction<List<List<string>>> responseTrade = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<List<List<string>>>());

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

            if (responseTrade.data[0].Count < 2)
            {
                return;
            }

            Trade trade = new Trade();
            trade.SecurityNameCode = responseTrade.arg.instId + "_UMCBL";

            trade.Price = responseTrade.data[0][1].ToDecimal();
            trade.Id = responseTrade.data[0][0];

            if (trade.Id == null)
            {
                return;
            }
            trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseTrade.data[0][0]));
            trade.Volume = responseTrade.data[0][2].ToDecimal();
            trade.Side = responseTrade.data[0][3].Equals("buy") ? Side.Buy : Side.Sell;

            NewTradesEvent(trade);
        }

        private void UpdateDepth(string message)
        {
            ResponseWebSocketMessageAction<List<ResponseWebSocketDepthItem>> responseDepth = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<List<ResponseWebSocketDepthItem>>());

            if (responseDepth.data == null)
            {
                return;
            }

            MarketDepth marketDepth = new MarketDepth();

            List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
            List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

            marketDepth.SecurityNameCode = responseDepth.arg.instId + "_UMCBL";

            for (int i = 0; i < responseDepth.data[0].asks.Count; i++)
            {
                ascs.Add(new MarketDepthLevel()
                {
                    Ask = responseDepth.data[0].asks[i][1].ToString().ToDecimal(),
                    Price = responseDepth.data[0].asks[i][0].ToString().ToDecimal()
                });
            }

            for (int i = 0; i < responseDepth.data[0].bids.Count; i++)
            {
                bids.Add(new MarketDepthLevel()
                {
                    Bid = responseDepth.data[0].bids[i][1].ToString().ToDecimal(),
                    Price = responseDepth.data[0].bids[i][0].ToString().ToDecimal()
                });
            }

            marketDepth.Asks = ascs;
            marketDepth.Bids = bids;

            marketDepth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseDepth.data[0].ts));


            MarketDepthEvent(marketDepth);
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        #endregion

        #region 11 Trade

        private RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        private RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        public void SendOrder(Order order)
        {
            string side = String.Empty;

            if (order.PositionConditionType == OrderPositionConditionType.Close)
            {
                side = order.Side == Side.Buy ? "close_short" : "close_long";
            }
            else
            {
                side = order.Side == Side.Buy ? "open_long" : "open_short";
            }


            _rateGateSendOrder.WaitToProceed();
            string jsonRequest = JsonConvert.SerializeObject(new
            {
                symbol = order.SecurityNameCode,
                marginCoin = order.SecurityClassCode,
                side = side,
                orderType = order.TypeOrder.ToString().ToLower(),
                timeInForceValue = "normal",
                price = order.Price.ToString().Replace(",", "."),
                size = order.Volume.ToString().Replace(",", "."),
                clientOid = order.NumberUser
            });

            HttpResponseMessage responseMessage = CreatePrivateQueryOrders("/api/mix/v1/order/placeOrder", Method.POST.ToString(), null, jsonRequest);
            string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

            ResponseRestMessage<object> stateResponse = JsonConvert.DeserializeAnonymousType(JsonResponse, new ResponseRestMessage<object>());

            if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
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

        public void CancelOrder(Order order)
        {
            _rateGateCancelOrder.WaitToProceed();

            string jsonRequest = JsonConvert.SerializeObject(new
            {
                symbol = order.SecurityNameCode,
                marginCoin = "USDT",
                orderId = order.NumberMarket
            });

            HttpResponseMessage response = CreatePrivateQueryOrders("/api/mix/v1/order/cancel-order", Method.POST.ToString(), null, jsonRequest);
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

        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllOpenOrders();

            if(orders == null)
            {
                return;
            }

            for(int i =0;i < orders.Count;i++)
            {
                if(MyOrderEvent != null)
                {
                    MyOrderEvent(orders[i]);
                }
            }
        }

        public void GetOrderStatus(Order order)
        {
            // GET /api/mix/v1/order/detail

            string path = "/api/mix/v1/order/detail?symbol=" + order.SecurityNameCode 
                + "&clientOid=" + order.NumberUser;

            IRestResponse responseMessage 
                = CreatePrivateQuery(path, Method.GET, null, null);
            string json = responseMessage.Content;

            RestOrderStatusResponce stateResponse 
                = JsonConvert.DeserializeAnonymousType(json, new RestOrderStatusResponce());

            if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
            {
                if (stateResponse.code.Equals("00000") == true)
                {
                    Order curOder = ConvertRestToOrder(stateResponse.data);

                    if(curOder != null
                        && MyOrderEvent != null)
                    {
                        MyOrderEvent(curOder);
                    }

                    if(curOder.State == OrderStateType.Done ||
                        curOder.State == OrderStateType.Patrial)
                    {
                        FindMyTradesToOrder(curOder);
                    }
                }
                else
                {
                    SendLogMessage($"Code: {stateResponse.code}\n"
                        , LogMessageType.Error);
                }
            }
        }

        private void FindMyTradesToOrder(Order order)
        {
            //GET /api/mix/v1/order/fills

            string path = "/api/mix/v1/order/fills?symbol=" + order.SecurityNameCode
             + "&orderId=" + order.NumberMarket;

            IRestResponse responseMessage = CreatePrivateQuery(path, Method.GET, null, null);
            string json = responseMessage.Content;

            RestMyTradesResponce stateResponse = JsonConvert.DeserializeAnonymousType(json, new RestMyTradesResponce());

            if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
            {
                if (stateResponse.code.Equals("00000") == true)
                {
                    for (int i = 0; i < stateResponse.data.Count; i++)
                    {
                        RestMyTrade item = stateResponse.data[i];

                        MyTrade myTrade = new MyTrade();
                        myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ctime));
                        myTrade.NumberOrderParent = item.orderId.ToString();
                        myTrade.NumberTrade = item.tradeId;

                        myTrade.Volume = item.sizeQty.ToDecimal();
                        myTrade.Price = item.price.ToDecimal();
                        myTrade.SecurityNameCode = item.symbol.ToUpper();

                        if (item.side == "open_long" ||
                            item.side == "close_short" ||
                            item.side == "buy_single" ||
                            item.side == "reduce_sell_single")
                        {
                            myTrade.Side = Side.Buy;
                        }
                        else if (item.side == "open_short" ||
                            item.side == "close_long" ||
                            item.side == "sell_single" ||
                            item.side == "reduce_buy_single")
                        {
                            myTrade.Side = Side.Sell;
                        }
                       
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

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            _rateGateCancelOrder.WaitToProceed();

            string jsonRequest = JsonConvert.SerializeObject(new
            {
                symbol = security.Name,
                marginCoin = "USDT",
            });

            CreatePrivateQueryOrders("/api/mix/v1/order/cancel-symbol-orders", Method.POST.ToString(), null, jsonRequest);
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

        public List<Order> GetAllOpenOrders()
        {
            //GET /api/mix/v1/order/marginCoinCurrent

            IRestResponse responseMessage = CreatePrivateQuery("/api/mix/v1/order/marginCoinCurrent?productType=umcbl", Method.GET, null, null);
            string json = responseMessage.Content;

            ResponseRestMessage<List< RestMessageOrders >> stateResponse = JsonConvert.DeserializeAnonymousType(json, new ResponseRestMessage<List<RestMessageOrders>>());

            if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
            {
                if (stateResponse.code.Equals("00000") == true)
                {
                    List<Order> orders = new List<Order>();

                    for(int i = 0;i < stateResponse.data.Count;i++)
                    {
                        Order curOder = ConvertRestToOrder(stateResponse.data[i]);
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

            return null;
        }

        private Order ConvertRestToOrder(RestMessageOrders item)
        {
            Order newOrder = new Order();

            OrderStateType stateType = GetOrderState(item.state);

            newOrder.SecurityNameCode = item.symbol; //.Replace("_SPBL", "")
            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.cTime));

            if (string.IsNullOrEmpty(item.clientOid) == false)
            {
                try
                {
                    newOrder.NumberUser = Convert.ToInt32(item.clientOid);
                }
                catch
                {
                    // ignore
                }
            }

            newOrder.NumberMarket = item.orderId.ToString();

            if(item.side == "open_long" ||
                item.side == "close_short" ||
                item.side == "buy_single" ||
                item.side == "reduce_sell_single")
            {
                newOrder.Side = Side.Buy;
            }
            else if (item.side == "open_short" ||
                item.side == "close_long" ||
                item.side == "sell_single" ||
                item.side == "reduce_buy_single")
            {
                newOrder.Side = Side.Sell;
            }

            newOrder.State = stateType;
            newOrder.Volume = item.size.ToDecimal();
            newOrder.Price = item.price.ToDecimal();
            newOrder.ServerType = ServerType.BitGetFutures;
            newOrder.PortfolioNumber = "BitGetFutures";
            newOrder.TypeOrder = OrderPriceType.Limit;
            
            return newOrder;
        }

        private OrderStateType GetOrderState(string orderStateResponse)
        {
            OrderStateType stateType;

            switch (orderStateResponse)
            {
                case ("init"):
                case ("new"):
                    stateType = OrderStateType.Activ;
                    break;
                case ("partial-fill"):
                    stateType = OrderStateType.Patrial;
                    break;
                case ("full-fill"):
                    stateType = OrderStateType.Done;
                    break;
                case ("filled"):
                    stateType = OrderStateType.Done;
                    break;
                case ("cancelled"):
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

        private HttpResponseMessage CreatePrivateQueryOrders(string path, string method, string queryString, string body)
        {
            string requestPath = path;
            string url = $"{BaseUrl}{requestPath}";
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string signature = GenerateSignature(timestamp, method, requestPath, queryString, body, SeckretKey);

            HttpClient httpClient = new HttpClient();

            httpClient.DefaultRequestHeaders.Add("ACCESS-KEY", PublicKey);
            httpClient.DefaultRequestHeaders.Add("ACCESS-SIGN", signature);
            httpClient.DefaultRequestHeaders.Add("ACCESS-TIMESTAMP", timestamp);
            httpClient.DefaultRequestHeaders.Add("ACCESS-PASSPHRASE", Passphrase);
            httpClient.DefaultRequestHeaders.Add("X-CHANNEL-API-CODE", "6yq7w");

            if (method.Equals("POST"))
            {
                return httpClient.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json")).Result;
            }
            else
            {
                return httpClient.GetAsync(url).Result;
            }
        }

        private string GenerateSignature(string timestamp, string method, string requestPath, string queryString, string body, string secretKey)
        {
            method = method.ToUpper();
            body = string.IsNullOrEmpty(body) ? string.Empty : body;
            queryString = string.IsNullOrEmpty(queryString) ? string.Empty : "?" + queryString;

            string preHash = timestamp + method + requestPath + queryString + body;

            byte[] secretKeyBytes = Encoding.UTF8.GetBytes(secretKey);
            using (var hmac = new HMACSHA256(secretKeyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(preHash));
                return Convert.ToBase64String(hashBytes);
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