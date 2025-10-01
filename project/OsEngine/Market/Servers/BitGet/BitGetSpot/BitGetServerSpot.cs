/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Entity.WebSocketOsEngine;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.BitGet.BitGetSpot.Entity;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;


namespace OsEngine.Market.Servers.BitGet.BitGetSpot
{
    public class BitGetServerSpot : AServer
    {
        public BitGetServerSpot(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            BitGetServerSpotRealization realization = new BitGetServerSpotRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterPassphrase, "");
            CreateParameterBoolean("Extended Data", false);
        }
    }

    public class BitGetServerSpotRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public BitGetServerSpotRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread threadMessageReaderPublic = new Thread(MessageReaderPublic);
            threadMessageReaderPublic.IsBackground = true;
            threadMessageReaderPublic.Name = "MessageReaderPublic";
            threadMessageReaderPublic.Start();

            Thread threadMessageReaderPrivate = new Thread(MessageReaderPrivate);
            threadMessageReaderPrivate.IsBackground = true;
            threadMessageReaderPrivate.Name = "MessageReaderPrivate";
            threadMessageReaderPrivate.Start();

            Thread thread = new Thread(CheckAliveWebSocket);
            thread.Name = "CheckAliveWebSocket";
            thread.Start();
        }

        private WebProxy _myProxy;

        public void Connect(WebProxy proxy = null)
        {
            _myProxy = proxy;
            PublicKey = ((ServerParameterString)ServerParameters[0]).Value;
            SeckretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
            Passphrase = ((ServerParameterPassword)ServerParameters[2]).Value;

            if (string.IsNullOrEmpty(PublicKey) ||
                string.IsNullOrEmpty(SeckretKey) ||
                string.IsNullOrEmpty(Passphrase))
            {
                SendLogMessage("Can`t run Bitget Spot connector. No keys or passphrase",
                    LogMessageType.Error);
                return;
            }

            if (((ServerParameterBool)ServerParameters[3]).Value == true)
            {
                _extendedMarketData = true;
            }
            else
            {
                _extendedMarketData = false;
            }

            try
            {
                string requestStr = "/api/v2/public/time";
                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                RestClient client = new RestClient(BaseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    CreatePublicWebSocketConnect();
                    CreatePrivateWebSocketConnect();
                    _lastConnectionStartTime = DateTime.Now;
                }
                else
                {
                    SendLogMessage("Connection can be open. BitGet Spot. Error request", LogMessageType.Error);
                    Disconnect();
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                SendLogMessage("Connection can be open. BitGet Spot. Error request", LogMessageType.Error);
                Disconnect();
            }
        }

        public void Dispose()
        {
            try
            {
                UnsubscribeFromAllWebSockets();
                _subscribedSecutiries.Clear();
                DeleteWebSocketConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
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
                DisconnectEvent();
            }
        }

        public ServerType ServerType
        {
            get { return ServerType.BitGetSpot; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public DateTime ServerTime { get; set; }

        private DateTime _lastConnectionStartTime = DateTime.MinValue;

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string BaseUrl = "https://api.bitget.com";

        private string PublicKey;

        private string SeckretKey;

        private string Passphrase;

        private int _limitCandlesData = 200;

        private bool _extendedMarketData;

        #endregion

        #region 3 Securities

        private List<Security> _securities;

        private RateGate _rateGateSecurity = new RateGate(2, TimeSpan.FromMilliseconds(100));

        public void GetSecurities()
        {
            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            _rateGateSecurity.WaitToProceed();

            try
            {
                string requestStr = $"/api/v2/spot/public/symbols";
                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                RestClient client = new RestClient(BaseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<RestMessageSymbol>> symbols = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<RestMessageSymbol>>());

                    if (symbols.code == "00000")
                    {
                        for (int i = 0; i < symbols.data.Count; i++)
                        {
                            RestMessageSymbol item = symbols.data[i];

                            decimal priceStep = GetPriceStep(Convert.ToInt32(item.pricePrecision));

                            if (item.status != "online")
                            {
                                continue;
                            }

                            Security newSecurity = new Security();

                            newSecurity.Exchange = ServerType.BitGetSpot.ToString();
                            newSecurity.DecimalsVolume = Convert.ToInt32(item.quantityPrecision);
                            newSecurity.Lot = 1;
                            newSecurity.Name = item.symbol;
                            newSecurity.NameFull = item.symbol;
                            newSecurity.NameClass = item.quoteCoin;
                            newSecurity.NameId = item.symbol;
                            newSecurity.SecurityType = SecurityType.CurrencyPair;
                            newSecurity.Decimals = Convert.ToInt32(item.pricePrecision);
                            newSecurity.PriceStep = priceStep;
                            newSecurity.PriceStepCost = priceStep;
                            newSecurity.State = SecurityStateType.Activ;
                            newSecurity.MinTradeAmountType = MinTradeAmountType.C_Currency;
                            newSecurity.MinTradeAmount = item.minTradeUSDT.ToDecimal();

                            if (newSecurity.DecimalsVolume == 0)
                            {
                                newSecurity.VolumeStep = 1;
                            }
                            else
                            {
                                newSecurity.VolumeStep = GetVolumeStep(newSecurity.DecimalsVolume);
                            }

                            _securities.Add(newSecurity);
                        }

                        SecurityEvent(_securities);
                    }
                    else
                    {
                        SendLogMessage($"Securities error. {symbols.code} || msg: {symbols.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Securities error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Securities request error: {ex.Message} {ex.StackTrace}" + ex.ToString(), LogMessageType.Error);
            }
        }

        private decimal GetVolumeStep(int quantityPrecision)
        {
            if (quantityPrecision == 0)
            {
                return 1;
            }
            string priceStep = "0,";
            for (int i = 0; i < quantityPrecision - 1; i++)
            {
                priceStep += "0";
            }

            priceStep += "1";

            return priceStep.ToDecimal();
        }

        private decimal GetPriceStep(int pricePrecision)
        {
            if (pricePrecision == 0)
            {
                return 1;
            }

            string res = String.Empty;

            for (int i = 0; i < pricePrecision; i++)
            {
                if (i == 0)
                {
                    res += "0,";
                }
                else
                {
                    res += "0";
                }
            }
            res += "1";

            return res.ToDecimal();
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        public void GetPortfolios()
        {
            CreateQueryPortfolio(true);
            _portfolioIsStarted = true;
        }

        private bool _portfolioIsStarted = false;

        private RateGate _rateGatePortfolio = new RateGate(1, TimeSpan.FromMilliseconds(100));

        private void CreateQueryPortfolio(bool IsUpdateValueBegin)
        {
            _rateGatePortfolio.WaitToProceed();

            try
            {
                IRestResponse response = CreatePrivateQueryOrders("/api/v2/spot/account/assets", Method.GET, null, null);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<RestMessageAccount>> stateResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<RestMessageAccount>>());

                    if (stateResponse.code.Equals("00000") == true)
                    {
                        Portfolio portfolio = new Portfolio();

                        portfolio.Number = "BitGetSpot";
                        portfolio.ValueBegin = 1;
                        portfolio.ValueCurrent = 1;

                        for (int i = 0; i < stateResponse.data.Count; i++)
                        {
                            PositionOnBoard pos = new PositionOnBoard();

                            pos.PortfolioName = "BitGetSpot";
                            pos.SecurityNameCode = stateResponse.data[i].coin.ToUpper();
                            pos.ValueBlocked = stateResponse.data[i].frozen.ToDecimal();
                            pos.ValueCurrent = stateResponse.data[i].available.ToDecimal();

                            if (IsUpdateValueBegin)
                            {
                                pos.ValueBegin = stateResponse.data[i].available.ToDecimal();
                            }

                            portfolio.SetNewPosition(pos);
                        }

                        PortfolioEvent(new List<Portfolio> { portfolio });
                    }
                    else
                    {
                        SendLogMessage($"Portfolio error. {stateResponse.code} || msg: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Portfolio error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Portfolio request error: {ex.Message} {ex.StackTrace}" + ex.ToString(), LogMessageType.Error);
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

            return GetCandleData(security, timeFrameBuilder, startTime, endTime, endTime);
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return GetCandleData(security, timeFrameBuilder, startTime, endTime, actualTime);
        }

        private List<Candle> GetCandleData(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
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

            int limitCandles = _limitCandlesData;

            TimeSpan span = endTime - startTime;

            if (limitCandles > span.TotalMinutes / tfTotalMinutes)
            {
                limitCandles = (int)Math.Round(span.TotalMinutes / tfTotalMinutes, MidpointRounding.AwayFromZero);
            }

            List<Candle> allCandles = new List<Candle>();

            DateTime startTimeData = startTime;
            DateTime endTimeData = startTimeData.AddMinutes(tfTotalMinutes * limitCandles);

            do
            {
                long from = TimeManager.GetTimeStampMilliSecondsToDateTime(startTimeData);
                long to = TimeManager.GetTimeStampMilliSecondsToDateTime(endTimeData);

                string interval = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);

                List<Candle> candles = RequestCandleHistory(security, interval, from, to, limitCandles);

                if (candles == null || candles.Count == 0)
                {
                    break;
                }

                Candle last = candles[candles.Count - 1];

                if (allCandles.Count > 0)
                {
                    if (allCandles[allCandles.Count - 1].TimeStart == candles[0].TimeStart)
                    {
                        candles.RemoveAt(0);
                    }
                }

                if (last.TimeStart >= endTime)

                {
                    for (int i = 0; i < candles.Count; i++)
                    {
                        if (candles[i].TimeStart <= endTime)
                        {
                            allCandles.Add(candles[i]);
                        }
                    }
                    break;
                }

                allCandles.AddRange(candles);

                startTimeData = endTimeData;
                endTimeData = startTimeData.AddMinutes(tfTotalMinutes * limitCandles);

                if (startTimeData >= endTime)
                {
                    break;
                }

                if (endTimeData > endTime)
                {
                    endTimeData = endTime;
                }

                span = endTimeData - startTimeData;

                if (limitCandles > span.TotalMinutes / tfTotalMinutes)
                {
                    limitCandles = (int)Math.Round(span.TotalMinutes / tfTotalMinutes, MidpointRounding.AwayFromZero);
                }

            } while (true);

            return allCandles;
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
                timeFrameMinutes == 3 ||
                timeFrameMinutes == 5 ||
                timeFrameMinutes == 15 ||
                timeFrameMinutes == 30 ||
                timeFrameMinutes == 60 ||
                timeFrameMinutes == 240)
            {
                return true;
            }
            return false;
        }

        private string GetInterval(TimeSpan tf)
        {
            if (tf.Minutes != 0)
            {
                return $"{tf.Minutes}min";
            }
            else
            {
                return $"{tf.Hours}h";
            }
        }

        private readonly RateGate _rgCandleData = new RateGate(1, TimeSpan.FromMilliseconds(100));

        private List<Candle> RequestCandleHistory(Security security, string interval, long startTime, long endTime, int limitCandles)
        {
            _rgCandleData.WaitToProceed();

            string stringUrl = "/api/v2/spot/market/history-candles";

            try
            {
                string requestStr = $"{stringUrl}?symbol={security.Name}&productType={security.NameClass.ToLower()}&" +
                    $"startTime={startTime}&granularity={interval}&limit={limitCandles}&endTime={endTime}";
                RestRequest requestRest = new RestRequest(requestStr, Method.GET);

                RestClient client = new RestClient(BaseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    RestMessageCandle candlesResponse = JsonConvert.DeserializeObject<RestMessageCandle>(response.Content);

                    if (candlesResponse.code == "00000")
                    {
                        List<Candle> candles = new List<Candle>();

                        for (int i = 0; i < candlesResponse.data.Count; i++)
                        {
                            if (CheckCandlesToZeroData(candlesResponse.data[i]))
                            {
                                continue;
                            }

                            Candle candle = new Candle();

                            candle.State = CandleState.Finished;
                            candle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(long.Parse(candlesResponse.data[i][0]));
                            candle.Volume = candlesResponse.data[i][5].ToDecimal();
                            candle.Close = candlesResponse.data[i][4].ToDecimal();
                            candle.High = candlesResponse.data[i][2].ToDecimal();
                            candle.Low = candlesResponse.data[i][3].ToDecimal();
                            candle.Open = candlesResponse.data[i][1].ToDecimal();

                            candles.Add(candle);
                        }

                        return candles;
                    }
                    else
                    {
                        SendLogMessage($"Candles request error. {candlesResponse.code} || msg: {candlesResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Candles error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private bool CheckCandlesToZeroData(List<string> item)
        {
            if (item[1].ToDecimal() == 0 ||
                item[2].ToDecimal() == 0 ||
                item[3].ToDecimal() == 0 ||
                item[4].ToDecimal() == 0)
            {
                return true;
            }
            return false;
        }
        private readonly RateGate _rgTickData = new RateGate(1, TimeSpan.FromMilliseconds(110));

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
            endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
            actualTime = DateTime.SpecifyKind(actualTime, DateTimeKind.Utc);

            if (startTime < DateTime.UtcNow.AddDays(-90))
            {
                SendLogMessage("History more than 90 days is not supported by API", LogMessageType.Error);
                return null;
            }

            TimeSpan span = endTime - startTime;

            if (span.Days > 7)
            {
                SendLogMessage("The time interval between startTime and endTime should not exceed 7 days", LogMessageType.Error);
                return null;
            }

            if (!CheckTime(startTime, endTime, actualTime))
            {
                return null;
            }

            List<Trade> trades = new List<Trade>();
            List<Trade> newTrades = GetTickHistoryToSecurity(security, endTime, startTime);

            if (newTrades == null ||
                    newTrades.Count == 0)
            {
                return null;
            }

            trades.AddRange(newTrades);
            DateTime timeEnd = DateTime.SpecifyKind(trades[0].Time, DateTimeKind.Utc);

            while (timeEnd > startTime)
            {
                newTrades = GetTickHistoryToSecurity(security, timeEnd, startTime);

                if (newTrades != null && trades.Count != 0 && newTrades.Count != 0)
                {
                    for (int j = 0; j < trades.Count; j++)
                    {
                        for (int i = 0; i < newTrades.Count; i++)
                        {
                            if (trades[j].Id == newTrades[i].Id)
                            {
                                newTrades.RemoveAt(i);
                                i--;
                            }
                        }
                    }
                }

                if (newTrades.Count == 0)
                {
                    break;
                }

                trades.InsertRange(0, newTrades);
                timeEnd = DateTime.SpecifyKind(trades[0].Time, DateTimeKind.Utc);
            }

            if (trades.Count == 0)
            {
                return null;
            }

            for (int i = trades.Count - 1; i >= 0; i--)
            {
                if (DateTime.SpecifyKind(trades[i].Time, DateTimeKind.Utc) <= endTime)
                {
                    break;
                }
                else
                {
                    trades.RemoveAt(i);
                }
            }

            return trades;
        }

        private List<Trade> GetTickHistoryToSecurity(Security security, DateTime endTime, DateTime startTime)
        {
            _rgTickData.WaitToProceed();

            try
            {
                List<Trade> trades = new List<Trade>();

                long timeEnd = TimeManager.GetTimeStampMilliSecondsToDateTime(endTime);
                long timeStart = TimeManager.GetTimeStampMilliSecondsToDateTime(startTime);

                string requestStr = $"/api/v2/spot/market/fills-history?symbol={security.Name}&limit=1000&endTime={timeEnd}&startTime={timeStart}";

                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                RestClient client = new RestClient(BaseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<TradeData>> tradesResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<TradeData>>());

                    if (tradesResponse.code == "00000")
                    {
                        for (int i = 0; i < tradesResponse.data.Count; i++)
                        {
                            TradeData item = tradesResponse.data[i];

                            Trade trade = new Trade();
                            trade.SecurityNameCode = item.symbol;
                            trade.Id = item.tradeId;
                            trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts));
                            trade.Price = item.price.ToDecimal();
                            trade.Volume = item.size.ToDecimal();
                            trade.Side = item.side == "Sell" ? Side.Sell : Side.Buy;
                            trades.Add(trade);
                        }

                        trades.Reverse();
                        return trades;
                    }
                    else
                    {
                        SendLogMessage($"Trades request error: {tradesResponse.code} - {tradesResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Trades request error: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception error)
            {
                SendLogMessage($"Trades request error: {error.Message} {error.StackTrace}", LogMessageType.Error);
            }

            return null;
        }

        #endregion

        #region 6 WebSocket creation

        private string _webSocketUrlPublic = "wss://ws.bitget.com/v2/ws/public";

        private string _webSocketUrlPrivate = "wss://ws.bitget.com/v2/ws/private";

        private List<WebSocket> _webSocketPublic = new List<WebSocket>();

        private WebSocket _webSocketPrivate;

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
                WebSocket webSocketPublicNew = new WebSocket(_webSocketUrlPublic);

                if (_myProxy != null)
                {
                    webSocketPublicNew.SetProxy(_myProxy);
                }

                webSocketPublicNew.EmitOnPing = true;
                webSocketPublicNew.OnOpen += WebSocketPublic_Opened;
                webSocketPublicNew.OnMessage += WebSocketPublic_MessageReceived;
                webSocketPublicNew.OnError += WebSocketPublic_Error;
                webSocketPublicNew.OnClose += WebSocketPublic_Closed;
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

                _webSocketPrivate = new WebSocket(_webSocketUrlPrivate);

                if (_myProxy != null)
                {
                    _webSocketPrivate.SetProxy(_myProxy);
                }

                _webSocketPrivate.EmitOnPing = true;
                _webSocketPrivate.OnOpen += WebSocketPrivate_Opened;
                _webSocketPrivate.OnClose += WebSocketPrivate_Closed;
                _webSocketPrivate.OnMessage += WebSocketPrivate_MessageReceived;
                _webSocketPrivate.OnError += WebSocketPrivate_Error;
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

                        webSocketPublic.OnOpen -= WebSocketPublic_Opened;
                        webSocketPublic.OnClose -= WebSocketPublic_Closed;
                        webSocketPublic.OnMessage -= WebSocketPublic_MessageReceived;
                        webSocketPublic.OnError -= WebSocketPublic_Error;

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
                    _webSocketPrivate.OnOpen -= WebSocketPrivate_Opened;
                    _webSocketPrivate.OnClose -= WebSocketPrivate_Closed;
                    _webSocketPrivate.OnMessage -= WebSocketPrivate_MessageReceived;
                    _webSocketPrivate.OnError -= WebSocketPrivate_Error;
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

        private void CreateAuthMessageWebSocekt()
        {
            try
            {
                string TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                string Sign = GenerateSignature(TimeStamp, "GET", "/user/verify", null, null, SeckretKey);

                RequestWebsocketAuth requestWebsocketAuth = new RequestWebsocketAuth();

                requestWebsocketAuth.op = "login";
                requestWebsocketAuth.args = new List<AuthItem>();
                requestWebsocketAuth.args.Add(new AuthItem());
                requestWebsocketAuth.args[0].apiKey = PublicKey;
                requestWebsocketAuth.args[0].passphrase = Passphrase;
                requestWebsocketAuth.args[0].timestamp = TimeStamp;
                requestWebsocketAuth.args[0].sign = Sign;

                string AuthJson = JsonConvert.SerializeObject(requestWebsocketAuth);

                _webSocketPrivate.SendAsync(AuthJson);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocketPublic_Opened(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("Bitget WebSocket Public connection open", LogMessageType.System);
                    CheckSocketsActivate();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublic_Closed(object sender, CloseEventArgs e)
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

        private void WebSocketPublic_MessageReceived(object sender, MessageEventArgs e)
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
                if (e.Data.Length == 4)
                { // pong message
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
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublic_Error(object sender, ErrorEventArgs e)
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

        private void WebSocketPrivate_Opened(object sender, EventArgs e)
        {
            try
            {
                CreateAuthMessageWebSocekt();
                SendLogMessage("Bitget WebSocket Private connection open", LogMessageType.System);
                CheckSocketsActivate();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPrivate_Closed(object sender, CloseEventArgs e)
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

        private void WebSocketPrivate_MessageReceived(object sender, MessageEventArgs e)
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
                if (e.Data.Length == 4)
                { // pong message
                    return;
                }

                if (e.Data.Contains("login"))
                {
                    SubscribePrivate();
                }

                if (FIFOListWebSocketPrivateMessage == null)
                {
                    return;
                }

                FIFOListWebSocketPrivateMessage.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPrivate_Error(object sender, ErrorEventArgs e)
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

        #endregion

        #region 8 WebSocket check alive

        private void CheckAliveWebSocket()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(25000);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
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
                        _webSocketPrivate.ReadyState == WebSocketState.Connecting)
                        )
                    {
                        _webSocketPrivate.SendAsync("ping");
                    }
                    else
                    {
                        Disconnect();
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(3000);
                }
            }
        }

        #endregion

        #region 9 Security subscribe

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(350));

        private List<string> _subscribedSecutiries = new List<string>();

        public void Subscribe(Security security)
        {
            try
            {
                _rateGateSubscribe.WaitToProceed();
                CreateSubscribeSecurityMessageWebSocket(security);

            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void CreateSubscribeSecurityMessageWebSocket(Security security)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (_subscribedSecutiries != null)
                {
                    for (int i = 0; i < _subscribedSecutiries.Count; i++)
                    {
                        if (_subscribedSecutiries.Equals(security.Name))
                        {
                            return;
                        }
                    }
                }

                _subscribedSecutiries.Add(security.Name);

                if (_webSocketPublic.Count == 0)
                {
                    return;
                }

                WebSocket webSocketPublic = _webSocketPublic[_webSocketPublic.Count - 1];

                if (webSocketPublic.ReadyState == WebSocketState.Open
                    && _subscribedSecutiries.Count != 0
                    && _subscribedSecutiries.Count % 50 == 0)
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
                    webSocketPublic.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"instType\": \"SPOT\",\"channel\": \"books15\",\"instId\": \"{security.Name}\"}}]}}");
                    webSocketPublic.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{ \"instType\": \"SPOT\",\"channel\": \"trade\",\"instId\": \"{security.Name}\"}}]}}");

                    if (_extendedMarketData)
                    {
                        webSocketPublic.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{ \"instType\": \"SPOT\",\"channel\": \"ticker\",\"instId\": \"{security.Name}\"}}]}}");
                    }
                }

                if (_webSocketPrivate != null)
                {
                    _webSocketPrivate.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"instType\": \"SPOT\",\"channel\": \"orders\",\"instId\": \"{security.Name}\"}}]}}");
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private void SubscribePrivate()
        {
            try
            {
                _webSocketPrivate.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"instType\": \"SPOT\",\"channel\": \"account\",\"coin\": \"default\"}}]}}");
                _webSocketPrivate.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"instType\": \"SPOT\",\"channel\": \"fill\",\"coin\": \"default\"}}]}}");
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Error);
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
                                if (_subscribedSecutiries != null)
                                {
                                    for (int i2 = 0; i2 < _subscribedSecutiries.Count; i2++)
                                    {
                                        webSocketPublic.SendAsync($"{{\"op\": \"unsubscribe\",\"args\": [{{\"instType\": \"SPOT\",\"channel\": \"books15\",\"instId\": \"{_subscribedSecutiries[i2]}\"}}]}}");
                                        webSocketPublic.SendAsync($"{{\"op\": \"unsubscribe\",\"args\": [{{\"instType\": \"SPOT\",\"channel\": \"trade\",\"instId\": \"{_subscribedSecutiries[i2]}\"}}]}}");

                                        if (_extendedMarketData)
                                        {
                                            webSocketPublic.SendAsync($"{{\"op\": \"unsubscribe\",\"args\": [{{ \"instType\": \"SPOT\",\"channel\": \"ticker\",\"instId\": \"{_subscribedSecutiries[i2]}\"}}]}}");
                                        }

                                        _webSocketPrivate.SendAsync($"{{\"op\": \"unsubscribe\",\"args\": [{{\"instType\": \"SPOT\",\"channel\": \"orders\",\"instId\": \"{_subscribedSecutiries[i2]}\"}}]}}");
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


            if (_webSocketPrivate != null)
            {
                try
                {
                    _webSocketPrivate.SendAsync($"{{\"op\": \"unsubscribe\",\"args\": [{{\"instType\": \"SPOT\",\"channel\": \"account\",\"coin\": \"default\"}}]}}");
                    _webSocketPrivate.SendAsync($"{{\"op\": \"unsubscribe\",\"args\": [{{\"instType\": \"SPOT\",\"channel\": \"fill\",\"coin\": \"default\"}}]}}");
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

        private void MessageReaderPublic()
        {
            Thread.Sleep(5000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (FIFOListWebSocketPublicMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message = null;

                    FIFOListWebSocketPublicMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    ResponseWebSocketMessageSubscribe SubscribeState = null;

                    try
                    {
                        SubscribeState = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageSubscribe());
                    }
                    catch (Exception error)
                    {
                        SendLogMessage("Error in message reader: " + error.ToString(), LogMessageType.Error);
                        SendLogMessage("message str: \n" + message, LogMessageType.Error);
                        continue;
                    }

                    if (SubscribeState.code != null)
                    {
                        if (SubscribeState.code.Equals("0") == false)
                        {
                            SendLogMessage("WebSocket listener error", LogMessageType.Error);
                            SendLogMessage(SubscribeState.code + "\n" +
                                SubscribeState.msg, LogMessageType.Error);

                            if (_lastConnectionStartTime.AddMinutes(5) > DateTime.Now)
                            { // if there are problems with the web socket startup, you need to restart it
                                ServerStatus = ServerConnectStatus.Disconnect;
                                DisconnectEvent();
                            }
                        }

                        continue;
                    }
                    else
                    {
                        ResponseWebSocketMessageAction<object> action = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<object>());

                        if (action.arg != null)
                        {
                            if (action.arg.channel.Equals("books15"))
                            {
                                UpdateDepth(message);
                                continue;
                            }
                            if (action.arg.channel.Equals("trade"))
                            {
                                UpdateTrade(message);
                                continue;
                            }
                            if (action.arg.channel.Equals("ticker"))
                            {
                                UpdateTicker(message);
                                continue;
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(3000);
                }
            }
        }

        private void MessageReaderPrivate()
        {
            Thread.Sleep(5000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (FIFOListWebSocketPrivateMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message = null;

                    FIFOListWebSocketPrivateMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    ResponseWebSocketMessageSubscribe SubscribeState = null;

                    try
                    {
                        SubscribeState = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageSubscribe());
                    }
                    catch (Exception error)
                    {
                        SendLogMessage("Error in message reader: " + error.ToString(), LogMessageType.Error);
                        SendLogMessage("message str: \n" + message, LogMessageType.Error);
                        continue;
                    }

                    if (SubscribeState.code != null)
                    {
                        if (SubscribeState.code.Equals("0") == false)
                        {
                            SendLogMessage("WebSocket listener error", LogMessageType.Error);
                            SendLogMessage(SubscribeState.code + "\n" +
                                SubscribeState.msg, LogMessageType.Error);

                            if (_lastConnectionStartTime.AddMinutes(5) > DateTime.Now)
                            { // if there are problems with the web socket startup, you need to restart it
                                ServerStatus = ServerConnectStatus.Disconnect;
                                DisconnectEvent();
                            }
                        }

                        continue;
                    }
                    else
                    {
                        ResponseWebSocketMessageAction<object> action = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<object>());

                        if (action.arg != null)
                        {
                            if (action.arg.channel.Equals("account"))
                            {
                                UpdateAccount(message);
                                continue;
                            }

                            if (action.arg.channel.Equals("orders"))
                            {
                                UpdateOrder(message);
                                continue;
                            }

                            if (action.arg.channel.Equals("fill"))
                            {
                                UpdateMyTrade(message);
                                continue;
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(3000);
                }
            }
        }

        private void UpdateAccount(string message)
        {
            if (_portfolioIsStarted == false)
            {
                return;
            }

            try
            {
                ResponseWebSocketMessageAction<List<ResponseWebSocketAccount>> assets = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<List<ResponseWebSocketAccount>>());

                if (assets.data == null ||
                    assets.data.Count == 0)
                {
                    return;
                }

                Portfolio portfolio = new Portfolio();
                portfolio.Number = "BitGetSpot";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;

                for (int i = 0; i < assets.data.Count; i++)
                {
                    PositionOnBoard pos = new PositionOnBoard();

                    pos.PortfolioName = "BitGetSpot";
                    pos.SecurityNameCode = assets.data[i].coin;
                    pos.ValueBlocked = assets.data[i].frozen.ToDecimal();
                    pos.ValueCurrent = assets.data[i].available.ToDecimal();

                    portfolio.SetNewPosition(pos);
                }

                PortfolioEvent(new List<Portfolio> { portfolio });
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private void UpdateMyTrade(string message)
        {
            try
            {
                ResponseWebSocketMessageAction<List<ResponseWebSocketMyTrade>> trade = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<List<ResponseWebSocketMyTrade>>());

                if (trade.data == null ||
                    trade.data.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < trade.data.Count; i++)
                {
                    ResponseWebSocketMyTrade item = trade.data[i];

                    MyTrade myTrade = new MyTrade();
                    myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.uTime));
                    myTrade.NumberOrderParent = item.orderId.ToString();
                    myTrade.NumberTrade = item.tradeId;
                    myTrade.Price = item.priceAvg.ToDecimal();
                    myTrade.SecurityNameCode = item.symbol;
                    myTrade.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;

                    if (string.IsNullOrEmpty(item.feeDetail[0].feeCoin) == false
                        && string.IsNullOrEmpty(item.feeDetail[0].totalFee) == false
                        && item.feeDetail[0].totalFee.ToDecimal() != 0)
                    {
                        if (myTrade.SecurityNameCode.StartsWith(item.feeDetail[0].feeCoin))
                        {
                            myTrade.Volume = item.size.ToDecimal() - item.feeDetail[0].totalFee.ToDecimal();
                            int decimalVolume = GetVolumeDecimals(myTrade.SecurityNameCode);

                            if (decimalVolume > 0)
                            {
                                myTrade.Volume = Math.Floor(myTrade.Volume * (decimal)Math.Pow(10, decimalVolume)) / (decimal)Math.Pow(10, decimalVolume);
                            }
                        }
                        else
                        {
                            myTrade.Volume = item.size.ToDecimal();
                        }
                    }
                    else
                    {
                        myTrade.Volume = item.size.ToDecimal();
                    }

                    MyTradeEvent(myTrade);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private void UpdateOrder(string message)
        {
            try
            {
                ResponseWebSocketMessageAction<List<ResponseWebSocketOrder>> order = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<List<ResponseWebSocketOrder>>());

                if (order.data == null ||
                    order.data.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < order.data.Count; i++)
                {
                    ResponseWebSocketOrder item = order.data[i];

                    if (string.IsNullOrEmpty(item.orderId))
                    {
                        continue;
                    }

                    OrderStateType stateType = GetOrderState(item.status);

                    if (item.orderType.Equals("market") &&
                        stateType != OrderStateType.Done &&
                        stateType != OrderStateType.Partial)
                    {
                        continue;
                    }

                    Order newOrder = new Order();
                    newOrder.SecurityNameCode = item.instId;
                    newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.cTime));
                    int.TryParse(item.clientOid, out newOrder.NumberUser);
                    newOrder.NumberMarket = item.orderId.ToString();
                    newOrder.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;
                    newOrder.State = stateType;
                    newOrder.Volume = item.newSize.ToDecimal();
                    newOrder.Price = item.price.ToDecimal();
                    newOrder.ServerType = ServerType.BitGetSpot;
                    newOrder.PortfolioNumber = "BitGetSpot";
                    newOrder.SecurityClassCode = order.arg.instType.ToString();
                    newOrder.TypeOrder = OrderPriceType.Limit;

                    MyOrderEvent(newOrder);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private int GetVolumeDecimals(string security)
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

        private void UpdateTrade(string message)
        {
            try
            {
                ResponseWebSocketMessageAction<List<ResponseWebsocketTrade>> responseTrade = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<List<ResponseWebsocketTrade>>());

                if (responseTrade == null
                    || responseTrade.data == null
                    || responseTrade.data.Count == 0)
                {
                    return;
                }

                long time = 0;

                for (int i = 0; i < responseTrade.data.Count; i++)
                {
                    Trade trade = new Trade();

                    if (i == 0)
                    {
                        trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseTrade.data[i].ts));
                        time = Convert.ToInt64(responseTrade.data[i].ts);
                    }
                    else
                    {
                        time += 1;
                        trade.Time = TimeManager.GetDateTimeFromTimeStamp(time);
                    }

                    trade.SecurityNameCode = responseTrade.arg.instId;
                    trade.Price = responseTrade.data[i].price.ToDecimal();
                    trade.Id = responseTrade.data[i].tradeId;

                    if (trade.Id == null)
                    {
                        return;
                    }

                    trade.Volume = responseTrade.data[i].size.ToDecimal();
                    trade.Side = responseTrade.data[i].side.Equals("buy") ? Side.Buy : Side.Sell;

                    NewTradesEvent(trade);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private void UpdateDepth(string message)
        {
            try
            {
                ResponseWebSocketMessageAction<List<ResponseWebSocketDepthItem>> responseDepth = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<List<ResponseWebSocketDepthItem>>());

                if (responseDepth.data == null)
                {
                    return;
                }

                if (responseDepth.data[0].asks.Count == 0 && responseDepth.data[0].bids.Count == 0)
                {
                    return;
                }

                MarketDepth marketDepth = new MarketDepth();

                List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                marketDepth.SecurityNameCode = responseDepth.arg.instId;

                for (int i = 0; i < responseDepth.data[0].asks.Count; i++)
                {
                    double ask = responseDepth.data[0].asks[i][1].ToString().ToDouble();
                    double price = responseDepth.data[0].asks[i][0].ToString().ToDouble();

                    if (ask == 0 ||
                        price == 0)
                    {
                        continue;
                    }

                    MarketDepthLevel level = new MarketDepthLevel();
                    level.Ask = ask;
                    level.Price = price;
                    ascs.Add(level);
                }

                for (int i = 0; i < responseDepth.data[0].bids.Count; i++)
                {
                    double bid = responseDepth.data[0].bids[i][1].ToString().ToDouble();
                    double price = responseDepth.data[0].bids[i][0].ToString().ToDouble();

                    if (bid == 0 ||
                        price == 0)
                    {
                        continue;
                    }

                    MarketDepthLevel level = new MarketDepthLevel();
                    level.Bid = bid;
                    level.Price = price;
                    bids.Add(level);
                }

                marketDepth.Asks = ascs;
                marketDepth.Bids = bids;

                marketDepth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseDepth.data[0].ts));

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

                MarketDepthEvent(marketDepth);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private void UpdateTicker(string message)
        {
            try
            {
                ResponseWebSocketMessageAction<List<TickerItem>> responseTicker = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<List<TickerItem>>());

                if (responseTicker == null
                    || responseTicker.data == null
                    || responseTicker.data.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < responseTicker.data.Count; i++)
                {
                    TickerItem item = responseTicker.data[i];

                    SecurityVolumes volume = new SecurityVolumes();

                    volume.SecurityNameCode = item.instId;
                    volume.Volume24h = item.baseVolume.ToDecimal();
                    volume.Volume24hUSDT = item.quoteVolume.ToDecimal();
                    volume.TimeUpdate = TimeManager.GetDateTimeFromTimeStamp((long)item.ts.ToDecimal());

                    Volume24hUpdateEvent?.Invoke(volume);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private DateTime _lastTimeMd;

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent;

        #endregion

        #region 11 Trade

        private RateGate _rateGateOrder = new RateGate(1, TimeSpan.FromMilliseconds(100));

        public void SendOrder(Order order)
        {
            try
            {
                _rateGateOrder.WaitToProceed();

                Dictionary<string, dynamic> jsonContent = new Dictionary<string, dynamic>();

                jsonContent.Add("symbol", order.SecurityNameCode);
                jsonContent.Add("side", order.Side == Side.Buy ? "buy" : "sell");
                jsonContent.Add("orderType", order.TypeOrder.ToString().ToLower());
                jsonContent.Add("price", order.Price.ToString().Replace(",", "."));
                jsonContent.Add("size", order.Volume.ToString().Replace(",", "."));
                jsonContent.Add("clientOid", order.NumberUser);
                jsonContent.Add("force", "gtc");

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                IRestResponse responseMessage = CreatePrivateQueryOrders("/api/v2/spot/trade/place-order", Method.POST, null, jsonRequest);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<object> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<object>());

                    if (stateResponse.code.Equals("00000") == true)
                    {
                        // ignore
                    }
                    else
                    {
                        CreateOrderFail(order);
                        SendLogMessage($"Order Fail. {stateResponse.code} Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    CreateOrderFail(order);
                    SendLogMessage($"Order Fail. Code: {responseMessage.StatusCode} || {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        public bool CancelOrder(Order order)
        {
            try
            {
                _rateGateOrder.WaitToProceed();

                Dictionary<string, string> jsonContent = new Dictionary<string, string>();

                jsonContent.Add("symbol", order.SecurityNameCode);
                jsonContent.Add("orderId", order.NumberMarket);

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                IRestResponse response = CreatePrivateQueryOrders("/api/v2/spot/trade/cancel-order", Method.POST, null, jsonRequest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<object> stateResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<object>());

                    if (stateResponse.code.Equals("00000") == true)
                    {
                        return true;
                    }
                    else
                    {
                        OrderStateType state = GetOrderStatus(order);

                        if (state == OrderStateType.None)
                        {
                            SendLogMessage($"Cancel order failed. {stateResponse.code} Message: {stateResponse.msg}", LogMessageType.Error);
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
                        SendLogMessage($"Cancel order failed. Code: {response.StatusCode} || {response.Content}", LogMessageType.Error);

                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
            return false;
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            try
            {
                _rateGateOrder.WaitToProceed();

                Dictionary<string, string> jsonContent = new Dictionary<string, string>();

                jsonContent.Add("symbol", security.Name);

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                CreatePrivateQueryOrders("/api/v2/spot/trade/cancel-symbol-order", Method.POST, null, jsonRequest);
            }
            catch (Exception e)
            {
                SendLogMessage(e.Message, LogMessageType.Error);
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
            List<Order> ordersOpenAll = GetAllActivOrdersArray(100);

            for (int i = 0; i < ordersOpenAll.Count; i++)
            {
                if (MyOrderEvent != null)
                {
                    MyOrderEvent(ordersOpenAll[i]);
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

        public void GetAllOpenOrders(List<Order> array, int maxCount)
        {
            _rateGateOrder.WaitToProceed();

            try
            {
                IRestResponse responseMessage = CreatePrivateQuery($"/api/v2/spot/trade/unfilled-orders?limit=100", Method.GET, null, null);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<RestMessageOrders>> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<List<RestMessageOrders>>());

                    if (stateResponse.code.Equals("00000") == true)
                    {
                        if (stateResponse.data == null)
                        {
                            return;
                        }

                        List<Order> orders = new List<Order>();

                        for (int ind = 0; ind < stateResponse.data.Count; ind++)
                        {
                            RestMessageOrders item = stateResponse.data[ind];

                            Order newOrder = new Order();

                            OrderStateType stateType = GetOrderState(item.status);

                            newOrder.SecurityNameCode = item.symbol;
                            newOrder.SecurityClassCode = "Spot";
                            newOrder.State = stateType;
                            newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.cTime));
                            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.uTime));

                            if (newOrder.State == OrderStateType.Cancel)
                            {
                                newOrder.TimeCancel = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.uTime));
                            }

                            if (newOrder.State == OrderStateType.Done)
                            {
                                newOrder.TimeDone = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.uTime));
                            }

                            try
                            {
                                newOrder.NumberUser = Convert.ToInt32(item.clientOid);
                            }
                            catch
                            {

                            }

                            newOrder.NumberMarket = item.orderId.ToString();
                            newOrder.Side = item.side == "buy" ? Side.Buy : Side.Sell;
                            newOrder.Volume = item.size.ToDecimal();
                            newOrder.Price = item.priceAvg.ToDecimal();
                            newOrder.ServerType = ServerType.BitGetSpot;
                            newOrder.PortfolioNumber = "BitGetSpot";
                            newOrder.TypeOrder = OrderPriceType.Limit;

                            orders.Add(newOrder);
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
                        SendLogMessage($"Get all open orders request error. {responseMessage.StatusCode} || {responseMessage.Content}", LogMessageType.Error);
                        return;
                    }
                }
                else
                {
                    SendLogMessage($"Get all open orders request error. Code: {responseMessage.StatusCode} || {responseMessage.Content}", LogMessageType.Error);
                    return;
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.Message, LogMessageType.Error);
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

                // check trades

                if (myOrder.State == OrderStateType.Partial
                    || myOrder.State == OrderStateType.Done)
                {
                    FindMyTradesToOrder(myOrder);
                }

                return myOrder.State;
            }
            catch (Exception ex)
            {
                SendLogMessage($"GetOrderStatus>. Order error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }

            return OrderStateType.None;
        }

        private void FindMyTradesToOrder(Order order)
        {
            _rateGateOrder.WaitToProceed();

            try
            {
                string path = $"/api/v2/spot/trade/fills?symbol={order.SecurityNameCode}";

                IRestResponse responseMessage = CreatePrivateQuery(path, Method.GET, null, null);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    RestMyTradesResponce stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new RestMyTradesResponce());

                    if (stateResponse.code.Equals("00000") == true)
                    {
                        for (int i = 0; i < stateResponse.data.Count; i++)
                        {
                            if (stateResponse.data[i].orderId != order.NumberMarket)
                            {
                                continue;
                            }

                            DataMyTrades item = stateResponse.data[i];

                            MyTrade myTrade = new MyTrade();
                            myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.cTime));
                            myTrade.NumberOrderParent = item.orderId.ToString();
                            myTrade.NumberTrade = item.tradeId;
                            myTrade.Price = item.priceAvg.ToDecimal();
                            myTrade.SecurityNameCode = item.symbol.ToUpper();
                            myTrade.Side = item.side == "buy" ? Side.Buy : Side.Sell;

                            myTrade.Volume = item.size.ToDecimal();

                            if (string.IsNullOrEmpty(item.feeDetail.feeCoin) == false
                            && string.IsNullOrEmpty(item.feeDetail.totalFee) == false
                            && item.feeDetail.totalFee.ToDecimal() != 0)
                            {
                                if (myTrade.SecurityNameCode.StartsWith(item.feeDetail.feeCoin))
                                {
                                    myTrade.Volume = Math.Round(item.size.ToDecimal() + item.feeDetail.totalFee.ToDecimal(), 6, MidpointRounding.AwayFromZero);
                                }
                                else
                                {
                                    myTrade.Volume = item.size.ToDecimal();
                                }
                            }
                            else
                            {
                                myTrade.Volume = item.size.ToDecimal();
                            }

                            MyTradeEvent(myTrade);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Get order status. {responseMessage.StatusCode} || {responseMessage.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Order trade request error. {responseMessage.StatusCode} || {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.Message, LogMessageType.Error);
            }
        }

        private OrderStateType GetOrderState(string orderStateResponse)
        {
            OrderStateType stateType;

            switch (orderStateResponse)
            {
                case ("live"):
                    stateType = OrderStateType.Active;
                    break;
                case ("partially_filled"):
                    stateType = OrderStateType.Partial;
                    break;
                case ("filled"):
                    stateType = OrderStateType.Done;
                    break;
                case ("cancelled"):
                    stateType = OrderStateType.Cancel;
                    break;
                default:
                    stateType = OrderStateType.None;
                    break;
            }

            return stateType;
        }

        private void CreateOrderFail(Order order)
        {
            order.State = OrderStateType.Fail;

            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
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
            _rateGateOrder.WaitToProceed();

            try
            {
                IRestResponse responseMessage = CreatePrivateQuery($"/api/v2/spot/trade/history-orders?limit=100", Method.GET, null, null);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<RestMessageOrders>> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<List<RestMessageOrders>>());

                    if (stateResponse.code.Equals("00000") == true)
                    {
                        if (stateResponse.data == null)
                        {
                            return;
                        }

                        List<Order> orders = new List<Order>();

                        for (int j = 0; j < stateResponse.data.Count; j++)
                        {
                            RestMessageOrders item = stateResponse.data[j];

                            Order newOrder = new Order();

                            OrderStateType stateType = GetOrderState(item.status);

                            newOrder.SecurityNameCode = item.symbol;
                            newOrder.SecurityClassCode = "Spot";
                            newOrder.State = stateType;
                            newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.cTime));
                            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.uTime));

                            if (newOrder.State == OrderStateType.Cancel)
                            {
                                newOrder.TimeCancel = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.uTime));
                            }

                            if (newOrder.State == OrderStateType.Done)
                            {
                                newOrder.TimeDone = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.uTime));
                            }

                            try
                            {
                                newOrder.NumberUser = Convert.ToInt32(item.clientOid);
                            }
                            catch
                            {

                            }

                            newOrder.NumberMarket = item.orderId.ToString();
                            newOrder.Side = item.side == "buy" ? Side.Buy : Side.Sell;
                            newOrder.Volume = item.size.ToDecimal();
                            newOrder.Price = item.price.ToDecimal();
                            newOrder.ServerType = ServerType.BitGetSpot;
                            newOrder.PortfolioNumber = "BitGetSpot";
                            newOrder.TypeOrder = OrderPriceType.Limit;

                            orders.Add(newOrder);
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
                        SendLogMessage($"Get all historical orders request error. {responseMessage.StatusCode} || {responseMessage.Content}", LogMessageType.Error);
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

        private IRestResponse CreatePrivateQuery(string path, Method method, string queryString, string body)
        {
            try
            {
                RestRequest requestRest = new RestRequest(path, method);

                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string signature = GenerateSignature(timestamp, method.ToString(), path, queryString, body, SeckretKey);

                requestRest.AddHeader("ACCESS-KEY", PublicKey);
                requestRest.AddHeader("ACCESS-SIGN", signature);
                requestRest.AddHeader("ACCESS-TIMESTAMP", timestamp);
                requestRest.AddHeader("ACCESS-PASSPHRASE", Passphrase);
                requestRest.AddHeader("X-CHANNEL-API-CODE", "6yq7w");

                RestClient client = new RestClient(BaseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.Content.Contains("\"Request timestamp expired\"")
                    || response.Content == "")
                {
                    Disconnect();
                }

                return response;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        private IRestResponse CreatePrivateQueryOrders(string path, Method method, string queryString, string body)
        {
            try
            {
                RestRequest requestRest = new RestRequest(path, method);

                string requestPath = path;
                string url = $"{BaseUrl}{requestPath}";
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string signature = GenerateSignature(timestamp, method.ToString(), requestPath, queryString, body, SeckretKey);

                requestRest.AddHeader("ACCESS-KEY", PublicKey);
                requestRest.AddHeader("ACCESS-SIGN", signature);
                requestRest.AddHeader("ACCESS-TIMESTAMP", timestamp);
                requestRest.AddHeader("ACCESS-PASSPHRASE", Passphrase);
                requestRest.AddHeader("X-CHANNEL-API-CODE", "6yq7w");

                if (method.ToString().Equals("POST"))
                {
                    requestRest.AddParameter("application/json", body, ParameterType.RequestBody);
                }

                RestClient client = new RestClient(BaseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.Content.Contains("\"Request timestamp expired\"")
                    || response.Content == "")
                {
                    Disconnect();
                }

                return response;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        private string GenerateSignature(string timestamp, string method, string requestPath, string queryString, string body, string secretKey)
        {
            method = method.ToUpper();
            body = string.IsNullOrEmpty(body) ? string.Empty : body;
            queryString = string.IsNullOrEmpty(queryString) ? string.Empty : "?" + queryString;

            string preHash = timestamp + method + requestPath + queryString + body;

            byte[] secretKeyBytes = Encoding.UTF8.GetBytes(secretKey);
            using (HMACSHA256 hmac = new HMACSHA256(secretKeyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(preHash));
                return Convert.ToBase64String(hashBytes);
            }
        }

        #endregion

        #region 13 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}