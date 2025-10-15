

//using Newtonsoft.Json;
//using OsEngine.Entity;
//using OsEngine.Language;
//using OsEngine.Logging;
//using OsEngine.Market.Servers.Entity;
//using OsEngine.Market.Servers.XT.XTFutures.Entity;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Net;
//using System.Security.Cryptography;
//using System.Text;
//using System.Threading;
//using OsEngine.Entity.WebSocketOsEngine;
//using RestSharp;
//using JsonConvert = Newtonsoft.Json.JsonConvert;
//using Trade = OsEngine.Entity.Trade;
//using Candle = OsEngine.Entity.Candle;
//using Order = OsEngine.Entity.Order;
//using System.Globalization;
//using Security = OsEngine.Entity.Security;
//using OsEngine.Candles.Series;
//using System.IO;
//using ErrorEventArgs = OsEngine.Entity.WebSocketOsEngine.ErrorEventArgs;







//namespace OsEngine.Market.Servers.XT.XTFutures
//{
//    public class XTFuturesServer : AServer
//    {
//        public XTFuturesServer(int uniqueNumber)
//        {
//            ServerNum = uniqueNumber;
//            XTServerSpotRealization realization = new XTServerSpotRealization();
//            ServerRealization = realization;

//            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
//            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
//        }

//        public class XTServerSpotRealization : IServerRealization
//        {
//            #region 1 Constructor, Status, Connection

//            public XTServerSpotRealization()
//            {
//                ServerStatus = ServerConnectStatus.Disconnect;

//                Thread threadCheckAlive = new Thread(ConnectionCheck);
//                threadCheckAlive.IsBackground = true;
//                threadCheckAlive.Name = "CheckAliveXT";
//                threadCheckAlive.Start();

//                Thread threadForPublicMessages = new Thread(PublicMarketDepthsMessageReader);
//                threadForPublicMessages.IsBackground = true;
//                threadForPublicMessages.Name = "PublicMessageReaderXT";
//                threadForPublicMessages.Start();

//                Thread threadForTradesMessages = new Thread(PublicTradesMessageReader);
//                threadForTradesMessages.IsBackground = true;
//                threadForTradesMessages.Name = "PublicTradesMessageReaderXT";
//                threadForTradesMessages.Start();

//                Thread threadForPrivateMessages = new Thread(PrivateMessageReader);
//                threadForPrivateMessages.IsBackground = true;
//                threadForPrivateMessages.Name = "PrivateMessageReaderXT";
//                threadForPrivateMessages.Start();

//                Thread threadForGetPortfolios = new Thread(UpdatePortfolios);
//                threadForGetPortfolios.IsBackground = true;
//                threadForGetPortfolios.Name = "UpdatePortfoliosXT";
//                threadForGetPortfolios.Start();
//            }

//            public DateTime ServerTime { get; set; }

//            private WebProxy _myProxy;

//            public void Connect(WebProxy proxy)
//            {
//                _myProxy = proxy;
//                _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
//                _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

//                if (string.IsNullOrEmpty(_publicKey)
//                   || string.IsNullOrEmpty(_secretKey))
//                {
//                    SendLogMessage("Can`t run XTFutures connector. No keys", LogMessageType.Error);
//                    return;
//                }

//                try
//                {
//                    IRestResponse responseMessage = CreatePublicQuery("/future/public/client", Method.GET);

//                    if (responseMessage.StatusCode == HttpStatusCode.OK)
//                    {
//                        try
//                        {
//                            _listenKey = GetListenKey();
//                            if (string.IsNullOrEmpty(_listenKey))
//                            {
//                                SendLogMessage("Check the Public and Private Key!", LogMessageType.Error);
//                                ServerStatus = ServerConnectStatus.Disconnect;

//                                DisconnectEvent?.Invoke();
//                                return;
//                            }

//                            FIFOListWebSocketPublicMarketDepthsMessage = new ConcurrentQueue<string>();
//                            FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();
//                            FIFOListWebSocketPublicTradesMessage = new ConcurrentQueue<string>();

//                            CreateWebSocketConnection();
//                            CheckFullActivation();
//                        }
//                        catch (Exception exception)
//                        {
//                            SendLogMessage("Connection cannot be open. XTFutures.  Error", LogMessageType.Error);
//                            ServerStatus = ServerConnectStatus.Disconnect;
//                            DisconnectEvent?.Invoke();
//                        }
//                    }
//                    else
//                    {
//                        SendLogMessage("Connection cannot be open. XTFutures. Error request", LogMessageType.Error);
//                        ServerStatus = ServerConnectStatus.Disconnect;
//                        DisconnectEvent?.Invoke();
//                    }
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("Dispose error" + exception.ToString(), LogMessageType.Error);
//                }
//            }

//            public void Dispose()
//            {
//                try
//                {
//                    UnsubscribeFromAllWebSockets();
//                    _subscribedSecurities.Clear();
//                    DeleteWebsocketConnection();
//                    _marketDepths.Clear();
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("Dispose error" + exception.ToString(), LogMessageType.Error);
//                }

//                FIFOListWebSocketPublicMarketDepthsMessage = new ConcurrentQueue<string>();
//                FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();
//                FIFOListWebSocketPublicTradesMessage = new ConcurrentQueue<string>();

//                Disconnect();
//            }

//            public void Disconnect()
//            {
//                if (ServerStatus != ServerConnectStatus.Disconnect)
//                {
//                    ServerStatus = ServerConnectStatus.Disconnect;
//                    DisconnectEvent?.Invoke();
//                }
//            }

//            private void UnsubscribeFromAllWebSockets()
//            {
//                if (_webSocketPublicMarketDepths == null
//                    || _webSocketPrivate == null
//                    || _webSocketPublicTrades == null)
//                {
//                    return;
//                }

//                if (ServerStatus != ServerConnectStatus.Connect)
//                {
//                    return;
//                }

//                try
//                {
//                    for (int i = 0; i < _subscribedSecurities.Count; i++)
//                    {
//                        string securityName = _subscribedSecurities[i];

//                        _webSocketPublicMarketDepths.SendAsync($"{{\"method\": \"unsubscribe\", \"params\": [\"depth_update@{securityName}\",\"depth@{securityName},{20}\"], \"id\": \"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");
//                        _webSocketPublicTrades.SendAsync($"{{\"method\": \"unsubscribe\", \"params\": [\"trades@{securityName}\"], \"id\": \"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");
//                    }

//                    _webSocketPrivate.SendAsync($"{{\"method\": \"unsubscribe\", \"params\": [\"balance\",\"order\",\"trade\"], \"id\": \"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");
//                }
//                catch
//                {
//                    // ignore
//                }
//            }

//            public ServerType ServerType
//            {
//                get { return ServerType.XTFutures; }
//            }

//            public ServerConnectStatus ServerStatus { get; set; }

//            public event Action ConnectEvent;

//            public event Action DisconnectEvent;

//            #endregion

//            #region 2 Properties

//            public List<IServerParameter> ServerParameters { get; set; }

//            private string _publicKey;

//            private string _secretKey;

//            private string _listenKey; // lifetime 8 hours

//            private readonly string _baseUrl = "https://fapi.xt.com";

//            private string _portfolioName = "XTFuturesPortfolio";

//            #endregion

//            #region 3 Securities

//            public event Action<List<Security>> SecurityEvent;

//            private readonly RateGate _rateGateSecurity = new RateGate(1, TimeSpan.FromMilliseconds(200));

//            private string GetNameClass(string security)
//            {
//                if (security.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
//                {
//                    return "USDT";
//                }

//                return "Futures";
//            }

//            public void GetSecurities()
//            {
//                _rateGateSecurity.WaitToProceed();

//                try
//                {
//                    IRestResponse response = CreatePublicQuery("/future/market/v3/public/symbol/list", Method.GET, "");

//                    if (response.StatusCode == HttpStatusCode.OK)
//                    {
//                        XTFuturesResponseRest<XTFuturesSymbolListResult> securityList =
//                            JsonConvert.DeserializeObject<XTFuturesResponseRest<XTFuturesSymbolListResult>>(response.Content);

//                        if (securityList == null)
//                        {
//                            SendLogMessage("GetSecurities> Deserialization resulted in null", LogMessageType.Error);
//                            return;
//                        }

//                        if (response.StatusCode == HttpStatusCode.OK && response != null)
//                        {
//                            if (securityList.returnCode.Equals("0") && securityList.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
//                            {
//                                SendLogMessage("Securities loaded. Count: " + securityList.result.symbols.Count, LogMessageType.System);

//                                List<Security> securities = new List<Security>();

//                                for (int i = 0; i < securityList.result.symbols.Count; i++)
//                                {
//                                    XTFuturesSymbol symbols = securityList.result.symbols[i];

//                                    Security newSecurity = new Security();

//                                    newSecurity.Exchange = ServerType.XTFutures.ToString();
//                                    newSecurity.Name = symbols.symbol;
//                                    newSecurity.NameFull = symbols.symbol;
//                                    newSecurity.NameClass = GetNameClass(symbols.symbol);
//                                    newSecurity.NameId = symbols.symbolGroupId;
//                                    newSecurity.SecurityType = SecurityType.Futures;
//                                    newSecurity.Lot = 1;
//                                    newSecurity.PriceLimitLow = symbols.minPrice.ToDecimal();
//                                    newSecurity.PriceLimitHigh = symbols.maxPrice.ToDecimal();

//                                    if (symbols.tradeSwitch == "false" ||
//                                        symbols.contractType != "PERPETUAL")
//                                    {
//                                        continue;
//                                    }

//                                    newSecurity.State = SecurityStateType.Activ;
//                                    newSecurity.PriceStep = (symbols.minStepPrice).ToDecimal();
//                                    newSecurity.Decimals = Convert.ToInt32(symbols.pricePrecision);
//                                    newSecurity.PriceStepCost = newSecurity.PriceStep;
//                                    newSecurity.DecimalsVolume = Convert.ToInt32(symbols.quoteCoinDisplayPrecision);
//                                    newSecurity.MinTradeAmount = symbols.minNotional.ToDecimal();
//                                    newSecurity.MinTradeAmountType = MinTradeAmountType.C_Currency;
//                                    newSecurity.VolumeStep = newSecurity.DecimalsVolume.GetValueByDecimals();

//                                    securities.Add(newSecurity);
//                                }

//                                SecurityEvent?.Invoke(securities);
//                            }
//                            else
//                            {
//                                SendLogMessage($"GetSecurities return code: {securityList.returnCode}\n"
//                                               + $"Message Code: {securityList.msgInfo}", LogMessageType.Error);
//                            }
//                        }
//                        else
//                        {
//                            SendLogMessage($"GetSecurities> State Code: {securityList.returnCode}", LogMessageType.Error);

//                            if (securityList != null && securityList.returnCode != null)
//                            {
//                                SendLogMessage($"Return Code: {securityList.returnCode}\n"
//                                               + $"Message Code: {securityList.msgInfo}", LogMessageType.Error);
//                            }
//                        }
//                    }
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("GetSecurities error: " + exception.ToString(), LogMessageType.Error);
//                }
//            }

//            #endregion

//            #region 4 Portfolios

//            private List<Portfolio> _portfolios;

//            public event Action<List<Portfolio>> PortfolioEvent;

//            public void GetPortfolios()
//            {
//                if (_portfolios == null)
//                {
//                    GetNewPortfolio();
//                }

//                CreateQueryPortfolio(true);
//                CreateQueryPositions(true);
//            }

//            private void GetNewPortfolio()
//            {
//                _portfolios = new List<Portfolio>();

//                Portfolio portfolioInitial = new Portfolio();
//                portfolioInitial.Number = _portfolioName;
//                portfolioInitial.ValueBegin = 1;
//                portfolioInitial.ValueCurrent = 1;
//                portfolioInitial.ValueBlocked = 0;

//                _portfolios.Add(portfolioInitial);

//                PortfolioEvent?.Invoke(_portfolios);
//            }

//            private void UpdatePortfolios()
//            {
//                while (true)
//                {
//                    try
//                    {
//                        if (ServerStatus == ServerConnectStatus.Disconnect)
//                        {
//                            Thread.Sleep(2000);
//                            continue;
//                        }

//                        Thread.Sleep(10000);

//                        if (_portfolios == null)
//                        {
//                            GetNewPortfolio();
//                        }

//                        CreateQueryPortfolio(false);
//                        CreateQueryPositions(false);
//                    }
//                    catch (Exception error)
//                    {
//                        SendLogMessage(error.ToString(), LogMessageType.Error);
//                    }
//                }
//            }

//            #endregion

//            #region 5 Data

//            public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
//            {
//                return null;
//            }

//            public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
//            {
//                int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
//                DateTime endTime = DateTime.UtcNow;
//                DateTime startTime = endTime.AddMinutes(-tfTotalMinutes * candleCount);

//                return GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, startTime);
//            }

//            private List<Candle> GetCandleHistory(string nameSec, TimeSpan tf, bool isOsData, int CountToLoad, DateTime startTime, DateTime endTime)
//            {
//                int needToLoadCandles = CountToLoad;

//                List<Candle> candles = new List<Candle>();

//                DateTime currentEndTime = endTime;
//                DateTime currentStartTime = startTime;

//                do
//                {
//                    int limit = needToLoadCandles;

//                    if (needToLoadCandles > 1000)
//                    {
//                        limit = 1000;
//                    }

//                    DateTime queryStartTime = currentEndTime - TimeSpan.FromMinutes(tf.TotalMinutes * limit);
//                    if (queryStartTime < currentStartTime)
//                    {
//                        queryStartTime = currentStartTime;
//                    }

//                    List<Candle> rangeCandles;

//                    rangeCandles = CreateQueryCandles(nameSec, GetStringInterval(tf), queryStartTime, currentEndTime, limit);

//                    if (rangeCandles == null)
//                    {
//                        return null;
//                    }

//                    rangeCandles.Reverse();

//                    candles.InsertRange(0, rangeCandles);

//                    if (candles.Count != 0)
//                    {
//                        currentEndTime = candles[0].TimeStart;
//                    }

//                    needToLoadCandles -= limit;
//                }

//                while (needToLoadCandles > 0 && currentEndTime > currentStartTime);

//                return FilterCandlesByTimeRange(candles, startTime, endTime);
//            }

//            public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
//            {
//                startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
//                endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
//                actualTime = DateTime.SpecifyKind(actualTime, DateTimeKind.Utc);

//                if (!CheckTime(startTime, endTime, actualTime))
//                {
//                    return null;
//                }

//                int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

//                if (!CheckTf(tfTotalMinutes))
//                {
//                    return null;
//                }

//                int countNeedToLoad = GetCountCandlesFromSliceTime(startTime, endTime, timeFrameBuilder.TimeFrameTimeSpan);

//                return GetCandleHistory(security.Name, timeFrameBuilder.TimeFrameTimeSpan, true, countNeedToLoad, startTime, endTime);
//            }

//            private int GetCountCandlesFromSliceTime(DateTime startTime, DateTime endTime, TimeSpan tf)
//            {
//                TimeSpan timeSlice = endTime - startTime;
//                double totalMinutes = timeSlice.TotalMinutes;
//                double tfMinutes = tf.TotalMinutes;

//                return (int)Math.Ceiling(totalMinutes / tfMinutes);
//            }

//            private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
//            {
//                if (startTime >= endTime ||
//                    startTime >= DateTime.UtcNow ||
//                    actualTime > endTime ||
//                    actualTime > DateTime.UtcNow)
//                {
//                    return false;
//                }

//                return true;
//            }

//            private bool CheckTf(int timeFrameMinutes)
//            {
//                if (timeFrameMinutes == 1 ||
//                    timeFrameMinutes == 5 ||
//                    timeFrameMinutes == 15 ||
//                    timeFrameMinutes == 30 ||
//                    timeFrameMinutes == 60 ||
//                    timeFrameMinutes == 120 ||
//                    timeFrameMinutes == 240 ||
//                    timeFrameMinutes == 1440)
//                {
//                    return true;
//                }

//                return false;
//            }

//            private string GetStringInterval(TimeSpan tf)
//            {
//                if (tf.Minutes != 0)
//                {
//                    return $"{tf.Minutes}m";
//                }
//                else if (tf.Days != 0)
//                {
//                    return $"1d";
//                }
//                else
//                {
//                    return $"{tf.Hours}h";
//                }
//            }

//            private List<Candle> FilterCandlesByTimeRange(List<Candle> candles, DateTime startTime, DateTime endTime)
//            {
//                if (candles == null || candles.Count == 0)
//                {
//                    return candles;
//                }

//                List<Candle> filteredCandles = new List<Candle>();

//                for (int i = 0; i < candles.Count; i++)
//                {
//                    Candle candle = candles[i];

//                    if (candle.TimeStart >= startTime && candle.TimeStart <= endTime)
//                    {
//                        filteredCandles.Add(candle);
//                    }
//                }

//                return filteredCandles;
//            }

//            private readonly RateGate _rateGateCandleHistory = new RateGate(1, TimeSpan.FromMilliseconds(200));

//            private List<Candle> CreateQueryCandles(string nameSec, string stringInterval, DateTime timeStart, DateTime timeEnd, int count)
//            {
//                _rateGateCandleHistory.WaitToProceed();

//                try
//                {
//                    string startTime = TimeManager.GetTimeStampMilliSecondsToDateTime(timeStart).ToString();
//                    string endTime = TimeManager.GetTimeStampMilliSecondsToDateTime(timeEnd).ToString();
//                    string limit = count.ToString();

//                    string param = "symbol=" + nameSec
//                                 + "&interval=" + stringInterval
//                                 + "&startTime=" + startTime
//                                 + "&endTime=" + endTime
//                                 + "&limit=" + limit;

//                    IRestResponse responseMessage = CreatePublicQuery("/future/market/v1/public/q/kline", Method.GET, param);

//                    if (responseMessage.StatusCode == HttpStatusCode.OK)
//                    {
//                        if (responseMessage == null)
//                        {
//                            return null;
//                        }

//                        XTFuturesResponseRest<List<XTFuturesCandle>> symbols = JsonConvert.DeserializeObject<XTFuturesResponseRest<List<XTFuturesCandle>>>(responseMessage.Content);

//                        if (symbols == null)
//                        {
//                            SendLogMessage("CreateQueryCandles: Response is null", LogMessageType.Error);
//                            return null;
//                        }

//                        if (symbols.returnCode.Equals("0") && symbols.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
//                        {
//                            List<Candle> candles = new List<Candle>();

//                            for (int i = 0; i < symbols.result.Count; i++)
//                            {
//                                XTFuturesCandle item = symbols.result[i];

//                                Candle newCandle = new Candle();

//                                newCandle.Open = item.o.ToDecimal();
//                                newCandle.Close = item.c.ToDecimal();
//                                newCandle.High = item.h.ToDecimal();
//                                newCandle.Low = item.l.ToDecimal();
//                                newCandle.Volume = item.a.ToDecimal();
//                                newCandle.State = CandleState.Finished;
//                                newCandle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.t));
//                                candles.Add(newCandle);
//                            }

//                            return candles;
//                        }

//                        SendLogMessage($"CreateQueryCandles error, Code: {symbols.returnCode}\n"
//                                       + $"Message code: {symbols.msgInfo}", LogMessageType.Error);
//                        return null;
//                    }
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("CreateQueryCandles error: " + exception.ToString(), LogMessageType.Error);
//                }

//                return null;
//            }

//            #endregion

//            #region 6 WebSocket creation

//            private WebSocket _webSocketPrivate;

//            private WebSocket _webSocketPublicMarketDepths;

//            private WebSocket _webSocketPublicTrades;

//            private readonly string _webSocketPrivateUrl = "wss://fstream.xt.com/ws/user";

//            private readonly string _webSocketPublicUrl = "wss://fstream.xt.com/ws/market";

//            private readonly string _socketLocker = "webSocketLockerXT";

//            private void CreateWebSocketConnection()
//            {
//                try
//                {
//                    if (_webSocketPrivate != null)
//                    {
//                        return;
//                    }

//                    if (_myProxy != null)
//                    {
//                        _webSocketPrivate.SetProxy(_myProxy);
//                    }

//                    _webSocketPrivate = new WebSocket(_webSocketPrivateUrl);

//                    _webSocketPrivate.EmitOnPing = true;
//                    _webSocketPrivate.OnOpen += _webSocketPrivate_OnOpen;
//                    _webSocketPrivate.OnClose += _webSocketPrivate_OnClose;
//                    _webSocketPrivate.OnMessage += _webSocketPrivate_OnMessage;
//                    _webSocketPrivate.OnError += _webSocketPrivate_OnError;
//                    _webSocketPrivate.Connect();

//                    if (_webSocketPublicMarketDepths != null)
//                    {
//                        return;
//                    }

//                    _webSocketPublicMarketDepths = new WebSocket(_webSocketPublicUrl);

//                    _webSocketPublicMarketDepths.EmitOnPing = true;
//                    _webSocketPublicMarketDepths.OnOpen += _webSocketPublicMarketDepths_OnOpen;
//                    _webSocketPublicMarketDepths.OnClose += _webSocketPublicMarketDepths_OnClose;
//                    _webSocketPublicMarketDepths.OnMessage += _webSocketPublicMarketDepths_OnMessage;
//                    _webSocketPublicMarketDepths.OnError += _webSocketPublicMarketDepths_OnError;
//                    _webSocketPublicMarketDepths.Connect();

//                    if (_webSocketPublicTrades != null)
//                    {
//                        return;
//                    }

//                    _webSocketPublicTrades = new WebSocket(_webSocketPublicUrl);

//                    _webSocketPublicTrades.EmitOnPing = true;
//                    _webSocketPublicTrades.OnOpen += _webSocketPublicTrades_OnOpen;
//                    _webSocketPublicTrades.OnClose += _webSocketPublicTrades_OnClose;
//                    _webSocketPublicTrades.OnMessage += _webSocketPublicTrades_OnMessage;
//                    _webSocketPublicTrades.OnError += _webSocketPublicTrades_OnError;
//                    _webSocketPublicTrades.Connect();
//                }
//                catch
//                {
//                    SendLogMessage("Create WebSocket Connection error.", LogMessageType.Error);
//                }
//            }

//            private void DeleteWebsocketConnection()
//            {
//                if (_webSocketPublicMarketDepths != null)
//                {
//                    try
//                    {
//                        _webSocketPublicMarketDepths.OnOpen -= _webSocketPublicMarketDepths_OnOpen;
//                        _webSocketPublicMarketDepths.OnClose -= _webSocketPublicMarketDepths_OnClose;
//                        _webSocketPublicMarketDepths.OnMessage -= _webSocketPublicMarketDepths_OnMessage;
//                        _webSocketPublicMarketDepths.OnError -= _webSocketPublicMarketDepths_OnError;
//                        _webSocketPublicMarketDepths.CloseAsync();
//                    }
//                    catch
//                    {
//                        // ignore
//                    }

//                    _webSocketPublicMarketDepths = null;
//                }

//                if (_webSocketPublicTrades != null)
//                {
//                    try
//                    {
//                        _webSocketPublicTrades.OnOpen -= _webSocketPublicTrades_OnOpen;
//                        _webSocketPublicTrades.OnClose -= _webSocketPublicTrades_OnClose;
//                        _webSocketPublicTrades.OnMessage -= _webSocketPublicTrades_OnMessage;
//                        _webSocketPublicTrades.OnError -= _webSocketPublicTrades_OnError;
//                        _webSocketPublicTrades.CloseAsync();
//                    }
//                    catch
//                    {
//                        // ignore
//                    }

//                    _webSocketPublicTrades = null;
//                }

//                if (_webSocketPrivate != null)
//                {
//                    try
//                    {
//                        _webSocketPrivate.OnOpen -= _webSocketPrivate_OnOpen;
//                        _webSocketPrivate.OnClose -= _webSocketPrivate_OnClose;
//                        _webSocketPrivate.OnMessage -= _webSocketPrivate_OnMessage;
//                        _webSocketPrivate.OnError -= _webSocketPrivate_OnError;
//                        _webSocketPrivate.CloseAsync();
//                    }
//                    catch
//                    {
//                        // ignore
//                    }

//                    _webSocketPrivate = null;
//                }
//            }

//            private string _socketActivateLocker = "socketActivateLocker";

//            private void CheckFullActivation()
//            {
//                try
//                {
//                    lock (_socketActivateLocker)
//                    {
//                        if (_webSocketPrivate == null
//                        || _webSocketPrivate?.ReadyState != WebSocketState.Open)
//                        {
//                            Disconnect();
//                            return;
//                        }

//                        if (_webSocketPublicMarketDepths == null
//                            || _webSocketPublicMarketDepths?.ReadyState != WebSocketState.Open)
//                        {
//                            Disconnect();
//                            return;
//                        }

//                        if (_webSocketPublicTrades == null
//                            || _webSocketPublicTrades?.ReadyState != WebSocketState.Open)
//                        {
//                            Disconnect();
//                            return;
//                        }

//                        if (ServerStatus != ServerConnectStatus.Connect)
//                        {
//                            ServerStatus = ServerConnectStatus.Connect;
//                            ConnectEvent?.Invoke();
//                        }
//                    }
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage(exception.Message, LogMessageType.Error);
//                }
//            }

//            #endregion

//            #region 7 WebSocket events

//            private void _webSocketPublicTrades_OnError(object sender, ErrorEventArgs e)
//            {
//                try
//                {
//                    if (ServerStatus == ServerConnectStatus.Disconnect)
//                    {
//                        return;
//                    }

//                    if (e.Exception != null)
//                    {
//                        string message = e.Exception.ToString();

//                        if (message.Contains("The remote party closed the WebSocket connection"))
//                        {
//                            // ignore
//                        }
//                        else
//                        {
//                            SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
//                        }
//                    }
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("Data socket error" + exception.ToString(), LogMessageType.Error);
//                }
//            }

//            private void _webSocketPublicTrades_OnMessage(object sender, MessageEventArgs e)
//            {
//                try
//                {
//                    if (e == null)
//                    {
//                        return;
//                    }

//                    if (string.IsNullOrEmpty(e.Data))
//                    {
//                        return;
//                    }

//                    if (e.Data.Contains("pong"))
//                    {
//                        // pong message
//                        return;
//                    }

//                    if (FIFOListWebSocketPublicMarketDepthsMessage == null)
//                    {
//                        return;
//                    }

//                    FIFOListWebSocketPublicTradesMessage.Enqueue(e.Data);
//                }
//                catch (Exception error)
//                {
//                    SendLogMessage("WebSocketPublicTrades Message Received error: " + error.ToString(), LogMessageType.Error);
//                }
//            }

//            private void _webSocketPublicTrades_OnClose(object sender, CloseEventArgs e)
//            {
//                try
//                {
//                    if (ServerStatus != ServerConnectStatus.Disconnect)
//                    {
//                        string message = this.GetType().Name + OsLocalization.Market.Message101 + "\n";
//                        message += OsLocalization.Market.Message102;

//                        SendLogMessage(message, LogMessageType.Error);
//                        ServerStatus = ServerConnectStatus.Disconnect;
//                        DisconnectEvent?.Invoke();
//                    }
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage(exception.ToString(), LogMessageType.Error);
//                }
//            }

//            private void _webSocketPublicTrades_OnOpen(object sender, EventArgs e)
//            {
//                CheckFullActivation();
//                SendLogMessage("WebSocketPublicTrades Connection to public trades is Open", LogMessageType.System);
//            }

//            private void _webSocketPublicMarketDepths_OnError(object sender, ErrorEventArgs e)
//            {
//                try
//                {
//                    if (ServerStatus == ServerConnectStatus.Disconnect)
//                    {
//                        return;
//                    }

//                    if (e.Exception != null)
//                    {
//                        string message = e.Exception.ToString();

//                        if (message.Contains("The remote party closed the WebSocket connection"))
//                        {
//                            // ignore
//                        }
//                        else
//                        {
//                            SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
//                        }
//                    }
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("Data socket error" + exception.ToString(), LogMessageType.Error);
//                }
//            }

//            private void _webSocketPublicMarketDepths_OnMessage(object sender, MessageEventArgs e)
//            {
//                try
//                {
//                    if (e == null)
//                    {
//                        return;
//                    }

//                    if (string.IsNullOrEmpty(e.Data))
//                    {
//                        return;
//                    }

//                    if (e.Data.Contains("pong"))
//                    {
//                        // pong message
//                        return;
//                    }

//                    if (FIFOListWebSocketPublicMarketDepthsMessage == null)
//                    {
//                        return;
//                    }

//                    FIFOListWebSocketPublicMarketDepthsMessage.Enqueue(e.Data);
//                }
//                catch (Exception error)
//                {
//                    SendLogMessage("WebSocketPublic Message Received error: " + error.ToString(), LogMessageType.Error);
//                }
//            }

//            private void _webSocketPublicMarketDepths_OnClose(object sender, CloseEventArgs e)
//            {
//                try
//                {
//                    if (ServerStatus != ServerConnectStatus.Disconnect)
//                    {
//                        string message = this.GetType().Name + OsLocalization.Market.Message101 + "\n";
//                        message += OsLocalization.Market.Message102;

//                        SendLogMessage(message, LogMessageType.Error);
//                        ServerStatus = ServerConnectStatus.Disconnect;
//                        DisconnectEvent?.Invoke();
//                    }
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage(exception.ToString(), LogMessageType.Error);
//                }
//            }

//            private void _webSocketPublicMarketDepths_OnOpen(object sender, EventArgs e)
//            {
//                CheckFullActivation();
//                SendLogMessage("WebSocketPublic Connection to public data is Open", LogMessageType.System);
//            }

//            private void _webSocketPrivate_OnError(object sender, ErrorEventArgs e)
//            {
//                try
//                {
//                    if (ServerStatus == ServerConnectStatus.Disconnect)
//                    {
//                        return;
//                    }

//                    if (e.Exception != null)
//                    {
//                        string message = e.Exception.ToString();

//                        if (message.Contains("The remote party closed the WebSocket connection"))
//                        {
//                            // ignore
//                        }
//                        else
//                        {
//                            SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
//                        }
//                    }
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("Data socket error" + exception.ToString(), LogMessageType.Error);
//                }
//            }

//            private void _webSocketPrivate_OnMessage(object sender, MessageEventArgs e)
//            {
//                try
//                {
//                    if (ServerStatus == ServerConnectStatus.Disconnect)
//                    {
//                        return;
//                    }

//                    if (e == null)
//                    {
//                        return;
//                    }

//                    if (string.IsNullOrEmpty(e.Data))
//                    {
//                        return;
//                    }

//                    if (e.Data.Contains("pong"))
//                    {
//                        // pong message
//                        return;
//                    }

//                    if (FIFOListWebSocketPrivateMessage == null)
//                    {
//                        return;
//                    }

//                    FIFOListWebSocketPrivateMessage.Enqueue(e.Data);
//                }
//                catch (Exception error)
//                {
//                    SendLogMessage("WebSocketPrivate Message Received error: " + error.ToString(), LogMessageType.Error);
//                }
//            }

//            private void _webSocketPrivate_OnClose(object sender, CloseEventArgs e)
//            {
//                try
//                {
//                    if (ServerStatus != ServerConnectStatus.Disconnect)
//                    {
//                        string message = this.GetType().Name + OsLocalization.Market.Message101 + "\n";
//                        message += OsLocalization.Market.Message102;

//                        SendLogMessage(message, LogMessageType.Error);
//                        ServerStatus = ServerConnectStatus.Disconnect;
//                        DisconnectEvent?.Invoke();
//                    }
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage(exception.ToString(), LogMessageType.Error);
//                }
//            }

//            private void _webSocketPrivate_OnOpen(object sender, EventArgs e)
//            {
//                try
//                {
//                    CheckFullActivation();
//                    SendLogMessage("WebSocketPrivate Connection to private data is Open", LogMessageType.System);
//                    _webSocketPrivate.SendAsync($"{{\"method\":\"SUBSCRIBE\",\"params\":[\"order@{_listenKey}\",\"trade@{_listenKey}\",\"balance@{_listenKey}\"],\"id\":\"sub-1\"}}");
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
//                }
//            }

//            #endregion

//            #region 8 WebSocket check alive

//            private void ConnectionCheck()
//            {
//                while (true)
//                {
//                    try
//                    {
//                        if (ServerStatus == ServerConnectStatus.Disconnect)
//                        {
//                            Thread.Sleep(2000);
//                            continue;
//                        }

//                        Thread.Sleep(10000);

//                        if (_webSocketPublicMarketDepths == null)
//                        {
//                            continue;
//                        }

//                        if (_webSocketPrivate == null)
//                        {
//                            continue;
//                        }

//                        if (_webSocketPublicTrades == null)
//                        {
//                            continue;
//                        }

//                        if (_webSocketPublicMarketDepths.ReadyState == WebSocketState.Open
//                            && _webSocketPublicTrades.ReadyState == WebSocketState.Open
//                            && _webSocketPrivate.ReadyState == WebSocketState.Open)
//                        {
//                            _webSocketPublicMarketDepths.SendAsync("ping");
//                            _webSocketPrivate.SendAsync("ping");
//                            _webSocketPublicTrades.SendAsync("ping");
//                        }
//                        else
//                        {
//                            Dispose();
//                        }
//                    }
//                    catch (Exception error)
//                    {
//                        SendLogMessage(error.ToString(), LogMessageType.Error);
//                    }
//                }
//            }

//            #endregion

//            #region 9 Security Subscribed

//            private readonly RateGate _rateGateSubscribed = new RateGate(1, TimeSpan.FromMilliseconds(200));

//            public void Subscribe(Security security)
//            {
//                try
//                {
//                    _rateGateSubscribed.WaitToProceed();

//                    if (ServerStatus == ServerConnectStatus.Disconnect)
//                    {
//                        return;
//                    }

//                    CreateSubscribedSecurityMessageWebSocket(security);
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage(exception.ToString(), LogMessageType.Error);
//                }
//            }

//            private void CreateSubscribedSecurityMessageWebSocket(Security security)
//            {
//                for (int i = 0; i < _subscribedSecurities.Count; i++)
//                {
//                    if (_subscribedSecurities[i].Equals(security.Name, StringComparison.OrdinalIgnoreCase))
//                    {
//                        return;
//                    }
//                }

//                lock (_socketLocker)
//                {
//                    _webSocketPublicMarketDepths.SendAsync($"{{\"method\":\"subscribe\",\"params\":[\"depth_update@{security.Name}\", \"depth@{security.Name},{20}\"],\"id\":\"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");
//                    _webSocketPublicTrades.SendAsync($"{{\"method\":\"subscribe\",\"params\":[\"trade@{security.Name}\"],\"id\":\"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");
//                }
//            }

//            public event Action<News> NewsEvent;

//            #endregion

//            #region 10 WebSocket parsing the messages

//            private ConcurrentQueue<string> FIFOListWebSocketPublicMarketDepthsMessage = new ConcurrentQueue<string>();

//            private ConcurrentQueue<string> FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

//            private ConcurrentQueue<string> FIFOListWebSocketPublicTradesMessage = new ConcurrentQueue<string>();

//            private void PublicMarketDepthsMessageReader()
//            {
//                while (true)
//                {
//                    try
//                    {
//                        if (ServerStatus != ServerConnectStatus.Connect)
//                        {
//                            Thread.Sleep(1000);
//                            continue;
//                        }

//                        if (FIFOListWebSocketPublicMarketDepthsMessage.IsEmpty)
//                        {
//                            Thread.Sleep(1);
//                            continue;
//                        }

//                        string message;

//                        FIFOListWebSocketPublicMarketDepthsMessage.TryDequeue(out message);

//                        if (message == null)
//                        {
//                            continue;
//                        }

//                        if (message.Equals("pong", StringComparison.OrdinalIgnoreCase))
//                        {
//                            continue;
//                        }

//                        XTFuturesResponseWebSocket<object> action = JsonConvert.DeserializeObject<XTFuturesResponseWebSocket<object>>(message);

//                        if (action != null && action.topic != null)
//                        {
//                            if (action.topic.Equals("depth_update", StringComparison.OrdinalIgnoreCase))
//                            {
//                                UpdateDepth(message);
//                            }
//                            else if (action.topic.Equals("depth", StringComparison.OrdinalIgnoreCase))
//                            {
//                                SnapshotDepth(message);
//                            }
//                        }
//                    }
//                    catch (Exception exception)
//                    {
//                        Thread.Sleep(2000);
//                    }
//                }
//            }

//            private void PublicTradesMessageReader()
//            {
//                while (true)
//                {
//                    try
//                    {
//                        if (ServerStatus != ServerConnectStatus.Connect)
//                        {
//                            Thread.Sleep(1000);
//                            continue;
//                        }

//                        if (FIFOListWebSocketPublicTradesMessage.IsEmpty)
//                        {
//                            Thread.Sleep(1);
//                            continue;
//                        }

//                        FIFOListWebSocketPublicTradesMessage.TryDequeue(out string message);

//                        if (message == null)
//                        {
//                            continue;
//                        }

//                        if (message.Equals("pong", StringComparison.OrdinalIgnoreCase))
//                        {
//                            continue;
//                        }

//                        XTFuturesResponseWebSocket<object> action = JsonConvert.DeserializeAnonymousType(message, new XTFuturesResponseWebSocket<object>());

//                        if (action != null && action.topic != null && action.@event != null)
//                        {
//                            string evt = action.@event;

//                            if (!string.IsNullOrEmpty(evt) && evt.StartsWith("trade@", StringComparison.OrdinalIgnoreCase))
//                            {
//                                UpdateTrade(message);
//                            }
//                        }
//                    }
//                    catch (Exception exception)
//                    {
//                        Thread.Sleep(2000);
//                    }
//                }
//            }

//            private void PrivateMessageReader()
//            {
//                while (true)
//                {
//                    try
//                    {
//                        if (ServerStatus != ServerConnectStatus.Connect)
//                        {
//                            Thread.Sleep(1000);
//                            continue;
//                        }

//                        if (FIFOListWebSocketPrivateMessage.IsEmpty)
//                        {
//                            Thread.Sleep(1);
//                            continue;
//                        }

//                        FIFOListWebSocketPrivateMessage.TryDequeue(out string message);

//                        if (message == null)
//                        {
//                            continue;
//                        }

//                        if (message.Equals("pong"))
//                        {
//                            continue;
//                        }

//                        XTFuturesResponseWebSocket<object> action = JsonConvert.DeserializeAnonymousType(message, new XTFuturesResponseWebSocket<object>());

//                        if (action == null || action.topic == null)
//                        {
//                            continue;
//                        }

//                        if (action.topic.Equals("order"))
//                        {
//                            UpdateOrder(message);
//                            continue;
//                        }

//                        if (action.topic.Equals("balance"))
//                        {
//                            UpdatePortfolio(message);
//                            continue;
//                        }

//                        if (action.topic.Equals("trade"))
//                        {
//                            UpdateMyTrade(message);
//                            continue;
//                        }
//                    }
//                    catch (Exception exception)
//                    {
//                        Thread.Sleep(2000);
//                    }
//                }
//            }

//            private void UpdateTrade(string message)
//            {
//                try
//                {
//                    XTFuturesResponseWebSocket<XTFuturesPublicTrade> responseTrade = JsonConvert.DeserializeAnonymousType(message, new XTFuturesResponseWebSocket<XTFuturesPublicTrade>());

//                    if (responseTrade?.data == null)
//                    {
//                        return;
//                    }

//                    Trade trade = new Trade();
//                    trade.SecurityNameCode = responseTrade.data?.s ?? string.Empty;
//                    trade.Price = (responseTrade.data?.p).ToDecimal();
//                    trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseTrade.data?.t));
//                    trade.Volume = (responseTrade.data?.a).ToDecimal();
//                    trade.Side = responseTrade.data?.m?.Equals("BID", StringComparison.OrdinalIgnoreCase) == true ? Side.Buy : Side.Sell;
//                    trade.Id = Convert.ToInt64(responseTrade.data?.t).ToString();

//                    NewTradesEvent?.Invoke(trade);
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("UpdateTrade error: " + exception.ToString(), LogMessageType.Error);
//                }
//            }

//            private List<MarketDepth> _marketDepths = new List<MarketDepth>();

//            public event Action<MarketDepth> MarketDepthEvent;

//            private DateTime _lastTimeMd = DateTime.MinValue;

//            private bool startDepth = true;

//            int level = 20;

//            private MarketDepth GetOrCreateDepth(string symbol)
//            {
//                for (int i = 0; i < _marketDepths.Count; i++)
//                {
//                    if (_marketDepths[i].SecurityNameCode == symbol)
//                    {
//                        return _marketDepths[i];
//                    }
//                }

//                MarketDepth md = new MarketDepth
//                {
//                    SecurityNameCode = symbol,
//                    Asks = new List<MarketDepthLevel>(level),
//                    Bids = new List<MarketDepthLevel>(level)
//                };

//                _marketDepths.Add(md);

//                return md;
//            }

//            private int CompareAsk(MarketDepthLevel x, MarketDepthLevel y) => x.Price.CompareTo(y.Price);
//            private int CompareBid(MarketDepthLevel x, MarketDepthLevel y) => y.Price.CompareTo(x.Price);

//            private void UpsertLevel(List<MarketDepthLevel> list, double price, double qty, bool isAsk)
//            {
//                int idx = -1;

//                for (int i = 0; i < list.Count; i++)
//                {
//                    if (list[i].Price == price)
//                    {
//                        idx = i;
//                        break;
//                    }
//                }

//                if (idx >= 0)
//                {
//                    if (qty == 0)
//                    {
//                        list.RemoveAt(idx);
//                    }
//                    else
//                    {
//                        MarketDepthLevel lvl = list[idx];

//                        if (isAsk)
//                        {
//                            lvl.Ask = qty;
//                        }
//                        else
//                        {
//                            lvl.Bid = qty;
//                        }

//                        list[idx] = lvl;
//                    }

//                    return;
//                }

//                if (qty == 0)
//                {
//                    return;
//                }

//                MarketDepthLevel lvlNew = isAsk
//                    ? new MarketDepthLevel { Price = price, Ask = qty }
//                    : new MarketDepthLevel { Price = price, Bid = qty };

//                int pos = 0;
//                while (pos < list.Count &&
//                       (isAsk ? list[pos].Price < price
//                              : list[pos].Price > price))
//                {
//                    pos++;
//                }

//                list.Insert(pos, lvlNew);

//                if (list.Count > level)
//                {
//                    list.RemoveAt(20);
//                }
//            }

//            private void ApplySnapshotSide(List<List<string>> side, List<MarketDepthLevel> dest, bool isAsk, int maxLevels = 20)
//            {
//                dest.Clear();

//                if (side == null || side.Count == 0)
//                {
//                    return;
//                }

//                int take = Math.Min(maxLevels, side.Count);

//                for (int i = 0; i < take; i++)
//                {
//                    if (side[i] == null || side[i].Count < 2)
//                    {
//                        continue;
//                    }

//                    decimal p = (side[i][0]).ToDecimal();
//                    decimal q = (side[i][1]).ToDecimal();

//                    if (q <= 0m)
//                    {
//                        continue;
//                    }

//                    dest.Add(isAsk
//                        ? new MarketDepthLevel { Price = (double)p, Ask = (double)q }
//                        : new MarketDepthLevel { Price = (double)p, Bid = (double)q });
//                }

//                if (isAsk)
//                {
//                    dest.Sort(CompareAsk);
//                }
//                else
//                {
//                    dest.Sort(CompareBid);
//                }

//                if (dest.Count > maxLevels)
//                {
//                    dest.RemoveRange(maxLevels, dest.Count - maxLevels);
//                }
//            }

//            private void ApplyIncrementSide(List<List<string>> side, List<MarketDepthLevel> dest, bool isAsk)
//            {
//                if (side == null)
//                {
//                    return;
//                }

//                for (int i = 0; i < side.Count; i++)
//                {
//                    decimal p = (side[i][0]).ToDecimal();
//                    decimal q = (side[i][1]).ToDecimal();

//                    UpsertLevel(dest, (double)p, (double)q, isAsk);
//                }
//            }

//            private void SnapshotDepth(string message)
//            {
//                try
//                {
//                    XTFuturesResponseWebSocket<XTFuturesSnapshotDepth> responseDepth =
//                        JsonConvert.DeserializeObject<XTFuturesResponseWebSocket<XTFuturesSnapshotDepth>>(message);

//                    XTFuturesSnapshotDepth depth = responseDepth?.data;

//                    if (depth == null)
//                    {
//                        return;
//                    }

//                    string symbol = depth.s;

//                    if (string.IsNullOrWhiteSpace(symbol))
//                    {
//                        SendLogMessage("SnapshotDepth: empty symbol in payload", LogMessageType.Error);
//                        return;
//                    }

//                    MarketDepth copyToFire = null;

//                    lock (_mdLock)
//                    {
//                        MarketDepth md = GetOrCreateDepth(symbol);

//                        if (md.Asks == null)
//                        {
//                            md.Asks = new List<MarketDepthLevel>(level);
//                        }

//                        if (md.Bids == null)
//                        {
//                            md.Bids = new List<MarketDepthLevel>(level);
//                        }

//                        ApplySnapshotSide(depth.a, md.Asks, isAsk: true, maxLevels: level);
//                        ApplySnapshotSide(depth.b, md.Bids, isAsk: false, maxLevels: level);

//                        md.Time = ServerTime;

//                        if (md.Time < _lastTimeMd)
//                        {
//                            md.Time = _lastTimeMd;
//                        }
//                        else if (md.Time == _lastTimeMd)
//                        {
//                            _lastTimeMd = DateTime.FromBinary(_lastTimeMd.Ticks + 1);
//                            md.Time = _lastTimeMd;
//                        }

//                        _lastTimeMd = md.Time;

//                        _awaitingSnapshot.Remove(symbol);

//                        startDepth = false;

//                        if (_bufferBySymbol != null &&
//                            _bufferBySymbol.TryGetValue(symbol, out List<XTFuturesUpdateDepth> list) &&
//                            list != null && list.Count > 0)
//                        {
//                            for (int i = 0; i < list.Count; i++)
//                            {
//                                XTFuturesUpdateDepth ev = list[i];

//                                ApplyIncrementSide(ev.a, md.Asks, isAsk: true);
//                                ApplyIncrementSide(ev.b, md.Bids, isAsk: false);
//                            }

//                            list.Clear();
//                        }

//                        copyToFire = md.GetCopy();
//                    }

//                    MarketDepthEvent?.Invoke(copyToFire);
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("SnapshotDepth error: " + exception, LogMessageType.Error);
//                }
//            }

//            private readonly object _mdLock = new object();

//            private readonly Dictionary<string, List<XTFuturesUpdateDepth>> _bufferBySymbol =
//                new Dictionary<string, List<XTFuturesUpdateDepth>>();

//            private readonly HashSet<string> _awaitingSnapshot = new HashSet<string>();

//            private void UpdateDepth(string message)
//            {
//                try
//                {
//                    XTFuturesResponseWebSocket<XTFuturesUpdateDepth> resp = JsonConvert.DeserializeObject
//                       <XTFuturesResponseWebSocket<XTFuturesUpdateDepth>>(message);

//                    XTFuturesUpdateDepth depth = resp?.data;
//                    if (depth == null)
//                    {
//                        return;
//                    }

//                    string symbol = depth.s;

//                    if (string.IsNullOrWhiteSpace(symbol))
//                    {
//                        SendLogMessage("UpdateDepth: empty symbol in payload", LogMessageType.Error);
//                        return;
//                    }

//                    MarketDepth copyToFire = null;

//                    lock (_mdLock)
//                    {
//                        MarketDepth md = GetOrCreateDepth(symbol);

//                        bool needsSnapshot = startDepth || md.Asks == null ||
//                                             md.Bids == null || md.Asks.Count == 0 ||
//                                             md.Bids.Count == 0 || _awaitingSnapshot.Contains(symbol);

//                        if (needsSnapshot)
//                        {
//                            _awaitingSnapshot.Add(symbol);

//                            if (!_bufferBySymbol.TryGetValue(symbol, out List<XTFuturesUpdateDepth> list) || list == null)
//                            {
//                                list = new List<XTFuturesUpdateDepth>();
//                                _bufferBySymbol[symbol] = list;
//                            }

//                            list.Add(depth);
//                        }
//                        else
//                        {
//                            ApplyIncrementSide(depth.a, md.Asks, isAsk: true);
//                            ApplyIncrementSide(depth.b, md.Bids, isAsk: false);

//                            md.Time = ServerTime;

//                            if (md.Time < _lastTimeMd)
//                            {
//                                md.Time = _lastTimeMd;
//                            }
//                            else if (md.Time == _lastTimeMd)
//                            {
//                                _lastTimeMd = DateTime.FromBinary(_lastTimeMd.Ticks + 1);

//                                md.Time = _lastTimeMd;
//                            }

//                            _lastTimeMd = md.Time;

//                            copyToFire = md.GetCopy();
//                        }
//                    }

//                    if (copyToFire != null)

//                        MarketDepthEvent?.Invoke(copyToFire);
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("UpdateDepth error: " + exception, LogMessageType.Error);
//                }
//            }

//            private void UpdateMyTrade(string message)
//            {
//                try
//                {
//                    XTFuturesResponseWebSocket<XTFuturesMyTrade> response = JsonConvert.DeserializeObject<XTFuturesResponseWebSocket<XTFuturesMyTrade>>(message);

//                    if (response == null || response.data == null)
//                    {
//                        return;
//                    }

//                    MyTrade myTrade = new MyTrade();

//                    myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(response.data.timestamp));
//                    myTrade.NumberOrderParent = response.data.orderId;
//                    myTrade.NumberTrade = Convert.ToInt64(response.data.timestamp).ToString();
//                    myTrade.Price = response.data.price.ToDecimal();
//                    myTrade.SecurityNameCode = response.data.symbol;
//                    myTrade.Volume = response.data.quantity.ToDecimal();

//                    myTrade.Side = response.data.orderSide == "BUY" ? Side.Buy : Side.Sell;

//                    try
//                    {
//                        string oSide = response.data.orderSide;
//                        string pSide = response.data.positionSide;

//                        if (!string.IsNullOrWhiteSpace(response.data.symbol) &&
//                            (oSide == "BUY" || oSide == "SELL") &&
//                            (pSide == "LONG" || pSide == "SHORT"))
//                        {
//                            decimal qtyAbs;
//                            string qtyStr = response.data.quantity;
//                            if (!decimal.TryParse(qtyStr, NumberStyles.Any, CultureInfo.InvariantCulture, out qtyAbs))
//                                qtyAbs = 0m;

//                            if (qtyAbs > 0m)
//                            {
//                                ApplyTradeToPositions(response.data.symbol,
//                                                      response.data.orderSide,
//                                                      response.data.positionSide,
//                                                      qtyAbs);
//                            }
//                        }
//                    }
//                    catch (Exception exception)
//                    {
//                        SendLogMessage("Update positions by WS trade failed: " + exception, LogMessageType.Error);
//                    }

//                    SendLogMessage($"Private  tradeId={myTrade.NumberTrade} ordPar={myTrade.NumberOrderParent} sym={myTrade.SecurityNameCode} side={myTrade.Side} price={myTrade.Price} qty={myTrade.Volume}", LogMessageType.Error);

//                    MyTradeEvent?.Invoke(myTrade);
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("UpdateMyTrade error: " + exception.ToString(), LogMessageType.Error);
//                }
//            }

//            private readonly object _posLock = new object();

//            private void ApplyTradeToPositions(string symbol, string orderSide, string positionSide, decimal qtyAbs)
//            {
//                if (_portfolios == null || _portfolios.Count == 0)
//                {
//                    return;
//                }

//                Portfolio portfolio = _portfolios[0];

//                string code = symbol + "_" + positionSide;

//                decimal delta;
//                if (positionSide == "LONG")
//                {
//                    delta = (orderSide == "BUY") ? +qtyAbs : -qtyAbs;
//                }
//                else // SHORT
//                {
//                    delta = (orderSide == "SELL") ? -qtyAbs : +qtyAbs;
//                }

//                lock (_posLock)
//                {
//                    List<PositionOnBoard> list = portfolio.GetPositionOnBoard();

//                    PositionOnBoard pos = null;

//                    for (int i = 0; i < list.Count; i++)
//                    {
//                        var positions = list[i];
//                        if (positions != null && !string.IsNullOrWhiteSpace(positions.SecurityNameCode) &&
//                            positions.SecurityNameCode.Equals(code, StringComparison.OrdinalIgnoreCase))
//                        {
//                            pos = positions;
//                            break;
//                        }
//                    }

//                    if (pos == null)
//                    {
//                        pos = new PositionOnBoard
//                        {
//                            PortfolioName = portfolio.Number,
//                            SecurityNameCode = code,
//                            ValueCurrent = 0m,
//                            UnrealizedPnl = 0m
//                        };
//                    }

//                    pos.ValueCurrent += delta;
//                    portfolio.SetNewPosition(pos);
//                }

//                PortfolioEvent?.Invoke(_portfolios);
//            }

//            private void UpdatePortfolio(string message)
//            {
//                try
//                {
//                    XTFuturesResponseWebSocket<XTFuturesResponsePortfolio> portfolios =
//                        JsonConvert.DeserializeObject<XTFuturesResponseWebSocket<XTFuturesResponsePortfolio>>(message);

//                    if (portfolios == null || portfolios.data == null ||
//                       _portfolios == null || _portfolios.Count == 0)
//                    {
//                        return;
//                    }

//                    Portfolio portfolio = _portfolios[0];

//                    decimal wallet = portfolios.data.walletBalance.ToDecimal();
//                    decimal orderFrozen = portfolios.data.openOrderMarginFrozen.ToDecimal();
//                    decimal locked = orderFrozen;
//                    decimal available = wallet - locked;

//                    if (available < 0m)
//                    {
//                        available = 0m;
//                    }

//                    portfolio.ValueCurrent = 1;

//                    PositionOnBoard pos = new PositionOnBoard();
//                    pos.PortfolioName = _portfolioName;
//                    pos.SecurityNameCode = (portfolios.data.coin.ToUpper() ?? "USDT");
//                    pos.ValueBlocked = Math.Round(locked, 5);
//                    pos.ValueCurrent = Math.Round(available, 5);

//                    portfolio.SetNewPosition(pos);

//                    PortfolioEvent?.Invoke(_portfolios);
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("UpdatePortfolio error: " + exception.ToString(), LogMessageType.Error);
//                }
//            }

//            private void UpdateOrder(string message)
//            {
//                try
//                {
//                    XTFuturesResponseWebSocket<XTFuturesUpdateOrder> order =
//                        JsonConvert.DeserializeObject<XTFuturesResponseWebSocket<XTFuturesUpdateOrder>>(message);

//                    if (order == null)
//                    {
//                        return;
//                    }

//                    Order newOrder = new Order();

//                    XTFuturesUpdateOrder item = order.data;

//                    newOrder.SecurityNameCode = item.symbol;
//                    newOrder.SecurityClassCode = GetNameClass(item.symbol);
//                    newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createdTime));
//                    newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.updatedTime));
//                    newOrder.NumberMarket = item.orderId;
//                    newOrder.NumberUser = Convert.ToInt32(item.clientOrderId);
//                    newOrder.Side = item.orderSide.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;
//                    newOrder.State = GetOrderState(item.state);

//                    if (string.Equals(item.orderType, "MARKET", StringComparison.OrdinalIgnoreCase))
//                    {
//                        newOrder.TypeOrder = OrderPriceType.Market;
//                    }
//                    else
//                    {
//                        newOrder.TypeOrder = OrderPriceType.Limit;
//                    }

//                    newOrder.ServerType = ServerType.XTFutures;
//                    newOrder.PortfolioNumber = _portfolioName;
//                    newOrder.Volume = item.origQty.ToDecimal();
//                    newOrder.Price = item.price.ToDecimal();

//                    if (newOrder.State == OrderStateType.Done)
//                    {
//                        newOrder.TimeDone = newOrder.TimeCallBack;
//                    }
//                    else if (newOrder.State == OrderStateType.Cancel)
//                    {
//                        newOrder.TimeCancel = newOrder.TimeCallBack;
//                    }

//                    MyOrderEvent?.Invoke(newOrder);

//                    if (newOrder.State == OrderStateType.Done || newOrder.State == OrderStateType.Partial)
//                    {
//                        CreateQueryMyTrade(newOrder.NumberMarket);
//                    }
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("UpdateOrder error: " + exception.ToString(), LogMessageType.Error);
//                }
//            }

//            private OrderStateType GetOrderState(string orderStatusResponse)
//            {
//                OrderStateType stateType;

//                switch (orderStatusResponse.ToLower())
//                {
//                    case ("new"):
//                    case ("placed"):
//                        stateType = OrderStateType.Active;
//                        break;

//                    case ("partially_filled"):
//                        stateType = OrderStateType.Partial;
//                        break;

//                    case ("filled"):
//                        stateType = OrderStateType.Done;
//                        break;

//                    case ("canceled"):
//                    case ("expired"):
//                        stateType = OrderStateType.Cancel;
//                        break;

//                    case ("rejected"):
//                        stateType = OrderStateType.Fail;
//                        break;

//                    default:
//                        stateType = OrderStateType.None;
//                        break;
//                }

//                return stateType;
//            }

//            #endregion

//            #region 11 Trade

//            private readonly RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(300));

//            private readonly RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(300));

//            private readonly RateGate _rateGatePosition = new RateGate(1, TimeSpan.FromMilliseconds(300));

//            private readonly RateGate _rateGateChangeOrderPrice = new RateGate(1, TimeSpan.FromMilliseconds(300));

//            private readonly RateGate _rateGateOpenOrder = new RateGate(1, TimeSpan.FromMilliseconds(300));

//            public event Action<MyTrade> MyTradeEvent;

//            public event Action<Trade> NewTradesEvent;

//            public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;

//            //Открыть / увеличить лонг → orderSide = BUY, positionSide = LONG

//            //Закрыть / уменьшить лонг → orderSide = SELL, positionSide = LONG

//            //Открыть / увеличить шорт → orderSide = SELL, positionSide = SHORT

//            //Закрыть / уменьшить шорт → orderSide = BUY, positionSide = SHORT

//            public void SendOrder(Order order)
//            {
//                _rateGateSendOrder.WaitToProceed();

//                try
//                {
//                    if (order == null || string.IsNullOrWhiteSpace(order.SecurityNameCode))
//                    {
//                        SendLogMessage("SendOrder> bad order or symbol", LogMessageType.Error);
//                        CreateOrderFail(order);
//                        return;
//                    }

//                    if (order.Volume <= 0)
//                    {
//                        SendLogMessage($"SendOrder> bad volume {order.Volume}", LogMessageType.Error);
//                        CreateOrderFail(order);
//                        return;
//                    }

//                    bool isMarket = order.TypeOrder == OrderPriceType.Market;
//                    if (!isMarket && order.Price <= 0)
//                    {
//                        SendLogMessage($"SendOrder> bad price {order.Price}", LogMessageType.Error);
//                        CreateOrderFail(order);
//                        return;
//                    }

//                    string symbol = order.SecurityNameCode;

//                    string orderSide = order.Side == Side.Buy ? "BUY" : "SELL";

//                    // --- Определяем positionSide из текущих позиций (user задаёт только BUY/SELL)
//                    // SELL: если есть LONG>0 → уменьшаем LONG; иначе открываем SHORT
//                    // BUY:  если есть SHORT>0 → уменьшаем SHORT; иначе открываем  LONG
//                    decimal longAbs = GetSidePositionAbs(symbol, "LONG");
//                    decimal shortAbs = GetSidePositionAbs(symbol, "SHORT");

//                    string positionSide;
//                    bool isReduceIntent;

//                    if (orderSide == "SELL")
//                    {
//                        if (longAbs > 0m)
//                        {
//                            positionSide = "LONG";
//                            isReduceIntent = true;
//                        }
//                        else
//                        {
//                            positionSide = "SHORT";
//                            isReduceIntent = false;
//                        }
//                    }
//                    else
//                    {
//                        if (shortAbs > 0m)
//                        {
//                            positionSide = "SHORT";
//                            isReduceIntent = true;
//                        }
//                        else
//                        {
//                            positionSide = "LONG"; isReduceIntent = false;
//                        }
//                    }

//                    // MARKET — без price/timeInForce; LIMIT — GTC.
//                    // Если уменьшаем (reduce), чтобы не открыть противоположную сторону остатком — ставим IOC.
//                    string tif = null;
//                    if (!isMarket)
//                    {
//                        tif = isReduceIntent ? "IOC" : "GTC";// IOC уменьшаем иначе увеличиваем
//                    }

//                    var data = new XTFuturesSendOrder
//                    {
//                        symbol = symbol,
//                        clientOrderId = order.NumberUser.ToString(),
//                        orderSide = orderSide,
//                        orderType = isMarket ? "MARKET" : "LIMIT",
//                        price = isMarket ? null : order.Price.ToString(CultureInfo.InvariantCulture),
//                        timeInForce = isMarket ? null : tif,
//                        origQty = order.Volume.ToString(CultureInfo.InvariantCulture),
//                        positionSide = positionSide
//                    };

//                    order.PortfolioNumber = _portfolioName;

//                    IRestResponse responseMessage = CreatePrivateQuery("/future/trade/v1/order/create", Method.POST, null, data);

//                    if (responseMessage == null)
//                    {
//                        SendLogMessage("SendOrder: response is null", LogMessageType.Error);
//                        CreateOrderFail(order);
//                        return;
//                    }

//                    XTFuturesResponseRest<string> stateResponse = JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(responseMessage.Content);

//                    if (responseMessage.StatusCode != HttpStatusCode.OK)
//                    {
//                        return;
//                    }

//                    if (stateResponse.returnCode == "0"
//                       && string.Equals(stateResponse.msgInfo, "SUCCESS", StringComparison.OrdinalIgnoreCase))
//                    {
//                        order.NumberMarket = stateResponse.result;

//                        SendLogMessage($"Order sent success. NumUser: {order.NumberUser}, OrderId: {order.NumberMarket}", LogMessageType.Trade);

//                        if (isMarket)
//                        {
//                            order.State = OrderStateType.Done;
//                            CreateQueryMyTrade(order.NumberMarket);
//                        }
//                        else
//                        {
//                            order.State = OrderStateType.Active;
//                        }

//                        MyOrderEvent?.Invoke(order);
//                    }
//                    else
//                    {
//                        SendLogMessage($"SendOrder Fail: {stateResponse.error.code}, {stateResponse.error.msg}", LogMessageType.Error);
//                        CreateOrderFail(order);
//                    }
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("SendOrder error: " + exception, LogMessageType.Error);
//                }
//            }

//            private decimal GetSidePositionAbs(string symbol, string sideUpper)
//            {
//                try
//                {
//                    if (_portfolios == null || _portfolios.Count == 0)
//                    {
//                        return 0m;
//                    }

//                    List<PositionOnBoard> list = _portfolios[0].GetPositionOnBoard();

//                    if (list == null || list.Count == 0)
//                    {
//                        return 0m;
//                    }

//                    string keyUnd = symbol + "_" + sideUpper;

//                    for (int i = 0; i < list.Count; i++)
//                    {
//                        var p = list[i];

//                        if (p == null || string.IsNullOrWhiteSpace(p.SecurityNameCode))
//                        {
//                            continue;
//                        }

//                        string name = p.SecurityNameCode;

//                        if (name.Equals(keyUnd, StringComparison.OrdinalIgnoreCase))
//                        {
//                            return Math.Abs(p.ValueCurrent);
//                        }
//                    }
//                }
//                catch
//                {
//                    /* ignore */
//                }

//                return 0m;
//            }

//            public void ChangeOrderPrice(Order order, decimal newPrice)
//            {
//                _rateGateChangeOrderPrice.WaitToProceed();

//                try
//                {
//                    if (order.TypeOrder == OrderPriceType.Market || order.State == OrderStateType.Done)
//                    {
//                        SendLogMessage("ChangeOrderPrice> Can't change price for  Order Market", LogMessageType.Error);
//                        return;
//                    }

//                    var body = new
//                    {
//                        orderId = long.Parse(order.NumberMarket, CultureInfo.InvariantCulture),
//                        price = newPrice.ToString("0.#######", CultureInfo.InvariantCulture),
//                        origQty = order.Volume.ToString("0.#######", CultureInfo.InvariantCulture)
//                    };

//                    if (newPrice <= 0)
//                    {
//                        SendLogMessage($"ChangeOrderPrice> bad price: {newPrice}", LogMessageType.Error);
//                        return;
//                    }

//                    if (string.IsNullOrWhiteSpace(order.NumberMarket))
//                    {
//                        SendLogMessage("ChangeOrderPrice> empty exchange order id (NumberMarket)", LogMessageType.Error);
//                        return;
//                    }

//                    IRestResponse response = CreatePrivateQuery("/future/trade/v1/order/update", Method.POST, null, body);

//                    if (order.State == OrderStateType.Cancel || order.State == OrderStateType.Done)
//                    {
//                        return;
//                    }

//                    XTFuturesResponseRest<string> stateResponse = JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(response.Content);

//                    if (response.StatusCode == HttpStatusCode.OK && stateResponse != null && stateResponse.returnCode == "0")
//                    {
//                        order.PortfolioNumber = _portfolioName;

//                        if (!string.IsNullOrEmpty(stateResponse.result))
//                        {
//                            order.Price = newPrice;
//                            order.Volume = order.Volume;

//                            SendLogMessage($"Success! Order {order.NumberMarket} updated." +
//                                $" New orderId={stateResponse.result}, price={newPrice}, qty={order.Volume}", LogMessageType.Error);
//                        }
//                        else
//                        {
//                            SendLogMessage($"Update returned an empty result {response.Content}", LogMessageType.Error);
//                        }
//                    }
//                    else
//                    {
//                        string code = stateResponse.error.code;
//                        string msg = stateResponse.error.msg;
//                        SendLogMessage($"Error: returnCode={stateResponse?.returnCode}, code={code}," +
//                            $" msg={msg}, raw={response.Content}", LogMessageType.Error);
//                    }
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage(exception.ToString(), LogMessageType.Error);
//                }
//            }

//            public void CancelAllOrders()
//            {
//                _rateGateCancelOrder.WaitToProceed();

//                try
//                {
//                    var body = new { symbol = "" };

//                    IRestResponse responseMessage = CreatePrivateQuery("/future/trade/v1/order/cancel-all", Method.POST, null, body);

//                    XTFuturesResponseRest<string> stateResponse = JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(responseMessage.Content);

//                    if (responseMessage.StatusCode == HttpStatusCode.OK)
//                    {
//                        if (stateResponse.returnCode.Equals("0") && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.CurrentCulture))
//                        {
//                            // ignore
//                        }
//                    }
//                    else
//                    {
//                        SendLogMessage($"CancelAllOrders>  error State Code: {responseMessage.StatusCode}", LogMessageType.Error);
//                    }
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("CancelAllOrders error: " + exception.ToString(), LogMessageType.Error);
//                }
//            }

//            public void CancelAllOrdersToSecurity(Security security)
//            {
//                _rateGateCancelOrder.WaitToProceed();

//                try
//                {
//                    string jsonRequest = JsonConvert.SerializeObject(
//            new XTFuturesCancelAllOrders { symbol = security.Name },
//            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

//                    IRestResponse responseMessage = CreatePrivateQuery("/future/trade/v1/order/cancel-all", Method.POST, null, body: jsonRequest);

//                    XTFuturesResponseRest<string> stateResponse = JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(responseMessage.Content);

//                    if (responseMessage.StatusCode == HttpStatusCode.OK)
//                    {
//                        if (stateResponse.returnCode.Equals("0") && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.CurrentCulture))
//                        {
//                            // ignore
//                        }
//                        else
//                        {
//                            SendLogMessage($"CancelAllOrdersToSecurity error, Code: {stateResponse.returnCode}\n"
//                                + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
//                        }
//                    }
//                    else
//                    {
//                        SendLogMessage($"CancelAllOrdersToSecurity>  State Code: {responseMessage.StatusCode}", LogMessageType.Error);
//                    }
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("CancelAllOrdersToSecurity error: " + exception.ToString(), LogMessageType.Error);
//                }
//            }

//            public bool CancelOrder(Order order)
//            {
//                _rateGateCancelOrder.WaitToProceed();

//                try
//                {
//                    var body = new
//                    {
//                        orderId = order.NumberMarket.ToString(CultureInfo.InvariantCulture)
//                    };

//                    IRestResponse response = CreatePrivateQuery(
//                        "/future/trade/v1/order/cancel",
//                        Method.POST,
//                        query: null,
//                        body: body
//                    );

//                    XTFuturesResponseRest<string> stateResponse = JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(response.Content);

//                    if (response.StatusCode == HttpStatusCode.OK)
//                    {
//                        if (stateResponse.returnCode.Equals("0") && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
//                        {
//                            SendLogMessage($"Successfully canceled the order, order Id: {stateResponse.result}", LogMessageType.Trade);
//                            return true;
//                        }
//                        else
//                        {
//                            GetOrderStatus(order);
//                            SendLogMessage($"CancelOrder error, Code: {stateResponse.returnCode}\n"
//                                + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
//                        }
//                    }
//                    else
//                    {
//                        GetOrderStatus(order);
//                        SendLogMessage($"CancelOrder>  State Code: {response.StatusCode}", LogMessageType.Error);

//                        if (stateResponse != null && stateResponse.returnCode != null)
//                        {
//                            SendLogMessage($"CancelOrder error, Code: {stateResponse.returnCode}\n"
//                                + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
//                        }
//                    }
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("CancelOrder error: " + exception.ToString(), LogMessageType.Error);
//                }

//                return false;
//            }

//            private void CreateQueryPositions(bool needUpd)
//            {
//                _rateGatePosition.WaitToProceed();

//                try
//                {
//                    IRestResponse response = CreatePrivateQuery("/future/user/v1/position", Method.GET);

//                    if (response == null || response.StatusCode != HttpStatusCode.OK)
//                    {
//                        SendLogMessage($"CreateQueryPositions:  content empty {response.Content}", LogMessageType.Error);
//                        return;
//                    }

//                    XTFuturesResponseRest<List<XTFuturesPosition>> state =
//                        JsonConvert.DeserializeObject<XTFuturesResponseRest<List<XTFuturesPosition>>>(response.Content);

//                    if (state == null || state.result == null || state.result.Count == 0)
//                    {
//                        return;
//                    }

//                    var portfolio = _portfolios[0];

//                    HashSet<string> aliveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

//                    for (int i = 0; i < state.result.Count; i++)
//                    {
//                        var pos = state.result[i];

//                        if (pos == null || string.IsNullOrWhiteSpace(pos.symbol) || string.IsNullOrWhiteSpace(pos.positionSide))
//                        {
//                            continue;
//                        }

//                        string sym = pos.symbol;
//                        string side = pos.positionSide.ToUpperInvariant();

//                        if (side != "LONG" && side != "SHORT")
//                        {
//                            continue;
//                        }

//                        string code = sym + "_" + side;
//                        aliveKeys.Add(code);

//                        var sizeAbs = Math.Abs(pos.positionSize.ToDecimal());
//                        var size = pos.positionSide.Equals("SHORT", StringComparison.OrdinalIgnoreCase)
//                            ? -sizeAbs
//                            : sizeAbs;

//                        PositionOnBoard position = new PositionOnBoard();

//                        position.PortfolioName = portfolio.Number;
//                        position.SecurityNameCode = code;
//                        position.ValueCurrent = size;
//                        position.UnrealizedPnl = pos.floatingPL.ToDecimal();
//                        position.ValueBegin = needUpd ? size : 0m;

//                        portfolio.SetNewPosition(position);
//                    }

//                    var existing = portfolio.GetPositionOnBoard();

//                    for (int i = 0; i < existing.Count; i++)
//                    {
//                        var p = existing[i];
//                        if (p == null || string.IsNullOrWhiteSpace(p.SecurityNameCode))
//                        {
//                            continue;
//                        }

//                        bool isSidePos =
//                            p.SecurityNameCode.EndsWith("_LONG", StringComparison.OrdinalIgnoreCase) ||
//                            p.SecurityNameCode.EndsWith("_SHORT", StringComparison.OrdinalIgnoreCase);

//                        if (isSidePos && !aliveKeys.Contains(p.SecurityNameCode))
//                        {
//                            if (p.ValueCurrent != 0m || p.UnrealizedPnl != 0m)
//                            {
//                                p.ValueCurrent = 0m;
//                                p.UnrealizedPnl = 0m;
//                                portfolio.SetNewPosition(p);
//                                SendLogMessage($"[Positions] cleared missing on exchange: {p.SecurityNameCode}", LogMessageType.System);
//                            }
//                        }
//                    }

//                    PortfolioEvent?.Invoke(_portfolios);
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("CreateQueryPositions error: " + exception, LogMessageType.Error);
//                }
//            }

//            private void UpdatePortfolioRest(List<XTFuturesBalance> list, bool isUpdateValueBegin)
//            {
//                try
//                {
//                    if (_portfolios == null || _portfolios.Count == 0 || list == null || list.Count == 0)
//                    {
//                        return;
//                    }

//                    Portfolio portfolio = _portfolios[0];

//                    XTFuturesBalance coin = list[0];

//                    decimal wallet = coin.walletBalance.ToDecimal();
//                    decimal isoMargin = coin.isolatedMargin.ToDecimal();
//                    decimal crossMargin = coin.crossedMargin.ToDecimal();
//                    decimal orderFrozen = coin.openOrderMarginFrozen.ToDecimal();
//                    decimal locked = isoMargin + crossMargin + orderFrozen;
//                    decimal available = wallet - locked;

//                    if (isUpdateValueBegin)
//                    {
//                        portfolio.ValueBegin = 1;
//                    }

//                    portfolio.ValueCurrent = 1;

//                    string coinCode = (coin.coin.ToUpper() ?? "USDT");

//                    PositionOnBoard moneyPos = new PositionOnBoard();

//                    moneyPos.PortfolioName = portfolio.Number;
//                    moneyPos.SecurityNameCode = coinCode;
//                    moneyPos.ValueCurrent = Math.Round(available, 4);
//                    moneyPos.ValueBlocked = Math.Round(locked, 4);
//                    moneyPos.ValueBegin = isUpdateValueBegin ? Math.Round(available, 4) : 0m;

//                    portfolio.SetNewPosition(moneyPos);

//                    PortfolioEvent?.Invoke(_portfolios);
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("UpdatePortfolioRest error: " + exception.ToString(), LogMessageType.Error);
//                }
//            }

//            public void GetAllActivOrders()
//            {
//                try
//                {
//                    List<Order> orders = FetchAllActiveOrders();

//                    if (orders == null || orders.Count == 0)
//                    {
//                        SendLogMessage("GetActiveOrders> no active orders", LogMessageType.System);
//                        return;
//                    }

//                    for (int i = 0; i < orders.Count; i++)
//                    {
//                        Order order = orders[i];
//                        MyOrderEvent?.Invoke(order);
//                    }
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("GetActiveOrders error: " + exception, LogMessageType.Error);
//                }
//            }

//            private List<Order> FetchAllActiveOrders()
//            {
//                _rateGateOpenOrder.WaitToProceed();

//                try
//                {
//                    IRestResponse responseMessage = CreatePrivateQuery("/future/trade/v1/order/list-open-order", Method.POST);

//                    if (responseMessage == null || responseMessage.StatusCode != HttpStatusCode.OK)
//                    {
//                        SendLogMessage($"FetchAllActiveOrders> bad HTTP status: {responseMessage?.StatusCode}", LogMessageType.Error);
//                        return new List<Order>();
//                    }

//                    XTFuturesResponseRest<List<XTFuturesOrderItem>> stateResponse =
//                        JsonConvert.DeserializeObject<XTFuturesResponseRest<List<XTFuturesOrderItem>>>(responseMessage.Content);

//                    if (stateResponse == null)
//                    {
//                        return new List<Order>();
//                    }

//                    if (stateResponse.returnCode == "0" && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
//                    {
//                        List<XTFuturesOrderItem> activeOrders = stateResponse.result ?? new List<XTFuturesOrderItem>();

//                        List<Order> listOrders = new List<Order>(activeOrders.Count);

//                        for (int i = 0; i < activeOrders.Count; i++)
//                        {
//                            XTFuturesOrderItem item = activeOrders[i];

//                            if (item == null)
//                            {
//                                continue;
//                            }

//                            int.TryParse(item.clientOrderId, out int numberUser);

//                            Order activeOrder = new Order();

//                            activeOrder.NumberUser = numberUser;
//                            activeOrder.NumberMarket = item.orderId;
//                            activeOrder.SecurityNameCode = item.symbol;
//                            activeOrder.ServerType = ServerType.XTFutures;
//                            activeOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createdTime));
//                            activeOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.updatedTime));
//                            activeOrder.Volume = item.origQty.ToDecimal();
//                            activeOrder.Price = item.price.ToDecimal();
//                            activeOrder.Side = item.orderSide.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;
//                            activeOrder.State = GetOrderState(item.state);
//                            activeOrder.SecurityClassCode = GetNameClass(item.symbol);
//                            activeOrder.TypeOrder = item.orderType.Equals("LIMIT", StringComparison.OrdinalIgnoreCase)
//                                            ? OrderPriceType.Limit : OrderPriceType.Market;
//                            activeOrder.PortfolioNumber = _portfolioName;

//                            listOrders.Add(activeOrder);

//                            MyOrderEvent?.Invoke(activeOrder);
//                        }

//                        return listOrders;
//                    }

//                    return new List<Order>();
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("FetchAllActiveOrders error: " + exception, LogMessageType.Error);
//                    return new List<Order>();
//                }
//            }

//            private List<Order> GetOrderHistory()
//            {
//                _rateGateOpenOrder.WaitToProceed();

//                try
//                {
//                    string query = "limit=100";
//                    IRestResponse responseMessage =
//                        CreatePrivateQuery("/future/trade/v1/order/list-history", Method.GET, query);

//                    XTFuturesResponseRest<XTFuturesOrderResult> stateResponse =
//                        JsonConvert.DeserializeObject<XTFuturesResponseRest<XTFuturesOrderResult>>(responseMessage.Content);

//                    if (stateResponse == null)
//                    {
//                        return null;
//                    }

//                    if (stateResponse.returnCode.Equals("0") &&
//                        stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
//                    {
//                        List<XTFuturesOrderItem> result = stateResponse.result.items;
//                        if (result == null) { return new List<Order>(); }

//                        List<Order> orders = new List<Order>();

//                        for (int i = 0; i < result.Count; i++)
//                        {
//                            XTFuturesOrderItem item = result[i];
//                            Order historyOrder = new Order();

//                            historyOrder.NumberMarket = item.orderId;

//                            int.TryParse(item.clientOrderId, out int numberUser);
//                            historyOrder.NumberUser = numberUser;

//                            historyOrder.SecurityNameCode = item.symbol;
//                            historyOrder.SecurityClassCode = GetNameClass(item.symbol);
//                            historyOrder.Side = item.orderSide.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;
//                            historyOrder.State = GetOrderState(item.state);
//                            historyOrder.TypeOrder = item.orderType.Equals("LIMIT", StringComparison.OrdinalIgnoreCase) ? OrderPriceType.Limit : OrderPriceType.Market;
//                            historyOrder.Volume = item.origQty.ToDecimal();
//                            historyOrder.Price = item.price.ToDecimal();
//                            historyOrder.PortfolioNumber = _portfolioName;

//                            historyOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createdTime));
//                            historyOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(
//                                Convert.ToInt64(string.IsNullOrEmpty(item.updatedTime) ? item.createdTime : item.updatedTime));

//                            if (historyOrder.State == OrderStateType.Done)
//                                historyOrder.TimeDone = historyOrder.TimeCallBack;
//                            else if (historyOrder.State == OrderStateType.Cancel)
//                                historyOrder.TimeCancel = historyOrder.TimeCallBack;

//                            orders.Add(historyOrder);
//                        }

//                        return orders;
//                    }

//                    return new List<Order>();
//                }
//                catch (Exception)
//                {
//                    return null;
//                }
//            }


//            public List<Order> GetAllOpenOrders()
//            {
//                try
//                {
//                    IRestResponse responseMessage =
//                        CreatePrivateQuery("/future/trade/v1/order/list-open-order", Method.POST);

//                    XTFuturesResponseRest<List<XTFuturesOrderItem>> stateResponse =
//                        JsonConvert.DeserializeObject<XTFuturesResponseRest<List<XTFuturesOrderItem>>>(responseMessage.Content);

//                    if (stateResponse == null)
//                    {
//                        SendLogMessage("GetAllOpenOrders: deserialization returned null", LogMessageType.Error);
//                        return null;
//                    }

//                    if (stateResponse.returnCode == "0" &&
//                        stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
//                    {
//                        List<XTFuturesOrderItem> src = stateResponse.result ?? new List<XTFuturesOrderItem>();
//                        List<Order> listOrders = new List<Order>(src.Count);

//                        for (int i = 0; i < src.Count; i++)
//                        {
//                            XTFuturesOrderItem item = src[i];
//                            if (item == null) { continue; }

//                            int.TryParse(item.clientOrderId, out int numberUser);

//                            Order activeOrder = new Order();
//                            activeOrder.NumberUser = numberUser;
//                            activeOrder.NumberMarket = item.orderId;
//                            activeOrder.SecurityNameCode = item.symbol;
//                            activeOrder.ServerType = ServerType.XTFutures;
//                            activeOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createdTime));
//                            activeOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(
//                                Convert.ToInt64(string.IsNullOrEmpty(item.updatedTime) ? item.createdTime : item.updatedTime));
//                            activeOrder.Volume = item.origQty.ToDecimal();
//                            activeOrder.Price = item.price.ToDecimal();
//                            activeOrder.Side = item.orderSide.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;
//                            activeOrder.State = GetOrderState(item.state);
//                            activeOrder.SecurityClassCode = GetNameClass(item.symbol);
//                            activeOrder.TypeOrder = item.orderType.Equals("LIMIT", StringComparison.OrdinalIgnoreCase) ? OrderPriceType.Limit : OrderPriceType.Market;
//                            activeOrder.PortfolioNumber = _portfolioName;

//                            listOrders.Add(activeOrder);
//                            MyOrderEvent?.Invoke(activeOrder);
//                        }
//                        return listOrders;
//                    }
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("GetAllOpenOrders error: " + exception, LogMessageType.Error);
//                    return null;
//                }

//                return new List<Order>();
//            }

//            public void SaveOrdersToFile(List<Order> orders, string filePath)
//            {
//                try
//                {
//                    using (StreamWriter writer = new StreamWriter(filePath, false))
//                    {
//                        for (int i = 0; i < orders.Count; i++)
//                        {
//                            Order order = orders[i];

//                            if (order == null)
//                            {
//                                continue;
//                            }

//                            string line =
//                                "№: " + i +
//                                " | Market: " + order.NumberMarket +
//                                " | User: " + order.NumberUser +
//                                " | Security: " + order.SecurityNameCode +
//                                " | State: " + order.State +
//                                " | Create: " + order.TimeCreate.ToString("yyyy-MM-dd HH:mm:ss") +
//                                " | Callback: " + order.TimeCallBack.ToString("yyyy-MM-dd HH:mm:ss");

//                            writer.WriteLine(line);
//                        }
//                    }

//                    SendLogMessage("Orders list saved to: " + filePath, LogMessageType.System);
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("Error saving orders to file: " + exception.ToString(), LogMessageType.Error);
//                }
//            }
//            public List<Order> GetActiveOrders(int startIndex, int count)
//            {
//                if (startIndex < 0)
//                {
//                    startIndex = 0;
//                }

//                if (count <= 0)
//                {
//                    count = 100;
//                }

//                List<Order> orders = GetAllOpenOrders();

//                SaveOrdersToFile(orders, "I:\\XT_Debug\\active_orders.txt");

//                if (orders == null)
//                {
//                    orders = new List<Order>();
//                }

//                //orders.RemoveAll(o => o == null);

//                //orders.Sort((firstOrder, secondOrder) =>
//                //{
//                //    DateTime firstOrderTime = firstOrder.TimeCallBack == DateTime.MinValue ? firstOrder.TimeCreate : firstOrder.TimeCallBack;
//                //    DateTime secondOrderTime = secondOrder.TimeCallBack == DateTime.MinValue ? secondOrder.TimeCreate : secondOrder.TimeCallBack;
//                //    return secondOrderTime.CompareTo(firstOrderTime);
//                //});

//                if (startIndex >= orders.Count)
//                {
//                    return new List<Order>();
//                }

//                int take = Math.Min(count, orders.Count - startIndex);

//                return orders.GetRange(startIndex, take);
//            }

//            public List<Order> GetHistoricalOrders(int startIndex, int count)
//            {
//                if (startIndex < 0) startIndex = 0;
//                if (count <= 0) count = 100;

//                List<Order> result = GetOrderHistory();

//                SaveOrdersToFile(result, "I:\\XT_Debug\\history_orders.txt"); // осталось как у тебя

//                if (result == null || startIndex >= result.Count)
//                {
//                    return new List<Order>();
//                }

//                int take = Math.Min(count, result.Count - startIndex);
//                for (int i = startIndex; i < startIndex + take; i++)
//                {
//                    Order o = result[i];

//                    // гарантия валидных времён — иначе тест отфильтрует
//                    if (o.TimeCallBack == DateTime.MinValue)
//                        o.TimeCallBack = o.TimeCreate;

//                    if (o.State == OrderStateType.Done && o.TimeDone == DateTime.MinValue)
//                        o.TimeDone = o.TimeCallBack;
//                    else if (o.State == OrderStateType.Cancel && o.TimeCancel == DateTime.MinValue)
//                        o.TimeCancel = o.TimeCallBack;

//                    if (o.State == OrderStateType.Done || o.State == OrderStateType.Cancel)
//                        MyOrderEvent?.Invoke(o);
//                }

//                return result.GetRange(startIndex, take);
//            }


//            public OrderStateType GetOrderStatus(Order order)
//            {
//                _rateGateOpenOrder.WaitToProceed();

//                try
//                {
//                    if (order == null)
//                    {
//                        SendLogMessage("GetOrderStatus > Order is null", LogMessageType.Error);
//                        return OrderStateType.None;
//                    }

//                    Order orderOnMarket = null;

//                    List<Order> ordersActive = GetActiveOrders(0, 100);
//                    List<Order> ordersHistory = GetHistoricalOrders(0, 100);

//                    if (!string.IsNullOrEmpty(order.NumberMarket))
//                    {
//                        if (ordersActive != null)
//                        {
//                            for (int i = 0; i < ordersActive.Count; i++)
//                            {
//                                if (ordersActive[i].NumberMarket == order.NumberMarket)
//                                {
//                                    orderOnMarket = ordersActive[i];
//                                    break;
//                                }
//                            }
//                        }
//                        if (orderOnMarket == null && ordersHistory != null)
//                        {
//                            for (int i = 0; i < ordersHistory.Count; i++)
//                            {
//                                if (ordersHistory[i].NumberMarket == order.NumberMarket)
//                                {
//                                    orderOnMarket = ordersHistory[i];
//                                    break;
//                                }
//                            }
//                        }
//                    }

//                    if (orderOnMarket == null && order.NumberUser != 0)
//                    {
//                        if (ordersActive != null)
//                        {
//                            for (int i = 0; i < ordersActive.Count; i++)
//                            {
//                                if (ordersActive[i].NumberUser == order.NumberUser)
//                                {
//                                    orderOnMarket = ordersActive[i];
//                                    break;
//                                }
//                            }
//                        }
//                        if (orderOnMarket == null && ordersHistory != null)
//                        {
//                            for (int i = 0; i < ordersHistory.Count; i++)
//                            {
//                                if (ordersHistory[i].NumberUser == order.NumberUser)
//                                {
//                                    orderOnMarket = ordersHistory[i];
//                                    break;
//                                }
//                            }
//                        }
//                    }

//                    if (orderOnMarket == null)
//                    {
//                        return OrderStateType.None;
//                    }

//                    // публикуем снапшот статуса — ключ к прохождению О10
//                    MyOrderEvent?.Invoke(orderOnMarket);

//                    // добор трейдов при Done/Partial
//                    if ((orderOnMarket.State == OrderStateType.Done ||
//                         orderOnMarket.State == OrderStateType.Partial) &&
//                        !string.IsNullOrEmpty(orderOnMarket.NumberMarket))
//                    {
//                        CreateQueryMyTrade(orderOnMarket.NumberMarket);
//                    }

//                    return orderOnMarket.State;
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage($"GetOrderStatus> exception: {exception.Message}", LogMessageType.Error);
//                    return OrderStateType.None;
//                }
//            }


//            #endregion

//            #region 12 Queries

//            private List<string> _subscribedSecurities = new List<string>();

//            private readonly RateGate _rateGateGetToken = new RateGate(1, TimeSpan.FromMilliseconds(10000));

//            private string GetListenKey()
//            {
//                _rateGateGetToken.WaitToProceed();

//                string listenKey = "";

//                try
//                {
//                    IRestResponse responseMessage = CreatePrivateQuery("/future/user/v1/user/listen-key", Method.GET);

//                    XTFuturesResponseRest<string> stateResponse = JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(responseMessage.Content);

//                    if (stateResponse == null)
//                    {
//                        return null;
//                    }

//                    if (responseMessage.StatusCode == HttpStatusCode.OK && stateResponse != null)
//                    {
//                        if (stateResponse.returnCode.Equals("0") && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
//                        {
//                            SendLogMessage($"ListenKey successfully received.", LogMessageType.Connect);
//                            listenKey = stateResponse?.result;
//                        }
//                        else
//                        {
//                            SendLogMessage($"GetListenKey error, Code: {stateResponse.returnCode}\n"
//                                           + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
//                        }
//                    }
//                    else
//                    {
//                        SendLogMessage($"Receiving Token>  State Code: {responseMessage.StatusCode}", LogMessageType.Error);

//                        if (stateResponse != null && stateResponse.returnCode != null)
//                        {
//                            SendLogMessage($"GetListenKey error, Code: {stateResponse.returnCode}\n"
//                                           + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
//                        }
//                    }
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage(exception.ToString(), LogMessageType.Error);
//                }

//                return listenKey;
//            }

//            private readonly RateGate _rateGateGetPortfolio = new RateGate(1, TimeSpan.FromMilliseconds(333));

//            private void CreateQueryPortfolio(bool isUpdateValueBegin)
//            {
//                _rateGateGetPortfolio.WaitToProceed();

//                try
//                {
//                    IRestResponse response = CreatePrivateQuery("/future/user/v1/compat/balance/list", Method.GET);

//                    if (response == null)
//                    {
//                        return;
//                    }

//                    if (response.StatusCode != HttpStatusCode.OK)
//                    {
//                        return;
//                    }

//                    XTFuturesResponseRestNew<List<XTFuturesBalance>> stateResponse =
//                        JsonConvert.DeserializeObject<XTFuturesResponseRestNew<List<XTFuturesBalance>>>(response.Content);

//                    if (stateResponse == null)
//                    {
//                        return;
//                    }

//                    if (stateResponse.rc == "0" &&
//                        string.Equals(stateResponse.mc, "SUCCESS", StringComparison.OrdinalIgnoreCase))
//                    {
//                        if (_portfolios == null || _portfolios.Count == 0)
//                        {
//                            GetNewPortfolio();
//                        }

//                        UpdatePortfolioRest(stateResponse.result, isUpdateValueBegin);
//                    }
//                    else
//                    {
//                        SendLogMessage(
//                            $"CreateQueryPortfolio error, Code: {stateResponse.rc} Message code: {stateResponse.mc}",
//                            LogMessageType.Error);
//                    }
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("CreateQueryPortfolio error: " + exception, LogMessageType.Error);
//                }
//            }

//            private readonly RateGate _rateGateGetMyTradeState = new RateGate(1, TimeSpan.FromMilliseconds(100));

//            private void CreateQueryMyTrade(string orderId)
//            {
//                _rateGateGetMyTradeState.WaitToProceed();

//                List<MyTrade> list = new List<MyTrade>();

//                try
//                {
//                    string orderIdStr = orderId.ToString(CultureInfo.InvariantCulture);

//                    IRestResponse response = CreatePrivateQuery("/future/trade/v1/order/trade-list", Method.GET, query: "orderId=" + orderIdStr);

//                    XTFuturesResponseRest<XTFuturesTradeHistoryResult> stateResponse =
//                    JsonConvert.DeserializeObject<XTFuturesResponseRest<XTFuturesTradeHistoryResult>>(response.Content);

//                    if (stateResponse == null)
//                    {
//                        return;
//                    }

//                    if (response.StatusCode == HttpStatusCode.OK)
//                    {
//                        if (stateResponse.returnCode.Equals("0") && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
//                        {
//                            for (int i = 0; i < stateResponse.result.items.Count; i++)
//                            {
//                                XTFuturesTradeHistory data = stateResponse.result.items[i];

//                                if (data == null)
//                                {
//                                    continue;
//                                }

//                                MyTrade trade = new MyTrade();

//                                trade.NumberOrderParent = data.orderId;
//                                trade.NumberTrade = data.execId;
//                                trade.SecurityNameCode = data.symbol;
//                                trade.Price = data.price.ToDecimal();
//                                trade.Volume = data.quantity.ToDecimal();
//                                trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(data.timestamp));
//                                trade.Side = string.Equals(data.orderSide, "BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;

//                                MyTradeEvent?.Invoke(trade);
//                            }
//                        }
//                        else
//                        {
//                            SendLogMessage($"CreateQueryMyTrade error, Code: {stateResponse.returnCode}\n"
//                                + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
//                        }
//                    }
//                    else
//                    {
//                        SendLogMessage($"CreateQueryMyTrade>  State Code: {stateResponse.returnCode}", LogMessageType.Error);
//                    }
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("CreateQueryMyTrade error: " + exception.ToString(), LogMessageType.Error);
//                }
//            }

//            IRestResponse CreatePublicQuery(string path, Method method, string parameters = "")
//            {
//                RestClient client = new RestClient(_baseUrl);

//                if (_myProxy != null)
//                {
//                    client.Proxy = _myProxy;
//                }

//                if (method == Method.GET && !string.IsNullOrEmpty(parameters))
//                {
//                    path += "?" + parameters;
//                }

//                RestRequest request = new RestRequest(path, method);
//                request.AddHeader("Accept", "application/json");

//                IRestResponse response = client.Execute(request);
//                return response;
//            }

//            private string HmacSHA256(string key, string data)
//            {
//                using (var h = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
//                {
//                    var hash = h.ComputeHash(Encoding.UTF8.GetBytes(data));
//                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
//                }
//            }

//            private string _recvWindow = "5000";

//            private IRestResponse CreatePrivateQuery(string path, Method method, string query = null, object body = null)
//            {
//                try
//                {
//                    string resource = string.IsNullOrEmpty(query) ? path : $"{path}?{query}";

//                    string bodyString = "";
//                    var jsonSettings = new JsonSerializerSettings
//                    {
//                        NullValueHandling = NullValueHandling.Ignore,
//                        Culture = CultureInfo.InvariantCulture
//                    };
//                    if (method == Method.POST && body != null)
//                    {
//                        bodyString = body is string s ? s : JsonConvert.SerializeObject(body, Formatting.None);
//                    }

//                    string ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
//                    string X = $"validate-appkey={_publicKey}&validate-timestamp={ts}";

//                    StringBuilder yb = new StringBuilder().Append('#').Append(path);
//                    if (!string.IsNullOrEmpty(query)) yb.Append('#').Append(query);
//                    if (method == Method.POST && bodyString.Length > 0)
//                        yb.Append('#').Append(bodyString);

//                    string signInput = X + yb;
//                    string signature = HmacSHA256(_secretKey, signInput);

//                    RestClient client = new RestClient(_baseUrl);
//                    RestRequest request = new RestRequest(resource, method);

//                    if (_myProxy != null)
//                    {
//                        client.Proxy = _myProxy;
//                    }

//                    request.AddHeader("Accept", "application/json");
//                    request.AddHeader("validate-appkey", _publicKey);
//                    request.AddHeader("validate-timestamp", ts);
//                    request.AddHeader("validate-signature", signature);
//                    request.AddHeader("validate-algorithms", "HmacSHA256");
//                    if (!string.IsNullOrEmpty(_recvWindow))
//                        request.AddHeader("validate-recvwindow", _recvWindow);

//                    if (method == Method.POST && bodyString.Length > 0)
//                    {
//                        request.AddHeader("Content-Type", "application/json");
//                        request.AddParameter("application/json", bodyString, ParameterType.RequestBody);
//                    }

//                    return client.Execute(request);
//                }
//                catch (Exception exception)
//                {
//                    SendLogMessage("SendPrivate error: " + exception, LogMessageType.Error);
//                    return null;
//                }
//            }

//            private void CreateOrderFail(Order order)
//            {
//                order.State = OrderStateType.Fail;
//                MyOrderEvent?.Invoke(order);
//            }

//            public event Action<string, LogMessageType> LogMessageEvent;

//            public event Action<Funding> FundingUpdateEvent;

//            public event Action<SecurityVolumes> Volume24hUpdateEvent;

//            public event Action<Order> MyOrderEvent;
//            public bool SubscribeNews()
//            {
//                return false;
//            }

//            #endregion

//            #region 13 Log

//            private void SendLogMessage(string message, LogMessageType messageType)
//            {
//                LogMessageEvent?.Invoke(message, messageType);
//            }

//            #endregion
//        }
//    }
//}

