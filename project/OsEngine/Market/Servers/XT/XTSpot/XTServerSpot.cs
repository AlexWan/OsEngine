using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.XT.XTSpot.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using WebSocket4Net;

namespace OsEngine.Market.Servers.XT.XTSpot
{
    public class XTServerSpot : AServer
    {
        public XTServerSpot()
        {
            XTServerSpotRealization realization = new XTServerSpotRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
        }

        public class XTServerSpotRealization : IServerRealization
        {
            #region 1 Constructor, Status, Connection

            public XTServerSpotRealization()
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                
                Thread threadCheckAlive = new Thread(ConnectionCheck);
                threadCheckAlive.IsBackground = true;
                threadCheckAlive.Name = "CheckAliveXT";
                threadCheckAlive.Start();
                
                Thread threadForPublicMessages = new Thread(PublicMessageReader);
                threadForPublicMessages.IsBackground = true;
                threadForPublicMessages.Name = "PublicMessageReaderXT";
                threadForPublicMessages.Start();

                Thread threadForTradesMessages = new Thread(PublicTradesMessageReader);
                threadForTradesMessages.IsBackground = true;
                threadForTradesMessages.Name = "PublicTradesMessageReaderXT";
                threadForTradesMessages.Start();

                Thread threadForPrivateMessages = new Thread(PrivateMessageReader);
                threadForPrivateMessages.IsBackground = true;
                threadForPrivateMessages.Name = "PrivateMessageReaderXT";
                threadForPrivateMessages.Start();

                Thread threadForGetPortfolios = new Thread(UpdatePortfolios);
                threadForGetPortfolios.IsBackground = true;
                threadForGetPortfolios.Name = "UpdatePortfoliosXT";
                threadForGetPortfolios.Start();
            }

            public DateTime ServerTime { get; set; }

            public void Connect()
            {
                _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
                _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
                
                HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(_baseUrl + "/v4/public/time").Result;
                string json = responseMessage.Content.ReadAsStringAsync().Result;
                
                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    try
                    {
                        _listenKey = GetListenKey();
                        if (string.IsNullOrEmpty(_listenKey))
                        {
                            SendLogMessage("Check the Public and Private Key!", LogMessageType.Error);
                            ServerStatus = ServerConnectStatus.Disconnect;

                            DisconnectEvent?.Invoke();
                            return;
                        }

                        _webSocketPublicMessages = new ConcurrentQueue<string>();
                        _webSocketPrivateMessages = new ConcurrentQueue<string>();
                        _webSocketPublicTradesMessages = new ConcurrentQueue<string>();
                        
                        CreateWebSocketConnection();
                        GetPortfolios();
                    }
                    catch (Exception exception)
                    {
                        SendLogMessage(exception.ToString(), LogMessageType.Error);
                        SendLogMessage("Connection cannot be open. XT. Error request", LogMessageType.Error);
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent?.Invoke();
                    }
                }
                else
                {
                    SendLogMessage("Connection cannot be open. XT. Error request", LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent?.Invoke();
                }
            }

            public void Dispose()
            {
                try
                {
                    UnsubscribeFromAllWebSockets();
                    _subscribedSecurities.Clear();
                    DeleteWebsocketConnection();
                    _bufferDepthSecurity.Clear();
                    _depthSecurity.Clear();
                }
                catch (Exception exception)
                {
                    SendLogMessage("Dispose error" + exception.ToString(), LogMessageType.Error);
                }
                finally
                {
                    _webSocketPublicMessages = new ConcurrentQueue<string>();
                    _webSocketPrivateMessages = new ConcurrentQueue<string>();
                    _webSocketPublicTradesMessages = new ConcurrentQueue<string>();

                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent?.Invoke();
                    }
                }
            }

            private void UnsubscribeFromAllWebSockets()
            {
                if (_webSocketPublic == null || _webSocketPrivate == null || _webSocketPublicTrades == null)
                    return;
                if(ServerStatus != ServerConnectStatus.Connect)
                    return;
                
                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    string securityName = _subscribedSecurities[i];

                    _webSocketPublic.Send($"{{\"method\": \"unsubscribe\", \"params\": [\"depth-update@{securityName}\",\"depth@{securityName},{20}\"], \"id\": \"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");
                    _webSocketPublicTrades.Send($"{{\"method\": \"unsubscribe\", \"params\": [\"trades@{securityName}\"], \"id\": \"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");
                }
                
                _webSocketPrivate.Send($"{{\"method\": \"unsubscribe\", \"params\": [\"balance\",\"order\"], \"id\": \"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");
            }

            public ServerType ServerType
            {
                get { return ServerType.XTSpot; }
            }

            public ServerConnectStatus ServerStatus { get; set; }

            public event Action ConnectEvent;

            public event Action DisconnectEvent;

            #endregion

            #region 2 Properties

            public List<IServerParameter> ServerParameters { get; set; }
            private string _publicKey;
            private string _secretKey;
            private string _listenKey; // lifetime <= 30 days
            private Dictionary<string, List<ResponseWebSocketDepthIncremental>> _bufferDepthSecurity = new Dictionary<string, List<ResponseWebSocketDepthIncremental>>();
            private Dictionary<string, ResponseWebSocketDepth> _depthSecurity = new Dictionary<string, ResponseWebSocketDepth>();
            #endregion

            #region 3 Securities

            public void GetSecurities()
            {
                try
                {
                    HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(_baseUrl + "/v4/public/symbol").Result;
                    string json = responseMessage.Content.ReadAsStringAsync().Result;

                    ResponseMessageRest<object> stateResponse =
                        JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<object>());

                    if (responseMessage.StatusCode == HttpStatusCode.OK && stateResponse != null)
                    {
                        if (stateResponse.rc.Equals("0") && stateResponse.mc.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                        {
                            UpdateSecurity(json);
                        }
                        else
                        {
                            SendLogMessage($"GetSecurities return code: {stateResponse.rc}\n"
                                           + $"Message Code: {stateResponse.mc}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage($"GetSecurities> Http State Code: {responseMessage.StatusCode}",
                            LogMessageType.Error);

                        if (stateResponse != null && stateResponse.rc != null)
                        {
                            SendLogMessage($"Return Code: {stateResponse.rc}\n"
                                           + $"Message Code: {stateResponse.mc}", LogMessageType.Error);
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage("GetSecurities error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            private void UpdateSecurity(string json)
            {
                ResponseMessageRest<ResponseSymbols> symbols =
                    JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<ResponseSymbols>());

                List<Security> securities = new List<Security>();

                for (int i = 0; i < symbols.result.symbols.Count; i++)
                {
                    ResponseSymbol item = symbols.result.symbols[i];

                    if (!item.openapiEnabled.Equals("true", StringComparison.OrdinalIgnoreCase)
                        || !item.tradingEnabled.Equals("true", StringComparison.OrdinalIgnoreCase)
                        || !item.state.Equals("ONLINE", StringComparison.OrdinalIgnoreCase)) 
                        
                        continue;
                    
                    Security newSecurity = new Security();

                    newSecurity.Exchange = ServerType.XTSpot.ToString();
                    newSecurity.Lot = 1;
                    newSecurity.Name = item.symbol;
                    newSecurity.NameFull = item.displayName;
                    newSecurity.NameClass = item.quoteCurrency;
                    newSecurity.NameId = item.id;
                    newSecurity.SecurityType = SecurityType.CurrencyPair;

                    if (string.IsNullOrEmpty(item.pricePrecision) == false)
                    {
                        newSecurity.Decimals = Convert.ToInt32(item.pricePrecision);
                    }

                    if (string.IsNullOrEmpty(item.quoteCurrencyPrecision) == false)
                    {
                        newSecurity.DecimalsVolume = Convert.ToInt32(item.quoteCurrencyPrecision);
                    }

                    newSecurity.PriceStep = Convert.ToInt32(item.pricePrecision).GetValueByDecimals();
                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                    newSecurity.State = SecurityStateType.Activ;
                    securities.Add(newSecurity);
                }
                
                SecurityEvent?.Invoke(securities);
            }

            public event Action<List<Security>> SecurityEvent;

            #endregion

            #region 4 Portfolios

            public void GetPortfolios()
            {
                CreateQueryPortfolio(true);
            }

            public event Action<List<Portfolio>> PortfolioEvent;

            private void UpdatePortfolios()
            {
                while (true)
                {
                    try
                    {
                        if(ServerStatus == ServerConnectStatus.Disconnect)
                        {
                            Thread.Sleep(2000);
                            continue;
                        }

                        Thread.Sleep(3000);

                        CreateQueryPortfolio(true);
                    }
                    catch(Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                    }
                }
            }

            #endregion

            #region 5 Data

            private readonly RateGate _rateGateCandleHistory = new RateGate(700, TimeSpan.FromSeconds(20));

            public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
            {
                return null;
            }

            public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
            {
                DateTime timeStart = DateTime.UtcNow - TimeSpan.FromMinutes(timeFrameBuilder.TimeFrameTimeSpan.Minutes * candleCount);
                DateTime timeEnd = DateTime.UtcNow;

                return GetCandleDataToSecurity(security, timeFrameBuilder, timeStart, timeEnd, timeStart);
            }

            private List<Candle> GetCandleHistory(string nameSec, TimeSpan tf, bool isOsData, int CountToLoad, DateTime timeEnd)
            {
                int needToLoadCandles = CountToLoad;

                List<Candle> candles = new List<Candle>();

                DateTime fromTime = timeEnd - TimeSpan.FromMinutes(tf.TotalMinutes * CountToLoad);

                do
                {
                    int limit = needToLoadCandles;
                    
                    if (needToLoadCandles > 1000) // limit by XT
                    {
                        limit = 1000;
                    }

                    List<Candle> rangeCandles;

                    rangeCandles = CreateQueryCandles(nameSec, GetStringInterval(tf), fromTime, timeEnd);

                   if (rangeCandles == null)
                        return null;

                    rangeCandles.Reverse();

                    candles.InsertRange(0, rangeCandles);

                    if (candles.Count != 0)
                    {
                        timeEnd = candles[0].TimeStart;
                    }

                    needToLoadCandles -= limit;

                } 
                while (needToLoadCandles > 0);

                return candles;
            }

            public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
            {
                if (startTime != actualTime)
                {
                    startTime = actualTime;
                }

                int countNeedToLoad = GetCountCandlesFromSliceTime(startTime, endTime, timeFrameBuilder.TimeFrameTimeSpan);
                
                return GetCandleHistory(security.Name, timeFrameBuilder.TimeFrameTimeSpan, true, countNeedToLoad, endTime);
            }

            private int GetCountCandlesFromSliceTime(DateTime startTime, DateTime endTime, TimeSpan tf)
            {
                TimeSpan timeSlice = endTime - startTime;
                
                if (tf.Hours != 0)
                {
                    return (int)timeSlice.TotalHours / (int)tf.TotalHours;
                }
                else if(tf.Days != 0)
                {
                    return (int)timeSlice.TotalDays;
                }
                else
                {
                    return (int)timeSlice.TotalMinutes / tf.Minutes;
                }
            }

            private string GetStringInterval(TimeSpan tf)
            {
                // Type of candlestick patterns: 1m, 3m, 5m, 15m, 30m, 1h, 2h, 4h, 6h, 8h, 12h, 1d, 1w
                if (tf.Minutes != 0)
                {
                    return $"{tf.Minutes}m";
                }
                else if (tf.Days !=0)
                {
                    return $"1d";
                }
                else
                {
                    return $"{tf.Hours}h";
                }
            }

            #endregion

            #region 6 WecSocket creation

            private WebSocket _webSocketPrivate;
            private WebSocket _webSocketPublic;
            private WebSocket _webSocketPublicTrades;

            private readonly string _webSocketPrivateUrl = "wss://stream.xt.com/private";
            private readonly string _webSocketPublicUrl = "wss://stream.xt.com/public";

            private readonly string _socketLocker = "webSocketLockerXT";

            private void CreateWebSocketConnection()
            {
                try
                {
                    _webSocketPrivate = new WebSocket(_webSocketPrivateUrl);
                    _webSocketPrivate.EnableAutoSendPing = true;
                    _webSocketPrivate.AutoSendPingInterval = 20;

                    _webSocketPrivate.Opened += WebSocketPrivate_Opened;
                    _webSocketPrivate.Closed += WebSocketPrivate_Closed;
                    _webSocketPrivate.MessageReceived += WebSocketPrivate_MessageReceived;
                    _webSocketPrivate.Error += WebSocketPrivate_Error;
                    _webSocketPrivate.Open();

                    _webSocketPublic = new WebSocket(_webSocketPublicUrl);
                    _webSocketPublic.EnableAutoSendPing = true;
                    _webSocketPublic.AutoSendPingInterval = 20;

                    _webSocketPublic.Opened += WebSocketPublic_Opened;
                    _webSocketPublic.Closed += WebSocketPublic_Closed;
                    _webSocketPublic.MessageReceived += WebSocketPublic_MessageReceived;
                    _webSocketPublic.Error += WebSocketPublic_Error;
                    _webSocketPublic.Open();

                    _webSocketPublicTrades = new WebSocket(_webSocketPublicUrl);
                    _webSocketPublicTrades.EnableAutoSendPing = true;
                    _webSocketPublicTrades.AutoSendPingInterval = 20;

                    _webSocketPublicTrades.Opened += WebSocketPublicTrades_Opened;
                    _webSocketPublicTrades.Closed += WebSocketPublicTrades_Closed;
                    _webSocketPublicTrades.MessageReceived += WebSocketPublicTrades_MessageReceived;
                    _webSocketPublicTrades.Error += WebSocketPublicTrades_Error;
                    _webSocketPublicTrades.Open();
                }
                catch
                {
                    SendLogMessage("Create WebSocket Connection error.", LogMessageType.Error);
                }
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
                    _webSocketPublic.MessageReceived -= WebSocketPublic_MessageReceived;
                    _webSocketPublic.Error -= WebSocketPublic_Error;
                    _webSocketPublic = null;
                }

                if (_webSocketPublicTrades != null)
                {
                    try
                    {
                        _webSocketPublicTrades.Close();
                    }
                    catch
                    {
                        // ignore
                    }

                    _webSocketPublicTrades.Opened -= WebSocketPublicTrades_Opened;
                    _webSocketPublicTrades.Closed -= WebSocketPublicTrades_Closed;
                    _webSocketPublicTrades.MessageReceived -= WebSocketPublicTrades_MessageReceived;
                    _webSocketPublicTrades.Error -= WebSocketPublicTrades_Error;
                    _webSocketPublicTrades = null;
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
                    _webSocketPrivate.MessageReceived -= WebSocketPrivate_MessageReceived;
                    _webSocketPrivate.Error -= WebSocketPrivate_Error;
                    _webSocketPrivate = null;
                }
            }

            #endregion

            #region 7 WebSocket events

            private void WebSocketPublic_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
            {
                SuperSocket.ClientEngine.ErrorEventArgs error = (SuperSocket.ClientEngine.ErrorEventArgs)e;
                
                if (error.Exception != null)
                {
                    SendLogMessage("WebSocketPublic Error: " + error.Exception.ToString(), LogMessageType.Error);
                }
            }

            private void WebSocketPublicTrades_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
            {
                SuperSocket.ClientEngine.ErrorEventArgs error = (SuperSocket.ClientEngine.ErrorEventArgs)e;
                
                if (error.Exception != null)
                {
                    SendLogMessage("WebSocketPublicTrades Error: " + error.Exception.ToString(), LogMessageType.Error);
                }
            }

            private void WebSocketPrivate_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
            {
                SuperSocket.ClientEngine.ErrorEventArgs error = (SuperSocket.ClientEngine.ErrorEventArgs)e;
                if (error.Exception != null)
                {
                    SendLogMessage("WebSocketPrivate Error" + error.Exception.ToString(), LogMessageType.Error);
                }
            }

            private void WebSocketPublic_MessageReceived(object sender, MessageReceivedEventArgs e)
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
                    {
                        // pong message
                        return;
                    }

                    if (_webSocketPublicMessages == null)
                    {
                        return;
                    }

                    _webSocketPublicMessages.Enqueue(e.Message);
                }
                catch (Exception error)
                {
                    SendLogMessage("WebSocketPublic Message Received error: " + error.ToString(), LogMessageType.Error);
                }
            }

            private void WebSocketPublicTrades_MessageReceived(object sender, MessageReceivedEventArgs e)
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
                    {
                        // pong message
                        return;
                    }

                    if (_webSocketPublicMessages == null)
                    {
                        return;
                    }

                    _webSocketPublicTradesMessages.Enqueue(e.Message);
                }
                catch (Exception error)
                {
                    SendLogMessage("WebSocketPublicTrades Message Received error: " + error.ToString(), LogMessageType.Error);
                }
            }

            private void WebSocketPrivate_MessageReceived(object sender, MessageReceivedEventArgs e)
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
                    {
                        // pong message
                        return;
                    }

                    if (_webSocketPrivateMessages == null)
                    {
                        return;
                    }

                    _webSocketPrivateMessages.Enqueue(e.Message);
                }
                catch (Exception error)
                {
                    SendLogMessage("WebSocketPrivate Message Received error: " + error.ToString(), LogMessageType.Error);
                }
            }

            private void WebSocketPublic_Closed(object sender, EventArgs e)
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("WebSocketPublic Connection Closed by XT. WebSocket Closed Event", LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent?.Invoke();
                }
            }

            private void WebSocketPublicTrades_Closed(object sender, EventArgs e)
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("WebSocketPublicTrades Connection Closed by XT. WebSocket Closed Event", LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent?.Invoke();
                }
            }

            private void WebSocketPrivate_Closed(object sender, EventArgs e)
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("WebSocketPrivate Connection Closed by XT. WebSocket Closed Event", LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent?.Invoke();
                }
            }

            private void WebSocketPublic_Opened(object sender, EventArgs e)
            {
                SendLogMessage("WebSocketPublic Connection to public data is Open", LogMessageType.System);

                if (ServerStatus != ServerConnectStatus.Connect 
                    && _webSocketPrivate.State == WebSocketState.Open
                    && _webSocketPublicTrades.State == WebSocketState.Open)
                {
                    ServerStatus = ServerConnectStatus.Connect;
                    ConnectEvent?.Invoke();
                }
            }

            private void WebSocketPublicTrades_Opened(object sender, EventArgs e)
            {
                SendLogMessage("WebSocketPublicTrades Connection to public trades is Open", LogMessageType.System);

                if (ServerStatus != ServerConnectStatus.Connect 
                    && _webSocketPrivate.State == WebSocketState.Open
                    && _webSocketPublic.State == WebSocketState.Open)
                {
                    ServerStatus = ServerConnectStatus.Connect;
                    ConnectEvent?.Invoke();
                }
            }

            private void WebSocketPrivate_Opened(object sender, EventArgs e)
            {
                SendLogMessage("WebSocketPrivate Connection to private data is Open", LogMessageType.System);

                if (ServerStatus != ServerConnectStatus.Connect
                    && _webSocketPublic.State == WebSocketState.Open
                    && _webSocketPublicTrades.State == WebSocketState.Open)
                {
                    ServerStatus = ServerConnectStatus.Connect;
                    ConnectEvent?.Invoke();
                }

                // sign up for order and portfolio changes
                _webSocketPrivate.Send($"{{\"method\":\"subscribe\",\"params\":[\"order\",\"balance\"],\"listenKey\":\"{_listenKey}\",\"id\":\"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}"); // change orders
            }

            #endregion

            #region 8 WebSocket check alive
            
            private void ConnectionCheck()
            {
                while (true)
                {
                    try
                    {
                        if(ServerStatus == ServerConnectStatus.Disconnect)
                        {
                            Thread.Sleep(2000);
                            continue;
                        }

                        Thread.Sleep(15000);
                        if(_webSocketPublic == null)
                            continue;
                        
                        if(_webSocketPrivate == null)
                            continue;
                        
                        if(_webSocketPublicTrades == null)
                            continue;

                        if (_webSocketPublic.State == WebSocketState.Open 
                            && _webSocketPublicTrades.State == WebSocketState.Open 
                            && _webSocketPrivate.State == WebSocketState.Open)
                        {
                            _webSocketPublic.Send("ping");
                            _webSocketPrivate.Send("ping");
                            _webSocketPublicTrades.Send("ping");
                        }
                        else
                        {
                            Dispose();
                        }
                    }
                    catch(Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                    }
                }
            }

            #endregion

            #region 9 Security Subscribed

            private readonly RateGate _rateGateSubscribed = new RateGate(1, TimeSpan.FromMilliseconds(200));

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

            #endregion

            #region 10 WebSocket parsing the messages

            private ConcurrentQueue<string> _webSocketPublicMessages = new ConcurrentQueue<string>();

            private ConcurrentQueue<string> _webSocketPrivateMessages = new ConcurrentQueue<string>();

            private ConcurrentQueue<string> _webSocketPublicTradesMessages = new ConcurrentQueue<string>();

            private void PublicMessageReader()
            {
                Thread.Sleep(1000);

                while (true)
                {
                    try
                    {
                        if(ServerStatus != ServerConnectStatus.Connect)
                        {
                            Thread.Sleep(1000);
                            continue;
                        }

                        if (_webSocketPublicMessages.IsEmpty)
                        {
                            Thread.Sleep(1);
                            continue;
                        }

                        string message;

                        _webSocketPublicMessages.TryDequeue(out message);

                        if (message == null)
                        {
                            continue;
                        }

                        if (message.Equals("pong",StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        ResponseWebSocketMessageAction<object> action = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<object>());

                        if (action != null && action.Topic != null)
                        {
                            if (action.Topic.Equals("depth_update",StringComparison.OrdinalIgnoreCase))
                            {
                                UpdateDepth(message);
                            }
                            else if (action.Topic.Equals("depth", StringComparison.OrdinalIgnoreCase))
                            {
                                SaveDepth(message);
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        Thread.Sleep(2000);
                        SendLogMessage("PublicMessageReader error: " + exception.ToString(), LogMessageType.Error);
                    }
                }
            }

            private void PublicTradesMessageReader()
            {
                Thread.Sleep(1000);

                while (true)
                {
                    try
                    {
                        if(ServerStatus != ServerConnectStatus.Connect)
                        {
                            Thread.Sleep(1000);
                            continue;
                        }

                        if (_webSocketPublicTradesMessages.IsEmpty)
                        {
                            Thread.Sleep(1);
                            continue;
                        }

                        _webSocketPublicTradesMessages.TryDequeue(out var message);

                        if (message == null)
                        {
                            continue;
                        }

                        if (message.Equals("pong",StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        ResponseWebSocketMessageAction<object> action = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<object>());

                        if (action != null && action.Topic != null)
                        {
                            if (action.Topic.Equals("trade", StringComparison.OrdinalIgnoreCase))
                            {
                                UpdateTrade(message);
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        Thread.Sleep(2000);
                        SendLogMessage("PublicTradesMessageReader error: " + exception.ToString(), LogMessageType.Error);
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
                            Thread.Sleep(1000);
                            continue;
                        }

                        if (_webSocketPrivateMessages.IsEmpty)
                        {
                            Thread.Sleep(1);
                            continue;
                        }

                        _webSocketPrivateMessages.TryDequeue(out var message);

                        if (message == null)
                        {
                            continue;
                        }

                        if (message.Equals("pong"))
                        {
                            continue;
                        }

                        ResponseWebSocketMessageAction<object> action = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<object>());

                        if (action == null || action.Topic == null) 
                            continue;
                        
                        if (action.Topic.Equals("order"))
                        {
                            UpdateOrder(message);
                            continue;
                        }
                            
                        if (action.Topic.Equals("balance"))
                        {
                            UpdatePortfolio(message);
                        }
                    }
                    catch (Exception exception)
                    {
                        Thread.Sleep(2000);
                        SendLogMessage("PrivateMessageReader error: " + exception.ToString(), LogMessageType.Error);
                    }
                }
            }

            private void UpdateTrade(string message)
            {
                ResponseWebSocketMessageAction<WsTrade> responseTrade = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<WsTrade>());

                if (responseTrade?.Data == null)
                {
                    return;
                }

                Trade trade = new Trade();
                trade.SecurityNameCode = responseTrade.Data.Symbol;
                trade.Price = responseTrade.Data.TradePrice.ToDecimal();
                trade.Id = responseTrade.Data.TradeId;
                trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseTrade.Data.TradeTime));
                trade.Volume = responseTrade.Data.TradeQuantity.ToDecimal();
                trade.Side = responseTrade.Data.IsBuyerMaker.Equals("true") ? Side.Buy : Side.Sell;

                NewTradesEvent?.Invoke(trade);
            }
            
            private void SaveDepth(string message)
            {
                ResponseWebSocketMessageAction<ResponseWebSocketDepth> responseDepth = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketDepth>());
                
                if (responseDepth?.Data == null)
                {
                    return;
                }

                if (_depthSecurity.ContainsKey(responseDepth.Data.Symbol))
                {
                    if (Convert.ToInt64(_depthSecurity[responseDepth.Data.Symbol].Time) < Convert.ToInt64(responseDepth.Data.Time))
                    {
                        _depthSecurity[responseDepth.Data.Symbol] = responseDepth.Data;
                    }
                }
                else
                {
                    _depthSecurity[responseDepth.Data.Symbol] = responseDepth.Data;
                }
            }
            
            private ResponseWebSocketDepth _snapshotDepth;

            private void UpdateDepth(string message)
            {
                ResponseWebSocketMessageAction<ResponseWebSocketDepthIncremental> responseDepth = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketDepthIncremental>());

                if (responseDepth?.Data == null)
                {
                    return;
                }

                if (_bufferDepthSecurity.ContainsKey(responseDepth.Data.Symbol))
                {
                    _bufferDepthSecurity[responseDepth.Data.Symbol].Add(responseDepth.Data);
                }
                else
                {
                    _bufferDepthSecurity.Add(responseDepth.Data.Symbol, new List<ResponseWebSocketDepthIncremental>() { responseDepth.Data });
                }

                if (_depthSecurity == null || !_depthSecurity.ContainsKey(responseDepth.Data.Symbol))
                {
                    return;
                }
                    
                _snapshotDepth = _depthSecurity[responseDepth.Data.Symbol];    
                
                MarketDepth marketDepth = new MarketDepth();

                List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                marketDepth.SecurityNameCode = _snapshotDepth.Symbol;
                
                if (_snapshotDepth.asks != null)
                {
                    for (int i = 0; i < _snapshotDepth.asks.Count; i++)
                    {
                        MarketDepthLevel newMdLevel = new MarketDepthLevel();
                        newMdLevel.Ask = _snapshotDepth.asks[i][1].ToDecimal();
                        newMdLevel.Price = _snapshotDepth.asks[i][0].ToDecimal();
                    
                        ascs.Add(newMdLevel);
                    }
                }

                if (_snapshotDepth.bids != null)
                {
                    for (int i = 0; i < _snapshotDepth.bids.Count; i++)
                    {
                        MarketDepthLevel newMdLevel = new MarketDepthLevel();
                        newMdLevel.Bid = _snapshotDepth.bids[i][1].ToDecimal();
                        newMdLevel.Price = _snapshotDepth.bids[i][0].ToDecimal();

                        bids.Add(newMdLevel);
                    } 
                }
                
                long snapId = Convert.ToInt64(_snapshotDepth.Time);
                string lastUpdateTime = _snapshotDepth.Time;
                
                if(_bufferDepthSecurity.ContainsKey(responseDepth.Data.Symbol) && _bufferDepthSecurity[responseDepth.Data.Symbol].Count > 0)
                {
                    List<ResponseWebSocketDepthIncremental> buffer = _bufferDepthSecurity[responseDepth.Data.Symbol];
                    
                    for (int i = 0; i < buffer.Count; i++)
                    {
                        long buffId = Convert.ToInt64(buffer[i].LastUpdateId);
                        
                        if (buffId > snapId)
                        {
                            lastUpdateTime = buffer[i].LastUpdateId;

                            if (buffer[i].asks != null 
                                && buffer[i].asks.Count > 0)
                            {
                                for (int k = 0; k < buffer[i].asks.Count; k++)
                                {
                                    for (int j = 1; j < ascs.Count; j++)
                                    {
                                        if (ascs[j-1].Price == buffer[i].asks[k][0].ToDecimal())
                                        {
                                            if (buffer[i].asks[k][1].ToDecimal() == 0)
                                            {
                                                ascs.RemoveAt(j-1);
                                            }
                                            else
                                            {
                                                ascs[j-1].Ask = buffer[i].asks[k][1].ToDecimal();
                                            }
                                        }
                                        else if (buffer[i].asks[k][0].ToDecimal() < ascs[j].Price
                                                && buffer[i].asks[k][0].ToDecimal() > ascs[j-1].Price   
                                                && buffer[i].asks[k][1].ToDecimal() != 0)
                                        {
                                            //add new level
                                            ascs.Insert(j, new MarketDepthLevel
                                            {
                                                Ask = buffer[i].asks[k][1].ToDecimal(),
                                                Price = buffer[i].asks[k][0].ToDecimal()
                                            });
                                        }
                                    }
                                }
                            }

                            if (buffer[i].bids != null
                                && buffer[i].bids.Count > 0)
                            {
                                for (int k = 0; k < buffer[i].bids.Count; k++)
                                {
                                    for (int j = 1; j < bids.Count; j++)
                                    {
                                        if (bids[j-1].Price == buffer[i].bids[k][0].ToDecimal())
                                        {
                                            if (buffer[i].bids[k][1].ToDecimal() == 0)
                                            {
                                                bids.RemoveAt(j-1);
                                            }
                                            else
                                            {
                                                bids[j-1].Bid = buffer[i].bids[k][1].ToDecimal();
                                            }
                                        }

                                        else if (buffer[i].bids[k][0].ToDecimal() > bids[j].Price
                                                && buffer[i].bids[k][0].ToDecimal() < bids[j-1].Price
                                                && buffer[i].bids[k][1].ToDecimal() != 0)
                                        {
                                            //add new level
                                            bids.Insert(j, new MarketDepthLevel
                                            {
                                                Bid = buffer[i].bids[k][1].ToDecimal(),
                                                Price = buffer[i].bids[k][0].ToDecimal()
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                _bufferDepthSecurity[responseDepth.Data.Symbol].Clear();
                _depthSecurity[responseDepth.Data.Symbol].Time = lastUpdateTime;
                
                marketDepth.Asks = ascs;
                marketDepth.Bids = bids;
                marketDepth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(lastUpdateTime));
                
                MarketDepthEvent?.Invoke(marketDepth);
            }

            private void UpdateMyTrade(string json)
            {
                ResponseMessageRest<ResponseMyTrades> responseMyTrades = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<ResponseMyTrades>());

                for (int i = 0; i < responseMyTrades.result.items.Count; i++)
                {
                    ResponseMyTrade responseT = responseMyTrades.result.items[i];

                    MyTrade myTrade = new MyTrade();

                    myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseT.time));
                    myTrade.NumberOrderParent = responseT.orderId;
                    myTrade.NumberTrade = responseT.tradeId;
                    myTrade.Price = responseT.price.ToDecimal();
                    myTrade.SecurityNameCode = responseT.symbol;
                    myTrade.Side = responseT.orderSide.Equals("Buy", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;

                    string comissionSecName = responseT.feeCurrency;

                    if (myTrade.SecurityNameCode.StartsWith(comissionSecName))
                    {
                        myTrade.Volume = responseT.quantity.ToDecimal() - responseT.fee.ToDecimal();
                    }
                    else
                    {
                        myTrade.Volume = responseT.quantity.ToDecimal();
                    }

                    MyTradeEvent?.Invoke(myTrade);
                }
            }

            private void UpdatePortfolio(string message)
            {
                ResponseWebSocketMessageAction<ResponseWebSocketPortfolio> Portfolio = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketPortfolio>());

                Portfolio portfolio = new Portfolio();
                portfolio.Number = "XTSpot";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;

                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = "XTSpot";
                pos.SecurityNameCode = Portfolio.Data.Symbol;
                pos.ValueBlocked = Portfolio.Data.Frozen.ToDecimal();
                pos.ValueCurrent = Portfolio.Data.Balance.ToDecimal();

                portfolio.SetNewPosition(pos);

                PortfolioEvent?.Invoke(new List<Portfolio> { portfolio });
            }

            private void UpdateOrder(string message)
            {
                ResponseWebSocketMessageAction<ResponseWebSocketOrder> Order = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketOrder>());

                if (Order.Data == null)
                {
                    return;
                }

                ResponseWebSocketOrder item = Order.Data;

                OrderStateType stateType = GetOrderState(item.State);

                if (item.Type.Equals("Market", StringComparison.OrdinalIgnoreCase) 
                    && stateType == OrderStateType.Activ)
                {
                    return;
                }

                Order newOrder = new Order();
                newOrder.SecurityNameCode = item.Symbol;
                long time = item.HappenedTime == null ? Convert.ToInt64(item.CreateTime) : Convert.ToInt64(item.HappenedTime);
                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(time);

                if (item.ClientOrderId != null)
                {
                    try
                    {
                        newOrder.NumberUser = Convert.ToInt32(item.ClientOrderId);
                    }
                    catch
                    {
                        SendLogMessage("Strange order num: " + item.ClientOrderId, LogMessageType.Error);
                        return;
                    }
                }

                newOrder.NumberMarket = item.OrderId;

                OrderPriceType.TryParse(item.Type, true, out newOrder.TypeOrder);

                newOrder.Side = item.Side.Equals("Buy", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;
                newOrder.State = stateType;
                

                if(newOrder.TypeOrder == OrderPriceType.Market && newOrder.Side == Side.Buy)
                {
                    newOrder.Volume = item.ExecutedQuantity.ToDecimal() / item.AveragePrice.ToDecimal();  //сумма покупки / цену сделки округлить до точности объема
                    newOrder.Price = item.AveragePrice.ToDecimal(); //price
                }
                else
                {
                    newOrder.Volume = item.OriginalQuantity?.ToDecimal() ?? 0;
                    newOrder.Price = item.Price?.ToDecimal() ?? 0;
                }
                
                newOrder.ServerType = ServerType.XTSpot;
                newOrder.PortfolioNumber = "XTSpot";

                if (stateType == OrderStateType.Done || stateType == OrderStateType.Patrial)
                {
                    CreateQueryMyTrade(newOrder.SecurityNameCode, newOrder.NumberMarket, 1);
                }

                MyOrderEvent?.Invoke(newOrder);
            }

            private OrderStateType GetOrderState(string orderStatusResponse)
            {
                OrderStateType stateType;

                switch (orderStatusResponse.ToLower())
                {
                    case ("new"):
                        stateType = OrderStateType.Activ;
                        break;
                    
                    case ("partially_filled"):
                        stateType = OrderStateType.Patrial;
                        break;
                    
                    case ("filled"):
                        stateType = OrderStateType.Done;
                        break;

                    case ("canceled"):
                        stateType = OrderStateType.Cancel;
                        break;

                    case ("rejected"):
                        stateType = OrderStateType.Fail;
                        break;
                    
                    case ("expired"):
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

            private readonly RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(200));

            private readonly RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(95));

            public void SendOrder(Order order)
            {
                // https://doc.xt.com/#orderorderPost
                _rateGateSendOrder.WaitToProceed();

                SendOrderRequestData data = new SendOrderRequestData();
                data.symbol = order.SecurityNameCode;
                data.clientOrderId = (order.NumberUser + 1000).ToString();
                data.side = order.Side.ToString().ToUpper();
                data.type = order.TypeOrder.ToString().ToUpper();
                data.timeInForce = "GTC";
                data.bizType = "SPOT";
                data.price = order.TypeOrder == OrderPriceType.Market ? null : order.Price.ToString().Replace(",", ".");
                
                if(order.TypeOrder == OrderPriceType.Limit)
                {
                    data.price = order.Price.ToString().Replace(",", ".");
                    data.quantity = order.Volume.ToString().Replace(",", ".");
                }
                else
                {
                    data.timeInForce = "FOK";
                    if (data.side == "BUY")
                    {
                        data.quantity = null;
                        data.quoteQty = (order.Volume * order.Price).ToString().Replace(",", ".");
                    }
                    else
                    {
                        data.quoteQty = null;
                        data.quantity = order.Volume.ToString().Replace(",", ".");
                    }
                }
                
                JsonSerializerSettings dataSerializerSettings = new JsonSerializerSettings();
                dataSerializerSettings.NullValueHandling = NullValueHandling.Ignore;

                string jsonRequest = JsonConvert.SerializeObject(data, dataSerializerSettings);

                HttpResponseMessage responseMessage = CreatePrivateQuery("/v4/order", "POST", jsonRequest);
                string jsonResponse = responseMessage.Content.ReadAsStringAsync().Result;
                
                ResponseMessageRest<ResponsePlaceOrder> stateResponse = JsonConvert.DeserializeAnonymousType(jsonResponse, new ResponseMessageRest<ResponsePlaceOrder>());

                if (responseMessage.StatusCode == HttpStatusCode.OK && stateResponse != null)
                {
                    if (stateResponse.rc.Equals("0") && stateResponse.mc.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {
                        SendLogMessage($"Order num {order.NumberUser} on XT exchange.", LogMessageType.Trade);
                        order.State = OrderStateType.Activ;
                        if (order.TypeOrder == OrderPriceType.Market)
                        {
                            order.State = OrderStateType.Done;
                            CreateQueryMyTrade(order.SecurityNameCode, stateResponse.result.orderId, 1);
                        }
                        order.NumberMarket = stateResponse.result.orderId;

                        MyOrderEvent?.Invoke(order);
                    }
                    else
                    {
                        CreateOrderFail(order);
                        SendLogMessage($"SendOrder Fail, Code: {stateResponse.rc}\n"
                            + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                    }
                }
                else
                {
                    CreateOrderFail(order);
                    SendLogMessage($"SendOrder> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                    if (stateResponse != null && stateResponse.rc != null)
                    {
                        SendLogMessage($"SendOrder Fail, Code: {stateResponse.rc}\n"
                            + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                    }
                }
            }

            public void ChangeOrderPrice(Order order, decimal newPrice)
            {

            }

            public void GetOrdersState(List<Order> orders)
            {

            }

            public void CancelAllOrders()
            {
                _rateGateCancelOrder.WaitToProceed();

                HttpResponseMessage responseMessage = CreatePrivateQuery("/v4/open-order", "DELETE", null);
                string jsonResponse = responseMessage.Content.ReadAsStringAsync().Result;
                ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(jsonResponse, new ResponseMessageRest<object>());

                if (responseMessage.StatusCode == HttpStatusCode.OK && stateResponse != null)
                {
                    if (stateResponse.rc.Equals("0") && stateResponse.mc.Equals("SUCCESS", StringComparison.CurrentCulture))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage($"CancelAllOrders error, Code: {stateResponse.rc}\n"
                                       + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"CancelAllOrders> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                    if (stateResponse != null && stateResponse.rc != null)
                    {
                        SendLogMessage($"CancelAllOrders error, Code: {stateResponse.rc}\n"
                                       + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                    }
                }
            }

            public void CancelAllOrdersToSecurity(Security security)
            {
                _rateGateCancelOrder.WaitToProceed();

                CancelAllOrdersRequestData data = new CancelAllOrdersRequestData();
                data.symbol = security.Name;
                data.bizType = "SPOT";

                string jsonRequest = JsonConvert.SerializeObject(data);

                HttpResponseMessage responseMessage = CreatePrivateQuery("/v4/open-order", "DELETE", jsonRequest);
                string jsonResponse = responseMessage.Content.ReadAsStringAsync().Result;
                ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(jsonResponse, new ResponseMessageRest<object>());

                if (responseMessage.StatusCode == HttpStatusCode.OK && stateResponse != null)
                {
                    if (stateResponse.rc.Equals("0") && stateResponse.mc.Equals("SUCCESS", StringComparison.CurrentCulture))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage($"CancelAllOrdersToSecurity error, Code: {stateResponse.rc}\n"
                            + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"CancelAllOrdersToSecurity> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                    if (stateResponse != null && stateResponse.rc != null)
                    {
                        SendLogMessage($"CancelAllOrdersToSecurity error, Code: {stateResponse.rc}\n"
                            + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                    }
                }
            }

            public void CancelOrder(Order order)
            {
                _rateGateCancelOrder.WaitToProceed();
                
                HttpResponseMessage responseMessage = CreatePrivateQuery("/v4/order/" + order.NumberMarket, "DELETE", "");
                
                string jsonResponse = responseMessage.Content.ReadAsStringAsync().Result;
                
                ResponseMessageRest<CancaledOrderResponse> stateResponse = JsonConvert.DeserializeAnonymousType(jsonResponse, new ResponseMessageRest<CancaledOrderResponse>());

                if (responseMessage.StatusCode == HttpStatusCode.OK && stateResponse != null)
                {
                    if (stateResponse.rc.Equals("0") && stateResponse.mc.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {
                        SendLogMessage($"Successfully canceled the order, order Id: {stateResponse.result.cancelId}", LogMessageType.Trade);
                    }
                    else
                    {
                        CreateOrderFail(order);
                        SendLogMessage($"CancelOrder error, Code: {stateResponse.rc}\n"
                            + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                    }
                }
                else
                {
                    CreateOrderFail(order);
                    SendLogMessage($"CancelOrder> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                    if (stateResponse != null && stateResponse.rc != null)
                    {
                        SendLogMessage($"CancelOrder error, Code: {stateResponse.rc}\n"
                            + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                    }
                }
            }
           
            private void CreateOrderFail(Order order)
            {
                order.State = OrderStateType.Fail;
                MyOrderEvent?.Invoke(order);
            }

            public void GetAllActivOrders()
            {
                List<Order> orders = GetAllOrdersFromExchange();

                if(orders == null || orders.Count == 0)
                {
                    return;
                }

                for(int i = 0; i < orders.Count; i++)
                {
                    orders[i].TimeCreate = orders[i].TimeCallBack;
                    MyOrderEvent?.Invoke(orders[i]);
                }
            }

            private Order OrderUpdate(OrderResponse orderResponse, OrderStateType type)
            {
                OrderResponse item = orderResponse;

                Order newOrder = new Order();
                
                newOrder.SecurityNameCode = item.symbol;
                
                if(!string.IsNullOrEmpty(item.updateTime))
                {
                    newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.updateTime));
                }
                else
                {
                    newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.time));
                }
                
                if (type == OrderStateType.Done)
                {
                    newOrder.TimeDone = newOrder.TimeCallBack;
                }
                else if (type == OrderStateType.Cancel)
                {
                    newOrder.TimeCancel = newOrder.TimeCallBack;
                }
                
                if (!string.IsNullOrEmpty(item.clientOrderId))
                {
                    newOrder.NumberUser = Convert.ToInt32(item.clientOrderId);
                }

                newOrder.NumberMarket = item.orderId;

                newOrder.Side = item.side.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;

                newOrder.State = type;
                newOrder.Volume = item.origQty.ToDecimal();
                newOrder.PortfolioNumber = "XTSpot";

                if (string.IsNullOrEmpty(item.avgPrice) == false
                    && item.avgPrice != "0")
                {
                    newOrder.Price = item.avgPrice.ToDecimal();
                }
                else if (string.IsNullOrEmpty(item.price) == false
                         && item.price != "0")
                {
                    newOrder.Price = item.price.ToDecimal();
                }

                newOrder.TypeOrder = item.type.Equals("MARKET", StringComparison.OrdinalIgnoreCase) ? OrderPriceType.Market : OrderPriceType.Limit;

                newOrder.ServerType = ServerType.XTSpot;

                return newOrder;
            }
            
            private List<Order> GetAllOrdersFromExchange()
            {
                string url = "/v4/open-order";
                string query = "bizType=SPOT";

                HttpResponseMessage res = CreatePrivateQuery(url,"GET",query);

                string contentStr = res.Content.ReadAsStringAsync().Result;

                if (res.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage(contentStr, LogMessageType.Error);
                    return null;
                }

                ResponseMessageRest<List<OrderResponse>> OrderResponse = JsonConvert.DeserializeAnonymousType(contentStr, new ResponseMessageRest<List<OrderResponse>>());

                if (OrderResponse.rc != "0")
                {
                    SendLogMessage($"GetAllOrdersFromExchange> error, Code: {OrderResponse.rc}\n"
                        + $"Message code: {OrderResponse.mc}", LogMessageType.Error);
                    return null;
                }   
                
                List<Order> orders = new List<Order>();

                for (int i = 0; i < OrderResponse.result.Count; i++)
                {
                    Order newOrder = null;

                    newOrder = OrderUpdate(OrderResponse.result[i], GetOrderState(OrderResponse.result[i].state));

                    if (newOrder == null)
                    {
                        continue;
                    }

                    orders.Add(newOrder);
                }

                return orders;
            }

            public void GetOrderStatus(Order order)
            {
                // GET /v4/order
                string url = "/v4/order";
                
                string query = null;
                
                query = string.IsNullOrEmpty(order.NumberMarket) ? $"clientOrderId={order.NumberMarket}" : $"orderId={order.NumberMarket}";

                HttpResponseMessage res = CreatePrivateQuery(url, "GET", query);

                string contentStr = res.Content.ReadAsStringAsync().Result;

                if (res.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage(contentStr, LogMessageType.Error);
                    return;
                }

                ResponseMessageRest<OrderResponse> OrderResponse = JsonConvert.DeserializeAnonymousType(contentStr, new ResponseMessageRest<OrderResponse>());

                if(OrderResponse.rc != "0")
                {
                    SendLogMessage($"GetOrderStatus error, code: {OrderResponse.rc}\n"
                        + $"Message code: {OrderResponse.mc}", LogMessageType.Error);
                    return;
                }

                Order newOrder = OrderUpdate(OrderResponse.result, GetOrderState(OrderResponse.result.state));
                
                if (newOrder == null)
                {
                    return;
                }

                Order myOrder = newOrder;

                MyOrderEvent?.Invoke(myOrder);

                if(myOrder.State == OrderStateType.Done 
                   || myOrder.State == OrderStateType.Patrial)
                {
                    CreateQueryMyTrade(myOrder.SecurityNameCode,myOrder.NumberMarket,1);
                }
            }

            #endregion

            #region 12 Queries

            private readonly string _baseUrl = "https://sapi.xt.com";

            private readonly string _timeOut = "5000";

            private readonly string _encry = "HmacSHA256";

            private readonly RateGate _rateGateGetMyTradeState = new RateGate(1, TimeSpan.FromMilliseconds(90));

            HttpClient _httpPublicClient = new HttpClient();

            private List<string> _subscribedSecurities = new List<string>();

            private string GetListenKey()
            {
                string listenKey = "";

                try
                {
                    HttpResponseMessage responseMessage = CreatePrivateQuery("/v4/ws-token", "POST", "");
                    string jsonResponse = responseMessage.Content.ReadAsStringAsync().Result;
                    ResponseMessageRest<ResponseToken> stateResponse = JsonConvert.DeserializeAnonymousType(jsonResponse, new ResponseMessageRest<ResponseToken>());

                    if (responseMessage.StatusCode == HttpStatusCode.OK && stateResponse != null)
                    {
                        if (stateResponse.rc.Equals("0") && stateResponse.mc.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                        {
                            SendLogMessage($"ListenKey successfully received.", LogMessageType.Connect);
                            listenKey =  stateResponse.result.accessToken;
                        }
                        else
                        {
                            SendLogMessage($"GetListenKey error, Code: {stateResponse.rc}\n"
                                           + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Receiving Token> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                        if (stateResponse != null && stateResponse.rc != null)
                        {
                            SendLogMessage($"GetListenKey error, Code: {stateResponse.rc}\n"
                                           + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                        }
                    }

                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
                
                return listenKey;
            }
            
            private void CreateSubscribedSecurityMessageWebSocket(Security security)
            {
                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    if (_subscribedSecurities[i].Equals(security.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }

                lock (_socketLocker)
                {
                    _webSocketPublic.Send($"{{\"method\":\"subscribe\",\"params\":[\"depth_update@{security.Name}\", \"depth@{security.Name},{20}\"],\"id\":\"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");
                    _webSocketPublicTrades.Send($"{{\"method\":\"subscribe\",\"params\":[\"trade@{security.Name}\"],\"id\":\"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");
                }
            }

            private void CreateQueryPortfolio(bool isUpdateValueBegin)
            {
                try
                {
                    HttpResponseMessage responseMessage = CreatePrivateQuery("/v4/balances", "GET", null);
                    
                    string json = responseMessage.Content.ReadAsStringAsync().Result;

                    ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<object>());

                    if (responseMessage.StatusCode == HttpStatusCode.OK && stateResponse != null)
                    {
                        if (stateResponse.rc.Equals("0") && stateResponse.mc.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                        {
                            UpdatePortfolioRest(json, isUpdateValueBegin);
                        }
                        else
                        {
                            SendLogMessage($"CreateQueryPortfolio error, Code: {stateResponse.rc}\n" 
                                           + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage($"CreateQueryPortfolio> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                        if (stateResponse != null && stateResponse.rc != null)
                        {
                            SendLogMessage($"CreateQueryPortfolio error, Code: {stateResponse.rc}\n" 
                                           + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage("CreateQueryPortfolio error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            private void UpdatePortfolioRest(string json, bool isUpdateValueBegin)
            {
                ResponseMessageRest<ResponseAssets> assets = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<ResponseAssets>());

                Portfolio portfolio = new Portfolio
                {
                    Number = "XTSpot",
                    ValueBegin = 1,
                    ValueCurrent = 1
                };

                List<PositionOnBoard> alreadySendPositions = new List<PositionOnBoard>();
                
                for (int i = 0; i < assets.result.assets.Count; i++)
                {
                    ResponseAsset item = assets.result.assets[i];

                    PositionOnBoard pos = new PositionOnBoard
                    {
                        PortfolioName = "XTSpot",
                        SecurityNameCode = item.currency,
                        ValueBlocked = item.frozenAmount.ToDecimal(),
                        ValueCurrent = item.availableAmount.ToDecimal()
                    };

                    if (isUpdateValueBegin)
                    {
                        pos.ValueBegin = item.availableAmount.ToDecimal();
                    }

                    bool canSend = true;

                    for(int j = 0; j < alreadySendPositions.Count; j++)
                    {
                        if (!alreadySendPositions[j].SecurityNameCode.Equals(pos.SecurityNameCode, StringComparison.OrdinalIgnoreCase) 
                            || pos.ValueCurrent != 0) 
                            continue;
                        
                        canSend = false;
                        break;
                    }

                    if (!canSend) 
                        continue;
                    
                    portfolio.SetNewPosition(pos);
                    alreadySendPositions.Add(pos);
                }

                PortfolioEvent?.Invoke(new List<Portfolio> { portfolio });
            }

            private void CreateQueryMyTrade(string nameSec, string OrdId, int serialStart)
            {
                _rateGateGetMyTradeState.WaitToProceed();

                string queryString = $"orderId={OrdId}";
                int startCount = serialStart;

                HttpResponseMessage responseMessage = CreatePrivateQuery("/v4/trade", "GET", queryString);
                string jsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

                ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(jsonResponse, new ResponseMessageRest<object>());

                if (responseMessage.StatusCode == HttpStatusCode.OK && stateResponse != null)
                {
                    if (stateResponse.rc.Equals("0") && stateResponse.mc.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {
                        ResponseMessageRest<ResponseMyTrades> responseMyTrades = JsonConvert.DeserializeAnonymousType(jsonResponse, new ResponseMessageRest<ResponseMyTrades>());
                        
                        if (responseMyTrades.result.items.Count == 0)
                        {
                            if (startCount >= 4)
                            {
                                SendLogMessage($"Failed {startCount} attempts to receive trades for order #{OrdId}", LogMessageType.Error);
                                return;
                            }
                            
                            Thread.Sleep(200 * startCount);
                            startCount++;
                            CreateQueryMyTrade(nameSec, OrdId, startCount);
                        }

                        UpdateMyTrade(jsonResponse);
                    }
                    else
                    {
                        SendLogMessage($"CreateQueryMyTrade error, Code: {stateResponse.rc}\n"
                            + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"CreateQueryMyTrade> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                    if (stateResponse != null && stateResponse.rc != null)
                    {
                        SendLogMessage($"CreateQueryMyTrade error, Code: {stateResponse.rc}\n"
                            + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                    }
                }
            }

            private List<Candle> CreateQueryCandles(string nameSec, string stringInterval, DateTime timeFrom, DateTime timeTo)
            {
                _rateGateCandleHistory.WaitToProceed(100);

                string startTime = TimeManager.GetTimeStampMilliSecondsToDateTime(timeFrom).ToString();
                string endTime = TimeManager.GetTimeStampMilliSecondsToDateTime(timeTo).ToString();
                string limit = "1000";
                
                string uriCandles = "/v4/public/kline?symbol=" + nameSec + "&interval=" + stringInterval 
                             + "&startTime=" + startTime + "&endTime=" + endTime + "&limit=" + limit;

                HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(_baseUrl + uriCandles).Result;
                string content = responseMessage.Content.ReadAsStringAsync().Result;

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<List<ResponseCandle>> symbols = JsonConvert.DeserializeAnonymousType(content, new ResponseMessageRest<List<ResponseCandle>>());

                    if (symbols.rc.Equals("0") && symbols.mc.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {
                        List<Candle> candles = new List<Candle>();

                        for (int i = 0; i < symbols.result.Count; i++)
                        {
                            ResponseCandle item = symbols.result[i];

                            Candle newCandle = new Candle();

                            newCandle.Open = item.Open.ToDecimal();
                            newCandle.Close = item.Close.ToDecimal();
                            newCandle.High = item.High.ToDecimal();
                            newCandle.Low = item.Low.ToDecimal();
                            newCandle.Volume = item.Volume.ToDecimal();
                            newCandle.State = CandleState.Finished;
                            newCandle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.OpenTime));
                            candles.Add(newCandle);
                        }
                        
                        return candles;
                    }
                    
                    SendLogMessage($"CreateQueryCandles error, Code: {symbols.rc}\n" 
                                   + $"Message code: {symbols.mc}", LogMessageType.Error);
                    return null;
                }
                
                SendLogMessage($"CreateQueryCandles error, State Code: {responseMessage.StatusCode}", LogMessageType.Error);
                
                return null;
            }

            private HttpResponseMessage CreatePrivateQuery(string path, string method, string queryString)
            {
                string requestPath = path;
                string url = $"{_baseUrl}{requestPath}";
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                
                string signature = GetHMACSHA256(queryString, timestamp, method, requestPath);
                
                HttpClient httpClient = new HttpClient();

                httpClient.DefaultRequestHeaders.Add("validate-algorithms", _encry);
                httpClient.DefaultRequestHeaders.Add("validate-appkey", _publicKey);
                httpClient.DefaultRequestHeaders.Add("validate-recvwindow", _timeOut);
                httpClient.DefaultRequestHeaders.Add("validate-timestamp", timestamp);
                httpClient.DefaultRequestHeaders.Add("validate-signature", signature);

                if (method.Equals("POST"))
                {
                    return httpClient.PostAsync(url, new StringContent(queryString, Encoding.UTF8, "application/json")).Result;
                }
                if (method.Equals("DELETE"))
                {
                    HttpRequestMessage request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Delete,
                        RequestUri = new Uri(url),
                        Content = queryString != null ? new StringContent(queryString, Encoding.UTF8, "application/json") : null
                    };

                    return httpClient.SendAsync(request).Result;
                }
                
                string param = string.Empty;
                
                if (!string.IsNullOrEmpty(queryString))
                {
                    param = "?" + queryString;
                }
                
                return httpClient.GetAsync(url + param).Result;
            }

            private string GetHMACSHA256(string queryString, string time, string method, string url)
            {
                Encoding ascii = Encoding.ASCII;

                string s1 = "validate-algorithms=" + _encry + "&validate-appkey=" + _publicKey +
                            "&validate-recvwindow=" + _timeOut + "&validate-timestamp=" + time;

                string s2 = $"#{method}#{url}";

                if (!string.IsNullOrEmpty(queryString))
                {
                    s2 += "#" + queryString;
                }

                HMACSHA256 hmac = new HMACSHA256(ascii.GetBytes(_secretKey));

                string signature = BitConverter.ToString(hmac.ComputeHash(ascii.GetBytes(s1 + s2))).Replace("-", "");

                hmac.Dispose();

                return signature;
            }

            #endregion

            #region 13 Log

            private void SendLogMessage(string message, LogMessageType messageType)
            {
                LogMessageEvent?.Invoke(message, messageType);
            }

            public event Action<string, LogMessageType> LogMessageEvent;

            #endregion
        }
    }
}
