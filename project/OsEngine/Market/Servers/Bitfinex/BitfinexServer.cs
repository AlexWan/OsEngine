
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Bitfinex.Json;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using Candle = OsEngine.Entity.Candle;
using MarketDepth = OsEngine.Entity.MarketDepth;
using Method = RestSharp.Method;
using Order = OsEngine.Entity.Order;
using Security = OsEngine.Entity.Security;
using Side = OsEngine.Entity.Side;
using Timer = System.Timers.Timer;
using Trade = OsEngine.Entity.Trade;
using WebSocketSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Timers;
using Newtonsoft.Json;
using System.IO;
using ErrorEventArgs = WebSocketSharp.ErrorEventArgs;
using Google.Protobuf.WellKnownTypes;
using System.Runtime.InteropServices.ComTypes;


namespace OsEngine.Market.Servers.Bitfinex
{
    public class BitfinexServer : AServer
    {
        public BitfinexServer()
        {
            BitfinexServerRealization realization = new BitfinexServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
        }
    }
    public class BitfinexServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection
        public BitfinexServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread threadForPublicMessages = new Thread(PublicMessageReader);

            threadForPublicMessages.IsBackground = true;
            threadForPublicMessages.Name = "PublicMessageReaderBitfinex";
            threadForPublicMessages.Start();

            Thread threadForPrivateMessages = new Thread(PrivateMessageReader);

            threadForPrivateMessages.IsBackground = true;
            threadForPrivateMessages.Name = "PrivateMessageReaderBitfinex";
            threadForPrivateMessages.Start();
        }
        public DateTime ServerTime { get; set; }
        public void Connect()
        {
            try
            {
                _publicKey = ((ServerParameterString)ServerParameters[0]).Value;

                _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

                if (string.IsNullOrEmpty(_publicKey) || string.IsNullOrEmpty(_secretKey))
                {
                    SendLogMessage("Error:Invalid public or secret key.", LogMessageType.Error);

                    ServerStatus = ServerConnectStatus.Disconnect;

                    DisconnectEvent();

                    return;
                }

                string _apiPath = "v2/platform/status";

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(_apiPath);

                request.AddHeader("accept", "application/json");

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string responseBody = response.Content;

                    if (responseBody.Contains("1"))
                    {
                        CreateWebSocketConnection();

                        SendLogMessage("Start Bitfinex Connection", LogMessageType.System);
                    }
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
                _subscribledSecurities?.Clear();

                DeleteWebSocketConnection();
            }

            catch (Exception exception)
            {
                SendLogMessage("Connection closed by Bitfinex. WebSocket Data Closed Event" + exception.ToString(), LogMessageType.System);
            }

            finally
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;

                    DisconnectEvent();
                }
            }
        }
        public ServerType ServerType
        {
            get { return ServerType.Bitfinex; }
        }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

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

        private RateGate _rateGateSecurity = new RateGate(30, TimeSpan.FromMinutes(1));

        private List<Security> _securities = new List<Security>();
        public void GetSecurities()
        {
            try
            {
                _rateGateSecurity.WaitToProceed();

                RequestMinSizes();

                string _apiPath = "v2/tickers?symbols=ALL";

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(_apiPath);

                request.AddHeader("accept", "application/json");

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string jsonResponse = response.Content;

                    List<List<object>> securityList = JsonConvert.DeserializeObject<List<List<object>>>(jsonResponse);

                    if (securityList == null)
                    {
                        SendLogMessage("Deserialization resulted in null", LogMessageType.Error);

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
                        List<object> item = securityList[i];

                        string symbol = item[0]?.ToString();
                        string price = item[1]?.ToString()?.Replace('.', ',');
                        string volume = item[8]?.ToString()?.Replace('.', ',');

                        if (symbol.Contains(":"))
                        {
                            continue;
                        }

                        SecurityType securityType = GetSecurityType(symbol);

                        if (securityType == SecurityType.None)
                        {
                            continue;
                        }

                        Security newSecurity = new Security();

                        newSecurity.Exchange = ServerType.Bitfinex.ToString();
                        newSecurity.Name = symbol;
                        newSecurity.NameFull = symbol;
                        newSecurity.NameClass = symbol.StartsWith("f") ? "Futures" : "CurrencyPair";
                        newSecurity.NameId = symbol;
                        newSecurity.SecurityType = securityType;
                        newSecurity.Lot = 1;
                        newSecurity.State = SecurityStateType.Activ;
                        newSecurity.Decimals = price.DecimalsCount() == 0 ? 1 : price.DecimalsCount();
                        newSecurity.PriceStep = newSecurity.Decimals.GetValueByDecimals();

                        if (newSecurity.PriceStep == 0)
                        {
                            newSecurity.PriceStep = 1;
                        }
                        newSecurity.PriceStepCost = newSecurity.PriceStep;
                        newSecurity.DecimalsVolume = DigitsAfterComma(volume);
                        newSecurity.MinTradeAmount = GetMinSize(symbol);

                        securities.Add(newSecurity);
                    }

                    if (SecurityEvent != null)
                    {
                        SecurityEvent(securities);
                    }
                }
                else
                {
                    SendLogMessage("Securities request exception. Status: " + response.Content, LogMessageType.Error);
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
            try
            {
                string _apiPatch = "/v2/conf/pub:info:pair";

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(_apiPatch, Method.GET);

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(response.Content))
                {
                    List<List<object>> data = JsonConvert.DeserializeObject<List<List<object>>>(response.Content);

                    for (int i = 0; i < data.Count; i++)
                    {
                        List<object> subArray = data[i];

                        for (int j = 0; j < subArray.Count; j++)
                        {
                            if (subArray[j] is List<object> pairData && pairData.Count > 1)
                            {
                                string pair = "t" + (pairData[0]?.ToString() ?? "");

                                if (pairData[1] is List<object> limits && limits.Count > 3 && limits[3] != null)
                                {
                                    string minSizeString = limits[3].ToString(); 

                                    decimal minSize;

                                    if (decimal.TryParse(minSizeString, System.Globalization.NumberStyles.Float,
                                        System.Globalization.CultureInfo.InvariantCulture, out minSize))
                                    {
                                        minSizes.Add(pair, minSize); 
                                    }
                                }
                                else
                                {
                                    SendLogMessage("Error: json is null", LogMessageType.Error); 
                                }
                            }

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error : {ex.Message}", LogMessageType.Error);
            }
        }
        public decimal GetMinSize(string symbol)
        {
            if (minSizes.TryGetValue(symbol, out decimal minSize))
            {
                return minSize;
            }

            return 1;
        }
        private SecurityType GetSecurityType(string type)
        {
            SecurityType _securityType = type.StartsWith("t") ? SecurityType.CurrencyPair : SecurityType.Futures;

            return _securityType;
        }

        #endregion

        #region 4 Portfolios

        private List<Portfolio> _portfolios = new List<Portfolio>();

        public event Action<List<Portfolio>> PortfolioEvent;

        private RateGate _rateGatePortfolio = new RateGate(90, TimeSpan.FromMinutes(1));
        public void GetPortfolios()
        {
            CreateQueryPortfolio();

            if (_portfolios.Count != 0)
            {
                PortfolioEvent?.Invoke(_portfolios);
            }
        }
        private void CreateQueryPortfolio()
        {
            try
            {
                _rateGatePortfolio.WaitToProceed();

                if (_publicKey == null || _secretKey == null)
                {
                    return;
                }
                string nonce = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).ToString();
                string _apiPath = "v2/auth/r/wallets";
                string signature = $"/api/{_apiPath}{nonce}";
                string sig = ComputeHmacSha384(_secretKey, signature);

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(_apiPath, Method.POST);

                request.AddHeader("accept", "application/json");
                request.AddHeader("bfx-nonce", nonce);
                request.AddHeader("bfx-apikey", _publicKey);
                request.AddHeader("bfx-signature", sig);

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Portfolio portfolio = new Portfolio();

                    portfolio.Number = "BitfinexPortfolio";
                    portfolio.ValueBegin = 1;
                    portfolio.ValueCurrent = 1;

                    List<List<object>> wallets = JsonConvert.DeserializeObject<List<List<object>>>(response.Content);

                    for (int i = 0; i < wallets.Count; i++)
                    {
                        List<object> wallet = wallets[i];

                        if (wallet[0].ToString() == "exchange")
                        {
                            PositionOnBoard position = new PositionOnBoard();

                            position.PortfolioName = "BitfinexPortfolio";
                            position.SecurityNameCode = wallet[1].ToString();
                            position.ValueBegin = wallet[2].ToString().ToDecimal();
                            position.ValueCurrent = wallet[4].ToString().ToDecimal();

                            if (wallet[4] != null)
                            {
                                position.ValueBlocked = wallet[2].ToString().ToDecimal() - wallet[4].ToString().ToDecimal();
                            }

                            else
                            {
                                position.ValueBlocked = 0;
                            }

                            portfolio.SetNewPosition(position);
                        }
                    }

                    _portfolios.Add(portfolio);

                    if (_portfolios.Count != 0)
                    {
                        PortfolioEvent?.Invoke(_portfolios);
                    }
                }

                else
                {
                    SendLogMessage($"Error rest request: Invalid public or secret key.{response.Content}", LogMessageType.Error);

                    ServerStatus = ServerConnectStatus.Disconnect;

                    DisconnectEvent();
                }
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
                List<object> walletArray = JsonConvert.DeserializeObject<List<object>>(json);

                List<object> wallet = JsonConvert.DeserializeObject<List<object>>(walletArray[2].ToString());

                if (walletArray == null)
                {
                    return;
                }

                if (wallet == null)
                {
                    return;
                }

                Portfolio portfolio = new Portfolio();

                portfolio.Number = "BitfinexPortfolio";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;
                portfolio.ServerType = ServerType.Bitfinex;

                if (wallet[0].ToString() == "exchange")
                {
                    PositionOnBoard position = new PositionOnBoard();

                    position.PortfolioName = "BitfinexPortfolio";
                    position.SecurityNameCode = wallet[1].ToString();
                    position.ValueCurrent = wallet[2].ToString().ToDecimal();
                    position.ValueBegin = wallet[2].ToString().ToDecimal();

                    if (wallet[4] != null)
                    {
                        position.ValueBlocked = wallet[2].ToString().ToDecimal() - wallet[4].ToString().ToDecimal();
                    }
                    else
                    {
                        position.ValueBlocked = 0;
                    }
                    portfolio.SetNewPosition(position);
                }

                _portfolios.Add(portfolio);

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

        #region 5 Data

        private RateGate _rateGateCandleHistory = new RateGate(25, TimeSpan.FromMinutes(1));

        private HashSet<(string security, DateTime start, DateTime end)> _loadedIntervals = new HashSet<(string, DateTime, DateTime)>();
        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
           
            int limit = 10000;

            List<Trade> trades = new List<Trade>();

            DateTime now = DateTime.UtcNow;

            if (endTime > now)
            {
                endTime = now;
            }
            if ((startTime < DateTime.UtcNow.AddYears(-20)) ||
                (endTime < DateTime.UtcNow.AddYears(-20)) ||
                (startTime >= now) || (actualTime > endTime) ||
                (actualTime > now) ||
                (startTime > endTime))
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

                if (newTrades == null || newTrades.Count == 0)
                {
                    SendLogMessage($"No data for interval {nextStartTime} - {nextEndTime}", LogMessageType.Error);
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
        private List<Trade> GetTrades(string security, int limit, DateTime startTime, DateTime endTime)
        {
            try
            {
                Thread.Sleep(3000);

                _rateGateTrades.WaitToProceed();

                long startDate = new DateTimeOffset(startTime).ToUnixTimeMilliseconds();
                long endDate = new DateTimeOffset(endTime).ToUnixTimeMilliseconds();

                string _apiPath = $"/v2/trades/{security}/hist?limit={limit}&start={startDate}&end={endDate}";

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(_apiPath, Method.GET);
                request.AddHeader("accept", "application/json");

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    List<List<object>> tradeList = JsonConvert.DeserializeObject<List<List<object>>>(response.Content);

                    List<Trade> trades = new List<Trade>();

                    if (tradeList == null || tradeList.Count == 0)
                    {
                        SendLogMessage($"No data available for the {security} pair for the specified period." +
                            $"{response.StatusCode}- {response.Content}", LogMessageType.Error);

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
        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf, bool isOsData, int countToLoad, DateTime timeEnd)
        {

            // int limit = 10000;
            int limit = 5000;

            List<Candle> allCandles = new List<Candle>();
            //перескакивает на сутки назад
            // TimeStart = EndTime - (Количество_свечей * Длительность_таймфрейма)
            //ровно сутки данных, то N = 24 * 60 / 5 = 288 свечей.
            DateTime startTime = timeEnd - TimeSpan.FromMinutes(tf.TotalMinutes * countToLoad);

            // DateTime startTime = timeEnd.AddMinutes(-tf.TotalMinutes * countToLoad);

            HashSet<DateTime> uniqueTimes = new HashSet<DateTime>();

            int candlesLoaded = 0;

            DateTime periodEnd = startTime;

            while (candlesLoaded < countToLoad || periodEnd < timeEnd)
            {
                int candlesToLoad = Math.Min(limit, countToLoad - candlesLoaded);

                DateTime periodStart = startTime;

                periodEnd = periodStart.AddMinutes(tf.TotalMinutes * candlesToLoad);

                if (periodEnd > DateTime.UtcNow)
                {
                    periodEnd = DateTime.UtcNow;
                }

                string timeFrame = GetInterval(tf);

                List<Candle> rangeCandles = CreateQueryCandles(nameSec, timeFrame, periodStart, periodEnd, candlesToLoad);

                if (rangeCandles == null)
                {
                    SendLogMessage($"Error: rangeCandles is null, security:{nameSec},periodStart{periodStart},periodEnd{periodEnd},timeFrame:{timeFrame}", LogMessageType.Error);
                    
                    return null;
                }

                if (rangeCandles.Count == 0)
                {
                    SendLogMessage("Error: rangeCandles is empty", LogMessageType.Error);
                   
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

                //  startTime = rangeCandles[rangeCandles.Count - 1].TimeStart.AddMinutes(tf.TotalMinutes);
                startTime = rangeCandles[rangeCandles.Count - 1].TimeStart;


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
       
        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

            if (!CheckTf(tfTotalMinutes))
            {
                return null;
            }
           DateTime startTime = DateTime.UtcNow.AddMinutes(-timeFrameBuilder.TimeFrameTimeSpan.Minutes * candleCount);

            DateTime timeStart = DateTime.UtcNow - TimeSpan.FromMinutes(timeFrameBuilder.TimeFrameTimeSpan.Minutes * candleCount);

            DateTime endTime = DateTime.UtcNow;

            return GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, startTime);
        }
        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (startTime != actualTime)
            {
                startTime = actualTime;
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
        private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (startTime >= endTime ||
                startTime >= DateTime.UtcNow ||
                actualTime > endTime ||
                actualTime > DateTime.UtcNow ||
                endTime < DateTime.UtcNow.AddYears(-20))
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
        private List<Candle> CreateQueryCandles(string nameSec, string tf, DateTime startTime, DateTime endTime, int limit)
        {
            try
            {
                _rateGateCandleHistory.WaitToProceed();

                long startDate = TimeManager.GetTimeStampMilliSecondsToDateTime(startTime);
                long endDate = TimeManager.GetTimeStampMilliSecondsToDateTime(endTime);
             
                string _apiPath = $"/v2/candles/trade:{tf}:{nameSec}/hist?sort=1&start={startDate}&end={endDate}&limit={limit}";

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(_apiPath, Method.GET);
                request.AddHeader("accept", "application/json");

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string jsonResponse = response.Content;

                    List<List<object>> candles = JsonConvert.DeserializeObject<List<List<object>>>(jsonResponse);

                    if (candles == null || candles.Count == 0)
                    {
                        SendLogMessage($"No data {nameSec} period {startTime} - {endTime},timeFrame:{tf}", LogMessageType.Error);
                        return null;
                    }
                    List<BitfinexCandle> candleList = new List<BitfinexCandle>();

                    for (int i = 0; i < candles.Count; i++)
                    {
                        List<object> candleData = candles[i];

                        BitfinexCandle newCandle = new BitfinexCandle();

                        newCandle.Mts = candleData[0].ToString();
                        newCandle.Open = candleData[1].ToString();
                        newCandle.Close = candleData[2].ToString();
                        newCandle.High = candleData[3].ToString();
                        newCandle.Low = candleData[4].ToString();
                        newCandle.Volume = candleData[5].ToString();

                        candleList.Add(newCandle);
                    }
                    string filePath = @"C:\Users\Public\Documents\candles.txt";

                    File.AppendAllText(filePath, "Новые данные\n");

                    using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                    {
                        foreach (BitfinexCandle candle in candleList)
                        {
                            long mts = long.Parse(candle.Mts);

                            DateTime date = DateTimeOffset.FromUnixTimeMilliseconds(mts).DateTime;

                            string line = $"{date:yyyy-MM-dd HH:mm:ss},{candle.Open},{candle.High},{candle.Low},{candle.Close},{candle.Volume}";
                          
                            writer.WriteLine(line);
                        }
                    }
                    return ConvertToCandles(candleList);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Request error{exception.Message}", LogMessageType.Error);
            }

            return null;
        }
        private List<Candle> ConvertToCandles(List<BitfinexCandle> candleList)
        {
            List<Candle> candles = new List<Candle>();

            try
            {
                for (int i = 0; i < candleList.Count; i++)
                {
                    BitfinexCandle candle = candleList[i];

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

        private Timer _pingTimer;

        private bool _publicSocketActive = false;

        private bool _privateSocketActive = false;

        private string _lockerCheckActivateionSockets = "lockerCheckActivateionSockets";

        private WebSocket _webSocketPublic;

        private WebSocket _webSocketPrivate;

        private ConcurrentQueue<string> _webSocketPublicMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _webSocketPrivateMessage = new ConcurrentQueue<string>();
        private void CreateWebSocketConnection()
        {
            _publicSocketActive = false;

            _privateSocketActive = false;

            try
            {
                if (_webSocketPublic != null)
                {
                    return;
                }

                _webSocketPublic = new WebSocket(_webSocketPublicUrl);

                _webSocketPublic.OnOpen += WebSocketPublic_Opened;
                _webSocketPublic.OnClose += WebSocketPublic_Closed;
                _webSocketPublic.OnMessage += WebSocketPublic_MessageReceived;
                _webSocketPublic.OnError += WebSocketPublic_Error;

                _webSocketPublic.Connect();

                _webSocketPrivate = new WebSocket(_webSocketPrivateUrl);

                _webSocketPrivate.OnOpen += WebSocketPrivate_Opened;
                _webSocketPrivate.OnClose += WebSocketPrivate_Closed;
                _webSocketPrivate.OnMessage += WebSocketPrivate_MessageReceived;
                _webSocketPrivate.OnError += WebSocketPrivate_Error;

                _webSocketPrivate.Connect();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }
        private void DeleteWebSocketConnection()
        {
            try
            {
                if (_webSocketPublic != null)
                {
                    _webSocketPublic.OnOpen -= WebSocketPublic_Opened;
                    _webSocketPublic.OnClose -= WebSocketPublic_Closed;
                    _webSocketPublic.OnMessage -= WebSocketPublic_MessageReceived;
                    _webSocketPublic.OnError -= WebSocketPublic_Error;

                    try
                    {
                        _webSocketPublic.Close();
                    }

                    catch (Exception exception)
                    {
                        SendLogMessage(exception.ToString(), LogMessageType.System);
                    }

                    _webSocketPublic = null;
                }

                if (_webSocketPrivate != null)
                {
                    _webSocketPrivate.OnOpen -= WebSocketPrivate_Opened;
                    _webSocketPrivate.OnClose -= WebSocketPrivate_Closed;
                    _webSocketPrivate.OnMessage -= WebSocketPrivate_MessageReceived;
                    _webSocketPrivate.OnError -= WebSocketPrivate_Error;

                    try
                    {
                        _webSocketPrivate.Close();
                    }

                    catch (Exception exception)
                    {
                        SendLogMessage(exception.ToString(), LogMessageType.System);
                    }

                    _webSocketPrivate = null;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.System);
            }
        }

        #endregion

        #region  7 WebSocket events

        private DateTime _lastTimeMd = DateTime.MinValue;

        public event Action<MarketDepth> MarketDepthEvent;

        private List<MarketDepth> _marketDepths = new List<MarketDepth>();
        private void WebSocketPublic_Opened(object sender, EventArgs e)
        {
            Thread.Sleep(2000);

            _publicSocketActive = true;

            try
            {
                CheckActivationSockets();

                SendLogMessage("WebSocket public Bitfinex Opened", LogMessageType.System);

                _pingTimer = new Timer(30000);
                _pingTimer.Elapsed += SendPing;
                _pingTimer.AutoReset = true;
                _pingTimer.Start();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }
        private void WebSocketPublic_Closed(object sender, CloseEventArgs e)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;

                    DisconnectEvent?.Invoke();
                }

                if (_pingTimer != null)
                {
                    _pingTimer.Stop();
                    _pingTimer.Elapsed -= SendPing;
                    _pingTimer = null;
                }

                SendLogMessage("WebSocket Public closed by Bitfinex.", LogMessageType.Error);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }
        private void WebSocketPublic_Error(object sender, ErrorEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(e.Message))
                {
                    SendLogMessage($"WebSocket Error: {e.Message}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Data socket exception: " + exception.ToString(), LogMessageType.Error);
            }
        }
        private void WebSocketPublic_MessageReceived(object sender, MessageEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect || e?.Data == null || string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                _webSocketPublicMessage?.Enqueue(e.Data);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }
        private void WebSocketPrivate_Opened(object sender, EventArgs e)
        {
            _privateSocketActive = true;

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
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent?.Invoke();
                }

                if (_pingTimer != null)
                {
                    _pingTimer.Stop();
                    _pingTimer.Elapsed -= SendPing;
                    _pingTimer = null;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            SendLogMessage("Connection Closed by Bitfinex. WebSocket Private closed", LogMessageType.Error);
        }
        private void WebSocketPrivate_Error(object sender, ErrorEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(e.Message))
                {
                    SendLogMessage($"WebSocket Error: {e.Message}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }
        private void WebSocketPrivate_MessageReceived(object sender, MessageEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect || string.IsNullOrEmpty(e?.Data))
                {
                    return;
                }

                _webSocketPrivateMessage?.Enqueue(e.Data);
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

                _webSocketPrivate.Send(authJson);

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
                if (_publicSocketActive == false)
                {
                    return;
                }

                if (_privateSocketActive == false)
                {
                    return;
                }

                try
                {
                    if (ServerStatus != ServerConnectStatus.Connect &&
                        _webSocketPublic != null && _webSocketPrivate != null &&
                        _webSocketPublic.ReadyState == WebSocketState.Open &&
                        _webSocketPrivate.ReadyState == WebSocketState.Open)
                    {
                        ServerStatus = ServerConnectStatus.Connect;

                        ConnectEvent?.Invoke();
                    }

                    SendLogMessage("All sockets activated.", LogMessageType.System);
                }

                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }
        private void SendPing(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (_webSocketPublic != null && _webSocketPublic.ReadyState == WebSocketState.Open)
                {
                    string pingMessage = "{\"event\":\"ping\", \"cid\":1234}";

                    _webSocketPublic.Send(pingMessage);
                }
            }

            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region  8  WebSocket security subscrible

        private List<Security> _subscribledSecurities = new List<Security>();
        public void Subscrible(Security security)
        {
            try
            {
                CreateSubscribleMessageWebSocket(security);

                Thread.Sleep(200);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }
        private void CreateSubscribleMessageWebSocket(Security security)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                for (int i = 0; i < _subscribledSecurities.Count; i++)
                {
                    if (_subscribledSecurities[i].Name == security.Name &&
                        _subscribledSecurities[i].NameClass == security.NameClass)
                    {
                        return;
                    }
                }

                _subscribledSecurities.Add(security);

                _webSocketPublic.Send($"{{\"event\":\"subscribe\",\"channel\":\"book\",\"symbol\":\"{security.Name}\",\"prec\":\"P0\",\"freq\":\"F0\",\"len\":\"25\"}}");

                _webSocketPublic.Send($"{{\"event\":\"subscribe\",\"channel\":\"trades\",\"symbol\":\"{security.Name}\"}}");
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

                int tradeCount = _tradeDictionary.Count;

                int i = 0;

                while (i < tradeCount)
                {
                    int chanId = new List<int>(_tradeDictionary.Keys)[i];

                    string message = "{\"event\":\"unsubscribe\",\"chanId\":" + chanId + "}";

                    _webSocketPublic.Send(message);

                    i++;
                }

                _tradeDictionary.Clear();

                int depthCount = _depthDictionary.Count;

                int j = 0;

                while (j < depthCount)
                {
                    int chanId = new List<int>(_depthDictionary.Keys)[j];

                    string message = "{\"event\":\"unsubscribe\",\"chanId\":" + chanId + "}";

                    _webSocketPublic.Send(message);

                    j++;
                }

                _depthDictionary.Clear();

                SendLogMessage("All subscriptions have been successfully removed", LogMessageType.System);
            }
            catch (Exception exception)
            {
                SendLogMessage("Error unsubscribing from channels:" + exception.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region  9 WebSocket parsing the messages

        public event Action<Trade> NewTradesEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<Order> MyOrderEvent;

        private int _currentChannelIdDepth;

        private int _channelIdTrade;

        private Dictionary<int, string> _tradeDictionary = new Dictionary<int, string>();

        private Dictionary<int, string> _depthDictionary = new Dictionary<int, string>();
        private void PublicMessageReader()
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

                    if (_webSocketPublicMessage.IsEmpty)
                    {
                        Thread.Sleep(1);

                        continue;
                    }

                    _webSocketPublicMessage.TryDequeue(out string message);

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

                    else if (message.Contains("book"))
                    {
                        BitfinexSubscriptionResponse responseDepth = JsonConvert.DeserializeObject<BitfinexSubscriptionResponse>(message);

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

                    if (!message.Contains("[[") && !message.Contains("te") &&
                        !message.Contains("tu") && !message.Contains("ws") &&
                        !message.Contains("event") && !message.Contains("hb"))
                    {
                        UpdateDepth(message);
                    }

                    if ((message.Contains("te") || message.Contains("tu")) && _channelIdTrade != 0)
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

                    if (_webSocketPrivateMessage.IsEmpty)
                    {
                        Thread.Sleep(1);

                        continue;
                    }

                    _webSocketPrivateMessage.TryDequeue(out string message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Contains("\"event\":\"info\""))
                    {
                        SendLogMessage("WebSocket opened", LogMessageType.System);
                    }

                    if (message.Contains("\"event\":\"auth\""))
                    {
                        BitfinexAuthResponseWebSocket authResponse = JsonConvert.DeserializeObject<BitfinexAuthResponseWebSocket>(message);

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

                    else if (message.StartsWith("[0,\"tu\",["))
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
                }
                catch (Exception exception)
                {
                    Thread.Sleep(5000);

                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }
        private void SnapshotDepth(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                {
                    SendLogMessage("Received empty message", LogMessageType.Error);

                    return;
                }

                List<object> root = JsonConvert.DeserializeObject<List<object>>(message);

                if (root == null || root.Count < 2)
                {
                    SendLogMessage("Invalid root structure", LogMessageType.Error);

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
                            Bid = (value[2]).ToString().ToDecimal(),
                            Price = (value[0]).ToString().ToDecimal(),
                        });
                    }
                    else
                    {
                        asks.Add(new MarketDepthLevel()
                        {
                            Ask = Math.Abs((value[2]).ToString().ToDecimal()),
                            Price = (value[0]).ToString().ToDecimal(),
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
                    SendLogMessage("Received empty message", LogMessageType.Error);

                    return;
                }
                List<object> root = JsonConvert.DeserializeObject<List<object>>(message);

                if (root == null || root.Count < 2)
                {
                    SendLogMessage("Invalid root structure", LogMessageType.Error);

                    return;
                }

                List<object> update = JsonConvert.DeserializeObject<List<object>>(root[1].ToString());

                if (update == null || update.Count < 3)
                {
                    SendLogMessage("Invalid update data", LogMessageType.Error);

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

                decimal price = (update[0]).ToString().ToDecimal();

                decimal count = (update[1]).ToString().ToDecimal();

                decimal amount = (update[2]).ToString().ToDecimal();

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
            if (message.Contains("tu"))
            {
                return;
            }

            try
            {
                List<object> root = JsonConvert.DeserializeObject<List<object>>(message);

                if (root == null && root.Count < 2)
                {
                    SendLogMessage("Received empty json", LogMessageType.Error);

                    return;
                }

                List<object> tradeData = JsonConvert.DeserializeObject<List<object>>(root[2].ToString());

                int channelId = Convert.ToInt32(root[0]);

                if (tradeData == null && tradeData.Count < 4)
                {
                    SendLogMessage("Received empty json", LogMessageType.Error);

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
                    SendLogMessage("Received empty json", LogMessageType.Error);

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

                myTrade.Volume = volume;

                MyTradeEvent?.Invoke(myTrade);

                SendLogMessage(myTrade.ToString(), LogMessageType.Trade);
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
                    SendLogMessage("Received empty json", LogMessageType.Error);

                    return;
                }

                List<object> orderDataList = JsonConvert.DeserializeObject<List<object>>(rootArray[2].ToString());

                if (orderDataList == null)
                {
                    SendLogMessage("Received empty json", LogMessageType.Error);

                    return;
                }

                Order updateOrder = new Order();

                updateOrder.SecurityNameCode = (orderDataList[3]).ToString();
                updateOrder.SecurityClassCode = orderDataList[3].ToString().StartsWith("f") ? "Futures" : "CurrencyPair";
                updateOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(orderDataList[4]));
                updateOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(orderDataList[5]));
                updateOrder.NumberUser = Convert.ToInt32(orderDataList[2]);
                updateOrder.NumberMarket = (orderDataList[0]).ToString();
                updateOrder.Side = (orderDataList[7]).ToString().ToDecimal() > 0 ? Side.Buy : Side.Sell;
                updateOrder.State = GetOrderState((orderDataList[13]).ToString());
                string typeOrder = (orderDataList[8]).ToString();

                if (typeOrder == "EXCHANGE LIMIT")
                {
                    updateOrder.TypeOrder = OrderPriceType.Limit;
                }

                else
                {
                    updateOrder.TypeOrder = OrderPriceType.Market;
                }

                updateOrder.Price = (orderDataList[16]).ToString().ToDecimal();
                updateOrder.ServerType = ServerType.Bitfinex;
                decimal volume = (orderDataList[7]).ToString().ToDecimal();

                if (volume < 0)
                {
                    volume = Math.Abs(volume);
                }
                updateOrder.VolumeExecute = volume;
                updateOrder.Volume = volume;
                updateOrder.PortfolioNumber = "BitfinexPortfolio";

                MyOrderEvent?.Invoke(updateOrder);
            }

            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region  10 Trade

        private RateGate _rateGateOrder = new RateGate(90, TimeSpan.FromMinutes(1));

        private RateGate _rateGateTrades = new RateGate(13, TimeSpan.FromMinutes(1));
        public void SendOrder(Order order)
        {
            try
            {
                _rateGateOrder.WaitToProceed();

                string nonce = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string _apiPath = "v2/auth/w/order/submit";

                BitfinexOrderData newOrder = new BitfinexOrderData();

                newOrder.Cid = order.NumberUser.ToString();
                newOrder.Symbol = order.SecurityNameCode;
                order.PortfolioNumber = "BitfinexPortfolio";

                if (order.TypeOrder == OrderPriceType.Limit)
                {
                    newOrder.OrderType = "EXCHANGE LIMIT";
                }
                else
                {
                    newOrder.OrderType = "EXCHANGE MARKET";
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
                string body = $"{{\"type\":\"{newOrder.OrderType}\",\"symbol\":\"{newOrder.Symbol}\"," +
                    $"\"amount\":\"{newOrder.Amount}\",\"price\":\"{newOrder.Price}\",\"cid\":{newOrder.Cid}}}";

                string signature = $"/api/{_apiPath}{nonce}{body}";

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(_apiPath, Method.POST);
                string sig = ComputeHmacSha384(_secretKey, signature);

                request.AddHeader("accept", "application/json");
                request.AddHeader("bfx-nonce", nonce);
                request.AddHeader("bfx-apikey", _publicKey);
                request.AddHeader("bfx-signature", sig);
                request.AddParameter("application/json", body, ParameterType.RequestBody);

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string responseBody = response.Content;

                    List<object> responseArray = JsonConvert.DeserializeObject<List<object>>(responseBody);

                    if (responseArray == null)
                    {
                        SendLogMessage("Deserialization resulted in null", LogMessageType.Error);

                        return;
                    }

                    string dataJson = responseArray[4].ToString();

                    List<List<object>> ordersArray = JsonConvert.DeserializeObject<List<List<object>>>(dataJson);

                    if (ordersArray == null)
                    {
                        SendLogMessage("Deserialization resulted in null", LogMessageType.Error);
                        return;
                    }

                    List<object> orders = ordersArray[0];

                    if (orders[0] != null)
                    {
                        Order newOsOrder = new Order();

                        newOsOrder.SecurityNameCode = (orders[3]).ToString();
                        newOsOrder.NumberMarket = orders[0].ToString();
                        newOsOrder.NumberUser = Convert.ToInt32(orders[2]);
                        newOsOrder.State = GetOrderState(orders[13].ToString());
                        newOsOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(orders[4]));
                        newOsOrder.SecurityClassCode = orders[3].ToString().StartsWith("f") ? "Futures" : "CurrencyPair";
                        newOsOrder.Side = (orders[6]).ToString().ToDecimal() > 0 ? Side.Buy : Side.Sell;
                        newOsOrder.ServerType = ServerType.Bitfinex;
                        newOsOrder.Price = (orders[16]).ToString().ToDecimal();
                        newOsOrder.PortfolioNumber = "BitfinexPortfolio";
                        newOsOrder.Volume = (orders[7]).ToString().ToDecimal();

                        SendLogMessage($"Order number {newOsOrder.NumberMarket} on exchange.", LogMessageType.Trade);

                        string typeOrder = (orders[8]).ToString();

                        if (typeOrder == "EXCHANGE LIMIT")
                        {
                            newOsOrder.TypeOrder = OrderPriceType.Limit;
                        }
                        else
                        {
                            newOsOrder.TypeOrder = OrderPriceType.Market;
                        }
                    }

                    GetPortfolios();
                }
                else
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

                string nonce = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).ToString();
                string _apiPath = "v2/auth/w/order/cancel/multi";
                string body = $"{{\"all\":1}}";

                string signature = $"/api/{_apiPath}{nonce}{body}";
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(_apiPath, Method.POST);
                string sig = ComputeHmacSha384(_secretKey, signature);

                request.AddHeader("accept", "application/json");
                request.AddHeader("bfx-nonce", nonce);
                request.AddHeader("bfx-apikey", _publicKey);
                request.AddHeader("bfx-signature", sig);
                request.AddParameter("application/json", body, ParameterType.RequestBody);

                IRestResponse response = client.Execute(request);

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
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }
        public void CancelOrder(Order order)
        {
            try
            {
                _rateGateOrder.WaitToProceed();

                string nonce = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).ToString();
                string _apiPath = "v2/auth/w/order/cancel";

                if (order.State == OrderStateType.Cancel)
                {
                    return;
                }

                string body = $"{{\"id\":{order.NumberMarket}}}";

                string signature = $"/api/{_apiPath}{nonce}{body}";
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(_apiPath, Method.POST);
                string sig = ComputeHmacSha384(_secretKey, signature);

                request.AddHeader("accept", "application/json");
                request.AddHeader("bfx-nonce", nonce);
                request.AddHeader("bfx-apikey", _publicKey);
                request.AddHeader("bfx-signature", sig);
                request.AddParameter("application/json", body, ParameterType.RequestBody);

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string responseBody = response.Content;

                    List<object> responseJson = JsonConvert.DeserializeObject<List<object>>(responseBody);

                    if (responseJson == null)
                    {
                        SendLogMessage("Deserialization resulted in null", LogMessageType.Error);
                        return;
                    }

                    SendLogMessage($"Order canceled Successfully. Order ID:{order.NumberMarket}", LogMessageType.Trade);

                    order.State = OrderStateType.Cancel;

                    MyOrderEvent(order);

                    GetPortfolios();
                }
                else
                {
                    SendLogMessage($" Error Order cancellation:  {response.Content}, {response.ErrorMessage}", LogMessageType.Error);

                    order.State = OrderStateType.Fail;

                    MyOrderEvent?.Invoke(order);

                }
            }
            catch (Exception exception)
            {

                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }
        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            _rateGateOrder.WaitToProceed();

            try
            {
                string nonce = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).ToString();

                string price = newPrice.ToString().Replace(',', '.');

                if (order.TypeOrder == OrderPriceType.Market)
                {
                    SendLogMessage("Can't change price for  Order Market", LogMessageType.Error);

                    return;
                }

                string _apiPath = "v2/auth/w/order/update";
                string body = $"{{\"id\":{order.NumberMarket},\"price\":\"{price}\"}}";

                string signature = $"/api/{_apiPath}{nonce}{body}";
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(_apiPath, Method.POST);
                string sig = ComputeHmacSha384(_secretKey, signature);

                request.AddHeader("accept", "application/json");
                request.AddHeader("bfx-nonce", nonce);
                request.AddHeader("bfx-apikey", _publicKey);
                request.AddHeader("bfx-signature", sig);
                request.AddParameter("application/json", body, ParameterType.RequestBody);

                IRestResponse response = client.Execute(request);

                if (order.State == OrderStateType.Cancel)
                {
                    return;
                }

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string responseBody = response.Content;

                    if (string.IsNullOrEmpty(responseBody))
                    {
                        SendLogMessage("Response body is empty.", LogMessageType.Error);
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
                        SendLogMessage("Invalid response array structure.", LogMessageType.Error);
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
            throw new NotImplementedException();
        }
        public List<Order> GetAllActiveOrders()
        {
            List<Order> orders = new List<Order>();

            try
            {
                _rateGateOrder.WaitToProceed();

                string nonce = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).ToString();
                string _apiPath = "v2/auth/r/orders";
                string signature = $"/api/{_apiPath}{nonce}";

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(_apiPath, Method.POST);
                string sig = ComputeHmacSha384(_secretKey, signature);

                request.AddHeader("accept", "application/json");
                request.AddHeader("bfx-nonce", nonce);
                request.AddHeader("bfx-apikey", _publicKey);
                request.AddHeader("bfx-signature", sig);

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string responseBody = response.Content;

                    if (string.IsNullOrEmpty(responseBody) || responseBody == "[]")
                    {
                        SendLogMessage("No active orders found.", LogMessageType.Trade);
                        return null;
                    }

                    List<List<object>> listOrders = JsonConvert.DeserializeObject<List<List<object>>>(response.Content);

                    if (listOrders == null)
                    {
                        SendLogMessage("Deserialization resulted in null", LogMessageType.Error);
                        return null;
                    }

                    for (int i = 0; i < listOrders.Count; i++)
                    {
                        List<object> orderData = listOrders[i];

                        Order activeOrder = new Order();

                        activeOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(orderData[5]));
                        activeOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(orderData[4]));
                        activeOrder.ServerType = ServerType.Bitfinex;
                        activeOrder.SecurityNameCode = orderData[3].ToString();
                        activeOrder.SecurityClassCode = orderData[3].ToString().StartsWith("f") ? "Futures" : "CurrencyPair";
                        activeOrder.NumberUser = Convert.ToInt32(orderData[2]);
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
                        activeOrder.PortfolioNumber = "BitfinexPortfolio";

                        orders.Add(activeOrder);
                    }
                }

                else
                {
                    SendLogMessage($" Can't get all orders. State Code: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return orders;
        }
        public void GetOrderStatus(Order order)
        {
            try
            {
                if (order == null || order.NumberUser == 0)
                {
                    SendLogMessage("Order or NumberUser is null/zero.", LogMessageType.Error);
                    return;
                }

                List<Order> ordersActive = GetAllActiveOrders();

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
                    SendLogMessage($"Order with NumberUser {order.NumberUser} not found.", LogMessageType.Error);
                    return;
                }

                MyOrderEvent?.Invoke(orderOnMarket);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }
        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllActiveOrders();

            if (orders == null || orders.Count == 0)
            {
                SendLogMessage("No active orders found.", LogMessageType.Trade);
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
                string nonce = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string _apiPath = "v2/auth/r/orders/hist";
                string signature = $"/api/{_apiPath}{nonce}";

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(_apiPath, Method.POST);
                string sig = ComputeHmacSha384(_secretKey, signature);

                request.AddHeader("accept", "application/json");
                request.AddHeader("bfx-nonce", nonce);
                request.AddHeader("bfx-apikey", _publicKey);
                request.AddHeader("bfx-signature", sig);

                IRestResponse response = client.Execute(request);

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
                                historyOrder.ServerType = ServerType.Bitfinex;
                                historyOrder.SecurityNameCode = orderData[3]?.ToString();
                                historyOrder.SecurityClassCode = historyOrder.SecurityNameCode.StartsWith("f") ? "Futures" : "CurrencyPair";
                                historyOrder.Side = orderData[7]?.ToString().ToDecimal() > 0 ? Side.Buy : Side.Sell;
                                historyOrder.State = GetOrderState(orderData[13]?.ToString());
                                string typeOrder = orderData[8].ToString();

                                if (typeOrder == "EXCHANGE LIMIT")
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
                                historyOrder.PortfolioNumber = "BitfinexPortfolio";
                                historyOrder.VolumeExecute = volume;
                                historyOrder.Volume = volume;

                                ordersHistory.Add(historyOrder);
                            }
                        }
                    }
                }

                else
                {
                    SendLogMessage($"GetOrderState. Http State Code: {response.StatusCode}, Content: {response.Content}", LogMessageType.Error);
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

        public event Action<News> NewsEvent;
        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;


        #endregion

        #region  11 Helpers
        private string ComputeHmacSha384(string apiSecret, string signature)
        {
            using HMACSHA384 hmac = new HMACSHA384(Encoding.UTF8.GetBytes(apiSecret));
            {
                byte[] output = hmac.ComputeHash(Encoding.UTF8.GetBytes(signature));

                return BitConverter.ToString(output).Replace("-", "").ToLower();
            }
        }

        #endregion

        #region 12 Log

        public event Action<string, LogMessageType> LogMessageEvent;
        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);
        }

        #endregion
    }
}





