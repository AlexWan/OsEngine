/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Entity.WebSocketOsEngine;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.Pionex.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace OsEngine.Market.Servers.Pionex
{
    public class PionexServerSpot : AServer
    {
        public PionexServerSpot()
        {
            PionexServerRealization realization = new PionexServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
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

        public DateTime ServerTime { get; set; }

        public void Connect(WebProxy proxy)
        {
            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

            HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(_baseUrl + _prefix + "common/symbols?symbol=BTC_USDT").Result;

            if (responseMessage.StatusCode == HttpStatusCode.OK)
            {
                CreatePublicWebSocketConnect();
                CreatePrivateWebSocketConnect();
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
                DeleteWebSocketConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            _subscribedSecurities.Clear();
            _decimalsVolume.Clear();

            FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
            FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

            Disconnect();
        }

        public void Disconnect()
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public ServerType ServerType
        {
            get { return ServerType.PionexSpot; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

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
            return CreateQueryCandles(security, timeFrameBuilder, candleCount, 1);
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

        private List<WebSocket> _webSocketPublic = new List<WebSocket>();

        private WebSocket _webSocketPrivate;

        private string _webSocketPublicUrl = "wss://ws.pionex.com/wsPub";

        private string _webSocketPrivateUrl = "wss://ws.pionex.com/ws";

        private void CreatePublicWebSocketConnect()
        {
            try
            {
                if (FIFOListWebSocketPublicMessage == null)
                {
                    FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
                }

                _webSocketPublic.Add(CreateNewPublicSocket());
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private WebSocket CreateNewPublicSocket()
        {
            try
            {
                WebSocket webSocketPublicNew = new WebSocket(_webSocketPublicUrl);

                //if (_myProxy != null)
                //{
                //    webSocketPublicNew.SetProxy(_myProxy);
                //}

                webSocketPublicNew.EmitOnPing = true;
                webSocketPublicNew.OnOpen += WebSocketPublicNew_OnOpen;
                webSocketPublicNew.OnMessage += WebSocketPublicNew_OnMessage;
                webSocketPublicNew.OnError += WebSocketPublicNew_OnError;
                webSocketPublicNew.OnClose += WebSocketPublicNew_OnClose;
                webSocketPublicNew.Connect();

                return webSocketPublicNew;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private void CreatePrivateWebSocketConnect()
        {
            try
            {
                if (_webSocketPrivate != null)
                {
                    return;
                }

                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                string preHash = "/ws?key=" + _publicKey + "&timestamp=" + timestamp + "websocket_auth";

                string signature = SHA256HexHashString(_secretKey, preHash);

                string privateURL = $"{_webSocketPrivateUrl}?key={_publicKey}&timestamp={timestamp}&signature={signature}";

                _webSocketPrivate = new WebSocket(privateURL);

                //if (_myProxy != null)
                //{
                //    _webSocketPrivate.SetProxy(_myProxy);
                //}

                _webSocketPrivate.EmitOnPing = true;
                _webSocketPrivate.OnOpen += _webSocketPrivate_OnOpen;
                _webSocketPrivate.OnClose += _webSocketPrivate_OnClose;
                _webSocketPrivate.OnMessage += _webSocketPrivate_OnMessage;
                _webSocketPrivate.OnError += _webSocketPrivate_OnError;
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
                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        webSocketPublic.OnOpen -= WebSocketPublicNew_OnOpen;
                        webSocketPublic.OnClose -= WebSocketPublicNew_OnClose;
                        webSocketPublic.OnMessage -= WebSocketPublicNew_OnMessage;
                        webSocketPublic.OnError -= WebSocketPublicNew_OnError;

                        if (webSocketPublic.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublic.CloseAsync();
                        }

                        webSocketPublic = null;
                    }
                }
                catch
                {
                    // ignore
                }

                _webSocketPublic.Clear();
            }

            if (_webSocketPrivate != null)
            {
                try
                {
                    _webSocketPrivate.OnOpen -= _webSocketPrivate_OnOpen;
                    _webSocketPrivate.OnClose -= _webSocketPrivate_OnClose;
                    _webSocketPrivate.OnMessage -= _webSocketPrivate_OnMessage;
                    _webSocketPrivate.OnError -= _webSocketPrivate_OnError;
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

                if (_webSocketPrivate == null
                    || _webSocketPrivate.ReadyState != WebSocketState.Open)
                {
                    Disconnect();
                    return;
                }

                if (_webSocketPublic.Count == 0)
                {
                    Disconnect();
                    return;
                }

                WebSocket webSocketPublic = _webSocketPublic[0];

                if (webSocketPublic == null
                    || webSocketPublic?.ReadyState != WebSocketState.Open)
                {
                    Disconnect();
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Connect;

                    if (ConnectEvent != null)
                    {
                        ConnectEvent();
                    }
                }
            }
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocketPublicNew_OnClose(object arg1, CloseEventArgs e)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    string message = this.GetType().Name + OsLocalization.Market.Message101 + "\n";
                    message += OsLocalization.Market.Message102;

                    SendLogMessage(message, LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublicNew_OnError(object arg1, ErrorEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e.Exception != null)
                {
                    string message = e.Exception.ToString();

                    if (message.Contains("The remote party closed the WebSocket connection"))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Data socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublicNew_OnMessage(object arg1, MessageEventArgs e)
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

                if (FIFOListWebSocketPublicMessage == null)
                {
                    return;
                }

                if (e.IsBinary)
                {
                    FIFOListWebSocketPublicMessage.Enqueue(Encoding.UTF8.GetString(e.RawData));
                }

                if (e.IsText)
                {
                    FIFOListWebSocketPublicMessage.Enqueue(e.Data);
                }

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublicNew_OnOpen(object arg1, EventArgs arg2)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    CheckSocketsActivate();
                    SendLogMessage("PionexSpot WebSocket Public connection open", LogMessageType.System);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnError(object arg1, ErrorEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e.Exception != null)
                {
                    string message = e.Exception.ToString();

                    if (message.Contains("The remote party closed the WebSocket connection"))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnMessage(object arg1, MessageEventArgs e)
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

                if (FIFOListWebSocketPrivateMessage == null)
                {
                    return;
                }

                if (e.IsBinary)
                {
                    FIFOListWebSocketPrivateMessage.Enqueue(Encoding.UTF8.GetString(e.RawData));
                }

                if (e.IsText)
                {
                    FIFOListWebSocketPrivateMessage.Enqueue(e.Data);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnClose(object arg1, CloseEventArgs arg2)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    string message = this.GetType().Name + OsLocalization.Market.Message101 + "\n";
                    message += OsLocalization.Market.Message102;

                    SendLogMessage(message, LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnOpen(object arg1, EventArgs arg2)
        {
            try
            {
                CheckSocketsActivate();
                SendLogMessage("BitMartSpot WebSocket Private connection open", LogMessageType.System);

                _webSocketPrivate.Send("{\"op\": \"SUBSCRIBE\", \"topic\": \"BALANCE\"}");
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 Security Subscribed

        private List<string> _subscribedSecurities = new List<string>();

        private RateGate _rateGateSubscribed = new RateGate(1, TimeSpan.FromMilliseconds(500));

        public void Subscribe(Security security)
        {
            try
            {
                _rateGateSubscribed.WaitToProceed();

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

                if (_webSocketPublic.Count == 0)
                {
                    return;
                }

                WebSocket webSocketPublic = _webSocketPublic[_webSocketPublic.Count - 1];

                if (webSocketPublic.ReadyState == WebSocketState.Open
                    && _subscribedSecurities.Count != 0
                    && _subscribedSecurities.Count % 50 == 0)
                {
                    // creating a new socket
                    WebSocket newSocket = CreateNewPublicSocket();

                    DateTime timeEnd = DateTime.Now.AddSeconds(10);

                    while (newSocket.ReadyState != WebSocketState.Open)
                    {
                        Thread.Sleep(1000);

                        if (timeEnd < DateTime.Now)
                        {
                            break;
                        }
                    }

                    if (newSocket.ReadyState == WebSocketState.Open)
                    {
                        _webSocketPublic.Add(newSocket);
                        webSocketPublic = newSocket;
                    }
                }

                if (webSocketPublic != null)
                {
                    webSocketPublic.Send($"{{\"op\": \"SUBSCRIBE\", \"topic\": \"TRADE\", \"symbol\": \"{security.Name}\"}}");
                    webSocketPublic.Send($"{{\"op\": \"SUBSCRIBE\",  \"topic\":  \"DEPTH\",  \"symbol\": \"{security.Name}\", \"limit\":  10 }}");
                }

                if (_webSocketPrivate != null)
                {
                    _webSocketPrivate.Send($"{{\"op\": \"SUBSCRIBE\", \"topic\": \"ORDER\", \"symbol\": \"{security.Name}\"}}");
                    _webSocketPrivate.Send($"{{\"op\": \"SUBSCRIBE\",  \"topic\":  \"FILL\",  \"symbol\": \"{security.Name}\"}}");
                }

                // собираем знаки после запятой в объеме инструмента, для корректного отображения в объеме позиции
                _decimalsVolume.Add(security.Name, security.DecimalsVolume);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UnsubscribeFromAllWebSockets()
        {
            try
            {
                if (_webSocketPublic.Count != 0
                    && _webSocketPublic != null)
                {
                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        try
                        {
                            if (webSocketPublic != null && webSocketPublic?.ReadyState == WebSocketState.Open)
                            {
                                if (_subscribedSecurities != null)
                                {
                                    for (int j = 0; j < _subscribedSecurities.Count; j++)
                                    {
                                        string securityName = _subscribedSecurities[j];

                                        webSocketPublic.Send($"{{\"op\": \"UNSUBSCRIBED\", \"topic\": \"TRADE\", \"symbol\": {securityName}\"}}");
                                        webSocketPublic.Send($"{{\"op\": \"UNSUBSCRIBED\",  \"topic\":  \"DEPTH\",  \"symbol\": {securityName}\", \"limit\":  10 }}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }

            if (_webSocketPrivate != null
                && _webSocketPrivate.ReadyState == WebSocketState.Open)
            {
                try
                {
                    if (_subscribedSecurities != null)
                    {
                        for (int j = 0; j < _subscribedSecurities.Count; j++)
                        {
                            string securityName = _subscribedSecurities[j];

                            _webSocketPrivate.Send($"{{\"op\": \"UNSUBSCRIBE\", \"topic\": \"ORDER\", \"symbol\": \"{securityName}\"}}"); // myorders
                            _webSocketPrivate.Send($"{{\"op\": \"UNSUBSCRIBE\",  \"topic\":  \"FILL\",  \"symbol\": \"{securityName}\"}}"); // mytrades
                        }
                    }

                    _webSocketPrivate.Send("{\"op\": \"UNSUBSCRIBE\", \"topic\": \"BALANCE\"}");
                }
                catch
                {
                    // ignore
                }
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent;

        #endregion

        #region 9 WebSocket parsing the messages

        private ConcurrentQueue<string> FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        private Dictionary<string, int> _decimalsVolume = new Dictionary<string, int>();

        private void PublicMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    if (FIFOListWebSocketPublicMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    FIFOListWebSocketPublicMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Contains("PING"))
                    {
                        for (int i = 0; i < _webSocketPublic.Count; i++)
                        {
                            WebSocket webSocketPublic = _webSocketPublic[i];

                            if (webSocketPublic != null
                                && webSocketPublic?.ReadyState == WebSocketState.Open)
                            {
                                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                                string pong = $"{{\"op\": \"PONG\", \"timestamp\": {timeStamp}}}";
                                webSocketPublic.Send(pong);
                            }
                            else
                            {
                                Disconnect();
                            }
                        }

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
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    if (FIFOListWebSocketPrivateMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    FIFOListWebSocketPrivateMessage.TryDequeue(out message);

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

            if (marketDepth.Asks.Count == 0 ||
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

            newTrade.Volume = GetVolumeForMyTrade(item.symbol, preVolume);

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

            if (item.type.Equals("MARKET") && stateType == OrderStateType.Active)
            {
                return;
            }

            Order newOrder = new Order();

            newOrder.SecurityNameCode = item.symbol;
            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createTime));

            if (string.IsNullOrEmpty(item.clientOrderId))
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
                stateType = OrderStateType.Active;
            }
            else if (status == "OPEN" && filledSize != "0")
            {
                stateType = OrderStateType.Partial;
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

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;

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
                    order.State = OrderStateType.Active;
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

        public bool CancelOrder(Order order)
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
                    return true;
                }
                else
                {
                    CreateOrderFail(order);
                    SendLogMessage($"Order cancellation error: code - {response.code} | message - {response.message}", LogMessageType.Error);
                    return false;
                }
            }
            else
            {
                SendLogMessage($"Http State Code: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                return false;
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

        public OrderStateType GetOrderStatus(Order order)
        {
            return OrderStateType.None;
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
                        UpdatePortfolioREST(responce.data.balances);
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

                        for (int i = responce.data.klines.Length - 1; i >= 0; i--)
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
                        if (taskCount >= 3)
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

        public event Action<Funding> FundingUpdateEvent;

        public event Action<SecurityVolumes> Volume24hUpdateEvent;

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);
        }

        #endregion
    }
}