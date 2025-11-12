/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Bitfinex.Json;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using OsEngine.Entity.WebSocketOsEngine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using System.Globalization;

namespace OsEngine.Market.Servers.Bitfinex.BitfinexFutures
{
    public class BitfinexFuturesServer : AServer
    {
        public BitfinexFuturesServer(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            BitfinexFuturesServerRealization realization = new BitfinexFuturesServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
        }
    }

    public class BitfinexFuturesServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public BitfinexFuturesServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread threadForPublicMessagesMarketDepths = new Thread(PublicMessageMarketDepthsReader);
            threadForPublicMessagesMarketDepths.IsBackground = true;
            threadForPublicMessagesMarketDepths.Name = "PublicMarketDepthsMessageReaderBitfinex";
            threadForPublicMessagesMarketDepths.Start();

            Thread threadForPublicTradesMessages = new Thread(PublicMessageTradesReader);
            threadForPublicTradesMessages.IsBackground = true;
            threadForPublicTradesMessages.Name = "PublicTradeMessageReaderBitfinex";
            threadForPublicTradesMessages.Start();

            Thread threadForPrivateMessages = new Thread(PrivateMessageReader);
            threadForPrivateMessages.IsBackground = true;
            threadForPrivateMessages.Name = "PrivateMessageReaderBitfinex";
            threadForPrivateMessages.Start();

            Thread threadCheckAliveWebSocket = new Thread(CheckAliveWebSocket);
            threadCheckAliveWebSocket.IsBackground = true;
            threadCheckAliveWebSocket.Name = "CheckAliveWebSocket";
            threadCheckAliveWebSocket.Start();

            Thread threadGetPortfolios = new Thread(ThreadGetPortfolios);
            threadGetPortfolios.IsBackground = true;
            threadGetPortfolios.Name = "ThreadBitfinexFuturesPortfolio";
            threadGetPortfolios.Start();
        }

        public DateTime ServerTime { get; set; }

        public void Connect(WebProxy proxy = null)
        {
            try
            {
                _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
                _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

                if (string.IsNullOrEmpty(_publicKey) || string.IsNullOrEmpty(_secretKey))
                {
                    SendLogMessage("Error:Invalid public or secret key.", LogMessageType.Error);
                    return;
                }

                string _apiPath = "v2/platform/status";

                IRestResponse response = CreatePublicQuery(_apiPath, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string responseBody = response.Content;

                    if (responseBody.Contains("1"))
                    {
                        FIFOListWebSocketPublicMarketDepthsMessage = new ConcurrentQueue<string>();
                        FIFOListWebSocketPublicTradesMessage = new ConcurrentQueue<string>();
                        FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();
                        CreatePublicWebSocketMarketDepthsConnect();
                        CreatePublicWebSocketTradesConnect();
                        CreatePrivateWebSocketConnect();
                        CheckActivationSockets();

                        SendLogMessage("Start Bitfinex Connection", LogMessageType.System);
                    }
                    else if (responseBody.Contains("0"))
                    {
                        SendLogMessage("Status: Maintenance mode", LogMessageType.System);
                    }
                }
                else
                {
                    SendLogMessage($"No connection to Bitfinex server. Code:{response.StatusCode}, Error:{response.Content}", LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Connection cannot be open Bitfinex. exception:" + exception.ToString(), LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public void Dispose()
        {
            try
            {
                UnsubscribeFromAllChannels();

                _securities?.Clear();
                _portfolios?.Clear();
                _subscribedSecurities?.Clear();

                DeleteWebSocketConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage("Dispose method error: " + exception.ToString(), LogMessageType.System);
            }

            FIFOListWebSocketPublicMarketDepthsMessage = null;
            FIFOListWebSocketPublicTradesMessage = null;
            FIFOListWebSocketPrivateMessage = null;

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
            get { return ServerType.BitfinexFutures; }
        }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region 2 Properties 

        public List<IServerParameter> ServerParameters { get; set; }

        public ServerConnectStatus ServerStatus { get; set; }

        private string _publicKey = "";

        private string _secretKey = "";

        private string _baseUrl = "https://api.bitfinex.com";

        #endregion

        #region 3 Securities

        public Dictionary<string, decimal> minSizes = new Dictionary<string, decimal>();

        public event Action<List<Security>> SecurityEvent;

        private RateGate _rateGateSecurity = new RateGate(1, TimeSpan.FromMilliseconds(2100));

        private List<Security> _securities = new List<Security>();

        public void GetSecurities()
        {
            try
            {
                _rateGateSecurity.WaitToProceed();

                RequestMinSizes();

                string _apiPath = "v2/status/deriv?keys=ALL";


                IRestResponse response = CreatePublicQuery(_apiPath, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string jsonResponse = response.Content;

                    List<List<string>> securityList = JsonConvert.DeserializeObject<List<List<string>>>(jsonResponse);

                    if (securityList == null)
                    {
                        SendLogMessage("GetSecurities> Deserialization resulted in null", LogMessageType.Error);
                        return;
                    }

                    if (securityList.Count > 0)
                    {
                        SendLogMessage("Securities loaded. Count: " + securityList.Count, LogMessageType.System);
                        SecurityEvent?.Invoke(_securities);
                    }

                    List<Security> securities = new List<Security>();

                    for (int i = 0; i < securityList.Count; i++)
                    {
                        List<string> item = securityList[i];

                        string symbol = item[0]?.ToString();
                        string derevativePrice = item[3]?.ToString()?.Replace('.', ',');

                        if (symbol.Contains("TEST"))
                        {
                            continue;
                        }

                        if (symbol.Contains("USTF0"))
                        {
                            Security newSecurity = new Security();

                            newSecurity.Exchange = ServerType.BitfinexFutures.ToString();
                            newSecurity.Name = symbol;
                            newSecurity.NameFull = symbol;
                            newSecurity.NameClass = GetNameClass(symbol);
                            newSecurity.NameId = symbol;
                            newSecurity.SecurityType = SecurityType.Futures;
                            newSecurity.Lot = 1;
                            newSecurity.State = SecurityStateType.Activ;
                            newSecurity.Decimals = derevativePrice.DecimalsCount() == 0 ? 1 : derevativePrice.DecimalsCount();
                            newSecurity.PriceStep = newSecurity.Decimals.GetValueByDecimals();
                            newSecurity.MinTradeAmountType = MinTradeAmountType.Contract;
                            newSecurity.VolumeStep = newSecurity.DecimalsVolume.GetValueByDecimals();

                            if (newSecurity.PriceStep == 0)
                            {
                                newSecurity.PriceStep = 1;
                            }

                            newSecurity.PriceStepCost = newSecurity.PriceStep;
                            newSecurity.DecimalsVolume = DigitsAfterComma(derevativePrice);
                            newSecurity.MinTradeAmount = GetMinSize(symbol);

                            securities.Add(newSecurity);
                        }
                    }
                    if (SecurityEvent != null)
                    {
                        SecurityEvent(securities);
                    }
                }
                else
                {
                    SendLogMessage($"Securities request error. Code:{response.StatusCode}, Error:{response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Securities request exception" + exception.ToString(), LogMessageType.Error);
            }
        }

        private int DigitsAfterComma(string valueNumber)
        {
            int commaPosition = valueNumber.IndexOf(',');
            int digitsAfterComma = valueNumber.Length - commaPosition - 1;

            return digitsAfterComma;
        }

        public void RequestMinSizes()
        {
            _rateGateSecurity.WaitToProceed();

            try
            {
                string _apiPath = "/v2/conf/pub:info:pair:futures";

                IRestResponse response = CreatePublicQuery(_apiPath, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(response.Content))
                {
                    List<List<object>> data = JsonConvert.DeserializeObject<List<List<object>>>(response.Content);

                    for (int i = 0; i < data.Count; i++)
                    {
                        List<object> subArray = data[i];

                        for (int j = 0; j < subArray.Count; j++)
                        {
                            object[] pairData = JsonConvert.DeserializeObject<object[]>(subArray[j]?.ToString());
                            string pair = "t" + (pairData[0]?.ToString() ?? "");
                            object[] pairData2 = JsonConvert.DeserializeObject<object[]>(pairData[1]?.ToString());

                            string minSizeString = pairData2[3].ToString();
                            decimal minSize;

                            if (decimal.TryParse(minSizeString, System.Globalization.NumberStyles.Float,
                                        System.Globalization.CultureInfo.InvariantCulture, out minSize))
                            {
                                minSizes.Add(pair, minSize);
                            }
                        }
                    }
                }
                else
                {
                    SendLogMessage($"Failed to request min sizes. Code:{response.StatusCode}, Error:{response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error : {ex.Message}", LogMessageType.Error);
            }
        }

        private string GetNameClass(string security)
        {
            if (security.EndsWith("USTF0"))
            {
                return "USDT";
            }

            return "Futures";
        }

        public decimal GetMinSize(string symbol)
        {
            if (minSizes.TryGetValue(symbol, out decimal minSize))
            {
                return minSize;
            }

            return 1;
        }

        #endregion

        #region 4 Portfolios

        private List<Portfolio> _portfolios = new List<Portfolio>();

        public event Action<List<Portfolio>> PortfolioEvent;

        private RateGate _rateGatePortfolio = new RateGate(1, TimeSpan.FromMilliseconds(690));

        public List<Portfolio> Portfolios;

        private void ThreadGetPortfolios()
        {
            _rateGatePortfolio.WaitToProceed();

            Thread.Sleep(10000);

            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(3000);
                    continue;
                }
                try
                {
                    Thread.Sleep(5000);

                    CreateQueryPortfolio(false);
                    CreateQueryPositions(false);

                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                }
            }
        }

        public void GetPortfolios()
        {
            if (Portfolios == null)
            {
                Portfolios = new List<Portfolio>();

                Portfolio portfolioInitial = new Portfolio
                {
                    Number = "BitfinexFuturesPortfolio",
                    ValueBegin = 1,
                    ValueCurrent = 1,
                    ValueBlocked = 0,
                    ServerType = ServerType.BitfinexFutures
                };

                Portfolios.Add(portfolioInitial);
            }

            CreateQueryPortfolio(true);
            CreateQueryPositions(true);

            PortfolioEvent?.Invoke(Portfolios);
        }

        public void CreateQueryPortfolio(bool updateValueBegin)
        {
            try
            {
                string path = "v2/auth/r/wallets";

                IRestResponse response = CreatePrivateQuery(path, Method.POST, null);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    List<List<object>> wallets = JsonConvert.DeserializeObject<List<List<object>>>(response.Content);

                    Portfolio portfolio = new Portfolio
                    {
                        Number = "BitfinexFuturesPortfolio",
                        ServerType = ServerType.BitfinexFutures,
                        ValueBegin = 1,
                        ValueCurrent = 1
                    };

                    for (int i = 0; i < wallets.Count; i++)
                    {
                        List<object> wallet = wallets[i];

                        if (wallet[0]?.ToString() == "margin")
                        {
                            PositionOnBoard position = new PositionOnBoard();

                            position.PortfolioName = "BitfinexFuturesPortfolio";
                            position.SecurityNameCode = wallet[1]?.ToString();
                            position.ValueBegin = Math.Round(wallet[2]?.ToString().ToDecimal() ?? 1, 4);
                            position.ValueCurrent = Math.Round(wallet[4]?.ToString().ToDecimal() ?? 1, 4);
                            position.ValueBlocked = (wallet[4] != null)
                            ? Math.Round(wallet[2].ToString().ToDecimal() - wallet[4].ToString().ToDecimal(), 4)
                            : 0;

                            portfolio.SetNewPosition(position);
                        }
                    }

                    _portfolios.Clear();
                    _portfolios.Add(portfolio);

                    PortfolioEvent?.Invoke(_portfolios);
                }
                else
                {
                    SendLogMessage($"Wallet request failed: {response.StatusCode}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("CreateQueryPortfolio error: " + ex.Message, LogMessageType.Error);
            }
        }

        private RateGate _rateGatePositions = new RateGate(1, TimeSpan.FromMilliseconds(706));

        public void CreateQueryPositions(bool updateValueBegin)
        {
            _rateGatePositions.WaitToProceed();

            try
            {
                if (_portfolios == null || _portfolios.Count == 0)
                {
                    return;
                }

                string path = "v2/auth/r/positions";

                IRestResponse response = CreatePrivateQuery(path, Method.POST, null);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    List<List<object>> positionsRaw = JsonConvert.DeserializeObject<List<List<object>>>(response.Content);

                    if (positionsRaw == null || positionsRaw.Count == 0)
                    {
                        return;
                    }

                    Portfolio portfolio = _portfolios[0];

                    List<PositionOnBoard> positions = portfolio.GetPositionOnBoard();

                    for (int i = 0; i < positionsRaw.Count; i++)
                    {
                        List<object> pos = positionsRaw[i];

                        var position = new PositionOnBoard();

                        position.PortfolioName = "BitfinexFuturesPortfolio";
                        position.SecurityNameCode = pos[0]?.ToString();
                        position.ValueCurrent = Math.Round(pos[2]?.ToString().ToDecimal() ?? 1, 4);
                        position.UnrealizedPnl = Math.Round(pos[6]?.ToString().ToDecimal() ?? 0, 4);

                        portfolio.SetNewPosition(position);

                    }
                    if (_portfolios != null && _portfolios.Count > 0)
                    {
                        PortfolioEvent.Invoke(_portfolios);
                    }
                }
                else
                {
                    SendLogMessage($"Positions request failed: {response.StatusCode}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("CreateQueryPositions error: " + ex.Message, LogMessageType.Error);
            }
        }

        #endregion

        #region 5 Data

        private HashSet<(string security, DateTime start, DateTime end)> _loadedIntervals = new HashSet<(string, DateTime, DateTime)>();

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
            endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
            actualTime = DateTime.SpecifyKind(actualTime, DateTimeKind.Utc);

            int limit = 4990;

            List<Trade> trades = new List<Trade>();

            if (endTime > DateTime.UtcNow)
            {
                endTime = DateTime.UtcNow;
            }

            if (!CheckTime(startTime, endTime, actualTime))
            {
                return null;
            }

            TimeSpan additionTime = TimeSpan.FromMinutes(1440);

            DateTime nextStartTime = startTime;

            while (nextStartTime < endTime)
            {
                DateTime nextEndTime = nextStartTime.Add(additionTime);

                if (nextEndTime > endTime)
                {
                    nextEndTime = endTime;
                }

                var intervalKey = (security.Name, nextStartTime, nextEndTime);

                if (_loadedIntervals.Contains(intervalKey))
                {

                    trades.RemoveAll(t => t.Time >= nextStartTime && t.Time <= nextEndTime);

                    _loadedIntervals.Remove(intervalKey);
                }

                List<Trade> newTrades = GetTrades(security.Name, limit, nextStartTime, nextEndTime);

                if (newTrades == null
                    || newTrades.Count == 0)
                {
                    return null;
                }
                else
                {
                    trades.AddRange(newTrades);

                    _loadedIntervals.Add(intervalKey);
                }

                nextStartTime = nextEndTime.AddMinutes(1);
            }

            trades.Sort((a, b) => a.Time.CompareTo(b.Time));

            for (int i = trades.Count - 1; i > 0; i--)
            {
                Trade tradeNow = trades[i];
                Trade tradeLast = trades[i - 1];

                if (tradeLast.Time == tradeNow.Time)
                {
                    trades.RemoveAt(i);
                }
            }

            return trades;
        }

        private RateGate _rateGateTrades = new RateGate(1, TimeSpan.FromMilliseconds(4500));

        private List<Trade> GetTrades(string security, int limit, DateTime startTime, DateTime endTime)
        {
            try
            {
                _rateGateTrades.WaitToProceed();

                long startDate = new DateTimeOffset(startTime).ToUnixTimeMilliseconds();
                long endDate = new DateTimeOffset(endTime).ToUnixTimeMilliseconds();

                string _apiPath = $"/v2/trades/{security}/hist?limit={limit}&start={startDate}&end={endDate}";

                IRestResponse response = CreatePublicQuery(_apiPath, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    List<List<object>> tradeList = JsonConvert.DeserializeObject<List<List<object>>>(response.Content);

                    List<Trade> trades = new List<Trade>();

                    if (tradeList == null
                        || tradeList.Count == 0)
                    {
                        return trades;
                    }

                    DateTime lastTime = DateTime.MinValue;

                    for (int i = 0; i < tradeList.Count; i++)
                    {
                        DateTime tradeTime = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(tradeList[i][1]));

                        Trade newTrade = new Trade();

                        newTrade.Id = tradeList[i][0].ToString();
                        newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(tradeList[i][1]));
                        newTrade.SecurityNameCode = security;
                        decimal amount = tradeList[i][2].ToString().ToDecimal();
                        newTrade.Volume = Math.Abs(amount);
                        newTrade.Price = (tradeList[i][3]).ToString().ToDecimal();
                        newTrade.Side = amount > 0 ? Side.Buy : Side.Sell;

                        trades.Add(newTrade);
                    }

                    return trades;
                }
                else
                {
                    SendLogMessage($"The request returned an error. {response.StatusCode} - {response.Content}", LogMessageType.Error);

                    return new List<Trade>();
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);

                return new List<Trade>();
            }
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
            DateTime timeEnd = DateTime.UtcNow;
            DateTime timeStart = timeEnd.AddMinutes(-tfTotalMinutes * candleCount);

            return GetCandleDataToSecurity(security, timeFrameBuilder, timeStart, timeEnd, timeStart);
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

            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

            if (!CheckTf(tfTotalMinutes))
            {
                return null;
            }

            if (endTime > DateTime.UtcNow)
            {
                endTime = DateTime.UtcNow;
            }

            if (!CheckTime(startTime, endTime, actualTime))
            {
                return null;
            }

            int countNeedToLoad = GetCountCandlesFromPeriod(startTime, endTime, timeFrameBuilder.TimeFrameTimeSpan);

            return GetCandleHistory(security.NameFull, timeFrameBuilder.TimeFrameTimeSpan, true, countNeedToLoad, endTime);
        }

        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf, bool isOsData, int countToLoad, DateTime timeEnd)
        {
            int limit = 4990;

            List<Candle> allCandles = new List<Candle>();

            DateTime startTime = timeEnd - TimeSpan.FromMinutes(tf.TotalMinutes * countToLoad);
            HashSet<DateTime> uniqueTimes = new HashSet<DateTime>();

            int candlesLoaded = 0;
            string timeFrame = GetInterval(tf);

            DateTime periodEnd = startTime;

            while (candlesLoaded < countToLoad && periodEnd < timeEnd)
            {
                int candlesToLoad = Math.Min(limit, countToLoad - candlesLoaded);
                DateTime periodStart = startTime;

                periodEnd = periodStart.AddMinutes(tf.TotalMinutes * candlesToLoad);

                if (periodEnd > DateTime.UtcNow)
                {
                    periodEnd = DateTime.UtcNow;
                }

                List<Candle> rangeCandles = CreateQueryCandles(nameSec, timeFrame, periodStart, periodEnd, candlesToLoad);

                if (rangeCandles == null)
                {
                    return null;
                }

                if (rangeCandles.Count == 0)
                {
                    return null;
                }

                for (int i = 0; i < rangeCandles.Count; i++)
                {
                    if (uniqueTimes.Add(rangeCandles[i].TimeStart))
                    {
                        allCandles.Add(rangeCandles[i]);
                    }
                }

                int actualCandlesLoaded = rangeCandles.Count;

                candlesLoaded += actualCandlesLoaded;
                startTime = allCandles[allCandles.Count - 1].TimeStart;

                if (periodEnd >= timeEnd)
                {
                    break;
                }
            }

            for (int i = allCandles.Count - 1; i >= 0; i--)
            {
                if (allCandles[i].TimeStart > timeEnd)
                {
                    allCandles.RemoveAt(i);
                }
            }

            for (int i = allCandles.Count - 1; i > 0; i--)
            {
                if (allCandles[i].TimeStart == allCandles[i - 1].TimeStart)
                {
                    allCandles.RemoveAt(i);
                }
            }

            return allCandles;
        }

        private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (startTime >= endTime ||
                startTime >= DateTime.UtcNow ||
                actualTime > endTime ||
                actualTime > DateTime.UtcNow)
            {
                SendLogMessage("Error: The date is incorrect", LogMessageType.User);
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
                timeFrameMinutes == 240 ||
                timeFrameMinutes == 1440)
            {
                return true;
            }
            return false;
        }

        private string GetInterval(TimeSpan tf)
        {
            if (tf.Days > 0)
            {
                return $"{tf.Days}D";
            }
            else if (tf.Hours > 0)
            {
                return $"{tf.Hours}h";
            }
            else if (tf.Minutes > 0)
            {
                return $"{tf.Minutes}m";
            }
            else
            {
                SendLogMessage("Error:The timeframe is incorrect", LogMessageType.User);
                return null;
            }
        }

        private int GetCountCandlesFromPeriod(DateTime startTime, DateTime endTime, TimeSpan tf)
        {
            TimeSpan timePeriod = endTime - startTime;

            if (tf.Days > 0)
            {
                return Convert.ToInt32(timePeriod.TotalDays / tf.TotalDays);
            }
            else if (tf.Hours > 0)
            {
                return Convert.ToInt32(timePeriod.TotalHours / tf.TotalHours);
            }
            else if (tf.Minutes > 0)
            {
                return Convert.ToInt32(timePeriod.TotalMinutes / tf.TotalMinutes);
            }
            else
            {
                SendLogMessage(" Timeframe must be defined in days, hours, or minutes.", LogMessageType.Error);
            }

            return 0;
        }

        private RateGate _rateGateCandleHistory = new RateGate(1, TimeSpan.FromMilliseconds(2100));

        private List<Candle> CreateQueryCandles(string nameSec, string tf, DateTime startTime, DateTime endTime, int limit)
        {
            _rateGateCandleHistory.WaitToProceed();

            try
            {
                long startDate = TimeManager.GetTimeStampMilliSecondsToDateTime(startTime);
                long endDate = TimeManager.GetTimeStampMilliSecondsToDateTime(endTime);

                string _apiPath = $"/v2/candles/trade:{tf}:{nameSec}/hist?sort=1&start={startDate}&end={endDate}&limit={limit}";

                IRestResponse response = CreatePublicQuery(_apiPath, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string jsonResponse = response.Content;

                    List<List<string>> candles = JsonConvert.DeserializeObject<List<List<string>>>(jsonResponse);

                    if (candles == null
                        || candles.Count == 0)
                    {
                        return null;
                    }

                    List<BitfinexFuturesCandle> candleList = new List<BitfinexFuturesCandle>();

                    for (int i = 0; i < candles.Count; i++)
                    {
                        List<string> candleData = candles[i];

                        BitfinexFuturesCandle newCandle = new BitfinexFuturesCandle();

                        newCandle.Mts = candleData[0].ToString();
                        newCandle.Open = candleData[1].ToString();
                        newCandle.Close = candleData[2].ToString();
                        newCandle.High = candleData[3].ToString();
                        newCandle.Low = candleData[4].ToString();
                        newCandle.Volume = candleData[5].ToString();

                        candleList.Add(newCandle);
                    }

                    return ConvertToCandles(candleList);
                }
                else
                {
                    SendLogMessage($"Failed to query candles. Code:{response.StatusCode}, Error:{response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Request error{exception.Message}", LogMessageType.Error);
            }

            return null;
        }

        private List<Candle> ConvertToCandles(List<BitfinexFuturesCandle> candleList)
        {
            List<Candle> candles = new List<Candle>();

            try
            {
                for (int i = 0; i < candleList.Count; i++)
                {
                    BitfinexFuturesCandle candle = candleList[i];

                    try
                    {
                        if (string.IsNullOrEmpty(candle.Mts) || string.IsNullOrEmpty(candle.Open) ||
                            string.IsNullOrEmpty(candle.Close) || string.IsNullOrEmpty(candle.High) ||
                            string.IsNullOrEmpty(candle.Low) || string.IsNullOrEmpty(candle.Volume))
                        {
                            SendLogMessage("Candle data contains null or empty values", LogMessageType.Error);
                            continue;
                        }

                        if ((candle.Open).ToDecimal() == 0 || (candle.Close).ToDecimal() == 0 ||
                            (candle.High).ToDecimal() == 0 || (candle.Low).ToDecimal() == 0 ||
                            (candle.Volume).ToDecimal() == 0)
                        {
                            SendLogMessage("Candle data contains zero values", LogMessageType.Error);
                            continue;
                        }

                        Candle newCandle = new Candle();

                        newCandle.State = CandleState.Finished;
                        newCandle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(candle.Mts));
                        newCandle.Open = candle.Open.ToDecimal();
                        newCandle.Close = candle.Close.ToDecimal();
                        newCandle.High = candle.High.ToDecimal();
                        newCandle.Low = candle.Low.ToDecimal();
                        newCandle.Volume = candle.Volume.ToDecimal();

                        candles.Add(newCandle);
                    }
                    catch (Exception exception)
                    {
                        SendLogMessage($"Format exception: {exception.Message}", LogMessageType.Error);
                    }
                }

                return candles;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        #endregion

        #region  6 WebSocket creation

        private string _webSocketPublicUrl = "wss://api-pub.bitfinex.com/ws/2";

        private string _webSocketPrivateUrl = "wss://api.bitfinex.com/ws/2";

        private string _lockerCheckActivateionSockets = "lockerCheckActivateionSockets";

        private List<WebSocket> _webSocketPublicMarketDepths = new List<WebSocket>();

        private List<WebSocket> _webSocketPublicTrades = new List<WebSocket>();

        private WebSocket _webSocketPrivate;

        private ConcurrentQueue<string> FIFOListWebSocketPublicMarketDepthsMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> FIFOListWebSocketPublicTradesMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        private void CreatePublicWebSocketMarketDepthsConnect()
        {
            try
            {
                if (FIFOListWebSocketPublicMarketDepthsMessage == null)
                {
                    FIFOListWebSocketPublicMarketDepthsMessage = new ConcurrentQueue<string>();
                }

                _webSocketPublicMarketDepths.Add(CreateNewPublicMarketDepthsSocket());
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private WebSocket CreateNewPublicMarketDepthsSocket()
        {
            try
            {
                WebSocket webSocketPublicMarketDepthsNew = new WebSocket(_webSocketPublicUrl);

                webSocketPublicMarketDepthsNew.EmitOnPing = true;
                webSocketPublicMarketDepthsNew.OnOpen += WebSocketPublicMarketDepthsNew_OnOpen;
                webSocketPublicMarketDepthsNew.OnClose += WebSocketPublicMarketDepthsNew_OnClose;
                webSocketPublicMarketDepthsNew.OnMessage += WebSocketPublicMarketDepthsNew_OnMessage;
                webSocketPublicMarketDepthsNew.OnError += WebSocketPublicMarketDepthsNew_OnError;
                webSocketPublicMarketDepthsNew.ConnectAsync();

                return webSocketPublicMarketDepthsNew;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private void CreatePublicWebSocketTradesConnect()
        {
            try
            {
                if (FIFOListWebSocketPublicTradesMessage == null)
                {
                    FIFOListWebSocketPublicTradesMessage = new ConcurrentQueue<string>();
                }

                _webSocketPublicTrades.Add(CreateNewPublicTradesSocket());
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private WebSocket CreateNewPublicTradesSocket()
        {
            try
            {
                WebSocket webSocketPublicTradesNew = new WebSocket(_webSocketPublicUrl);

                webSocketPublicTradesNew.EmitOnPing = true;
                webSocketPublicTradesNew.OnOpen += WebSocketPublicTradesNew_OnOpen;
                webSocketPublicTradesNew.OnClose += WebSocketPublicTradesNew_OnClose;
                webSocketPublicTradesNew.OnMessage += WebSocketPublicTradesNew_OnMessage;
                webSocketPublicTradesNew.OnError += WebSocketPublicTradesNew_OnError;
                webSocketPublicTradesNew.ConnectAsync();

                return webSocketPublicTradesNew;
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
            if (_webSocketPublicMarketDepths != null)
            {
                try
                {
                    for (int i = 0; i < _webSocketPublicMarketDepths.Count; i++)
                    {
                        WebSocket webSocketPublicMarketDepthsNew = _webSocketPublicMarketDepths[i];

                        webSocketPublicMarketDepthsNew.OnOpen -= WebSocketPublicMarketDepthsNew_OnOpen;
                        webSocketPublicMarketDepthsNew.OnClose -= WebSocketPublicMarketDepthsNew_OnClose;
                        webSocketPublicMarketDepthsNew.OnMessage -= WebSocketPublicMarketDepthsNew_OnMessage;
                        webSocketPublicMarketDepthsNew.OnError -= WebSocketPublicMarketDepthsNew_OnError;

                        if (webSocketPublicMarketDepthsNew.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublicMarketDepthsNew.CloseAsync();
                        }
                        webSocketPublicMarketDepthsNew = null;
                    }
                }
                catch
                {
                    // ignore
                }

                _webSocketPublicMarketDepths.Clear();
            }

            if (_webSocketPublicTrades != null)
            {
                try
                {
                    for (int i = 0; i < _webSocketPublicTrades.Count; i++)
                    {
                        WebSocket webSocketPublicTradesNew = _webSocketPublicTrades[i];

                        webSocketPublicTradesNew.OnOpen -= WebSocketPublicTradesNew_OnOpen;
                        webSocketPublicTradesNew.OnClose -= WebSocketPublicTradesNew_OnClose;
                        webSocketPublicTradesNew.OnMessage -= WebSocketPublicTradesNew_OnMessage;
                        webSocketPublicTradesNew.OnError -= WebSocketPublicTradesNew_OnError;

                        if (webSocketPublicTradesNew.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublicTradesNew.CloseAsync();
                        }
                        webSocketPublicTradesNew = null;
                    }
                }
                catch
                {
                    // ignore
                }

                _webSocketPublicTrades.Clear();
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

        #endregion

        #region  7 WebSocket events

        private void WebSocketPublicMarketDepthsNew_OnError(object sender, ErrorEventArgs e)
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

        private void WebSocketPublicMarketDepthsNew_OnMessage(object sender, MessageEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect
                    || e?.Data == null
                    || string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                if (FIFOListWebSocketPublicMarketDepthsMessage == null)
                {
                    return;
                }

                if (e.Data.Contains("pong"))
                { // pong message
                    return;
                }

                if (e.Data.Contains("hb"))
                { // heartbeating
                    return;
                }

                FIFOListWebSocketPublicMarketDepthsMessage?.Enqueue(e.Data);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublicMarketDepthsNew_OnClose(object sender, CloseEventArgs e)
        {
            try
            {
                if (DisconnectEvent != null
                    & ServerStatus != ServerConnectStatus.Disconnect)
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
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void WebSocketPublicMarketDepthsNew_OnOpen(object sender, EventArgs e)
        {
            try
            {
                CheckActivationSockets();

                SendLogMessage("WebSocket public MarketDepths Bitfinex open.", LogMessageType.System);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublicTradesNew_OnError(object sender, ErrorEventArgs e)
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

        private void WebSocketPublicTradesNew_OnMessage(object sender, MessageEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect
                    || e?.Data == null
                    || string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                if (FIFOListWebSocketPublicTradesMessage == null)
                {
                    return;
                }

                if (e.Data.Contains("pong"))
                { // pong message
                    return;
                }

                if (e.Data.Contains("hb"))
                { // heartbeating
                    return;
                }

                FIFOListWebSocketPublicTradesMessage?.Enqueue(e.Data);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublicTradesNew_OnClose(object sender, CloseEventArgs e)
        {
            try
            {
                if (DisconnectEvent != null
                    & ServerStatus != ServerConnectStatus.Disconnect)
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
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void WebSocketPublicTradesNew_OnOpen(object sender, EventArgs e)
        {
            try
            {
                CheckActivationSockets();

                SendLogMessage("WebSocket public Trades Bitfinex open.", LogMessageType.System);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPrivate_Opened(object sender, EventArgs e)
        {
            try
            {
                GenerateAuthenticate();
                CheckActivationSockets();

                SendLogMessage("Connection to private data is Open", LogMessageType.System);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
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

        private void WebSocketPrivate_MessageReceived(object sender, MessageEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect
                    || e?.Data == null
                    || string.IsNullOrEmpty(e?.Data))
                {
                    return;
                }

                if (FIFOListWebSocketPrivateMessage == null)
                {
                    return;
                }

                if (e.Data.Contains("pong"))
                { // pong message
                    return;
                }

                if (e.Data.Contains("hb"))
                { // heartbeating
                    return;
                }

                FIFOListWebSocketPrivateMessage?.Enqueue(e.Data);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void GenerateAuthenticate()
        {
            try
            {
                string nonce = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).ToString();
                string authPayload = "AUTH" + nonce;
                string authSig;

                using (var hmac = new HMACSHA384(Encoding.UTF8.GetBytes(_secretKey)))
                {
                    byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(authPayload));
                    authSig = BitConverter.ToString(hash).Replace("-", "").ToLower();
                }

                var payload = new
                {
                    @event = "auth",
                    apiKey = _publicKey,
                    authSig,
                    authNonce = nonce,
                    authPayload
                };

                string authJson = JsonConvert.SerializeObject(payload);
                _webSocketPrivate.SendAsync(authJson);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void CheckActivationSockets()
        {
            lock (_lockerCheckActivateionSockets)
            {
                try
                {
                    if (_webSocketPrivate == null
                       || _webSocketPrivate?.ReadyState != WebSocketState.Open)
                    {
                        Disconnect();
                        return;
                    }

                    if (_webSocketPublicMarketDepths.Count == 0)
                    {
                        Disconnect();
                        return;
                    }

                    WebSocket webSocketPublicMarketDepths = _webSocketPublicMarketDepths[0];

                    if (webSocketPublicMarketDepths == null
                        || webSocketPublicMarketDepths?.ReadyState != WebSocketState.Open)
                    {
                        Disconnect();
                        return;
                    }

                    if (_webSocketPublicTrades.Count == 0)
                    {
                        Disconnect();
                        return;
                    }

                    WebSocket webSocketPublicTrades = _webSocketPublicTrades[0];

                    if (webSocketPublicTrades == null
                        || webSocketPublicTrades?.ReadyState != WebSocketState.Open)
                    {
                        Disconnect();
                        return;
                    }

                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        ServerStatus = ServerConnectStatus.Connect;
                        ConnectEvent();
                    }

                    SendLogMessage("All sockets activated.", LogMessageType.System);
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
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
                    Thread.Sleep(20000);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        continue;
                    }

                    for (int i = 0; i < _webSocketPublicMarketDepths.Count; i++)
                    {
                        WebSocket webSocketPublicMarketDepths = _webSocketPublicMarketDepths[i];
                        if (webSocketPublicMarketDepths != null
                            && webSocketPublicMarketDepths?.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublicMarketDepths?.SendAsync("{\"event\":\"ping\", \"cid\":1204}");
                        }
                        else
                        {
                            Disconnect();
                        }
                    }

                    for (int i = 0; i < _webSocketPublicTrades.Count; i++)
                    {
                        WebSocket webSocketPublicTrades = _webSocketPublicTrades[i];
                        if (webSocketPublicTrades != null
                            && webSocketPublicTrades?.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublicTrades.SendAsync("{\"event\":\"ping\", \"cid\":1254}");
                        }
                        else
                        {
                            Disconnect();
                        }
                    }

                    if (_webSocketPrivate != null
                        && (_webSocketPrivate.ReadyState == WebSocketState.Open
                    || _webSocketPrivate.ReadyState == WebSocketState.Connecting))
                    {
                        _webSocketPrivate.SendAsync("{\"event\":\"ping\", \"cid\":1274}");
                    }
                    else
                    {
                        Disconnect();
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        #endregion

        #region  9  WebSocket security subscribe

        private List<string> _subscribedSecurities = new List<string>();

        private RateGate _rateGateSubscribed = new RateGate(1, TimeSpan.FromMilliseconds(2500));

        public void Subscribe(Security security)
        {
            try
            {
                _rateGateSubscribed.WaitToProceed();

                CreateSubscribeMessageWebSocket(security);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void CreateSubscribeMessageWebSocket(Security security)
        {
            try
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

                if (_webSocketPublicMarketDepths.Count == 0
                    || _webSocketPublicTrades.Count == 0)
                {
                    return;
                }

                WebSocket webSocketPublicMarketDepths = _webSocketPublicMarketDepths[_webSocketPublicMarketDepths.Count - 1];
                WebSocket webSocketPublicTrades = _webSocketPublicTrades[_webSocketPublicTrades.Count - 1];

                if (webSocketPublicMarketDepths.ReadyState == WebSocketState.Open
                    && webSocketPublicTrades.ReadyState == WebSocketState.Open
                    && _subscribedSecurities.Count != 0
                    && _subscribedSecurities.Count % 15 == 0)
                {
                    // creating a new socket
                    WebSocket newSocketMarketDepths = CreateNewPublicMarketDepthsSocket();
                    WebSocket newSocketTrades = CreateNewPublicTradesSocket();

                    DateTime timeEndMarketDepths = DateTime.Now.AddSeconds(10);
                    while (newSocketMarketDepths.ReadyState != WebSocketState.Open)
                    {
                        Thread.Sleep(500);

                        if (timeEndMarketDepths < DateTime.Now)
                        {
                            break;
                        }
                    }

                    if (newSocketMarketDepths.ReadyState == WebSocketState.Open)
                    {
                        _webSocketPublicMarketDepths.Add(newSocketMarketDepths);
                        webSocketPublicMarketDepths = newSocketMarketDepths;
                    }

                    DateTime timeEndTrades = DateTime.Now.AddSeconds(10);
                    while (newSocketTrades.ReadyState != WebSocketState.Open)
                    {
                        Thread.Sleep(500);

                        if (timeEndTrades < DateTime.Now)
                        {
                            break;
                        }
                    }

                    if (newSocketTrades.ReadyState == WebSocketState.Open)
                    {
                        _webSocketPublicTrades.Add(newSocketTrades);
                        webSocketPublicTrades = newSocketTrades;
                    }
                }

                if (webSocketPublicMarketDepths != null
                    && webSocketPublicTrades != null)
                {
                    webSocketPublicMarketDepths.SendAsync($"{{\"event\":\"subscribe\",\"channel\":\"book\",\"symbol\":\"{security.Name}\",\"prec\":\"P0\",\"freq\":\"F0\",\"len\":\"25\"}}");
                    webSocketPublicTrades.SendAsync($"{{\"event\":\"subscribe\",\"channel\":\"trades\",\"symbol\":\"{security.Name}\"}}");
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UnsubscribeFromAllChannels()
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                for (int i = 0; i < _webSocketPublicMarketDepths.Count; i++)
                {
                    WebSocket webSocketPublicMarketDepths = _webSocketPublicMarketDepths[i];

                    if (webSocketPublicMarketDepths != null && webSocketPublicMarketDepths?.ReadyState == WebSocketState.Open)
                    {
                        int depthCount = _depthDictionary.Count;

                        int k = 0;

                        while (k < depthCount)
                        {
                            int chanId = new List<int>(_depthDictionary.Keys)[k];

                            string message = "{\"event\":\"unsubscribe\",\"chanId\":" + chanId + "}";

                            webSocketPublicMarketDepths.SendAsync(message);

                            k++;
                        }

                        _depthDictionary.Clear();
                    }
                }

                for (int i = 0; i < _webSocketPublicTrades.Count; i++)
                {
                    WebSocket webSocketPublicTrades = _webSocketPublicTrades[i];

                    if (webSocketPublicTrades != null && webSocketPublicTrades?.ReadyState == WebSocketState.Open)
                    {
                        int tradeCount = _tradeDictionary.Count;

                        int j = 0;

                        while (j < tradeCount)
                        {
                            int chanId = new List<int>(_tradeDictionary.Keys)[j];

                            string message = "{\"event\":\"unsubscribe\",\"chanId\":" + chanId + "}";

                            webSocketPublicTrades.SendAsync(message);

                            j++;
                        }

                        _tradeDictionary.Clear();
                    }
                }

                SendLogMessage("All subscriptions have been successfully removed", LogMessageType.System);
            }
            catch (Exception exception)
            {
                SendLogMessage("Error unsubscribing from channels:" + exception.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region  10 WebSocket parsing the messages

        public event Action<Trade> NewTradesEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<Order> MyOrderEvent;

        private int _currentChannelIdDepth;

        private int _channelIdTrade;

        private Dictionary<int, string> _tradeDictionary = new Dictionary<int, string>();

        private Dictionary<int, string> _depthDictionary = new Dictionary<int, string>();

        private void PublicMessageMarketDepthsReader()
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

                    if (FIFOListWebSocketPublicMarketDepthsMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    FIFOListWebSocketPublicMarketDepthsMessage.TryDequeue(out string message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Contains("book"))
                    {
                        BitfinexFuturesSubscriptionResponse responseDepth = JsonConvert.DeserializeObject<BitfinexFuturesSubscriptionResponse>(message);

                        int key = Convert.ToInt32(responseDepth.ChanId);

                        if (!_depthDictionary.ContainsKey(key))
                        {
                            _depthDictionary.Add(key, responseDepth.Symbol);
                            _currentChannelIdDepth = key;
                        }
                    }

                    if (message.Contains("[["))
                    {
                        List<object> root = JsonConvert.DeserializeObject<List<object>>(message);

                        int channelId = Convert.ToInt32(root[0]);

                        if (root == null || root.Count < 2)
                        {
                            SendLogMessage("Incorrect message format: insufficient elements.", LogMessageType.Error);

                            return;
                        }

                        if (channelId == _currentChannelIdDepth)
                        {
                            SnapshotDepth(message);
                        }
                    }

                    if (!message.Contains("[[")
                        && !message.Contains("ws")
                        && !message.Contains("event"))
                    {
                        UpdateDepth(message);
                    }
                }
                catch (Exception exception)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }

        private void PublicMessageTradesReader()
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

                    if (message.Contains("trades"))
                    {
                        BitfinexSubscriptionResponse responseTrade = JsonConvert.DeserializeObject<BitfinexSubscriptionResponse>(message);

                        int key = Convert.ToInt32(responseTrade.ChanId);

                        if (!_tradeDictionary.ContainsKey(key))
                        {
                            _tradeDictionary.Add(key, responseTrade.Symbol);
                            _channelIdTrade = key;
                        }
                    }

                    if ((message.Contains("te")
                        || message.Contains("tu"))
                        && _channelIdTrade != 0
                        && !message.Contains("event"))
                    {
                        UpdateTrade(message);
                    }
                }
                catch (Exception exception)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }

        private void PrivateMessageReader()
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

                    if (message.Contains("\"event\":\"info\""))
                    {
                        SendLogMessage("WebSocket private opened", LogMessageType.System);
                    }

                    if (message.Contains("\"event\":\"auth\""))
                    {
                        BitfinexFuturesAuthResponseWebSocket authResponse = JsonConvert.DeserializeObject<BitfinexFuturesAuthResponseWebSocket>(message);

                        if (authResponse.Status == "OK")
                        {
                            SendLogMessage("WebSocket authentication successful", LogMessageType.System);
                        }
                        else
                        {
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                            SendLogMessage($"WebSocket authentication error: Invalid public or secret key: {authResponse.Msg}", LogMessageType.Error);
                        }
                    }

                    else if (message.StartsWith("[0,\"tu\",[") || (message.StartsWith("[0,\"te\",[")))
                    {
                        UpdateMyTrade(message);
                    }
                    else if (message.StartsWith("[0,\"on\",[") || message.StartsWith("[0,\"oc\",[") || message.StartsWith("[0,\"ou\",["))
                    {
                        UpdateOrder(message);
                    }
                    else if (message.StartsWith("[0,\"wu\",["))
                    {
                        UpdatePortfolio(message);
                    }
                    else if (message.StartsWith("[0,\"pu\",[") || message.StartsWith("[0,\"pn\",[") || message.StartsWith("[0,\"pc\",["))
                    {
                        UpdatePosition(message);
                    }
                }
                catch (Exception exception)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }

        private void UpdatePosition(string message)
        {
            try
            {
                if (_portfolios == null || _portfolios.Count == 0)
                {
                    return;
                }

                object[] data = JsonConvert.DeserializeObject<object[]>(message);

                if (data.Length >= 3)
                {
                    string json = JsonConvert.SerializeObject(data[2]);

                    List<object> positionArray = JsonConvert.DeserializeObject<List<object>>(json);

                    Portfolio portfolio = _portfolios[0];

                    if (portfolio == null)
                    {
                        SendLogMessage("UpdatePosition: Portfolio not found", LogMessageType.Error);
                        return;
                    }

                    PositionOnBoard boardPosition = new PositionOnBoard();

                    boardPosition.PortfolioName = "BitfinexFuturesPortfolio";
                    boardPosition.SecurityNameCode = positionArray[0].ToString();
                    boardPosition.ValueCurrent = Math.Round(positionArray[2].ToString().ToDecimal(), 4);

                    if (positionArray[6] != null)
                    {
                        boardPosition.UnrealizedPnl = Math.Round(positionArray[6].ToString().ToDecimal(), 6);
                    }

                    portfolio.SetNewPosition(boardPosition);
                }
                else
                {
                    SendLogMessage("Error: the position data is incorrect.", LogMessageType.Error);
                }

                PortfolioEvent?.Invoke(_portfolios);

            }
            catch (Exception exception)
            {
                SendLogMessage("Error, while processing the position: " + exception.ToString(), LogMessageType.Error);
            }
        }

        private DateTime _lastTimeMd = DateTime.MinValue;

        public event Action<MarketDepth> MarketDepthEvent;

        private List<MarketDepth> _marketDepths = new List<MarketDepth>();

        private void SnapshotDepth(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                {
                    SendLogMessage("SnapshotDept> Received empty message", LogMessageType.Error);
                    return;
                }

                List<object> root = JsonConvert.DeserializeObject<List<object>>(message);

                if (root == null || root.Count < 2)
                {
                    SendLogMessage("SnapshotDept> Invalid root structure", LogMessageType.Error);

                    return;
                }

                int channelId = Convert.ToInt32(root[0]);

                string securityName = GetSymbolByKeyInDepth(channelId);

                List<List<object>> snapshot = JsonConvert.DeserializeObject<List<List<object>>>(root[1].ToString());

                if (snapshot == null || snapshot.Count == 0)
                {
                    SendLogMessage("Snapshot data is empty", LogMessageType.Error);

                    return;
                }

                if (_marketDepths == null)
                {
                    _marketDepths = new List<MarketDepth>();
                }

                MarketDepth needDepth = _marketDepths.Find(depth =>
                    depth.SecurityNameCode == securityName);

                if (needDepth == null)
                {
                    needDepth = new MarketDepth();

                    needDepth.SecurityNameCode = securityName;
                    _marketDepths.Add(needDepth);
                }

                List<MarketDepthLevel> asks = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                for (int i = 0; i < snapshot.Count; i++)
                {
                    List<object> value = snapshot[i];

                    decimal amount = (value[2]).ToString().ToDecimal();

                    if (amount > 0)
                    {
                        bids.Add(new MarketDepthLevel()
                        {
                            Bid = (value[2]).ToString().ToDouble(),
                            Price = (value[0]).ToString().ToDouble(),
                        });
                    }
                    else
                    {
                        asks.Add(new MarketDepthLevel()
                        {
                            Ask = Math.Abs((value[2]).ToString().ToDouble()),
                            Price = (value[0]).ToString().ToDouble(),
                        });
                    }
                }

                needDepth.Asks = asks;
                needDepth.Bids = bids;
                needDepth.Time = ServerTime;

                if (needDepth.Time < _lastTimeMd)
                {
                    needDepth.Time = _lastTimeMd;
                }
                else if (needDepth.Time == _lastTimeMd)
                {
                    _lastTimeMd = DateTime.FromBinary(_lastTimeMd.Ticks + 1);

                    needDepth.Time = _lastTimeMd;
                }
                _lastTimeMd = needDepth.Time;

                if (MarketDepthEvent != null)
                {
                    MarketDepthEvent(needDepth.GetCopy());
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateDepth(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                {
                    SendLogMessage("UpdateDepth> Received empty message", LogMessageType.Error);
                    return;
                }

                List<object> root = JsonConvert.DeserializeObject<List<object>>(message);

                if (root == null || root.Count < 2)
                {
                    SendLogMessage("UpdateDepth> Invalid root structure", LogMessageType.Error);
                    return;
                }

                List<object> update = JsonConvert.DeserializeObject<List<object>>(root[1].ToString());

                if (update == null || update.Count < 3)
                {
                    SendLogMessage("UpdateDepth> Invalid update data", LogMessageType.Error);
                    return;
                }

                int channelId = Convert.ToInt32(root[0]);
                string securityName = GetSymbolByKeyInDepth(channelId);

                if (_marketDepths == null)
                {
                    return;
                }

                MarketDepth needDepth = _marketDepths.Find(depth =>
                      depth.SecurityNameCode == securityName);

                if (needDepth == null)
                {
                    return;
                }

                double price = (update[0]).ToString().ToDouble();
                double count = (update[1]).ToString().ToDouble();
                double amount = (update[2]).ToString().ToDouble();

                needDepth.Time = ServerTime;

                if (needDepth.Time < _lastTimeMd)
                {
                    needDepth.Time = _lastTimeMd;
                }
                else if (needDepth.Time == _lastTimeMd)
                {
                    _lastTimeMd = DateTime.FromBinary(_lastTimeMd.Ticks + 1);

                    needDepth.Time = _lastTimeMd;
                }

                _lastTimeMd = needDepth.Time;

                if (count == 0)
                {
                    if (amount < 0)
                    {
                        needDepth.Asks.Remove(needDepth.Asks.Find(level => level.Price == price));
                    }
                    if (amount > 0)
                    {
                        needDepth.Bids.Remove(needDepth.Bids.Find(level => level.Price == price));
                    }
                    return;
                }
                else if (amount > 0)
                {
                    MarketDepthLevel needLevel = needDepth.Bids.Find(bid => bid.Price == price);

                    if (needLevel == null)
                    {
                        needDepth.Bids.Add(new MarketDepthLevel()
                        {
                            Bid = amount,

                            Price = price
                        });

                        needDepth.Bids.Sort((level, depthLevel) => level.Price > depthLevel.Price ? -1 : level.Price < depthLevel.Price ? 1 : 0);
                    }
                    else
                    {
                        needLevel.Bid = amount;
                    }
                }
                else if (amount < 0)
                {
                    MarketDepthLevel needLevel = needDepth.Asks.Find(ask => ask.Price == price);

                    if (needLevel == null)
                    {
                        needDepth.Asks.Add(new MarketDepthLevel()
                        {
                            Ask = Math.Abs(amount),

                            Price = price
                        });

                        needDepth.Asks.Sort((level, depthLevel) => level.Price > depthLevel.Price ? 1 : level.Price < depthLevel.Price ? -1 : 0);
                    }
                    else
                    {
                        needLevel.Ask = Math.Abs(amount);
                    }
                }

                if (needDepth.Asks.Count < 2 ||
                    needDepth.Bids.Count < 2)
                {
                    return;
                }

                if (needDepth.Asks[0].Price > needDepth.Asks[1].Price)
                {
                    needDepth.Asks.RemoveAt(0);
                }

                if (needDepth.Bids[0].Price < needDepth.Bids[1].Price)
                {
                    needDepth.Asks.RemoveAt(0);
                }

                if (needDepth.Asks[0].Price < needDepth.Bids[0].Price)
                {
                    if (needDepth.Asks[0].Price < needDepth.Bids[1].Price)
                    {
                        needDepth.Asks.Remove(needDepth.Asks[0]);
                    }
                    else if (needDepth.Bids[0].Price > needDepth.Asks[1].Price)
                    {
                        needDepth.Bids.Remove(needDepth.Bids[0]);
                    }
                }

                if (MarketDepthEvent != null)
                {
                    MarketDepthEvent(needDepth.GetCopy());
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateTrade(string message)
        {

            try
            {
                List<object> root = JsonConvert.DeserializeObject<List<object>>(message);

                if (root == null && root.Count < 2)
                {
                    SendLogMessage("UpdateTrade> Received empty json", LogMessageType.Error);
                    return;
                }

                List<object> tradeData = JsonConvert.DeserializeObject<List<object>>(root[2].ToString());

                int channelId = Convert.ToInt32(root[0]);

                if (tradeData == null && tradeData.Count < 4)
                {
                    SendLogMessage("UpdateTrade> Received empty json", LogMessageType.Error);
                    return;
                }

                Trade newTrade = new Trade();

                newTrade.SecurityNameCode = GetSymbolByKeyInTrades(channelId);
                newTrade.Id = tradeData[0].ToString();
                newTrade.Price = tradeData[3].ToString().ToDecimal();

                decimal volume = tradeData[2].ToString().ToDecimal();

                if (volume < 0)
                {
                    volume = Math.Abs(volume);
                }

                newTrade.Volume = volume;
                newTrade.Side = volume > 0 ? Side.Buy : Side.Sell;
                newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(tradeData[1]));

                NewTradesEvent?.Invoke(newTrade);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateMyTrade(string message)
        {
            try
            {
                List<object> tradyList = JsonConvert.DeserializeObject<List<object>>(message);

                if (tradyList == null || tradyList.Count < 3)
                {
                    return;
                }

                string tradeDataJson = tradyList[2]?.ToString();

                if (string.IsNullOrEmpty(tradeDataJson))
                {
                    return;
                }
                List<object> tradeData = JsonConvert.DeserializeObject<List<object>>(tradeDataJson);

                if (tradeData == null)
                {
                    SendLogMessage("UpdateMyTrade> Received empty json", LogMessageType.Error);
                    return;
                }

                MyTrade myTrade = new MyTrade();

                myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(tradeData[2]));
                myTrade.SecurityNameCode = Convert.ToString(tradeData[1]);
                myTrade.NumberOrderParent = (tradeData[3]).ToString();
                myTrade.Price = (tradeData[7]).ToString().ToDecimal();
                myTrade.NumberTrade = (tradeData[0]).ToString();
                decimal volume = (tradeData[4]).ToString().ToDecimal();
                myTrade.Side = volume > 0 ? Side.Buy : Side.Sell;

                if (volume < 0)
                {
                    volume = Math.Abs(volume);

                }
                if (message.Contains("tu"))
                {
                    string commissionSecName = tradeData[10].ToString();
                    myTrade.Volume = volume - tradeData[9].ToString().ToDecimal();
                }
                //else
                //{
                //    myTrade.Volume = volume;
                //}

                SendLogMessage(myTrade.ToString(), LogMessageType.Trade);

                MyTradeEvent?.Invoke(myTrade);

            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateOrder(string message)
        {
            try
            {
                List<object> rootArray = JsonConvert.DeserializeObject<List<object>>(message);

                if (rootArray == null)
                {
                    SendLogMessage("UpdateOrder> Received empty json", LogMessageType.Error);
                    return;
                }

                List<object> orderDataList = JsonConvert.DeserializeObject<List<object>>(rootArray[2].ToString());

                if (orderDataList == null)
                {
                    SendLogMessage("UpdateOrder> Received empty json", LogMessageType.Error);
                    return;
                }

                Order updateOrder = new Order();

                updateOrder.SecurityNameCode = (orderDataList[3]).ToString();
                updateOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(orderDataList[4]));
                updateOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(orderDataList[5]));

                try
                {
                    updateOrder.NumberUser = Convert.ToInt32(orderDataList[2]);
                }
                catch
                {
                    // ignore
                }

                updateOrder.NumberMarket = (orderDataList[0]).ToString();
                updateOrder.Side = (orderDataList[7]).ToString().ToDecimal() > 0 ? Side.Buy : Side.Sell;
                updateOrder.State = GetOrderState((orderDataList[13]).ToString());

                string typeOrder = (orderDataList[8]).ToString();

                if (typeOrder == "LIMIT")
                {
                    updateOrder.TypeOrder = OrderPriceType.Limit;
                }

                else
                {
                    updateOrder.TypeOrder = OrderPriceType.Market;
                }

                updateOrder.Price = (orderDataList[16]).ToString().ToDecimal();
                updateOrder.ServerType = ServerType.BitfinexFutures;
                decimal volume = (orderDataList[7]).ToString().ToDecimal();

                if (volume < 0)
                {
                    volume = Math.Abs(volume);
                }

                updateOrder.VolumeExecute = volume;
                updateOrder.Volume = volume;
                updateOrder.PortfolioNumber = "BitfinexFuturesPortfolio";

                MyOrderEvent?.Invoke(updateOrder);

            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdatePortfolio(string json)
        {
            try
            {
                if (_portfolios == null || _portfolios.Count == 0)
                {
                    return;
                }

                List<object> walletArray = JsonConvert.DeserializeObject<List<object>>(json);

                if (walletArray == null)
                {
                    return;
                }

                List<object> wallet = JsonConvert.DeserializeObject<List<object>>(walletArray[2].ToString());

                if (wallet == null)
                {
                    return;
                }


                Portfolio portfolio = _portfolios[0];

                portfolio.Number = "BitfinexFuturesPortfolio";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;
                portfolio.ServerType = ServerType.BitfinexFutures;

                if (wallet[0].ToString() == "margin")
                {
                    PositionOnBoard position = new PositionOnBoard();

                    position.PortfolioName = "BitfinexFuturesPortfolio";
                    position.SecurityNameCode = wallet[1].ToString();
                    position.ValueBegin = Math.Round(wallet[2].ToString().ToDecimal(), 4);
                    position.ValueCurrent = Math.Round(wallet[4] != null ? wallet[4].ToString().ToDecimal() : 1, 4);

                    if (wallet[4] != null)
                    {
                        position.ValueBlocked = Math.Round(wallet[2].ToString().ToDecimal() - wallet[4].ToString().ToDecimal(), 4);
                    }
                    else
                    {
                        position.ValueBlocked = 0;
                    }

                    portfolio.SetNewPosition(position);

                }

                if (_portfolios.Count > 0)
                {
                    PortfolioEvent?.Invoke(_portfolios);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region  11 Trade

        private RateGate _rateGateOrder = new RateGate(1, TimeSpan.FromMilliseconds(706));

        public void SendOrder(Order order)
        {
            try
            {
                _rateGateOrder.WaitToProceed();

                string _apiPath = "v2/auth/w/order/submit";

                BitfinexFuturesOrderData newOrder = new BitfinexFuturesOrderData();

                newOrder.Cid = order.NumberUser.ToString();
                newOrder.Symbol = order.SecurityNameCode;
                order.PortfolioNumber = "BitfinexFuturesPortfolio";
                int leverage = 2;

                if (order.TypeOrder == OrderPriceType.Limit)
                {
                    newOrder.OrderType = "LIMIT";
                }
                else
                {
                    newOrder.OrderType = "MARKET";
                }

                newOrder.Price = order.TypeOrder == OrderPriceType.Market ? null : order.Price.ToString().Replace(",", ".");

                if (order.Side.ToString() == "Sell")
                {
                    newOrder.Amount = "-" + (order.Volume).ToString().Replace(",", ".");
                }
                else
                {
                    newOrder.Amount = (order.Volume).ToString().Replace(",", ".");
                }

                string body = $"{{\"type\":\"{newOrder.OrderType}\"," +
                 $"\"symbol\":\"{newOrder.Symbol}\"," +
                 $"\"amount\":\"{newOrder.Amount}\"," +
                 $"\"price\":\"{newOrder.Price}\"," +
                 $"\"lev\":\"{leverage}\"," +
                 $"\"cid\":{newOrder.Cid}}}";

                IRestResponse response = CreatePrivateQuery(_apiPath, Method.POST, body);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"Error Order exception {response.Content}", LogMessageType.Error);
                    order.State = OrderStateType.Fail;
                    MyOrderEvent?.Invoke(order);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order send exception " + exception.ToString(), LogMessageType.Error);
            }
        }

        public void CancelAllOrders()
        {
            try
            {
                _rateGateOrder.WaitToProceed();

                string _apiPath = "v2/auth/w/order/cancel/multi";

                string body = $"{{\"all\":1}}";

                IRestResponse response = CreatePrivateQuery(_apiPath, Method.POST, body);

                if (response == null)
                {
                    return;
                }

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    List<object> responseJson = JsonConvert.DeserializeObject<List<object>>(response.Content);

                    if (responseJson == null)
                    {
                        SendLogMessage("Deserialization resulted in null", LogMessageType.Error);

                        return;
                    }

                    if (responseJson.Contains("oc_multi-req"))
                    {
                        SendLogMessage($"All active orders canceled: {response.Content}", LogMessageType.Trade);

                        GetPortfolios();
                    }
                    else
                    {
                        SendLogMessage($" {response.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Error : Failed to cancel all orders", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public bool CancelOrder(Order order)
        {
            try
            {
                _rateGateOrder.WaitToProceed();

                string _apiPath = "v2/auth/w/order/cancel";

                if (order.State == OrderStateType.Cancel)
                {
                    return true;
                }

                string body = $"{{\"id\":{order.NumberMarket}}}";

                IRestResponse response = CreatePrivateQuery(_apiPath, Method.POST, body);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string responseBody = response.Content;

                    List<object> responseJson = JsonConvert.DeserializeObject<List<object>>(responseBody);

                    if (responseJson == null)
                    {
                        OrderStateType state = GetOrderStatus(order);

                        if (state == OrderStateType.None)
                        {
                            SendLogMessage("CancelOrder> Deserialization resulted in null", LogMessageType.Error);
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }

                    return true;
                }
                else
                {
                    OrderStateType state = GetOrderStatus(order);

                    if (state == OrderStateType.None)
                    {
                        SendLogMessage($" Error Order cancellation:  {response.Content}, {response.ErrorMessage}", LogMessageType.Error);
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
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
            return false;
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            _rateGateOrder.WaitToProceed();

            try
            {
                string price = newPrice.ToString("F5", CultureInfo.InvariantCulture);


                if (order.TypeOrder == OrderPriceType.Market)
                {
                    SendLogMessage("ChangeOrderPrice> Can't change price for  Order Market", LogMessageType.Error);
                    return;
                }

                string _apiPath = "v2/auth/w/order/update";

                string body = $"{{\"id\":{order.NumberMarket},\"price\":\"{price}\"}}";

                IRestResponse response = CreatePrivateQuery(_apiPath, Method.POST, body);

                if (order.State == OrderStateType.Cancel)
                {
                    return;
                }

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string responseBody = response.Content;

                    if (string.IsNullOrEmpty(responseBody))
                    {
                        SendLogMessage("ChangeOrderPrice> Response body is empty.", LogMessageType.Error);
                        return;
                    }

                    List<object> responseArray = JsonConvert.DeserializeObject<List<object>>(responseBody);

                    if (responseArray == null)
                    {
                        SendLogMessage("Invalid response array structure.", LogMessageType.Error);
                        return;
                    }

                    List<object> orderDataArray = JsonConvert.DeserializeObject<List<object>>(responseArray[4].ToString());

                    if (orderDataArray == null)
                    {
                        SendLogMessage("ChangeOrderPrice> Invalid response array structure.", LogMessageType.Error);
                        return;
                    }

                    order.Price = orderDataArray[16].ToString().ToDecimal();
                    order.State = GetOrderState(orderDataArray[13].ToString());

                    SendLogMessage("Order change price. New price: " + order.Price
                      + "  " + order.SecurityNameCode, LogMessageType.Trade);
                }
                else
                {
                    SendLogMessage("Change price order Fail. Status: "
                    + response.Content + "  " + order.SecurityNameCode, LogMessageType.Error);
                }
            }

            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {

        }

        public List<Order> GetAllOpenOrders()
        {
            List<Order> orders = new List<Order>();

            try
            {
                _rateGateOrder.WaitToProceed();

                string _apiPath = "v2/auth/r/orders";

                IRestResponse response = CreatePrivateQuery(_apiPath, Method.POST, null);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string responseBody = response.Content;

                    if (string.IsNullOrEmpty(responseBody) || responseBody == "[]")
                    {
                        SendLogMessage("GetAllOpenOrders> No active orders found.", LogMessageType.Trade);
                        return null;
                    }

                    List<List<object>> listOrders = JsonConvert.DeserializeObject<List<List<object>>>(response.Content);

                    if (listOrders == null)
                    {
                        SendLogMessage("GetAllOpenOrders> Deserialization resulted in null", LogMessageType.Error);
                        return null;
                    }

                    for (int i = 0; i < listOrders.Count; i++)
                    {
                        List<object> orderData = listOrders[i];

                        Order activeOrder = new Order();

                        activeOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(orderData[5]));
                        activeOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(orderData[4]));
                        activeOrder.ServerType = ServerType.BitfinexFutures;
                        activeOrder.SecurityNameCode = orderData[3].ToString();

                        try
                        {
                            activeOrder.NumberUser = Convert.ToInt32(orderData[2]);

                        }
                        catch
                        {
                            // ignore
                        }

                        activeOrder.NumberMarket = orderData[0].ToString();
                        activeOrder.Side = (orderData[7]).ToString().ToDecimal() > 0 ? Side.Buy : Side.Sell;
                        activeOrder.State = GetOrderState(orderData[13].ToString());
                        decimal volume = orderData[7].ToString().ToDecimal();

                        if (volume < 0)
                        {
                            volume = Math.Abs(volume);
                        }
                        activeOrder.Volume = volume;
                        activeOrder.Price = orderData[16].ToString().ToDecimal();
                        activeOrder.PortfolioNumber = "BitfinexFuturesPortfolio";

                        orders.Add(activeOrder);
                    }
                }
                else
                {
                    SendLogMessage($" GetAllOpenOrders> Can't get all orders. State Code: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return orders;
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            try
            {
                if (order == null || order.NumberUser == 0)
                {
                    SendLogMessage("GetOrderStatus> Order or NumberUser is null or zero.", LogMessageType.Error);
                    return OrderStateType.None;
                }

                List<Order> ordersActive = GetAllOpenOrders();
                List<Order> ordersHistory = GetHistoryOrders();

                Order orderOnMarket = null;

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

                if (orderOnMarket == null && ordersHistory != null && ordersHistory.Count > 0)
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

                if (orderOnMarket == null)
                {
                    SendLogMessage($"GetOrderStatus> Order with NumberUser {order.NumberUser} not found.", LogMessageType.Error);
                    return OrderStateType.None;
                }

                MyOrderEvent?.Invoke(orderOnMarket);

                if (orderOnMarket.State == OrderStateType.Done
                || orderOnMarket.State == OrderStateType.Partial)
                {
                    CreateMyTrade(order.SecurityNameCode, order.NumberUser);
                }
                return orderOnMarket.State;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return OrderStateType.None;
        }

        private void CreateMyTrade(string nameSec, int numberUser)
        {
            _rateGateOrder.WaitToProceed();

            try
            {
                string _apiPath = $"v2/auth/r/trades/{nameSec}/hist";

                IRestResponse response = CreatePrivateQuery(_apiPath, Method.POST, null);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    List<List<string>> data = JsonConvert.DeserializeObject<List<List<string>>>(response.Content);

                    if (data != null && data.Count > 0)
                    {
                        for (int i = 0; i < data.Count; i++)
                        {
                            List<string> tradeData = data[i];

                            if (tradeData == null)
                            {
                                return;
                            }

                            int userNumber = 0;

                            try
                            {
                                userNumber = Convert.ToInt32(tradeData[11]);
                            }
                            catch
                            {
                                // ignore
                            }

                            if (numberUser == userNumber)
                            {
                                MyTrade myTrade = new MyTrade();

                                myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(tradeData[2]));
                                myTrade.SecurityNameCode = Convert.ToString(tradeData[1]);
                                myTrade.NumberOrderParent = (tradeData[3]).ToString();
                                myTrade.Price = (tradeData[7]).ToString().ToDecimal();
                                myTrade.NumberTrade = (tradeData[0]).ToString();
                                decimal volume = (tradeData[4]).ToString().ToDecimal();
                                myTrade.Side = volume > 0 ? Side.Buy : Side.Sell;

                                if (volume < 0)
                                {
                                    volume = Math.Abs(volume);
                                }

                                string commissionSecName = tradeData[10].ToString();

                                if (myTrade.SecurityNameCode.StartsWith("t" + commissionSecName))
                                {
                                    myTrade.Volume = volume + tradeData[9].ToString().ToDecimal();
                                }
                                else
                                {
                                    myTrade.Volume = volume;
                                }

                                MyTradeEvent?.Invoke(myTrade);
                            }
                        }
                    }
                }
                else
                {
                    SendLogMessage($"CreateMyTrade> Http State Code: {response.StatusCode}, Content: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllOpenOrders();

            if (orders == null
                || orders.Count == 0)
            {
                return;
            }

            for (int i = 0; i < orders.Count; i++)
            {
                MyOrderEvent?.Invoke(orders[i]);
            }
        }

        public List<Order> GetHistoryOrders()
        {
            _rateGateOrder.WaitToProceed();

            List<Order> ordersHistory = new List<Order>();

            try
            {
                string _apiPath = "v2/auth/r/orders/hist";

                IRestResponse response = CreatePrivateQuery(_apiPath, Method.POST, null);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    List<List<object>> data = JsonConvert.DeserializeObject<List<List<object>>>(response.Content);

                    if (data != null && data.Count > 0)
                    {
                        for (int i = 0; i < data.Count; i++)
                        {
                            List<object> orderData = data[i];

                            if (orderData != null && orderData.Count > 0)
                            {
                                Order historyOrder = new Order();

                                if (int.TryParse(orderData[2]?.ToString(), out int number))
                                {
                                    historyOrder.NumberUser = Convert.ToInt32(number);
                                }

                                historyOrder.NumberMarket = orderData[0]?.ToString();
                                historyOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(orderData[5]));
                                historyOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(orderData[4]));
                                historyOrder.ServerType = ServerType.BitfinexFutures;
                                historyOrder.SecurityNameCode = orderData[3]?.ToString();
                                historyOrder.Side = orderData[7]?.ToString().ToDecimal() > 0 ? Side.Buy : Side.Sell;
                                historyOrder.State = GetOrderState(orderData[13]?.ToString());
                                string typeOrder = orderData[8].ToString();

                                if (typeOrder == "LIMIT")
                                {
                                    historyOrder.TypeOrder = OrderPriceType.Limit;
                                }
                                else
                                {
                                    historyOrder.TypeOrder = OrderPriceType.Market;
                                }

                                decimal volume = orderData[7].ToString().ToDecimal();

                                if (volume < 0)
                                {
                                    volume = Math.Abs(volume);
                                }

                                historyOrder.Price = orderData[16].ToString().ToDecimal();
                                historyOrder.PortfolioNumber = "BitfinexFuturesPortfolio";
                                historyOrder.Volume = volume;

                                ordersHistory.Add(historyOrder);
                            }
                        }
                    }
                }
                else
                {
                    SendLogMessage($"GetHistoryOrders> Http State Code: {response.StatusCode}, Content: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return ordersHistory;
        }

        private OrderStateType GetOrderState(string orderStateResponse)
        {
            if (orderStateResponse.StartsWith("ACTIVE"))
            {
                return OrderStateType.Active;
            }
            else if (orderStateResponse.StartsWith("EXECUTED"))
            {
                return OrderStateType.Done;
            }
            else if (orderStateResponse.StartsWith("PARTIALLY FILLED"))
            {
                return OrderStateType.Partial;
            }
            else if (orderStateResponse.StartsWith("CANCELED"))
            {
                return OrderStateType.Cancel;
            }

            return OrderStateType.None;
        }

        private string GetSymbolByKeyInTrades(int channelId)
        {
            string symbol = "";

            if (_tradeDictionary.TryGetValue(channelId, out symbol))
            {
                return symbol;
            }
            return null;
        }

        private string GetSymbolByKeyInDepth(int channelId)
        {
            string symbol = "";

            if (_depthDictionary.TryGetValue(channelId, out symbol))
            {
                return symbol;
            }
            return null;
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            return null;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            return null;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public bool SubscribeNews()
        {
            return false;
        }

        #endregion

        #region  12 Queries

        private IRestResponse CreatePublicQuery(string path, Method method)
        {
            try
            {
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(path, method);
                request.AddHeader("accept", "application/json");
                IRestResponse response = client.Execute(request);

                return response;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        private IRestResponse CreatePrivateQuery(string path, Method method, string body)
        {
            try
            {
                string nonce = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).ToString();
                string signature = $"/api/{path}{nonce}{body}";
                string sig = ComputeHmacSha384(_secretKey, signature);

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(path, method);

                request.AddHeader("accept", "application/json");
                request.AddHeader("bfx-nonce", nonce);
                request.AddHeader("bfx-apikey", _publicKey);
                request.AddHeader("bfx-signature", sig);

                if (body != null)
                {
                    request.AddParameter("application/json", body, ParameterType.RequestBody);
                }

                IRestResponse response = client.Execute(request);

                return response;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        private string ComputeHmacSha384(string apiSecret, string signature)
        {
            using HMACSHA384 hmac = new HMACSHA384(Encoding.UTF8.GetBytes(apiSecret));
            {
                byte[] output = hmac.ComputeHash(Encoding.UTF8.GetBytes(signature));

                return BitConverter.ToString(output).Replace("-", "").ToLower();
            }
        }

        #endregion

        #region 13 Log

        public event Action<string, LogMessageType> LogMessageEvent;

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);

        }

        #endregion
    }
}