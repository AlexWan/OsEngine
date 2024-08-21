using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.BingX.BingXSpot.Entity;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using WebSocketSharp;

namespace OsEngine.Market.Servers.BinGxSpot
{
    public class BingXServerSpot : AServer
    {
        public BingXServerSpot()
        {
            BingXServerSpotRealization realization = new BingXServerSpotRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
        }
    }

    public class BingXServerSpotRealization : IServerRealization
    {

        #region 1 Constructor, Status, Connection

        public BingXServerSpotRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread keepalive = new Thread(RequestListenKey);
            keepalive.CurrentCulture = new CultureInfo("ru-RU");
            keepalive.IsBackground = true;
            keepalive.Start();

            Thread thread = new Thread(GetUpdatePortfolio);
            thread.IsBackground = true;
            thread.Start();

            Thread threadReader = new Thread(MessageReader);
            threadReader.IsBackground = true;
            threadReader.Name = "MessageReaderBingXSpot";
            threadReader.Start();
        }

        public DateTime ServerTime { get; set; }

        public void Connect()
        {
            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

            HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(_baseUrl + "/openApi/swap/v2/server/time").Result;
            string json = responseMessage.Content.ReadAsStringAsync().Result;

            if (responseMessage.StatusCode != HttpStatusCode.OK)
            {
                SendLogMessage($"The server is not available. No internet", LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
            else
            {
                try
                {
                    CreateWebSocketConnect();
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    SendLogMessage("The connection cannot be opened. BingX. Error Request", LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }

        }

        public void Dispose()
        {

            try
            {
                _subscribledSecutiries.Clear();
                DeleteWebscoektConnection();
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }

            _fifoListWebSocketMessage = new ConcurrentQueue<string>();

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }

        }

        public ServerType ServerType
        {
            get { return ServerType.BingXSpot; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private RateGate _rateGate = new RateGate(3, TimeSpan.FromMilliseconds(700));

        private string _publicKey;

        private string _secretKey;

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            _rateGate.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest("/openApi/spot/v1/common/symbols", Method.GET);

                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string parameters = $"timestamp={timeStamp}";
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                ResponseSpotBingX<SymbolArray> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseSpotBingX<SymbolArray>());

                List<SymbolData> currencyPairs = new List<SymbolData>();

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    for (int i = 0; i < response.data.symbols.Count; i++)
                    {
                        if (response.data.symbols[i].symbol.Contains("#")) // убираем NFT из списка доступных
                        {
                            continue;
                        }
                        currencyPairs.Add(response.data.symbols[i]);
                    }
                    UpdateSecurity(currencyPairs);
                }
                else
                {
                    SendLogMessage($"Http State Code: {json.StatusCode}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSecurity(List<SymbolData> currencyPairs)
        {
            List<Security> securities = new List<Security>();

            for (int i = 0; i < currencyPairs.Count; i++)
            {
                SymbolData current = currencyPairs[i];

                if (current.status == "1")
                {
                    Security security = new Security();

                    security.Lot = current.tickSize.ToDecimal();
                    security.Name = current.symbol;
                    security.NameFull = current.symbol;
                    security.NameClass = NameClass(current.symbol);
                    security.NameId = security.Name;
                    security.Exchange = nameof(ServerType.BingXSpot);
                    security.State = SecurityStateType.Activ;
                    security.PriceStep = current.tickSize.ToDecimal();
                    security.PriceStepCost = security.PriceStep;
                    security.SecurityType = SecurityType.CurrencyPair;

                    security.Decimals = current.tickSize.DecimalsCount();
                    security.DecimalsVolume = current.stepSize.DecimalsCount();

                    securities.Add(security);
                }
            }

            SecurityEvent(securities);
        }

        private string NameClass(string character)
        {
            string[] parts = character.Split('-');
            string nameClass = parts[1];
            return nameClass;
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        public event Action<List<Portfolio>> PortfolioEvent;

        public void GetPortfolios()
        {
            CreateQueryPortfolio();
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

                CreateQueryPortfolio();
            }
        }

        private void CreateQueryPortfolio()
        {
            _rateGate.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest("/openApi/spot/v1/account/balance", Method.GET);

                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string parameters = $"timestamp={timeStamp}";
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                ResponseSpotBingX<BalanceArray> responce = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseSpotBingX<BalanceArray>());

                List<BalanceData> assets = new List<BalanceData>();
                if (json.StatusCode == HttpStatusCode.OK)
                {
                    if (responce.code == "0")
                    {
                        for (int i = 0; i < responce.data.balances.Count; i++)
                        {
                            assets.Add(responce.data.balances[i]);
                        }
                        UpdatePortfolio(assets);
                    }
                    else
                    {
                        ResponseErrorCode responseError = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseErrorCode());
                        SendLogMessage($"Http State Code: {responseError.code} - message: {responseError.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Http State Code: {json.StatusCode}", LogMessageType.Error);

                    if (assets != null && responce != null)
                    {
                        SendLogMessage($"Code: {responce.code}\n"
                            + $"Message: {responce.msg}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdatePortfolio(List<BalanceData> assets)
        {
            try
            {
                Portfolio portfolio = new Portfolio();

                portfolio.Number = "BingXSpot";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;

                if (assets == null || assets.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < assets.Count; i++)
                {
                    PositionOnBoard newPortf = new PositionOnBoard();
                    newPortf.SecurityNameCode = assets[i].asset;
                    newPortf.ValueBegin = assets[i].free.ToDecimal();
                    newPortf.ValueCurrent = assets[i].free.ToDecimal();
                    newPortf.ValueBlocked = assets[i].locked.ToDecimal();
                    newPortf.PortfolioName = "BingXSpot";
                    portfolio.SetNewPosition(newPortf);
                }

                PortfolioEvent(new List<Portfolio> { portfolio });

            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        #endregion

        #region 5 Data

        #region Candles

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            string tf = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);
            return RequestCandleHistory(security.Name, tf);
        }

        private List<Candle> RequestCandleHistory(string nameSec, string tameFrame, long limit = 500, long fromTimeStamp = 0, long toTimeStamp = 0)
        {
            _rateGate.WaitToProceed();

            try
            {
                string endPoint = "/openApi/spot/v2/market/kline";
                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                string parameters = "";
                if (fromTimeStamp != 0 && toTimeStamp != 0)
                {
                    parameters = $"symbol={nameSec}&interval={tameFrame}&startTime={fromTimeStamp}&endTime={toTimeStamp}&limit={limit}&timestamp={timeStamp}";
                }
                else
                {
                    parameters = $"symbol={nameSec}&interval={tameFrame}&limit={limit}&timestamp={timeStamp}";
                }

                string sign = CalculateHmacSha256(parameters);
                string requestUri = $"{_baseUrl}{endPoint}?{parameters}&signature={sign}";

                HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(requestUri).Result;

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    string json = responseMessage.Content.ReadAsStringAsync().Result;
                    CandlestickChartData response = JsonConvert.DeserializeAnonymousType(json, new CandlestickChartData());

                    if (response.code == "0")
                    {
                        return ConvertCandles(response.data);
                    }
                    else
                    {
                        ResponseErrorCode responseError = JsonConvert.DeserializeAnonymousType(json, new ResponseErrorCode());
                        SendLogMessage($"Http State Code: {responseError.code} - message: {responseError.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    string json = responseMessage.Content.ReadAsStringAsync().Result;
                    SendLogMessage($"Http State Code: {responseMessage.StatusCode} - {json}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private List<Candle> ConvertCandles(List<string[]> rawList)
        {
            try
            {
                List<Candle> candles = new List<Candle>();

                for (int i = 0; i < rawList.Count; i++)
                {
                    string[] current = rawList[i];

                    Candle candle = new Candle();

                    candle.State = CandleState.Finished;
                    candle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(current[0]));
                    candle.Volume = current[7].ToDecimal();
                    candle.Close = current[4].ToDecimal();
                    candle.High = current[2].ToDecimal();
                    candle.Low = current[3].ToDecimal();
                    candle.Open = current[1].ToDecimal();

                    candles.Add(candle);
                }
                candles.Reverse();

                return candles;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private string GetInterval(TimeSpan timeFrame)
        {
            if (timeFrame.Minutes != 0)
            {
                return $"{timeFrame.Minutes}m";
            }
            else if (timeFrame.Hours != 0)
            {
                return $"{timeFrame.Hours}h";
            }
            else
            {
                return $"{timeFrame.Days}d";
            }
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime)
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

            List<Candle> allCandles = new List<Candle>();

            string interval = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);

            int timeRange = 0;
            if (interval == "1m")
            {
                timeRange = tfTotalMinutes * 10000; // для 1m ограничение 10000 свечек
            }
            else
            {
                timeRange = tfTotalMinutes * 20000; // для остальных тф 20000
            }

            DateTime maxStartTime = DateTime.Now.AddMinutes(-timeRange);
            DateTime startTimeData = startTime;

            if (maxStartTime > startTime)
            {
                SendLogMessage($"Превышено максимальное колличество свечек для ТФ {interval}", LogMessageType.Error);
                return null;
            }

            DateTime partEndTime = startTimeData.AddMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * 720);

            do
            {
                List<Candle> candles = new List<Candle>();

                long from = TimeManager.GetTimeStampMilliSecondsToDateTime(startTimeData);
                long to = TimeManager.GetTimeStampMilliSecondsToDateTime(partEndTime);

                candles = RequestCandleHistory(security.Name, interval, 720, from, to);

                if (candles == null || candles.Count == 0)
                {
                    break;
                }

                Candle last = candles.Last();

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

                startTimeData = partEndTime;
                partEndTime = startTimeData.AddMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * 720);

                if (startTimeData >= DateTime.Now)
                {
                    break;
                }

                if (partEndTime > DateTime.Now)
                {
                    partEndTime = DateTime.Now;
                }
            }
            while (true);

            return allCandles;
        }

        private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (endTime > DateTime.Now ||
                startTime >= endTime ||
                startTime >= DateTime.Now ||
                actualTime > endTime ||
                actualTime > DateTime.Now)
            {
                return false;
            }

            return true;
        }

        private bool CheckTf(int timeFrameMinutes)
        {
            if (timeFrameMinutes == 1 ||
                timeFrameMinutes == 5 ||
                timeFrameMinutes == 15 ||
                timeFrameMinutes == 30 ||
                timeFrameMinutes == 60 ||
                timeFrameMinutes == 120 ||
                timeFrameMinutes == 240)
            {
                return true;
            }

            return false;
        }

        #endregion

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }
        #endregion

        #region 6 WebSocket creation

        private WebSocket _webSocket;

        private const string _webSocketUrl = "wss://open-api-ws.bingx.com/market";

        private string _listenKey = "";

        private void CreateWebSocketConnect()
        {
            _listenKey = CreateListenKey();

            if (_listenKey == null)
            {
                SendLogMessage("Autorization error. Listen key is note created", LogMessageType.Error);
                return;
            }

            string urlStr = $"{_webSocketUrl}?listenKey={_listenKey}";

            _webSocket = new WebSocket(urlStr);

            _webSocket.EmitOnPing = true;
            _webSocket.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.None;
            _webSocket.OnOpen += WebSocket_Opened;
            _webSocket.OnMessage += WebSocket_DataReceived;
            _webSocket.OnError += WebSocket_Error;
            _webSocket.OnClose += WebSocket_Closed;

            _webSocket.Connect();
        }

        private void DeleteWebscoektConnection()
        {
            if (_webSocket != null)
            {
                _webSocket.OnOpen -= WebSocket_Opened;
                _webSocket.OnClose -= WebSocket_Closed;
                _webSocket.OnMessage -= WebSocket_DataReceived;
                _webSocket.OnError -= WebSocket_Error;

                try
                {
                    _webSocket.Close();
                }
                catch
                {
                    // ignore
                }

                _webSocket = null;
            }
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocket_Error(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            if (e.Exception != null)
            {
                SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
            }
            else
            {
                SendLogMessage("Socket error" + e.Message.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocket_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Connection Open", LogMessageType.Connect);

            if (ConnectEvent != null
                && ServerStatus != ServerConnectStatus.Connect)
            {
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent();
            }

            _webSocket.Send($"{{\"id\":\"{GenerateNewId()}\", \"reqType\": \"sub\", \"dataType\": \"spot.executionReport\"}}"); // изменение ордеров
        }

        private void WebSocket_Closed(object sender, EventArgs e)
        {
            if (DisconnectEvent != null
                && ServerStatus != ServerConnectStatus.Disconnect)
            {
                SendLogMessage("Connection Closed by BingX. WebSocket Closed Event", LogMessageType.Connect);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void WebSocket_DataReceived(object sender, MessageEventArgs e)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Connect)
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

                if (_fifoListWebSocketMessage == null)
                {
                    return;
                }

                if (e.IsBinary)
                {
                    string item = Decompress(e.RawData);

                    if (item.Contains("Ping")) // отправлять сразу после получения. 
                    {
                        _webSocket.Send("Pong");
                        return;
                    }

                    _fifoListWebSocketMessage.Enqueue(item);
                }

                if (e.IsText)
                {
                    _fifoListWebSocketMessage.Enqueue(e.Data);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                SendLogMessage($"Error message read. Error: {error}", LogMessageType.Error);
            }
        }

        #endregion

        #region 8 Security subscrible

        private List<string> _subscribledSecutiries = new List<string>();

        public void Subscrible(Security security)
        {
            try
            {
                CreateSubscribleSecurityMessageWebSocket(security);

                Thread.Sleep(200);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
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

            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            _webSocket.Send($"{{\"id\": \"{GenerateNewId()}\", \"reqType\": \"sub\", \"dataType\": \"{security.Name}@trade\"}}"); // трейды

            _webSocket.Send($"{{ \"id\":\"{GenerateNewId()}\", \"reqType\": \"sub\", \"dataType\": \"{security.Name}@depth20\" }}"); // глубина
        }

        #endregion

        #region 9 WebSocket parsing the messages

        private ConcurrentQueue<string> _fifoListWebSocketMessage = new ConcurrentQueue<string>();

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        private void MessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (_fifoListWebSocketMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_fifoListWebSocketMessage.TryDequeue(out string message))
                    {
                        if (message.Contains("spot.executionReport"))
                        {
                            UpdateOrder(message);
                            continue;
                        }
                        else if (message.Contains("@depth20"))
                        {
                            UpdateDepth(message);
                            continue;
                        }
                        else if (message.Contains("@trade"))
                        {
                            UpdateTrade(message);
                            continue;
                        }
                    }
                }
                catch (Exception exeption)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(exeption.ToString(), LogMessageType.Error);
                }
            }
        }

        private void UpdateOrder(string message)
        {
            try
            {
                ResponceWebSocketBingXMessage<SubscriptionOrderUpdateData> responceOrder =
                    JsonConvert.DeserializeAnonymousType(message, new ResponceWebSocketBingXMessage<SubscriptionOrderUpdateData>());

                Order newOrder = new Order();

                OrderStateType orderState = OrderStateType.None;

                switch (responceOrder.data.X)
                {
                    case "FILLED":
                        orderState = OrderStateType.Done;
                        break;
                    case "PENDING":
                        orderState = OrderStateType.Activ;
                        break;
                    case "PARTIALLY_FILLED":
                        orderState = OrderStateType.Patrial;
                        break;
                    case "CANCELED":
                        orderState = OrderStateType.Cancel;
                        break;
                    case "FAILED":
                        orderState = OrderStateType.Fail;
                        break;
                    case "NEW":
                        orderState = OrderStateType.Activ;
                        break;
                }

                newOrder.NumberUser = Convert.ToInt32(responceOrder.data.C);
                newOrder.NumberMarket = responceOrder.data.i.ToString();
                newOrder.SecurityNameCode = responceOrder.data.s;
                newOrder.SecurityClassCode = responceOrder.data.s.Split('-')[1];
                newOrder.PortfolioNumber = "BingXSpot";
                newOrder.Side = responceOrder.data.S.Equals("BUY") ? Side.Buy : Side.Sell;
                newOrder.Price = responceOrder.data.p.Replace('.', ',').ToDecimal();
                newOrder.Volume = responceOrder.data.q.Replace('.', ',').ToDecimal();
                newOrder.State = orderState;
                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responceOrder.data.E));
                newOrder.TypeOrder = responceOrder.data.o.Equals("MARKET") ? OrderPriceType.Market : OrderPriceType.Limit;
                newOrder.ServerType = ServerType.BingXSpot;

                MyOrderEvent(newOrder);

                //если ордер исполнен, вызываем MyTradeEvent
                if (orderState == OrderStateType.Done
                    || orderState == OrderStateType.Patrial)
                {
                    UpdateMyTrade(message);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateMyTrade(string message)
        {
            try
            {
                ResponceWebSocketBingXMessage<SubscriptionOrderUpdateData> responceOrder =
                    JsonConvert.DeserializeAnonymousType(message, new ResponceWebSocketBingXMessage<SubscriptionOrderUpdateData>());

                MyTrade newTrade = new MyTrade();

                newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responceOrder.data.T));
                newTrade.SecurityNameCode = responceOrder.data.s;
                newTrade.NumberOrderParent = responceOrder.data.i;
                newTrade.Price = responceOrder.data.p.ToDecimal();
                newTrade.NumberTrade = responceOrder.data.t;
                newTrade.Side = responceOrder.data.S.Equals("BUY") ? Side.Buy : Side.Sell;
                newTrade.Volume = responceOrder.data.q.ToDecimal();

                MyTradeEvent(newTrade);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateTrade(string message)
        {
            try
            {
                ResponceWebSocketBingXMessage<ResponseWebSocketTrade> responceTrades =
                    JsonConvert.DeserializeAnonymousType(message, new ResponceWebSocketBingXMessage<ResponseWebSocketTrade>());

                Trade trade = new Trade();
                trade.SecurityNameCode = responceTrades.data.s;

                trade.Price = responceTrades.data.p.Replace('.', ',').ToDecimal();
                trade.Id = responceTrades.data.t;
                trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responceTrades.data.T));
                trade.Volume = responceTrades.data.q.Replace('.', ',').ToDecimal();
                if (responceTrades.data.m == "true")
                    trade.Side = Side.Sell;
                else trade.Side = Side.Buy;

                NewTradesEvent(trade);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private readonly Dictionary<string, MarketDepth> _allDepths = new Dictionary<string, MarketDepth>();

        private void UpdateDepth(string message)
        {
            try
            {
                ResponceWebSocketBingXMessage<MarketDepthDataMessage> responceDepths =
                    JsonConvert.DeserializeAnonymousType(message, new ResponceWebSocketBingXMessage<MarketDepthDataMessage>());
                MarketDepth depth = new MarketDepth();

                List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                depth.SecurityNameCode = responceDepths.dataType.Split('@')[0];

                for (int i = 0; i < responceDepths.data.asks.Count; i++)
                {
                    MarketDepthLevel level = new MarketDepthLevel()
                    {
                        Price = responceDepths.data.asks[i][0].ToDecimal(),
                        Ask = responceDepths.data.asks[i][1].ToDecimal()
                    };

                    ascs.Insert(0, level);
                }

                for (int i = 0; i < responceDepths.data.bids.Count; i++)
                {
                    bids.Add(new MarketDepthLevel()
                    {
                        Price = responceDepths.data.bids[i][0].ToDecimal(),
                        Bid = responceDepths.data.bids[i][1].ToDecimal()
                    });
                }

                depth.Asks = ascs;
                depth.Bids = bids;

                depth.Time = DateTime.UtcNow;

                _allDepths[depth.SecurityNameCode] = depth;

                MarketDepthEvent(depth);
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        #endregion

        #region 10 Trade

        public void SendOrder(Order order)
        {
            _rateGate.WaitToProceed();

            RestClient client = new RestClient(_baseUrl);
            RestRequest request = new RestRequest("/openApi/spot/v1/trade/order", Method.POST);

            string secName = order.SecurityNameCode;
            string side = order.Side == Side.Buy ? "BUY" : "SELL";
            string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string quantity = order.Volume.ToString().Replace(",", ".");
            string typeOrder = "";
            string parameters = "";
            string price = "";

            if (order.TypeOrder == OrderPriceType.Market)
            {
                typeOrder = "MARKET";
                parameters = $"timestamp={timeStamp}&symbol={secName}&side={side}&type={typeOrder}&quantity={quantity}&newClientOrderId={order.NumberUser}";
            }
            else if (order.TypeOrder == OrderPriceType.Limit)
            {
                typeOrder = "LIMIT";
                price = order.Price.ToString().Replace(",", ".");
                parameters = $"timestamp={timeStamp}&symbol={secName}&side={side}&type={typeOrder}&quantity={quantity}&price={price}&newClientOrderId={order.NumberUser}";
            }
            string sign = CalculateHmacSha256(parameters);

            request.AddParameter("timestamp", timeStamp);
            request.AddParameter("symbol", secName);
            request.AddParameter("side", side);
            request.AddParameter("type", typeOrder);
            request.AddParameter("quantity", quantity);

            if (typeOrder == "LIMIT")
                request.AddParameter("price", price);

            request.AddParameter("newClientOrderId", order.NumberUser);
            request.AddParameter("signature", sign);
            request.AddHeader("X-BX-APIKEY", _publicKey);

            IRestResponse json = client.Execute(request);

            if (json.StatusCode == HttpStatusCode.OK)
            {
                ResponseSpotBingX<ResponseCreateOrder> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseSpotBingX<ResponseCreateOrder>());
                if (response.code == "0")
                {
                    order.State = OrderStateType.Activ;
                    order.NumberMarket = response.data.orderId;
                }
                else
                {
                    CreateOrderFail(order);
                    ResponseErrorCode responseError = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseErrorCode());

                    SendLogMessage($"Order execution error: code - {responseError.code} | message - {responseError.msg}", LogMessageType.Trade);
                }
            }
            else
            {
                CreateOrderFail(order);
                SendLogMessage($"Http State Code: {json.StatusCode} - {json.Content}", LogMessageType.Error);
            }

            MyOrderEvent.Invoke(order);
        }

        public void GetOrdersState(List<Order> orders)
        {

        }

        public void CancelAllOrders()
        {

        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            _rateGate.WaitToProceed();

            RestClient client = new RestClient(_baseUrl);
            RestRequest request = new RestRequest("/openApi/spot/v1/trade/cancelOpenOrders", Method.POST);

            string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string symbol = security.Name;
            string parameters = $"timestamp={timeStamp}&symbol={symbol}";
            string sign = CalculateHmacSha256(parameters);

            request.AddParameter("timestamp", timeStamp);
            request.AddParameter("symbol", symbol);
            request.AddParameter("signature", sign);
            request.AddHeader("X-BX-APIKEY", _publicKey);

            IRestResponse json = client.Execute(request);

            if (json.StatusCode == HttpStatusCode.OK)
            {
                ResponseSpotBingX<ResponseCreateOrder> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseSpotBingX<ResponseCreateOrder>());
                if (response.code == "0")
                {

                }
                else
                {
                    ResponseErrorCode responseError = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseErrorCode());
                    SendLogMessage($"Order cancel error: code - {responseError.code} | message - {responseError.msg}", LogMessageType.Trade);
                }
            }
            else
            {
                SendLogMessage($"Http State Code: {json.StatusCode} - {json.Content}", LogMessageType.Error);
            }
        }

        public void CancelOrder(Order order)
        {
            _rateGate.WaitToProceed();

            RestClient client = new RestClient(_baseUrl);
            RestRequest request = new RestRequest("/openApi/spot/v1/trade/cancel", Method.POST);

            string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string symbol = order.SecurityNameCode.ToString();
            string orderId = order.NumberMarket.ToString();
            string parameters = $"timestamp={timeStamp}&symbol={symbol}&orderId={orderId}";
            string sign = CalculateHmacSha256(parameters);

            request.AddParameter("timestamp", timeStamp);
            request.AddParameter("symbol", symbol);
            request.AddParameter("orderId", orderId);
            request.AddParameter("signature", sign);
            request.AddHeader("X-BX-APIKEY", _publicKey);

            IRestResponse json = client.Execute(request);

            if (json.StatusCode == HttpStatusCode.OK)
            {
                ResponseSpotBingX<ResponseCreateOrder> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseSpotBingX<ResponseCreateOrder>());
                if (response.code == "0")
                {

                }
                else
                {
                    CreateOrderFail(order);
                    ResponseErrorCode responseError = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseErrorCode());
                    SendLogMessage($"Order cancel error: code - {responseError.code} | message - {responseError.msg}", LogMessageType.Trade);
                }
            }
            else
            {
                CreateOrderFail(order);
                SendLogMessage($"Http State Code: {json.StatusCode} - {json.Content}", LogMessageType.Error);
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

        public void ResearchTradesToOrders(List<Order> orders)
        {

        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        public void GetAllActivOrders()
        {

        }

        public void GetOrderStatus(Order order)
        {

        }

        #endregion

        #region 11 Queries

        private const string _baseUrl = "https://open-api.bingx.com";

        private readonly HttpClient _httpPublicClient = new HttpClient();

        private string CreateListenKey()
        {
            _rateGate.WaitToProceed();

            try
            {
                string baseUrl = "https://open-api.bingx.com";
                string endpoint = "/openApi/user/auth/userDataStream";

                RestRequest request = new RestRequest(endpoint, Method.POST);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                string response = new RestClient(baseUrl).Execute(request).Content;

                string responseStr = JsonConvert.DeserializeAnonymousType(response, new ListenKeyBingX()).listenKey;

                _timeLastUpdateListenKey = DateTime.Now;

                return responseStr;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private DateTime _timeLastUpdateListenKey = DateTime.MinValue;

        private void RequestListenKey()
        {
            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(5000);
                    continue;
                }

                if (_timeLastUpdateListenKey.AddMinutes(30) > DateTime.Now)
                {   // спим 30 минут
                    Thread.Sleep(10000);
                    continue;
                }

                try
                {
                    if (_listenKey == "")
                    {
                        continue;
                    }

                    _rateGate.WaitToProceed();

                    string endpoint = "/openApi/user/auth/userDataStream";

                    RestClient client = new RestClient(_baseUrl);
                    RestRequest request = new RestRequest(endpoint, Method.PUT);

                    request.AddQueryParameter("listenKey", _listenKey);

                    IRestResponse response = client.Execute(request);

                    _timeLastUpdateListenKey = DateTime.Now;
                }
                catch (Exception exception)
                {
                    SendLogMessage($"Request Listen Key Error. Error: {exception}", LogMessageType.Error);
                }
            }
        }

        #endregion

        #region 12 Log

        public event Action<string, LogMessageType> LogMessageEvent;

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            if (LogMessageEvent != null)
                LogMessageEvent(message, messageType);
        }

        #endregion

        #region 13 Helpers

        private string Decompress(byte[] data)
        {
            try
            {
                using (var compressedStream = new MemoryStream(data))
                {
                    using (var decompressor = new GZipStream(compressedStream, CompressionMode.Decompress))
                    {
                        using (var resultStream = new MemoryStream())
                        {
                            decompressor.CopyTo(resultStream);

                            return Encoding.UTF8.GetString(resultStream.ToArray());
                        }
                    }
                }
            }
            catch
            {
                SendLogMessage("Decompress error", LogMessageType.Error);
                return null;
            }
        }

        private string GenerateNewId()
        {
            return Guid.NewGuid().ToString();
        }

        private string CalculateHmacSha256(string parametrs)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(_secretKey);
            byte[] inputBytes = Encoding.UTF8.GetBytes(parametrs);
            using (HMACSHA256 hmac = new HMACSHA256(keyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(inputBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        #endregion
    }
}