﻿using Newtonsoft.Json;
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
using WebSocketSharp;

namespace OsEngine.Market.Servers.XT.XTSpot
{
    public class XTServerSpot : AServer
    {
        public XTServerSpot(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            XTServerSpotRealization realization = new XTServerSpotRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
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

                Thread threadForPublicMessages = new Thread(PublicMarketDepthsMessageReader);
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

                        FIFOListWebSocketPublicMarketDepthsMessage = new ConcurrentQueue<string>();
                        FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();
                        FIFOListWebSocketPublicTradesMessage = new ConcurrentQueue<string>();

                        CreateWebSocketConnection();
                        CheckFullActivation();
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
                    _marketDepths.Clear();
                }
                catch (Exception exception)
                {
                    SendLogMessage("Dispose error" + exception.ToString(), LogMessageType.Error);
                }

                FIFOListWebSocketPublicMarketDepthsMessage = new ConcurrentQueue<string>();
                FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();
                FIFOListWebSocketPublicTradesMessage = new ConcurrentQueue<string>();

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

            private void UnsubscribeFromAllWebSockets()
            {
                if (_webSocketPublicMarketDepths == null
                    || _webSocketPrivate == null
                    || _webSocketPublicTrades == null)
                {
                    return;
                }

                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    return;
                }

                try
                {
                    for (int i = 0; i < _subscribedSecurities.Count; i++)
                    {
                        string securityName = _subscribedSecurities[i];

                        _webSocketPublicMarketDepths.Send($"{{\"method\": \"unsubscribe\", \"params\": [\"depth-update@{securityName}\",\"depth@{securityName},{20}\"], \"id\": \"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");
                        _webSocketPublicTrades.Send($"{{\"method\": \"unsubscribe\", \"params\": [\"trades@{securityName}\"], \"id\": \"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");
                    }

                    _webSocketPrivate.Send($"{{\"method\": \"unsubscribe\", \"params\": [\"balance\",\"order\"], \"id\": \"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");
                }
                catch
                {

                }
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

            #endregion

            #region 3 Securities

            private List<Security> _securities;

            private readonly RateGate _rateGateSecurity = new RateGate(1, TimeSpan.FromMilliseconds(100));

            public void GetSecurities()
            {
                _rateGateSecurity.WaitToProceed();

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
                try
                {
                    if (_securities == null)
                    {
                        _securities = new List<Security>();
                    }

                    ResponseMessageRest<ResponseSymbols> symbols =
                    JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<ResponseSymbols>());

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

                        if (string.IsNullOrEmpty(item.quantityPrecision) == false)
                        {
                            newSecurity.DecimalsVolume = Convert.ToInt32(item.quantityPrecision);
                        }

                        newSecurity.PriceStep = Convert.ToInt32(item.pricePrecision).GetValueByDecimals();
                        newSecurity.PriceStepCost = newSecurity.PriceStep;
                        newSecurity.State = SecurityStateType.Activ;
                        _securities.Add(newSecurity);
                    }

                    SecurityEvent(_securities);
                }
                catch (Exception exception)
                {
                    SendLogMessage("GetSecurities error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            public event Action<List<Security>> SecurityEvent;

            #endregion

            #region 4 Portfolios

            private List<Portfolio> _portfolios;

            public void GetPortfolios()
            {
                if (_portfolios == null)
                {
                    GetNewPortfolio();
                }

                CreateQueryPortfolio(true);
            }

            private void GetNewPortfolio()
            {
                _portfolios = new List<Portfolio>();

                Portfolio portfolioInitial = new Portfolio();
                portfolioInitial.Number = "XTSpotPortfolio";
                portfolioInitial.ValueBegin = 1;
                portfolioInitial.ValueCurrent = 1;
                portfolioInitial.ValueBlocked = 0;

                _portfolios.Add(portfolioInitial);

                PortfolioEvent(_portfolios);
            }

            private void UpdatePortfolios()
            {
                while (true)
                {
                    try
                    {
                        if (ServerStatus == ServerConnectStatus.Disconnect)
                        {
                            Thread.Sleep(2000);
                            continue;
                        }

                        Thread.Sleep(10000);

                        if (_portfolios == null)
                        {
                            GetNewPortfolio();
                        }

                        CreateQueryPortfolio(false);
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
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

            public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
            {
                int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
                DateTime endTime = DateTime.UtcNow;
                DateTime startTime = endTime.AddMinutes(-tfTotalMinutes * candleCount);

                return GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, startTime);
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
                startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
                endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
                actualTime = DateTime.SpecifyKind(actualTime, DateTimeKind.Utc);

                if (startTime != actualTime)
                {
                    startTime = actualTime;
                }

                if (!CheckTime(startTime, endTime, actualTime))
                {
                    return null;
                }

                int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

                if (!CheckTf(tfTotalMinutes))
                {
                    return null;
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
                else if (tf.Days != 0)
                {
                    return (int)timeSlice.TotalDays;
                }
                else
                {
                    return (int)timeSlice.TotalMinutes / tf.Minutes;
                }
            }

            private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
            {
                if (startTime >= endTime ||
                    startTime >= DateTime.UtcNow ||
                    actualTime > endTime ||
                    actualTime > DateTime.UtcNow)
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
                    timeFrameMinutes == 240
                    || timeFrameMinutes == 1440)
                {
                    return true;
                }
                return false;
            }

            private string GetStringInterval(TimeSpan tf)
            {
                // Type of candlestick patterns: 1m, 3m, 5m, 15m, 30m, 1h, 2h, 4h, 6h, 8h, 12h, 1d, 1w
                if (tf.Minutes != 0)
                {
                    return $"{tf.Minutes}m";
                }
                else if (tf.Days != 0)
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

            private WebSocket _webSocketPublicMarketDepths;

            private WebSocket _webSocketPublicTrades;

            private readonly string _webSocketPrivateUrl = "wss://stream.xt.com/private";

            private readonly string _webSocketPublicUrl = "wss://stream.xt.com/public";

            private readonly string _socketLocker = "webSocketLockerXT";

            private void CreateWebSocketConnection()
            {
                try
                {
                    if (_webSocketPrivate != null)
                    {
                        return;
                    }

                    _webSocketPrivate = new WebSocket(_webSocketPrivateUrl);
                    _webSocketPrivate.EmitOnPing = true;
                    _webSocketPrivate.SslConfiguration.EnabledSslProtocols
                        = System.Security.Authentication.SslProtocols.Ssl3
                       | System.Security.Authentication.SslProtocols.Tls11
                       | System.Security.Authentication.SslProtocols.Tls12
                       | System.Security.Authentication.SslProtocols.Tls13
                       | System.Security.Authentication.SslProtocols.Tls;
                    _webSocketPrivate.OnOpen += _webSocketPrivate_OnOpen;
                    _webSocketPrivate.OnClose += _webSocketPrivate_OnClose;
                    _webSocketPrivate.OnMessage += _webSocketPrivate_OnMessage;
                    _webSocketPrivate.OnError += _webSocketPrivate_OnError;
                    _webSocketPrivate.Connect();

                    if (_webSocketPublicMarketDepths != null)
                    {
                        return;
                    }

                    _webSocketPublicMarketDepths = new WebSocket(_webSocketPublicUrl);
                    _webSocketPublicMarketDepths.EmitOnPing = true;
                    _webSocketPublicMarketDepths.SslConfiguration.EnabledSslProtocols
                        = System.Security.Authentication.SslProtocols.Ssl3
                       | System.Security.Authentication.SslProtocols.Tls11
                       | System.Security.Authentication.SslProtocols.Tls12
                       | System.Security.Authentication.SslProtocols.Tls13
                       | System.Security.Authentication.SslProtocols.Tls;

                    _webSocketPublicMarketDepths.OnOpen += _webSocketPublicMarketDepths_OnOpen;
                    _webSocketPublicMarketDepths.OnClose += _webSocketPublicMarketDepths_OnClose;
                    _webSocketPublicMarketDepths.OnMessage += _webSocketPublicMarketDepths_OnMessage;
                    _webSocketPublicMarketDepths.OnError += _webSocketPublicMarketDepths_OnError;
                    _webSocketPublicMarketDepths.Connect();

                    if (_webSocketPublicTrades != null)
                    {
                        return;
                    }

                    _webSocketPublicTrades = new WebSocket(_webSocketPublicUrl);
                    _webSocketPublicTrades.EmitOnPing = true;
                    _webSocketPublicTrades.SslConfiguration.EnabledSslProtocols
                        = System.Security.Authentication.SslProtocols.Ssl3
                       | System.Security.Authentication.SslProtocols.Tls11
                       | System.Security.Authentication.SslProtocols.Tls12
                       | System.Security.Authentication.SslProtocols.Tls13
                       | System.Security.Authentication.SslProtocols.Tls;
                    _webSocketPublicTrades.OnOpen += _webSocketPublicTrades_OnOpen;
                    _webSocketPublicTrades.OnClose += _webSocketPublicTrades_OnClose;
                    _webSocketPublicTrades.OnMessage += _webSocketPublicTrades_OnMessage;
                    _webSocketPublicTrades.OnError += _webSocketPublicTrades_OnError;
                    _webSocketPublicTrades.Connect();
                }
                catch
                {
                    SendLogMessage("Create WebSocket Connection error.", LogMessageType.Error);
                }
            }

            private void DeleteWebsocketConnection()
            {
                if (_webSocketPublicMarketDepths != null)
                {
                    try
                    {
                        _webSocketPublicMarketDepths.OnOpen -= _webSocketPublicMarketDepths_OnOpen;
                        _webSocketPublicMarketDepths.OnClose -= _webSocketPublicMarketDepths_OnClose;
                        _webSocketPublicMarketDepths.OnMessage -= _webSocketPublicMarketDepths_OnMessage;
                        _webSocketPublicMarketDepths.OnError -= _webSocketPublicMarketDepths_OnError;
                        _webSocketPublicMarketDepths.CloseAsync();
                    }
                    catch
                    {
                        // ignore
                    }

                    _webSocketPublicMarketDepths = null;
                }

                if (_webSocketPublicTrades != null)
                {
                    try
                    {
                        _webSocketPublicTrades.OnOpen -= _webSocketPublicTrades_OnOpen;
                        _webSocketPublicTrades.OnClose -= _webSocketPublicTrades_OnClose;
                        _webSocketPublicTrades.OnMessage -= _webSocketPublicTrades_OnMessage;
                        _webSocketPublicTrades.OnError -= _webSocketPublicTrades_OnError;
                        _webSocketPublicTrades.CloseAsync();
                    }
                    catch
                    {
                        // ignore
                    }

                    _webSocketPublicTrades = null;
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

            private string _socketActivateLocker = "socketActivateLocker";

            private void CheckFullActivation()
            {
                try
                {
                    lock (_socketActivateLocker)
                    {
                        if (_webSocketPrivate == null
                        || _webSocketPrivate?.ReadyState != WebSocketState.Open)
                        {
                            Disconnect();
                            return;
                        }

                        if (_webSocketPublicMarketDepths == null
                            || _webSocketPublicMarketDepths?.ReadyState != WebSocketState.Open)
                        {
                            Disconnect();
                            return;
                        }

                        if (_webSocketPublicTrades == null
                            || _webSocketPublicTrades?.ReadyState != WebSocketState.Open)
                        {
                            Disconnect();
                            return;
                        }

                        if (ServerStatus != ServerConnectStatus.Connect)
                        {
                            ServerStatus = ServerConnectStatus.Connect;
                            ConnectEvent();
                        }
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.Message, LogMessageType.Error);
                }
            }

            #endregion

            #region 7 WebSocket events

            private void _webSocketPublicTrades_OnError(object sender, ErrorEventArgs e)
            {
                WebSocketSharp.ErrorEventArgs error = e;

                if (error.Exception != null)
                {
                    SendLogMessage("WebSocketPublicTrades Error: " + error.Exception.ToString(), LogMessageType.Error);
                }

                CheckFullActivation();
            }

            private void _webSocketPublicTrades_OnMessage(object sender, MessageEventArgs e)
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

                    if (e.Data.Contains("pong"))
                    {
                        // pong message
                        return;
                    }

                    if (FIFOListWebSocketPublicMarketDepthsMessage == null)
                    {
                        return;
                    }

                    FIFOListWebSocketPublicTradesMessage.Enqueue(e.Data);
                }
                catch (Exception error)
                {
                    SendLogMessage("WebSocketPublicTrades Message Received error: " + error.ToString(), LogMessageType.Error);
                }
            }

            private void _webSocketPublicTrades_OnClose(object sender, CloseEventArgs e)
            {
                try
                {
                    CheckFullActivation();
                    SendLogMessage("WebSocketPublicTrades Connection Closed by XT. WebSocket Closed Event", LogMessageType.Error);
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                }
            }

            private void _webSocketPublicTrades_OnOpen(object sender, EventArgs e)
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    CheckFullActivation();
                    SendLogMessage("WebSocketPublicTrades Connection to public trades is Open", LogMessageType.System);
                }
            }

            private void _webSocketPublicMarketDepths_OnError(object sender, ErrorEventArgs e)
            {
                WebSocketSharp.ErrorEventArgs error = e;

                if (error.Exception != null)
                {
                    SendLogMessage("WebSocketPublic Error: " + error.Exception.ToString(), LogMessageType.Error);
                }

                CheckFullActivation();
            }

            private void _webSocketPublicMarketDepths_OnMessage(object sender, MessageEventArgs e)
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

                    if (e.Data.Contains("pong"))
                    {
                        // pong message
                        return;
                    }

                    if (FIFOListWebSocketPublicMarketDepthsMessage == null)
                    {
                        return;
                    }

                    FIFOListWebSocketPublicMarketDepthsMessage.Enqueue(e.Data);
                }
                catch (Exception error)
                {
                    SendLogMessage("WebSocketPublic Message Received error: " + error.ToString(), LogMessageType.Error);
                }
            }

            private void _webSocketPublicMarketDepths_OnClose(object sender, CloseEventArgs e)
            {
                try
                {
                    CheckFullActivation();
                    SendLogMessage("WebSocketPublicTMarketDepths Connection Closed by XT. WebSocket Closed Event", LogMessageType.Error);
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                }
            }

            private void _webSocketPublicMarketDepths_OnOpen(object sender, EventArgs e)
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    CheckFullActivation();
                    SendLogMessage("WebSocketPublic Connection to public data is Open", LogMessageType.System);
                }
            }

            private void _webSocketPrivate_OnError(object sender, ErrorEventArgs e)
            {
                WebSocketSharp.ErrorEventArgs error = e;

                if (error.Exception != null)
                {
                    SendLogMessage("WebSocketPrivate Error" + error.Exception.ToString(), LogMessageType.Error);
                }

                CheckFullActivation();
            }

            private void _webSocketPrivate_OnMessage(object sender, MessageEventArgs e)
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

                    if (string.IsNullOrEmpty(e.Data))
                    {
                        return;
                    }

                    if (e.Data.Contains("pong"))
                    {
                        // pong message
                        return;
                    }

                    if (FIFOListWebSocketPrivateMessage == null)
                    {
                        return;
                    }

                    FIFOListWebSocketPrivateMessage.Enqueue(e.Data);
                }
                catch (Exception error)
                {
                    SendLogMessage("WebSocketPrivate Message Received error: " + error.ToString(), LogMessageType.Error);
                }
            }

            private void _webSocketPrivate_OnClose(object sender, CloseEventArgs e)
            {
                try
                {
                    CheckFullActivation();
                    SendLogMessage("WebSocketPrivate Connection Closed by XT. WebSocket Closed Event", LogMessageType.Error);
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                }
            }

            private void _webSocketPrivate_OnOpen(object sender, EventArgs e)
            {
                try
                {
                    CheckFullActivation();
                    SendLogMessage("WebSocketPrivate Connection to private data is Open", LogMessageType.System);

                    // sign up for order and portfolio changes
                    _webSocketPrivate.Send($"{{\"method\":\"subscribe\",\"params\":[\"order\",\"balance\"],\"listenKey\":\"{_listenKey}\",\"id\":\"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}"); // change orders
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                }
            }

            #endregion

            #region 8 WebSocket check alive

            private void ConnectionCheck()
            {
                while (true)
                {
                    try
                    {
                        if (ServerStatus == ServerConnectStatus.Disconnect)
                        {
                            Thread.Sleep(2000);
                            continue;
                        }

                        Thread.Sleep(15000);

                        if (_webSocketPublicMarketDepths == null)
                        {
                            continue;
                        }

                        if (_webSocketPrivate == null)
                        {
                            continue;
                        }

                        if (_webSocketPublicTrades == null)
                        {
                            continue;
                        }

                        if (_webSocketPublicMarketDepths.ReadyState == WebSocketState.Open
                            && _webSocketPublicTrades.ReadyState == WebSocketState.Open
                            && _webSocketPrivate.ReadyState == WebSocketState.Open)
                        {
                            _webSocketPublicMarketDepths.Send("ping");
                            _webSocketPrivate.Send("ping");
                            _webSocketPublicTrades.Send("ping");
                        }
                        else
                        {
                            Dispose();
                        }
                    }
                    catch (Exception error)
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

            public bool SubscribeNews()
            {
                return false;
            }

            public event Action<News> NewsEvent;

            #endregion

            #region 10 WebSocket parsing the messages

            private ConcurrentQueue<string> FIFOListWebSocketPublicMarketDepthsMessage = new ConcurrentQueue<string>();

            private ConcurrentQueue<string> FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

            private ConcurrentQueue<string> FIFOListWebSocketPublicTradesMessage = new ConcurrentQueue<string>();

            private void PublicMarketDepthsMessageReader()
            {
                while (true)
                {
                    try
                    {
                        if (ServerStatus != ServerConnectStatus.Connect)
                        {
                            Thread.Sleep(1000);
                            continue;
                        }

                        if (FIFOListWebSocketPublicMarketDepthsMessage.IsEmpty)
                        {
                            Thread.Sleep(1);
                            continue;
                        }

                        string message;

                        FIFOListWebSocketPublicMarketDepthsMessage.TryDequeue(out message);

                        if (message == null)
                        {
                            continue;
                        }

                        if (message.Equals("pong", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        ResponseWebSocketMessageAction<object> action = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<object>());

                        if (action != null && action.topic != null)
                        {
                            if (action.topic.Equals("depth_update", StringComparison.OrdinalIgnoreCase))
                            {
                                UpdateDepth(message);
                            }
                            else if (action.topic.Equals("depth", StringComparison.OrdinalIgnoreCase))
                            {
                                SnapshotDepth(message);
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        Thread.Sleep(2000);
                        SendLogMessage("PublicMarketDepthsMessageReader error: " + exception.ToString(), LogMessageType.Error);
                    }
                }
            }

            private void PublicTradesMessageReader()
            {
                while (true)
                {
                    try
                    {
                        if (ServerStatus != ServerConnectStatus.Connect)
                        {
                            Thread.Sleep(1000);
                            continue;
                        }

                        if (FIFOListWebSocketPublicTradesMessage.IsEmpty)
                        {
                            Thread.Sleep(1);
                            continue;
                        }

                        FIFOListWebSocketPublicTradesMessage.TryDequeue(out var message);

                        if (message == null)
                        {
                            continue;
                        }

                        if (message.Equals("pong", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        ResponseWebSocketMessageAction<object> action = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<object>());

                        if (action != null && action.topic != null)
                        {
                            if (action.topic.Equals("trade", StringComparison.OrdinalIgnoreCase))
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
                while (true)
                {
                    try
                    {
                        if (ServerStatus != ServerConnectStatus.Connect)
                        {
                            Thread.Sleep(1000);
                            continue;
                        }

                        if (FIFOListWebSocketPrivateMessage.IsEmpty)
                        {
                            Thread.Sleep(1);
                            continue;
                        }

                        FIFOListWebSocketPrivateMessage.TryDequeue(out var message);

                        if (message == null)
                        {
                            continue;
                        }

                        if (message.Equals("pong"))
                        {
                            continue;
                        }

                        ResponseWebSocketMessageAction<object> action = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<object>());

                        if (action == null || action.topic == null)
                            continue;

                        if (action.topic.Equals("order"))
                        {
                            UpdateOrder(message);
                            continue;
                        }

                        if (action.topic.Equals("balance"))
                        {
                            //UpdatePortfolio(message);
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
                try
                {
                    ResponseWebSocketMessageAction<WsTrade> responseTrade = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<WsTrade>());

                    if (responseTrade?.data == null)
                    {
                        return;
                    }

                    Trade trade = new Trade();
                    trade.SecurityNameCode = responseTrade.data.s;
                    trade.Price = responseTrade.data.p.ToDecimal();
                    trade.Id = responseTrade.data.i;
                    trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseTrade.data.t));
                    trade.Volume = responseTrade.data.q.ToDecimal();
                    trade.Side = responseTrade.data.b.Equals("true") ? Side.Buy : Side.Sell;

                    NewTradesEvent?.Invoke(trade);
                }
                catch (Exception exception)
                {
                    SendLogMessage("SnapshotDepth error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            private List<MarketDepth> _marketDepths = new List<MarketDepth>();

            private DateTime _lastTimeMd = DateTime.MinValue;

            private bool startDepth = true;

            private void SnapshotDepth(string message)
            {
                try
                {
                    ResponseWebSocketMessageAction<ResponseWebSocketDepth> responseDepth = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketDepth>());

                    if (responseDepth?.data == null)
                    {
                        return;
                    }

                    MarketDepth marketDepth = null;

                    for (int i = 0; i < _marketDepths.Count; i++)
                    {
                        if (_marketDepths[i].SecurityNameCode == responseDepth.data.s)
                        {
                            marketDepth = _marketDepths[i];
                            break;
                        }
                    }

                    if (startDepth)
                    {
                        if (marketDepth == null)
                        {
                            marketDepth = new MarketDepth();
                            _marketDepths.Add(marketDepth);
                        }
                        else
                        {
                            marketDepth.Asks.Clear();
                            marketDepth.Bids.Clear();
                        }

                        List<MarketDepthLevel> asks = new List<MarketDepthLevel>();
                        List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                        marketDepth.SecurityNameCode = responseDepth.data.s;

                        if (responseDepth.data.a != null)
                        {
                            for (int i = 0; i < responseDepth.data.a.Count; i++)
                            {
                                asks.Add(new MarketDepthLevel()
                                {
                                    Ask = responseDepth.data.a[i][1].ToDecimal(),
                                    Price = responseDepth.data.a[i][0].ToDecimal(),
                                });
                            }
                        }

                        if (responseDepth.data.b != null)
                        {
                            for (int i = 0; i < responseDepth.data.b.Count; i++)
                            {
                                bids.Add(new MarketDepthLevel()
                                {
                                    Bid = responseDepth.data.b[i][1].ToDecimal(),
                                    Price = responseDepth.data.b[i][0].ToDecimal(),
                                });
                            }
                        }

                        marketDepth.Asks = asks;
                        marketDepth.Bids = bids;

                        marketDepth.Time = ServerTime;

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

                        if (MarketDepthEvent != null)
                        {
                            MarketDepthEvent(marketDepth.GetCopy());
                        }

                        startDepth = false;
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage("SnapshotDepth error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            private void UpdateDepth(string message)
            {
                try
                {
                    ResponseWebSocketMessageAction<ResponseWebSocketDepthIncremental> responseDepth = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketDepthIncremental>());

                    if (responseDepth?.data == null)
                    {
                        return;
                    }

                    if (_marketDepths == null
                        || _marketDepths.Count == 0)
                    {
                        startDepth = true;
                        return;
                    }

                    MarketDepth marketDepth = null;

                    for (int i = 0; i < _marketDepths.Count; i++)
                    {
                        if (_marketDepths[i].SecurityNameCode == responseDepth.data.s)
                        {
                            marketDepth = _marketDepths[i];
                            break;
                        }
                    }

                    if (marketDepth == null)
                    {
                        startDepth = true;
                        return;
                    }

                    if (marketDepth.Asks.Count == 0
                        || marketDepth.Bids.Count == 0)
                    {
                        startDepth = true;
                        return;
                    }

                    if (responseDepth.data.a != null
                            && responseDepth.data.a.Count > 0)
                    {
                        for (int k = 0; k < responseDepth.data.a.Count; k++)
                        {
                            decimal priceLevel = responseDepth.data.a[k][0].ToDecimal();

                            for (int j = 0; j < marketDepth.Asks.Count; j++)
                            {
                                if (marketDepth.Asks[j].Price == priceLevel)
                                {
                                    if (responseDepth.data.a[k][1].ToDecimal() == 0)
                                    {
                                        marketDepth.Asks.RemoveAt(j);
                                    }
                                    else
                                    {
                                        marketDepth.Asks[j].Ask = responseDepth.data.a[k][1].ToDecimal();
                                    }
                                }
                                else if (j == 0 && priceLevel < marketDepth.Asks[j].Price
                                   && responseDepth.data.a[k][1].ToDecimal() != 0)
                                {
                                    marketDepth.Asks.Insert(j, new MarketDepthLevel()
                                    {
                                        Ask = responseDepth.data.a[k][1].ToDecimal(),
                                        Price = responseDepth.data.a[k][0].ToDecimal()
                                    });
                                }
                                else if (j != marketDepth.Asks.Count - 1 && priceLevel > marketDepth.Asks[j].Price 
                                    && priceLevel < marketDepth.Asks[j + 1].Price
                                    && responseDepth.data.a[k][1].ToDecimal() != 0)
                                {
                                    marketDepth.Asks.Insert(j + 1, new MarketDepthLevel()
                                    {
                                        Ask = responseDepth.data.a[k][1].ToDecimal(),
                                        Price = responseDepth.data.a[k][0].ToDecimal()
                                    });
                                }
                                else if (j == marketDepth.Asks.Count - 1 && priceLevel > marketDepth.Asks[j].Price
                                    && responseDepth.data.a[k][1].ToDecimal() != 0)
                                {
                                    marketDepth.Asks.Add(new MarketDepthLevel()
                                    {
                                        Ask = responseDepth.data.a[k][1].ToDecimal(),
                                        Price = responseDepth.data.a[k][0].ToDecimal()
                                    });
                                }

                                if (marketDepth.Bids != null && marketDepth.Bids.Count > 2
                                    && priceLevel < marketDepth.Bids[0].Price)
                                {
                                    marketDepth.Bids.RemoveAt(0);
                                }
                            }
                        }
                    }

                    if (responseDepth.data.b != null
                        && responseDepth.data.b.Count > 0)
                    {
                        for (int k = 0; k < responseDepth.data.b.Count; k++)
                        {
                            decimal priceLevel = responseDepth.data.b[k][0].ToDecimal();

                            for (int j = 0; j < marketDepth.Bids.Count; j++)
                            {
                                if (marketDepth.Bids[j].Price == priceLevel)
                                {
                                    if (responseDepth.data.b[k][1].ToDecimal() == 0)
                                    {
                                        marketDepth.Bids.RemoveAt(j);
                                    }
                                    else
                                    {
                                        marketDepth.Bids[j].Bid = responseDepth.data.b[k][1].ToDecimal();
                                    }
                                }
                                else if (j == 0 && priceLevel > marketDepth.Bids[j].Price
                                    && responseDepth.data.b[k][1].ToDecimal() != 0)
                                {
                                    marketDepth.Bids.Insert(j, new MarketDepthLevel()
                                    {
                                        Bid = responseDepth.data.b[k][1].ToDecimal(),
                                        Price = responseDepth.data.b[k][0].ToDecimal()
                                    });
                                }
                                else if (j != marketDepth.Bids.Count - 1 && priceLevel < marketDepth.Bids[j].Price && priceLevel > marketDepth.Bids[j + 1].Price
                                    && responseDepth.data.b[k][1].ToDecimal() != 0)
                                {
                                    marketDepth.Bids.Insert(j + 1, new MarketDepthLevel()
                                    {
                                        Bid = responseDepth.data.b[k][1].ToDecimal(),
                                        Price = responseDepth.data.b[k][0].ToDecimal()
                                    });
                                }
                                else if (j == marketDepth.Bids.Count - 1 && priceLevel < marketDepth.Bids[j].Price
                                    && responseDepth.data.b[k][1].ToDecimal() != 0)
                                {
                                    marketDepth.Bids.Add(new MarketDepthLevel()
                                    {
                                        Bid = responseDepth.data.b[k][1].ToDecimal(),
                                        Price = responseDepth.data.b[k][0].ToDecimal()
                                    });
                                }

                                if (marketDepth.Asks != null && marketDepth.Asks.Count > 2
                                    && priceLevel > marketDepth.Asks[0].Price)
                                {
                                    marketDepth.Asks.RemoveAt(0);
                                }
                            }
                        }
                    }

                    while (marketDepth.Asks.Count > 20)
                    {
                        marketDepth.Asks.RemoveAt(20);
                    }

                    while (marketDepth.Bids.Count > 20)
                    {
                        marketDepth.Bids.RemoveAt(20);
                    }

                    if (marketDepth.Asks.Count < 5
                        || marketDepth.Bids.Count < 5)
                    {
                        return;
                    }

                    marketDepth.Time = ServerTime;  //TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(lastUpdateTime));

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

                    if (MarketDepthEvent != null)
                    {
                        MarketDepthEvent(marketDepth.GetCopy());
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage("UpdateDepth error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            private void UpdateMyTrade(string json)
            {
                try
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

                        string commissionSecName = responseT.feeCurrency;

                        if (myTrade.SecurityNameCode.StartsWith(commissionSecName))
                        {
                            myTrade.Volume = responseT.quantity.ToDecimal() - responseT.fee.ToDecimal();

                            int decimalVolum = GetDecimalsVolume(myTrade.SecurityNameCode);
                            if (decimalVolum > 0)
                            {
                                myTrade.Volume = Math.Floor(myTrade.Volume * (decimal)Math.Pow(10, decimalVolum)) / (decimal)Math.Pow(10, decimalVolum);
                            }
                        }
                        else
                        {
                            myTrade.Volume = responseT.quantity.ToDecimal();
                        }

                        MyTradeEvent?.Invoke(myTrade);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage("UpdateMyTrade error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            private int GetDecimalsVolume(string security)
            {
                for (int i = 0; i < _securities.Count; i++)
                {
                    if (security == _securities[i].Name)
                    {
                        return _securities[i].DecimalsVolume;
                    }
                }

                return 0;
            }

            private void UpdatePortfolio(string message)
            {
                try
                {
                    ResponseWebSocketMessageAction<ResponseWebSocketPortfolio> Portfolio = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketPortfolio>());

                    Portfolio portfolio = _portfolios[0];

                    PositionOnBoard pos = new PositionOnBoard();

                    pos.PortfolioName = "XTSpotPortfolio";
                    pos.SecurityNameCode = Portfolio.data.s;
                    pos.ValueBlocked = Portfolio.data.f.ToDecimal();
                    pos.ValueCurrent = Portfolio.data.b.ToDecimal();

                    portfolio.SetNewPosition(pos);

                    PortfolioEvent(_portfolios);
                }
                catch (Exception exception)
                {
                    SendLogMessage("UpdatePortfolio error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            private void UpdateOrder(string message)
            {
                try
                {
                    ResponseWebSocketMessageAction<ResponseWebSocketOrder> Order = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketOrder>());

                    if (Order.data == null)
                    {
                        return;
                    }

                    ResponseWebSocketOrder item = Order.data;

                    OrderStateType stateType = GetOrderState(item.st);

                    if (item.tp.Equals("Market", StringComparison.OrdinalIgnoreCase)
                        && stateType == OrderStateType.Active)
                    {
                        return;
                    }

                    Order newOrder = new Order();
                    newOrder.SecurityNameCode = item.s;
                    long time = item.t == null ? Convert.ToInt64(item.ct) : Convert.ToInt64(item.t);
                    newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(time);
                    newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(time);

                    if (item.ci != null)
                    {
                        try
                        {
                            newOrder.NumberUser = Convert.ToInt32(item.ci) - 1000;
                        }
                        catch
                        {
                            SendLogMessage("Strange order num: " + item.ci, LogMessageType.Error);
                            return;
                        }
                    }

                    newOrder.NumberMarket = item.i;

                    OrderPriceType.TryParse(item.tp, true, out newOrder.TypeOrder);

                    newOrder.Side = item.sd.Equals("Buy", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;
                    newOrder.State = stateType;


                    if (newOrder.TypeOrder == OrderPriceType.Market && newOrder.Side == Side.Buy)
                    {
                        newOrder.Volume = item.eq.ToDecimal();
                        newOrder.Price = item.ap.ToDecimal(); //price
                    }
                    else
                    {
                        newOrder.Volume = item.oq?.ToDecimal() ?? 0;
                        newOrder.Price = item.p?.ToDecimal() ?? 0;
                    }

                    newOrder.ServerType = ServerType.XTSpot;
                    newOrder.PortfolioNumber = "XTSpotPortfolio";

                    MyOrderEvent?.Invoke(newOrder);

                    if (stateType == OrderStateType.Done || stateType == OrderStateType.Partial)
                    {
                        CreateQueryMyTrade(newOrder.SecurityNameCode, newOrder.NumberMarket, 1);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage("UpdateOrder error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            private OrderStateType GetOrderState(string orderStatusResponse)
            {
                OrderStateType stateType;

                switch (orderStatusResponse.ToLower())
                {
                    case ("new"):
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

            public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;

            #endregion

            #region 11 Trade

            private readonly RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(60));

            private readonly RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(100));

            public void SendOrder(Order order)
            {
                // https://doc.xt.com/#orderorderPost
                _rateGateSendOrder.WaitToProceed();

                try
                {
                    SendOrderRequestData data = new SendOrderRequestData();
                    data.symbol = order.SecurityNameCode;
                    data.clientOrderId = (order.NumberUser + 1000).ToString();
                    data.side = order.Side.ToString().ToUpper();
                    data.type = order.TypeOrder.ToString().ToUpper();
                    data.timeInForce = "GTC";
                    data.bizType = "SPOT";
                    data.price = order.TypeOrder == OrderPriceType.Market ? null : order.Price.ToString().Replace(",", ".");

                    if (order.TypeOrder == OrderPriceType.Limit)
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
                            //SendLogMessage($"Order num {order.NumberUser} on XT exchange.", LogMessageType.Trade);
                            //order.State = OrderStateType.Active;
                            //if (order.TypeOrder == OrderPriceType.Market)
                            //{
                            //    order.State = OrderStateType.Done;
                            //    CreateQueryMyTrade(order.SecurityNameCode, stateResponse.result.orderId, 1);
                            //}
                            //order.NumberMarket = stateResponse.result.orderId;

                            //MyOrderEvent?.Invoke(order);
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
                catch (Exception exception)
                {
                    SendLogMessage("SendOrder error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            public void ChangeOrderPrice(Order order, decimal newPrice)
            {

            }

            public void CancelAllOrders()
            {
                _rateGateCancelOrder.WaitToProceed();

                try
                {
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
                catch (Exception exception)
                {
                    SendLogMessage("CancelAllOrders error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            public void CancelAllOrdersToSecurity(Security security)
            {
                _rateGateCancelOrder.WaitToProceed();

                try
                {
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
                catch (Exception exception)
                {
                    SendLogMessage("CancelAllOrdersToSecurity error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            public void CancelOrder(Order order)
            {
                _rateGateCancelOrder.WaitToProceed();

                try
                {
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
                catch (Exception exception)
                {
                    SendLogMessage("CancelOrder error: " + exception.ToString(), LogMessageType.Error);
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

                if (orders == null || orders.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < orders.Count; i++)
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

                if (!string.IsNullOrEmpty(item.updateTime))
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
                    newOrder.NumberUser = Convert.ToInt32(item.clientOrderId) - 1000;
                }

                newOrder.NumberMarket = item.orderId;
                newOrder.Side = item.side.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;

                newOrder.State = type;
                newOrder.Volume = item.origQty.ToDecimal();
                newOrder.PortfolioNumber = "XTSpotPortfolio";

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

            private readonly RateGate _rateGateOpenOrder = new RateGate(1, TimeSpan.FromMilliseconds(100));

            private List<Order> GetAllOrdersFromExchange()
            {
                _rateGateOpenOrder.WaitToProceed();

                try
                {
                    string url = "/v4/open-order";
                    string query = "bizType=SPOT";

                    HttpResponseMessage res = CreatePrivateQuery(url, "GET", query);

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
                catch (Exception exception)
                {
                    SendLogMessage("GetAllOrdersFromExchange error: " + exception.ToString(), LogMessageType.Error);
                    return null;
                }
            }

            public void GetOrderStatus(Order order)
            {
                _rateGateOpenOrder.WaitToProceed();

                try
                {
                    // GET /v4/order
                    string url = "/v4/order";

                    string numberUser = (order.NumberUser + 1000).ToString();

                    string query = null;

                    query = string.IsNullOrEmpty(order.NumberMarket) ? $"clientOrderId={numberUser}" : $"orderId={order.NumberMarket}";

                    HttpResponseMessage res = CreatePrivateQuery(url, "GET", query);

                    string contentStr = res.Content.ReadAsStringAsync().Result;

                    if (res.StatusCode != HttpStatusCode.OK)
                    {
                        SendLogMessage(contentStr, LogMessageType.Error);
                        return;
                    }

                    ResponseMessageRest<OrderResponse> OrderResponse = JsonConvert.DeserializeAnonymousType(contentStr, new ResponseMessageRest<OrderResponse>());

                    if (OrderResponse.rc != "0")
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

                    if (myOrder.State == OrderStateType.Done
                       || myOrder.State == OrderStateType.Partial)
                    {
                        CreateQueryMyTrade(myOrder.SecurityNameCode, myOrder.NumberMarket, 1);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage("GetOrderStatus error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            #endregion

            #region 12 Queries

            private readonly string _baseUrl = "https://sapi.xt.com";

            private readonly string _timeOut = "5000";

            private readonly string _encry = "HmacSHA256";

            HttpClient _httpPublicClient = new HttpClient();

            private List<string> _subscribedSecurities = new List<string>();

            private readonly RateGate _rateGateGetToken = new RateGate(1, TimeSpan.FromMilliseconds(10000));

            private string GetListenKey()
            {
                _rateGateGetToken.WaitToProceed();

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
                            listenKey = stateResponse.result.accessToken;
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
                    _webSocketPublicMarketDepths.Send($"{{\"method\":\"subscribe\",\"params\":[\"depth_update@{security.Name}\", \"depth@{security.Name},{20}\"],\"id\":\"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");
                    _webSocketPublicTrades.Send($"{{\"method\":\"subscribe\",\"params\":[\"trade@{security.Name}\"],\"id\":\"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");
                }
            }

            private readonly RateGate _rateGateGetPortfolio = new RateGate(1, TimeSpan.FromMilliseconds(100));

            private void CreateQueryPortfolio(bool isUpdateValueBegin)
            {
                _rateGateGetPortfolio.WaitToProceed();

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
                try
                {
                    if (_portfolios == null)
                    {
                        return;
                    }

                    ResponseMessageRest<ResponseAssets> assets = JsonConvert.DeserializeAnonymousType(json, new ResponseMessageRest<ResponseAssets>());

                    Portfolio portfolio = _portfolios[0];

                    if (assets.result.totalUsdtAmount == "0")
                    {
                        portfolio.ValueBegin = 1;
                        portfolio.ValueCurrent = 1;
                    }
                    else
                    {
                        if (isUpdateValueBegin)
                        {
                            portfolio.ValueBegin = Math.Round(assets.result.totalUsdtAmount.ToDecimal(), 5);
                        }

                        portfolio.ValueCurrent = Math.Round(assets.result.totalUsdtAmount.ToDecimal(), 5);
                    }

                    List<PositionOnBoard> alreadySendPositions = new List<PositionOnBoard>();

                    for (int i = 0; i < assets.result.assets.Count; i++)
                    {
                        ResponseAsset item = assets.result.assets[i];

                        PositionOnBoard pos = new PositionOnBoard
                        {
                            PortfolioName = "XTSpotPortfolio",
                            SecurityNameCode = item.currency,
                            ValueBlocked = item.frozenAmount.ToDecimal(),
                            ValueCurrent = item.availableAmount.ToDecimal()
                        };

                        if (isUpdateValueBegin)
                        {
                            pos.ValueBegin = item.availableAmount.ToDecimal();
                        }

                        bool canSend = true;

                        for (int j = 0; j < alreadySendPositions.Count; j++)
                        {
                            if (!alreadySendPositions[j].SecurityNameCode.Equals(pos.SecurityNameCode, StringComparison.OrdinalIgnoreCase)
                                || pos.ValueCurrent != 0)
                                continue;

                            canSend = false;
                            break;
                        }

                        if (!canSend)
                        {
                            continue;
                        }

                        portfolio.SetNewPosition(pos);
                        alreadySendPositions.Add(pos);
                    }

                    PortfolioEvent(_portfolios);
                }
                catch (Exception exception)
                {
                    SendLogMessage("UpdatePortfolioRest error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            private readonly RateGate _rateGateGetMyTradeState = new RateGate(1, TimeSpan.FromMilliseconds(100));

            private void CreateQueryMyTrade(string nameSec, string OrdId, int serialStart)
            {
                _rateGateGetMyTradeState.WaitToProceed();

                try
                {
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
                catch (Exception exception)
                {
                    SendLogMessage("CreateQueryMyTrade error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            private readonly RateGate _rateGateCandleHistory = new RateGate(1, TimeSpan.FromMilliseconds(100));

            private List<Candle> CreateQueryCandles(string nameSec, string stringInterval, DateTime timeFrom, DateTime timeTo)
            {
                _rateGateCandleHistory.WaitToProceed();

                try
                {
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

                                newCandle.Open = item.o.ToDecimal();
                                newCandle.Close = item.c.ToDecimal();
                                newCandle.High = item.h.ToDecimal();
                                newCandle.Low = item.l.ToDecimal();
                                newCandle.Volume = item.v.ToDecimal();
                                newCandle.State = CandleState.Finished;
                                newCandle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.t));
                                candles.Add(newCandle);
                            }

                            return candles;
                        }

                        SendLogMessage($"CreateQueryCandles error, Code: {symbols.rc}\n"
                                       + $"Message code: {symbols.mc}", LogMessageType.Error);
                        return null;
                    }

                    SendLogMessage($"CreateQueryCandles error, State Code: {responseMessage.StatusCode}", LogMessageType.Error);
                }
                catch (Exception exception)
                {
                    SendLogMessage("CreateQueryCandles error: " + exception.ToString(), LogMessageType.Error);
                }
                return null;
            }

            private HttpResponseMessage CreatePrivateQuery(string path, string method, string queryString)
            {
                try
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
                catch (Exception exception)
                {
                    SendLogMessage("CreatePrivateQuery error: " + exception.ToString(), LogMessageType.Error);
                    return null;
                }
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
