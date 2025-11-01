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
using OsEngine.Market.Servers.XT.XTFutures.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;


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

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
        }


        public class XTServerSpotRealization : IServerRealization
        {
            #region 1 Constructor, Status, Connection

            public XTServerSpotRealization()
            {
                ServerStatus = ServerConnectStatus.Disconnect;

                Thread threadCheckAlive = new Thread(ConnectionCheck);
                threadCheckAlive.IsBackground = true;
                threadCheckAlive.Name = "CheckAliveXTFutures";
                threadCheckAlive.Start();

                Thread threadForPublicMessages = new Thread(PublicMessageReader);
                threadForPublicMessages.IsBackground = true;
                threadForPublicMessages.Name = "PublicMessageReaderXTFutures";
                threadForPublicMessages.Start();

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
                                SendLogMessage("Check the Public and Private Key!", LogMessageType.Error);
                                ServerStatus = ServerConnectStatus.Disconnect;

                                DisconnectEvent?.Invoke();
                                return;
                            }

                            CreatePublicWebSocketConnect();
                            CreatePrivateWebSocketConnect();
                        }
                        catch (Exception exception)
                        {
                            SendLogMessage(exception.ToString(), LogMessageType.Error);
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
                    UnsubscribeFromAllWebSockets();
                    _subscribedSecurities.Clear();
                    DeleteWebSocketConnection();
                    _allDepths.Clear();
                }
                catch (Exception exception)
                {
                    SendLogMessage("Dispose error" + exception.ToString(), LogMessageType.Error);
                }

                FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
                FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

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

            private string _baseUrl = "https://fapi.xt.com";

            #endregion

            #region 3 Securities

            private List<Security> _listSecurities;

            private RateGate _rateGateSecurity = new RateGate(1, TimeSpan.FromMilliseconds(200));

            public void GetSecurities()
            {
                if (_listSecurities == null)
                {
                    _listSecurities = new List<Security>();
                }

                _rateGateSecurity.WaitToProceed();

                try
                {
                    IRestResponse response = CreatePublicQuery("/future/market/v3/public/symbol/list", Method.GET, "");

                    if (response.StatusCode == HttpStatusCode.OK && response != null)
                    {
                        XTFuturesResponseRest<XTFuturesSymbolListResult> securityList =
                            JsonConvert.DeserializeObject<XTFuturesResponseRest<XTFuturesSymbolListResult>>(response.Content);

                        if (securityList.returnCode.Equals("0") && securityList.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                        {
                            SendLogMessage("Securities loaded. Count: " + securityList.result.symbols.Count, LogMessageType.System);

                            for (int i = 0; i < securityList.result.symbols.Count; i++)
                            {
                                XTFuturesSymbol symbols = securityList.result.symbols[i];

                                Security newSecurity = new Security();

                                newSecurity.Exchange = ServerType.XTFutures.ToString();
                                newSecurity.Name = symbols.symbol;
                                newSecurity.NameFull = symbols.symbol;
                                newSecurity.NameClass = symbols.quoteCoin;
                                newSecurity.NameId = symbols.id;
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
                                newSecurity.PriceStep = symbols.minStepPrice.ToDecimal();
                                newSecurity.Decimals = Convert.ToInt32(symbols.pricePrecision);
                                newSecurity.PriceStepCost = newSecurity.PriceStep;
                                newSecurity.DecimalsVolume = symbols.contractSize.DecimalsCount();
                                newSecurity.MinTradeAmount = symbols.contractSize.ToDecimal();
                                newSecurity.MinTradeAmountType = MinTradeAmountType.Contract;
                                newSecurity.VolumeStep = symbols.contractSize.ToDecimal();

                                _listSecurities.Add(newSecurity);
                            }

                            SecurityEvent?.Invoke(_listSecurities);
                        }
                        else
                        {
                            SendLogMessage($"GetSecurities return code: {securityList.returnCode}\n"
                                           + $"Message Code: {securityList.msgInfo}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Securities error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
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

            private string _portfolioName = "XTFuturesPortfolio";

            private List<Portfolio> _portfolios;

            private RateGate _rateGatePortfolio = new RateGate(1, TimeSpan.FromMilliseconds(20));

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
                    catch (Exception exception)
                    {
                        SendLogMessage(exception.ToString(), LogMessageType.Error);
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
                        SendLogMessage($"CreateQueryPortfolio: {response.StatusCode} || {response.Content}", LogMessageType.Error);
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
                            $"CreateQueryPortfolio error, Code: {stateResponse.rc} Message code: {stateResponse.mc}",
                            LogMessageType.Error);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage("CreateQueryPortfolio error: " + exception, LogMessageType.Error);
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

                    if (isUpdateValueBegin)
                    {
                        portfolio.ValueBegin = Math.Round(coin.totalAmount.ToDecimal(), 5);
                    }

                    portfolio.ValueCurrent = Math.Round(coin.marginBalance.ToDecimal(), 5);
                    portfolio.UnrealizedPnl = Math.Round(coin.notProfit.ToDecimal(), 5);
                    portfolio.ValueBlocked = Math.Round(coin.openOrderMarginFrozen.ToDecimal(), 5);

                    PositionOnBoard moneyPos = new PositionOnBoard();

                    moneyPos.PortfolioName = portfolio.Number;
                    moneyPos.SecurityNameCode = coin.coin;
                    moneyPos.ValueCurrent = Math.Round(coin.marginBalance.ToDecimal(), 5);
                    moneyPos.ValueBlocked = Math.Round(coin.openOrderMarginFrozen.ToDecimal(), 5);
                    moneyPos.ValueBegin = Math.Round(coin.walletBalance.ToDecimal(), 5);
                    moneyPos.UnrealizedPnl = Math.Round(coin.notProfit.ToDecimal(), 5);

                    portfolio.SetNewPosition(moneyPos);

                    if (portfolio.ValueCurrent == 0)
                    {
                        portfolio.ValueCurrent = 1;
                    }

                    PortfolioEvent?.Invoke(_portfolios);
                }
                catch (Exception exception)
                {
                    SendLogMessage("UpdatePortfolioRest error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            public void CreateQueryPositions(bool updateValueBegin)
            {
                _rateGateForAll.WaitToProceed();

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
                        SendLogMessage($"CreateQueryPositions: {response.StatusCode} || {response.Content}", LogMessageType.Error);
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

                    for (int i = 0; i < state.result.Count; i++)
                    {
                        XTFuturesPosition pos = state.result[i];
                        if (pos == null || string.IsNullOrWhiteSpace(pos.symbol) || string.IsNullOrWhiteSpace(pos.positionSide))
                        {
                            continue;
                        }

                        string side = pos.positionSide;
                        decimal size = pos.positionSize.ToDecimal() * GetVolume(pos.symbol);
                        size = side == "SHORT" ? -Math.Abs(size) : size;

                        PositionOnBoard position = new PositionOnBoard();
                        position.PortfolioName = portfolio.Number;
                        position.SecurityNameCode = pos.symbol + "_" + side;
                        position.ValueCurrent = Math.Round(size, 6);
                        position.UnrealizedPnl = Math.Round(pos.floatingPL.ToDecimal(), 6);
                        position.ValueBegin = updateValueBegin ? position.ValueCurrent : 0m;

                        portfolio.SetNewPosition(position);
                    }

                    PortfolioEvent?.Invoke(_portfolios);
                }
                catch (Exception ex)
                {
                    SendLogMessage("CreateQueryPositions error: " + ex, LogMessageType.Error);
                }
            }

            public event Action<List<Portfolio>> PortfolioEvent;

            #endregion

            #region 5 Data

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
                            SendLogMessage("CreateQueryCandles: Response is null", LogMessageType.Error);
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
                        else
                        {
                            SendLogMessage($"CreateQueryCandles error, Code: {symbols.returnCode}\n"
                                           + $"Message code: {symbols.msgInfo}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage($"CreateQueryCandles error, State Code: {responseMessage.StatusCode} || {responseMessage.Content}", LogMessageType.Error);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage("CreateQueryCandles error: " + exception.ToString(), LogMessageType.Error);
                }

                return null;
            }

            public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
            {
                return null;
            }

            #endregion

            #region 6 WebSocket creation

            private List<WebSocket> _webSocketPublic = new List<WebSocket>();

            private WebSocket _webSocketPrivate;

            private readonly string _webSocketPrivateUrl = "wss://fstream.xt.com/ws/user";

            private readonly string _webSocketPublicUrl = "wss://fstream.xt.com/ws/market";

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

                    if (_myProxy != null)
                    {
                        webSocketPublicNew.SetProxy(_myProxy);
                    }

                    webSocketPublicNew.EmitOnPing = true;
                    webSocketPublicNew.OnOpen += WebSocketPublicNew_OnOpen;
                    webSocketPublicNew.OnMessage += WebSocketPublicNew_OnMessage;
                    webSocketPublicNew.OnError += WebSocketPublicNew_OnError;
                    webSocketPublicNew.OnClose += WebSocketPublicNew_OnClose;
                    webSocketPublicNew.ConnectAsync();

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

                    _webSocketPrivate = new WebSocket(_webSocketPrivateUrl);

                    if (_myProxy != null)
                    {
                        _webSocketPrivate.SetProxy(_myProxy);
                    }

                    _webSocketPrivate.EmitOnPing = true;
                    _webSocketPrivate.OnOpen += _webSocketPrivate_OnOpen;
                    _webSocketPrivate.OnClose += _webSocketPrivate_OnClose;
                    _webSocketPrivate.OnMessage += _webSocketPrivate_OnMessage;
                    _webSocketPrivate.OnError += _webSocketPrivate_OnError;
                    _webSocketPrivate.ConnectAsync();
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

            private void WebSocketPublicNew_OnClose(object sender, CloseEventArgs e)
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

            private void WebSocketPublicNew_OnError(object sender, OsEngine.Entity.WebSocketOsEngine.ErrorEventArgs e)
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
                    SendLogMessage("Data socket error" + exception.ToString(), LogMessageType.Error);
                }
            }

            private void WebSocketPublicNew_OnMessage(object sender, MessageEventArgs e)
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

                    if (FIFOListWebSocketPublicMessage == null)
                    {
                        return;
                    }

                    FIFOListWebSocketPublicMessage.Enqueue(e.Data);
                }
                catch (Exception error)
                {
                    SendLogMessage("WebSocketPublic Message Received error: " + error.ToString(), LogMessageType.Error);
                }
            }

            private void WebSocketPublicNew_OnOpen(object sender, EventArgs e)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        CheckSocketsActivate();
                        SendLogMessage("XTFutures WebSocket Public connection open", LogMessageType.System);
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }

            private void _webSocketPrivate_OnError(object sender, OsEngine.Entity.WebSocketOsEngine.ErrorEventArgs e)
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
                catch (Exception exception)
                {
                    SendLogMessage("Data socket error" + exception.ToString(), LogMessageType.Error);
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
                catch (Exception exception)
                {
                    SendLogMessage("WebSocket Private Message Received error: " + exception.ToString(), LogMessageType.Error);
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
                    CheckSocketsActivate();
                    SendLogMessage("XTFutures WebSocket Private connection open", LogMessageType.System);

                    _webSocketPrivate.SendAsync($"{{\"method\":\"SUBSCRIBE\",\"params\":[\"order@{_listenKey}\",\"trade@{_listenKey}\",\"position@{_listenKey}\",\"balance@{_listenKey}\",\"notify@{_listenKey}\"],\"id\":\"3214\"}}");
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
                    Thread.Sleep(20000);

                    try
                    {
                        if (ServerStatus == ServerConnectStatus.Disconnect)
                        {
                            Thread.Sleep(2000);
                            continue;
                        }

                        for (int i = 0; i < _webSocketPublic.Count; i++)
                        {
                            WebSocket webSocketPublic = _webSocketPublic[i];

                            if (webSocketPublic != null
                                && webSocketPublic?.ReadyState == WebSocketState.Open)
                            {
                                webSocketPublic.SendAsync("ping");
                            }
                            else
                            {
                                Disconnect();
                            }
                        }

                        if (_webSocketPrivate != null &&
                            (_webSocketPrivate.ReadyState == WebSocketState.Open ||
                            _webSocketPrivate.ReadyState == WebSocketState.Connecting))
                        {
                            _webSocketPrivate.SendAsync("ping");
                        }
                        else
                        {
                            Disconnect();
                        }
                    }
                    catch (Exception exception)
                    {
                        SendLogMessage(exception.ToString(), LogMessageType.Error);
                    }
                }
            }

            #endregion

            #region 9 Security Subscribed

            private RateGate _rateGateSecuritySubscribed = new RateGate(1, TimeSpan.FromMilliseconds(200));

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

                    for (int i = 0; i < _subscribedSecurities.Count; i++)
                    {
                        if (_subscribedSecurities[i].Equals(security.Name, StringComparison.OrdinalIgnoreCase))
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
                        webSocketPublic.SendAsync($"{{\"method\":\"SUBSCRIBE\",\"params\":[\"depth_update@{security.Name}\", \"depth@{security.Name},20,100ms\"],\"id\":\"1126\"}}");
                        webSocketPublic.SendAsync($"{{\"method\":\"SUBSCRIBE\",\"params\":[\"trade@{security.Name}\"],\"id\":\"1127\"}}");
                    }
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

                                            webSocketPublic.SendAsync($"{{\"method\": \"UNSUBSCRIBE\", \"params\": [\"depth_update@{securityName}\",\"depth@{securityName},{20}\"], \"id\": \"1252\"}}");
                                            webSocketPublic.SendAsync($"{{\"method\": \"UNSUBSCRIBE\", \"params\": [\"trades@{securityName}\"], \"id\": \"1253\"}}");
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
                        _webSocketPrivate.SendAsync($"{{\"method\":\"UNSUBSCRIBE\",\"params\":[\"order@{_listenKey}\",\"trade@{_listenKey}\",\"position@{_listenKey}\",\"balance@{_listenKey}\",\"notify@{_listenKey}\"],\"id\":\"{1254}\"}}");
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

            public event Action<News> NewsEvent { add { } remove { } }

            #endregion

            #region 10 WebSocket parsing the messages

            private ConcurrentQueue<string> FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

            private ConcurrentQueue<string> FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

            private void PublicMessageReader()
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
                                continue;
                            }
                            else if (action.topic.Equals("depth", StringComparison.OrdinalIgnoreCase))
                            {
                                SnapshotDepth(message);
                                continue;
                            }
                            else if (action.topic.Equals("trade", StringComparison.OrdinalIgnoreCase))
                            {
                                UpdateTrade(message);
                                continue;
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

                        if (action == null || action.topic == null)
                        {
                            continue;
                        }

                        if (action.topic.Equals("order"))
                        {
                            UpdateOrder(message);
                            continue;
                        }

                        if (action.topic.Equals("balance"))
                        {
                            UpdatePortfolio(message);
                            continue;
                        }

                        if (action.topic.Equals("trade"))
                        {
                            //UpdateMyTrade(message);
                            continue;
                        }

                        if (action.topic.Equals("position"))
                        {
                            UpdatePosition(message);
                            continue;
                        }
                    }
                    catch (Exception exception)
                    {
                        Thread.Sleep(2000);
                        SendLogMessage("PrivateMessageReader error: " + exception.ToString(), LogMessageType.Error);
                    }
                }
            }

            private List<MarketDepth> _allDepths = new List<MarketDepth>();

            private bool _snapshotInitialized = false;

            private long _lastSeqNum = -1;

            private DateTime _lastTimeMd = DateTime.MinValue;

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

                    _lastSeqNum = Convert.ToInt64(depth.id);
                    _snapshotInitialized = true;

                    MarketDepth newDepth = new MarketDepth();

                    newDepth.SecurityNameCode = depth.s;
                    newDepth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(depth.t));

                    for (int i = 0; i < depth.b.Count && i < 25; i++)
                    {
                        List<string> level = depth.b[i];

                        newDepth.Bids.Add(new MarketDepthLevel
                        {
                            Price = level[0].ToDouble(),
                            Bid = level[1].ToDouble()
                        });
                    }

                    for (int i = 0; i < depth.a.Count && i < 25; i++)
                    {
                        List<string> level = depth.a[i];

                        newDepth.Asks.Add(new MarketDepthLevel
                        {
                            Price = level[0].ToDouble(),
                            Ask = level[1].ToDouble()
                        });
                    }

                    if (newDepth.Time <= _lastTimeMd)
                    {
                        _lastTimeMd = _lastTimeMd.AddTicks(1);
                        newDepth.Time = _lastTimeMd;
                    }
                    else
                    {
                        _lastTimeMd = newDepth.Time;
                    }

                    _allDepths.RemoveAll(d => d.SecurityNameCode == newDepth.SecurityNameCode);

                    _allDepths.Add(newDepth);

                    if (newDepth.Bids.Count == 0
                        || newDepth.Asks.Count == 0)
                    {
                        return;
                    }

                    MarketDepthEvent?.Invoke(newDepth.GetCopy());


                }
                catch (Exception exception)
                {
                    SendLogMessage("SnapshotDepth error: " + exception, LogMessageType.Error);
                }
            }

            private void UpdateDepth(string message)
            {
                try
                {
                    XTFuturesResponseWebSocket<XTFuturesUpdateDepth> resp = JsonConvert.DeserializeObject
                       <XTFuturesResponseWebSocket<XTFuturesUpdateDepth>>(message);

                    XTFuturesUpdateDepth marketDepth = resp?.data;

                    MarketDepth depth = _allDepths.Find(d => d.SecurityNameCode == marketDepth.s);

                    if (depth == null)
                    {
                        return;
                    }
                    if (marketDepth.s != depth.SecurityNameCode)
                    {
                        return;
                    }

                    if (!_snapshotInitialized)
                    {
                        return;
                    }

                    if (_lastSeqNum != -1 && Convert.ToInt64(marketDepth.fu) != _lastSeqNum + 1)
                    {
                        _snapshotInitialized = false;
                        _lastSeqNum = -1;
                        return;
                    }

                    _lastSeqNum = Convert.ToInt64(marketDepth.fu);

                    depth.Time = DateTime.UtcNow;

                    if (depth.Time <= _lastTimeMd)
                    {
                        _lastTimeMd = _lastTimeMd.AddTicks(1);
                        depth.Time = _lastTimeMd;
                    }
                    else
                    {
                        _lastTimeMd = depth.Time;
                    }

                    _lastTimeMd = depth.Time;

                    ApplyLevels(marketDepth.b, depth.Bids, isBid: true);
                    ApplyLevels(marketDepth.a, depth.Asks, isBid: false);

                    depth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(marketDepth.t));

                    List<MarketDepthLevel> topBids = new List<MarketDepthLevel>();

                    for (int i = 0; i < depth.Bids.Count && i < 25; i++)
                    {
                        topBids.Add(depth.Bids[i]);
                    }

                    depth.Bids = topBids;

                    List<MarketDepthLevel> topAsks = new List<MarketDepthLevel>();

                    for (int i = 0; i < depth.Asks.Count && i < 25; i++)
                    {
                        topAsks.Add(depth.Asks[i]);
                    }

                    depth.Asks = topAsks;

                    if (depth.Bids.Count == 0
                        || depth.Asks.Count == 0)
                    {
                        return;
                    }

                    MarketDepthEvent?.Invoke(depth.GetCopy());

                }
                catch (Exception exception)
                {
                    SendLogMessage("UpdateDepth error: " + exception, LogMessageType.Error);
                }
            }

            private void ApplyLevels(List<List<string>> updates, List<MarketDepthLevel> levels, bool isBid)
            {
                for (int i = 0; i < updates.Count; i++)
                {
                    double price = updates[i][0].ToDouble();
                    double size = updates[i][1].ToDouble();

                    MarketDepthLevel existing = levels.Find(x => x.Price == price);

                    if (size == 0)
                    {
                        if (existing != null)
                        {
                            levels.Remove(existing);
                        }
                    }
                    else
                    {
                        if (existing != null)
                        {
                            if (isBid)
                            {
                                existing.Bid = size;
                            }
                            else
                            {
                                existing.Ask = size;
                            }
                        }
                        else
                        {
                            MarketDepthLevel level = new MarketDepthLevel { Price = price };

                            if (isBid)
                            {
                                level.Bid = size;
                            }
                            else
                            {
                                level.Ask = size;
                            }

                            levels.Add(level);
                        }
                    }
                }

                if (isBid)
                {
                    levels.Sort((a, b) => b.Price.CompareTo(a.Price));
                }
                else
                {
                    levels.Sort((a, b) => a.Price.CompareTo(b.Price));
                }
            }

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
                    SendLogMessage("UpdateTrade error: " + exception.ToString(), LogMessageType.Error);
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

                    string side = string.IsNullOrWhiteSpace(response.data.positionSide) ? "LONG" : response.data.positionSide.ToUpper();

                    decimal qty = response.data.positionSize.ToDecimal() * GetVolume(response.data.symbol);
                    qty = side == "SHORT" ? -Math.Abs(qty) : qty;

                    PositionOnBoard pos = new PositionOnBoard();

                    pos.PortfolioName = _portfolioName;
                    pos.SecurityNameCode = response.data.symbol + "_" + side;
                    pos.ValueCurrent = Math.Round(qty, 6);
                    pos.UnrealizedPnl = Math.Round(response.data.realizedProfit.ToDecimal(), 6);

                    portfolio.SetNewPosition(pos);

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
                    myTrade.NumberTrade = Convert.ToInt64(response.data.clientOrderId).ToString();
                    myTrade.Price = response.data.price.ToDecimal();
                    myTrade.SecurityNameCode = response.data.symbol;
                    myTrade.Volume = response.data.quantity.ToDecimal() * GetVolume(myTrade.SecurityNameCode);
                    myTrade.Side = response.data.orderSide == "BUY" ? Side.Buy : Side.Sell;

                    MyTradeEvent?.Invoke(myTrade);
                }
                catch (Exception exception)
                {
                    SendLogMessage("UpdateMyTrade error: " + exception.ToString(), LogMessageType.Error);
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

                    PositionOnBoard pos = new PositionOnBoard();
                    pos.PortfolioName = _portfolioName;
                    pos.SecurityNameCode = portfolios.data.coin;
                    pos.ValueBlocked = Math.Round(portfolios.data.openOrderMarginFrozen.ToDecimal(), 5);
                    pos.ValueCurrent = Math.Round(portfolios.data.availableBalance.ToDecimal(), 5);

                    portfolio.SetNewPosition(pos);

                    PortfolioEvent?.Invoke(_portfolios);
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
                    XTFuturesResponseWebSocket<XTFuturesUpdateOrder> order =
                        JsonConvert.DeserializeObject<XTFuturesResponseWebSocket<XTFuturesUpdateOrder>>(message);

                    if (order == null)
                    {
                        return;
                    }

                    Order updateOrder = new Order();

                    updateOrder.SecurityNameCode = order.data.symbol;
                    updateOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.data.createdTime));
                    updateOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.data.updatedTime));
                    updateOrder.NumberMarket = order.data.orderId;
                    updateOrder.NumberUser = Convert.ToInt32(order.data.clientOrderId);
                    updateOrder.Side = order.data.orderSide.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;
                    updateOrder.State = GetOrderState(order.data.state);
                    updateOrder.TypeOrder = MapOrderType(order.data.orderType);
                    updateOrder.ServerType = ServerType.XTFutures;
                    updateOrder.PortfolioNumber = _portfolioName;
                    updateOrder.Volume = order.data.origQty.ToDecimal() * GetVolume(updateOrder.SecurityNameCode);
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
                    }

                    MyOrderEvent?.Invoke(updateOrder);
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

            public event Action<Order> MyOrderEvent;

            public event Action<MyTrade> MyTradeEvent;

            public event Action<MarketDepth> MarketDepthEvent;

            public event Action<Trade> NewTradesEvent;

            public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

            public event Action<Funding> FundingUpdateEvent { add { } remove { } }

            public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

            #endregion

            #region 11 Trade

            private RateGate _rateGateForAll = new RateGate(1, TimeSpan.FromMilliseconds(10));

            public void SendOrder(Order order)
            {
                _rateGateForAll.WaitToProceed();

                try
                {
                    string positionSide = "";

                    if (order.PositionConditionType == OrderPositionConditionType.Close)
                    {
                        positionSide = order.Side == Side.Buy ? "SHORT" : "LONG";
                    }
                    else
                    {
                        positionSide = order.Side == Side.Buy ? "LONG" : "SHORT";
                    }

                    XTFuturesSendOrder data = new XTFuturesSendOrder();
                    data.symbol = order.SecurityNameCode;
                    data.clientOrderId = order.NumberUser.ToString();
                    data.orderSide = order.Side.ToString().ToUpper();

                    decimal volume = order.Volume / GetVolume(order.SecurityNameCode);
                    data.origQty = volume.ToString(CultureInfo.InvariantCulture);

                    data.orderType = order.TypeOrder.ToString().ToUpper();
                    data.positionSide = positionSide;
                    data.timeInForce = order.TypeOrder == OrderPriceType.Limit ? "GTC" : "FOC";
                    data.price = order.TypeOrder == OrderPriceType.Market ? null : order.Price.ToString(CultureInfo.InvariantCulture);
                    order.PortfolioNumber = _portfolioName;

                    IRestResponse responseMessage = CreatePrivateQuery("/future/trade/v1/order/create", Method.POST, null, data);

                    if (responseMessage == null)
                    {
                        SendLogMessage("SendOrder: response is null", LogMessageType.Error);
                        CreateOrderFail(order);
                        return;
                    }

                    if (responseMessage.StatusCode != HttpStatusCode.OK)
                    {
                        SendLogMessage($"SendOrder: HTTP error - {responseMessage.StatusCode} || {responseMessage.Content}", LogMessageType.Error);
                        CreateOrderFail(order);
                        return;
                    }

                    XTFuturesResponseRest<string> stateResponse =
                        JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(responseMessage.Content);

                    if (stateResponse.returnCode == "0"
                        && string.Equals(stateResponse.msgInfo, "SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {
                        //order.NumberMarket = stateResponse.result;

                        //if (string.Equals(data.orderType, "MARKET", StringComparison.OrdinalIgnoreCase))
                        //{
                        //    order.State = OrderStateType.Done;
                        //    MyOrderEvent?.Invoke(order);
                        //    CreateQueryMyTrade(order.NumberMarket);
                        //}
                        //else
                        //{
                        //    order.State = OrderStateType.Active;
                        //    order.TimeCallBack = DateTime.Now;
                        //    MyOrderEvent?.Invoke(order);
                        //}
                    }
                    else
                    {
                        SendLogMessage("SendOrder Fail: Code=" + stateResponse.returnCode
                            + ", Msg=" + stateResponse.msgInfo
                            + ", Error=" + stateResponse.error.code, LogMessageType.Error);

                        CreateOrderFail(order);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage("SendOrder error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            private decimal GetVolume(string securityName)
            {
                decimal minVolume = 1;

                for (int i = 0; i < _listSecurities.Count; i++)
                {
                    if (_listSecurities[i].Name == securityName)
                    {
                        minVolume = _listSecurities[i].MinTradeAmount;
                        break;
                    }
                }

                if (minVolume <= 0)
                {
                    return 1;
                }

                return minVolume;
            }

            private void CreateOrderFail(Order order)
            {
                order.State = OrderStateType.Fail;
                MyOrderEvent?.Invoke(order);
            }

            public void ChangeOrderPrice(Order order, decimal newPrice)
            {
                //_rateGateForAll.WaitToProceed();

                //try
                //{
                //    if (order.TypeOrder == OrderPriceType.Market || order.State == OrderStateType.Done)
                //    {
                //        SendLogMessage("ChangeOrderPrice> Can't change price for  Order Market", LogMessageType.Error);
                //        return;
                //    }

                //    var body = new
                //    {
                //        orderId = long.Parse(order.NumberMarket, CultureInfo.InvariantCulture),
                //        price = newPrice.ToString("0.#####", CultureInfo.InvariantCulture),
                //        origQty = order.Volume.ToString("0.#####", CultureInfo.InvariantCulture)
                //    };

                //    if (newPrice <= 0)
                //    {
                //        SendLogMessage($"ChangeOrderPrice> bad price: {newPrice}", LogMessageType.Error);
                //        return;
                //    }

                //    if (string.IsNullOrWhiteSpace(order.NumberMarket))
                //    {
                //        SendLogMessage($"ChangeOrderPrice> empty exchange order id {order.NumberMarket}", LogMessageType.Error);
                //        return;
                //    }

                //    IRestResponse response = CreatePrivateQuery("/future/trade/v1/order/update", Method.POST, null, body);

                //    if (order.State == OrderStateType.Cancel || order.State == OrderStateType.Done)
                //    {
                //        return;
                //    }

                //    XTFuturesResponseRest<string> stateResponse = JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(response.Content);

                //    if (response.StatusCode == HttpStatusCode.OK && stateResponse != null && stateResponse.returnCode == "0")
                //    {
                //        order.PortfolioNumber = _portfolioName;

                //        if (!string.IsNullOrEmpty(stateResponse.result))
                //        {
                //            order.Price = newPrice;

                //            SendLogMessage($" Success! Order {order.NumberMarket} updated." +
                //                $" New orderId={stateResponse.result}, price={newPrice}, qty={order.Volume}", LogMessageType.System);
                //        }
                //        else
                //        {
                //            SendLogMessage($" Update returned an empty result {response.Content}", LogMessageType.Error);
                //        }
                //    }
                //    else
                //    {
                //        SendLogMessage($" Error: returnCode={stateResponse?.returnCode}, code={stateResponse.error.code}," +
                //            $" msg={stateResponse.error.msg}, raw={response.Content}", LogMessageType.Error);
                //    }
                //}
                //catch (Exception exception)
                //{
                //    SendLogMessage(exception.ToString(), LogMessageType.Error);
                //}
            }

            public void CancelAllOrders()
            {
                _rateGateForAll.WaitToProceed();

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
                        SendLogMessage($"CancelAllOrders>  error State Code: {responseMessage.StatusCode}", LogMessageType.Error);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage("CancelAllOrders error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            public void CancelAllOrdersToSecurity(Security security)
            {
                _rateGateForAll.WaitToProceed();

                try
                {
                    string jsonRequest = JsonConvert.SerializeObject(new XTFuturesCancelAllOrders { symbol = security.Name },
                        new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

                    IRestResponse responseMessage = CreatePrivateQuery("/future/trade/v1/order/cancel-all", Method.POST, null, body: jsonRequest);

                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        XTFuturesResponseRest<string> stateResponse = JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(responseMessage.Content);

                        if (stateResponse.returnCode.Equals("0"))
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
                        SendLogMessage($"CancelAllOrdersToSecurity>  State Code: {responseMessage.StatusCode} || {responseMessage.Content}", LogMessageType.Error);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage("CancelAllOrdersToSecurity error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            public bool CancelOrder(Order order)
            {
                _rateGateForAll.WaitToProceed();

                try
                {
                    var body = new
                    {
                        orderId = order.NumberMarket.ToString(CultureInfo.InvariantCulture)
                    };

                    IRestResponse response = CreatePrivateQuery("/future/trade/v1/order/cancel", Method.POST, query: null, body: body);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        XTFuturesResponseRest<string> stateResponse = JsonConvert.DeserializeObject<XTFuturesResponseRest<string>>(response.Content);

                        if (stateResponse.returnCode.Equals("0") && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                        else
                        {

                            OrderStateType state = GetOrderStatus(order);

                            if (state == OrderStateType.None)
                            {
                                SendLogMessage($"CancelOrder error, Code: {stateResponse.returnCode}\n"
                               + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
                                return false;
                            }
                            else
                            {
                                return true;
                            }
                        }
                    }
                    else
                    {
                        OrderStateType state = GetOrderStatus(order);

                        if (state == OrderStateType.None)
                        {
                            SendLogMessage($"Order cancellation error: {response.Content}", LogMessageType.Error);
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage("CancelOrder error: " + exception.ToString(), LogMessageType.Error);
                }

                return false;
            }

            public void GetAllActivOrders()
            {
                List<Order> orders = GetAllActivOrdersArray(100);

                for (int i = 0; orders != null && i < orders.Count; i++)
                {
                    if (orders[i] == null)
                    {
                        continue;
                    }

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(orders[i]);
                    }
                }
            }

            private List<Order> GetAllActivOrdersArray(int maxCountByCategory)
            {
                List<Order> ordersOpenAll = new List<Order>();

                List<Order> orders = new List<Order>();

                GetAllOpenOrders(orders, 100);

                if (orders != null
                    && orders.Count > 0)
                {
                    ordersOpenAll.AddRange(orders);
                }

                return ordersOpenAll;
            }

            private RateGate _rateGateOpenOrders = new RateGate(1, TimeSpan.FromMilliseconds(100));

            public void GetAllOpenOrders(List<Order> array, int maxCount)
            {
                _rateGateOpenOrders.WaitToProceed();

                try
                {
                    IRestResponse responseMessage = CreatePrivateQuery("/future/trade/v1/order/list-open-order", Method.POST);

                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        XTFuturesResponseRest<List<XTFuturesOrderItem>> stateResponse =
                       JsonConvert.DeserializeObject<XTFuturesResponseRest<List<XTFuturesOrderItem>>>(responseMessage.Content);

                        if (stateResponse.returnCode == "0" && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                        {
                            List<Order> orders = new List<Order>();
                            List<XTFuturesOrderItem> src = stateResponse.result;

                            for (int i = 0; i < src.Count; i++)
                            {
                                XTFuturesOrderItem item = src[i];

                                Order activeOrder = new Order();

                                try
                                {
                                    activeOrder.NumberUser = Convert.ToInt32(item.clientOrderId);
                                }
                                catch
                                {

                                }

                                activeOrder.NumberMarket = item.orderId;
                                activeOrder.SecurityNameCode = item.symbol;
                                activeOrder.ServerType = ServerType.XTFutures;
                                activeOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createdTime));
                                activeOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.updatedTime));
                                activeOrder.Volume = item.origQty.ToDecimal() * GetVolume(activeOrder.SecurityNameCode);
                                activeOrder.Price = item.price.ToDecimal();
                                activeOrder.Side = item.orderSide.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;
                                activeOrder.State = GetOrderState(item.state);
                                activeOrder.TypeOrder = MapOrderType(item.orderType);
                                activeOrder.PortfolioNumber = _portfolioName;

                                orders.Add(activeOrder);
                            }

                            if (orders.Count > 0)
                            {
                                array.AddRange(orders);

                                if (array.Count > maxCount)
                                {
                                    while (array.Count > maxCount)
                                    {
                                        array.RemoveAt(array.Count - 1);
                                    }
                                    return;
                                }
                                else if (array.Count < 100)
                                {
                                    return;
                                }
                            }
                            else
                            {
                                return;
                            }

                            return;
                        }
                        else
                        {
                            SendLogMessage($"Get all open orders failed: {responseMessage.Content}", LogMessageType.Error);
                            return;
                        }
                    }
                    else
                    {
                        SendLogMessage($"Get all open orders request error. Code: {responseMessage.StatusCode}\n"
                                + $"Message: {responseMessage.Content}", LogMessageType.Error);
                        return;
                    }

                }
                catch (Exception exception)
                {
                    SendLogMessage("GetAllActiveOrders error: " + exception, LogMessageType.Error);
                    return;
                }
            }

            private List<Order> _activeOrdersCash = new List<Order>();
            private List<Order> _historicalOrdersCash = new List<Order>();
            private DateTime _timeOrdersCashCreate;

            public OrderStateType GetOrderStatus(Order order)
            {
                try
                {
                    if (_timeOrdersCashCreate.AddSeconds(2) < DateTime.Now)
                    {
                        // We update order arrays once every two seconds.
                        // We are creating a cache for mass requesting statuses on reconnection.
                        _historicalOrdersCash = GetHistoricalOrders(0, 100);
                        _activeOrdersCash = GetActiveOrders(0, 100);
                        _timeOrdersCashCreate = DateTime.Now;
                    }

                    Order myOrder = null;

                    for (int i = 0; _historicalOrdersCash != null && i < _historicalOrdersCash.Count; i++)
                    {
                        if (_historicalOrdersCash[i].NumberUser == order.NumberUser)
                        {
                            myOrder = _historicalOrdersCash[i];
                            break;
                        }
                    }

                    if (myOrder == null)
                    {
                        for (int i = 0; _activeOrdersCash != null && i < _activeOrdersCash.Count; i++)
                        {
                            if (_activeOrdersCash[i].NumberUser == order.NumberUser)
                            {
                                myOrder = _activeOrdersCash[i];
                                break;
                            }
                        }
                    }

                    if (myOrder == null)
                    {
                        return OrderStateType.None;
                    }

                    MyOrderEvent?.Invoke(myOrder);

                    if (myOrder.State == OrderStateType.Done ||
                         myOrder.State == OrderStateType.Partial)
                    {
                        CreateQueryMyTrade(myOrder.NumberMarket);
                    }

                    return myOrder.State;
                }
                catch (Exception exception)
                {
                    SendLogMessage($"GetOrderStatus> exception: {exception.Message}", LogMessageType.Error);
                }

                return OrderStateType.None;
            }

            private void CreateQueryMyTrade(string orderId)
            {
                _rateGateForAll.WaitToProceed();

                List<MyTrade> list = new List<MyTrade>();

                try
                {
                    string orderIdStr = orderId.ToString(CultureInfo.InvariantCulture);

                    IRestResponse response = CreatePrivateQuery("/future/trade/v1/order/trade-list", Method.GET, query: "orderId=" + orderIdStr);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        XTFuturesResponseRest<XTFuturesTradeHistoryResult> stateResponse =
                    JsonConvert.DeserializeObject<XTFuturesResponseRest<XTFuturesTradeHistoryResult>>(response.Content);

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
                                trade.Volume = data.quantity.ToDecimal() * GetVolume(trade.SecurityNameCode);
                                trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(data.timestamp));
                                trade.Side = string.Equals(data.orderSide, "BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;

                                MyTradeEvent?.Invoke(trade);
                            }
                        }
                        else
                        {
                            SendLogMessage($"Query myTrade error, Code: {stateResponse.returnCode}\n"
                                + $"Message code: {stateResponse.msgInfo}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Query myTrade request error. {response.StatusCode} || {response.Content}", LogMessageType.Error);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage("CreateQueryMyTrade error: " + exception.ToString(), LogMessageType.Error);
                }
            }

            private OrderPriceType MapOrderType(string fromExchange)
            {
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

            public List<Order> GetActiveOrders(int startIndex, int count)
            {
                int countToMethod = startIndex + count;

                List<Order> result = GetAllActivOrdersArray(countToMethod);

                List<Order> resultExit = new List<Order>();

                if (result != null
                    && startIndex < result.Count)
                {
                    if (startIndex + count < result.Count)
                    {
                        resultExit = result.GetRange(startIndex, count);
                    }
                    else
                    {
                        resultExit = result.GetRange(startIndex, result.Count - startIndex);
                    }
                }

                return resultExit;
            }

            public List<Order> GetHistoricalOrders(int startIndex, int count)
            {
                int countToMethod = startIndex + count;

                List<Order> result = GetAllHistoricalOrdersArray(countToMethod);

                List<Order> resultExit = new List<Order>();

                if (result != null
                    && startIndex < result.Count)
                {
                    if (startIndex + count < result.Count)
                    {
                        resultExit = result.GetRange(startIndex, count);
                    }
                    else
                    {
                        resultExit = result.GetRange(startIndex, result.Count - startIndex);
                    }
                }

                return resultExit;
            }

            private List<Order> GetAllHistoricalOrdersArray(int maxCountByCategory)
            {
                List<Order> ordersOpenAll = new List<Order>();

                List<Order> orders = new List<Order>();

                GetAllHistoricalOrders(orders, 100);

                if (orders != null
                    && orders.Count > 0)
                {
                    ordersOpenAll.AddRange(orders);
                }

                return ordersOpenAll;
            }

            private void GetAllHistoricalOrders(List<Order> array, int maxCount)
            {
                _rateGateForAll.WaitToProceed();

                try
                {
                    string query = "limit=100";

                    IRestResponse responseMessage = CreatePrivateQuery("/future/trade/v1/order/list-history", Method.GET, query);

                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        XTFuturesResponseRest<XTFuturesOrderResult> stateResponse =
                        JsonConvert.DeserializeObject<XTFuturesResponseRest<XTFuturesOrderResult>>(responseMessage.Content);

                        if (stateResponse.returnCode.Equals("0")
                            && stateResponse.msgInfo.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                        {
                            List<XTFuturesOrderItem> result = stateResponse.result.items;

                            if (result == null)
                            {
                                return;
                            }

                            List<Order> orders = new List<Order>();

                            for (int i = 0; i < result.Count; i++)
                            {
                                XTFuturesOrderItem item = result[i];

                                if (item.state.Contains("NEW"))
                                {
                                    continue;
                                }

                                Order historyOrder = new Order();

                                historyOrder.NumberMarket = item.orderId;
                                historyOrder.NumberUser = Convert.ToInt32(item.clientOrderId);
                                historyOrder.SecurityNameCode = item.symbol;
                                historyOrder.Side = item.orderSide.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;
                                historyOrder.State = GetOrderState(item.state);
                                historyOrder.TypeOrder = MapOrderType(item.orderType);
                                historyOrder.Volume = item.origQty.ToDecimal() * GetVolume(historyOrder.SecurityNameCode);
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
                            }

                            if (orders.Count > 0)
                            {
                                array.AddRange(orders);

                                if (array.Count > maxCount)
                                {
                                    while (array.Count > maxCount)
                                    {
                                        array.RemoveAt(array.Count - 1);
                                    }
                                    return;
                                }
                                else if (array.Count < 100)
                                {
                                    return;
                                }
                            }
                            else
                            {
                                return;
                            }

                            return;
                        }
                        else
                        {
                            SendLogMessage($"Get all historical orders error. {responseMessage.StatusCode} || {responseMessage.Content}", LogMessageType.Error);
                            return;
                        }
                    }
                    else
                    {
                        SendLogMessage($"Get all historical orders request error. Code: {responseMessage.StatusCode} || {responseMessage.Content}", LogMessageType.Error);
                        return;
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage(e.Message, LogMessageType.Error);
                    return;
                }
            }

            #endregion

            #region 12 Queries

            private RateGate _rateGateListenKey = new RateGate(1, TimeSpan.FromMilliseconds(10000));

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
                        SendLogMessage($"GetListenKey>  State Code: {responseMessage.StatusCode}", LogMessageType.Error);

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
                try
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
                catch (Exception exception)
                {
                    SendLogMessage(" SendPrivate error: " + exception, LogMessageType.Error);
                    return null;
                }
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
