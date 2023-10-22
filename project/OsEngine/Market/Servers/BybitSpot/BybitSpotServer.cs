using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.BybitSpot.Entities;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using WebSocket4Net;

namespace OsEngine.Market.Servers.BybitSpot
{
    public class BybitSpotServer : AServer
    {

        public BybitSpotServer()
        {
            BybitSpotServerRealization realization = new BybitSpotServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");

        }

        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            return ((BybitSpotServerRealization)ServerRealization).GetCandleHistory(nameSec, tf, false, DateTime.Now, 0);
        }
    }

    public class BybitSpotServerRealization : IServerRealization
    {
        public ServerType ServerType => ServerType.BybitSpot;
        public ServerConnectStatus ServerStatus { get; set; }
        public List<IServerParameter> ServerParameters { get; set; }
        public DateTime ServerTime { get; set; }

        public BybitSpotServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        public void Connect()
        {
            PublicKey = ((ServerParameterString)ServerParameters[0]).Value;
            SeckretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

            HttpResponseMessage responseMessage = GetTimeServer();
            string content = responseMessage.Content.ReadAsStringAsync().Result;

            if (responseMessage.StatusCode != System.Net.HttpStatusCode.OK)
            {
                SendLogMessage(content, LogMessageType.Error);
            }
            else
            {
                IsDispose = false;
                ServerStatus = ServerConnectStatus.Connect;
                CreateMessageReaderHandler();
                StartUpdatePortfolios();
                StartChekedStateWebSocketConnection();
                CreatePublicWebSocketConnect();
                CreatePrivateWebSocketConnect();
                ConnectEvent();
            }
        }
        public void Dispose()
        {
            IsDispose = true;
            DisposeMessageReader();
            DisposePublicWebsocektConnect();
            DisposePrivateWebsocektConnect();
            ServerStatus = ServerConnectStatus.Disconnect;
            DisconnectEvent();
        }
        public void Subscrible(Security security)
        {
            SubscriblePubliceDataWebSocket(security);
        }

        #region Properties

        private string UrlWebSocketPublic = "wss://stream.bybit.com/spot/public/v3";
        private string UrlWebSocketPrivate = "wss://stream.bybit.com/spot/private/v3";
        private string UrlRest = "https://api.bytick.com";
        ConcurrentQueue<string> concurrentQueueMessageWebSocket;
        WebSocket webSocketPublic;
        WebSocket webSocketPrivate;
        private string PublicKey = String.Empty;
        private string SeckretKey = String.Empty;
        private bool IsDispose;

        #endregion

        #region MessageReader 
        private void CreateMessageReaderHandler()
        {
            concurrentQueueMessageWebSocket = new ConcurrentQueue<string>();
            Thread threadMessageReader = new Thread(() => MessageReader());
            threadMessageReader.IsBackground = true;
            threadMessageReader.Name = "ThreadMessageReader";
            threadMessageReader.Start();
        }

        private void DisposeMessageReader()
        {
            if (concurrentQueueMessageWebSocket != null)
            {
                concurrentQueueMessageWebSocket = null;
            }
        }

        private void MessageReader()
        {
            while (IsDispose == false)
            {
                Thread.Sleep(1);
                try
                {
                    if (concurrentQueueMessageWebSocket == null ||
                        concurrentQueueMessageWebSocket.Count == 0)
                    {
                        continue;
                    }

                    string message;
                    concurrentQueueMessageWebSocket.TryDequeue(out message);

                    SubscribleMessage subscribleMessage =
                       JsonConvert.DeserializeAnonymousType(message, new SubscribleMessage());

                    if (subscribleMessage.op != null)
                    {
                        if (subscribleMessage.success == false)
                        {
                            SendLogMessage("WebSocket Error: " + subscribleMessage.ret_msg, LogMessageType.Error);
                            Dispose();
                        }

                        continue;
                    }


                    ResponseWebSocketMessage<object> response =
                        JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<object>());

                    if (response.topic != null)
                    {
                        if (response.topic.Contains("trade"))
                        {
                            UpdateTrade(message, response);
                            continue;
                        }
                        else if (response.topic.Contains("orderbook"))
                        {
                            UpdateOrderBook(message, response);
                            continue;
                        }
                        else if (response.topic.Contains("ticketInfo"))
                        {
                            UpdateMyTrade(message);
                            continue;
                        }
                        else if (response.topic.Contains("order"))
                        {
                            UpdateOrder(message);
                            continue;
                        }
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.Message, LogMessageType.Error);

                    Thread.Sleep(1000);
                }
            }
        }


        #endregion

        #region PublicWebSocketConnection

        private void CreatePublicWebSocketConnect()
        {
            webSocketPublic = new WebSocket(UrlWebSocketPublic);
            webSocketPublic.EnableAutoSendPing = true;
            webSocketPublic.AutoSendPingInterval = 10;
            webSocketPublic.MessageReceived += WebSocketPublic_MessageReceived;
            webSocketPublic.Closed += WebSocketPublic_Closed;
            webSocketPublic.Error += WebSocketPublic_Error;
            webSocketPublic.Open();
        }

        private void DisposePublicWebsocektConnect()
        {
            if (webSocketPublic != null)
            {
                webSocketPublic.Dispose();
                webSocketPublic.MessageReceived -= WebSocketPublic_MessageReceived;
                webSocketPublic.Closed -= WebSocketPublic_Closed;
                webSocketPublic.Error -= WebSocketPublic_Error;
                webSocketPublic = null;

            }
        }

        private void WebSocketPublic_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            SendLogMessage(e.Exception.Message, LogMessageType.Error);
        }

        private void WebSocketPublic_Closed(object sender, EventArgs e)
        {
            Dispose();
        }

        private void WebSocketPublic_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            if (concurrentQueueMessageWebSocket != null)
            {
                concurrentQueueMessageWebSocket.Enqueue(e.Message);
            }
        }

        private void SubscriblePubliceDataWebSocket(Security security)
        {
            if (webSocketPublic != null)
            {
                if (webSocketPublic.State == WebSocketState.Open)
                {
                    webSocketPublic.Send($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"trade.{security.Name}\" ] }}");
                    webSocketPublic.Send($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"orderbook.40.{security.Name}\" ] }}");
                }
            }
        }

        #endregion

        #region PrivateWebSocketConnection

        private void CreatePrivateWebSocketConnect()
        {
            webSocketPrivate = new WebSocket(UrlWebSocketPrivate);
            webSocketPrivate.EnableAutoSendPing = true;
            webSocketPrivate.AutoSendPingInterval = 10;
            webSocketPrivate.MessageReceived += WebSocketPrivate_MessageReceived;
            webSocketPrivate.Closed += WebSocketPrivate_Closed;
            webSocketPrivate.Error += WebSocketPrivate_Error;
            webSocketPrivate.Opened += WebSocketPrivate_Opened;
            webSocketPrivate.Open();
        }

        private void DisposePrivateWebsocektConnect()
        {
            if (webSocketPrivate != null)
            {
                webSocketPrivate.Dispose();
                webSocketPrivate.MessageReceived -= WebSocketPrivate_MessageReceived;
                webSocketPrivate.Closed -= WebSocketPrivate_Closed;
                webSocketPrivate.Error -= WebSocketPrivate_Error;
                webSocketPrivate.Opened -= WebSocketPrivate_Opened;
                webSocketPrivate = null;

            }
        }

        private void WebSocketPrivate_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            if (concurrentQueueMessageWebSocket != null)
            {
                concurrentQueueMessageWebSocket.Enqueue(e.Message);
            }
        }

        private void WebSocketPrivate_Closed(object sender, EventArgs e)
        {
            Dispose();
        }

        private void WebSocketPrivate_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            SendLogMessage(e.Exception.Message, LogMessageType.Error);
        }

        private void WebSocketPrivate_Opened(object sender, EventArgs e)
        {
            string authRequest = GetWebSocketAuthRequest();

            webSocketPrivate.Send(authRequest);
            webSocketPrivate.Send("{\"req_id\": \"order_1\", \"op\": \"subscribe\",\"args\": [\"order\"]}");
            webSocketPrivate.Send("{\"req_id\": \"ticketInfo_1\", \"op\": \"subscribe\", \"args\": [ \"ticketInfo\"]}");
        }

        #endregion

        #region CheckAliveWebSocketsConnection

        private void StartChekedStateWebSocketConnection()
        {
            Thread thread = new Thread(CheckAliveStateWebSocket);
            thread.IsBackground = true;
            thread.Start();
        }

        private void CheckAliveStateWebSocket()
        {
            while (IsDispose == false)
            {
                Thread.Sleep(20000);

                if (webSocketPublic == null ||
                    webSocketPrivate == null)
                {
                    Dispose();
                    continue;
                }

                if (webSocketPublic.State != WebSocketState.Open ||
                    webSocketPrivate.State != WebSocketState.Open)
                {
                    Dispose();
                }
            }
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
            var candeles = GetCandleHistory(security.Name, timeFrameBuilder.TimeFrameTimeSpan, true, endTime, countNeedToLoad);
            return candeles;
        }

        #endregion

        #region Trade

        public void CancelAllOrdersToSecurity(Security security)
        {

        }
        public void GetOrdersState(List<Order> orders)
        {

        }

        public void ResearchTradesToOrders(List<Order> orders)
        {

        }
        public void SendOrder(Order order)
        {

            var orderPlace = new
            {
                symbol = order.SecurityNameCode,
                orderPrice = order.Price.ToString().Replace(",", "."),
                side = order.Side.ToString().ToUpper(),
                orderQty = order.Volume.ToString().Replace(",", "."),
                orderType = order.TypeOrder.ToString().ToUpper(),
                timeInForce = "GTC",
                orderLinkId = order.NumberUser.ToString()
            };

            string json = JsonConvert.SerializeObject(orderPlace);
            HttpContent httpContent = new StringContent(json, Encoding.UTF8, "application/json");


            string endpoint = UrlRest + "/spot/v3/private/order";
            long timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            long recvWindow = 90000000;

            HttpClient client = new HttpClient();
            string signature = GenerateSignature(SeckretKey, $"{timestamp}{PublicKey}{recvWindow}{json}");
            client.DefaultRequestHeaders.Add("X-BAPI-API-KEY", PublicKey);
            client.DefaultRequestHeaders.Add("X-BAPI-TIMESTAMP", timestamp.ToString());
            client.DefaultRequestHeaders.Add("X-BAPI-RECV-WINDOW", recvWindow.ToString());
            client.DefaultRequestHeaders.Add("X-BAPI-SIGN", signature);

            HttpResponseMessage httpResponse = client.PostAsync(endpoint, httpContent).Result;
            string responseMessage = httpResponse.Content.ReadAsStringAsync().Result;

            if (httpResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                ResponseRestMessage<object> ErrorMessage = JsonConvert.DeserializeAnonymousType(responseMessage, new ResponseRestMessage<object>());
                if (ErrorMessage.retCode.Equals("0") == true)
                {
                    SendLogMessage($"Order Execute - Security:{order.SecurityNameCode} Price:{order.Price} Volume{order.Volume}", LogMessageType.Trade);
                }
                else
                {
                    SendLogMessage($"Code: {ErrorMessage.retCode}\n"
                        + $"Message: {ErrorMessage.retMsg}", LogMessageType.Error);
                }
            }
            else
            {
                SendLogMessage($"State Code: {httpResponse.StatusCode}", LogMessageType.Error);
            }
        }


        public void CancelAllOrders()
        {

        }

        public void CancelOrder(Order order)
        {
            var orderPlace = new
            {
                orderId = order.NumberMarket,
            };

            string json = JsonConvert.SerializeObject(orderPlace);
            HttpContent httpContent = new StringContent(json, Encoding.UTF8, "application/json");


            string endpoint = UrlRest + "/spot/v3/private/cancel-order";
            long timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            long recvWindow = 90000000;

            HttpClient client = new HttpClient();
            string signature = GenerateSignature(SeckretKey, $"{timestamp}{PublicKey}{recvWindow}{json}");
            client.DefaultRequestHeaders.Add("X-BAPI-API-KEY", PublicKey);
            client.DefaultRequestHeaders.Add("X-BAPI-TIMESTAMP", timestamp.ToString());
            client.DefaultRequestHeaders.Add("X-BAPI-RECV-WINDOW", recvWindow.ToString());
            client.DefaultRequestHeaders.Add("X-BAPI-SIGN", signature);

            HttpResponseMessage httpResponse = client.PostAsync(endpoint, httpContent).Result;
            string responseMessage = httpResponse.Content.ReadAsStringAsync().Result;

            if (httpResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                ResponseRestMessage<object> ErrorMessage = JsonConvert.DeserializeAnonymousType(responseMessage, new ResponseRestMessage<object>());
                if (ErrorMessage.retCode.Equals("0") == true)
                {
                    SendLogMessage($"Cancel Order - Security:{order.SecurityNameCode} Price:{order.Price} Volume{order.Volume}", LogMessageType.Trade);
                }
                else
                {
                    SendLogMessage($"Code: {ErrorMessage.retCode}\n"
                        + $"Message: {ErrorMessage.retMsg}", LogMessageType.Error);
                }
            }
            else
            {
                SendLogMessage($"State Code: {httpResponse.StatusCode}", LogMessageType.Error);
            }
        }

        #endregion

        #region TradesEntity
        private void StartUpdatePortfolios()
        {
            new Thread(() => {
                UpdatingPortfolios();
            }).Start();
        }
        private void UpdatingPortfolios()
        {
            while (IsDispose == false)
            {
                Thread.Sleep(20000);
                try
                {
                    HttpResponseMessage response = GetQueryAccountInfo();

                    string content = response.Content.ReadAsStringAsync().Result;

                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        ResponseRestMessage<object> ErrorMessage = JsonConvert.DeserializeAnonymousType(content, new ResponseRestMessage<object>());
                        if (ErrorMessage.retCode.Equals("0") == true)
                        {
                            UpdatePorfolio(content, false);
                        }
                        else
                        {
                            SendLogMessage($"Code: {ErrorMessage.retCode}\n"
                                + $"Message: {ErrorMessage.retMsg}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage($"State Code: {response.StatusCode}", LogMessageType.Error);
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.Message, LogMessageType.Error);
                }

            }
        }
        public void GetPortfolios()
        {
            HttpResponseMessage response = GetQueryAccountInfo();

            string content = response.Content.ReadAsStringAsync().Result;

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                ResponseRestMessage<object> ErrorMessage = JsonConvert.DeserializeAnonymousType(content, new ResponseRestMessage<object>());
                if (ErrorMessage.retCode.Equals("0") == true)
                {
                    UpdatePorfolio(content, true);
                }
                else
                {
                    SendLogMessage($"Code: {ErrorMessage.retCode}\n"
                        + $"Message: {ErrorMessage.retMsg}", LogMessageType.Error);
                }
            }
            else
            {
                SendLogMessage($"State Code: {response.StatusCode}", LogMessageType.Error);
            }
        }
        public void GetSecurities()
        {
            HttpClient httpClient = new HttpClient();
            HttpResponseMessage responseMessage = httpClient.GetAsync(UrlRest + "/spot/v3/public/symbols").Result;
            string content = responseMessage.Content.ReadAsStringAsync().Result;

            if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
            {
                ResponseRestMessage<object> ErrorMessage = JsonConvert.DeserializeAnonymousType(content, new ResponseRestMessage<object>());
                if (ErrorMessage.retCode.Equals("0") == true)
                {
                    UpdateSecurities(content);
                }
                else
                {
                    SendLogMessage($"Code: {ErrorMessage.retCode}\n"
                       + $"Message: {ErrorMessage.retMsg}", LogMessageType.Error);
                }
            }
            else
            {
                SendLogMessage($"State Code: {responseMessage.StatusCode}", LogMessageType.Error);
            }
        }
        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf, bool IsOsData, DateTime timeEnd, int CountToLoad)
        {
            string stringInterval = $"{tf.TotalMinutes}m";

            if(tf.TotalMinutes >= 60)
            {
                stringInterval = $"{tf.TotalHours}h";
            }


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

                rangeCandles = GetQueryCandles(nameSec, stringInterval, limit, TimeToRequest.AddSeconds(10));

                rangeCandles.Reverse();

                candles.AddRange(rangeCandles);

                TimeToRequest = candles[candles.Count - 1].TimeStart;

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

        private void UpdateTrade(string message, ResponseWebSocketMessage<object> response)
        {
            ResponseWebSocketMessage<ResponseTrade> responseTrade =
                               JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<ResponseTrade>());

            var topic = response.topic.Split('.');

            if (responseTrade.data.m == true)
            {
                NewTradesEvent(new Trade()
                {
                    Id = responseTrade.data.v,
                    Time = TimeManager.GetDateTimeFromTimeStamp(responseTrade.data.t),
                    Price = responseTrade.data.p.ToDecimal(),
                    Volume = responseTrade.data.q.ToDecimal(),
                    Side = responseTrade.data.m == true ? Side.Buy : Side.Sell,
                    SecurityNameCode = topic[1]
                });
            }
        }
        private void UpdateOrderBook(string message, ResponseWebSocketMessage<object> response)
        {
            ResponseWebSocketMessage<ResponseOrderBook> responseDepth =
                              JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<ResponseOrderBook>());

            var topic = response.topic.Split('.');

            List<MarketDepthLevel> marketDepthLevelsAsks = new List<MarketDepthLevel>();
            List<MarketDepthLevel> marketDepthLevelsBids = new List<MarketDepthLevel>();

            for (int i = 0; i < (responseDepth.data.a.Length / 2) - 10; i++)
            {
                marketDepthLevelsAsks.Add(new MarketDepthLevel()
                {
                    Ask = responseDepth.data.a[i, 1].ToDecimal(),
                    Price = responseDepth.data.a[i, 0].ToDecimal(),
                });

                marketDepthLevelsBids.Add(new MarketDepthLevel()
                {
                    Bid = responseDepth.data.b[i, 1].ToDecimal(),
                    Price = responseDepth.data.b[i, 0].ToDecimal(),
                });

            }

            MarketDepthEvent(new MarketDepth()
            {
                Asks = marketDepthLevelsAsks,
                Bids = marketDepthLevelsBids,
                SecurityNameCode = topic[2],
                Time = TimeManager.GetDateTimeFromTimeStamp(responseDepth.ts)
            });
        }
        private void UpdatePorfolio(string message, bool IsUpdateValueBegin)
        {
            ResponseRestMessage<ArrayPortfolios> symbols = JsonConvert.DeserializeAnonymousType(message, new ResponseRestMessage<ArrayPortfolios>());

            Portfolio portfolio = new Portfolio();
            portfolio.Number = "BybitSpot";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            foreach (var item in symbols.result.balances)
            {

                var pos = new PositionOnBoard()
                {
                    PortfolioName = "BybitSpot",
                    SecurityNameCode = item.coinId,
                    ValueBlocked = item.locked.ToDecimal(),
                    ValueCurrent = item.free.ToDecimal()
                };

                if (IsUpdateValueBegin)
                {
                    pos.ValueBegin = item.free.ToDecimal();
                }

                portfolio.SetNewPosition(pos);
            }

            PortfolioEvent(new List<Portfolio> { portfolio });
        }
        private void UpdateSecurities(string message)
        {
            ResponseRestMessage<ArraySymbols> symbols = JsonConvert.DeserializeAnonymousType(message, new ResponseRestMessage<ArraySymbols>());

            List<Security> securities = new List<Security>();

            for (int i = 0; i < symbols.result.list.Count; i++)
            {
                var item = symbols.result.list[i];

                securities.Add(new Security()
                {
                    DecimalsVolume = GetDecimalsVolume(item.minTradeQty),
                    Name = item.name,
                    NameFull = item.name,
                    NameClass = item.quoteCoin,
                    NameId = item.name,
                    SecurityType = SecurityType.CurrencyPair,
                    Decimals = GetDecimalsPrice(item.minPricePrecision),
                    PriceStep = item.minPricePrecision.ToDecimal(),
                    PriceStepCost = item.minPricePrecision.ToDecimal(),
                    State = SecurityStateType.Activ,
                    Lot = 1,
                });
            }

            SecurityEvent(securities);
        }
        private void UpdateMyTrade(string message)
        {
            ResponseWebSocketMessage<List<ResponseMyTrades>> responseMyTrades = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<List<ResponseMyTrades>>());

            for (int i = 0; i < responseMyTrades.data.Count; i++)
            {
                MyTrade myTrade = new MyTrade();

                myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseMyTrades.data[i].t));
                myTrade.NumberOrderParent = responseMyTrades.data[i].o;
                myTrade.NumberTrade = responseMyTrades.data[i].T.ToString();
                myTrade.Volume = responseMyTrades.data[i].q.Replace('.', ',').ToDecimal();
                myTrade.Price = responseMyTrades.data[i].p.Replace('.', ',').ToDecimal();
                myTrade.SecurityNameCode = responseMyTrades.data[i].s;
                myTrade.Side = responseMyTrades.data[i].S.Equals("BUY") ? Side.Buy : Side.Sell;

                MyTradeEvent(myTrade);
            }
            
        }
        private void UpdateOrder(string message)
        {
            ResponseWebSocketMessage<List<ResponseOrder>> responseMyTrades = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<List<ResponseOrder>>());

            for (int i = 0; i < responseMyTrades.data.Count; i++)
            {
                OrderStateType stateType = OrderStateType.None;

                stateType = responseMyTrades.data[i].X switch
                {
                    "NEW" => OrderStateType.Activ,
                    "ORDER_NEW" => OrderStateType.Activ,
                    "PARTIALLY_FILLED" => OrderStateType.Patrial,
                    "FILLED" => OrderStateType.Done,
                    "ORDER_FILLED" => OrderStateType.Done,
                    "CANCELED" => OrderStateType.Cancel,
                    "ORDER_CANCELED" => OrderStateType.Cancel,
                    "PARTIALLY_FILLED_CANCELLED" => OrderStateType.Cancel,
                    "REJECTED" => OrderStateType.Fail,
                    "ORDER_REJECTED" => OrderStateType.Fail,
                    "ORDER_FAILED" => OrderStateType.Fail,
                    _ => OrderStateType.None,
                };
                Order newOrder = new Order();
                newOrder.SecurityNameCode = responseMyTrades.data[i].s;
                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseMyTrades.data[i].O));
                newOrder.NumberUser = Convert.ToInt32(responseMyTrades.data[i].c);

                newOrder.NumberMarket = responseMyTrades.data[i].i;
                newOrder.Side = responseMyTrades.data[i].S.Equals("BUY") ? Side.Buy : Side.Sell;
                newOrder.State = stateType;
                newOrder.Volume = responseMyTrades.data[i].q.Replace('.', ',').ToDecimal();
                newOrder.Price = responseMyTrades.data[i].p.Replace('.', ',').ToDecimal();
                newOrder.ServerType = ServerType.BybitSpot;
                newOrder.PortfolioNumber = "BybitSpot";


                MyOrderEvent(newOrder);
            }
        }
        private void SendLogMessage(string message, LogMessageType type)
        {
            LogMessageEvent(message, type);
        }

        #endregion

        private int GetDecimalsVolume(string str)
        {
            var s = str.Split('.');
            if (s.Length > 1)
            {
                return s[1].Length;
            }
            else
            {
                return 0;
            }
        }
        private int GetDecimalsPrice(string str)
        {
            var s = str.Split('.');
            if (s.Length > 1)
            {
                return s[1].Length;
            }
            else
            {
                return s[0].Length;
            }
        }
        private HttpResponseMessage GetTimeServer()
        {
            HttpClient httpClient = new HttpClient();
            HttpResponseMessage responseMessage = httpClient.GetAsync(UrlRest + "/v3/public/time").Result;
            return responseMessage;
        }
        private string GenerateSignature(string secret, string message)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }
        private string GetWebSocketAuthRequest()
        {
            var expires = TimeManager.GetTimeStampMilliSecondsToDateTime(DateTime.UtcNow) + 20000;
            var signature = GenerateSignature(SeckretKey, "GET/realtime" + expires.ToString());
            var sign = $"{{\"op\":\"auth\",\"args\":[\"{PublicKey}\",\"{expires}\", \"{signature}\"]}}";
            return sign;
        }
        private HttpResponseMessage GetQueryAccountInfo()
        {
            string endpoint = UrlRest + "/spot/v3/private/account";
            long timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            long recvWindow = 90000000;

            HttpClient client = new HttpClient();
            string signature = GenerateSignature(SeckretKey, $"{timestamp}{PublicKey}{recvWindow}");
            client.DefaultRequestHeaders.Add("X-BAPI-API-KEY", PublicKey);
            client.DefaultRequestHeaders.Add("X-BAPI-TIMESTAMP", timestamp.ToString());
            client.DefaultRequestHeaders.Add("X-BAPI-RECV-WINDOW", recvWindow.ToString());
            client.DefaultRequestHeaders.Add("X-BAPI-SIGN", signature);

            return client.GetAsync(endpoint).Result;

        }
        private List<Candle> GetQueryCandles(string nameSec, string stringInterval,
            int limit, DateTime timeEndToLoad)
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage responseMessage = client.GetAsync(UrlRest + $"/spot/v3/public/quote/kline?symbol={nameSec}&interval={stringInterval}&limit={limit}&endTime={TimeManager.GetTimeStampMilliSecondsToDateTime(timeEndToLoad)}").Result;
            string content = responseMessage.Content.ReadAsStringAsync().Result;


            if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
            {
                ResponseRestMessage<ArrayBars> symbols = JsonConvert.DeserializeAnonymousType(content, new ResponseRestMessage<ArrayBars>());

                if (symbols.retCode.Equals("0") == true)
                {
                    List<Candle> candles = new List<Candle>();

                    foreach (var item in symbols.result.list)
                    {
                        candles.Add(new Candle()
                        {
                            Close = item.c.ToDecimal(),
                            High = item.h.ToDecimal(),
                            Low = item.l.ToDecimal(),
                            Open = item.o.ToDecimal(),
                            Volume = item.v.ToDecimal(),
                            State = CandleState.Finished,
                            TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.t))
                        });
                    }

                    return candles;
                }
                else
                {
                    SendLogMessage($"Code: {symbols.retCode}\n"
                        + $"Message: {symbols.retMsg}", LogMessageType.Error);
                    return null;
                }
            }
            else
            {
                SendLogMessage($"State Code: {responseMessage.StatusCode}", LogMessageType.Error);
                return null;
            }
        }
        private int GetCountCandlesToLoad()
        {
            var server = (AServer)ServerMaster.GetServers().Find(server => server.ServerType == ServerType.BybitSpot);

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

    }
}
