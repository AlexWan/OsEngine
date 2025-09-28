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
using Trade = OsEngine.Entity.Trade;
using Candle = OsEngine.Entity.Candle;
using Order = OsEngine.Entity.Order;
using System.Globalization;
using Security = OsEngine.Entity.Security;
using System.Linq;
using Grpc.Tradeapi.V1.Orders;
using OsEngine.Market.Servers.AscendexSpot.Entity;
using OsEngine.Market.Servers.HTX.Swap.Entity;
using System.Windows;
using OpenFAST.Debug;
using OsEngine.Market.Servers.Transaq.TransaqEntity;







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
                    IRestResponse responseMessage = CreatePublicQuery("/future/public/client", Method.GET, "");

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
                            SendLogMessage("Connection cannot be open. XTFutures.  Error", LogMessageType.Error);
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent?.Invoke();
                        }
                    }
                    else
                    {
                        SendLogMessage("Connection cannot be open. XTFutures. Error request", LogMessageType.Error);
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

                        _webSocketPublicMarketDepths.Send($"{{\"method\": \"unsubscribe\", \"params\": [\"depth_update@{securityName}\",\"depth@{securityName},{20}\"], \"id\": \"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");
                        _webSocketPublicTrades.Send($"{{\"method\": \"unsubscribe\", \"params\": [\"trades@{securityName}\"], \"id\": \"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");
                    }

                    _webSocketPrivate.Send($"{{\"method\": \"unsubscribe\", \"params\": [\"balance\",\"order\",\"trade\"], \"id\": \"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");
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
                    IRestResponse response = CreatePublicQuery("/future/market/v3/public/symbol/list", Method.GET, "");

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
                                    newSecurity.PriceLimitLow = securityList.result.symbols[i].minPrice.ToDecimal();
                                    newSecurity.PriceLimitHigh = securityList.result.symbols[i].maxPrice.ToDecimal();

                                    if (securityList.result.symbols[i].tradeSwitch == "false" ||
                                        securityList.result.symbols[i].contractType != "PERPETUAL")
                                    {
                                        continue;
                                    }

                                    newSecurity.State = SecurityStateType.Activ;
                                    newSecurity.PriceStep = (securityList.result.symbols[i].minStepPrice).ToDecimal();
                                    newSecurity.Decimals = Convert.ToInt32(securityList.result.symbols[i].pricePrecision);
                                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                                    newSecurity.DecimalsVolume = Convert.ToInt32(securityList.result.symbols[i].quantityPrecision);
                                    newSecurity.MinTradeAmount = securityList.result.symbols[i].minNotional.ToDecimal();
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
                            SendLogMessage($"GetSecurities> State Code: {securityList.returnCode}",
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

                CreateQueryPortfolio(/*true*/);
            }

            private void GetNewPortfolio()
            {
                _portfolios = new List<Portfolio>();

                Portfolio portfolioInitial = new Portfolio();
                portfolioInitial.Number = _portfolioName;
                portfolioInitial.ValueBegin = 1;
                portfolioInitial.ValueCurrent = 1;
                portfolioInitial.ValueBlocked = 0;

                _portfolios.Add(portfolioInitial);

             PortfolioEvent(_portfolios);
               // PortfolioEvent(new List<Portfolio> { portfolios});
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

                        CreateQueryPortfolio(/*false*/);
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
                    _webSocketPrivate.Send($"{{\"method\":\"SUBSCRIBE\",\"params\":[\"order@{_listenKey}\",\"trade@{_listenKey}\",\"balance@{_listenKey}\"],\"id\":\"sub-1\"}}");
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

                        if (action.topic.Equals("trade"))
                        {
                            UpdateMyTrade(message);
                            continue;
                        }
                    }
                    catch (Exception exception)
                    {
                        Thread.Sleep(2000);
                    }
                }
            }

            private void UpdateTrade(string message)
            {
                try
                {
                    XTFuturesResponseWebSocketMessageAction<XTFuturesWsPublicTrade> responseTrade = JsonConvert.DeserializeAnonymousType(message, new XTFuturesResponseWebSocketMessageAction<XTFuturesWsPublicTrade>());

                    if (responseTrade?.data == null)
                    {
                        return;
                    }

                    Trade trade = new Trade();
                    trade.SecurityNameCode = responseTrade.data?.s ?? string.Empty;
                    trade.Price = (responseTrade.data?.p).ToDecimal();
                    trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseTrade.data?.t));
                    trade.Volume = (responseTrade.data?.a).ToDecimal();
                    trade.Side = responseTrade.data?.m?.Equals("BID", StringComparison.OrdinalIgnoreCase) == true ? Side.Buy : Side.Sell;
                    trade.Id = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseTrade.data?.t)).ToString();
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


            int level = 20;
            private MarketDepth GetOrCreateDepth(string symbol)
            {
                for (int i = 0; i < _marketDepths.Count; i++)
                    if (_marketDepths[i].SecurityNameCode == symbol)
                        return _marketDepths[i];

                var md = new MarketDepth
                {
                    SecurityNameCode = symbol,
                    Asks = new List<MarketDepthLevel>(level),
                    Bids = new List<MarketDepthLevel>(level)
                };
                _marketDepths.Add(md);
                return md;
            }

            private int CompareAsk(MarketDepthLevel x, MarketDepthLevel y) => x.Price.CompareTo(y.Price);
            private int CompareBid(MarketDepthLevel x, MarketDepthLevel y) => y.Price.CompareTo(x.Price);
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
                if (list.Count > level) list.RemoveAt(20);
            }


            private void ApplySnapshotSide(List<List<string>> side, List<MarketDepthLevel> dest, bool isAsk, int maxLevels = 20)
            {
                dest.Clear();
                if (side == null || side.Count == 0) return;


                int take = Math.Min(maxLevels, side.Count);
                for (int i = 0; i < take; i++)
                {
                    if (side[i] == null || side[i].Count < 2)
                    {
                        continue;
                    }

                    decimal p = (side[i][0]).ToDecimal();
                    decimal q = (side[i][1]).ToDecimal();
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
                    decimal p = (side[i][0]).ToDecimal();
                    decimal q = (side[i][1]).ToDecimal();
                    UpsertLevel(dest, p, q, isAsk);
                }
            }


            private void SnapshotDepth(string message)
            {
                try
                {
                    var responseDepth =
                        JsonConvert.DeserializeObject<XTFuturesResponseWebSocketMessageAction<XTFuturesResponseWebSocketSnapshotDepth>>(message);
                    var d = responseDepth?.data;
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
                        if (md.Asks == null) md.Asks = new List<MarketDepthLevel>(level);
                        if (md.Bids == null) md.Bids = new List<MarketDepthLevel>(level);


                        ApplySnapshotSide(d.a, md.Asks, isAsk: true, maxLevels: level);
                        ApplySnapshotSide(d.b, md.Bids, isAsk: false, maxLevels: level);

                        //  md.id = responseDepth.data.id;
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
                    var response = JsonConvert.DeserializeObject<XTFuturesResponseRest<XTFuturesTradeHistoryResult>>(message);

                    if (response == null)
                    {
                        return;
                    }
                    //response.result != null/* && response.result. != null*/)
                    {
                        for (int i = 0; i < response.result.items.Count; i++)
                        {
                            MyTrade myTrade = new MyTrade();

                            var it = response.result.items[i];

                            myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(it.timestamp));
                            myTrade.NumberOrderParent = it.orderId;
                            myTrade.NumberTrade = it.execId;
                            myTrade.Price = (it.price).ToDecimal();
                            myTrade.SecurityNameCode = it.symbol;
                            myTrade.Side = it.orderSide.Equals("BUY") ? Side.Buy : Side.Sell;

                            string commissionSecName = it.fee;

                            if (myTrade.SecurityNameCode.StartsWith(commissionSecName))
                            {
                                myTrade.Volume = it.quantity.ToDecimal() - it.fee.ToDecimal();

                                int decimalVolum = GetDecimalsVolume(myTrade.SecurityNameCode);

                                if (decimalVolum > 0)
                                {
                                    myTrade.Volume = Math.Floor(myTrade.Volume * (decimal)Math.Pow(10, decimalVolum)) / (decimal)Math.Pow(10, decimalVolum);
                                }
                            }
                            else
                            {
                                myTrade.Volume = it.quantity.ToDecimal();
                            }

                            MyTradeEvent?.Invoke(myTrade);
                        }
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
                try
                {
                    XTFuturesResponseWebSocketMessageAction<XTFuturesResponseWsUpdateOrder> order = JsonConvert.DeserializeObject<XTFuturesResponseWebSocketMessageAction<XTFuturesResponseWsUpdateOrder>>(message);

                    if (order == null)
                    {
                        return;
                    }

                    Order newOrder = new Order();

                    XTFuturesResponseWsUpdateOrder item = order.data;

                    newOrder.SecurityNameCode = item.symbol;
                    newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createdTime));
                    newOrder.SecurityClassCode = GetNameClass(item.symbol);
                    newOrder.NumberMarket = item.orderId;
                    newOrder.NumberUser = Convert.ToInt32(item.clientOrderId);


                    newOrder.Side = item.orderSide.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;
                    newOrder.State = GetOrderState(item.state);
                    newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.updatedTime));
                    if (string.Equals(item.orderType, "MARKET", StringComparison.OrdinalIgnoreCase))
                    {
                        newOrder.TypeOrder = OrderPriceType.Market; 
                    }

                    else
                    { 
                        newOrder.TypeOrder = OrderPriceType.Limit;
                    }

                 
                   // newOrder.TypeOrder = item.orderType.Equals("LIMIT", StringComparison.OrdinalIgnoreCase) ? OrderPriceType.Limit : OrderPriceType.Market;

                    newOrder.ServerType = ServerType.XTFutures;
                    newOrder.PortfolioNumber = _portfolioName;
                    newOrder.Volume = item.origQty.ToDecimal();
                    newOrder.Price = item.price.ToDecimal();
                    


                    if (newOrder.State == OrderStateType.Done || newOrder.State == OrderStateType.Partial)
                    {
                        CreateQueryMyTrade(newOrder.NumberMarket);
                    }
                    MyOrderEvent?.Invoke(newOrder);
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

            private readonly RateGate _rateGateChangeOrderPrice = new RateGate(1, TimeSpan.FromMilliseconds(60));

            public bool SetLeverage(string symbol, string positionSide, int leverage = 1)
            {
                var body = new { symbol, positionSide, leverage };

                var resp = CreatePrivateQuery(
                    "/future/user/v1/position/adjust-leverage",
                    Method.POST,
                    query: null,
                    body: body
                );
                var response = JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(resp.Content);
                if (resp.StatusCode == HttpStatusCode.OK && response != null && response.returnCode == "0")
                {
                    return true;
                }
                return false;
            }
            private decimal GetMarkPrice(string symbol)
            {
                var response = CreatePublicQuery(
        "/future/market/v1/public/q/symbol-mark-price", Method.GET, $"symbol={symbol}");



                if (response == null || response.StatusCode != HttpStatusCode.OK)
                    return 0m;

                var stateResponse = JsonConvert.DeserializeObject<XTFuturesResponseRest<MarkPriceResult>>(response.Content);

                if (stateResponse == null) return 0m;
                //if (stateResponse.returnCode = "0" && stateResponse.result == null)
                //{
                return stateResponse.result.p;
                //}

            }
            public XTFuturesBalance GetBalance(string coin)
            {
                var response = CreatePrivateQuery("/future/user/v1/compat/balance/list", Method.GET);
                if (response == null) return null;

                var stateResponse = JsonConvert.DeserializeObject<
                    XTFuturesResponseRestNew<List<XTFuturesBalance>>>(response.Content);

                if (stateResponse?.result == null)
                    return null;

                return stateResponse.result
                    .FirstOrDefault(x => x.coin.Equals(coin, StringComparison.OrdinalIgnoreCase));
            }
            private string NormalizeSymbol(string symbol)
            {
                if (string.IsNullOrWhiteSpace(symbol))
                    return string.Empty;

                // убираем разделители
                symbol = symbol.Replace("/", "").Replace("-", "").Trim();

                // если длина > 4, делим на base и quote
                // пример: BTCUSDT -> BTC + USDT
                if (symbol.Length > 4)
                {
                    string baseCoin = symbol.Substring(0, symbol.Length - 4);
                    string quoteCoin = symbol.Substring(symbol.Length - 4);
                    return $"{baseCoin.ToLower()}_{quoteCoin.ToLower()}";
                }

                return symbol.ToLower();
            }
            private decimal GetContractSize(string symbol)
            {
                string apiSymbol = NormalizeSymbol(symbol); // нормализуем BTCUSDT -> btc_usdt

                var response = CreatePublicQuery(
                    "/future/market/v1/public/symbol/detail",
                    Method.GET, $"symbol={apiSymbol}"

                );

                if (response == null || response.StatusCode != HttpStatusCode.OK)
                    return 0m;

                var stateResponse = JsonConvert.DeserializeObject<XTFuturesResponseRest<XTFuturesSymbol>>(response.Content);

                if (stateResponse == null)// ||
                                          //    stateResponse.returnCode != 0 ||
                                          //    stateResponse.result == null)
                    return 0m;

                return (stateResponse.result.contractSize).ToDecimal();
            }

            public int CalcOrigQty(Order order, decimal percent = 0.1m, int leverage = 1)
            {
                // 1) Баланс
                string symbol = (order.SecurityNameCode);
                var balance = GetBalance("USDT");
                if (balance == null) return 0;

                decimal wallet = balance.walletBalance.ToDecimal();
                decimal frozen = balance.openOrderMarginFrozen.ToDecimal();
                decimal available = wallet - frozen;

                // 2) Данные по рынку
                decimal markPrice = GetMarkPrice(order.SecurityNameCode);
                decimal contractSize = GetContractSize(symbol);

                if (markPrice <= 0 || contractSize <= 0 || available <= 0)
                    return 0;

                // 3) Формула
                decimal raw = (available * percent * leverage) / (markPrice * contractSize);
                return (int)Math.Truncate(raw);
            }
            public void SendOrder(Order order)
            {
                _rateGateSendOrder.WaitToProceed();

                try
                {
                    XTFuturesSendOrderRequestData data = new XTFuturesSendOrderRequestData();
                    data.symbol = order.SecurityNameCode;
                    data.clientOrderId = (order.NumberUser).ToString();
                    data.orderSide = order.Side == Side.Buy ? "BUY" : "SELL";
                    bool isMarket = order.TypeOrder == OrderPriceType.Market;

                    data.orderType = isMarket ? "MARKET" : "LIMIT";
                    data.price = isMarket ? null : order.Price.ToString(CultureInfo.InvariantCulture);
                    data.timeInForce = isMarket ? null : "GTC";
                    
                    data.origQty = order.Volume.ToString(CultureInfo.InvariantCulture);
                    data.positionSide = order.Side == Side.Buy ? "LONG" : "SHORT";
                    order.PortfolioNumber = _portfolioName;
                    IRestResponse responseMessage = CreatePrivateQuery(
                        "/future/trade/v1/order/create",
                        Method.POST, null, data);

                    XTFuturesResponseRest<string> stateResponse = JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(responseMessage.Content);

                    if (responseMessage.StatusCode == HttpStatusCode.OK && stateResponse != null)
                    {
                        if (stateResponse.returnCode.Equals("0") && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                        {
                            {
                                order.NumberMarket = stateResponse.result;

                                SendLogMessage($"Order sent OK. NumUser: {order.NumberUser}, OrderId: {order.NumberMarket}", LogMessageType.Trade);

                                if (order.TypeOrder == OrderPriceType.Market)
                                {
                                    order.State = OrderStateType.Done;
                                    CreateQueryMyTrade(order.NumberMarket);
                                }
                                else
                                {
                                    order.State = OrderStateType.Active;
                                }

                                MyOrderEvent?.Invoke(order);
                            }
                        }
                    }
                    else
                    {
                        CreateOrderFail(order);
                        SendLogMessage($"SendOrder Fail, Code: {stateResponse.error.msg}\n"
                                + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);

                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage("SendOrder error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            public void ChangeOrderPrice(Order order, decimal newPrice)
            {
                _rateGateChangeOrderPrice.WaitToProceed();

                try
                {

                    if (order.TypeOrder == OrderPriceType.Market || order.State == OrderStateType.Done)
                    {
                        SendLogMessage("ChangeOrderPrice> Can't change price for  Order Market", LogMessageType.Error);
                        return;
                    }

                    var body = new
                    {
                        orderId = long.Parse(order.NumberMarket, CultureInfo.InvariantCulture),
                        price = newPrice.ToString("0.##########", CultureInfo.InvariantCulture),
                        origQty = order.Volume.ToString("0.##########", CultureInfo.InvariantCulture)
                    };
                    if (newPrice <= 0)
                    {
                        SendLogMessage($"ChangeOrderPrice> bad price: {newPrice}", LogMessageType.Error);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(order.NumberMarket))
                    {
                        SendLogMessage("ChangeOrderPrice> empty exchange order id (NumberMarket)", LogMessageType.Error);
                        return;
                    }
                    //{\"returnCode\":0,\"msgInfo\":\"success\",\"error\":null,\"result\":\"541214599826300736\"}"
                    IRestResponse response = CreatePrivateQuery("/future/trade/v1/order/update", Method.POST, null, body);

                    if (order.State == OrderStateType.Cancel || order.State == OrderStateType.Done)
                    {
                        return;
                    }

                    XTFuturesResponseRest<string> stateResponse = JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(response.Content);

                    if (response.StatusCode == HttpStatusCode.OK && stateResponse != null && stateResponse.returnCode == "0")
                    {
                        order.PortfolioNumber = _portfolioName;

                        if (!string.IsNullOrEmpty(stateResponse.result))
                        {
                            order.Price = newPrice;
                            order.Volume = order.Volume;
                            // order.State = OrderStateType.Pending;


                            SendLogMessage($"Success! Order {order.NumberMarket} updated. New orderId={stateResponse.result}, price={newPrice}, qty={order.Volume}", LogMessageType.Error);
                        }
                        else
                        {
                            SendLogMessage($"Update returned an empty result {response.Content}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        string code = stateResponse?.error?.code ?? "";
                        string msg = stateResponse?.error?.msg ?? "";
                        SendLogMessage($"Error: returnCode={stateResponse?.returnCode}, code={code}, msg={msg}, raw={response.Content}", LogMessageType.Error);
                    }
                }

                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }

            public void CancelAllOrders()
            {
                _rateGateCancelOrder.WaitToProceed();

                try
                {
                    var body = new { symbol = "" };

                    IRestResponse responseMessage = CreatePrivateQuery("/future/trade/v1/order/cancel-all", Method.POST, null, body);

                    XTFuturesResponseRest<string> stateResponse = JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(responseMessage.Content);

                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        if (stateResponse.returnCode.Equals("0") && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.CurrentCulture))
                        {
                            // ignore//SendLogMessage($"CancelAllOrders SUCCESS, Code: {stateResponse.returnCode}\n"
                            //  +$"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
                        }

                    }
                    else
                    {
                        SendLogMessage($"CancelAllOrders>  error State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                        //if (stateResponse != null && stateResponse.returnCode != null)
                        //{
                        //    SendLogMessage($"CancelAllOrders error, Code: {stateResponse.returnCode}\n"
                        //                   + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
                        //}
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
                    var jsonRequest = JsonConvert.SerializeObject(
            new XTFuturesCancelAllOrdersRequestData { symbol = security.Name },
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }
        );

                    var responseMessage = CreatePrivateQuery(
                        "/future/trade/v1/order/cancel-all",
                        Method.POST,
                        query: null,
                        body: jsonRequest // строка уйдёт как application/json
                    );
                    XTFuturesResponseRest<string> stateResponse = JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(responseMessage.Content);

                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        if (stateResponse.returnCode.Equals("0") && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.CurrentCulture))
                        {
                            // ignore
                        }
                        else
                        {
                            SendLogMessage($"CancelAllOrdersToSecurity error, Code: {stateResponse.returnCode}\n"
                                + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage($"CancelAllOrdersToSecurity>  State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                        if (stateResponse != null && stateResponse.returnCode != null)
                        {
                            SendLogMessage($"CancelAllOrdersToSecurity error, Code: {stateResponse.returnCode}\n"
                                + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage("CancelAllOrdersToSecurity error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            public bool CancelOrder(Order order)
            {
                _rateGateCancelOrder.WaitToProceed();

                try
                {
                    //  var jsonBody = $"{{\"orderId\":\"{order.NumberMarket.ToString(CultureInfo.InvariantCulture)}\"}}";
                    var body = new
                    {
                        orderId = order.NumberMarket.ToString(CultureInfo.InvariantCulture)
                    };

                    var response = CreatePrivateQuery(
                        "/future/trade/v1/order/cancel",
                        Method.POST,
                        query: null,
                        body: body // строка уйдёт как application/json без доп. сериализации
                    );

                    XTFuturesResponseRest<string> stateResponse = JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(response.Content);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        if (stateResponse.returnCode.Equals("0") && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                        {
                            SendLogMessage($"Successfully canceled the order, order Id: {stateResponse.result}", LogMessageType.Trade);
                            return true;
                        }
                        else
                        {
                            GetOrderStatus(order);
                            SendLogMessage($"CancelOrder error, Code: {stateResponse.returnCode}\n"
                                + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        GetOrderStatus(order);
                        SendLogMessage($"CancelOrder>  State Code: {response.StatusCode}", LogMessageType.Error);

                        if (stateResponse != null && stateResponse.returnCode != null)
                        {
                            SendLogMessage($"CancelOrder error, Code: {stateResponse.returnCode}\n"
                                + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage("CancelOrder error: " + exception.ToString(), LogMessageType.Error);
                }

                return false;
            }

            private void CreateOrderFail(Order order)
            {
                order.State = OrderStateType.Fail;
                MyOrderEvent?.Invoke(order);
            }

            public void GetAllActivOrders()  ///future/trade/v1/order/list-open-order
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

            private List<Order> GetOrderHistory(string symbol)
            {
                _rateGateOpenOrder.WaitToProceed();

                try
                {
                    string query = "symbol=" + symbol;

                    var responseMessage = CreatePrivateQuery("/future/trade/v1/order/list-history", Method.GET, query,null);

                    XTFuturesResponseRest<XTFuturesOrderResult> stateResponse =
                        JsonConvert.DeserializeObject<XTFuturesResponseRest<XTFuturesOrderResult>>(responseMessage.Content);

                    if (stateResponse == null)
                        return null;

                    if (stateResponse.returnCode.Equals("0") &&
                        stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {
                        var result = stateResponse.result.items;

                        if (result == null)
                        {
                            return new List<Order>();
                        }

                        List<Order> orders = new List<Order>();

                        for (int i = 0; i < result.Count; i++)
                        {
                            var item = result[i];

                            Order order = new Order
                            {
                                NumberMarket = item.orderId,
                                SecurityNameCode = item.symbol,
                                SecurityClassCode = GetNameClass(item.symbol),
                                Side = item.orderSide.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell,
                                State = GetOrderState(item.state),
                                TypeOrder = item.orderType.Equals("LIMIT", StringComparison.OrdinalIgnoreCase) ? OrderPriceType.Limit : OrderPriceType.Market,
                                Volume = item.origQty.ToDecimal(),
                                Price = item.price.ToDecimal(),
                                PortfolioNumber = _portfolioName
                            };

                            orders.Add(order);
                        }

                        return orders;
                    }

                    return null; // или return new List<Order>();
                }
                catch (Exception exception)
                {
                    SendLogMessage("GetAllOrdersFromExchange error: " + exception, LogMessageType.Error);
                    return null;
                }
            }

            private List<Order> SeeOrderList()
            {
                _rateGateOpenOrder.WaitToProceed();

                try
                {
                    var responseMessage = CreatePrivateQuery("/future/trade/v1/order/list", Method.GET);

                    XTFuturesResponseRest<XTFuturesOrderResult> stateResponse =
                        JsonConvert.DeserializeObject<XTFuturesResponseRest<XTFuturesOrderResult>>(responseMessage.Content);

                    if (stateResponse == null)
                        return null;

                    if (stateResponse.returnCode.Equals("0") &&
                        stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {
                        var result = stateResponse.result.items;

                        if (result == null)
                        {
                            return new List<Order>();
                        }

                        List<Order> orders = new List<Order>();

                        for (int i = 0; i < result.Count; i++)
                        {
                            var item = result[i];

                            Order order = new Order
                            {
                                NumberMarket = item.orderId,
                                SecurityNameCode = item.symbol,
                                TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createdTime)),
                                Volume = (item.origQty).ToDecimal(),
                                Price = item.price.ToDecimal(),
                                Side = item.orderSide.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell,
                                State = GetOrderState(item.state)
                            };

                            orders.Add(order);
                        }

                        return orders;
                    }

                    return null; // или return new List<Order>();
                }
                catch (Exception exception)
                {
                    SendLogMessage("GetAllOrdersFromExchange error: " + exception, LogMessageType.Error);
                    return null;
                }
            }

           

            private readonly RateGate _rateGateOpenOrder = new RateGate(1, TimeSpan.FromMilliseconds(100));

            private List<Order> GetAllOrdersFromExchange()
            {
                _rateGateOpenOrder.WaitToProceed();

                try
                {//{\"returnCode\":0,\"msgInfo\":\"success\",\"error\":null,\"result\":{\"page\":1,\"ps\":10,\"total\":60,\"items\":[{\"orderId\":\"542507954757522176\",\"clientOrderId\":null,\"symbol\":\"gmt_usdt\",\"contractSize\":1.000000000000000000,\"orderType\":\"MARKET\",\"orderSide\":\"SELL\",\"positionSide\":\"LONG\",\"positionType\":\"CROSSED\",\"timeInForce\":\"IOC\",\"closePosition\":false,\"price\":\"0\",\"origQty\":\"136\",\"avgPrice\":\"0.0374\",\"executedQty\":\"136\",\"marginFrozen\":\"0\",\"remark\":null,\"sourceId\":null,\"sourceType\":\"DEFAULT\",\"forceClose\":false,\"leverage\":1,\"openPrice\":\"0.0379\",\"closeProfit\":\"-0.068\",\"state\":\"FILLED\",\"createdTime\":1759045975709,\"updatedTime\":1759045975784,\"welfareAccount\":false,\"triggerPriceType\":null,\"triggerProfitPrice\":null,\"profitDelegateOrderType\":null,\"profitDelegateTimeInForce\":null,\"profitDelegatePrice\":null,\"triggerStopPrice\":null,\"stopDelegateOrderType\":null,\"stopDelegateTimeInForce\":null,\"stopDelegatePrice\":null,\"markPrice\":\"0.0375\",\"desc\":null,\"systemCancel\":false,\"profit\":false},{\"orderId\":\"542322734448463680\",\"clientOrderId\":\"730325\",\"..."
                    var responseMessage = CreatePrivateQuery("/future/trade/v1/order/list", Method.GET);

                    XTFuturesResponseRest<XTFuturesOrderResult> stateResponse =
                        JsonConvert.DeserializeObject<XTFuturesResponseRest<XTFuturesOrderResult>>(responseMessage.Content);

                    if (stateResponse == null)
                        return null;

                    if (stateResponse.returnCode.Equals("0") &&
                        stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {
                        var result = stateResponse.result.items;

                        if (result == null)
                        {
                            return new List<Order>();
                        }

                        List<Order> orders = new List<Order>();

                        for (int i = 0; i < result.Count; i++)
                        {
                            var item = result[i];

                            Order order = new Order
                            {
                                NumberUser = Convert.ToInt32(item.clientOrderId),
                                NumberMarket = item.orderId,
                                SecurityNameCode = item.symbol,
                                ServerType = ServerType.XTFutures,
                                TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createdTime)),
                                Volume = (item.origQty).ToDecimal(),
                                Price = item.price.ToDecimal(),
                                Side = item.orderSide.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell,
                                State = GetOrderState(item.state),
                                SecurityClassCode = GetNameClass(item.symbol),
                                TypeOrder = item.orderType.Equals("LIMIT", StringComparison.OrdinalIgnoreCase) ? OrderPriceType.Limit : OrderPriceType.Market,
                                PortfolioNumber =_portfolioName
                            };

                            orders.Add(order);
                        }

                        return orders;
                    }

                    return null; // или return new List<Order>();
                }
                catch (Exception exception)
                {
                    SendLogMessage("GetAllOrdersFromExchange error: " + exception, LogMessageType.Error);
                    return null;
                }
            }

            public Order GetOrderStatusById(string NumberMarket)
            {
                try
                {
                    _rateGateOpenOrder.WaitToProceed();////переделать.WaitToProceed();

                    Order order = new Order();

                    if (NumberMarket == null)
                    {
                        SendLogMessage("GetOrderStatus> Order is null or empty", LogMessageType.Error);
                        return new Order();
                    }

                    string query = "orderId=" + order.NumberMarket.ToString(CultureInfo.InvariantCulture);

                    var responseMessage = CreatePrivateQuery("/future/trade/v1/order/detail", Method.GET, query, null);

                    if (responseMessage == null)
                    {
                        // return OrderStateType.None;
                    }

                    XTFuturesResponseRest<XTFuturesOrderDetailById> response = JsonConvert.DeserializeObject<XTFuturesResponseRest<XTFuturesOrderDetailById>>(responseMessage.Content);

                    if (response.returnCode.Equals("0") && response.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {

                        XTFuturesOrderDetailById orderData = response.result;

                        order.SecurityNameCode = orderData.symbol;
                        order.SecurityClassCode = GetNameClass(orderData.symbol);
                        order.NumberMarket = orderData.orderId;
                        // order.NumberUser = Convert.ToInt32(orderData.clientOrderId);
                        order.Price = orderData.price.ToDecimal();
                        order.PortfolioNumber = _portfolioName;
                        order.Side = orderData.orderSide.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;
                        order.TypeOrder = orderData.orderType == "Limit" ? OrderPriceType.Limit : OrderPriceType.Market;
                        order.Volume = orderData.origQty.ToDecimal();
                        order.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(orderData.createdTime));

                        order.State = GetOrderState(orderData.state);

                        if (orderData.state == "Filled" || orderData.state == "PartiallyFilled")
                        {
                            if (order.State == OrderStateType.Done
                          || order.State == OrderStateType.Partial)
                            {
                                CreateQueryMyTrade(order.NumberMarket);
                            }

                            ////MyTrade myTrade = new MyTrade();

                            ////myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(orderData.lastExecTime));
                            ////myTrade.SecurityNameCode = orderData.symbol;
                            ////myTrade.Price = orderData.price.ToDecimal();
                            ////myTrade.NumberTrade = orderData.seqNum;
                            ////myTrade.NumberOrderParent = orderData.orderId;
                            ////myTrade.Volume = orderData.orderQty.ToDecimal();
                            ////myTrade.Side = orderData.side == "Buy" ? Side.Buy : Side.Sell;

                            //MyTradeEvent?.Invoke(myTrade);
                        }

                        return order;
                    }
                    else
                    {
                        SendLogMessage($"Get order status. Error:{response.returnCode}", LogMessageType.Error);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage($"Exception in GetOrderStatusById: {exception}", LogMessageType.Error);

                }

                return null;
            }

            public OrderStateType GetOrderStatus(Order order)
            {
                _rateGateOpenOrder.WaitToProceed();

                try
                {
                    if (order == null)
                    {
                        SendLogMessage("GetOrderStatus > Order is null", LogMessageType.Error);
                        return OrderStateType.None;
                    }

                    List<Order> orders = GetAllOrdersFromExchange();
                    if (orders == null || orders.Count == 0)
                    {
                        return OrderStateType.None;
                    }

                    Order found = null;

                    for (int i = 0; i < orders.Count; i++)
                    {
                        var o = orders[i];

                        if ((!string.IsNullOrWhiteSpace(order.NumberMarket) && o.NumberMarket == order.NumberMarket)
                            || (order.NumberUser != 0 && o.NumberUser == order.NumberUser))
                        {
                            found = o;
                            break;
                        }
                    }

                    if (found == null)
                    {
                        return OrderStateType.None;
                    }


                    MyOrderEvent?.Invoke(found);

                    if (found.State == OrderStateType.Done || found.State == OrderStateType.Partial)
                    {
                        CreateQueryMyTrade(found.NumberMarket);
                    }

                    return found.State;
                }
                catch (Exception exception)
                {
                    SendLogMessage($"Exception in GetOrderStatus: {exception}", LogMessageType.Error);
                    return OrderStateType.None;
                }
            }

            //string query = "orderId=" + order.NumberMarket.ToString(CultureInfo.InvariantCulture);

            //var responseMessage = CreatePrivateQuery("/future/trade/v1/order/detail", Method.GET, query);

            //XTFuturesResponseRest<XTFuturesOrderItem> response = JsonConvert.DeserializeObject<XTFuturesResponseRest<XTFuturesOrderItem>>(responseMessage.Content);

            //if (response == null)
            //{
            //    return OrderStateType.None;
            //}

            //if (response.returnCode.Equals("0") && response.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
            //{


            //Order myOrder = OrderMapping(response.result);

            //if (myOrder == null)
            //{
            //    return OrderStateType.None;
            //}

            //if (myOrder.State == OrderStateType.Done
            //   || myOrder.State == OrderStateType.Partial)
            //{
            //    CreateQueryMyTrade(myOrder.NumberMarket);
            //}


            //            MyOrderEvent?.Invoke(myOrder);
            //            return myOrder.State;
            //        }

            //    catch (Exception exception)
            //    {
            //        SendLogMessage("GetOrderStatus error: " + exception.ToString(), LogMessageType.Error);
            //    }

            //    return OrderStateType.None;
            //}

            #endregion

            #region 12 Queries

            private readonly string _baseUrl = "https://fapi.xt.com";

            private List<string> _subscribedSecurities = new List<string>();

            private readonly RateGate _rateGateGetToken = new RateGate(1, TimeSpan.FromMilliseconds(10000));

            private string GetListenKey()
            {
                _rateGateGetToken.WaitToProceed();

                string listenKey = "";

                try
                {
                    var responseMessage = CreatePrivateQuery("/future/user/v1/user/listen-key", Method.GET);

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
                        SendLogMessage($"Receiving Token>  State Code: {responseMessage.StatusCode}", LogMessageType.Error);

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

            private readonly RateGate _rateGateGetPortfolio = new RateGate(1, TimeSpan.FromMilliseconds(333));
            private string _portfolioName = "XTFuturesPortfolio";
            private void CreateQueryPortfolio(/*bool isUpdateValueBegin*/)
            {
                _rateGateGetPortfolio.WaitToProceed();

                try
                {
                    IRestResponse response = CreatePrivateQuery("/future/user/v1/compat/balance/list", Method.GET);

                    if (response == null)
                    {
                        return;
                    }

                    XTFuturesResponseRestNew<List<XTFuturesBalance>> stateResponse =
                    JsonConvert.DeserializeObject<XTFuturesResponseRestNew<List<XTFuturesBalance>>>(response.Content);

                    if (response.StatusCode == HttpStatusCode.OK && stateResponse != null)
                    {
                        if (stateResponse.rc.Equals("0") && stateResponse.mc.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                        {
                            //UpdatePortfolioRest(response.Content, isUpdateValueBegin);



                            //if (stateResponse.result[i].walletBalance == "0")
                            //{
                            //portfolio.ValueBegin = 1;
                            //portfolio.ValueCurrent = 1;
                            //}
                            //else
                            //{
                            //    if (isUpdateValueBegin)
                            //    {
                            //        portfolio.ValueBegin = Math.Round(stateResponse.result.walletBalance.ToDecimal(), 5);
                            //    }

                            //    portfolio.ValueCurrent = Math.Round(stateResponse.result.convertUsdtAmount.ToDecimal(), 5);
                            //}
                            Portfolio portfolio = new Portfolio();


                            portfolio.Number = _portfolioName;
                            portfolio.ValueBegin = 1;
                            portfolio.ValueCurrent = 1;
                            List<PositionOnBoard> alreadySendPositions = new List<PositionOnBoard>();

                            for (int i = 0; i < stateResponse.result.Count; i++)
                            {
                                PositionOnBoard position = new PositionOnBoard();

                                position.PortfolioName = _portfolioName;
                                position.SecurityNameCode = stateResponse.result[i].coin;
                                position.ValueBegin = stateResponse.result[i].walletBalance.ToDecimal();
                                position.ValueCurrent = stateResponse.result[i].walletBalance.ToDecimal() - stateResponse.result[i].openOrderMarginFrozen.ToDecimal();
                                position.ValueBlocked = stateResponse.result[i].openOrderMarginFrozen.ToDecimal();

                                portfolio.SetNewPosition(position);
                                alreadySendPositions.Add(position);
                            }

                            PortfolioEvent(new List<Portfolio> { portfolio });


                            //List<PositionOnBoard> alreadySendPositions = new List<PositionOnBoard>();

                            //for (int i = 0; i < stateResponse.result.Count; i++)
                            //{
                            //    PositionOnBoard pos = new PositionOnBoard
                            //    {
                            //        PortfolioName = _portfolioName,
                            //        SecurityNameCode = stateResponse.result[i].coin,
                            //        ValueBlocked = stateResponse.result[i].openOrderMarginFrozen.ToDecimal(),
                            //        ValueCurrent = stateResponse.result[i].walletBalance.ToDecimal() - stateResponse.result[i].openOrderMarginFrozen.ToDecimal()
                            //    };

                            //    if (isUpdateValueBegin)
                            //    {
                            //        pos.ValueBegin = stateResponse.result[i].walletBalance.ToDecimal();
                            //    }

                            //    bool canSend = true;

                            //for (int j = 0; j < alreadySendPositions.Count; j++)
                            //{
                            //    if (!alreadySendPositions[j].SecurityNameCode.Equals(pos.SecurityNameCode, StringComparison.OrdinalIgnoreCase)
                            //        || pos.ValueCurrent != 0)
                            //        continue;

                            //    canSend = false;
                            //    break;
                            //}

                            //if (!canSend)
                            //{
                            //    continue;
                            //}

                            //portfolio.SetNewPosition(pos);
                            //alreadySendPositions.Add(pos);
                            //}

                            //PortfolioEvent(_portfolios);
                        }
                        else
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
                    //for (int i = 0; i < assets.result.Count; i++)
                    //{
                    //    if (string.Equals(assets.result[i].coin, "usdt", StringComparison.OrdinalIgnoreCase))
                    //    { idx = i; break; }
                    //}

                    //if (assets.result[i].walletBalance == "0")
                    //{
                    portfolio.ValueBegin = 1;
                    portfolio.ValueCurrent = 1;
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
                            PortfolioName = _portfolioName,
                            SecurityNameCode = assets.result[i].coin,
                            ValueBlocked = assets.result[i].openOrderMarginFrozen.ToDecimal(),
                            ValueCurrent = assets.result[i].walletBalance.ToDecimal() - assets.result[i].openOrderMarginFrozen.ToDecimal()
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

            private void CreateQueryMyTrade(string orderId)
            {
                _rateGateGetMyTradeState.WaitToProceed();
                var list = new List<MyTrade>();
                try
                {
                    var orderIdStr = orderId.ToString(CultureInfo.InvariantCulture);

                    var response = CreatePrivateQuery(
                        "/future/trade/v1/order/trade-list",
                        Method.GET,
                        query: "orderId=" + orderIdStr
                    );

                    XTFuturesResponseRest<XTFuturesTradeHistoryResult> stateResponse =
           JsonConvert.DeserializeObject<XTFuturesResponseRest<XTFuturesTradeHistoryResult>>(response.Content);

                    if (response.StatusCode == HttpStatusCode.OK && stateResponse != null)
                    {
                        if (stateResponse.returnCode.Equals("0") && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                        {

                            for (int i = 0; i < stateResponse.result.items.Count; i++)
                            {
                                var data = stateResponse.result.items[i];
                                if (data == null) continue;

                                MyTrade trade = new MyTrade();
                                trade.NumberOrderParent = data.orderId.ToString();
                                trade.NumberTrade = data.execId.ToString();
                                trade.SecurityNameCode = data.symbol;
                                trade.Price = data.price.ToDecimal();
                                trade.Volume = data.quantity.ToDecimal();
                                trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(data.timestamp));
                                trade.Side = data.orderSide.Equals("BUY") ? Side.Buy : Side.Sell;

                                MyTradeEvent?.Invoke(trade);
                            }
                        }

                        else
                        {
                            SendLogMessage($"CreateQueryMyTrade error, Code: {stateResponse.returnCode}\n"
                                + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage($"CreateQueryMyTrade>  State Code: {stateResponse.returnCode}", LogMessageType.Error);

                        if (stateResponse != null && stateResponse.returnCode != null)
                        {
                            SendLogMessage($"CreateQueryMyTrade error, Code: {stateResponse.returnCode}\n"
                                + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
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

                    string param = "symbol=" + nameSec
                                 + "&interval=" + stringInterval
                                 + "&startTime=" + startTime
                                 + "&endTime=" + endTime
                                 + "&limit=" + limit;


                    IRestResponse responseMessage = CreatePublicQuery("/future/market/v1/public/q/kline", Method.GET, param);

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
            IRestResponse CreatePublicQuery(string path, Method method, string parameters = "")
            {
                RestClient client = new RestClient(_baseUrl);

                if (method == Method.GET && !string.IsNullOrEmpty(parameters))
                {
                    path += "?" + parameters;
                }

                RestRequest request = new RestRequest(path, method);
                request.AddHeader("Accept", "application/json");

                IRestResponse response = client.Execute(request);
                return response;
            }
            //IRestResponse CreatePublicQuery(string path, string parameters, Method method)
            //{
            //    RestClient client = new RestClient(_baseUrl);

            //    if (method == Method.GET && !string.IsNullOrEmpty(parameters))
            //    {
            //        path += "?" + parameters;
            //    }

            //    RestRequest request = new RestRequest(path, method);

            //    request.AddHeader("Accept", "application/json");
            //    request.AddHeader("Content-Type", "application/x-www-form-urlencoded");

            //    if (method == Method.POST && !string.IsNullOrEmpty(parameters))
            //    {
            //        request.AddParameter("application/x-www-form-urlencoded", parameters, ParameterType.RequestBody);
            //    }

            //    IRestResponse response = client.Execute(request);
            //    return response;
            //}

            private string HmacSHA256(string key, string data)
            {
                using (var h = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
                {
                    var hash = h.ComputeHash(Encoding.UTF8.GetBytes(data));
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }

            private string _recvWindow = "5000";


            private IRestResponse CreatePrivateQuery(string path, Method method, string query = null, object body = null)
            {
                try
                {
                    string resource = string.IsNullOrEmpty(query) ? path : $"{path}?{query}";

                    string bodyString = "";
                    var jsonSettings = new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore, // не включать null-поля
                        Culture = CultureInfo.InvariantCulture
                    };
                    if (method == Method.POST && body != null)
                    {
                        bodyString = body is string s ? s : JsonConvert.SerializeObject(body, Formatting.None);
                    }

                    string ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                    string X = $"validate-appkey={_publicKey}&validate-timestamp={ts}";

                    var yb = new StringBuilder().Append('#').Append(path);
                    if (!string.IsNullOrEmpty(query)) yb.Append('#').Append(query);
                    if (method == Method.POST && bodyString.Length > 0)
                        yb.Append('#').Append(bodyString);

                    string signInput = X + yb;
                    string signature = HmacSHA256(_secretKey, signInput);

                    var client = new RestClient(_baseUrl);
                    var request = new RestRequest(resource, method);


                    request.AddHeader("Accept", "application/json");
                    request.AddHeader("validate-appkey", _publicKey);
                    request.AddHeader("validate-timestamp", ts);
                    request.AddHeader("validate-signature", signature);
                    request.AddHeader("validate-algorithms", "HmacSHA256");
                    if (!string.IsNullOrEmpty(_recvWindow))
                        request.AddHeader("validate-recvwindow", _recvWindow);

                    if (method == Method.POST && bodyString.Length > 0)
                    {
                        request.AddHeader("Content-Type", "application/json");
                        request.AddParameter("application/json", bodyString, ParameterType.RequestBody);
                    }

                    return client.Execute(request);
                }
                catch (Exception ex)
                {
                    SendLogMessage("SendPrivate error: " + ex, LogMessageType.Error);
                    return null;
                }
            }

            #endregion

            #region 13 Log

            private void SendLogMessage(string message, LogMessageType messageType)
            {
                LogMessageEvent?.Invoke(message, messageType);
            }

            public event Action<string, LogMessageType> LogMessageEvent;

            public event Action<Funding> FundingUpdateEvent;

            public event Action<SecurityVolumes> Volume24hUpdateEvent;

            #endregion
        }
    }
}
