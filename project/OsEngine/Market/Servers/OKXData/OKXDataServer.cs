using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.OKXData.Entity;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;

namespace OsEngine.Market.Servers.OKXData
{
    public class OKXDataServer : AServer
    {
        public OKXDataServer()
        {
            OKXDataServerRealization realization = new OKXDataServerRealization();
            ServerRealization = realization;
            NeedToHideParameters = true;
        }
    }

    public class OKXDataServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public OKXDataServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            if (!Directory.Exists(@"Data\Temp\OKXDataTempFiles\"))
            {
                Directory.CreateDirectory(@"Data\Temp\OKXDataTempFiles\");
            }
        }

        private WebProxy _myProxy;

        public void Connect(WebProxy proxy)
        {
            _myProxy = proxy;

            string startUrl = "https://www.okx.com/historical-data";

            HttpResponseMessage response = _httpClient.GetAsync(startUrl).Result;

            if (response.StatusCode == HttpStatusCode.OK)
            {
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent();
            }
            else
            {
                SendLogMessage($"Connect server error: {response.StatusCode}", LogMessageType.Error);
            }

            response.Dispose();
        }

        public void Dispose()
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public ServerType ServerType
        {
            get { return ServerType.OKXData; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region 2 Properties

        public DateTime ServerTime { get; set; }

        public List<IServerParameter> ServerParameters { get; set; }

        private HttpClient _httpClient = new HttpClient();

        private string _baseUrl = "https://www.okx.com";

        public RateGate _rateGateCandles = new RateGate(1, TimeSpan.FromMilliseconds(100));

        #endregion

        #region 3 Securities

        List<Security> _securities = new List<Security>();

        public void GetSecurities()
        {
            SendLogMessage("Downloading the list of securities...", LogMessageType.System);

            try
            {
                SecurityRespOkxData securityResponseFutures = GetFuturesSecurities();
                SecurityRespOkxData securityResponseSpot = GetSpotSecurities();
                securityResponseFutures.data.AddRange(securityResponseSpot.data);

                UpdatePairs(securityResponseFutures);
            }
            catch (Exception error)
            {
                if (error.Message.Equals("Unexpected character encountered while parsing value: <. Path '', line 0, position 0."))
                {
                    SendLogMessage("service is unavailable", LogMessageType.Error);
                    return;
                }

                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }

            if (_securities.Count > 0)
            {
                SendLogMessage($"{_securities.Count} securities loaded", LogMessageType.System);
            }

            SecurityEvent(_securities);
        }

        private SecurityRespOkxData GetFuturesSecurities()
        {
            try
            {
                RestRequest requestRest = new RestRequest("/api/v5/public/instruments?instType=SWAP", Method.GET);
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"GetFuturesSecurities - {response.Content}", LogMessageType.Error);
                }

                SecurityRespOkxData securityResponse = JsonConvert.DeserializeAnonymousType(response.Content, new SecurityRespOkxData());

                return securityResponse;
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        private SecurityRespOkxData GetSpotSecurities()
        {
            try
            {
                RestRequest requestRest = new RestRequest("/api/v5/public/instruments?instType=SPOT", Method.GET);
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"GetSpotSecurities - {response.Content}", LogMessageType.Error);
                }

                SecurityRespOkxData securityResponse = JsonConvert.DeserializeAnonymousType(response.Content, new SecurityRespOkxData());

                return securityResponse;
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        private void UpdatePairs(SecurityRespOkxData securityResponse)
        {
            for (int i = 0; i < securityResponse.data.Count; i++)
            {
                OkxSecurityData item = securityResponse.data[i];

                Security security = new Security();

                SecurityType securityType = SecurityType.CurrencyPair;

                if (item.instType.Equals("SWAP"))
                {
                    securityType = SecurityType.Futures;
                }
                else if (item.instType.Equals("OPTION"))
                {
                    continue;
                }

                security.Name = item.instId;
                security.NameFull = item.instId;

                if (item.lotSz == string.Empty)
                {
                    continue;
                }

                security.Lot = item.lotSz.ToDecimal();
                string volStep = item.minSz.Replace(',', '.');

                if (volStep != null
                        && volStep.Length > 0 &&
                        volStep.Split('.').Length > 1)
                {
                    security.DecimalsVolume = volStep.Split('.')[1].Length;
                }

                security.MinTradeAmountType = MinTradeAmountType.Contract;
                security.MinTradeAmount = item.minSz.ToDecimal();
                security.VolumeStep = item.lotSz.ToDecimal();


                if (securityType == SecurityType.CurrencyPair)
                {
                    security.NameClass = "SPOT_" + item.quoteCcy;
                }

                if (securityType == SecurityType.Futures)
                {
                    if (item.instId.Contains("-USD-"))
                    {
                        security.NameClass = "FUT_PERP_USD";
                    }
                    else
                    {
                        security.NameClass = "FUT_PERP_" + item.settleCcy;
                    }

                    security.Lot = item.ctVal.ToDecimal();
                }

                security.Exchange = ServerType.OKX.ToString();
                security.NameId = item.instId;
                security.SecurityType = securityType;
                security.PriceStep = item.tickSz.ToDecimal();
                security.PriceStepCost = security.PriceStep;

                if (security.PriceStep < 1)
                {
                    string prStep = security.PriceStep.ToString(CultureInfo.InvariantCulture);
                    security.Decimals = Convert.ToString(prStep).Split('.')[1].Split('1')[0].Length + 1;
                }
                else
                {
                    security.Decimals = 0;
                }

                security.State = SecurityStateType.Activ;

                // The Expiration field is used to store the listing date.
                security.Expiration = DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(item.listTime)).DateTime;

                _securities.Add(security);
            }

            if (SecurityEvent != null)
            {
                SecurityEvent(_securities);
            }
        }

        public event Action<List<Security>> SecurityEvent;
        #endregion

        #region 4 Portfolios

        private List<Portfolio> _myPortfolios = new List<Portfolio>();

        public void GetPortfolios()
        {
            Portfolio newPortfolio = new Portfolio();
            newPortfolio.Number = "OKXData Virtual Portfolio";
            newPortfolio.ValueCurrent = 1;
            _myPortfolios.Add(newPortfolio);

            if (_myPortfolios.Count != 0)
            {
                PortfolioEvent(_myPortfolios);
            }
        }

        public event Action<List<Portfolio>> PortfolioEvent;
        #endregion

        #region 5 Data

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
            endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
            actualTime = DateTime.SpecifyKind(actualTime, DateTimeKind.Utc);

            if (timeFrameBuilder.TimeFrame == TimeFrame.Min2
               || timeFrameBuilder.TimeFrame == TimeFrame.Min10)
            {
                return null;
            }

            if (actualTime > endTime)
            {
                return null;
            }

            if (startTime > endTime)
            {
                return null;
            }

            if (endTime > DateTime.UtcNow)
            {
                endTime = DateTime.UtcNow;
            }

            if (startTime < security.Expiration)
            {
                startTime = security.Expiration;

                SendLogMessage($"Listing time by {security.Name} started {startTime}", LogMessageType.System);
            }

            int CountCandlesNeedToLoad = GetCountCandlesFromTimeInterval(startTime, endTime, timeFrameBuilder.TimeFrameTimeSpan);

            List<Candle> candles = GetCandleDataHistory(security.Name, timeFrameBuilder.TimeFrameTimeSpan, CountCandlesNeedToLoad, TimeManager.GetTimeStampMilliSecondsToDateTime(endTime));

            for (int i = 0; i < candles.Count; i++)
            {
                if (candles[i].TimeStart > endTime)
                {
                    candles.RemoveAt(i);
                    i--;
                }
            }

            for (int i = 1; i < candles.Count; i++)
            {
                if (candles[i - 1].TimeStart == candles[i].TimeStart)
                {
                    candles.RemoveAt(i);
                    i--;
                }
            }

            return candles;
        }

        private int GetCountCandlesFromTimeInterval(DateTime startTime, DateTime endTime, TimeSpan timeFrameSpan)
        {
            TimeSpan timeSpanInterval = endTime - startTime;

            if (timeFrameSpan.Hours != 0)
            {
                return Convert.ToInt32(timeSpanInterval.TotalHours / timeFrameSpan.Hours);
            }
            else if (timeFrameSpan.Days != 0)
            {
                return Convert.ToInt32(timeSpanInterval.TotalDays / timeFrameSpan.Days);
            }
            else
            {
                return Convert.ToInt32(timeSpanInterval.TotalMinutes / timeFrameSpan.Minutes);
            }
        }

        public List<Candle> GetCandleDataHistory(string nameSec, TimeSpan tf, int NumberCandlesToLoad, long DataEnd)
        {
            OkxCandlesResponce securityResponse = GetResponseDataCandles(nameSec, tf, NumberCandlesToLoad, DataEnd);

            List<Candle> candles = new List<Candle>();

            ConvertCandles(securityResponse, candles);

            candles.Reverse();

            return candles;
        }

        private OkxCandlesResponce GetResponseDataCandles(string nameSec, TimeSpan tf, int NumberCandlesToLoad, long DataEnd)
        {
            try
            {
                string bar = GetStringBar(tf);

                OkxCandlesResponce candlesResponse = new OkxCandlesResponce();
                candlesResponse.data = new List<List<string>>();

                do
                {
                    _rateGateCandles.WaitToProceed();

                    int limit = NumberCandlesToLoad;

                    if (NumberCandlesToLoad > 300)
                    {
                        limit = 300;
                    }

                    string after = $"&after={Convert.ToString(DataEnd)}";

                    if (candlesResponse.data.Count != 0)
                    {
                        after = $"&after={candlesResponse.data[candlesResponse.data.Count - 1][0]}";
                    }

                    string url = _baseUrl + $"/api/v5/market/history-candles?instId={nameSec}&bar={bar}&limit={limit}" + after;

                    RestClient client = new RestClient(url);
                    RestRequest request = new RestRequest(Method.GET);
                    IRestResponse Response = client.Execute(request);

                    if (Response.StatusCode == HttpStatusCode.OK)
                    {
                        candlesResponse.data.AddRange(JsonConvert.DeserializeAnonymousType(Response.Content, new OkxCandlesResponce()).data);
                    }
                    else
                    {
                        SendLogMessage($"GetResponseDataCandles - {Response.Content}", LogMessageType.Error);
                    }

                    NumberCandlesToLoad -= limit;

                } while (NumberCandlesToLoad > 0);

                return candlesResponse;
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }

            return null;
        }

        private string GetStringBar(TimeSpan tf)
        {
            try
            {
                if (tf.Hours != 0)
                {
                    return $"{tf.Hours}H";
                }
                if (tf.Minutes != 0)
                {
                    return $"{tf.Minutes}m";
                }
                if (tf.Days != 0)
                {
                    return $"{tf.Days}D";
                }
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }

            return String.Empty;
        }

        private void ConvertCandles(OkxCandlesResponce candlesResponse, List<Candle> candles)
        {
            for (int j = 0; j < candlesResponse.data.Count; j++)
            {
                Candle candle = new Candle();
                try
                {
                    candle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(candlesResponse.data[j][0]));

                    candle.Open = candlesResponse.data[j][1].ToDecimal();
                    candle.High = candlesResponse.data[j][2].ToDecimal();
                    candle.Low = candlesResponse.data[j][3].ToDecimal();
                    candle.Close = candlesResponse.data[j][4].ToDecimal();
                    candle.Volume = candlesResponse.data[j][5].ToDecimal();

                    candles.Add(candle);
                }
                catch (Exception error)
                {
                    SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
                }
            }
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (ServerStatus != ServerConnectStatus.Connect)
            {
                return null;
            }

            if (actualTime > endTime)
            {
                return null;
            }

            List<Trade> trades = new List<Trade>();

            // trades are available until 16:00 yesterday
            if (endTime > DateTime.UtcNow.AddDays(-1))
            {
                endTime = DateTime.UtcNow.AddDays(-1);
            }

            if (startTime < security.Expiration)
            {
                startTime = security.Expiration;

                SendLogMessage($"Listing time by {security.Name} started {startTime}", LogMessageType.System);
            }

            if (startTime > endTime)
            {
                return null;
            }

            try
            {
                // https://www.okx.com/cdn/okex/traderecords/aggtrades/daily/20250120/TRUMP-USDT-SWAP-aggtrades-2025-01-20.zip

                DateTime startLoop = startTime;

                while (startLoop <= endTime)
                {
                    string date1 = startLoop.ToString("yyyyMMdd");

                    string date2 = startLoop.ToString("yyyy-MM-dd");

                    string path = $"{_baseUrl}/cdn/okex/traderecords/aggtrades/daily/{date1}/{security.Name}-aggtrades-{date2}.zip";

                    string zipArchivePath = DownloadZipArchive(path);

                    if (zipArchivePath != null)
                    {
                        string csvFilePath = GetSCVFileFromArchive(zipArchivePath);

                        trades.AddRange(ParseCsvFileToTrades(csvFilePath, security.Name));
                    }
                    else
                    {
                        return null;
                    }

                    startLoop = startLoop.AddDays(1);
                }

                trades.Sort(new TradeComparer());

                if (trades.Count == 0)
                    return null;
                else
                    return trades;
            }
            catch (Exception error)
            {
                SendLogMessage($"Trades data downloading error: {error}", LogMessageType.Error);
                return null;
            }
        }

        private List<Trade> ParseCsvFileToTrades(string csvFilePath, string secName)
        {
            List<Trade> trades = new List<Trade>();

            string[] lines = File.ReadAllLines(csvFilePath);

            for (int i = 0; i < lines.Length; i++)
            {
                if (char.IsLetter(lines[i][0]))
                    continue;

                string[] tradeParts = lines[i].Split(',', StringSplitOptions.RemoveEmptyEntries);

                Trade trade = new Trade();

                // trade_id/side/size/price/created_time
                // 56666271,buy,2.0,0.8726,1751348199680

                trade.Id = tradeParts[0];
                trade.Side = tradeParts[1] == "buy" ? Side.Buy : Side.Sell;
                trade.Volume = Math.Abs(tradeParts[2].ToDecimal());
                trade.Price = tradeParts[3].ToDecimal();
                trade.Time = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(tradeParts[4])).DateTime;
                trade.MicroSeconds = 0;
                trade.SecurityNameCode = secName;

                trades.Add(trade);
            }

            string[] files = Directory.GetFiles(@"Data\Temp\OKXDataTempFiles\");

            if (files.Length > 0)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    File.Delete(files[i]);
                }
            }

            return trades;
        }

        private string DownloadZipArchive(string path)
        {
            try
            {
                string tempZipPath = @"Data\Temp\OKXDataTempFiles\" + Path.GetRandomFileName();

                using (HttpResponseMessage response = _httpClient.GetAsync(path).GetAwaiter().GetResult())
                {
                    response.EnsureSuccessStatusCode();

                    using (Stream contentStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())

                    using (FileStream fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        contentStream.CopyTo(fileStream);
                    }
                }

                return tempZipPath;
            }
            catch (Exception ex)
            {
                SendLogMessage("Сouldn't upload zip archive.\n" + ex.Message, LogMessageType.System);
                return null;
            }
        }

        private string GetSCVFileFromArchive(string tempZipPath)
        {
            string extractPath = @"Data\Temp\OKXDataTempFiles\";

            SafeExtractZip(tempZipPath, extractPath);

            // search first CSV file in archive
            string[] csvFiles = Directory.GetFiles(extractPath, "*.csv");

            if (csvFiles.Length == 0)
            {
                SendLogMessage("The CSV file was not found in the archive", LogMessageType.Error);
                return null;
            }
            else
            {
                string csvFilePath = csvFiles[0];
                return csvFilePath;
            }
        }

        private void SafeExtractZip(string zipPath, string extractPath)
        {
            try
            {
                if (!File.Exists(zipPath))
                    SendLogMessage("ZIP not found", LogMessageType.Error);

                if (new FileInfo(zipPath).Length == 0)
                    SendLogMessage("ZIP is empty", LogMessageType.Error);

                // trying to open the archive for verification
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    archive.ExtractToDirectory(extractPath);
                    return;
                }
            }
            catch (InvalidDataException ex)
            {
                SendLogMessage($"Couldn't extract archive\n" + ex, LogMessageType.Error);

                File.Delete(zipPath);
            }
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            return null;
        }

        #endregion

        #region 6 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;
        #endregion

        #region 7 Unused methods

        public void Subscribe(Security security)
        {

        }

        public void SendOrder(Order order)
        {

        }

        public void CancelAllOrders()
        {

        }

        public void CancelAllOrdersToSecurity(Security security)
        {

        }

        public bool CancelOrder(Order order)
        {
            return false;
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        public void GetAllActivOrders()
        {

        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            return null;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            return null;
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            return OrderStateType.None;
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }
        public event Action<MarketDepth> MarketDepthEvent { add { } remove { } }
        public event Action<Trade> NewTradesEvent { add { } remove { } }
        public event Action<Order> MyOrderEvent { add { } remove { } }
        public event Action<MyTrade> MyTradeEvent { add { } remove { } }
        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }
        public event Action<Funding> FundingUpdateEvent { add { } remove { } }
        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        #endregion
    }
}
