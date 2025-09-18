using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.XT.XTFutures.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using OsEngine.Entity.WebSocketOsEngine;
using RestSharp;
using JsonConvert = Newtonsoft.Json.JsonConvert;
using Tinkoff.InvestApi.V1;
using Trade = OsEngine.Entity.Trade;
using Candle = OsEngine.Entity.Candle;
using Order = OsEngine.Entity.Order;







namespace OsEngine.Market.Servers.XT.XTFutures
{
    public class XTFuturesServer : AServer
    {
        public XTFuturesServer(int uniqueNumber)
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

            public void Connect(WebProxy proxy)
            {
                _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
                _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

                if (string.IsNullOrEmpty(_publicKey)
                   || string.IsNullOrEmpty(_secretKey))
                {
                    SendLogMessage("Can`t run XTFutures connector. No keys", LogMessageType.Error);
                    return;
                }

                try
                {
                    IRestResponse responseMessage = CreatePublicQuery("/future/public/client", "", Method.GET);

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
                catch (Exception exception)
                {
                    SendLogMessage("Dispose error" + exception.ToString(), LogMessageType.Error);
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
                get { return ServerType.XTFutures; }
            }

            public ServerConnectStatus ServerStatus { get; set; }

            public event Action ConnectEvent;

            public event Action DisconnectEvent;

            #endregion

            #region 2 Properties

            public List<IServerParameter> ServerParameters { get; set; }

            private string _publicKey;

            private string _secretKey;

            private string _listenKey; // lifetime 8 hours

            #endregion

            #region 3 Securities

            private List<Security> _securities;

            private readonly RateGate _rateGateSecurity = new RateGate(1, TimeSpan.FromMilliseconds(200));
            private string GetNameClass(string security)
            {
                if (security.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
                {
                    return "USDT";
                }

                return "Futures";
            }

            public void GetSecurities()
            {
                _rateGateSecurity.WaitToProceed();

                try
                {
                    IRestResponse response = CreatePublicQuery("/future/market/v3/public/symbol/list", "", Method.GET);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {

                        XTFuturesResponseRest<XTFuturesSymbolListResult> securityList = JsonConvert.DeserializeObject<XTFuturesResponseRest<XTFuturesSymbolListResult>>(response.Content);

                        if (securityList == null)
                        {
                            SendLogMessage("GetSecurities> Deserialization resulted in null", LogMessageType.Error);
                            return;
                        }

                        if (response.StatusCode == HttpStatusCode.OK && response != null)
                        {
                            if (securityList.returnCode.Equals("0") && securityList.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                            {

                                SendLogMessage("Securities loaded. Count: " + securityList.result.symbols.Count, LogMessageType.System);


                                List<Security> securities = new List<Security>();

                                for (int i = 0; i < securityList.result.symbols.Count; i++)
                                {
                                    string symbol = securityList.result.symbols[i].symbol;


                                    Security newSecurity = new Security();

                                    newSecurity.Exchange = ServerType.XTFutures.ToString();
                                    newSecurity.Name = symbol;
                                    newSecurity.NameFull = symbol;
                                    newSecurity.NameClass = GetNameClass(symbol);
                                    newSecurity.NameId = securityList.result.symbols[i].symbolGroupId;
                                    newSecurity.SecurityType = SecurityType.Futures;
                                    newSecurity.Lot = 1;
                                    if (securityList.result.symbols[i].tradeSwitch == "false")
                                    {
                                        continue;
                                    }
                                    newSecurity.State = SecurityStateType.Activ;
                                    newSecurity.PriceStep = securityList.result.symbols[i].minStepPrice.ToDecimal();
                                    newSecurity.Decimals = Convert.ToInt32(securityList.result.symbols[i].quoteCoinPrecision);
                                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                                    newSecurity.DecimalsVolume = Convert.ToInt32(securityList.result.symbols[i].quoteCoinPrecision);
                                    newSecurity.MinTradeAmount = securityList.result.symbols[i].minQty.ToDecimal();
                                    newSecurity.MinTradeAmountType = MinTradeAmountType.C_Currency;
                                    newSecurity.VolumeStep = newSecurity.DecimalsVolume.GetValueByDecimals();

                                    securities.Add(newSecurity);
                                }

                                SecurityEvent?.Invoke(securities);
                            }
                            else
                            {
                                SendLogMessage($"GetSecurities return code: {securityList.returnCode}\n"
                                               + $"Message Code: {securityList.msgInfo}", LogMessageType.Error);
                            }
                        }
                        else
                        {
                            SendLogMessage($"GetSecurities> Http State Code: {securityList.returnCode}",
                                LogMessageType.Error);

                            if (securityList != null && securityList.returnCode != null)
                            {
                                SendLogMessage($"Return Code: {securityList.returnCode}\n"
                                               + $"Message Code: {securityList.msgInfo}", LogMessageType.Error);
                            }
                        }
                    }
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
                portfolioInitial.Number = "XTFuturesPortfolio";
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

            #region 6 WebSocket creation

            private WebSocket _webSocketPrivate;

            private WebSocket _webSocketPublicMarketDepths;

            private WebSocket _webSocketPublicTrades;

            private readonly string _webSocketPrivateUrl = "wss://fstream.xt.com/ws/user";

            private readonly string _webSocketPublicUrl = "wss://fstream.xt.com/ws/market";

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

            private void _webSocketPublicTrades_OnOpen(object sender, EventArgs e)
            {

                CheckFullActivation();
                SendLogMessage("WebSocketPublicTrades Connection to public trades is Open", LogMessageType.System);

            }

            private void _webSocketPublicMarketDepths_OnError(object sender, ErrorEventArgs e)
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

            private void _webSocketPublicMarketDepths_OnOpen(object sender, EventArgs e)
            {

                CheckFullActivation();
                SendLogMessage("WebSocketPublic Connection to public data is Open", LogMessageType.System);

            }

            private void _webSocketPrivate_OnError(object sender, ErrorEventArgs e)
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

            private void _webSocketPrivate_OnOpen(object sender, EventArgs e)
            {
                try
                {
                    CheckFullActivation();
                    SendLogMessage("WebSocketPrivate Connection to private data is Open", LogMessageType.System);
                    _webSocketPrivate.Send($"{{\"method\":\"SUBSCRIBE\",\"params\":[\"balance@{_listenKey}\"],\"id\":\"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");

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

                        Thread.Sleep(10000);

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

            public void Subscribe(Security security)
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
                        var action = JsonConvert.DeserializeObject<XTFuturesResponseWebSocketMessageAction<object>>(message);

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

                        XTFuturesResponseWebSocketMessageAction<object> action = JsonConvert.DeserializeAnonymousType(message, new XTFuturesResponseWebSocketMessageAction<object>());

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

                        XTFuturesResponseWebSocketMessageAction<object> action = JsonConvert.DeserializeAnonymousType(message, new XTFuturesResponseWebSocketMessageAction<object>());

                        if (action == null || action.topic == null)
                            continue;

                        if (action.topic.Equals("order"))
                        {
                            UpdateOrder(message);
                            continue;
                        }

                        if (action.topic.Equals("balance"))
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
                try
                {
                    XTFuturesResponseWebSocketMessageAction<XTFuturesWsTrade> responseTrade = JsonConvert.DeserializeAnonymousType(message, new XTFuturesResponseWebSocketMessageAction<XTFuturesWsTrade>());

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
                    //trade.Side = responseTrade.data.b.Equals("true") ? Side.Buy : Side.Sell;

                    NewTradesEvent?.Invoke(trade);
                }
                catch (Exception exception)
                {
                    SendLogMessage("UpdateTrade error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            private List<MarketDepth> _marketDepths = new List<MarketDepth>();

            private DateTime _lastTimeMd = DateTime.MinValue;

            private bool startDepth = true;


            //    private void SnapshotDepth(string message, string symbol)
            //    {
            //        try
            //        {

            //        XTFuturesResponseWebSocketMessageAction<XTFuturesResponseWebSocketDepth> responseDepth = JsonConvert.DeserializeObject<XTFuturesResponseWebSocketMessageAction<XTFuturesResponseWebSocketDepth>>(responseMessage.Content);

            //        if (responseDepth?.data == null)
            //        {
            //            return;
            //        }

            //        MarketDepth marketDepth = null;

            //        for (int i = 0; i < _marketDepths.Count; i++)
            //        {
            //            if (_marketDepths[i].SecurityNameCode == responseDepth.ticker_id)
            //            {
            //                marketDepth = _marketDepths[i];
            //                break;
            //            }
            //        }

            //        if (startDepth)
            //        {
            //            if (marketDepth == null)
            //            {
            //                marketDepth = new MarketDepth();
            //                _marketDepths.Add(marketDepth);
            //            }
            //            else
            //            {
            //                marketDepth.Asks.Clear();
            //                marketDepth.Bids.Clear();
            //            }

            //            List<MarketDepthLevel> asks = new List<MarketDepthLevel>();
            //            List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

            //            marketDepth.SecurityNameCode = responseDepth.data.s;

            //            if (responseDepth.data.a != null)
            //            {
            //                for (int i = 0; i < responseDepth.data.a.Count; i++)
            //                {
            //                    asks.Add(new MarketDepthLevel()
            //                    {
            //                        Ask = responseDepth.data.a[i][1].ToDecimal(),
            //                        Price = responseDepth.data.a[i][0].ToDecimal(),
            //                    });
            //                }
            //            }

            //            if (responseDepth.data.b != null)
            //            {
            //                for (int i = 0; i < responseDepth.data.b.Count; i++)
            //                {
            //                    bids.Add(new MarketDepthLevel()
            //                    {
            //                        Bid = responseDepth.data.b[i][1].ToDecimal(),
            //                        Price = responseDepth.data.b[i][0].ToDecimal(),
            //                    });
            //                }
            //            }

            //            marketDepth.Asks = asks;
            //            marketDepth.Bids = bids;

            //            marketDepth.Time = ServerTime;

            //            if (marketDepth.Time < _lastTimeMd)
            //            {
            //                marketDepth.Time = _lastTimeMd;
            //            }
            //            else if (marketDepth.Time == _lastTimeMd)
            //            {
            //                _lastTimeMd = DateTime.FromBinary(_lastTimeMd.Ticks + 1);

            //                marketDepth.Time = _lastTimeMd;
            //            }
            //            _lastTimeMd = marketDepth.Time;

            //            if (MarketDepthEvent != null)
            //            {
            //                MarketDepthEvent(marketDepth.GetCopy());
            //            }

            //            startDepth = false;
            //        }
            //    }
            //    catch (Exception exception)
            //    {
            //        SendLogMessage("SnapshotDepth error: " + exception.ToString(), LogMessageType.Error);
            //}
            //     }

            //    private void UpdateDepth(string message)
            //    {
            //        try
            //        {
            //            XTFuturesResponseWebSocketMessageAction<XTFuturesResponseWebSocketDepthIncremental> responseDepth = JsonConvert.DeserializeObject<XTFuturesResponseWebSocketMessageAction<XTFuturesResponseWebSocketDepthIncremental>>(message);

            //            if (responseDepth?.data == null)
            //            {
            //                return;
            //            }

            //            if (_marketDepths == null
            //                || _marketDepths.Count == 0)
            //            {
            //                startDepth = true;
            //                return;
            //            }

            //            MarketDepth marketDepth = null;

            //            for (int i = 0; i < _marketDepths.Count; i++)
            //            {
            //                if (_marketDepths[i].SecurityNameCode == responseDepth.data.s)
            //                {
            //                    marketDepth = _marketDepths[i];
            //                    break;
            //                }
            //            }

            //            if (marketDepth == null)
            //            {
            //                startDepth = true;
            //                return;
            //            }

            //            if (marketDepth.Asks.Count == 0
            //                || marketDepth.Bids.Count == 0)
            //            {
            //                startDepth = true;
            //                return;
            //            }

            //            if (responseDepth.data.a != null
            //                    && responseDepth.data.a.Count > 0)
            //            {
            //                for (int k = 0; k < responseDepth.data.a.Count; k++)
            //                {
            //                    decimal priceLevel = responseDepth.data.a[k][0].ToDecimal();

            //                    for (int j = 0; j < marketDepth.Asks.Count; j++)
            //                    {
            //                        if (marketDepth.Asks[j].Price == priceLevel)
            //                        {
            //                            if (responseDepth.data.a[k][1].ToDecimal() == 0)
            //                            {
            //                                marketDepth.Asks.RemoveAt(j);
            //                            }
            //                            else
            //                            {
            //                                marketDepth.Asks[j].Ask = responseDepth.data.a[k][1].ToDecimal();
            //                            }
            //                        }
            //                        else if (j == 0 && priceLevel < marketDepth.Asks[j].Price
            //                           && responseDepth.data.a[k][1].ToDecimal() != 0)
            //                        {
            //                            marketDepth.Asks.Insert(j, new MarketDepthLevel()
            //                            {
            //                                Ask = responseDepth.data.a[k][1].ToDecimal(),
            //                                Price = responseDepth.data.a[k][0].ToDecimal()
            //                            });
            //                        }
            //                        else if (j != marketDepth.Asks.Count - 1 && priceLevel > marketDepth.Asks[j].Price
            //                            && priceLevel < marketDepth.Asks[j + 1].Price
            //                            && responseDepth.data.a[k][1].ToDecimal() != 0)
            //                        {
            //                            marketDepth.Asks.Insert(j + 1, new MarketDepthLevel()
            //                            {
            //                                Ask = responseDepth.data.a[k][1].ToDecimal(),
            //                                Price = responseDepth.data.a[k][0].ToDecimal()
            //                            });
            //                        }
            //                        else if (j == marketDepth.Asks.Count - 1 && priceLevel > marketDepth.Asks[j].Price
            //                            && responseDepth.data.a[k][1].ToDecimal() != 0)
            //                        {
            //                            marketDepth.Asks.Add(new MarketDepthLevel()
            //                            {
            //                                Ask = responseDepth.data.a[k][1].ToDecimal(),
            //                                Price = responseDepth.data.a[k][0].ToDecimal()
            //                            });
            //                        }

            //                        if (marketDepth.Bids != null && marketDepth.Bids.Count > 2
            //                            && priceLevel < marketDepth.Bids[0].Price)
            //                        {
            //                            marketDepth.Bids.RemoveAt(0);
            //                        }
            //                    }
            //                }
            //            }

            //            if (responseDepth.data.b != null
            //                && responseDepth.data.b.Count > 0)
            //            {
            //                for (int k = 0; k < responseDepth.data.b.Count; k++)
            //                {
            //                    decimal priceLevel = responseDepth.data.b[k][0].ToDecimal();

            //                    for (int j = 0; j < marketDepth.Bids.Count; j++)
            //                    {
            //                        if (marketDepth.Bids[j].Price == priceLevel)
            //                        {
            //                            if (responseDepth.data.b[k][1].ToDecimal() == 0)
            //                            {
            //                                marketDepth.Bids.RemoveAt(j);
            //                            }
            //                            else
            //                            {
            //                                marketDepth.Bids[j].Bid = responseDepth.data.b[k][1].ToDecimal();
            //                            }
            //                        }
            //                        else if (j == 0 && priceLevel > marketDepth.Bids[j].Price
            //                            && responseDepth.data.b[k][1].ToDecimal() != 0)
            //                        {
            //                            marketDepth.Bids.Insert(j, new MarketDepthLevel()
            //                            {
            //                                Bid = responseDepth.data.b[k][1].ToDecimal(),
            //                                Price = responseDepth.data.b[k][0].ToDecimal()
            //                            });
            //                        }
            //                        else if (j != marketDepth.Bids.Count - 1 && priceLevel < marketDepth.Bids[j].Price && priceLevel > marketDepth.Bids[j + 1].Price
            //                            && responseDepth.data.b[k][1].ToDecimal() != 0)
            //                        {
            //                            marketDepth.Bids.Insert(j + 1, new MarketDepthLevel()
            //                            {
            //                                Bid = responseDepth.data.b[k][1].ToDecimal(),
            //                                Price = responseDepth.data.b[k][0].ToDecimal()
            //                            });
            //                        }
            //                        else if (j == marketDepth.Bids.Count - 1 && priceLevel < marketDepth.Bids[j].Price
            //                            && responseDepth.data.b[k][1].ToDecimal() != 0)
            //                        {
            //                            marketDepth.Bids.Add(new MarketDepthLevel()
            //                            {
            //                                Bid = responseDepth.data.b[k][1].ToDecimal(),
            //                                Price = responseDepth.data.b[k][0].ToDecimal()
            //                            });
            //                        }

            //                        if (marketDepth.Asks != null && marketDepth.Asks.Count > 2
            //                            && priceLevel > marketDepth.Asks[0].Price)
            //                        {
            //                            marketDepth.Asks.RemoveAt(0);
            //                        }
            //                    }
            //                }
            //            }

            //            while (marketDepth.Asks.Count > 20)
            //            {
            //                marketDepth.Asks.RemoveAt(20);
            //            }

            //            while (marketDepth.Bids.Count > 20)
            //            {
            //                marketDepth.Bids.RemoveAt(20);
            //            }

            //            if (marketDepth.Asks.Count < 5
            //                || marketDepth.Bids.Count < 5)
            //            {
            //                return;
            //            }

            //            marketDepth.Time = ServerTime;  //TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(lastUpdateTime));

            //            if (marketDepth.Time < _lastTimeMd)
            //            {
            //                marketDepth.Time = _lastTimeMd;
            //            }
            //            else if (marketDepth.Time == _lastTimeMd)
            //            {
            //                _lastTimeMd = DateTime.FromBinary(_lastTimeMd.Ticks + 1);

            //                marketDepth.Time = _lastTimeMd;
            //            }
            //            _lastTimeMd = marketDepth.Time;

            //            if (MarketDepthEvent != null)
            //            {
            //                MarketDepthEvent(marketDepth.GetCopy());
            //            }
            //        }
            //        catch (Exception exception)
            //        {
            //            SendLogMessage("UpdateDepth error: " + exception.ToString(), LogMessageType.Error);
            //        }
            //    }
            // внутри XTServerSpotRealization

            private MarketDepth GetOrCreateDepth(string symbol)
            {
                for (int i = 0; i < _marketDepths.Count; i++)
                    if (_marketDepths[i].SecurityNameCode == symbol)
                        return _marketDepths[i];

                var md = new MarketDepth
                {
                    SecurityNameCode = symbol,
                    Asks = new List<MarketDepthLevel>(20),
                    Bids = new List<MarketDepthLevel>(20)
                };
                _marketDepths.Add(md);
                return md;
            }

            private  int CompareAsk(MarketDepthLevel x, MarketDepthLevel y) => x.Price.CompareTo(y.Price);
            private  int CompareBid(MarketDepthLevel x, MarketDepthLevel y) => y.Price.CompareTo(x.Price);
            // Линейный апсерт уровня: найти, обновить/удалить или вставить в нужное место.
            // Порядок поддерживается: asks по возрастанию цены, bids по убыванию.
            private void UpsertLevel(List<MarketDepthLevel> list, decimal price, decimal qty, bool isAsk)
            {
                // 1) ищем уровень по точной цене
                int idx = -1;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Price == price)
                    {
                        idx = i;
                        break;
                    }
                }

                if (idx >= 0)
                {
                    // нашли: qty==0 -> удаляем, иначе обновляем размер на нужной стороне
                    if (qty == 0m)
                    {
                        list.RemoveAt(idx);
                    }
                    else
                    {
                        var lvl = list[idx];
                        if (isAsk) lvl.Ask = qty; else lvl.Bid = qty;
                        list[idx] = lvl;
                    }
                    return;
                }

                // 2) уровня с такой ценой нет: если qty==0 — нечего вставлять
                if (qty == 0m) return;

                // 3) собираем новый уровень под нужную сторону
                var lvlNew = isAsk
                    ? new MarketDepthLevel { Price = price, Ask = qty }
                    : new MarketDepthLevel { Price = price, Bid = qty };

                // 4) линейно находим позицию вставки
                int pos = 0;
                while (pos < list.Count &&
                       (isAsk ? list[pos].Price < price   // asks: возрастающий порядок
                              : list[pos].Price > price)) // bids: убывающий порядок
                {
                    pos++;
                }

                // 5) вставляем и ограничиваем глубину до 20 уровней
                list.Insert(pos, lvlNew);
                if (list.Count > 20) list.RemoveAt(20);
            }


            private void ApplySnapshotSide(List<List<string>> side, List<MarketDepthLevel> dest, bool isAsk, int maxLevels = 20)
            {
                dest.Clear();
                if (side == null || side.Count == 0) return;


                int take = Math.Min(maxLevels, side.Count);
                for (int i = 0; i < take; i++)
                {
                    decimal p = side[i][0].ToDecimal();
                    decimal q = side[i][1].ToDecimal();
                    if (q <= 0m) continue;

                    dest.Add(isAsk
                        ? new MarketDepthLevel { Price = p, Ask = q }
                        : new MarketDepthLevel { Price = p, Bid = q });
                }

                if (isAsk) dest.Sort(CompareAsk);
                else dest.Sort(CompareBid);

                if (dest.Count > maxLevels)
                    dest.RemoveRange(maxLevels, dest.Count - maxLevels);
            }

            private void ApplyIncrementSide(List<List<string>> side, List<MarketDepthLevel> dest, bool isAsk)
            {
                if (side == null) return;

                for (int i = 0; i < side.Count; i++)
                {
                    decimal p = side[i][0].ToDecimal();
                    decimal q = side[i][1].ToDecimal();
                    UpsertLevel(dest, p, q, isAsk);
                }
            }


            private void SnapshotDepth(string message)
            {
                try
                {
                    var responseDepth =
                        JsonConvert.DeserializeObject<XTFuturesResponseWebSocketMessageAction<XTFuturesResponseWebSocketSnapshotDepth>>(message);
                    var d = responseDepth.data;
                    if (d == null) return;

                    var symbol = d.s;
                    if (string.IsNullOrWhiteSpace(symbol))
                    {
                        SendLogMessage("SnapshotDepth: empty symbol in payload", LogMessageType.Error);
                        return;
                    }

                    MarketDepth copyToFire = null;

                    lock (_mdLock)
                    {
                        var md = GetOrCreateDepth(symbol);
                        if (md.Asks == null) md.Asks = new List<MarketDepthLevel>(20);
                        if (md.Bids == null) md.Bids = new List<MarketDepthLevel>(20);


                        ApplySnapshotSide(d.a, md.Asks, isAsk: true, maxLevels: 20);
                        ApplySnapshotSide(d.b, md.Bids, isAsk: false, maxLevels: 20);


                        md.Time = ServerTime;
                        if (md.Time < _lastTimeMd) md.Time = _lastTimeMd;
                        else if (md.Time == _lastTimeMd)
                        {
                            _lastTimeMd = DateTime.FromBinary(_lastTimeMd.Ticks + 1);
                            md.Time = _lastTimeMd;
                        }
                        _lastTimeMd = md.Time;


                        _awaitingSnapshot.Remove(symbol);
                        startDepth = false;


                        if (_bufferBySymbol != null &&
                            _bufferBySymbol.TryGetValue(symbol, out var list) &&
                            list != null && list.Count > 0)
                        {
                            for (int i = 0; i < list.Count; i++)
                            {
                                var ev = list[i];
                                ApplyIncrementSide(ev.a, md.Asks, isAsk: true);
                                ApplyIncrementSide(ev.b, md.Bids, isAsk: false);
                            }
                            list.Clear();
                        }


                        copyToFire = md.GetCopy();
                    }


                    MarketDepthEvent?.Invoke(copyToFire);
                }
                catch (Exception ex)
                {
                    SendLogMessage("SnapshotDepth error: " + ex, LogMessageType.Error);
                }
            }

            private readonly object _mdLock = new object();
            private readonly Dictionary<string, List<XTFuturesResponseWebSocketUpdateDepth>> _bufferBySymbol =
                new Dictionary<string, List<XTFuturesResponseWebSocketUpdateDepth>>();
            private readonly HashSet<string> _awaitingSnapshot = new HashSet<string>();

            private void UpdateDepth(string message)
            {
                try
                {
                    var resp = JsonConvert.DeserializeObject
                       <XTFuturesResponseWebSocketMessageAction<XTFuturesResponseWebSocketUpdateDepth>>(message);

                    var d = resp?.data;
                    if (d == null) return;

                    var symbol = d.s;
                    if (string.IsNullOrWhiteSpace(symbol))
                    {
                        SendLogMessage("UpdateDepth: empty symbol in payload", LogMessageType.Error);
                        return;
                    }

                    MarketDepth copyToFire = null;

                    lock (_mdLock)
                    {
                        var md = GetOrCreateDepth(symbol);

                        bool needsSnapshot =
                               startDepth
                            || md.Asks == null || md.Bids == null
                            || md.Asks.Count == 0 || md.Bids.Count == 0
                            || _awaitingSnapshot.Contains(symbol);

                        if (needsSnapshot)
                        {
                            _awaitingSnapshot.Add(symbol);

                            if (!_bufferBySymbol.TryGetValue(symbol, out var list) || list == null)
                            {
                                list = new List<XTFuturesResponseWebSocketUpdateDepth>(32);
                                _bufferBySymbol[symbol] = list;
                            }
                            list.Add(d);
                        }
                        else
                        {
                            ApplyIncrementSide(d.a, md.Asks, isAsk: true);
                            ApplyIncrementSide(d.b, md.Bids, isAsk: false);

                            md.Time = ServerTime;
                            if (md.Time < _lastTimeMd) md.Time = _lastTimeMd;
                            else if (md.Time == _lastTimeMd)
                            {
                                _lastTimeMd = DateTime.FromBinary(_lastTimeMd.Ticks + 1);
                                md.Time = _lastTimeMd;
                            }
                            _lastTimeMd = md.Time;

                            copyToFire = md.GetCopy();
                        }
                    }

                    if (copyToFire != null)
                        MarketDepthEvent?.Invoke(copyToFire);
                }
                catch (Exception ex)
                {
                    SendLogMessage("UpdateDepth error: " + ex, LogMessageType.Error);
                }
            }

            private void UpdateMyTrade(string message)
            {
                try
                {
                    XTFuturesResponseMessageRest<XTFuturesResponseMyTrades> responseMyTrades = JsonConvert.DeserializeObject<XTFuturesResponseMessageRest<XTFuturesResponseMyTrades>>(message);

                    for (int i = 0; i < responseMyTrades.result.items.Count; i++)
                    {
                        XTFuturesResponseMyTrade responseT = responseMyTrades.result.items[i];

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
                    XTFuturesResponseWebSocketMessageAction<XTFuturesResponseWebSocketPortfolio> portfolios = JsonConvert.DeserializeObject<XTFuturesResponseWebSocketMessageAction<XTFuturesResponseWebSocketPortfolio>>(message);

                    Portfolio portfolio = _portfolios[0];

                    PositionOnBoard pos = new PositionOnBoard();

                    pos.PortfolioName = "XTFuturesPortfolio";
                    pos.SecurityNameCode = portfolios.data.coin;
                    pos.ValueBlocked = portfolios.data.openOrderMarginFrozen.ToDecimal();
                    pos.ValueCurrent = portfolios.data.walletBalance.ToDecimal();

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
                //try
                //{
                //    XTFuturesResponseWebSocketMessageAction<XTFuturesResponseWebSocketOrder> order = JsonConvert.DeserializeObject<XTFuturesResponseWebSocketMessageAction<XTFuturesResponseWebSocketOrder>>(message);

                //    if (order.data == null)
                //    {
                //        return;
                //    }

                //    XTFuturesResponseWebSocketOrder item = order.data;

                //    OrderStateType stateType = GetOrderState(item.state);

                //    if (item.type.Equals("Market", StringComparison.OrdinalIgnoreCase)
                //        && stateType == OrderStateType.Active)
                //    {
                //        return;
                //    }

                //    Order newOrder = new Order();
                //    newOrder.SecurityNameCode = item.symbol;
                //    newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createdTime));

                //    //newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(time);

                //    if (item.ci != null)
                //    {
                //        try
                //        {
                //            newOrder.NumberUser = Convert.ToInt32(item.ci);
                //        }
                //        catch
                //        {
                //            SendLogMessage("Strange order num: " + item.ci, LogMessageType.Error);
                //            return;
                //        }
                //    }

                //    newOrder.NumberMarket = item.orderId;

                //    OrderPriceType.TryParse(item.type, true, out newOrder.TypeOrder);

                //    newOrder.Side = item.orderSide.Equals("Buy", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;
                //    newOrder.State = stateType;


                //    if (newOrder.TypeOrder == OrderPriceType.Market && newOrder.Side == Side.Buy)
                //    {
                //        newOrder.Volume = item.origQty.ToDecimal();
                //        newOrder.Price = item.price.ToDecimal(); //price
                //    }
                //    else
                //    {
                //        newOrder.Volume = item.origQty?.ToDecimal() ?? 0;
                //        newOrder.Price = item.price?.ToDecimal() ?? 0;
                //    }

                //    newOrder.ServerType = ServerType.XTFutures;
                //    newOrder.PortfolioNumber = "XTFuturesPortfolio";

                //    MyOrderEvent?.Invoke(newOrder);

                //    if (stateType == OrderStateType.Done || stateType == OrderStateType.Partial)
                //    {
                //        // CreateQueryMyTrade(newOrder.SecurityNameCode, newOrder.NumberMarket, 1);
                //    }
                //}
                //catch (Exception exception)
                //{
                //    SendLogMessage("UpdateOrder error: " + exception.ToString(), LogMessageType.Error);
                //}
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

            public List<Order> GetActiveOrders(int startIndex, int count)
            {
                return null;
            }

            public List<Order> GetHistoricalOrders(int startIndex, int count)
            {
                return null;
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
                //_rateGateSendOrder.WaitToProceed();

                //try
                //{
                //    XTFuturesSendOrderRequestData data = new XTFuturesSendOrderRequestData();
                //    data.symbol = order.SecurityNameCode;
                //    data.clientOrderId = (order.NumberUser).ToString();
                //    data.orderSide = order.Side.ToString().ToUpper();
                //    data.orderType = order.TypeOrder.ToString().ToUpper();
                //    data.timeInForce = "GTC";
                //    //data.bizType = "";
                //    data.price = order.TypeOrder == OrderPriceType.Market ? null : order.Price.ToString().Replace(",", ".");

                //    if (order.TypeOrder == OrderPriceType.Limit)
                //    {
                //        data.price = order.Price.ToString().Replace(",", ".");
                //        data.origQty = order.Volume.ToString().Replace(",", ".");
                //    }
                //    else
                //    {
                //        data.timeInForce = "FOK";

                //        if (data.orderSide == "BUY")
                //        {
                //            data.quantity = null;
                //            data.origQty = (order.Volume * order.Price).ToString().Replace(",", ".");
                //        }
                //        else
                //        {
                //            data.origQty = null;
                //            data.quantity = order.Volume.ToString().Replace(",", ".");
                //        }
                //    }

                //    JsonSerializerSettings dataSerializerSettings = new JsonSerializerSettings();
                //    dataSerializerSettings.NullValueHandling = NullValueHandling.Ignore;

                //    string jsonRequest = JsonConvert.SerializeObject(data, dataSerializerSettings);

                //   IRestResponse responseMessage = CreatePrivateQuery("/future/trade/v1/order/create", "POST", jsonRequest, "", "","","");


                //    XTFuturesResponseMessageRest<XTFuturesSendOrderRequestData> stateResponse = JsonConvert.DeserializeObject(XTFuturesResponseMessageRest<XTFuturesSendOrderRequestData>(jsonRequest.con));

                //    if (responseMessage.StatusCode == HttpStatusCode.OK && stateResponse != null)
                //    {
                //        if (stateResponse.rc.Equals("0") && stateResponse.mc.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                //        {
                //            SendLogMessage($"Order num {order.NumberUser} on XT exchange.", LogMessageType.Trade);
                //            order.State = OrderStateType.Active;
                //            if (order.TypeOrder == OrderPriceType.Market)
                //            {
                //                order.State = OrderStateType.Done;
                //                CreateQueryMyTrade(order.SecurityNameCode, stateResponse.result.orderId, 1);
                //            }
                //            order.NumberMarket = stateResponse.result.orderId;

                //            //MyOrderEvent?.Invoke(order);
                //        }
                //        else
                //        {
                //            CreateOrderFail(order);
                //            SendLogMessage($"SendOrder Fail, Code: {stateResponse.rc}\n"
                //                + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                //        }
                //    }
                //    else
                //    {
                //        CreateOrderFail(order);
                //        SendLogMessage($"SendOrder> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                //        if (stateResponse != null && stateResponse.rc != null)
                //        {
                //            SendLogMessage($"SendOrder Fail, Code: {stateResponse.rc}\n"
                //                + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                //        }
                //    }
                //}
                //catch (Exception exception)
                //{
                //    SendLogMessage("SendOrder error: " + exception.ToString(), LogMessageType.Error);
                //}
            }

            public void ChangeOrderPrice(Order order, decimal newPrice)
            {

            }

            public void CancelAllOrders()
            {
                //_rateGateCancelOrder.WaitToProceed();

                //try
                //{
                //    IRestResponse responseMessage = CreatePrivateQuery("/future/trade/v1/order/cancel-all", Method.POST, "", "", "");
                //    XTFuturesResponseMessageRest<object> stateResponse = JsonConvert.DeserializeObject<XTFuturesResponseMessageRest<object>>(responseMessage.Content);

                //    if (responseMessage.StatusCode == HttpStatusCode.OK && stateResponse != null)
                //    {
                //        if (stateResponse.rc.Equals("0") && stateResponse.mc.Equals("SUCCESS", StringComparison.CurrentCulture))
                //        {
                //            // ignore
                //        }
                //        else
                //        {
                //            SendLogMessage($"CancelAllOrders error, Code: {stateResponse.rc}\n"
                //                           + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                //        }
                //    }
                //    else
                //    {
                //        SendLogMessage($"CancelAllOrders> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                //        if (stateResponse != null && stateResponse.rc != null)
                //        {
                //            SendLogMessage($"CancelAllOrders error, Code: {stateResponse.rc}\n"
                //                           + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                //        }
                //    }
                //}
                //catch (Exception exception)
                //{
                //    SendLogMessage("CancelAllOrders error: " + exception.ToString(), LogMessageType.Error);
                //}
            }

            //public void CancelAllOrdersToSecurity(Security security)
            //{
            //    _rateGateCancelOrder.WaitToProceed();

            //    try
            //    {
            //        XTFuturesCancelAllOrdersRequestData data = new XTFuturesCancelAllOrdersRequestData();
            //        data.symbol = security.Name;
            //        data.bizType = "SPOT";

            //        string jsonRequest = JsonConvert.SerializeObject(data);

            //        var responseMessage = CreatePrivateQuery("/v4/open-order", "DELETE", jsonRequest, "", "");
            //        string jsonResponse = responseMessage.Content.ReadAsStringAsync().Result;
            //        XTFuturesResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(jsonResponse, new XTFuturesResponseMessageRest<object>());

            //        if (responseMessage.StatusCode == HttpStatusCode.OK && stateResponse != null)
            //        {
            //            if (stateResponse.returnCode.Equals("0") && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.CurrentCulture))
            //            {
            //                // ignore
            //            }
            //            else
            //            {
            //                SendLogMessage($"CancelAllOrdersToSecurity error, Code: {stateResponse.returnCode}\n"
            //                    + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
            //            }
            //        }
            //        else
            //        {
            //            SendLogMessage($"CancelAllOrdersToSecurity> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

            //            if (stateResponse != null && stateResponse.returnCode != null)
            //            {
            //                SendLogMessage($"CancelAllOrdersToSecurity error, Code: {stateResponse.returnCode}\n"
            //                    + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
            //            }
            //        }
            //    }
            //    catch (Exception exception)
            //    {
            //        SendLogMessage("CancelAllOrdersToSecurity error: " + exception.ToString(), LogMessageType.Error);
            //    }
            // }

            public bool CancelOrder(Order order)
            {
                //_rateGateCancelOrder.WaitToProceed();

                //try
                //{
                //   IRestResponse responseMessage = CreatePrivateQuery("/future/trade/v1/order/cancel" + order.NumberMarket, Method.POST, "", "", "");

                //    XTFuturesResponseMessageRest<XTFuturesCancaledOrderResponse> stateResponse = JsonConvert.DeserializeObject<XTFuturesResponseMessageRest<XTFuturesCancaledOrderResponse>>(responseMessage.Content);

                //    if (responseMessage.StatusCode == HttpStatusCode.OK && stateResponse != null)
                //    {
                //        if (stateResponse.rc.Equals("0") && stateResponse.mc.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                //        {
                //            SendLogMessage($"Successfully canceled the order, order Id: {stateResponse.result.cancelId}", LogMessageType.Trade);
                //            return true;
                //        }
                //        else
                //        {
                //            GetOrderStatus(order);
                //            SendLogMessage($"CancelOrder error, Code: {stateResponse.rc}\n"
                //                + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                //        }
                //    }
                //    else
                //    {
                //        GetOrderStatus(order);
                //        SendLogMessage($"CancelOrder> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                //        if (stateResponse != null && stateResponse.rc != null)
                //        {
                //            SendLogMessage($"CancelOrder error, Code: {stateResponse.rc}\n"
                //                + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                //        }
                //    }
                //}
                //catch (Exception exception)
                //{
                //    SendLogMessage("CancelOrder error: " + exception.ToString(), LogMessageType.Error);
                //}

                return false;
            }

            private void CreateOrderFail(Order order)
            {
                order.State = OrderStateType.Fail;
                MyOrderEvent?.Invoke(order);
            }

            public void GetAllActivOrders()
            {
                //    List<Order> orders = GetAllOrdersFromExchange();

                //    if (orders == null || orders.Count == 0)
                //    {
                //        return;
                //    }

                //    for (int i = 0; i < orders.Count; i++)
                //    {
                //        orders[i].TimeCreate = orders[i].TimeCallBack;
                //        MyOrderEvent?.Invoke(orders[i]);
                //    }
            }

            private Order OrderUpdate(XTFuturesOrderResponse orderResponse, OrderStateType type)
            {
                XTFuturesOrderResponse item = orderResponse;

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
                newOrder.PortfolioNumber = "XTFuturesPortfolio";

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

                newOrder.ServerType = ServerType.XTFutures;

                return newOrder;
            }

            private readonly RateGate _rateGateOpenOrder = new RateGate(1, TimeSpan.FromMilliseconds(100));

            //private List<Order> GetAllOrdersFromExchange()
            //{
            //    _rateGateOpenOrder.WaitToProceed();

            //    try
            //    {
            //        //        string url = "/v4/open-order";
            //        //        string query = "bizType=SPOT";

            //        //        var res = CreatePrivateQuery(url, "GET", query, "", "");

            //        //        string contentStr = res.Content.ReadAsStringAsync().Result;

            //        //        if (res.StatusCode != HttpStatusCode.OK)
            //        //        {
            //        //            SendLogMessage(contentStr, LogMessageType.Error);
            //        //            return null;
            //        //        }

            //        //        XTFuturesResponseMessageRest<List<XTFuturesOrderResponse>> OrderResponse = JsonConvert.DeserializeAnonymousType(contentStr, new XTFuturesResponseMessageRest<List<XTFuturesOrderResponse>>());

            //        //        if (OrderResponse.returnCode != "0")
            //        //        {
            //        //            SendLogMessage($"GetAllOrdersFromExchange> error, Code: {OrderResponse.returnCode}\n"
            //        //                + $"Message code: {OrderResponse.msgInfo}", LogMessageType.Error);
            //        //            return null;
            //        //        }

            //        //        List<Order> orders = new List<Order>();

            //        //        for (int i = 0; i < OrderResponse.result.Count; i++)
            //        //        {
            //        //            Order newOrder = null;

            //        //            newOrder = OrderUpdate(OrderResponse.result[i], GetOrderState(OrderResponse.result[i].state));

            //        //            if (newOrder == null)
            //        //            {
            //        //                continue;
            //        //            }

            //        //            orders.Add(newOrder);
            //        //        }

            //        // return orders;
            //        return null;//убрать
            //    }
            //    catch (Exception exception)
            //    {
            //        SendLogMessage("GetAllOrdersFromExchange error: " + exception.ToString(), LogMessageType.Error);
            //        return null;
            //    }
            //}

            public OrderStateType GetOrderStatus(Order order)
            {
                //    _rateGateOpenOrder.WaitToProceed();

                //    try
                //    {

                //        string numberUser = (order.NumberUser + 1000).ToString();

                //        string query = null;

                //        query = string.IsNullOrEmpty(order.NumberMarket) ? $"clientOrderId={numberUser}" : $"orderId={order.NumberMarket}";

                //        var res = CreatePrivateQuery("/future/trade/v1/order/detail", Method.GET, query, "", "");

                //        string contentStr = res.Content.ReadAsStringAsync().Result;

                //        if (res.StatusCode != HttpStatusCode.OK)
                //        {
                //            SendLogMessage(contentStr, LogMessageType.Error);
                //            return OrderStateType.None;
                //        }

                //        XTFuturesResponseMessageRest<XTFuturesOrderResponse> OrderResponse = JsonConvert.DeserializeAnonymousType(contentStr, new XTFuturesResponseMessageRest<XTFuturesOrderResponse>());

                //        if (OrderResponse.returnCode != "0")
                //        {
                //            SendLogMessage($"GetOrderStatus error, code: {OrderResponse.returnCode}\n"
                //                + $"Message code: {OrderResponse.msgInfo}", LogMessageType.Error);
                //            return OrderStateType.None;
                //        }

                //        Order newOrder = OrderUpdate(OrderResponse.result, GetOrderState(OrderResponse.result.state));

                //        if (newOrder == null)
                //        {
                //            return OrderStateType.None;
                //        }

                //        Order myOrder = newOrder;

                //        MyOrderEvent?.Invoke(myOrder);

                //        if (myOrder.State == OrderStateType.Done
                //           || myOrder.State == OrderStateType.Partial)
                //        {
                //            CreateQueryMyTrade(myOrder.SecurityNameCode, myOrder.NumberMarket, 1);
                //        }

                //        return myOrder.State;
                //    }
                //    catch (Exception exception)
                //    {
                //        SendLogMessage("GetOrderStatus error: " + exception.ToString(), LogMessageType.Error);
                //    }

                return OrderStateType.None;
            }

            #endregion

            #region 12 Queries

            private readonly string _baseUrl = "https://fapi.xt.com";

            private readonly string _timeOut = "50000";

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
                    IRestResponse responseMessage = CreatePrivateQuery("/future/user/v1/user/listen-key",
                        Method.GET,
                        null, null,
                        BodyKind.None,
                        null, null,
                        null);

                    ListenKeyResponse stateResponse = JsonConvert.DeserializeObject<ListenKeyResponse>(responseMessage.Content);

                    if (stateResponse?.returnCode != "0" || !string.IsNullOrEmpty(stateResponse?.error?.code))
                    {
                        SendLogMessage($"API error: returnCode={stateResponse?.returnCode}, code={stateResponse?.error?.code}, msg={stateResponse?.error?.msg}, msgInfo={stateResponse?.msgInfo}", LogMessageType.Error);

                    }

                    if (responseMessage.StatusCode == HttpStatusCode.OK && stateResponse != null)
                    {
                        if (stateResponse.returnCode.Equals("0") && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                        {
                            SendLogMessage($"ListenKey successfully received.", LogMessageType.Connect);
                            listenKey = stateResponse?.result;
                        }
                        else
                        {
                            SendLogMessage($"GetListenKey error, Code: {stateResponse.returnCode}\n"
                                           + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Receiving Token> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                        if (stateResponse != null && stateResponse.returnCode != null)
                        {
                            SendLogMessage($"GetListenKey error, Code: {stateResponse.returnCode}\n"
                                           + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
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
                    IRestResponse response = CreatePrivateQuery("/future/user/v1/compat/balance/list", Method.GET,
                        null, null,
                        BodyKind.None,
                        null, null,
                        null);
                    if (response == null) { return; }

                    string json = response.Content;
                    var сode = response.StatusCode;

                    XTFuturesResponseRestNew<List<XTFuturesBalance>> stateResponse =
                    JsonConvert.DeserializeObject<XTFuturesResponseRestNew<List<XTFuturesBalance>>>(response.Content);

                    if (response.StatusCode == HttpStatusCode.OK && stateResponse != null)
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
                        SendLogMessage($"CreateQueryPortfolio> Http State Code: {response.StatusCode}", LogMessageType.Error);

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

                    XTFuturesResponseRestNew<List<XTFuturesBalance>> assets =
                               JsonConvert.DeserializeObject<XTFuturesResponseRestNew<List<XTFuturesBalance>>>(json);

                    if (assets.result == null || assets.result.Count == 0)
                    {
                        return;
                    }

                    Portfolio portfolio = _portfolios[0];

                    int idx = 0;
                    for (int i = 0; i < assets.result.Count; i++)
                    {
                        if (string.Equals(assets.result[i].coin, "usdt", StringComparison.OrdinalIgnoreCase))
                        { idx = i; break; }
                    }
                    //if (assets.result[i].walletBalance == "0")
                    //{
                    //    portfolio.ValueBegin = 1;
                    //    portfolio.ValueCurrent = 1;
                    //}
                    //else
                    //{
                    //    if (isUpdateValueBegin)
                    //    {
                    //        portfolio.ValueBegin = Math.Round(assets.result.walletBalance.ToDecimal(), 5);
                    //    }

                    //    portfolio.ValueCurrent = Math.Round(assets.result.totalAmount.ToDecimal(), 5);
                    //}

                    List<PositionOnBoard> alreadySendPositions = new List<PositionOnBoard>();

                    for (int i = 0; i < assets.result.Count; i++)
                    {
                        PositionOnBoard pos = new PositionOnBoard
                        {
                            PortfolioName = "XTFuturesPortfolio",
                            SecurityNameCode = assets.result[i].coin,
                            ValueBlocked = assets.result[i].openOrderMarginFrozen.ToDecimal(),
                            ValueCurrent = assets.result[i].walletBalance.ToDecimal()
                        };

                        if (isUpdateValueBegin)
                        {
                            pos.ValueBegin = assets.result[i].walletBalance.ToDecimal();
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
                //     _rateGateGetMyTradeState.WaitToProceed();

                //     try
                //     {
                //         string queryString = $"orderId={OrdId}";
                //         int startCount = serialStart;

                //         var response = CreatePrivateQuery("/future/trade/v1/order/trade-list", Method.GET, queryString,null, null,
                //             BodyKind.None,
                //             null, null );


                //         var code = response.StatusCode;
                //         string json = response.Content ?? "";

                //         XTFuturesResponseMessageRest<XTFuturesResponseMyTrades> stateResponse =
                //JsonConvert.DeserializeObject<XTFuturesResponseMessageRest<XTFuturesResponseMyTrades>>(json);

                //         if (response.StatusCode == HttpStatusCode.OK && stateResponse != null)
                //         {
                //             if (stateResponse.rc.Equals("0") && stateResponse.mc.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                //             {
                //                 XTFuturesResponseMessageRest<XTFuturesResponseMyTrades> responseMyTrades = JsonConvert.DeserializeObject<XTFuturesResponseMessageRest<XTFuturesResponseMyTrades>(response.Content);

                //                 if (responseMyTrades.result.items.Count == 0)
                //                 {
                //                     if (startCount >= 4)
                //                     {
                //                         SendLogMessage($"Failed {startCount} attempts to receive trades for order #{OrdId}", LogMessageType.Error);
                //                         return;
                //                     }

                //                     Thread.Sleep(200 * startCount);
                //                     startCount++;
                //                     CreateQueryMyTrade(nameSec, OrdId, startCount);
                //                 }

                //                 UpdateMyTrade(jsonResponse);
                //             }
                //             else
                //             {
                //                 SendLogMessage($"CreateQueryMyTrade error, Code: {stateResponse.rc}\n"
                //                     + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                //             }
                //         }
                //         else
                //         {
                //             SendLogMessage($"CreateQueryMyTrade> Http State Code: {rc.StatusCode}", LogMessageType.Error);

                //             if (stateResponse != null && stateResponse.returnCode != null)
                //             {
                //                 SendLogMessage($"CreateQueryMyTrade error, Code: {stateResponse.rc}\n"
                //                     + $"Message code: {stateResponse.mc}", LogMessageType.Error);
                //             }
                //         }
                //     }
                //     catch (Exception exception)
                //     {
                //         SendLogMessage("CreateQueryMyTrade error: " + exception.ToString(), LogMessageType.Error);
                //     }
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

                    string param = "symbol=" + nameSec
                                 + "&interval=" + stringInterval
                                 + "&startTime=" + startTime
                                 + "&endTime=" + endTime
                                 + "&limit=" + limit;


                    IRestResponse responseMessage = CreatePublicQuery("/future/market/v1/public/q/kline", param, Method.GET);

                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        XTFuturesResponseRest<List<XTFuturesResponseCandle>> symbols = JsonConvert.DeserializeObject<XTFuturesResponseRest<List<XTFuturesResponseCandle>>>(responseMessage.Content);

                        if (symbols.returnCode.Equals("0") && symbols.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                        {
                            List<Candle> candles = new List<Candle>();

                            for (int i = 0; i < symbols.result.Count; i++)
                            {
                                XTFuturesResponseCandle item = symbols.result[i];

                                Candle newCandle = new Candle();

                                newCandle.Open = item.o.ToDecimal();
                                newCandle.Close = item.c.ToDecimal();
                                newCandle.High = item.h.ToDecimal();
                                newCandle.Low = item.l.ToDecimal();
                                newCandle.Volume = item.a.ToDecimal();
                                newCandle.State = CandleState.Finished;
                                newCandle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.t));
                                candles.Add(newCandle);
                            }

                            return candles;
                        }

                        SendLogMessage($"CreateQueryCandles error, Code: {symbols.returnCode}\n"
                                       + $"Message code: {symbols.msgInfo}", LogMessageType.Error);
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

            IRestResponse CreatePublicQuery(string path, string parameters, Method method)
            {
                RestClient client = new RestClient(_baseUrl);

                // GET — параметры в query
                if (method == Method.GET && !string.IsNullOrEmpty(parameters))
                {
                    path += "?" + parameters;
                }

                RestRequest request = new RestRequest(path, method);

                request.AddHeader("Accept", "application/json");
                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");

                // POST — параметры в тело
                if (method == Method.POST && !string.IsNullOrEmpty(parameters))
                {
                    request.AddParameter("application/x-www-form-urlencoded", parameters, ParameterType.RequestBody);
                }

                IRestResponse response = client.Execute(request);
                return response;
            }
            enum BodyKind { None, FormUrlEncoded, Json }

            private string BuildQuery(string[] keys, string[] values)
            {
                string[] k = (string[])keys.Clone();
                string[] v = (string[])values.Clone();

                // сортировка по ключам (Ordinal)
                Array.Sort(k, v, StringComparer.Ordinal);

                StringBuilder sb = new StringBuilder();
                int i = 0;
                while (i < k.Length)
                {
                    if (i > 0) sb.Append('&');
                    string ek = Uri.EscapeDataString(k[i] ?? "");
                    string ev = Uri.EscapeDataString(v[i] ?? "");
                    sb.Append(ek).Append('=').Append(ev);
                    i++;
                }
                return sb.ToString();
            }
            private string HmacSHA256(string key, string data)
            {
                using (var h = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
                {
                    var hash = h.ComputeHash(Encoding.UTF8.GetBytes(data));
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            private string _recvWindow = null;
            private string BuildX(string ts)
            {
                return "validate-algorithms=" + _encry
                     + "&validate-appkey=" + _publicKey
                     + "&validate-recvwindow=" + _timeOut
                     + "&validate-timestamp=" + ts;
            }

            private string BuildY(string method, string path, string query, string body)
            {
                var sb = new StringBuilder();
                sb.Append('#').Append(method).Append('#').Append(path);
                if (!string.IsNullOrEmpty(query)) sb.Append('#').Append(query);
                if (!string.IsNullOrEmpty(body)) sb.Append('#').Append(body);
                return sb.ToString();
            }
            private IRestResponse CreatePrivateQuery(string path,
        Method httpMethod,
        // query: пары одинаковой длины; можно передать null/пустые
        string[] queryKeys, string[] queryValues,
        // body-kind: None / FormUrlEncoded / Json
        BodyKind bodyKind,
        // form body (если bodyKind == FormUrlEncoded): пары одинаковой длины; иначе передать null
        string[] bodyKeys, string[] bodyValues,
        // json body (если bodyKind == Json): уже готовая JSON-строка; иначе передать null/""
        string jsonBody)
            {

                // 1) Канонизируем query
                string queryString = "";
                if (queryKeys != null && queryValues != null && queryKeys.Length > 0)
                    queryString = BuildQuery(queryKeys, queryValues);

                // 2) Канонизируем body
                string bodyString = "";
                string contentType = "application/x-www-form-urlencoded";
                if (bodyKind == BodyKind.FormUrlEncoded)
                {
                    if (bodyKeys != null && bodyValues != null && bodyKeys.Length > 0)
                        bodyString = BuildQuery(bodyKeys, bodyValues); // form = k=v&... (сортированный)
                }
                else if (bodyKind == BodyKind.Json)
                {
                    contentType = "application/json";
                    if (!string.IsNullOrEmpty(jsonBody))
                        bodyString = jsonBody; // JSON — ровно как есть
                }

                // 3) Собираем X и Y для подписи
                string ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                // X: только appkey + timestamp (упрощённая схема v1)
                string X = "validate-appkey=" + _publicKey + "&validate-timestamp=" + ts;

                // Y: #path[#query][#body]
                StringBuilder yb = new StringBuilder();
                yb.Append('#').Append(path);
                if (!string.IsNullOrEmpty(queryString)) yb.Append('#').Append(queryString);
                if (!string.IsNullOrEmpty(bodyString)) yb.Append('#').Append(bodyString);
                string Y = yb.ToString();

                string signInput = X + Y;
                string signature = HmacSHA256(_secretKey, signInput);

                // 4) Собираем запрос
                string url = _baseUrl;
                if (!string.IsNullOrEmpty(queryString))
                    url = url + path + "?" + queryString;
                else
                    url = url + path;

                var client = new RestClient(url);
                var request = new RestRequest(httpMethod);

                // Заголовки подписи
                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                request.AddHeader("Accept", "application/json");
                request.AddHeader("Content-Type", contentType);
                request.AddHeader("validate-appkey", _publicKey);
                request.AddHeader("validate-timestamp", ts);
                request.AddHeader("validate-signature", signature);
                // опционально
                request.AddHeader("validate-algorithms", "HmacSHA256");
                if (!string.IsNullOrEmpty(_recvWindow))
                    request.AddHeader("validate-recvwindow", _recvWindow);

                // Тело — только если есть body
                if (bodyKind == BodyKind.FormUrlEncoded && bodyString.Length > 0)
                    request.AddParameter("application/x-www-form-urlencoded", bodyString, ParameterType.RequestBody);
                else if (bodyKind == BodyKind.Json && bodyString.Length > 0)
                    request.AddParameter("application/json", bodyString, ParameterType.RequestBody);

                return client.Execute(request);
            }
            //private IRestResponse CreatePrivateQuery(string path, string method, string query, string body, string contentType)
            //{
            //    string urlPath = string.IsNullOrEmpty(query) ? path : (path + "?" + query);
            //    string ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

            //    string X = BuildX(ts);
            //    string Y = BuildY(method, path, query, body);
            //    string signature = HmacSHA256(_secretKey, X + Y);

            //    var client = new RestClient(_baseUrl);
            //    Method httpMethod = method == "POST" ? Method.POST : (method == "DELETE" ? Method.DELETE : Method.GET);
            //    var req = new RestRequest(urlPath, httpMethod);

            //    req.AddHeader("Accept", "application/json");
            //    req.AddHeader("validate-algorithms", _encry);
            //    req.AddHeader("validate-appkey", _publicKey);
            //    req.AddHeader("validate-recvwindow", _timeOut);
            //    req.AddHeader("validate-timestamp", ts);
            //    req.AddHeader("validate-signature", signature);

            //    if (httpMethod != Method.GET && !string.IsNullOrEmpty(body))
            //    {
            //        if (string.IsNullOrEmpty(contentType)) contentType = "application/json";
            //        req.AddHeader("Content-Type", contentType);
            //        req.AddParameter(contentType, body, ParameterType.RequestBody); // сырое тело, без сериализации
            //    }

            //    return client.Execute(req);
            //}

            #endregion

            #region 13 Log

            private void SendLogMessage(string message, LogMessageType messageType)
            {
                LogMessageEvent?.Invoke(message, messageType);
            }

            public void CancelAllOrdersToSecurity(Security security)
            {
                throw new NotImplementedException();
            }

            public event Action<string, LogMessageType> LogMessageEvent;

            public event Action<Funding> FundingUpdateEvent;

            public event Action<SecurityVolumes> Volume24hUpdateEvent;

            #endregion
        }
    }
}
