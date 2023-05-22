using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.BitGet.BitGetSpot.Entity;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        public ServerType ServerType
        {
            get { return ServerType.BitGetSpot; }
        }
        public ServerConnectStatus ServerStatus { get; set; }
        public List<IServerParameter> ServerParameters { get; set; }
        public DateTime ServerTime { get; set; }
        public BitGetServerSpotRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
        }
        public void Connect()
        {
            IsDispose = false;
            PublicKey = ((ServerParameterString)ServerParameters[0]).Value;
            SeckretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
            Passphrase = ((ServerParameterPassword)ServerParameters[2]).Value;

            HttpClient httpClient = new HttpClient();
            HttpResponseMessage responseMessage = httpClient.GetAsync(BaseUrl + "/api/spot/v1/public/time").Result;
            string json = responseMessage.Content.ReadAsStringAsync().Result;

            if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
            {
                try
                {
                    TimeToSendPing = DateTime.Now;
                    TimeToUprdatePortfolio = DateTime.Now;
                    FIFOListWebSocketMessage = new ConcurrentQueue<string>();
                    StartCheckAliveWebSocket();
                    StartMessageReader();
                    CreateWebSocketConnection();
                    StartUpdatePortfolio();
                    ServerStatus = ServerConnectStatus.Connect;
                    ConnectEvent();
                }
                catch (Exception exeption)
                {
                    HandlerExeption(exeption);
                    IsDispose = true;
                }
            }
            else
            {
                IsDispose = true;
            }
        }
        public void Dispose()
        {
            try
            {
                DeleteWebscoektConnection();
            }
            catch (Exception exeption)
            {
                HandlerExeption(exeption);
            }
            finally
            {
                IsDispose = true;
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
                FIFOListWebSocketMessage = null;
            }
        }
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

        #region Properties

        private string BaseUrl = "https://api.bitget.com";
        private string WebSocketUrl = "wss://ws.bitget.com/spot/v1/stream";
        private string PublicKey;
        private string SeckretKey;
        private string Passphrase;
        private bool IsDispose;
        private WebSocket webSocket;
        private ConcurrentQueue<string> FIFOListWebSocketMessage;
        private RateGate rateGateSubscrible = new RateGate(1, TimeSpan.FromMilliseconds(500));
        private RateGate rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(200));
        private RateGate rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(200));
        private DateTime TimeToSendPing = DateTime.Now;
        private DateTime TimeToUprdatePortfolio = DateTime.Now;

        #endregion

        #region WebSocketConnection

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
                webSocket.Close();

                webSocket.Opened += WebSocket_Opened;
                webSocket.Closed += WebSocket_Closed;
                webSocket.MessageReceived += WebSocket_MessageReceived;
                webSocket.Error += WebSocket_Error;
                webSocket = null;
            }
        }

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
            if (FIFOListWebSocketMessage != null)
            {
                FIFOListWebSocketMessage.Enqueue(e.Message);
            }
        }

        private void WebSocket_Closed(object sender, EventArgs e)
        {
            if (IsDispose == false)
            {
                IsDispose = true;
                SendLogMessage("Connection Close", LogMessageType.System);
                Dispose();
            }
        }

        private void WebSocket_Opened(object sender, EventArgs e)
        {
            CreateAuthMessageWebSocekt();
        }

        private void StartMessageReader()
        {
            Thread thread = new Thread(MessageReader);
            thread.IsBackground = true;
            thread.Name = "MessageReaderBitGet";
            thread.Start();
        }

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
                Thread.Sleep(100);

                if (webSocket != null &&
                    (webSocket.State == WebSocketState.Open ||
                    webSocket.State == WebSocketState.Connecting)
                    )
                {
                    if (TimeToSendPing.AddSeconds(30) < DateTime.Now)
                    {
                        webSocket.Send("ping");
                        TimeToSendPing = DateTime.Now;
                    }
                }
                else
                {
                    Dispose();
                }
            }
        }

        private void MessageReader()
        {
            Thread.Sleep(1000);

            while (IsDispose == false)
            {
                Thread.Sleep(1);

                try
                {
                    if (FIFOListWebSocketMessage == null ||
                        FIFOListWebSocketMessage.Count == 0)
                    {
                        continue;
                    }

                    string message;
                    FIFOListWebSocketMessage.TryDequeue(out message);

                    if (message.Equals("pong"))
                    {
                        continue;
                    }

                    SendLogMessage(message, LogMessageType.System);

                    ResponseWebSocketMessageSubscrible SubscribleState = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageSubscrible());

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

        #endregion

        #region Trade

        public void SendOrder(Order order)
        {
            rateGateSendOrder.WaitToProceed();
            string jsonRequest = JsonConvert.SerializeObject(new
            {
                symbol = order.SecurityNameCode + "_SPBL",
                side = order.Side.ToString().ToLower(),
                orderType = order.TypeOrder.ToString().ToLower(),
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
        public void GetOrdersState(List<Order> orders)
        {

        }
        public void CancelAllOrders()
        {

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
        public void ResearchTradesToOrders(List<Order> orders)
        {

        }

        #endregion

        #region Data

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            int countNeedToLoad = GetCountCandlesFromSliceTime(startTime, endTime, timeFrameBuilder.TimeFrameTimeSpan);
            return GetCandleHistory(security.NameFull, timeFrameBuilder.TimeFrameTimeSpan, true, countNeedToLoad, endTime);
        }

        #endregion

        #region TradesEnities

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
                Thread.Sleep(100);

                if (TimeToUprdatePortfolio.AddSeconds(30) < DateTime.Now)
                {
                    CreateQueryPortfolio(false);
                    TimeToUprdatePortfolio = DateTime.Now;
                }

            }
        }
        public void GetPortfolios()
        {
            CreateQueryPortfolio(true);
        }
        public void GetSecurities()
        {
            try
            {
                HttpClient httpClient = new HttpClient();
                HttpResponseMessage responseMessage = httpClient.GetAsync(BaseUrl + "/api/spot/v1/public/products").Result;
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

        #endregion

        #region Events

        public event Action<Order> MyOrderEvent;
        public event Action<MyTrade> MyTradeEvent;
        public event Action<List<Portfolio>> PortfolioEvent;
        public event Action<List<Security>> SecurityEvent;
        public event Action<MarketDepth> MarketDepthEvent;
        public event Action<Trade> NewTradesEvent;
        public event Action ConnectEvent;
        public event Action DisconnectEvent;
        public event Action<string, LogMessageType> LogMessageEvent;


        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);
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

        private void UpdateSecurity(string json)
        {
            ResponseMessageRest<List<ResposeSymbol>> symbols = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<List<ResposeSymbol>>());

            List<Security> securities = new List<Security>();

            for (int i = 0; i < symbols.data.Count; i++)
            {
                var item = symbols.data[i];

                if (item.status.Equals("online"))
                {
                    securities.Add(new Security()
                    {
                        Exchange = ServerType.BitGetSpot.ToString(),
                        DecimalsVolume = Convert.ToInt32(item.quantityScale),
                        Name = item.symbolName,
                        NameFull = item.symbol,
                        NameClass = item.quoteCoin,
                        NameId = item.symbol,
                        SecurityType = SecurityType.CurrencyPair,
                        Decimals = Convert.ToInt32(item.priceScale),
                        PriceStep = GetPriceStep(Convert.ToInt32(item.priceScale)),
                        PriceStepCost = GetPriceStep(Convert.ToInt32(item.priceScale)),
                        State = SecurityStateType.Activ,
                        Lot = 1,
                    });
                }
            }

            SecurityEvent(securities);
        }

        private void UpdateTrade(string message)
        {
            ResponseWebSocketMessageAction<List<List<string>>> responseTrade = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<List<List<string>>>());

            if (responseTrade.data == null)
            {
                return;
            }
            Trade trade = new Trade();
            trade.SecurityNameCode = responseTrade.arg.instId;

            trade.Price = Convert.ToDecimal(responseTrade.data[0][1].Replace(".", ","));
            trade.Id = responseTrade.data[0][0];
            trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseTrade.data[0][0]));
            trade.Volume = Convert.ToDecimal(responseTrade.data[0][2].Replace('.', ',').Replace(".", ","));
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
                MyTrade myTrade = new MyTrade();

                myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseMyTrades.data[i].cTime));
                myTrade.NumberOrderParent = responseMyTrades.data[i].orderId;
                myTrade.NumberTrade = responseMyTrades.data[i].fillId.ToString();
                myTrade.Volume = responseMyTrades.data[i].fillQuantity.Replace('.', ',').ToDecimal();
                myTrade.Price = responseMyTrades.data[i].fillPrice.Replace('.', ',').ToDecimal();
                myTrade.SecurityNameCode = responseMyTrades.data[i].symbol.ToUpper().Replace("_SPBL", "");
                myTrade.Side = responseMyTrades.data[i].side.Equals("buy") ? Side.Buy : Side.Sell;

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

                Order newOrder = new Order();
                newOrder.SecurityNameCode = item.instId.Replace("_SPBL", "");
                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.cTime));

                if (!item.clOrdId.Equals(String.Empty) == true)
                {
                    newOrder.NumberUser = Convert.ToInt32(item.clOrdId);
                }

                newOrder.NumberMarket = item.ordId.ToString();
                newOrder.Side = item.side.Equals("long") ? Side.Buy : Side.Sell;
                newOrder.State = stateType;
                newOrder.Volume = item.sz.Replace('.', ',').ToDecimal();
                if (item.ordType.Equals("market") == false)
                {
                    newOrder.Price = item.avgPx.Replace('.', ',').ToDecimal() != 0 ? item.avgPx.Replace('.', ',').ToDecimal() : item.px.Replace('.', ',').ToDecimal();
                }
                newOrder.ServerType = ServerType.BitGetSpot;
                newOrder.PortfolioNumber = "BitGetSpot";

                MyOrderEvent(newOrder);

                if (stateType == OrderStateType.Done ||
                    stateType == OrderStateType.Patrial)
                {
                    // как только приходит ордер исполненный или частично исполненный триггер на запрос моего трейда по имени бумаги
                    CreateQueryMyTrade(newOrder.SecurityNameCode + "_SPBL", newOrder.NumberMarket);
                    // как только приходит ордер исполненный или частично исполненный триггер на запрос портфеля
                    CreateQueryPortfolio(false);
                }

            }
        }

        #endregion

        #region Querys

        private void CreateSubscribleSecurityMessageWebSocket(Security security)
        {
            webSocket.Send($"{{\"op\": \"subscribe\",\"args\": [{{\"instType\": \"sp\",\"channel\": \"trade\",\"instId\": \"{security.Name}\"}}]}}");
            webSocket.Send($"{{\"op\": \"subscribe\",\"args\": [{{ \"instType\": \"sp\",\"channel\": \"books15\",\"instId\": \"{security.Name}\"}}]}}");
            webSocket.Send($"{{\"op\": \"subscribe\",\"args\": [{{\"channel\": \"orders\",\"instType\": \"spbl\",\"instId\": \"{security.Name}_SPBL\"}}]}}");
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
        private void CreateQueryMyTrade(string nameSec, string OrdId)
        {
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
            HttpClient client = new HttpClient();
            HttpResponseMessage responseMessage = client.GetAsync(BaseUrl + $"/api/spot/v1/market/candles?symbol={nameSec}&period={stringInterval}&limit={limit}&before={TimeManager.GetTimeStampMilliSecondsToDateTime(timeEndToLoad)}").Result;
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
                            Volume = item.quoteVol.ToDecimal(),
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

        #endregion

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
                if (server.ServerParameters[i].Name.Equals("Candles to load"))
                {
                    var Param = (ServerParameterInt)server.ServerParameters[i];
                    return Param.Value;
                }
            }

            return 300;
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

            return Convert.ToDecimal(priceStep);

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
    }
}
