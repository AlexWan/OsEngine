using Com.Lmax.Api.Internal;
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.Pionex.Entity;
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

namespace OsEngine.Market.Servers.Pionex
{
    public class PionexServerSpot : AServer
    {
        public PionexServerSpot()
        {
             PionexServerRealization realization = new PionexServerRealization();
             ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
        }
    }

    public class PionexServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public PionexServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread threadForPublicMessages = new Thread(PublicMessageReader);
            threadForPublicMessages.IsBackground = true;
            threadForPublicMessages.Name = "PublicMessageReaderPionex";
            threadForPublicMessages.Start();

            Thread threadForPrivateMessages = new Thread(PrivateMessageReader);
            threadForPrivateMessages.IsBackground = true;
            threadForPrivateMessages.Name = "PrivateMessageReaderPionex";
            threadForPrivateMessages.Start();
        }

        public DateTime ServerTime { get ; set ; }

        public void Connect()
        {
            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

            HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(_baseUrl + _prefix + "common/symbols?symbol=BTC_USDT").Result;

            if (responseMessage.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    WebSocketPublicMessages = new ConcurrentQueue<string>();
                    WebSocketPrivateMessages = new ConcurrentQueue<string>();
                    CreateWebSocketConnection();
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    SendLogMessage("Connection cannot be open. Pionex. Error request", LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            else
            {
                SendLogMessage("Connection cannot be open. Pionex. Error request", LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public void Dispose()
        {
            try
            {
                UnsubscribeFromAllWebSockets();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            try
            {
                DeleteWebsocketConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            _subscribedSecurities.Clear();
            _decimalsVolume.Clear();

            WebSocketPublicMessages = new ConcurrentQueue<string>();
            WebSocketPrivateMessages = new ConcurrentQueue<string>();

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void UnsubscribeFromAllWebSockets()
        {
            if (_webSocketPublic == null || _webSocketPrivate == null)
            {  return; }
               
            for (int i = 0; i < _subscribedSecurities.Count; i++)
            {
                string securityName = _subscribedSecurities[i];

                _webSocketPublic.Send($"{{\"op\": \"UNSUBSCRIBED\", \"topic\": \"TRADE\", \"symbol\": {securityName}\"}}"); // trades
                _webSocketPublic.Send($"{{\"op\": \"UNSUBSCRIBED\",  \"topic\":  \"DEPTH\",  \"symbol\": {securityName}\", \"limit\":  10 }}"); // depth

                _webSocketPrivate.Send("{\"op\": \"UNSUBSCRIBE\", \"topic\": \"BALANCE\"}"); // myportfolio
                _webSocketPrivate.Send($"{{\"op\": \"UNSUBSCRIBE\", \"topic\": \"ORDER\", \"symbol\": \"{securityName}\"}}"); // myorders
                _webSocketPrivate.Send($"{{\"op\": \"UNSUBSCRIBE\",  \"topic\":  \"FILL\",  \"symbol\": \"{securityName}\"}}"); // mytrades
            }
        }

        public ServerType ServerType
        {
            get { return ServerType.PionexSpot; }
        }

        public ServerConnectStatus ServerStatus { get; set ; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string _publicKey;

        private string _secretKey;

        private string _baseUrl = "https://api.pionex.com";

        private string _prefix = "/api/v1/";

        private string _pathUrl = string.Empty;

        #endregion

        #region 3 Securities

        RateGate _rateGateGetSec = new RateGate(2, TimeSpan.FromMilliseconds(1000));

        public void GetSecurities()
        {
            _rateGateGetSec.WaitToProceed();

            try
            {
                HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(_baseUrl + _prefix + "common/symbols").Result;
                string json = responseMessage.Content.ReadAsStringAsync().Result;

                ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<object>());

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    if (stateResponse.result.Equals("true") == true)
                    {
                        UpdateSecurity(json);
                    }
                    else
                    {
                        SendLogMessage($"Result: {stateResponse.result}\n"
                            + $"Message: {stateResponse.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"GetSecurities> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                    if (stateResponse != null && stateResponse.code != null)
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n"
                            + $"Message: {stateResponse.message}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSecurity(string json)
        {
            ResponseMessageRest<ResponseSymbols> symbols = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<ResponseSymbols>());

            List<Security> securities = new List<Security>();

            for (int i = 0; i < symbols.data.symbols.Length; i++)
            {
                Symbol item = symbols.data.symbols[i];

                if (item.enable.Equals("true"))
                {
                    Security newSecurity = new Security();

                    newSecurity.Exchange = ServerType.PionexSpot.ToString();
                    
                    newSecurity.Lot = 1;
                    newSecurity.Name = item.symbol;
                    newSecurity.NameFull = item.symbol;
                    newSecurity.NameClass = item.symbol.Split('_')[1];
                    newSecurity.NameId = item.symbol;
                    newSecurity.SecurityType = SecurityType.CurrencyPair;
                    newSecurity.MinTradeAmount = item.minAmount.ToDecimal();
                    newSecurity.Decimals = Convert.ToInt32(item.quotePrecision);
                    newSecurity.PriceStep = newSecurity.Decimals.GetValueByDecimals();
                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                    newSecurity.DecimalsVolume = Convert.ToInt32(item.basePrecision); 
                    newSecurity.State = SecurityStateType.Activ;

                    securities.Add(newSecurity);
                }
            }

            SecurityEvent(securities);
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        public void GetPortfolios()
        {
            CreateQueryPortfolio();
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            return CreateQueryCandles(security, timeFrameBuilder, candleCount,1);
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            // лимит на 500 свечек по таймфрейму
            return null;
        }

        #endregion

        #region 6 WebSocket creation

        private WebSocket _webSocketPrivate;

        private WebSocket _webSocketPublic;
        
        private string _webSocketPublicUrl = "wss://ws.pionex.com/wsPub";

        private void CreateWebSocketConnection()
        {
            _webSocketPublic = new WebSocket(_webSocketPublicUrl);
            _webSocketPublic.EnableAutoSendPing = true;
            _webSocketPublic.AutoSendPingInterval = 15;

            _webSocketPublic.Opened += WebSocketPublic_Opened;
            _webSocketPublic.Closed += WebSocketPublic_Closed;
            _webSocketPublic.DataReceived += WebSocketPublic_DataReceived;
            _webSocketPublic.Error += WebSocketPublic_Error;
            _webSocketPublic.Open();

            _webSocketPrivate = new WebSocket(GenerateUrlForPrivateWS());
            _webSocketPrivate.EnableAutoSendPing = true;
            _webSocketPrivate.AutoSendPingInterval = 15;

            _webSocketPrivate.Opened += WebSocketPrivate_Opened;
            _webSocketPrivate.Closed += WebSocketPrivate_Closed;
            _webSocketPrivate.DataReceived += WebSocketPrivate_DataReceived;
            _webSocketPrivate.Error += WebSocketPrivate_Error;
            _webSocketPrivate.Open();

        }

        private void DeleteWebsocketConnection()
        {
            if (_webSocketPublic != null)
            {
                try
                {
                    _webSocketPublic.Close();
                }
                catch
                {
                    // ignore
                }

                _webSocketPublic.Opened -= WebSocketPublic_Opened;
                _webSocketPublic.Closed -= WebSocketPublic_Closed;
                _webSocketPublic.DataReceived -= WebSocketPublic_DataReceived;
                _webSocketPublic.Error -= WebSocketPublic_Error;
                _webSocketPublic = null;
            }

            if (_webSocketPrivate != null)
            {
                try
                {
                    _webSocketPrivate.Close();
                }
                catch
                {
                    // ignore
                }

                _webSocketPrivate.Opened -= WebSocketPrivate_Opened;
                _webSocketPrivate.Closed -= WebSocketPrivate_Closed;
                _webSocketPrivate.DataReceived -= WebSocketPrivate_DataReceived;
                _webSocketPrivate.Error -= WebSocketPrivate_Error;
                _webSocketPrivate = null;
            }
        }

        private string GenerateUrlForPrivateWS()
        {
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

            string preHash = "/ws" + "?key=" + _publicKey + "&timestamp=" + timestamp + "websocket_auth";

            string signature = SHA256HexHashString(_secretKey, preHash);

            return "wss://ws.pionex.com/ws" + "?key=" + _publicKey + "&timestamp=" + timestamp + "&signature=" + signature;
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocketPublic_Error(object sender, ErrorEventArgs e)
        {
            ErrorEventArgs error = (ErrorEventArgs)e;

            if (error.Exception != null)
            {
                SendLogMessage(error.Exception.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublic_DataReceived(object sender, DataReceivedEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
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

                if (WebSocketPublicMessages == null)
                {
                    return;
                }

                if (e.Data.GetType().ToString() == "System.Byte[]")
                {
                   WebSocketPublicMessages.Enqueue(Encoding.UTF8.GetString(e.Data));
                }

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublic_Closed(object sender, EventArgs e)
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                SendLogMessage("Connection Closed by Pionex. WebSocket Public Closed Event", LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void WebSocketPublic_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Connection to public data is Open", LogMessageType.System);

            if (ServerStatus != ServerConnectStatus.Connect
                && _webSocketPublic != null
                && _webSocketPublic.State == WebSocketState.Open)
            {
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent();
            }
        }

        private void WebSocketPrivate_Error(object sender, ErrorEventArgs e)
        {
            ErrorEventArgs error = (SuperSocket.ClientEngine.ErrorEventArgs)e;
            if (error.Exception != null)
            {
                SendLogMessage(error.Exception.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPrivate_DataReceived(object sender, DataReceivedEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
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

                if (WebSocketPrivateMessages == null)
                {
                    return;
                }

                if (e.Data.GetType().ToString() == "System.Byte[]")
                {
                    WebSocketPrivateMessages.Enqueue(Encoding.UTF8.GetString(e.Data));
                }

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPrivate_Closed(object sender, EventArgs e)
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                SendLogMessage("Connection Closed by Pionex. WebSocket Private Closed Event", LogMessageType.Error);
               
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void WebSocketPrivate_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Connection to private data is Open", LogMessageType.System);

            if (ServerStatus != ServerConnectStatus.Connect
                && _webSocketPrivate != null
                && _webSocketPrivate.State == WebSocketState.Open)
            {
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent();
            }
    
            _webSocketPrivate.Send("{\"op\": \"SUBSCRIBE\", \"topic\": \"BALANCE\"}"); // подписка сразу на изменение портфеля
        }

        #endregion

        #region 8 Security Subscribed

        private List<string> _subscribedSecurities = new List<string>();

        private RateGate _rateGateSubscribed = new RateGate(1, TimeSpan.FromMilliseconds(500));

        public void Subscrible(Security security)
        {
            try
            {
                _rateGateSubscribed.WaitToProceed();

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                CreateSubscribedSecurityMessageWebSocket(security);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void CreateSubscribedSecurityMessageWebSocket(Security security)
        {

            if (ServerStatus == ServerConnectStatus.Disconnect)
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

            _subscribedSecurities.Add(security.Name);

            _webSocketPublic.Send($"{{\"op\": \"SUBSCRIBE\", \"topic\": \"TRADE\", \"symbol\": \"{security.Name}\"}}"); // trades
            _webSocketPublic.Send($"{{\"op\": \"SUBSCRIBE\",  \"topic\":  \"DEPTH\",  \"symbol\": \"{security.Name}\", \"limit\":  10 }}"); // depth https://pionex-doc.gitbook.io/apidocs/websocket/public-stream/depth

            _webSocketPrivate.Send($"{{\"op\": \"SUBSCRIBE\", \"topic\": \"ORDER\", \"symbol\": \"{security.Name}\"}}"); // myorders
            _webSocketPrivate.Send($"{{\"op\": \"SUBSCRIBE\",  \"topic\":  \"FILL\",  \"symbol\": \"{security.Name}\"}}"); // mytrades

            // собираем знаки после запятой в объеме инструмента, для корректного отображения в объеме позиции
            _decimalsVolume.Add(security.Name, security.DecimalsVolume);
        }

        #endregion

        #region 9 WebSocket parsing the messages

        private ConcurrentQueue<string> WebSocketPublicMessages = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> WebSocketPrivateMessages = new ConcurrentQueue<string>();

        private Dictionary<string, int> _decimalsVolume = new Dictionary<string, int>();

        private void PublicMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    if (WebSocketPublicMessages.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    WebSocketPublicMessages.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Contains("PING"))
                    {
                        string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                        string pong = $"{{\"op\": \"PONG\", \"timestamp\": {timeStamp}}}";
                        _webSocketPublic.Send(pong);
                        continue;
                    }

                    ResponseWebSocketMessage<object> stream = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<object>());

                    if (stream.topic != null && stream.data != null)
                    {
                        if (stream.topic.Equals("DEPTH"))
                        {
                            UpdateDepth(message);
                            continue;
                        }

                        if (stream.topic.Equals("TRADE"))
                        {
                            UpdateTrade(message);
                            continue;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Thread.Sleep(2000);
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }

        private void PrivateMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    if (WebSocketPrivateMessages.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    WebSocketPrivateMessages.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Contains("PING"))
                    {
                        string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                        string pong = $"{{\"op\": \"PONG\", \"timestamp\": {timeStamp}}}";
                        _webSocketPrivate.Send(pong);
                        continue;
                    }

                    ResponseWebSocketMessage<object> stream = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<object>());

                    if (stream.topic != null && stream.data != null)
                    {
                        if (stream.topic.Equals("ORDER"))
                        {
                            UpdateOrder(message);
                            continue;
                        }

                        if (stream.topic.Equals("FILL"))
                        {
                            UpdateMyTrade(message);
                            continue;
                        }

                        if (stream.topic.Equals("BALANCE"))
                        {
                            UpdatePortfolio(message);
                            continue;
                        }
                    }

                }
                catch (Exception exception)
                {
                    Thread.Sleep(2000);
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }

        private void UpdateTrade(string message)
        {
            ResponseWebSocketMessage<List<TradeElements>> responseTrade = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<List<TradeElements>>());

            if (responseTrade == null)
            {
                return;
            }

            if (responseTrade.data == null)
            {
                return;
            }

            TradeElements element = responseTrade.data[responseTrade.data.Count - 1];

            Trade trade = new Trade();

            trade.SecurityNameCode = element.symbol;
            trade.Price = element.price.ToDecimal();
            trade.Id = element.tradeId;
            trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(element.timestamp));
            trade.Volume = element.size.ToDecimal();
            trade.Side = element.side.Equals("BUY") ? Side.Buy : Side.Sell;

            NewTradesEvent(trade);
        }

        private void UpdateDepth(string message)
        {
            ResponseWebSocketMessage<ResponseWebSocketDepthItem> responseDepth = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<ResponseWebSocketDepthItem>());

            if (responseDepth.data == null)
            {
                return;
            }

            MarketDepth marketDepth = new MarketDepth();

            List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
            List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

            marketDepth.SecurityNameCode = responseDepth.symbol;

            for (int i = 0; i < responseDepth.data.asks.Count; i++)
            {
                MarketDepthLevel newMDLevel = new MarketDepthLevel();
                newMDLevel.Ask = responseDepth.data.asks[i][1].ToDecimal();
                newMDLevel.Price = responseDepth.data.asks[i][0].ToDecimal();
                ascs.Add(newMDLevel);
            }

            for (int i = 0; i < responseDepth.data.bids.Count; i++)
            {
                MarketDepthLevel newMDLevel = new MarketDepthLevel();
                newMDLevel.Bid = responseDepth.data.bids[i][1].ToDecimal();
                newMDLevel.Price = responseDepth.data.bids[i][0].ToDecimal();
                bids.Add(newMDLevel);
            }

            marketDepth.Asks = ascs;
            marketDepth.Bids = bids;
            marketDepth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseDepth.timestamp));

            if(marketDepth.Asks.Count == 0 ||
                marketDepth.Bids.Count == 0)
            {
                return;
            }

            MarketDepthEvent(marketDepth);
        }

        private void UpdatePortfolio(string message)
        {
            ResponseWebSocketMessage<ResponceWSBalance> responce = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<ResponceWSBalance>());

            Portfolio portfolio = new Portfolio();
            portfolio.Number = "PionexSpot";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            for (int i = 0; i < responce.data.balances.Length; i++)
            {
                PositionOnBoard newPortf = new PositionOnBoard();
                newPortf.SecurityNameCode = responce.data.balances[i].coin;
                newPortf.ValueBegin = responce.data.balances[i].free.ToDecimal();
                newPortf.ValueCurrent = responce.data.balances[i].free.ToDecimal();
                newPortf.ValueBlocked = responce.data.balances[i].frozen.ToDecimal();
                newPortf.PortfolioName = "PionexSpot";
                portfolio.SetNewPosition(newPortf);
            }

            PortfolioEvent(new List<Portfolio> { portfolio });
        }

        private void UpdateMyTrade(string message)
        {
            ResponseWebSocketMessage<MyTrades> responseTrades = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<MyTrades>());

            MyTrades item = responseTrades.data;

            long time = Convert.ToInt64(item.timestamp);

            MyTrade newTrade = new MyTrade();

            newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(time);
            newTrade.SecurityNameCode = item.symbol;
            newTrade.NumberOrderParent = item.orderId;
            newTrade.Price = item.price.ToDecimal();
            newTrade.NumberTrade = item.id;
            newTrade.Side = item.side.Equals("SELL") ? Side.Sell : Side.Buy;

            // при покупке комиссия берется с монеты и объем уменьшается и появляются лишние знаки после запятой
            decimal preVolume = newTrade.Side == Side.Sell ? item.size.ToDecimal() : item.size.ToDecimal() - item.fee.ToDecimal();

            newTrade.Volume = GetVolumeForMyTrade(item.symbol ,preVolume); 
            
            MyTradeEvent(newTrade);
        }

        private decimal GetVolumeForMyTrade(string symbol, decimal preVolume)
        {
            int forTruncate = 1;

            Dictionary<string, int>.Enumerator enumerator = _decimalsVolume.GetEnumerator();

            while (enumerator.MoveNext())
            {
                string key = enumerator.Current.Key;
                int value = enumerator.Current.Value;  

                if (key.Equals(symbol))
                {
                    if (value != 0)
                    {
                        for (int i = 0; i < value; i++)
                        {
                            forTruncate *= 10;
                        }
                    }
                    return Math.Truncate(preVolume * forTruncate) / forTruncate; // при округлении может получиться больше доступного объема, поэтому обрезаем
                }
            }
            return preVolume;
        }

        private void UpdateOrder(string message)
        {
            ResponseWebSocketMessage<MyOrders> responseOrders = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<MyOrders>());

            if (responseOrders.data == null)
            {
                return;
            }

            MyOrders item = responseOrders.data;

            OrderStateType stateType = GetOrderState(item.status, item.filledSize);

            if (item.type.Equals("MARKET") && stateType == OrderStateType.Activ)
            {
                return;
            }

            Order newOrder = new Order();

            newOrder.SecurityNameCode = item.symbol;
            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createTime));

            if(string.IsNullOrEmpty(item.clientOrderId))
            {
                return;
            }

            newOrder.NumberUser = Convert.ToInt32(item.clientOrderId);
            newOrder.NumberMarket = item.orderId;
            newOrder.Side = item.side.Equals("BUY") ? Side.Buy : Side.Sell;
            newOrder.State = stateType;
            newOrder.TypeOrder = item.type.Equals("MARKET") ? OrderPriceType.Market : OrderPriceType.Limit;
            newOrder.Volume = item.status.Equals("OPEN") ? item.size.ToDecimal() : item.filledSize.ToDecimal();
            newOrder.Price = item.price.ToDecimal();
            newOrder.ServerType = ServerType.PionexSpot;
            newOrder.PortfolioNumber = "PionexSpot";

            MyOrderEvent(newOrder);
        }

        private OrderStateType GetOrderState(string status, string filledSize)
        {
            OrderStateType stateType;

            if (status == "OPEN" && filledSize == "0")
            {
                stateType = OrderStateType.Activ;
            }
            else if (status == "OPEN" && filledSize != "0")
            {
                stateType = OrderStateType.Patrial;
            }
            else if (status == "CLOSED" && filledSize != "0")
            {
                stateType = OrderStateType.Done;
            }
            else if (status == "CLOSED" && filledSize == "0")
            {
                stateType = OrderStateType.Cancel;
            }
            else
            {
                stateType = OrderStateType.None;
            }

            return stateType;
        }

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        #endregion

        #region 10 Trade

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public void SendOrder(Order order)
        {
            _rateGate.WaitToProceed();

            SendNewOrder data = new SendNewOrder();
            data.clientOrderId = order.NumberUser.ToString();
            data.symbol = order.SecurityNameCode;
            data.side = order.Side.ToString().ToUpper();
            data.type = order.TypeOrder.ToString().ToUpper();
            data.price = order.TypeOrder == OrderPriceType.Market ? null : order.Price.ToString().Replace(",", ".");
            data.size = order.Volume.ToString().Replace(",", ".");
            data.amount = (order.Volume * order.Price).ToString().Replace(",", "."); // для BUY MARKET ORDER указывается размер в USDT не меньше 10
            data.IOC = false;

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

            _pathUrl = "trade/order";

            JsonSerializerSettings dataSerializerSettings = new JsonSerializerSettings();

            dataSerializerSettings.NullValueHandling = NullValueHandling.Ignore;// если LIMIT-ордер, то игнорим параметр amount

            string body = JsonConvert.SerializeObject(data, dataSerializerSettings);

            string _signature = GenerateSignature("POST", _pathUrl, timestamp, body, null);

            IRestResponse json = CreatePrivateRequest(_signature, _pathUrl, Method.POST, timestamp, body, null);

            if (json.StatusCode == HttpStatusCode.OK)
            {
                ResponseMessageRest<ResponseCreateOrder> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseMessageRest<ResponseCreateOrder>());
                if (response.result == "true")
                {
                    SendLogMessage($"Order num {order.NumberUser} on exchange.", LogMessageType.Trade);
                    order.State = OrderStateType.Activ;
                    order.NumberMarket = response.data.orderId;
                }
                else
                {
                    CreateOrderFail(order);
                    SendLogMessage($"Code: {response.code}\nMessage: {response.message}", LogMessageType.Error);
                }
            }
            else
            {
                CreateOrderFail(order);
                SendLogMessage($"Http State Code: {json.StatusCode} - {json.Content}", LogMessageType.Error);
            }

            MyOrderEvent.Invoke(order);
        }

        private void CreateOrderFail(Order order)
        {
            order.State = OrderStateType.Fail;

            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            _rateGate.WaitToProceed();

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

            _pathUrl = "trade/allOrders";

            string body = $"{{\"symbol\":\"{security.Name}\"}}"; 

            string signature = GenerateSignature("DELETE", _pathUrl, timestamp, body, null);

            IRestResponse json = CreatePrivateRequest(signature, _pathUrl, Method.DELETE, timestamp, body, null);

            if (json.StatusCode == HttpStatusCode.OK)
            {
                ResponseMessageRest<object> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseMessageRest<object>());

                if (response.result == "true")
                {
                    SendLogMessage($"Orders canceled", LogMessageType.Trade);
                }
                else
                {
                    SendLogMessage($"Orders cancel error: code - {response.code} | message - {response.message}", LogMessageType.Error);
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

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

            _pathUrl = "trade/order";

            string body = $"{{ \"symbol\":\"{order.SecurityNameCode}\",\"orderId\":{order.NumberMarket}}}";

            string _signature = GenerateSignature("DELETE", _pathUrl, timestamp, body, null);

            IRestResponse json = CreatePrivateRequest(_signature, _pathUrl, Method.DELETE, timestamp, body, null);

            if (json.StatusCode == HttpStatusCode.OK)
            {
                ResponseMessageRest<object> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseMessageRest<object>());

                if (response.result == "true")
                {
                    SendLogMessage($"The order has been cancelled", LogMessageType.Trade);
                }
                else
                {
                    CreateOrderFail(order);
                    SendLogMessage($"Order cancellation error: code - {response.code} | message - {response.message}", LogMessageType.Error);
                }
            }
            else
            {
                SendLogMessage($"Http State Code: {json.StatusCode} - {json.Content}", LogMessageType.Error);
            }

        }

        public void CancelAllOrders()
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

        private RateGate _rateGate = new RateGate(8, TimeSpan.FromMilliseconds(1000)); // https://pionex-doc.gitbook.io/apidocs/restful/general/rate-limit

        private HttpClient _httpPublicClient = new HttpClient();

        private void CreateQueryPortfolio()
        {
            _rateGate.WaitToProceed();

            try
            {
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                _pathUrl = "account/balances";

                string _signature = GenerateSignature("GET", _pathUrl, timestamp, null, null);

                IRestResponse json = CreatePrivateRequest(_signature, _pathUrl, Method.GET, timestamp, null, null);

                ResponseMessageRest<ResponceBalance> responce = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseMessageRest<ResponceBalance>());

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    if (responce.result == "true")
                    {
                      UpdatePortfolioREST( responce.data.balances);
                    }
                    else
                    {
                        SendLogMessage($"Http State Code: {responce.code} - message: {responce.message}", LogMessageType.Error);
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
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

        private void UpdatePortfolioREST(Balance[] balances)
        {
            Portfolio portfolio = new Portfolio();
            portfolio.Number = "PionexSpot";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;
            
            for (int i = 0; i < balances.Length; i++)
            {
                PositionOnBoard newPortf = new PositionOnBoard();
                newPortf.SecurityNameCode = balances[i].coin;
                newPortf.ValueBegin = balances[i].free.ToDecimal();
                newPortf.ValueCurrent = balances[i].free.ToDecimal();
                newPortf.ValueBlocked = balances[i].frozen.ToDecimal();
                newPortf.PortfolioName = "PionexSpot";
                portfolio.SetNewPosition(newPortf);
            }

            PortfolioEvent(new List<Portfolio> { portfolio });
        }

        private List<Candle> CreateQueryCandles(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount, int taskCount)
        {
            _rateGate.WaitToProceed();

            try
            {
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string _pathUrl = "market/klines";
                string interval = string.Empty;
                string limit = candleCount <= 500 ? candleCount.ToString() : "500"; // ограничение 500 свечек

                switch (timeFrameBuilder.TimeFrame)
                {
                    case TimeFrame.Min1:
                        interval = "1M";
                        break;
                    case TimeFrame.Min5:
                        interval = "5M";
                        break;
                    case TimeFrame.Min15:
                        interval = "15M";
                        break;
                    case TimeFrame.Min30:
                        interval = "30M";
                        break;
                    case TimeFrame.Hour1:
                        interval = "60M";
                        break;
                    case TimeFrame.Hour4:
                        interval = "4H";
                        break;
                    default:
                        SendLogMessage("Incorrect timeframe", LogMessageType.Error);
                        return null;
                }

                DateTime EndTime = DateTime.Now.AddSeconds(-10);
                string endTimeMs = new DateTimeOffset(EndTime).ToUnixTimeMilliseconds().ToString();

                SortedDictionary<string, string> parameters = new SortedDictionary<string, string>
                {
                    { "symbol", security.Name },
                    { "interval", interval },
                    { "endTime", endTimeMs },
                    { "limit", limit }
                };

                string _signature = GenerateSignature("GET", _pathUrl, timestamp, null, parameters);

                IRestResponse json = CreatePrivateRequest(_signature, _pathUrl, Method.GET, timestamp, null, parameters);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<ResponceCandles> responce = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseMessageRest<ResponceCandles>());

                    if (responce.result == "true")
                    {
                        List<Candle> candles = new List<Candle>();

                        for (int i = responce.data.klines.Length - 1; i >= 0 ; i--)
                        {
                            Candle newCandle = new Candle();

                            newCandle.Open = responce.data.klines[i].open.ToDecimal();
                            newCandle.Close = responce.data.klines[i].close.ToDecimal();
                            newCandle.High = responce.data.klines[i].high.ToDecimal();
                            newCandle.Low = responce.data.klines[i].low.ToDecimal();
                            newCandle.Volume = responce.data.klines[i].volume.ToDecimal();
                            newCandle.State = CandleState.Finished;
                            newCandle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responce.data.klines[i].time));
                            candles.Add(newCandle);
                        }

                        return candles;
                    }
                    else
                    {
                        if(taskCount >= 3)
                        {
                            SendLogMessage($"CreateQueryCandles Error: {responce.code} - message: {responce.message}", LogMessageType.Error);
                            return null;
                        }
                        else
                        {
                            taskCount++;
                            return CreateQueryCandles(security, timeFrameBuilder, candleCount, taskCount);
                        }
                    }
                }
                else
                {
                    if (taskCount >= 3)
                    {
                        SendLogMessage($"Http State Code: {json.StatusCode}", LogMessageType.Error);
                        return null;
                    }
                    else
                    {
                        taskCount++;
                        return CreateQueryCandles(security, timeFrameBuilder, candleCount, taskCount);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return null;
        }
        
        private string GenerateSignature(string method, string path, string timestamp, string body, SortedDictionary<string, string> param)
        {
            method = method.ToUpper();

            path = string.IsNullOrEmpty(path) ? string.Empty : path + "?";

            body = string.IsNullOrEmpty(body) ? string.Empty : body;

            string preHash = string.Empty;

            if (method == "GET")
            {
                preHash = method + _prefix + path + BuildParams(param) + "timestamp=" + timestamp;
            }
            if (method == "POST" || method == "DELETE")
            {
                preHash = method + _prefix + path + "timestamp=" + timestamp + body;
            }

            return SHA256HexHashString(_secretKey, preHash);
        }
       
        private string SHA256HexHashString(string key, string message)
        {
            string hashString;

            using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
            {
                byte[] b = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));

                hashString = ToHex(b, false);
            }

            return hashString;
        }
        
        private string ToHex(byte[] bytes, bool upperCase)
        {
            StringBuilder result = new StringBuilder(bytes.Length * 2);

            for (int i = 0; i < bytes.Length; i++)
            {
                result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));
            }

            return result.ToString();
        }
        
        public string BuildParams(SortedDictionary<string, string> _params)
        {
            if (_params == null)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();

            SortedDictionary<string, string>.Enumerator enumerator = _params.GetEnumerator();

            while (enumerator.MoveNext())
            {
                string key = enumerator.Current.Key;
                string value = enumerator.Current.Value;

                sb.Append('&');
                sb.Append(key).Append('=').Append(value);
            }
            return sb.ToString().Substring(1) + "&";
        }
        
        private IRestResponse CreatePrivateRequest(string signature, string pathUrl, Method method, string timestamp, string body, SortedDictionary<string, string> _params)
        {
            RestClient client = new RestClient(_baseUrl);

            RestRequest request = new RestRequest(_prefix + pathUrl, method);

            request.AddHeader("PIONEX-KEY", _publicKey);
            request.AddHeader("PIONEX-SIGNATURE", signature);
            request.AddQueryParameter("timestamp", timestamp);

            if (_params != null && body == null)
            {
                SortedDictionary<string, string>.Enumerator enumerator = _params.GetEnumerator();

                while (enumerator.MoveNext())
                {
                    string key = enumerator.Current.Key;
                    string value = enumerator.Current.Value;
                    request.AddQueryParameter(key, value);
                }
            }

            if (method == Method.POST || method == Method.DELETE)
            {
                request.AddHeader("Content-Type", "application/json");
            }
            if (body != null)
            {
                request.AddParameter("application/json", body, ParameterType.RequestBody);
            }

            return client.Execute(request);
        }

        #endregion

        #region 12 Log

        public event Action<string, LogMessageType> LogMessageEvent;

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);
        }

        #endregion
    }
}