using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.BitGet.BitGetSpot.Entity;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using WebSocket4Net;

namespace OsEngine.Market.Servers.BitGet.BitGetSpot
{
    public class BitGetServerSpot : AServer
    {
        public BitGetServerSpot()
        {

            BitGetServerSpotRealization realization = new BitGetServerSpotRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamPassphrase, "");
        }

        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            return ((BitGetServerSpotRealization)ServerRealization).GetCandleHistory(nameSec, tf, false, 0, DateTime.Now);
        }
    }

    public class BitGetServerSpotRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public BitGetServerSpotRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        public DateTime ServerTime { get; set; }

        public void Connect()
        {
            IsDispose = false;
            PublicKey = ((ServerParameterString)ServerParameters[0]).Value;
            SeckretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
            Passphrase = ((ServerParameterPassword)ServerParameters[2]).Value;

            ServicePointManager.SecurityProtocol =
            SecurityProtocolType.Ssl3
            | SecurityProtocolType.Tls11
            | SecurityProtocolType.Tls;

            HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(BaseUrl + "/api/spot/v1/public/time").Result;
            string json = responseMessage.Content.ReadAsStringAsync().Result;

            if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
            {
                try
                {
                    TimeLastSendPing = DateTime.Now;
                    TimeToUprdatePortfolio = DateTime.Now;
                    FIFOListWebSocketMessage = new ConcurrentQueue<string>();
                    StartCheckAliveWebSocket();
                    StartMessageReader();
                    CreateWebSocketConnection();
                    StartUpdatePortfolio();
                }
                catch (Exception exeption)
                {
                    HandlerExeption(exeption);
                    SendLogMessage("Connection can be open. BitGet. Error request", LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            else
            {
                IsDispose = true;
                SendLogMessage("Connection can be open. BitGet. Error request", LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
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
                HandlerExeption(exeption);
            }

            IsDispose = true;
            FIFOListWebSocketMessage = new ConcurrentQueue<string>();

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private bool IsDispose;

        public ServerType ServerType
        {
            get { return ServerType.BitGetSpot; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string PublicKey;

        private string SeckretKey;

        private string Passphrase;

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            try
            {
                HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(BaseUrl + "/api/spot/v1/public/products").Result;
                string json = responseMessage.Content.ReadAsStringAsync().Result;

                ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<object>());

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    if (stateResponse.code.Equals("00000") == true)
                    {
                        UpdateSecurity(json);
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
                HandlerExeption(exception);
            }
        }

        private void UpdateSecurity(string json)
        {
            ResponseMessageRest<List<ResposeSymbol>> symbols = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<List<ResposeSymbol>>());

            List<Security> securities = new List<Security>();

            for (int i = 0; i < symbols.data.Count; i++)
            {
                var item = symbols.data[i];

                if (item.status.Equals("online"))
                {
                    Security newSecurity = new Security();

                    newSecurity.Exchange = ServerType.BitGetSpot.ToString();
                    newSecurity.DecimalsVolume = Convert.ToInt32(item.quantityScale);
                    newSecurity.Lot = GetPriceStep(Convert.ToInt32(item.quantityScale));
                    newSecurity.Name = item.symbolName;
                    newSecurity.NameFull = item.symbol;
                    newSecurity.NameClass = item.quoteCoin;
                    newSecurity.NameId = item.symbol;
                    newSecurity.SecurityType = SecurityType.CurrencyPair;
                    newSecurity.Decimals = Convert.ToInt32(item.priceScale);
                    newSecurity.PriceStep = GetPriceStep(Convert.ToInt32(item.priceScale));
                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                    newSecurity.State = SecurityStateType.Activ;
                    securities.Add(newSecurity);
                }
            }

            SecurityEvent(securities);
        }

        private decimal GetPriceStep(int ScalePrice)
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

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private DateTime TimeToUprdatePortfolio = DateTime.Now;

        public void GetPortfolios()
        {
            CreateQueryPortfolio(true);
        }

        private void StartUpdatePortfolio()
        {
            Thread thread = new Thread(UpdatingPortfolio);
            thread.IsBackground = true;
            thread.Name = "UpdatingPortfolio";
            thread.Start();
        }

        private void UpdatingPortfolio()
        {
            while (IsDispose == false)
            {
                Thread.Sleep(5000);

                if (TimeToUprdatePortfolio.AddSeconds(50) < DateTime.Now)
                {
                    CreateQueryPortfolio(false);
                    TimeToUprdatePortfolio = DateTime.Now;
                }
            }
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            int countNeedToLoad = GetCountCandlesFromSliceTime(startTime, endTime, timeFrameBuilder.TimeFrameTimeSpan);
            return GetCandleHistory(security.NameFull, timeFrameBuilder.TimeFrameTimeSpan, true, countNeedToLoad, endTime);
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

                rangeCandles = CreateQueryCandles(nameSec + "_SPBL", stringInterval, limit, TimeToRequest.AddSeconds(10));

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

        private int GetCountCandlesFromSliceTime(DateTime startTime, DateTime endTime, TimeSpan tf)
        {
            if (tf.Hours != 0)
            {
                var totalHour = tf.TotalHours;
                TimeSpan TimeSlice = endTime - startTime;

                return Convert.ToInt32(TimeSlice.TotalHours / totalHour);
            }
            else
            {
                var totalMinutes = tf.Minutes;
                TimeSpan TimeSlice = endTime - startTime;
                return Convert.ToInt32(TimeSlice.TotalMinutes / totalMinutes);
            }
        }

        private int GetCountCandlesToLoad()
        {
            var server = (AServer)ServerMaster.GetServers().Find(server => server.ServerType == ServerType.BitGetSpot);

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
                return $"{tf.Minutes}min";
            }
            else
            {
                return $"{tf.Hours}h";
            }
        }

        #endregion

        #region 6 WebSocket creation

        private WebSocket webSocket;

        private string WebSocketUrl = "wss://ws.bitget.com/spot/v1/stream";

        private string _socketLocker = "webSocketLockerBitGet";

        private void CreateWebSocketConnection()
        {
            webSocket = new WebSocket(WebSocketUrl);
            webSocket.EnableAutoSendPing = true;
            webSocket.AutoSendPingInterval = 10;

            webSocket.Opened += WebSocket_Opened;
            webSocket.Closed += WebSocket_Closed;
            webSocket.MessageReceived += WebSocket_MessageReceived;
            webSocket.Error += WebSocket_Error;

            webSocket.Open();

        }

        private void DeleteWebscoektConnection()
        {
            if (webSocket != null)
            {
                try
                {
                    webSocket.Close();
                }
                catch
                {
                    // ignore
                }
               
                webSocket.Opened -= WebSocket_Opened;
                webSocket.Closed -= WebSocket_Closed;
                webSocket.MessageReceived -= WebSocket_MessageReceived;
                webSocket.Error -= WebSocket_Error;
                webSocket = null;
            }
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocket_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            var error = (SuperSocket.ClientEngine.ErrorEventArgs)e;
            if (error.Exception != null)
            {
                HandlerExeption(error.Exception);
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

        private void WebSocket_Closed(object sender, EventArgs e)
        {
            if (IsDispose == false)
            {
                if(webSocket != null)
                {
                    webSocket.Opened -= WebSocket_Opened;
                }
                
                SendLogMessage("Connection Closed by BitGet. WebSocket Closed Event", LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void WebSocket_Opened(object sender, EventArgs e)
        {
            CreateAuthMessageWebSocekt();
            SendLogMessage("Connection Open", LogMessageType.System);
            ServerStatus = ServerConnectStatus.Connect;
            ConnectEvent();
        }

        #endregion

        #region 8 WebSocket check alive

        private DateTime TimeLastSendPing = DateTime.Now;

        private void StartCheckAliveWebSocket()
        {
            Thread thread = new Thread(CheckAliveWebSocket);
            thread.IsBackground = true;
            thread.Name = "CheckAliveWebSocket";
            thread.Start();
        }

        private void CheckAliveWebSocket()
        {
            while (IsDispose == false)
            {
                Thread.Sleep(3000);

                if (webSocket != null &&
                    (webSocket.State == WebSocketState.Open ||
                    webSocket.State == WebSocketState.Connecting)
                    )
                {
                    if (TimeLastSendPing.AddSeconds(50) < DateTime.Now)
                    {
                        lock (_socketLocker)
                        {
                            webSocket.Send("ping");
                            TimeLastSendPing = DateTime.Now;
                        }
                    }
                }
                else
                {
                    Dispose();
                }
            }
        }

        #endregion

        #region 9 Security subscrible

        private RateGate rateGateSubscrible = new RateGate(1, TimeSpan.FromMilliseconds(350));

        public void Subscrible(Security security)
        {
            try
            {
                rateGateSubscrible.WaitToProceed();
                CreateSubscribleSecurityMessageWebSocket(security);
            }
            catch (Exception exeption)
            {
                HandlerExeption(exeption);
            }
        }

        #endregion

        #region 10 WebSocket parsing the messages

        private void StartMessageReader()
        {
            Thread thread = new Thread(MessageReader);
            thread.IsBackground = true;
            thread.Name = "MessageReaderBitGet";
            thread.Start();
        }

        private ConcurrentQueue<string> FIFOListWebSocketMessage = new ConcurrentQueue<string>();

        private void MessageReader()
        {
            Thread.Sleep(1000);

            while (IsDispose == false)
            {
                try
                {
                    if (FIFOListWebSocketMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    FIFOListWebSocketMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Equals("pong"))
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
                            SendLogMessage(SubscribleState.code + "\n" +
                                SubscribleState.msg, LogMessageType.Error);

                            Dispose();
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
                            if (action.arg.channel.Equals("orders"))
                            {
                                UpdateOrder(message);
                                continue;
                            }
                        }
                    }
                }
                catch (Exception exeption)
                {
                    HandlerExeption(exeption);
                }
            }
        }

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

            if (responseTrade.data.Count == 0)
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
            trade.SecurityNameCode = responseTrade.arg.instId;
            trade.Price = responseTrade.data[0][1].ToDecimal();
            trade.Id = responseTrade.data[0][0];
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

            marketDepth.SecurityNameCode = responseDepth.arg.instId;

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

        private void UpdateMytrade(string json)
        {
            ResponseMessageRest<List<ResponseMyTrade>> responseMyTrades = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<List<ResponseMyTrade>>());


            for (int i = 0; i < responseMyTrades.data.Count; i++)
            {
                ResponseMyTrade responseT = responseMyTrades.data[i];

                MyTrade myTrade = new MyTrade();

                myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseT.cTime));
                myTrade.NumberOrderParent = responseT.orderId;
                myTrade.NumberTrade = responseT.fillId.ToString();
                myTrade.Price = responseT.fillPrice.ToDecimal();
                myTrade.SecurityNameCode = responseT.symbol.ToUpper().Replace("_SPBL", "");
                myTrade.Side = responseT.side.Equals("buy") ? Side.Buy : Side.Sell;


                if (string.IsNullOrEmpty(responseT.feeCcy) == false
                    && string.IsNullOrEmpty(responseT.fees) == false
                    && responseT.fees.ToDecimal() != 0)
                {// комиссия берёться в какой-то монете
                    string comissionSecName = responseT.feeCcy;

                    if (myTrade.SecurityNameCode.StartsWith("BGB")
                        || myTrade.SecurityNameCode.StartsWith(comissionSecName))
                    {
                        myTrade.Volume = responseT.fillQuantity.ToDecimal() + responseT.fees.ToDecimal();
                    }
                    else
                    {
                        myTrade.Volume = responseT.fillQuantity.ToDecimal();
                    }
                }
                else
                {// не известная монета комиссии. Берём весь объём
                    myTrade.Volume = responseT.fillQuantity.ToDecimal();
                }

                MyTradeEvent(myTrade);
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

                if (item.ordType.Equals("market") && stateType == OrderStateType.Activ)
                {
                    continue;
                }

                Order newOrder = new Order();
                newOrder.SecurityNameCode = item.instId.Replace("_SPBL", "");
                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.cTime));

                if (!item.clOrdId.Equals(String.Empty) == true)
                {
                    try
                    {
                        newOrder.NumberUser = Convert.ToInt32(item.clOrdId);
                    }
                    catch
                    {
                        SendLogMessage("strage order num: " + item.clOrdId, LogMessageType.Error);
                        return;
                    }
                }

                newOrder.NumberMarket = item.ordId.ToString();
                newOrder.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;
                newOrder.State = stateType;
                newOrder.Volume = item.sz.Replace('.', ',').ToDecimal();
                newOrder.Price = item.avgPx.Replace('.', ',').ToDecimal();
                if (item.px != null)
                {
                    newOrder.Price = item.px.Replace('.', ',').ToDecimal();
                }
                newOrder.ServerType = ServerType.BitGetSpot;
                newOrder.PortfolioNumber = "BitGetSpot";

                if (stateType == OrderStateType.Done ||
                    stateType == OrderStateType.Patrial)
                {
                    // как только приходит ордер исполненный или частично исполненный триггер на запрос моего трейда по имени бумаги
                    CreateQueryMyTrade(newOrder.SecurityNameCode + "_SPBL", newOrder.NumberMarket);
                }
                MyOrderEvent(newOrder);
            }
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
                case ("cancelled"):
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

        private RateGate rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        private RateGate rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        public void SendOrder(Order order)
        {
            rateGateSendOrder.WaitToProceed();
            string jsonRequest = JsonConvert.SerializeObject(new
            {
                symbol = order.SecurityNameCode + "_SPBL",
                side = order.Side.ToString().ToLower(),
                orderType = OrderPriceType.Limit.ToString().ToLower(),
                force = "normal",
                price = order.Price.ToString().Replace(",", "."),
                quantity = order.Volume.ToString().Replace(",", "."),
                clientOrderId = order.NumberUser
            });

            HttpResponseMessage responseMessage = CreatePrivateQuery("/api/spot/v1/trade/orders", "POST", null, jsonRequest);
            string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;
            ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(JsonResponse, new ResponseMessageRest<object>());

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

        /// <summary>
        /// Order price change
        /// </summary>
        /// <param name="order">An order that will have a new price</param>
        /// <param name="newPrice">New price</param>
        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        public void GetOrdersState(List<Order> orders)
        {

        }

        public void CancelAllOrders()
        {

        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            rateGateCancelOrder.WaitToProceed();

            string jsonRequest = JsonConvert.SerializeObject(new
            {
                symbol = security.Name + "_SPBL"
            });

            HttpResponseMessage responseMessage = CreatePrivateQuery("/api/spot/v1/trade/cancel-order-v2", "POST", null, jsonRequest);
            string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;
            ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(JsonResponse, new ResponseMessageRest<object>());

            if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
            {
                if (stateResponse.code.Equals("00000") == true)
                {
                    // ignore
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

        public void CancelOrder(Order order)
        {
            rateGateCancelOrder.WaitToProceed();

            string jsonRequest = JsonConvert.SerializeObject(new
            {
                symbol = order.SecurityNameCode + "_SPBL",
                orderId = order.NumberMarket
            });

            HttpResponseMessage responseMessage = CreatePrivateQuery("/api/spot/v1/trade/cancel-order", "POST", null, jsonRequest);
            string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;
            ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(JsonResponse, new ResponseMessageRest<object>());

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

        public void ResearchTradesToOrders(List<Order> orders)
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

        #region 12 Queries

        private string BaseUrl = "https://api.bitget.com";

        private RateGate rateGateGetMyTradeState = new RateGate(1, TimeSpan.FromMilliseconds(200));

        HttpClient _httpPublicClient = new HttpClient();

        private List<string> _subscribledSecutiries = new List<string>();

        private void CreateSubscribleSecurityMessageWebSocket(Security security)
        {
            if (IsDispose)
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
                webSocket.Send($"{{\"op\": \"subscribe\",\"args\": [{{\"instType\": \"sp\",\"channel\": \"trade\",\"instId\": \"{security.Name}\"}}]}}");
                webSocket.Send($"{{\"op\": \"subscribe\",\"args\": [{{ \"instType\": \"sp\",\"channel\": \"books15\",\"instId\": \"{security.Name}\"}}]}}");
                webSocket.Send($"{{\"op\": \"subscribe\",\"args\": [{{\"channel\": \"orders\",\"instType\": \"spbl\",\"instId\": \"{security.Name}_SPBL\"}}]}}");
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
            webSocket.Send(AuthJson);
        }

        private void CreateQueryPortfolio(bool IsUpdateValueBegin)
        {
            try
            {
                HttpResponseMessage responseMessage = CreatePrivateQuery("/api/spot/v1/account/assets", "GET", null, null);
                string json = responseMessage.Content.ReadAsStringAsync().Result;

                ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<object>());

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    if (stateResponse.code.Equals("00000") == true)
                    {
                        UpdatePorfolio(json, IsUpdateValueBegin);
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
                HandlerExeption(exception);
            }
        }

        private void UpdatePorfolio(string json, bool IsUpdateValueBegin)
        {
            ResponseMessageRest<List<ResponseAsset>> assets = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<List<ResponseAsset>>());

            Portfolio portfolio = new Portfolio();
            portfolio.Number = "BitGetSpot";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            foreach (var item in assets.data)
            {
                var pos = new PositionOnBoard()
                {
                    PortfolioName = "BitGetSpot",
                    SecurityNameCode = item.coinName,
                    ValueBlocked = item.frozen.ToDecimal(),
                    ValueCurrent = item.available.ToDecimal()
                };

                if (IsUpdateValueBegin)
                {
                    pos.ValueBegin = item.available.ToDecimal();
                }

                portfolio.SetNewPosition(pos);
            }

            PortfolioEvent(new List<Portfolio> { portfolio });
        }

        private void CreateQueryMyTrade(string nameSec, string OrdId)
        {
            rateGateGetMyTradeState.WaitToProceed();

            string json = JsonConvert.SerializeObject(new
            {
                symbol = nameSec,
                orderId = OrdId
            });

            HttpResponseMessage responseMessage = CreatePrivateQuery("/api/spot/v1/trade/fills", "POST", null, json);
            string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

            ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(JsonResponse, new ResponseMessageRest<object>());

            if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
            {
                if (stateResponse.code.Equals("00000") == true)
                {
                    UpdateMytrade(JsonResponse);
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

        private List<Candle> CreateQueryCandles(string nameSec, string stringInterval, int limit, DateTime timeEndToLoad)
        {
            HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(BaseUrl + $"/api/spot/v1/market/candles?symbol={nameSec}&period={stringInterval}&limit={limit}&before={TimeManager.GetTimeStampMilliSecondsToDateTime(timeEndToLoad)}").Result;
            string content = responseMessage.Content.ReadAsStringAsync().Result;


            if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
            {
                ResponseMessageRest<List<ResponseCandle>> symbols = JsonConvert.DeserializeAnonymousType(content, new ResponseMessageRest<List<ResponseCandle>>());

                if (symbols.code.Equals("00000") == true)
                {
                    List<Candle> candles = new List<Candle>();

                    foreach (var item in symbols.data)
                    {
                        candles.Add(new Candle()
                        {
                            Close = item.close.ToDecimal(),
                            High = item.high.ToDecimal(),
                            Low = item.low.ToDecimal(),
                            Open = item.open.ToDecimal(),
                            Volume = item.baseVol.ToDecimal(),
                            State = CandleState.Finished,
                            TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts))
                        });
                    }

                    return candles;
                }
                else
                {
                    SendLogMessage($"Code: {symbols.code}\n"
                        + $"Message: {symbols.msg}", LogMessageType.Error);
                    return null;
                }
            }
            else
            {
                SendLogMessage($"State Code: {responseMessage.StatusCode}", LogMessageType.Error);
                return null;
            }
        }

        private HttpResponseMessage CreatePrivateQuery(string path, string method, string queryString, string body)
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

        public event Action<string, LogMessageType> LogMessageEvent;

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);
        }

        private void HandlerExeption(Exception exception)
        {
            if (exception is AggregateException)
            {
                AggregateException httpError = (AggregateException)exception;

                foreach (var item in httpError.InnerExceptions)

                {
                    if (item is NullReferenceException == false)
                    {
                        SendLogMessage(item.InnerException.Message + $" {exception.StackTrace}", LogMessageType.Error);
                    }

                }
            }
            else
            {
                if (exception is NullReferenceException == false)
                {
                    SendLogMessage(exception.Message + $" {exception.StackTrace}", LogMessageType.Error);
                }
            }
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
