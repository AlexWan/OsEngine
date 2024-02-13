using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using WebSocket4Net;
using System.Threading;
using System.Security.Cryptography;
using OsEngine.Market.Servers.BingX.BingXFutures.Entity;
using SuperSocket.ClientEngine;
using System.Globalization;

namespace OsEngine.Market.Servers.BingX.BingXFutures
{
    public class BingXServerFutures : AServer
    {
        public BingXServerFutures()
        {
            BingXServerFuturesRealization realization = new BingXServerFuturesRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
            CreateParameterBoolean("HedgeMode", false);
        }
    }

    public class BingXServerFuturesRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public BingXServerFuturesRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread keepalive = new Thread(RequestListenKey);
            keepalive.CurrentCulture = new CultureInfo("ru-RU");
            keepalive.IsBackground = true;
            keepalive.Start();

            Thread messageReader = new Thread(MessageReader);
            messageReader.IsBackground = true;
            messageReader.Name = "MessageReaderBingXFutures";
            messageReader.Start();
        }

        public DateTime ServerTime { get; set; }

        public void Connect()
        {
            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
            _hedgeMode = ((ServerParameterBool)ServerParameters[2]).Value;

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
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
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
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
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
            get { return ServerType.BingXFutures; }
        }

        public ServerConnectStatus ServerStatus { get; set; }
        public event Action ConnectEvent;
        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties
        public List<IServerParameter> ServerParameters { get; set; }
        private RateGate _rateGate = new RateGate(450, TimeSpan.FromSeconds(60));

        public string _publicKey;
        public string _secretKey;
        private bool _hedgeMode;


        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            _rateGate.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest("/openApi/swap/v2/quote/contracts", Method.GET);

                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string parameters = $"timestamp={timeStamp}";
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseFuturesBingX<BingXFuturesSymbols> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseFuturesBingX<BingXFuturesSymbols>());
                    List<BingXFuturesSymbols> currencyPairs = new List<BingXFuturesSymbols>();

                    if (response.code == "0")
                    {
                        for (int i = 0; i < response.data.Count; i++)
                        {
                            currencyPairs.Add(response.data[i]);
                        }
                        UpdateSecurity(currencyPairs);
                    }
                    else
                    {
                        SendLogMessage($"Error Code: {response.code} | msg: {response.msg}", LogMessageType.Error);
                    }
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

        private void UpdateSecurity(List<BingXFuturesSymbols> currencyPairs)
        {
            List<Security> securities = new List<Security>();

            for (int i = 0; i < currencyPairs.Count; i++)
            {
                BingXFuturesSymbols current = currencyPairs[i];

                if (current.status == "1")
                {
                    Security security = new Security();

                    security.Lot = current.size.ToDecimal();
                    security.Name = current.symbol;
                    security.NameFull = current.symbol;
                    security.NameClass = current.currency;
                    security.NameId = current.contractId;
                    security.Exchange = nameof(ServerType.BingXFutures);
                    security.State = SecurityStateType.Activ;
                    security.PriceStep = OsEngine.Entity.Extensions.GetValueByDecimals(Convert.ToInt32(current.pricePrecision));
                    security.PriceStepCost = security.PriceStep;
                    security.SecurityType = SecurityType.CurrencyPair;
                    security.Decimals = Convert.ToInt32(current.pricePrecision);
                    security.DecimalsVolume = security.Decimals;

                    securities.Add(security);
                }
            }

            SecurityEvent(securities);
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        public event Action<List<Portfolio>> PortfolioEvent;

        public void GetPortfolios()
        {
            CreateQueryPortfolio();
            CreateQueryPositions();
        }

        private void CreateQueryPositions()
        {
            _rateGate.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest("/openApi/swap/v2/user/positions", Method.GET);

                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string parameters = $"timestamp={timeStamp}";
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                ResponseFuturesBingX<PositionData> response = JsonConvert.DeserializeObject<ResponseFuturesBingX<PositionData>>(json.Content);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    if (response.code == "0")
                    {
                        UpdatePositions(response.data);
                    }
                    else
                    {
                        SendLogMessage($"Http State Code: {response.code} - message: {response.msg}", LogMessageType.Error);
                    }
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

        private void CreateQueryPortfolio()
        {
            _rateGate.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest("/openApi/swap/v2/user/balance", Method.GET);

                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string parameters = $"timestamp={timeStamp}";
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                ResponseFuturesBingXMessage<Balance> response = JsonConvert.DeserializeObject<ResponseFuturesBingXMessage<Balance>>(json.Content);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    if (response.code == "0")
                    {
                        UpdatePortfolio(response.data.balance);
                    }
                    else
                    {
                        SendLogMessage($"Http State Code: {response.code} - message: {response.msg}", LogMessageType.Error);
                    }
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

        private void UpdatePositions(List<PositionData> positionData)
        {
            try
            {
                if (positionData == null || positionData.Count == 0)
                {
                    return;
                }

                Portfolio portfolio = new Portfolio();

                portfolio.Number = "BingXFutures";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;

                for (int i = 0; i < positionData.Count; i++)
                {
                    PositionOnBoard position = new PositionOnBoard();

                    position.PortfolioName = "BingXFutures";

                    if (positionData[i].OnlyOnePosition == "true")
                    {
                        position.SecurityNameCode = positionData[i].Symbol + "_BOTH";

                        if (positionData[i].PositionSide == "LONG")
                        {
                            position.ValueCurrent = positionData[i].PositionAmt.ToDecimal();
                            position.ValueBegin = positionData[i].PositionAmt.ToDecimal();
                        }
                        else if (positionData[i].PositionSide == "SHORT")
                        {
                            position.ValueCurrent = -(positionData[i].PositionAmt.ToDecimal());
                            position.ValueBegin = -(positionData[i].PositionAmt.ToDecimal());
                        }

                        portfolio.SetNewPosition(position);
                        continue;
                    }
                    else
                    {
                        if (positionData[i].PositionSide == "LONG")
                        {
                            position.SecurityNameCode = positionData[i].Symbol + "_LONG";
                            position.ValueCurrent = positionData[i].PositionAmt.ToDecimal();
                            position.ValueBegin = positionData[i].PositionAmt.ToDecimal();
                            portfolio.SetNewPosition(position);
                            continue;
                        }
                        else if (positionData[i].PositionSide == "SHORT")
                        {
                            position.SecurityNameCode = positionData[i].Symbol + "_SHORT";
                            position.ValueCurrent = -(positionData[i].PositionAmt.ToDecimal());
                            position.ValueBegin = -(positionData[i].PositionAmt.ToDecimal());
                            portfolio.SetNewPosition(position);
                            continue;
                        }
                    }
                }

                PortfolioEvent(new List<Portfolio> { portfolio });
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdatePortfolio(BalanceInfoBingXFutures asset)
        {
            try
            {
                Portfolio portfolio = new Portfolio();

                portfolio.Number = "BingXFutures";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;

                if (asset == null)
                {
                    return;
                }

                PositionOnBoard newPortf = new PositionOnBoard();
                newPortf.SecurityNameCode = asset.asset;
                newPortf.ValueBegin = asset.balance.ToDecimal();
                newPortf.ValueCurrent = asset.equity.ToDecimal();
                newPortf.ValueBlocked = asset.freezedMargin.ToDecimal() + asset.usedMargin.ToDecimal(); // замороженные + маржа
                newPortf.PortfolioName = "BingXFutures";
                portfolio.SetNewPosition(newPortf);

                PortfolioEvent(new List<Portfolio> { portfolio });

            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        #endregion

        #region 5 Data

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
                string endPoint = "/openApi/swap/v3/quote/klines";
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
                string requestUri = $"{_baseUrl}{endPoint}?{parameters}&signature{sign}";

                HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(requestUri).Result;

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    string json = responseMessage.Content.ReadAsStringAsync().Result;

                    try
                    {
                        ResponseFuturesBingX<CandlestickChartDataFutures> response =
                            JsonConvert.DeserializeAnonymousType(json, new ResponseFuturesBingX<CandlestickChartDataFutures>());

                        if (response.code == "0" && response.data.Count != 1) // если дата старта свечек неправильная, биржа вместо ошибки шлет одну последнюю свечку
                        {
                            return ConvertCandles(response.data);
                        }
                        else
                        {
                            SendLogMessage($"Error: code {response.code}, or incorrect candle start time", LogMessageType.Error);
                        }
                    }
                    catch
                    {
                        JsonErrorResponse responseError = JsonConvert.DeserializeAnonymousType(json, new JsonErrorResponse());
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

        private List<Candle> ConvertCandles(List<CandlestickChartDataFutures> rawList)
        {
            try
            {
                List<Candle> candles = new List<Candle>();

                for (int i = 0; i < rawList.Count; i++)
                {
                    CandlestickChartDataFutures current = rawList[i];

                    Candle candle = new Candle();

                    candle.State = CandleState.Finished;
                    candle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(current.time));
                    candle.Volume = current.volume.ToDecimal();
                    candle.Close = current.close.ToDecimal();
                    candle.High = current.high.ToDecimal();
                    candle.Low = current.low.ToDecimal();
                    candle.Open = current.open.ToDecimal();

                    // Проверяем, что список не пуст и текущая свеча не дублирует последнюю
                    if (candles.Count > 0 && candle.TimeStart == candles[candles.Count - 1].TimeStart)
                    {
                        continue;
                    }

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

            DateTime startTimeData = startTime;
            DateTime partEndTime = startTimeData.AddMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * 1200);

            do
            {
                List<Candle> candles = new List<Candle>();

                long from = TimeManager.GetTimeStampMilliSecondsToDateTime(startTimeData);
                long to = TimeManager.GetTimeStampMilliSecondsToDateTime(partEndTime);

                candles = RequestCandleHistory(security.Name, interval, 1200, from, to); // максимум 1440 свечек

                if (candles == null || candles.Count == 0)
                {
                    break;
                }

                Candle last = candles[candles.Count - 1]; // последний элемент
                Candle first = candles[0]; // первый элемент

                // Проверяем, что список не пуст и текущая свеча не дублирует последнюю
                if (allCandles.Count > 0 && first.TimeStart == allCandles[allCandles.Count - 1].TimeStart)
                {
                    candles.RemoveAt(0);
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

                startTimeData = partEndTime;
                partEndTime = startTimeData.AddMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * 1200);

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

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        #endregion

        #region 6 WebSocket creation

        private WebSocket _webSocket;
        private const string _webSocketUrl = "wss://open-api-swap.bingx.com/swap-market";
        private string _listenKey = "";
        private void CreateWebSocketConnect()
        {
            _listenKey = CreateListenKey();
            string urlStr = $"{_webSocketUrl}?listenKey={_listenKey}";

            _webSocket = new WebSocket(urlStr);

            _webSocket.Opened += WebSocket_Opened;
            _webSocket.Closed += WebSocket_Closed;
            _webSocket.DataReceived += WebSocket_DataReceived;
            _webSocket.Error += WebSocket_Error;

            _webSocket.Open();
        }

        private void DeleteWebscoektConnection()
        {
            if (_webSocket != null)
            {
                try
                {
                    _webSocket.Close();
                }
                catch
                {
                    // ignore
                }

                _webSocket.Opened -= WebSocket_Opened;
                _webSocket.Closed -= WebSocket_Closed;
                _webSocket.DataReceived -= WebSocket_DataReceived;
                _webSocket.Error -= WebSocket_Error;
                _webSocket = null;
            }
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocket_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Connection Open", LogMessageType.Connect);

            if (ConnectEvent != null && ServerStatus != ServerConnectStatus.Connect)
            {
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent();
            }
        }

        private void WebSocket_Error(object sender, ErrorEventArgs e)
        {
            if (e.Exception != null)
            {
                SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
            }
            else
            {
                SendLogMessage("Socket error" + e.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocket_Closed(object sender, EventArgs e)
        {
            if (DisconnectEvent != null
                && ServerStatus != ServerConnectStatus.Disconnect)
            {
                SendLogMessage("Connection Closed by BingXFutures. WebSocket Closed Event", LogMessageType.Connect);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void WebSocket_DataReceived(object sender, DataReceivedEventArgs e)
        {
            try
            {
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
                if (e.Data is byte[])
                {
                    string item = Decompress(e.Data);

                    if (item.Contains("Ping")) // отправлять сразу после получения. 
                    {
                        _webSocket.Send("Pong");
                        return;
                    }
                    _fifoListWebSocketMessage.Enqueue(item);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                SendLogMessage($"Ошибка обработки ответа. Ошибка: {exception}", LogMessageType.Error);
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
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void CreateSubscribleSecurityMessageWebSocket(Security security)
        {
            for (int i = 0; i < _subscribledSecutiries.Count; i++)
            {
                if (_subscribledSecutiries[i].Equals(security.Name))
                {
                    return;
                }
            }

            _subscribledSecutiries.Add(security.Name);

            _webSocket.Send($"{{\"id\": \"{GenerateNewId()}\", \"reqType\": \"sub\", \"dataType\": \"{security.Name}@trade\"}}"); // трейды
            _webSocket.Send($"{{ \"id\":\"{GenerateNewId()}\", \"reqType\": \"sub\", \"dataType\": \"{security.Name}@depth20@500ms\" }}"); // глубина
        }

        #endregion

        #region 9 WebSocket parsing the messages

        private ConcurrentQueue<string> _fifoListWebSocketMessage = new ConcurrentQueue<string>();

        public event Action<Order> MyOrderEvent;
        public event Action<MyTrade> MyTradeEvent;
        public event Action<Trade> NewTradesEvent;
        public event Action<MarketDepth> MarketDepthEvent;

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
                        if (message.Contains("ORDER_TRADE_UPDATE"))
                        {
                            UpdateOrder(message);
                            continue;
                        }
                        else if (message.Contains("ACCOUNT_UPDATE"))
                        {
                            UpdatePortfolio(message);
                            UpdatePosition(message);
                            continue;
                        }
                        else if (message.Contains("@trade"))
                        {
                            UpdateTrade(message);
                            continue;
                        }
                        else if (message.Contains("@depth20"))
                        {
                            UpdateDepth(message);
                            continue;
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }

        private void UpdateTrade(string message)
        {
            SubscribeLatestTradeDetail<TradeDetails> response = JsonConvert.DeserializeObject<SubscribeLatestTradeDetail<TradeDetails>>(message);

            Trade trade = new Trade();

            for (int i = 0; i < response.data.Count; i++)
            {
                trade.SecurityNameCode = response.data[i].s;

                trade.Price = response.data[i].p.Replace('.', ',').ToDecimal();
                // trade.Id = // биржа не шлет id трейдов
                trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(response.data[i].T));
                trade.Volume = response.data[i].q.Replace('.', ',').ToDecimal();
                if (response.data[i].m == "true")
                    trade.Side = Side.Sell;
                else trade.Side = Side.Buy;

                NewTradesEvent(trade);
            }
        }

        private void UpdatePortfolio(string message)
        {
            try
            {
                AccountUpdateEvent accountUpdate = JsonConvert.DeserializeObject<AccountUpdateEvent>(message);

                Portfolio portfolio = new Portfolio();

                portfolio.Number = "BingXFutures";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;

                for (int i = 0; i < accountUpdate.a.B.Count; i++)
                {
                    PositionOnBoard pos = new PositionOnBoard();

                    pos.PortfolioName = "BingXFutures";
                    pos.SecurityNameCode = accountUpdate.a.B[i].a;
                    pos.ValueCurrent = accountUpdate.a.B[i].wb.ToDecimal();
                    pos.ValueBlocked = accountUpdate.a.B[i].wb.ToDecimal() - accountUpdate.a.B[i].cw.ToDecimal();

                    portfolio.SetNewPosition(pos);

                    PortfolioEvent(new List<Portfolio> { portfolio });
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdatePosition(string message)
        {
            try
            {
                AccountUpdateEvent accountUpdate = JsonConvert.DeserializeObject<AccountUpdateEvent>(message);

                Portfolio portfolio = new Portfolio();

                portfolio.Number = "BingXFutures";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;

                PositionOnBoard position = new PositionOnBoard();
                for (int i = 0; i < accountUpdate.a.P.Count; i++)
                {
                    if (!_hedgeMode)
                    {
                        position.SecurityNameCode = accountUpdate.a.P[i].s + "_BOTH";
                        position.PortfolioName = "BingXFutures";

                        if (accountUpdate.a.P[i].ps.Equals("LONG"))
                        {
                            position.ValueCurrent = accountUpdate.a.P[i].pa.ToDecimal();
                        }
                        else if (accountUpdate.a.P[i].ps.Equals("SHORT"))
                        {
                            position.ValueCurrent = -(accountUpdate.a.P[i].pa.ToDecimal());
                        }

                        portfolio.SetNewPosition(position);

                        PortfolioEvent(new List<Portfolio> { portfolio });

                        continue;
                    }

                    if (accountUpdate.a.P[i].ps.Equals("LONG"))
                    {
                        position.ValueCurrent = accountUpdate.a.P[i].pa.ToDecimal();
                        position.PortfolioName = "BingXFutures";
                        position.SecurityNameCode = accountUpdate.a.P[i].s + "_LONG";

                        portfolio.SetNewPosition(position);

                    }
                    else if (accountUpdate.a.P[i].ps.Equals("SHORT"))
                    {
                        position.ValueCurrent = -(accountUpdate.a.P[i].pa.ToDecimal());
                        position.PortfolioName = "BingXFutures";
                        position.SecurityNameCode = accountUpdate.a.P[i].s + "_SHORT";

                        portfolio.SetNewPosition(position);
                    }

                    PortfolioEvent(new List<Portfolio> { portfolio });
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateOrder(string message)
        {
            try
            {
                TradeUpdateEvent responceOrder = JsonConvert.DeserializeObject<TradeUpdateEvent>(message);

                Order newOrder = new Order();

                OrderStateType orderState = OrderStateType.None;

                switch (responceOrder.o.X)
                {
                    case "FILLED":
                        orderState = OrderStateType.Done;
                        break;
                    case "PARTIALLY_FILLED":
                        orderState = OrderStateType.Patrial;
                        break;
                    case "CANCELED":
                        orderState = OrderStateType.Cancel;
                        break;
                    case "NEW":
                        orderState = OrderStateType.Activ;
                        break;
                    case "EXPIRED":
                        orderState = OrderStateType.Fail;
                        break;
                    default:
                        orderState = OrderStateType.None;
                        break;
                }

                newOrder.NumberUser = Convert.ToInt32(responceOrder.o.c);
                newOrder.NumberMarket = responceOrder.o.i.ToString();
                newOrder.SecurityNameCode = responceOrder.o.s;
                newOrder.SecurityClassCode = responceOrder.o.N;
                newOrder.PortfolioNumber = "BingXFutures";
                newOrder.Side = responceOrder.o.S.Equals("BUY") ? Side.Buy : Side.Sell;
                newOrder.Price = responceOrder.o.p.Replace('.', ',').ToDecimal();
                newOrder.Volume = responceOrder.o.q.Replace('.', ',').ToDecimal();
                newOrder.State = orderState;
                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responceOrder.E));
                newOrder.TypeOrder = responceOrder.o.o.Equals("MARKET") ? OrderPriceType.Market : OrderPriceType.Limit;
                newOrder.ServerType = ServerType.BingXFutures;


                MyOrderEvent(newOrder);

                //если ордер исполнен, вызываем MyTradeEvent
                if (orderState == OrderStateType.Done || orderState == OrderStateType.Patrial)
                {
                    UpdateMyTrade(message);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateMyTrade(string message)
        {
            _rateGate.WaitToProceed();

            try
            {
                TradeUpdateEvent responseOrder = JsonConvert.DeserializeObject<TradeUpdateEvent>(message);

                MyTrade newTrade = new MyTrade();

                newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseOrder.o.T));
                newTrade.SecurityNameCode = responseOrder.o.s;
                newTrade.NumberOrderParent = responseOrder.o.i;
                newTrade.Price = responseOrder.o.ap.ToDecimal();
                newTrade.NumberTrade = responseOrder.o.i; // сервер не возвращает id транзакции, только id ордера
                newTrade.Side = responseOrder.o.S.Contains("BUY") ? Side.Buy : Side.Sell;
                newTrade.Volume = responseOrder.o.q.ToDecimal();

                MyTradeEvent(newTrade);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateDepth(string message)
        {
            try
            {
                ResponseWSBingXFuturesMessage<MarketDepthDataMessage> responceDepths =
                    JsonConvert.DeserializeAnonymousType(message, new ResponseWSBingXFuturesMessage<MarketDepthDataMessage>());

                MarketDepth depth = new MarketDepth();

                List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                depth.SecurityNameCode = responceDepths.dataType.Split('@')[0]; // из BTC-USDT@depth20@500ms получим BTC-USDT

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

                depth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responceDepths.ts));

                MarketDepthEvent(depth);
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        #endregion

        #region 10 Trade

        public void SendOrder(Order order)
        {
            _rateGate.WaitToProceed();

            RestClient client = new RestClient(_baseUrl);
            RestRequest request = new RestRequest("/openApi/swap/v2/trade/order", Method.POST);

            string symbol = order.SecurityNameCode;
            string side = order.Side == Side.Buy ? "BUY" : "SELL";

            string positionSide = CheckPositionSide(order); // проверим сторону сделки | LONG/SHORT

            string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string quantity = order.Volume.ToString().Replace(",", ".");
            string typeOrder = "";
            string parameters = "";
            string price = "";

            if (order.TypeOrder == OrderPriceType.Market)
            {
                typeOrder = "MARKET";
                parameters = $"timestamp={timeStamp}&symbol={symbol}&side={side}&positionSide={positionSide}" +
                    $"&type={typeOrder}&quantity={quantity}&clientOrderID={order.NumberUser}";
            }
            else if (order.TypeOrder == OrderPriceType.Limit)
            {
                typeOrder = "LIMIT";
                price = order.Price.ToString().Replace(",", ".");
                parameters = $"timestamp={timeStamp}&symbol={symbol}&side={side}&positionSide={positionSide}" +
                    $"&type={typeOrder}&quantity={quantity}&price={price}&clientOrderID={order.NumberUser}";
            }
            string sign = CalculateHmacSha256(parameters);

            request.AddParameter("timestamp", timeStamp);
            request.AddParameter("symbol", symbol);
            request.AddParameter("side", side);
            request.AddParameter("positionSide", positionSide);
            request.AddParameter("type", typeOrder);
            request.AddParameter("quantity", quantity);

            if (typeOrder == "LIMIT")
                request.AddParameter("price", price);

            request.AddParameter("clientOrderID", order.NumberUser);
            request.AddParameter("signature", sign);
            request.AddHeader("X-BX-APIKEY", _publicKey);

            IRestResponse json = client.Execute(request);

            if (json.StatusCode == HttpStatusCode.OK)
            {
                ResponseFuturesBingXMessage<OrderData> response = JsonConvert.DeserializeObject<ResponseFuturesBingXMessage<OrderData>>(json.Content);
                if (response.code == "0")
                {
                    order.State = OrderStateType.Activ;
                    order.NumberMarket = response.data.order.orderId;
                }
                else
                {
                    CreateOrderFail(order);
                    SendLogMessage($"Order execution error: code - {response.code} | message - {response.msg}", LogMessageType.Trade);
                }
            }
            else
            {
                CreateOrderFail(order);
                SendLogMessage($"Http State Code: {json.StatusCode} - {json.Content}", LogMessageType.Error);
            }

            MyOrderEvent.Invoke(order);
        }

        private string CheckPositionSide(Order order)
        {
            try
            {
                string positionSide = "";

                if (!_hedgeMode)
                {
                    positionSide = "BOTH";
                    return positionSide;
                }

                if (order.PositionConditionType == OrderPositionConditionType.Close)
                {
                    // Комбинации открытия/закрытия сделок
                    // open / buy LONG: side = BUY & positionSide = LONG
                    // close / sell LONG: side = SELL & positionSide = LONG
                    // open / sell SHORT: side = SELL & positionSide = SHORT
                    // close / buy SHORT: side = BUY & positionSide = SHORT

                    if (order.Side == Side.Sell)
                    {
                        positionSide = "LONG";
                    }
                    else if (order.Side == Side.Buy)
                    {
                        positionSide = "SHORT";
                    }
                }
                else if (order.PositionConditionType == OrderPositionConditionType.Open || order.PositionConditionType == OrderPositionConditionType.None)
                {
                    positionSide = order.Side == Side.Buy ? "LONG" : "SHORT";
                }

                return positionSide;
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        public void CancelAllOrders()
        {
            
        }

        public void CancelAllOrdersToSecurity(Security security)
        {

        }

        public void CancelOrder(Order order)
        {
            _rateGate.WaitToProceed();

            RestClient client = new RestClient(_baseUrl);
            RestRequest request = new RestRequest("/openApi/swap/v2/trade/order", Method.DELETE);

            string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string parameters = $"timestamp={timeStamp}&symbol={order.SecurityNameCode}&orderId={order.NumberMarket}&clientOrderID={order.NumberUser}";
            string sign = CalculateHmacSha256(parameters);

            request.AddParameter("timestamp", timeStamp);
            request.AddParameter("symbol", order.SecurityNameCode);
            request.AddParameter("orderId", order.NumberMarket);
            request.AddParameter("clientOrderID", order.NumberUser);
            request.AddParameter("signature", sign);
            request.AddHeader("X-BX-APIKEY", _publicKey);

            IRestResponse json = client.Execute(request);

            if (json.StatusCode == HttpStatusCode.OK)
            {
                ResponseFuturesBingXMessage<OrderData> response = JsonConvert.DeserializeObject<ResponseFuturesBingXMessage<OrderData>>(json.Content);
                if (response.code == "0")
                {

                }
                else
                {
                    SendLogMessage($"Order cancel error: code - {response.code} | message - {response.msg}", LogMessageType.Trade);
                }
            }
            else
            {
                SendLogMessage($"Http State Code: {json.StatusCode} - {json.Content}", LogMessageType.Error);
            }
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

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

        #region 11 Queries

        private const string _baseUrl = "https://open-api.bingx.com";
        private readonly HttpClient _httpPublicClient = new HttpClient();

        private string CreateListenKey()
        {
            _rateGate.WaitToProceed();

            try
            {
                string endpoint = "/openApi/user/auth/userDataStream";

                RestRequest request = new RestRequest(endpoint, Method.POST);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                string json = new RestClient(_baseUrl).Execute(request).Content;

                ListenKeyBingXFutures responseStr = JsonConvert.DeserializeObject<ListenKeyBingXFutures>(json);
                return responseStr.listenKey;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private void RequestListenKey()
        {
            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(5000);
                    continue;
                }

                try
                {
                    //спим 30 минут
                    Thread.Sleep(1800000);

                    if (_listenKey == "")
                    {
                        continue;
                    }

                    _rateGate.WaitToProceed();

                    string endpoint = "/openApi/user/auth/userDataStream";

                    HttpResponseMessage responseMessage = _httpPublicClient.GetAsync($"{_baseUrl}{endpoint}?listenKey={_listenKey}").Result;
                }
                catch
                {
                    SendLogMessage("Request Listen Key Error", LogMessageType.Error);
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
                using (System.IO.MemoryStream compressedStream = new System.IO.MemoryStream(data))
                {
                    using (GZipStream decompressor = new GZipStream(compressedStream, CompressionMode.Decompress))
                    {
                        using (System.IO.MemoryStream resultStream = new System.IO.MemoryStream())
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

        private string GenerateNewId()
        {
            return Guid.NewGuid().ToString();
        }

        #endregion
    }
}
