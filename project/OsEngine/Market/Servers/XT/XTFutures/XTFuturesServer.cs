
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
using System.IO;
using ErrorEventArgs = OsEngine.Entity.WebSocketOsEngine.ErrorEventArgs;

using System.Linq;







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
                threadForPublicMessages.Name = "PublicMessageReaderXTFutures";
                threadForPublicMessages.Start();

                Thread threadForTradesMessages = new Thread(PublicTradesMessageReader);
                threadForTradesMessages.IsBackground = true;
                threadForTradesMessages.Name = "PublicTradesMessageReaderXTFutures";
                threadForTradesMessages.Start();

                Thread threadForPrivateMessages = new Thread(PrivateMessageReader);
                threadForPrivateMessages.IsBackground = true;
                threadForPrivateMessages.Name = "PrivateMessageReaderXTFutures";
                threadForPrivateMessages.Start();

                Thread threadForGetPortfolios = new Thread(UpdatePortfolios);
                threadForGetPortfolios.IsBackground = true;
                threadForGetPortfolios.Name = "UpdatePortfoliosXTFutures";
                threadForGetPortfolios.Start();
            }

            public DateTime ServerTime { get; set; }

            private WebProxy _myProxy;

            public void Connect(WebProxy proxy)
            {
                LoadOrderTrackers();
                _myProxy = proxy;
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
                    IRestResponse responseMessage = CreatePublicQuery("/future/public/client", Method.GET);

                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        try
                        {
                            _listenKey = GetListenKey();

                            if (string.IsNullOrEmpty(_listenKey))
                            {
                                SendLogMessage(" Check the Public and Private Key!", LogMessageType.Error);
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
                            SendLogMessage("Connection cannot be open. Error", LogMessageType.Error);
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent?.Invoke();
                        }
                    }
                    else
                    {
                        SendLogMessage("Connection cannot be open. Error request", LogMessageType.Error);
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
                    //  UnsubscribeFromAllWebSockets();
                    _subscribedSecurities.Clear();
                    DeleteWebsocketConnection();
                    _marketDepths.Clear();
                }
                catch (Exception exception)
                {
                    SendLogMessage(" Dispose error" + exception.ToString(), LogMessageType.Error);
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
                    DisconnectEvent?.Invoke();
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

                        //_webSocketPublicMarketDepths.SendAsync($"{{\"method\": \"unsubscribe\", \"params\": [\"depth_update@{securityName}\",\"depth@{securityName},{20}\"], \"id\": \"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");
                        //_webSocketPublicTrades.SendAsync($"{{\"method\": \"unsubscribe\", \"params\": [\"trades@{securityName}\"], \"id\": \"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");
                        _webSocketPublicMarketDepths.SendAsync($"{{\"method\": \"UNSUBSCRIBE\", \"params\": [\"depth_update@{securityName}\",\"depth@{securityName},{20}\"], \"id\": \"1252\"}}");
                        _webSocketPublicTrades.SendAsync($"{{\"method\": \"UNSUBSCRIBE\", \"params\": [\"trades@{securityName}\"], \"id\": \"1253\"}}");
                    }

                    // _webSocketPrivate.SendAsync($"{{\"method\": \"unsubscribe\", \"params\": [\"balance\",\"order\",\"position\",\"trade\"], \"id\": \"{TimeManager.GetUnixTimeStampMilliseconds()}\"}}");\"id\":\"sub-1\"
                    // _webSocketPrivate.SendAsync($"{{\"method\": \"UNSUBSCRIBE\", \"params\": [\"balance\",\"order\",\"position\",\"trade\"], \"id\": \"1254\"}}");
                    _webSocketPrivate.SendAsync($"{{\"method\":\"UNSUBSCRIBE\",\"params\":[\"order@{_listenKey}\",\"trade@{_listenKey}\",\"position@{_listenKey}\",\"balance@{_listenKey}\",\"notify@{_listenKey}\"],\"id\":\"{1254}\"}}");
                }
                catch
                {
                    // ignore
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

            private readonly string _baseUrl = "https://fapi.xt.com";

            #endregion

            #region 3 Securities

            public event Action<List<Security>> SecurityEvent;

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
                        XTFuturesResponseRest<XTFuturesSymbolListResult> securityList =
                            JsonConvert.DeserializeObject<XTFuturesResponseRest<XTFuturesSymbolListResult>>(response.Content);

                        if (securityList == null)
                        {
                            SendLogMessage("XTFutures GetSecurities> Deserialization resulted in null", LogMessageType.Error);
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
                                    XTFuturesSymbol symbols = securityList.result.symbols[i];

                                    Security newSecurity = new Security();

                                    newSecurity.Exchange = ServerType.XTFutures.ToString();
                                    newSecurity.Name = symbols.symbol;
                                    newSecurity.NameFull = symbols.symbol;
                                    newSecurity.NameClass = GetNameClass(symbols.symbol);
                                    newSecurity.NameId = symbols.symbolGroupId;
                                    newSecurity.SecurityType = SecurityType.Futures;
                                    newSecurity.Lot = 1;
                                    newSecurity.PriceLimitLow = symbols.minPrice.ToDecimal();
                                    newSecurity.PriceLimitHigh = symbols.maxPrice.ToDecimal();

                                    if (symbols.tradeSwitch == "false" ||
                                        symbols.contractType != "PERPETUAL")
                                    {
                                        continue;
                                    }

                                    newSecurity.State = SecurityStateType.Activ;
                                    newSecurity.PriceStep = (symbols.minStepPrice).ToDecimal();
                                    newSecurity.Decimals = Convert.ToInt32(symbols.pricePrecision);
                                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                                    newSecurity.DecimalsVolume = Convert.ToInt32(symbols.quoteCoinDisplayPrecision);
                                    newSecurity.MinTradeAmount = symbols.minNotional.ToDecimal();
                                    newSecurity.MinTradeAmountType = MinTradeAmountType.C_Currency;
                                    newSecurity.VolumeStep = newSecurity.DecimalsVolume.GetValueByDecimals();

                                    securities.Add(newSecurity);
                                }

                                SecurityEvent?.Invoke(securities);
                            }
                            else
                            {
                                SendLogMessage($" GetSecurities return code: {securityList.returnCode}\n"
                                               + $"Message Code: {securityList.msgInfo}", LogMessageType.Error);
                            }
                        }
                        else
                        {
                            if (securityList != null && securityList.returnCode != null)
                            {
                                SendLogMessage($" Return Code: {securityList.returnCode}\n"
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

            #endregion

            #region 4 Portfolios

            private string _portfolioName = "XTFuturesPortfolio";

            private List<Portfolio> _portfolios;

            public event Action<List<Portfolio>> PortfolioEvent;

            private readonly RateGate _rateGatePortfolio = new RateGate(1, TimeSpan.FromMilliseconds(333));

            public void GetPortfolios()
            {
                if (_portfolios == null)
                {
                    GetNewPortfolio();
                }

                CreateQueryPortfolio(true);
                CreateQueryPositions(true);
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

                PortfolioEvent?.Invoke(_portfolios);
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
                        CreateQueryPositions(false);
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                    }
                }
            }

            private void CreateQueryPortfolio(bool isUpdateValueBegin)
            {
                _rateGatePortfolio.WaitToProceed();

                try
                {
                    IRestResponse response = CreatePrivateQuery("/future/user/v1/compat/balance/list", Method.GET);

                    if (response == null)
                    {
                        return;
                    }

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        return;
                    }

                    XTFuturesResponseRestNew<List<XTFuturesBalance>> stateResponse =
                        JsonConvert.DeserializeObject<XTFuturesResponseRestNew<List<XTFuturesBalance>>>(response.Content);

                    if (stateResponse == null)
                    {
                        return;
                    }

                    if (stateResponse.rc == "0" &&
                        string.Equals(stateResponse.mc, "SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {
                        if (_portfolios == null || _portfolios.Count == 0)
                        {
                            GetNewPortfolio();
                        }

                        UpdatePortfolioRest(stateResponse.result, isUpdateValueBegin);
                    }
                    else
                    {
                        SendLogMessage(
                            $" CreateQueryPortfolio error, Code: {stateResponse.rc} Message code: {stateResponse.mc}",
                            LogMessageType.Error);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(" CreateQueryPortfolio error: " + exception, LogMessageType.Error);
                }
            }

            private void UpdatePortfolioRest(List<XTFuturesBalance> list, bool isUpdateValueBegin)
            {
                try
                {
                    if (_portfolios == null || _portfolios.Count == 0 || list == null || list.Count == 0)
                    {
                        return;
                    }

                    Portfolio portfolio = _portfolios[0];

                    XTFuturesBalance coin = list[0];

                    decimal wallet = coin.walletBalance.ToDecimal();
                    decimal isoMargin = coin.isolatedMargin.ToDecimal();
                    decimal crossMargin = coin.crossedMargin.ToDecimal();
                    decimal orderFrozen = coin.openOrderMarginFrozen.ToDecimal();
                    decimal locked = isoMargin + crossMargin + orderFrozen;
                    decimal available = wallet - locked;

                    if (isUpdateValueBegin)
                    {
                        portfolio.ValueBegin = 1;
                    }

                    portfolio.ValueCurrent = 1;

                    string coinCode = (coin.coin.ToUpper() ?? "USDT");

                    PositionOnBoard moneyPos = new PositionOnBoard();

                    moneyPos.PortfolioName = portfolio.Number;
                    moneyPos.SecurityNameCode = coinCode;
                    moneyPos.ValueCurrent = Math.Round(available, 4);
                    moneyPos.ValueBlocked = Math.Round(locked, 4);
                    moneyPos.ValueBegin = isUpdateValueBegin ? Math.Round(available, 4) : 0m;

                    portfolio.SetNewPosition(moneyPos);

                    PortfolioEvent?.Invoke(_portfolios);
                }
                catch (Exception exception)
                {
                    SendLogMessage(" UpdatePortfolioRest error: " + exception.ToString(), LogMessageType.Error);
                }
            }

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

            private List<Candle> GetCandleHistory(string nameSec, TimeSpan tf, bool isOsData, int CountToLoad, DateTime startTime, DateTime endTime)
            {
                int needToLoadCandles = CountToLoad;

                List<Candle> candles = new List<Candle>();

                DateTime currentEndTime = endTime;
                DateTime currentStartTime = startTime;

                do
                {
                    int limit = needToLoadCandles;

                    if (needToLoadCandles > 1000)
                    {
                        limit = 1000;
                    }

                    DateTime queryStartTime = currentEndTime - TimeSpan.FromMinutes(tf.TotalMinutes * limit);

                    if (queryStartTime < currentStartTime)
                    {
                        queryStartTime = currentStartTime;
                    }

                    List<Candle> rangeCandles;

                    rangeCandles = CreateQueryCandles(nameSec, GetStringInterval(tf), queryStartTime, currentEndTime, limit);

                    if (rangeCandles == null)
                    {
                        return null;
                    }

                    rangeCandles.Reverse();

                    candles.InsertRange(0, rangeCandles);

                    if (candles.Count != 0)
                    {
                        currentEndTime = candles[0].TimeStart;
                    }

                    needToLoadCandles -= limit;
                }

                while (needToLoadCandles > 0 && currentEndTime > currentStartTime);

                return FilterCandlesByTimeRange(candles, startTime, endTime);
            }

            public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
            {
                startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
                endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
                actualTime = DateTime.SpecifyKind(actualTime, DateTimeKind.Utc);

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

                return GetCandleHistory(security.Name, timeFrameBuilder.TimeFrameTimeSpan, true, countNeedToLoad, startTime, endTime);
            }

            private int GetCountCandlesFromSliceTime(DateTime startTime, DateTime endTime, TimeSpan tf)
            {
                TimeSpan timeSlice = endTime - startTime;
                double totalMinutes = timeSlice.TotalMinutes;
                double tfMinutes = tf.TotalMinutes;

                return (int)Math.Ceiling(totalMinutes / tfMinutes);
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
                    timeFrameMinutes == 240 ||
                    timeFrameMinutes == 1440)
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

            private List<Candle> FilterCandlesByTimeRange(List<Candle> candles, DateTime startTime, DateTime endTime)
            {
                if (candles == null || candles.Count == 0)
                {
                    return candles;
                }

                List<Candle> filteredCandles = new List<Candle>();

                for (int i = 0; i < candles.Count; i++)
                {
                    Candle candle = candles[i];

                    if (candle.TimeStart >= startTime && candle.TimeStart <= endTime)
                    {
                        filteredCandles.Add(candle);
                    }
                }

                return filteredCandles;
            }

            private readonly RateGate _rateGateCandleHistory = new RateGate(1, TimeSpan.FromMilliseconds(200));

            private List<Candle> CreateQueryCandles(string nameSec, string stringInterval, DateTime timeStart, DateTime timeEnd, int count)
            {
                _rateGateCandleHistory.WaitToProceed();

                try
                {
                    string startTime = TimeManager.GetTimeStampMilliSecondsToDateTime(timeStart).ToString();
                    string endTime = TimeManager.GetTimeStampMilliSecondsToDateTime(timeEnd).ToString();
                    string limit = count.ToString();

                    string param = "symbol=" + nameSec
                                 + "&interval=" + stringInterval
                                 + "&startTime=" + startTime
                                 + "&endTime=" + endTime
                                 + "&limit=" + limit;

                    IRestResponse responseMessage = CreatePublicQuery("/future/market/v1/public/q/kline", Method.GET, param);

                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        if (responseMessage == null)
                        {
                            return null;
                        }

                        XTFuturesResponseRest<List<XTFuturesCandle>> symbols = JsonConvert.DeserializeObject<XTFuturesResponseRest<List<XTFuturesCandle>>>(responseMessage.Content);

                        if (symbols == null)
                        {
                            SendLogMessage(" CreateQueryCandles: Response is null", LogMessageType.Error);
                            return null;
                        }

                        if (symbols.returnCode.Equals("0") && symbols.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                        {
                            List<Candle> candles = new List<Candle>();

                            for (int i = 0; i < symbols.result.Count; i++)
                            {
                                XTFuturesCandle item = symbols.result[i];

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

                        SendLogMessage($" CreateQueryCandles error, Code: {symbols.returnCode}\n"
                                       + $"Message code: {symbols.msgInfo}", LogMessageType.Error);
                        return null;
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(" CreateQueryCandles error: " + exception.ToString(), LogMessageType.Error);
                }

                return null;
            }

            #endregion

            #region 6 WebSocket creation

            private WebSocket _webSocketPrivate;

            private WebSocket _webSocketPublicMarketDepths;

            private WebSocket _webSocketPublicTrades;

            private readonly string _webSocketPrivateUrl = "wss://fstream.xt.com/ws/user";

            private readonly string _webSocketPublicUrl = "wss://fstream.xt.com/ws/market";

            private readonly string _socketLocker = "webSocketLockerXT";

            private string _socketActivateLocker = "socketActivateLocker";

            private void CreateWebSocketConnection()
            {
                try
                {
                    if (_webSocketPrivate != null)
                    {
                        return;
                    }

                    //if (_myProxy != null)
                    //{
                    //    _webSocketPrivate.SetProxy(_myProxy);
                    //}

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
                            ConnectEvent?.Invoke();
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.Message, LogMessageType.Error);
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

                        if (message.Contains(" The remote party closed the WebSocket connection"))
                        {
                            // ignore
                        }
                        else
                        {
                            SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(" Data socket error" + exception.ToString(), LogMessageType.Error);
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
                    SendLogMessage(" WebSocketPublicTrades Message Received error: " + error.ToString(), LogMessageType.Error);
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
                        DisconnectEvent?.Invoke();
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }

            private void _webSocketPublicTrades_OnOpen(object sender, EventArgs e)
            {
                CheckFullActivation();
                SendLogMessage(" WebSocketPublicTrades Connection to public trades is Open", LogMessageType.System);
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

                        if (message.Contains(" The remote party closed the WebSocket connection"))
                        {
                            // ignore
                        }
                        else
                        {
                            SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(" Data socket error" + exception.ToString(), LogMessageType.Error);
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
                    SendLogMessage(" WebSocketPublicMessage Received error: " + error.ToString(), LogMessageType.Error);
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
                        DisconnectEvent?.Invoke();
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }

            private void _webSocketPublicMarketDepths_OnOpen(object sender, EventArgs e)
            {
                CheckFullActivation();
                SendLogMessage(" WebSocketPublicConnection to public data is Open", LogMessageType.System);
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

                        if (message.Contains(" The remote party closed the WebSocket connection"))
                        {
                            // ignore
                        }
                        else
                        {
                            SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(" Data socket error" + exception.ToString(), LogMessageType.Error);
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
                    SendLogMessage(" WebSocket Private Message Received error: " + error.ToString(), LogMessageType.Error);
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
                        DisconnectEvent?.Invoke();
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }

            private void _webSocketPrivate_OnOpen(object sender, EventArgs e)
            {
                try
                {
                    SendLogMessage($"ListenKey successfully received: {_listenKey}", LogMessageType.Error);
                    CheckFullActivation();
                    SendLogMessage(" WebSocketPrivateConnection to private data is Open", LogMessageType.System);
                    _webSocketPrivate.SendAsync($"{{\"method\":\"SUBSCRIBE\",\"params\":[\"order@{_listenKey}\",\"trade@{_listenKey}\",\"position@{_listenKey}\",\"balance@{_listenKey}\",\"notify@{_listenKey}\"],\"id\":\"3214\"}}");
                    SendLogMessage($"DEBUG PRIVATE SUBSCRIBE: {($"{{\"method\":\"SUBSCRIBE\",\"params\":[\"order@{_listenKey}\",\"trade@{_listenKey}\",\"position@{_listenKey}\",\"balance@{_listenKey}\",\"notify@{_listenKey}\"],\"id\":\"3214\"}}")}", LogMessageType.Error);
                }
                catch (Exception exception)
                {
                    SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
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
                            _webSocketPublicMarketDepths.SendAsync("ping");
                            _webSocketPrivate.SendAsync("ping");
                            _webSocketPublicTrades.SendAsync("ping");
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

            private readonly RateGate _rateGateSecuritySubscribed = new RateGate(1, TimeSpan.FromMilliseconds(200));

            private List<string> _subscribedSecurities = new List<string>();

            public void Subscribe(Security security)
            {
                try
                {
                    _rateGateSecuritySubscribed.WaitToProceed();

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
                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    if (_subscribedSecurities[i].Equals(security.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }

                lock (_socketLocker)
                {
                    _webSocketPublicMarketDepths.SendAsync($"{{\"method\":\"SUBSCRIBE\",\"params\":[\"depth_update@{security.Name}\", \"depth@{security.Name},{20}\"],\"id\":\"1126\"}}");
                    _webSocketPublicTrades.SendAsync($"{{\"method\":\"SUBSCRIBE\",\"params\":[\"trade@{security.Name}\"],\"id\":\"1127\"}}");
                }
            }

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

                        XTFuturesResponseWebSocket<object> action = JsonConvert.DeserializeObject<XTFuturesResponseWebSocket<object>>(message);

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

                        FIFOListWebSocketPublicTradesMessage.TryDequeue(out string message);

                        if (message == null)
                        {
                            continue;
                        }

                        if (message.Equals("pong", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (message.IndexOf("\"topic\"", StringComparison.OrdinalIgnoreCase) == -1)
                        {
                            continue;
                        }

                        XTFuturesResponseWebSocket<object> action =
                            JsonConvert.DeserializeAnonymousType(message, new XTFuturesResponseWebSocket<object>());

                        if (action != null && action.topic != null && action.@event != null)
                        {
                            string evt = action.@event;

                            if (!string.IsNullOrEmpty(evt) &&
                                evt.StartsWith("trade@", StringComparison.OrdinalIgnoreCase))
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

                        FIFOListWebSocketPrivateMessage.TryDequeue(out string message);

                        if (message == null)
                        {
                            continue;
                        }

                        if (message.Equals("pong"))
                        {
                            continue;
                        }

                        XTFuturesResponseWebSocket<object> action = JsonConvert.DeserializeAnonymousType(message, new XTFuturesResponseWebSocket<object>());
                        SendLogMessage($"{message}", LogMessageType.Error);

                        if (action == null || action.topic == null)
                        {
                            continue;
                        }

                        if (action.topic.Equals("order"))
                        {
                            SendLogMessage("WS PRIVATE: first 'order' event received — subscription alive", LogMessageType.Error);
                            UpdateOrder(message);
                            continue;
                        }

                        if (action.topic.Equals("balance"))
                        {
                            SendLogMessage("WS PRIVATE: first 'balance' event received — subscription alive", LogMessageType.Error);

                            UpdatePortfolio(message);
                            continue;
                        }

                        if (action.topic.Equals("trade"))
                        {
                            SendLogMessage("WS PRIVATE: first 'MyTrade' event received — subscription alive", LogMessageType.Error);

                            UpdateMyTrade(message);
                            continue;
                        }

                        if (action.topic.Equals("position"))
                        {
                            SendLogMessage("WS PRIVATE: first 'position' event received — subscription alive", LogMessageType.Error);

                            UpdatePosition(message);
                        }
                    }
                    catch (Exception exception)
                    {
                        Thread.Sleep(2000);
                    }
                }
            }

            private List<MarketDepth> _marketDepths = new List<MarketDepth>();

            public event Action<MarketDepth> MarketDepthEvent;

            private DateTime _lastTimeMd = DateTime.MinValue;

            private bool startDepth = true;

            private readonly object _mdLock = new object();

            private readonly object _posLock = new object();

            int level = 20;

            private MarketDepth GetOrCreateDepth(string symbol)
            {
                for (int i = 0; i < _marketDepths.Count; i++)
                {
                    if (_marketDepths[i].SecurityNameCode == symbol)
                    {
                        return _marketDepths[i];
                    }
                }

                MarketDepth md = new MarketDepth
                {
                    SecurityNameCode = symbol,
                    Asks = new List<MarketDepthLevel>(level),
                    Bids = new List<MarketDepthLevel>(level)
                };

                _marketDepths.Add(md);

                return md;
            }

            private void SnapshotDepth(string message)
            {
                try
                {
                    XTFuturesResponseWebSocket<XTFuturesSnapshotDepth> responseDepth =
                        JsonConvert.DeserializeObject<XTFuturesResponseWebSocket<XTFuturesSnapshotDepth>>(message);

                    XTFuturesSnapshotDepth depth = responseDepth?.data;

                    if (depth == null)
                    {
                        return;
                    }

                    string symbol = depth.s;

                    if (string.IsNullOrWhiteSpace(symbol))
                    {
                        SendLogMessage(" SnapshotDepth: empty symbol in payload", LogMessageType.Error);
                        return;
                    }

                    MarketDepth copyToFire = null;

                    lock (_mdLock)
                    {
                        MarketDepth md = GetOrCreateDepth(symbol);

                        if (md.Asks == null)
                        {
                            md.Asks = new List<MarketDepthLevel>(level);
                        }

                        if (md.Bids == null)
                        {
                            md.Bids = new List<MarketDepthLevel>(level);
                        }

                        ApplySnapshotSide(depth.a, md.Asks, isAsk: true, maxLevels: level);
                        ApplySnapshotSide(depth.b, md.Bids, isAsk: false, maxLevels: level);

                        md.Time = ServerTime;

                        if (md.Time < _lastTimeMd)
                        {
                            md.Time = _lastTimeMd;
                        }
                        else if (md.Time == _lastTimeMd)
                        {
                            _lastTimeMd = DateTime.FromBinary(_lastTimeMd.Ticks + 1);
                            md.Time = _lastTimeMd;
                        }

                        _lastTimeMd = md.Time;

                        _awaitingSnapshot.Remove(symbol);

                        startDepth = false;

                        if (_bufferBySymbol != null &&
                            _bufferBySymbol.TryGetValue(symbol, out List<XTFuturesUpdateDepth> list) &&
                            list != null && list.Count > 0)
                        {
                            for (int i = 0; i < list.Count; i++)
                            {
                                XTFuturesUpdateDepth ev = list[i];

                                ApplyIncrementSide(ev.a, md.Asks, isAsk: true);
                                ApplyIncrementSide(ev.b, md.Bids, isAsk: false);
                            }

                            list.Clear();
                        }

                        copyToFire = md.GetCopy();
                    }

                    MarketDepthEvent?.Invoke(copyToFire);
                }
                catch (Exception exception)
                {
                    SendLogMessage(" SnapshotDepth error: " + exception, LogMessageType.Error);
                }
            }

            private void UpsertLevel(List<MarketDepthLevel> list, double price, double qty, bool isAsk)
            {
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
                    if (qty == 0)
                    {
                        list.RemoveAt(idx);
                    }
                    else
                    {
                        MarketDepthLevel lvl = list[idx];

                        if (isAsk)
                        {
                            lvl.Ask = qty;
                        }
                        else
                        {
                            lvl.Bid = qty;
                        }

                        list[idx] = lvl;
                    }

                    return;
                }

                if (qty == 0)
                {
                    return;
                }

                MarketDepthLevel lvlNew = isAsk
                    ? new MarketDepthLevel { Price = price, Ask = qty }
                    : new MarketDepthLevel { Price = price, Bid = qty };

                int pos = 0;

                while (pos < list.Count &&
                       (isAsk ? list[pos].Price < price
                              : list[pos].Price > price))
                {
                    pos++;
                }

                list.Insert(pos, lvlNew);

                if (list.Count > level)
                {
                    list.RemoveAt(20);
                }
            }

            private void ApplySnapshotSide(List<List<string>> side, List<MarketDepthLevel> dest, bool isAsk, int maxLevels = 20)
            {
                dest.Clear();

                if (side == null || side.Count == 0)
                {
                    return;
                }

                int take = Math.Min(maxLevels, side.Count);

                for (int i = 0; i < take; i++)
                {
                    if (side[i] == null || side[i].Count < 2)
                    {
                        continue;
                    }

                    decimal price = (side[i][0]).ToDecimal();
                    decimal qty = (side[i][1]).ToDecimal();

                    if (qty <= 0m)
                    {
                        continue;
                    }

                    dest.Add(isAsk
                        ? new MarketDepthLevel { Price = (double)price, Ask = (double)qty }
                        : new MarketDepthLevel { Price = (double)price, Bid = (double)qty });
                }

                if (isAsk)
                {
                    dest.Sort(CompareAsk);
                }
                else
                {
                    dest.Sort(CompareBid);
                }

                if (dest.Count > maxLevels)
                {
                    dest.RemoveRange(maxLevels, dest.Count - maxLevels);
                }
            }

            private int CompareAsk(MarketDepthLevel x, MarketDepthLevel y) => x.Price.CompareTo(y.Price);

            private int CompareBid(MarketDepthLevel x, MarketDepthLevel y) => y.Price.CompareTo(x.Price);

            private void ApplyIncrementSide(List<List<string>> side, List<MarketDepthLevel> dest, bool isAsk)
            {
                if (side == null)
                {
                    return;
                }

                for (int i = 0; i < side.Count; i++)
                {
                    decimal p = (side[i][0]).ToDecimal();
                    decimal q = (side[i][1]).ToDecimal();

                    UpsertLevel(dest, (double)p, (double)q, isAsk);
                }
            }

            private readonly ConcurrentDictionary<string, List<XTFuturesUpdateDepth>> _bufferBySymbol =
                new ConcurrentDictionary<string, List<XTFuturesUpdateDepth>>();

            private readonly HashSet<string> _awaitingSnapshot = new HashSet<string>();

            private void UpdateDepth(string message)
            {
                try
                {
                    XTFuturesResponseWebSocket<XTFuturesUpdateDepth> resp = JsonConvert.DeserializeObject
                       <XTFuturesResponseWebSocket<XTFuturesUpdateDepth>>(message);

                    XTFuturesUpdateDepth depth = resp?.data;

                    if (depth == null)
                    {
                        return;
                    }

                    string symbol = depth.s;

                    if (string.IsNullOrWhiteSpace(symbol))
                    {
                        SendLogMessage(" UpdateDepth: empty symbol in payload", LogMessageType.Error);
                        return;
                    }

                    MarketDepth copyToFire = null;

                    lock (_mdLock)
                    {
                        MarketDepth md = GetOrCreateDepth(symbol);

                        bool needsSnapshot = startDepth || md.Asks == null ||
                                             md.Bids == null || md.Asks.Count == 0 ||
                                             md.Bids.Count == 0 || _awaitingSnapshot.Contains(symbol);

                        if (needsSnapshot)
                        {
                            _awaitingSnapshot.Add(symbol);

                            if (!_bufferBySymbol.TryGetValue(symbol, out List<XTFuturesUpdateDepth> list) || list == null)
                            {
                                list = new List<XTFuturesUpdateDepth>();
                                _bufferBySymbol[symbol] = list;
                            }

                            list.Add(depth);
                        }
                        else
                        {
                            ApplyIncrementSide(depth.a, md.Asks, isAsk: true);
                            ApplyIncrementSide(depth.b, md.Bids, isAsk: false);

                            md.Time = ServerTime;

                            if (md.Time < _lastTimeMd)
                            {
                                md.Time = _lastTimeMd;
                            }
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
                catch (Exception exception)
                {
                    SendLogMessage(" UpdateDepth error: " + exception, LogMessageType.Error);
                }
            }

            public event Action<MyTrade> MyTradeEvent;

            public event Action<Trade> NewTradesEvent;

            private void UpdateTrade(string message)
            {
                try
                {
                    XTFuturesResponseWebSocket<XTFuturesPublicTrade> responseTrade = JsonConvert.DeserializeObject<XTFuturesResponseWebSocket<XTFuturesPublicTrade>>(message);

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
                    trade.Id = Convert.ToInt64(responseTrade.data?.t).ToString();

                    NewTradesEvent?.Invoke(trade);
                }
                catch (Exception exception)
                {
                    SendLogMessage(" UpdateTrade error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            private void UpdatePosition(string message)
            {
                try
                {
                    if (_portfolios == null || _portfolios.Count == 0)
                    {
                        SendLogMessage("UpdatePosition: portfolio list is empty", LogMessageType.Error);
                        return;
                    }

                    var raw = message;
                    SendLogMessage("DEBUG Update positions by WS : " + raw, LogMessageType.System);
                    XTFuturesResponseWebSocket<XTFuturesPositionData> response =
                        JsonConvert.DeserializeObject<XTFuturesResponseWebSocket<XTFuturesPositionData>>(message);

                    if (response == null || response.data == null)
                    {
                        SendLogMessage("UpdatePosition: empty response or data", LogMessageType.Error);
                        return;
                    }

                    Portfolio portfolio = _portfolios[0];


                    if (portfolio == null)
                    {
                        SendLogMessage("UpdatePosition: Portfolio not found", LogMessageType.Error);
                        return;
                    }

                    string side = response.data.positionSide != null ? response.data.positionSide.ToUpperInvariant() : "LONG";
                    string symbol = response.data.symbol + "_" + side;
                    decimal qty = response.data.positionSize.ToDecimal();
                    decimal unreal = response.data.realizedProfit.ToDecimal();
                    decimal margin = response.data.openOrderMarginFrozen.ToDecimal();
                    qty = side == "SHORT" ? -Math.Abs(qty) : qty;

                    PositionOnBoard pos = new PositionOnBoard();

                    pos.PortfolioName = _portfolioName;/////portfolio.number
                    pos.SecurityNameCode = symbol;
                    pos.ValueCurrent = Math.Round(qty, 6);
                    pos.UnrealizedPnl = Math.Round(unreal, 6);
                    pos.ValueBlocked = Math.Round(margin, 6);
                    pos.ValueBegin = 0m;

                    portfolio.SetNewPosition(pos);

                    SendLogMessage($"[WS Position] {symbol} qty={qty} pnl={unreal} margin={margin} lev={response.data.leverage}", LogMessageType.Error);

                    PortfolioEvent?.Invoke(_portfolios);
                }
                catch (Exception exception)
                {
                    SendLogMessage("Error, while processing the position: " + exception.ToString(), LogMessageType.Error);
                }
            }

            private void UpdateMyTrade(string message)
            {
                try
                {

                    XTFuturesResponseWebSocket<XTFuturesMyTrade> response = JsonConvert.DeserializeObject<XTFuturesResponseWebSocket<XTFuturesMyTrade>>(message);

                    if (response == null || response.data == null)
                    {
                        return;
                    }

                    MyTrade myTrade = new MyTrade();

                    myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(response.data.timestamp));
                    myTrade.NumberOrderParent = response.data.orderId;

                    if (response.data.clientOrderId == "0" && !string.IsNullOrEmpty(response.data.orderId))
                    {
                        int mapped;

                        if (_marketOrderIdToClientId.TryGetValue(myTrade.NumberOrderParent, out mapped))
                        {
                            myTrade.NumberOrderParent = mapped.ToString();
                        }
                    }
                    else
                    {
                        myTrade.NumberTrade = Convert.ToInt64(response.data.clientOrderId).ToString();
                    }

                    myTrade.Price = response.data.price.ToDecimal();
                    myTrade.SecurityNameCode = response.data.symbol;
                    myTrade.Volume = response.data.quantity.ToDecimal();

                    myTrade.Side = response.data.orderSide == "BUY" ? Side.Buy : Side.Sell;



                    SendLogMessage($"myTrade created={myTrade.Time} OrderId={response.data.orderId} orderSide={response.data.orderSide} posSide = {response.data.positionSide} clientorderid = {response.data.clientOrderId}"

              , LogMessageType.Error);

                    MyTradeEvent?.Invoke(myTrade);
                }
                catch (Exception exception)
                {
                    SendLogMessage(" UpdateMyTrade error: " + exception.ToString(), LogMessageType.Error);
                }
            }


            private void UpdatePortfolio(string message)
            {
                try
                {
                    XTFuturesResponseWebSocket<XTFuturesResponsePortfolio> portfolios =
                        JsonConvert.DeserializeObject<XTFuturesResponseWebSocket<XTFuturesResponsePortfolio>>(message);

                    if (portfolios == null || portfolios.data == null ||
                       _portfolios == null || _portfolios.Count == 0)
                    {
                        return;
                    }

                    Portfolio portfolio = _portfolios[0];

                    decimal wallet = portfolios.data.walletBalance.ToDecimal();
                    decimal orderFrozen = portfolios.data.openOrderMarginFrozen.ToDecimal();
                    decimal locked = orderFrozen;
                    decimal available = wallet - locked;

                    if (available < 0m)
                    {
                        available = 0m;
                    }

                    portfolio.ValueCurrent = 1;

                    PositionOnBoard pos = new PositionOnBoard();
                    pos.PortfolioName = _portfolioName;
                    pos.SecurityNameCode = (portfolios.data.coin.ToUpper() ?? "USDT");
                    pos.ValueBlocked = Math.Round(locked, 5);
                    pos.ValueCurrent = Math.Round(available, 5);

                    portfolio.SetNewPosition(pos);

                    PortfolioEvent?.Invoke(_portfolios);
                }
                catch (Exception exception)
                {
                    SendLogMessage(" UpdatePortfolio error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            private readonly Dictionary<string, int> _marketOrderIdToClientId =
             new Dictionary<string, int>(StringComparer.Ordinal);

            private void UpdateOrder(string message)
            {
                try
                {
                    var mes = message;
                    SendLogMessage($"UpdateOrder{mes}", LogMessageType.System);
                    XTFuturesResponseWebSocket<XTFuturesUpdateOrder> order =
                        JsonConvert.DeserializeObject<XTFuturesResponseWebSocket<XTFuturesUpdateOrder>>(message);

                    if (order == null)
                    {
                        return;
                    }

                    SendLogMessage($"[DEBUG UpdateOrder] Order from exchange - " +
                    $"ID: {order.data.orderId}, " +
                    $"Type: {order.data.orderType}, " +
                    $"State: {order.data.state}, " +
                    $"Price: {order.data.price}, " +
                    $"Side: {order.data.orderSide}", LogMessageType.System);
                    Order updateOrder = new Order();

                    updateOrder.SecurityNameCode = order.data.symbol;
                    updateOrder.SecurityClassCode = GetNameClass(order.data.symbol);

                    updateOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.data.createdTime));
                    updateOrder.NumberMarket = order.data.orderId;

                    updateOrder.NumberUser = GetUserOrderNumber(order.data.orderId);

                    updateOrder.Side = order.data.orderSide.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;

                    updateOrder.State = GetOrderState(order.data.state);

                    updateOrder.TypeOrder = MapOrderType(order.data.orderType);


                    SendLogMessage($"[DEBUG UpdateOrder] After mapping - OsEngine type: {updateOrder.TypeOrder}", LogMessageType.System);

                    updateOrder.ServerType = ServerType.XTFutures;
                    updateOrder.PortfolioNumber = _portfolioName;
                    updateOrder.Volume = order.data.origQty.ToDecimal();
                    updateOrder.Price = order.data.price.ToDecimal();


                    if (updateOrder.State == OrderStateType.Done)
                    {
                        updateOrder.TimeDone = updateOrder.TimeCallBack;
                    }
                    else if (updateOrder.State == OrderStateType.Cancel)
                    {
                        updateOrder.TimeCancel = updateOrder.TimeCallBack;
                    }

                    if (updateOrder.State == OrderStateType.Done || updateOrder.State == OrderStateType.Partial)
                    {
                        CreateQueryMyTrade(updateOrder.NumberMarket);
                        CreateQueryPositions(false);////////////
                    }
                    if (updateOrder.State == OrderStateType.Done ||
           updateOrder.State == OrderStateType.Cancel ||
           updateOrder.State == OrderStateType.Partial)///////////////////////
                    {
                        try
                        {
                            CreateQueryPositions(false);
                            SendLogMessage("Positions refreshed after order state = " + updateOrder.State, LogMessageType.System);
                        }
                        catch (Exception ex)
                        {
                            SendLogMessage(" Error refreshing positions after order update: " + ex.Message, LogMessageType.Error);
                        }
                    }

                    SendLogMessage($"[DEBUG UpdateOrder] Final order - " +
                                  $"NumberUser: {updateOrder.NumberUser}, " +
                                  $"Type: {updateOrder.TypeOrder}, " +
                                  $"State: {updateOrder.State}, " +
                                  $"Price: {updateOrder.Price}", LogMessageType.System);



                    MyOrderEvent?.Invoke(updateOrder);
                }
                catch (Exception exception)
                {
                    SendLogMessage(" UpdateOrder error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            private OrderStateType GetOrderState(string orderStatusResponse)
            {
                OrderStateType stateType;

                switch (orderStatusResponse.ToLower())
                {
                    case ("new"):
                    case ("placed"):
                    case ("active"):
                        stateType = OrderStateType.Active;
                        break;

                    case ("partially_filled"):
                        stateType = OrderStateType.Partial;
                        break;

                    case ("filled"):
                    case ("done"):
                        stateType = OrderStateType.Done;
                        break;

                    case ("cancel"):
                    case ("canceled"):
                    case ("expired"):
                        stateType = OrderStateType.Cancel;
                        break;

                    case ("rejected"):
                        stateType = OrderStateType.Fail;
                        break;

                    default:
                        stateType = OrderStateType.None;
                        break;
                }

                return stateType;
            }

            #endregion

            #region 11 Trade

            private readonly RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(300));

            private readonly RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(300));

            private readonly RateGate _rateGatePosition = new RateGate(1, TimeSpan.FromMilliseconds(300));



            //BUY + LONG = открыть/увеличить лонг
            //SELL + LONG = закрыть/уменьшить лонг

            //BUY + SHORT = закрыть/уменьшить шорт
            //SELL + SHORT = открыть/увеличить шорт
            public void SendOrder(Order order)
            {
                _rateGateSendOrder.WaitToProceed();

                try
                {
                    // --- БАЗОВЫЕ ПРОВЕРКИ
                    if (order == null || string.IsNullOrWhiteSpace(order.SecurityNameCode))
                    {
                        SendLogMessage(" SendOrder> bad order or symbol", LogMessageType.Error);
                        CreateOrderFail(order);
                        return;
                    }

                    if (order.Volume <= 0)
                    {
                        SendLogMessage(" SendOrder> bad volume " + order.Volume.ToString(System.Globalization.CultureInfo.InvariantCulture), LogMessageType.Error);
                        CreateOrderFail(order);
                        return;
                    }

                    if (order.TypeOrder == OrderPriceType.Limit && order.Price <= 0)
                    {
                        SendLogMessage(" SendOrder> bad limit price " + order.Price.ToString(System.Globalization.CultureInfo.InvariantCulture), LogMessageType.Error);
                        CreateOrderFail(order);
                        return;
                    }

                    // BUY => LONG; SELL => SHORT. Никакой reduce-логики не используем.
                    // = order.Side == Side.Buy ? "BUY" : "SELL";
                    //string positionSide = order.Side == Side.Buy ? "LONG" : "SHORT";

                    string positionSide = "";

                    if (order.PositionConditionType == OrderPositionConditionType.Open || order.PositionConditionType == OrderPositionConditionType.None)
                    {
                        positionSide = order.Side == Side.Buy ? "LONG" : "SHORT";
                    }

                    if (order.PositionConditionType == OrderPositionConditionType.Close)
                    {
                        // BUY + LONG = open / increase a long position
                        // SELL + LONG = close / decrease a long position

                        // BUY + SHORT = close / decrease a short position
                        // SELL + SHORT = open / increase a short position

                        if (order.Side == Side.Sell)
                        {
                            positionSide = "LONG";
                        }
                        else if (order.Side == Side.Buy)
                        {
                            positionSide = "SHORT";
                        }
                    }

                    XTFuturesSendOrder data = new XTFuturesSendOrder();
                    data.symbol = order.SecurityNameCode;
                    data.clientOrderId = order.NumberUser.ToString();
                    data.orderSide = order.Side.ToString().ToUpper(); // "BUY" : "SELL"
                    data.origQty = order.Volume.ToString(CultureInfo.InvariantCulture);
                    data.orderType = order.TypeOrder.ToString().ToUpper(); // LIMIT : MARKET
                    data.positionSide = positionSide;                      // LONG : SHORT
                    data.timeInForce = order.TypeOrder == OrderPriceType.Limit ? "GTC" : "IOC";
                    data.price = order.TypeOrder == OrderPriceType.Market ? null : order.Price.ToString(CultureInfo.InvariantCulture);

                    SendLogMessage("SendOrder:Symbol=" + data.symbol
                        + " ,Side=" + data.orderSide
                        + " , PosSide=" + data.positionSide
                        + " , Price=" + (data.price == null ? "null" : data.price)
                        + " , Qty=" + data.origQty
                        + " , Type=" + data.orderType, LogMessageType.Error);

                    // Портфель OsEngine
                    order.PortfolioNumber = _portfolioName;

                    // --- ВЫЗОВ XT API
                    IRestResponse responseMessage = CreatePrivateQuery("/future/trade/v1/order/create", Method.POST, null, data);

                    if (responseMessage == null)
                    {
                        SendLogMessage(" SendOrder: response is null", LogMessageType.Error);
                        CreateOrderFail(order);
                        return;
                    }

                    XTFuturesResponseRest<string> stateResponse =
                        JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(responseMessage.Content);

                    if (responseMessage.StatusCode != HttpStatusCode.OK || stateResponse == null)
                    {
                        SendLogMessage(" SendOrder: HTTP error - " + responseMessage.StatusCode.ToString(), LogMessageType.Error);
                        CreateOrderFail(order);
                        return;
                    }

                    if (stateResponse.returnCode == "0"
                        && string.Equals(stateResponse.msgInfo, "SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {
                        order.NumberMarket = stateResponse.result;


                        if (order.NumberUser != 0 && order.NumberMarket != "0")
                        {
                            if (!_numberUser.ContainsKey(order.NumberMarket))
                            {
                                _numberUser.Add(order.NumberMarket, order.NumberUser);
                            }
                        }


                        SaveOrderTrackers();

                        SendLogMessage(" Order sent successfully. NumUser: " + order.NumberUser.ToString(CultureInfo.InvariantCulture)
                            + ", OrderId: " + order.NumberMarket
                            + ", OrderSide: " + data.orderSide
                            + ", PositionSide: " + positionSide
                            + ", OrderType: " + data.orderType
                            + ", Volume: " + data.origQty,
                            LogMessageType.Error);



                        if (string.Equals(data.orderType, "MARKET", StringComparison.OrdinalIgnoreCase))
                        {
                            order.State = OrderStateType.Done;
                            MyOrderEvent?.Invoke(order);
                            CreateQueryMyTrade(order.NumberMarket);
                        }
                        else
                        {
                            // LIMIT — уходим в Active
                            order.State = OrderStateType.Active;
                            order.TimeCallBack = DateTime.Now;
                            MyOrderEvent?.Invoke(order);
                        }
                    }
                    else
                    {
                        string errCode = stateResponse.error != null ? stateResponse.error.code : "";
                        string errMsg = stateResponse.error != null ? stateResponse.error.msg : "";

                        SendLogMessage(" SendOrder Fail: Code=" + stateResponse.returnCode
                            + ", Msg=" + stateResponse.msgInfo
                            + ", Error=" + errCode + " - " + errMsg, LogMessageType.Error);

                        CreateOrderFail(order);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(" SendOrder error: " + exception.ToString(), LogMessageType.Error);
                }
            }


            //public void SendOrder(Order order)
            //{
            //    _rateGateSendOrder.WaitToProceed();

            //    try
            //    {
            //        order.PortfolioNumber=_portfolioName;

            //        CreateQueryPositions(false);
            //        Thread.Sleep(1500);

            //        decimal longPos, shortAbs;
            //        GetCurrentPositionsForSymbol(order.SecurityNameCode, out longPos, out shortAbs);

            //        string orderSide = order.Side == Side.Buy ? "BUY" : "SELL";
            //        string positionSide;
            //        bool isReduceIntent;

            //        DecideHedgePositionSide(orderSide, longPos, shortAbs, out positionSide, out isReduceIntent);
            //        if (order.TypeOrder == OrderPriceType.Limit && order.Price <= 0)
            //        {
            //            SendLogMessage($"Error: Limit order price must be greater than 0. Price: {order.Price}", LogMessageType.Error);
            //            CreateOrderFail(order);
            //            return;
            //        }
            //        // --- EPSILON GUARD против ложного "уменьшения" несуществующей позиции
            //        decimal eps = 0.00000001m;
            //        // Если хотим SELL и "уменьшать" LONG, но фактического лонга нет — переключаемся на SHORT (открытие шорта)
            //        if (orderSide == "SELL" && isReduceIntent && longPos <= eps)
            //        {
            //            positionSide = "SHORT";
            //            isReduceIntent = false;
            //        }
            //        // Если хотим BUY и "уменьшать" SHORT, но фактического шорта нет — переключаемся на LONG (открытие лонга)
            //        if (orderSide == "BUY" && isReduceIntent && shortAbs <= eps)
            //        {
            //            positionSide = "LONG";
            //            isReduceIntent = false;
            //        }
            //        XTFuturesSendOrder data = new XTFuturesSendOrder();
            //        data.symbol = order.SecurityNameCode;
            //        data.clientOrderId = order.NumberUser.ToString();
            //        data.orderSide = orderSide;
            //        data.origQty = order.Volume.ToString(CultureInfo.InvariantCulture);
            //        data.orderType = order.TypeOrder.ToString().ToUpper();
            //        data.positionSide = positionSide;//
            //        data.timeInForce = order.TypeOrder == OrderPriceType.Limit ? "GTC" : "IOC";
            //        data.price = order.TypeOrder == OrderPriceType.Market ? null : order.Price.ToString(CultureInfo.InvariantCulture);

            //        SendLogMessage($"SendOrder:Symbol={data.symbol} ,Side={data.orderSide} , PosSide={data.positionSide} " +
            //                       $"Price={data.price} Qty={data.origQty}, Type= {data.orderType}", LogMessageType.Error);

            //        IRestResponse responseMessage = CreatePrivateQuery("/future/trade/v1/order/create", Method.POST, null, data);

            //        if (responseMessage == null)
            //        {
            //            SendLogMessage(" SendOrder: response is null", LogMessageType.Error);
            //            CreateOrderFail(order);
            //            return;
            //        }

            //        XTFuturesResponseRest<string> stateResponse =
            //            JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(responseMessage.Content);

            //        if (responseMessage.StatusCode != HttpStatusCode.OK || stateResponse == null)
            //        {
            //            SendLogMessage(" SendOrder: HTTP error - " + responseMessage.StatusCode.ToString(), LogMessageType.Error);
            //            CreateOrderFail(order);
            //            return;
            //        }

            //        if (stateResponse.returnCode == "0"
            //            && string.Equals(stateResponse.msgInfo, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            //        {
            //            order.NumberMarket = stateResponse.result;

            //            if (order.NumberUser != 0 && order.NumberMarket != "0")
            //                {
            //                if (!_numberUser.ContainsKey(order.NumberMarket))
            //                {
            //                    _numberUser.Add(order.NumberMarket, order.NumberUser);
            //                }
            //            }

            //            SaveOrderTrackers();

            //            SendLogMessage(" Order sent successfully. NumUser: " + order.NumberUser.ToString(CultureInfo.InvariantCulture)
            //                + ", OrderId: " + order.NumberMarket
            //                + ", OrderSide: " + orderSide
            //                + ", PositionSide: " + positionSide
            //                 + ", OrderType: " + data.orderType
            //                 + ", Volume:" + data.origQty,
            //                LogMessageType.Error);

            //            if (string.Equals(data.orderType, "MARKET", StringComparison.OrdinalIgnoreCase))
            //            {
            //                order.State = OrderStateType.Done;
            //                MyOrderEvent?.Invoke(order);
            //                CreateQueryMyTrade(order.NumberMarket);
            //            }
            //            else
            //            {
            //                order.State = OrderStateType.Active;
            //                order.TimeCallBack = DateTime.Now;
            //                MyOrderEvent?.Invoke(order);
            //            }
            //        }
            //        else
            //        {
            //            string errCode = stateResponse.error != null ? stateResponse.error.code : "";
            //            string errMsg = stateResponse.error != null ? stateResponse.error.msg : "";

            //            SendLogMessage(" SendOrder Fail: Code=" + stateResponse.returnCode
            //                + ", Msg=" + stateResponse.msgInfo
            //                + ", Error=" + errCode + " - " + errMsg, LogMessageType.Error);

            //            CreateOrderFail(order);
            //        }
            //    }
            //    catch (Exception exception)
            //    {
            //        SendLogMessage(" SendOrder error: " + exception.ToString(), LogMessageType.Error);
            //    }
            //}

            private void GetCurrentPositionsForSymbol(
    string symbol,
    out decimal longPosition,
    out decimal shortPositionAbs)
            {
                longPosition = 0m;
                shortPositionAbs = 0m;

                if (_portfolios == null || _portfolios.Count == 0)
                {
                    return;
                }

                Portfolio portfolio = _portfolios[0];
                List<PositionOnBoard> positions = portfolio.GetPositionOnBoard();

                if (positions == null || positions.Count == 0)
                {
                    return;
                }

                string keyLong = symbol + "_LONG";
                string keyShort = symbol + "_SHORT";

                for (int i = 0; i < positions.Count; i++)
                {
                    PositionOnBoard p = positions[i];
                    if (p == null || string.IsNullOrWhiteSpace(p.SecurityNameCode))
                    {
                        continue;
                    }

                    if (string.Equals(p.SecurityNameCode, keyLong, StringComparison.OrdinalIgnoreCase))
                    {
                        longPosition = p.ValueCurrent;
                    }
                    else if (string.Equals(p.SecurityNameCode, keyShort, StringComparison.OrdinalIgnoreCase))
                    {
                        decimal v = p.ValueCurrent;
                        if (v < 0m)
                        {
                            v = -v;
                        }
                        shortPositionAbs = v;
                    }
                }
            }

            private void DecideHedgePositionSide(
    string orderSide,            // "BUY" или "SELL"
    decimal longPosition,
    decimal shortPositionAbs,
    out string positionSide,     // "LONG" или "SHORT"
    out bool isReduceIntent)     // true = закрываем / уменьшаем
            {
                if (string.Equals(orderSide, "BUY", StringComparison.OrdinalIgnoreCase))
                {
                    if (shortPositionAbs > 0m)
                    {
                        positionSide = "SHORT";
                        isReduceIntent = true;
                    }
                    else
                    {
                        positionSide = "LONG";
                        isReduceIntent = false;
                    }
                }
                else // SELL
                {
                    if (longPosition > 0m)
                    {
                        positionSide = "LONG";
                        isReduceIntent = true;
                    }
                    else
                    {
                        positionSide = "SHORT";
                        isReduceIntent = false;
                    }
                }
            }

            //public void SendOrder(Order order)
            //{
            //    _rateGateSendOrder.WaitToProceed();

            //    try
            //    {
            //        // --- базовая валидация
            //        if (order == null || string.IsNullOrWhiteSpace(order.SecurityNameCode))
            //        {
            //            SendLogMessage(" SendOrder> bad order or symbol", LogMessageType.Error);
            //            CreateOrderFail(order);
            //            return;
            //        }

            //        if (order.Volume <= 0)
            //        {
            //            SendLogMessage(" SendOrder> bad volume " + order.Volume.ToString(CultureInfo.InvariantCulture), LogMessageType.Error);
            //            CreateOrderFail(order);
            //            return;
            //        }


            //        string symbol = order.SecurityNameCode;
            //        string orderSide = order.Side == Side.Buy ? "BUY" : "SELL";

            //        //---берём текущие стороны позиций из портфеля
            //        decimal longPosition = 0m;
            //        decimal shortPositionAbs = 0m;

            //        if (_portfolios != null && _portfolios.Count > 0)
            //        {
            //            Portfolio portfolio = _portfolios[0];
            //            List<PositionOnBoard> positions = portfolio.GetPositionOnBoard();

            //            if (positions != null && positions.Count > 0)
            //            {
            //                string keyLong = symbol + "_LONG";
            //                string keyShort = symbol + "_SHORT";

            //                for (int i = 0; i < positions.Count; i++)
            //                {
            //                    PositionOnBoard p = positions[i];
            //                    if (p == null || string.IsNullOrWhiteSpace(p.SecurityNameCode))
            //                    {
            //                        continue;
            //                    }

            //                    if (string.Equals(p.SecurityNameCode, keyLong, StringComparison.OrdinalIgnoreCase))
            //                    {
            //                        longPosition = p.ValueCurrent;
            //                    }
            //                    else if (string.Equals(p.SecurityNameCode, keyShort, StringComparison.OrdinalIgnoreCase))
            //                    {
            //                        decimal v = p.ValueCurrent;
            //                        if (v < 0m) v = -v;
            //                        shortPositionAbs = v;
            //                    }
            //                }
            //            }
            //        }



            //        // --- hedge mode: выбираем сторону позиции и признак уменьшения
            //        string positionSide;
            //        bool isReduceIntent;

            //        if (orderSide == "BUY")
            //        {
            //            if (shortPositionAbs > 0m)
            //            {
            //                positionSide = "SHORT";
            //                isReduceIntent = true;
            //                SendLogMessage("Closing SHORT position: " + shortPositionAbs.ToString(CultureInfo.InvariantCulture), LogMessageType.System);
            //            }
            //            else
            //            {
            //                positionSide = "LONG";
            //                isReduceIntent = false;
            //                SendLogMessage("Opening/Increasing LONG position", LogMessageType.System);
            //            }
            //        }
            //        else // SELL
            //        {
            //            if (longPosition > 0m)
            //            {
            //                positionSide = "LONG";
            //                isReduceIntent = true;
            //                SendLogMessage("Closing LONG position: " + longPosition.ToString(CultureInfo.InvariantCulture), LogMessageType.System);
            //            }
            //            else
            //            {
            //                positionSide = "SHORT";
            //                isReduceIntent = false;
            //                SendLogMessage("Opening/Increasing SHORT position", LogMessageType.System);
            //            }
            //        }

            //        // --- формирование запроса
            //        XTFuturesSendOrder data = new XTFuturesSendOrder();
            //        data.symbol = symbol;
            //        data.clientOrderId = order.NumberUser.ToString();
            //        data.orderSide = orderSide.ToUpper();
            //        data.origQty = order.Volume.ToString(CultureInfo.InvariantCulture);


            //        if (order.TypeOrder == OrderPriceType.Limit)
            //        {
            //            data.price = order.Price.ToString(CultureInfo.InvariantCulture);
            //            data.timeInForce = "GTC";
            //        }
            //        else
            //        { 
            //            data.price = order.TypeOrder == OrderPriceType.Market ? null : order.Price.ToString(CultureInfo.InvariantCulture);
            //            data.timeInForce = "IOC";
            //        }

            //        data.orderType = order.TypeOrder.ToString().ToUpper();

            //        data.positionSide = positionSide.ToUpper(); // LONG/SHORT

            //        SendLogMessage("SendOrder: Symbol=" + symbol
            //            + ", OrderSide=" + data.orderSide
            //            + ", PositionSide=" + data.positionSide
            //            + ", Type=" + data.orderType
            //            + ", Reduce=" + (isReduceIntent ? "true" : "false")
            //            + ", Qty=" + order.Volume.ToString(CultureInfo.InvariantCulture)
            //            , LogMessageType.Error);

            //        order.PortfolioNumber = _portfolioName;

            //        // --- отправка
            //        IRestResponse responseMessage = CreatePrivateQuery("/future/trade/v1/order/create", Method.POST, null, data);

            //        if (responseMessage == null)
            //        {
            //            SendLogMessage(" SendOrder: response is null", LogMessageType.Error);
            //            CreateOrderFail(order);
            //            return;
            //        }

            //        XTFuturesResponseRest<string> stateResponse =
            //            JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(responseMessage.Content);

            //        if (responseMessage.StatusCode != HttpStatusCode.OK || stateResponse == null)
            //        {
            //            SendLogMessage(" SendOrder: HTTP error - " + responseMessage.StatusCode.ToString(), LogMessageType.Error);
            //            CreateOrderFail(order);
            //            return;
            //        }

            //        if (stateResponse.returnCode == "0"
            //            && string.Equals(stateResponse.msgInfo, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            //        {
            //            order.NumberMarket = stateResponse.result;

            //            if (string.Equals(data.orderType, "MARKET", StringComparison.OrdinalIgnoreCase))  
            //            {
            //                if (!_numberUser.ContainsKey(order.NumberMarket))
            //                {
            //                    _numberUser.Add(order.NumberMarket, order.NumberUser);
            //                }
            //            }

            //            SaveOrderTrackers();

            //            SendLogMessage(" Order sent successfully. NumUser: " + order.NumberUser.ToString(CultureInfo.InvariantCulture)
            //                + ", OrderId: " + order.NumberMarket
            //                + ", OrderSide: " + orderSide
            //                + ", PositionSide: " + positionSide
            //                 + ", OrderType: " + data.orderType,
            //                LogMessageType.Error);

            //            if (string.Equals(data.orderType, "MARKET", StringComparison.OrdinalIgnoreCase))
            //            {
            //                order.State = OrderStateType.Done;
            //                MyOrderEvent?.Invoke(order);
            //                CreateQueryMyTrade(order.NumberMarket);
            //            }
            //            else
            //            {
            //                order.State = OrderStateType.Active;
            //                MyOrderEvent?.Invoke(order);
            //            }
            //        }
            //        else
            //        {
            //            string errCode = stateResponse.error != null ? stateResponse.error.code : "";
            //            string errMsg = stateResponse.error != null ? stateResponse.error.msg : "";

            //            SendLogMessage(" SendOrder Fail: Code=" + stateResponse.returnCode
            //                + ", Msg=" + stateResponse.msgInfo
            //                + ", Error=" + errCode + " - " + errMsg, LogMessageType.Error);

            //            CreateOrderFail(order);
            //        }
            //    }
            //    catch (Exception exception)
            //    {
            //        SendLogMessage(" SendOrder error: " + exception.ToString(), LogMessageType.Error);
            //    }
            //}


            public void ChangeOrderPrice(Order order, decimal newPrice)
            {
                _rateGateSendOrder.WaitToProceed();

                try
                {
                    if (order.TypeOrder == OrderPriceType.Market || order.State == OrderStateType.Done)
                    {
                        SendLogMessage(" ChangeOrderPrice> Can't change price for  Order Market", LogMessageType.Error);
                        return;
                    }

                    var body = new
                    {
                        orderId = long.Parse(order.NumberMarket, CultureInfo.InvariantCulture),
                        price = newPrice.ToString("0.#####", CultureInfo.InvariantCulture),
                        origQty = order.Volume.ToString("0.#####", CultureInfo.InvariantCulture)
                    };

                    if (newPrice <= 0)
                    {
                        SendLogMessage($" ChangeOrderPrice> bad price: {newPrice}", LogMessageType.Error);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(order.NumberMarket))
                    {
                        SendLogMessage(" ChangeOrderPrice> empty exchange order id (NumberMarket)", LogMessageType.Error);
                        return;
                    }

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

                            SendLogMessage($" Success! Order {order.NumberMarket} updated." +
                                $" New orderId={stateResponse.result}, price={newPrice}, qty={order.Volume}", LogMessageType.System);
                            //MyOrderEvent?.Invoke(order);
                        }
                        else
                        {
                            SendLogMessage($" Update returned an empty result {response.Content}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        string code = stateResponse.error.code;
                        string msg = stateResponse.error.msg;
                        SendLogMessage($" Error: returnCode={stateResponse?.returnCode}, code={code}," +
                            $" msg={msg}, raw={response.Content}", LogMessageType.Error);
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
                            // ignore
                        }
                    }
                    else
                    {
                        SendLogMessage($" CancelAllOrders>  error State Code: {responseMessage.StatusCode}", LogMessageType.Error);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(" CancelAllOrders error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            public void CancelAllOrdersToSecurity(Security security)
            {
                _rateGateCancelOrder.WaitToProceed();

                try
                {
                    string jsonRequest = JsonConvert.SerializeObject(
            new XTFuturesCancelAllOrders { symbol = security.Name },
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

                    IRestResponse responseMessage = CreatePrivateQuery("/future/trade/v1/order/cancel-all", Method.POST, null, body: jsonRequest);

                    XTFuturesResponseRest<string> stateResponse = JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(responseMessage.Content);

                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        if (stateResponse.returnCode.Equals("0") && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.CurrentCulture))
                        {
                            // ignore
                        }
                        else
                        {
                            SendLogMessage($" CancelAllOrdersToSecurity error, Code: {stateResponse.returnCode}\n"
                                + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage($" CancelAllOrdersToSecurity>  State Code: {responseMessage.StatusCode}", LogMessageType.Error);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(" CancelAllOrdersToSecurity error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            public bool CancelOrder(Order order)
            {
                _rateGateCancelOrder.WaitToProceed();

                try
                {
                    var body = new
                    {
                        orderId = order.NumberMarket.ToString(CultureInfo.InvariantCulture)
                    };

                    IRestResponse response = CreatePrivateQuery(
                        "/future/trade/v1/order/cancel",
                        Method.POST,
                        query: null,
                        body: body
                    );

                    XTFuturesResponseRest<string> stateResponse = JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(response.Content);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        if (stateResponse.returnCode.Equals("0") && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                        {
                            SendLogMessage($" Successfully canceled the order, order Id: {stateResponse.result}", LogMessageType.Trade);
                            order.State = OrderStateType.Cancel;
                            MyOrderEvent?.Invoke(order);
                            return true;

                        }
                        else
                        {
                            GetOrderStatus(order);
                            SendLogMessage($" CancelOrder error, Code: {stateResponse.returnCode}\n"
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
                    SendLogMessage(" CancelOrder error: " + exception.ToString(), LogMessageType.Error);
                }

                return false;
            }

            public void CreateQueryPositions(bool updateValueBegin)
            {
                _rateGatePosition.WaitToProceed();

                try
                {
                    if (_portfolios == null || _portfolios.Count == 0)
                    {
                        SendLogMessage("CreateQueryPositions: portfolios is empty", LogMessageType.Error);
                        return;
                    }

                    IRestResponse response = CreatePrivateQuery("/future/user/v1/position", Method.GET);

                    if (response == null || response.StatusCode != HttpStatusCode.OK || string.IsNullOrWhiteSpace(response.Content))
                    {
                        SendLogMessage("CreateQueryPositions: bad HTTP response", LogMessageType.Error);
                        return;
                    }

                    XTFuturesResponseRest<List<XTFuturesPosition>> state =
                        JsonConvert.DeserializeObject<XTFuturesResponseRest<List<XTFuturesPosition>>>(response.Content);

                    if (state == null || state.result == null)
                    {
                        SendLogMessage("CreateQueryPositions: result is null", LogMessageType.Error);
                        return;
                    }

                    Portfolio portfolio = _portfolios[0];

                    // Просто обновляем позиции
                    for (int i = 0; i < state.result.Count; i++)
                    {
                        XTFuturesPosition pos = state.result[i];
                        if (pos == null || string.IsNullOrWhiteSpace(pos.symbol) || string.IsNullOrWhiteSpace(pos.positionSide))
                        {
                            continue;
                        }

                        string side = pos.positionSide;
                        string code = pos.symbol + "_" + side;
                        decimal size = pos.positionSize.ToDecimal();
                        size = side == "SHORT" ? -Math.Abs(size) : size;
                        decimal unreal = pos.floatingPL.ToDecimal();
                        decimal margin = pos.isolatedMargin.ToDecimal();


                        PositionOnBoard position = new PositionOnBoard();
                        position.PortfolioName = portfolio.Number;
                        position.SecurityNameCode = code;
                        position.ValueCurrent = Math.Round(size, 6);
                        position.UnrealizedPnl = Math.Round(unreal, 6);
                        position.ValueBlocked = Math.Round(margin, 6);
                        position.ValueBegin = updateValueBegin ? position.ValueCurrent : 0m;

                        portfolio.SetNewPosition(position);

                        SendLogMessage("[REST Position] " + code
                            + " size=" + size.ToString(CultureInfo.InvariantCulture)
                            + " pnl=" + unreal.ToString(CultureInfo.InvariantCulture)
                            + " margin=" + margin.ToString(CultureInfo.InvariantCulture),
                            LogMessageType.System);
                    }

                    PortfolioEvent?.Invoke(_portfolios);
                }
                catch (Exception ex)
                {
                    SendLogMessage("CreateQueryPositions error: " + ex, LogMessageType.Error);
                }
            }


            //private void CreateQueryPositions(bool needUpd)
            //{
            //    _rateGatePosition.WaitToProceed();

            //    try
            //    {
            //        IRestResponse response = CreatePrivateQuery("/future/user/v1/position", Method.GET);

            //        if (response == null || response.StatusCode != HttpStatusCode.OK)
            //        {
            //            SendLogMessage($"CreateQueryPositions:  content empty {response.Content}", LogMessageType.Error);
            //            return;
            //        }

            //        XTFuturesResponseRest<List<XTFuturesPosition>> state =
            //            JsonConvert.DeserializeObject<XTFuturesResponseRest<List<XTFuturesPosition>>>(response.Content);

            //        if (state == null || state.result == null || state.result.Count == 0)
            //        {
            //            return;
            //        }

            //        var portfolio = _portfolios[0];

            //        HashSet<string> aliveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            //        for (int i = 0; i < state.result.Count; i++)
            //        {
            //            var pos = state.result[i];

            //            if (pos == null || string.IsNullOrWhiteSpace(pos.symbol) || string.IsNullOrWhiteSpace(pos.positionSide))
            //            {
            //                continue;
            //            }

            //            string sym = pos.symbol;
            //            string side = pos.positionSide.ToUpperInvariant();

            //            if (side != "LONG" && side != "SHORT")
            //            {
            //                continue;
            //            }

            //            string code = sym + "_" + side;
            //            aliveKeys.Add(code);

            //            var sizeAbs = Math.Abs(pos.positionSize.ToDecimal());
            //            var size = pos.positionSide.Equals("SHORT", StringComparison.OrdinalIgnoreCase)
            //                ? -sizeAbs
            //                : sizeAbs;

            //            PositionOnBoard position = new PositionOnBoard();

            //            position.PortfolioName = portfolio.Number;
            //            position.SecurityNameCode = code;
            //            position.ValueCurrent = size;
            //            position.UnrealizedPnl = pos.floatingPL.ToDecimal();
            //            position.ValueBegin = needUpd ? size : 0m;

            //            portfolio.SetNewPosition(position);
            //        }

            //        var existing = portfolio.GetPositionOnBoard();

            //        for (int i = 0; i < existing.Count; i++)
            //        {
            //            var p = existing[i];
            //            if (p == null || string.IsNullOrWhiteSpace(p.SecurityNameCode))
            //            {
            //                continue;
            //            }

            //            bool isSidePos =
            //                p.SecurityNameCode.EndsWith("_LONG", StringComparison.OrdinalIgnoreCase) ||
            //                p.SecurityNameCode.EndsWith("_SHORT", StringComparison.OrdinalIgnoreCase);

            //            if (isSidePos && !aliveKeys.Contains(p.SecurityNameCode))
            //            {
            //                if (p.ValueCurrent != 0m || p.UnrealizedPnl != 0m)
            //                {
            //                    p.ValueCurrent = 0m;
            //                    p.UnrealizedPnl = 0m;
            //                    portfolio.SetNewPosition(p);
            //                    SendLogMessage($"[Positions] cleared missing on exchange: {p.SecurityNameCode}", LogMessageType.System);
            //                }
            //            }
            //        }

            //        PortfolioEvent?.Invoke(_portfolios);
            //    }
            //    catch (Exception exception)
            //    {
            //        SendLogMessage(" CreateQueryPositions error: " + exception, LogMessageType.Error);
            //    }
            //}
            public void SaveOrdersToFile(List<Order> orders, string filePath)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(filePath, false))
                    {
                        for (int i = 0; i < orders.Count; i++)
                        {
                            Order order = orders[i];

                            if (order == null)
                            {
                                continue;
                            }

                            string line =
                                "№: " + i +
                                " | Market: " + order.NumberMarket +
                                " | User: " + order.NumberUser +
                                " | Security: " + order.SecurityNameCode +
                                " | State: " + order.State +
                                " | TypeOrder: " + order.TypeOrder.ToString() +
                                " | Side: " + order.Side.ToString() +
                                " | Create: " + order.TimeCreate.ToString("yyyy-MM-dd HH:mm:ss") +
                                " | Callback: " + order.TimeCallBack.ToString("yyyy-MM-dd HH:mm:ss");

                            writer.WriteLine(line);
                        }
                    }


                }
                catch (Exception exception)
                {
                    SendLogMessage("Error saving orders to file: " + exception.ToString(), LogMessageType.Error);
                }
            }

            public void GetAllActivOrders()
            {
                try
                {
                    List<Order> orders = GetAllOpenOrders();

                    if (orders == null || orders.Count == 0)
                    {
                        SendLogMessage(" GetActiveOrders> no active orders", LogMessageType.System);
                        return;
                    }

                    for (int i = 0; i < orders.Count; i++)
                    {
                        Order order = orders[i];
                        MyOrderEvent?.Invoke(order);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(" GetActiveOrders error: " + exception, LogMessageType.Error);
                }
            }

            private List<Order> GetOrderHistory()
            {
                _rateGateSendOrder.WaitToProceed();

                try
                {
                    string query = "limit=100";

                    IRestResponse responseMessage = CreatePrivateQuery("/future/trade/v1/order/list-history", Method.GET, query);
                    var res = responseMessage.Content;
                    SendLogMessage($"[list-history]{res}", LogMessageType.Error);

                    XTFuturesResponseRest<XTFuturesOrderResult> stateResponse =
                        JsonConvert.DeserializeObject<XTFuturesResponseRest<XTFuturesOrderResult>>(responseMessage.Content);

                    if (stateResponse == null)
                    {
                        return null;
                    }

                    if (stateResponse.returnCode.Equals("0") &&
                        stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {
                        List<XTFuturesOrderItem> result = stateResponse.result.items;

                        if (result == null)
                        {
                            return new List<Order>();
                        }

                        List<Order> orders = new List<Order>();

                        for (int i = 0; i < result.Count; i++)
                        {
                            XTFuturesOrderItem item = result[i];

                            Order historyOrder = new Order();

                            historyOrder.NumberMarket = item.orderId;

                            if (item.clientOrderId == null || item.clientOrderId == "0")
                            {
                                historyOrder.NumberUser = GetUserOrderNumber(item.orderId);

                            }
                            else
                            {
                                historyOrder.NumberUser = Convert.ToInt32(item.clientOrderId);
                            }

                            historyOrder.SecurityNameCode = item.symbol;
                            historyOrder.SecurityClassCode = GetNameClass(item.symbol);
                            historyOrder.Side = item.orderSide.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;
                            historyOrder.State = GetOrderState(item.state);
                            historyOrder.TypeOrder = MapOrderType(item.orderType);
                            historyOrder.Volume = item.origQty.ToDecimal();
                            historyOrder.Price = item.price.ToDecimal();
                            historyOrder.PortfolioNumber = _portfolioName;
                            historyOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createdTime));
                            historyOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.updatedTime));

                            historyOrder.ServerType = ServerType.XTFutures;

                            if (historyOrder.State == OrderStateType.Done)
                            {
                                historyOrder.TimeDone = historyOrder.TimeCallBack;
                                historyOrder.State = OrderStateType.Done;
                            }
                            else if (historyOrder.State == OrderStateType.Cancel)
                            {
                                historyOrder.TimeCancel = historyOrder.TimeCallBack;
                                historyOrder.State = OrderStateType.Cancel;
                            }

                            orders.Add(historyOrder);
                            SaveOrdersToFile(orders, "I:\\XT_Debug\\history_orders.txt");
                        }

                        return orders;
                    }

                    return new List<Order>();
                }
                catch (Exception exception)
                {
                    return null;
                }
            }

            public List<Order> GetAllOpenOrders()
            {
                List<Order> orders = new List<Order>();

                try
                {
                    IRestResponse responseMessage = CreatePrivateQuery("/future/trade/v1/order/list-open-order", Method.POST);
                    var res = responseMessage.Content;

                    SendLogMessage($"[DEBUG list-open-order]{res}", LogMessageType.Error);

                    XTFuturesResponseRest<List<XTFuturesOrderItem>> stateResponse =
                        JsonConvert.DeserializeObject<XTFuturesResponseRest<List<XTFuturesOrderItem>>>(responseMessage.Content);

                    if (stateResponse == null)
                    {
                        SendLogMessage(" GetAllActiveOrders: deserialization returned null", LogMessageType.Error);
                        return null;
                    }

                    if (stateResponse.returnCode == "0" && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {
                        List<XTFuturesOrderItem> src = stateResponse.result;

                        List<Order> listOrders = new List<Order>(src.Count);

                        for (int i = 0; i < src.Count; i++)
                        {
                            XTFuturesOrderItem item = src[i];

                            Order activeOrder = new Order();

                            if (item.clientOrderId == null || item.clientOrderId == "0")
                            {
                                activeOrder.NumberUser = GetUserOrderNumber(item.orderId);
                            }
                            else
                            {
                                activeOrder.NumberUser = Convert.ToInt32(item.clientOrderId);
                            }

                            activeOrder.NumberMarket = item.orderId;
                            activeOrder.SecurityNameCode = item.symbol;
                            activeOrder.ServerType = ServerType.XTFutures;
                            activeOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createdTime));
                            activeOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.updatedTime));

                            //if (!string.IsNullOrEmpty(item.updatedTime))
                            //{
                            //    activeOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.updatedTime));
                            //}
                            //else
                            //{
                            //    activeOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createdTime));
                            //}

                            activeOrder.Volume = item.origQty.ToDecimal();
                            activeOrder.Price = item.price.ToDecimal();
                            activeOrder.Side = item.orderSide.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;
                            activeOrder.State = GetOrderState(item.state);
                            activeOrder.SecurityClassCode = GetNameClass(item.symbol);
                            activeOrder.TypeOrder = MapOrderType(item.orderType);
                            SendLogMessage($" [ activeOrder.TypeOrder] {activeOrder.TypeOrder}, numberUser={activeOrder.NumberUser}, numberOrder={activeOrder.NumberMarket}", LogMessageType.Error);
                            activeOrder.PortfolioNumber = _portfolioName;

                            listOrders.Add(activeOrder);
                            SaveOrdersToFile(listOrders, "I:\\XT_Debug\\active_orders.txt");
                        }

                        return listOrders;
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(" GetAllActiveOrders error: " + exception, LogMessageType.Error);
                    return null;
                }

                return new List<Order>();
            }
            //          public List<Order> GetAllOpenOrders()
            //          {
            //              List<Order> orders = new List<Order>();
            //              string query = "size=100";

            //              try
            //              {
            //                  IRestResponse responseMessage = CreatePrivateQuery("/future/trade/v1/order/list", Method.GET, query);

            //                  XTFuturesResponseRest<XTFuturesOrderResult> stateResponse =
            //                      JsonConvert.DeserializeObject<XTFuturesResponseRest<XTFuturesOrderResult>>(responseMessage.Content);

            //                  if (stateResponse == null)
            //                  {
            //                      SendLogMessage(" GetAllActiveOrders: deserialization returned null", LogMessageType.Error);
            //                      return null;
            //                  }

            //                  if (stateResponse.returnCode == "0" && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
            //                  {
            //                      List<XTFuturesOrderItem> src = stateResponse.result.items;

            //                      List<Order> listOrders = new List<Order>(src.Count);

            //                      for (int i = 0; i < src.Count; i++)
            //                      {
            //                          XTFuturesOrderItem item = src[i];

            //                          Order activeOrder = new Order();

            //                          if (item.clientOrderId == null)
            //                          {
            //                              int mappedUser = 0;

            //                              if (_marketOrderIdToClientId.TryGetValue(item.orderId, out mappedUser))
            //                              {
            //                                  activeOrder.NumberUser = mappedUser;
            //                              }
            //                          }
            //                          else
            //                          {
            //                              activeOrder.NumberUser = Convert.ToInt32(item.clientOrderId);
            //                          }

            //                          activeOrder.NumberMarket = item.orderId;
            //                          activeOrder.SecurityNameCode = item.symbol;
            //                          activeOrder.ServerType = ServerType.XTFutures;
            //                          activeOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createdTime));

            //                          if (!string.IsNullOrEmpty(item.updatedTime))
            //                          {
            //                              activeOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.updatedTime));
            //                          }
            //                          else
            //                          {
            //                              activeOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createdTime));
            //                          }

            //                          activeOrder.Volume = item.origQty.ToDecimal();
            //                          activeOrder.Price = item.price.ToDecimal();
            //                          activeOrder.Side = item.orderSide.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;
            //                          activeOrder.State = GetOrderState(item.state);
            //                          activeOrder.SecurityClassCode = GetNameClass(item.symbol);
            //                          activeOrder.TypeOrder = item.orderType.Equals("LIMIT", StringComparison.OrdinalIgnoreCase) ? OrderPriceType.Limit : OrderPriceType.Market;
            //                          activeOrder.PortfolioNumber = _portfolioName;

            //                          listOrders.Add(activeOrder);
            //                          SaveOrdersToFile(listOrders, "I:\\XT_Debug\\active_orders.txt");
            //                          SendLogMessage(
            //$"[XTFutures] activeOrder raw: OrderId='{item.orderId}', updated='{item.updatedTime}', " +
            //$"parsed: TimeCreate={activeOrder.TimeCreate} , TimeCallBack= {activeOrder.TimeCallBack}, " +
            //$"state={activeOrder.State} , id= {activeOrder.NumberMarket}",
            //LogMessageType.Trade);
            //                          MyOrderEvent?.Invoke(activeOrder);
            //                      }

            //                      return listOrders;
            //                  }
            //              }
            //              catch (Exception exception)
            //              {
            //                  SendLogMessage(" GetAllActiveOrders error: " + exception, LogMessageType.Error);
            //                  return null;
            //              }

            //              return new List<Order>();
            //          }

            public List<Order> GetActiveOrders(int startIndex, int count)
            {
                if (startIndex < 0)
                {
                    startIndex = 0;
                }

                if (count <= 0)
                {
                    count = 100;
                }

                List<Order> orders = GetAllOpenOrders();

                if (orders == null)
                {
                    orders = new List<Order>();
                }

                if (startIndex >= orders.Count)
                {
                    return new List<Order>();
                }

                int take = Math.Min(count, orders.Count - startIndex);

                return orders.GetRange(startIndex, take);
            }

            public List<Order> GetHistoricalOrders(int startIndex, int count)
            {
                if (startIndex < 0)
                {
                    startIndex = 0;
                }

                if (count <= 0)
                {
                    count = 100;
                }

                List<Order> result = GetOrderHistory();

                if (startIndex >= result.Count)
                {
                    return new List<Order>();
                }

                int take = Math.Min(count, result.Count - startIndex);

                return result.GetRange(startIndex, take);
            }

            public OrderStateType GetOrderStatus(Order order)
            {
                _rateGateSendOrder.WaitToProceed();

                try
                {
                    if (order == null)
                    {
                        SendLogMessage("GetOrderStatus > Order is null", LogMessageType.Error);
                        return OrderStateType.None;
                    }

                    //if (order.NumberUser == 0)
                    //{
                    //    order.NumberUser = GetMarketOrderId(order.NumberMarket);
                    //}


                    if (string.IsNullOrEmpty(order.NumberMarket))
                    {
                        order.NumberMarket = GetMarketOrderId(order.NumberUser);
                    }

                    Order orderOnMarket = null;

                    List<Order> ordersActive = GetActiveOrders(0, 100);

                    List<Order> ordersHistory = GetHistoricalOrders(0, 100);

                    if (!string.IsNullOrEmpty(order.NumberMarket))
                    {
                        if (ordersActive != null)
                        {
                            for (int i = 0; i < ordersActive.Count; i++)
                            {
                                if (ordersActive[i].NumberMarket == order.NumberMarket)
                                {
                                    orderOnMarket = ordersActive[i];
                                    break;
                                }
                            }
                        }

                        if (orderOnMarket == null && ordersHistory != null)
                        {
                            for (int i = 0; i < ordersHistory.Count; i++)
                            {
                                if (ordersHistory[i].NumberMarket == order.NumberMarket)
                                {
                                    orderOnMarket = ordersHistory[i];
                                    break;
                                }
                            }
                        }
                    }
                    if (order.NumberUser != 0)
                    {
                        if (ordersActive != null)
                        {
                            for (int i = 0; i < ordersActive.Count; i++)
                            {
                                if (ordersActive[i].NumberUser == order.NumberUser)
                                {
                                    orderOnMarket = ordersActive[i];
                                    break;
                                }
                            }
                        }

                        if (orderOnMarket == null && ordersHistory != null)
                        {
                            for (int i = 0; i < ordersHistory.Count; i++)
                            {
                                if (ordersHistory[i].NumberUser == order.NumberUser)
                                {
                                    orderOnMarket = ordersHistory[i];
                                    break;
                                }
                            }
                        }
                    }
                    if (orderOnMarket == null)
                    {
                        return OrderStateType.None;
                    }

                    MyOrderEvent?.Invoke(orderOnMarket);

                    if (orderOnMarket.State == OrderStateType.Done ||
                         orderOnMarket.State == OrderStateType.Partial)
                    {
                        CreateQueryMyTrade(orderOnMarket.NumberMarket);
                    }

                    return orderOnMarket.State;
                }
                catch (Exception exception)
                {
                    SendLogMessage($" GetOrderStatus> exception: {exception.Message}", LogMessageType.Error);
                    return OrderStateType.None;
                }
            }

            private readonly RateGate _rateGateGetMyTradeState = new RateGate(1, TimeSpan.FromMilliseconds(100));

            private void CreateQueryMyTrade(string orderId)
            {
                _rateGateGetMyTradeState.WaitToProceed();

                List<MyTrade> list = new List<MyTrade>();

                try
                {
                    string orderIdStr = orderId.ToString(CultureInfo.InvariantCulture);

                    IRestResponse response = CreatePrivateQuery("/future/trade/v1/order/trade-list", Method.GET, query: "orderId=" + orderIdStr);

                    XTFuturesResponseRest<XTFuturesTradeHistoryResult> stateResponse =
                    JsonConvert.DeserializeObject<XTFuturesResponseRest<XTFuturesTradeHistoryResult>>(response.Content);

                    if (stateResponse == null)
                    {
                        return;
                    }

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        if (stateResponse.returnCode.Equals("0") && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                        {
                            for (int i = 0; i < stateResponse.result.items.Count; i++)
                            {
                                XTFuturesTradeHistory data = stateResponse.result.items[i];

                                if (data == null)
                                {
                                    continue;
                                }

                                MyTrade trade = new MyTrade();

                                trade.NumberOrderParent = data.orderId;
                                trade.NumberTrade = data.execId;
                                trade.SecurityNameCode = data.symbol;
                                trade.Price = data.price.ToDecimal();
                                trade.Volume = data.quantity.ToDecimal();
                                trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(data.timestamp));
                                trade.Side = string.Equals(data.orderSide, "BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;

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
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage("CreateQueryMyTrade error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            private OrderPriceType MapOrderType(string fromExchange)
            {
                if (string.IsNullOrEmpty(fromExchange))
                {
                    return OrderPriceType.Limit;
                }

                if (string.Equals(fromExchange, "MARKET", StringComparison.OrdinalIgnoreCase))
                {
                    return OrderPriceType.Market;
                }

                if (fromExchange.StartsWith("LIMIT", StringComparison.OrdinalIgnoreCase))
                {
                    return OrderPriceType.Limit;
                }

                return OrderPriceType.Limit;
            }

            #endregion

            #region 12 Queries

            private readonly RateGate _rateGateListenKey = new RateGate(1, TimeSpan.FromMilliseconds(10000));

            private string GetListenKey()
            {
                _rateGateListenKey.WaitToProceed();

                string listenKey = "";

                try
                {
                    IRestResponse responseMessage = CreatePrivateQuery("/future/user/v1/user/listen-key", Method.GET);

                    XTFuturesResponseRest<string> stateResponse = JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(responseMessage.Content);

                    if (stateResponse == null)
                    {
                        return null;
                    }

                    if (responseMessage.StatusCode == HttpStatusCode.OK && stateResponse != null)
                    {
                        if (stateResponse.returnCode.Equals("0") && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                        {
                            SendLogMessage($" ListenKey successfully received.", LogMessageType.Connect);
                            listenKey = stateResponse?.result;
                        }
                        else
                        {
                            SendLogMessage($" GetListenKey error, Code: {stateResponse.returnCode}\n"
                                           + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage($" GetListenKey>  State Code: {responseMessage.StatusCode}", LogMessageType.Error);

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

            IRestResponse CreatePublicQuery(string path, Method method, string parameters = "")
            {
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                if (method == Method.GET && !string.IsNullOrEmpty(parameters))
                {
                    path += "?" + parameters;
                }

                RestRequest request = new RestRequest(path, method);
                request.AddHeader("Accept", "application/json");

                IRestResponse response = client.Execute(request);
                return response;
            }

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
                        NullValueHandling = NullValueHandling.Ignore,
                        Culture = CultureInfo.InvariantCulture
                    };
                    if (method == Method.POST && body != null)
                    {
                        bodyString = body is string s ? s : JsonConvert.SerializeObject(body, Formatting.None);
                    }

                    string ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                    string X = $"validate-appkey={_publicKey}&validate-timestamp={ts}";

                    StringBuilder yb = new StringBuilder().Append('#').Append(path);
                    if (!string.IsNullOrEmpty(query)) yb.Append('#').Append(query);
                    if (method == Method.POST && bodyString.Length > 0)
                        yb.Append('#').Append(bodyString);

                    string signInput = X + yb;
                    string signature = HmacSHA256(_secretKey, signInput);

                    RestClient client = new RestClient(_baseUrl);
                    RestRequest request = new RestRequest(resource, method);

                    if (_myProxy != null)
                    {
                        client.Proxy = _myProxy;
                    }

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
                catch (Exception exception)
                {
                    SendLogMessage(" SendPrivate error: " + exception, LogMessageType.Error);
                    return null;
                }
            }

            private void CreateOrderFail(Order order)
            {
                order.State = OrderStateType.Fail;
                MyOrderEvent?.Invoke(order);
            }

            private Dictionary<string, int> _numberUser = new Dictionary<string, int>();

            private void LoadOrderTrackers()
            {
                try
                {
                    string engineDir = GetEngineDirectory();

                    string marketToUserPath = Path.Combine(engineDir, "numberUser.json");
                    string orderTrackerPath = Path.Combine(engineDir, "orderId.json");

                    if (File.Exists(marketToUserPath))
                    {
                        string marketToUserJson = File.ReadAllText(marketToUserPath);
                        _numberUser = JsonConvert.DeserializeObject<Dictionary<string, int>>(marketToUserJson);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage($"Exception in LoadOrderTrackers: {exception}", LogMessageType.Error);
                }
            }
            // private Dictionary<int, string> _orderTrackerDict = new Dictionary<int, string>();
            private Dictionary<string, int> _orderTrackerDict = new Dictionary<string, int>();

            private int GetMarketOrderId(string orderId)
            {
                if (_orderTrackerDict.Count == 0 || _orderTrackerDict.Count == 0)
                {
                    LoadOrderTrackers();
                }

                if (_orderTrackerDict.ContainsKey(orderId))
                {
                    return _orderTrackerDict[orderId];
                }

                return 0;
            }

            private void SaveOrderTrackers()
            {
                try
                {
                    if (_numberUser == null)
                    {
                        return;
                    }

                    string engineDir = GetEngineDirectory();

                    string marketToUserJson = JsonConvert.SerializeObject(_numberUser, Formatting.Indented);
                    File.WriteAllText(Path.Combine(engineDir, "numberUser.json"), marketToUserJson);
                }
                catch (Exception exception)
                {
                    SendLogMessage($"Exception in SaveOrderTrackers: {exception}", LogMessageType.Error);
                }
            }

            private int GetUserOrderNumber(string marketOrderId)
            {
                if (_numberUser == null || _numberUser.Count == 0)
                {
                    LoadOrderTrackers();
                }

                if (_numberUser.ContainsKey(marketOrderId))
                {
                    return _numberUser[marketOrderId];
                }

                return 0;
            }
            private string GetMarketOrderId(int numberUser)
            {
                return _numberUser.FirstOrDefault(x => x.Value == numberUser).Key;
            }

            private string GetEngineDirectory()
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                DirectoryInfo dir = new DirectoryInfo(baseDir);
                while (dir != null)
                {
                    string enginePath = Path.Combine(dir.FullName, "Engine");
                    if (Directory.Exists(enginePath))
                    {
                        return enginePath;
                    }

                    dir = dir.Parent;
                }

                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Engine");
            }

            #endregion

            #region 13 Log

            public event Action<string, LogMessageType> LogMessageEvent;

            public event Action<Order> MyOrderEvent;

            public event Action<Funding> FundingUpdateEvent { add { } remove { } }

            public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

            public bool SubscribeNews()
            {
                return false;
            }

            public event Action<News> NewsEvent { add { } remove { } }

            public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

            private void SendLogMessage(string message, LogMessageType messageType)
            {
                LogMessageEvent?.Invoke(message, messageType);
            }

            #endregion
        }
    }
}
