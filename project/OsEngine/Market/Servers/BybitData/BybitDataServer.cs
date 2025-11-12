using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.BybitData.Entity;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;


namespace OsEngine.Market.Servers.BybitData
{
    internal class BybitDataServer : AServer
    {
        public BybitDataServer()
        {
            BybitDataServerRealization realization = new BybitDataServerRealization();
            ServerRealization = realization;
            NeedToHideParameters = true;
        }
    }

    public class BybitDataServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public BybitDataServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            if (!Directory.Exists(@"Data\Temp\BybitDataTempFiles\"))
            {
                Directory.CreateDirectory(@"Data\Temp\BybitDataTempFiles\");
            }
        }

        public void Connect(WebProxy proxy)
        {
            ConfigureHttpClientForString();

            HttpResponseMessage response = _httpClient.GetAsync(_mainFtpUrl).Result;

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
            get { return ServerType.BybitData; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region 2 Properties

        public DateTime ServerTime { get; set; }

        public List<IServerParameter> ServerParameters { get; set; }

        private readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        });

        private string _mainFtpUrl = "https://public.bybit.com/";

        #endregion

        #region 3 Securities

        List<Security> _securities = new List<Security>();

        public void GetSecurities()
        {
            SendLogMessage("Downloading the list of securities...", LogMessageType.System);

            string[] secCategories = { "spot", "linear", "inverse" };

            for (int i = 0; i < secCategories.Length; i++)
            {
                GetSecuritiesByCategory(secCategories[i]);
            }

            if (_securities.Count > 0)
            {
                SendLogMessage($"{_securities.Count} securities loaded", LogMessageType.System);
            }

            SecurityEvent?.Invoke(_securities);
        }

        private void GetSecuritiesByCategory(string category)
        {
            try
            {
                Dictionary<string, object> parametrs = new Dictionary<string, object>();
                parametrs.Add("limit", "1000");
                parametrs.Add("category", category);

                string security = CreatePublicQuery(parametrs, HttpMethod.Get, "/v5/market/instruments-info");
                ByBitDataResponse<ListSymbols> responseSymbols;

                if (security != null)
                {
                    responseSymbols = JsonConvert.DeserializeObject<ByBitDataResponse<ListSymbols>>(security);

                    if (responseSymbols != null
                        && responseSymbols.retCode == "0"
                        && responseSymbols.retMsg == "OK")
                    {
                        ConvertSecurities(responseSymbols, category);
                    }
                    else
                    {
                        SendLogMessage($"Securities downloading error. Code: {responseSymbols.retCode}\n"
                            + $"Message: {responseSymbols.retMsg}", LogMessageType.Error);
                    }
                }

            }
            catch (Exception ex)
            {
                SendLogMessage($"Securities request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void ConvertSecurities(ByBitDataResponse<ListSymbols> symbols, string category)
        {
            try
            {
                for (int i = 0; i < symbols.result.list.Count - 1; i++)
                {
                    Symbol oneSec = symbols.result.list[i];

                    if (oneSec.status.ToLower() == "trading")
                    {
                        Security security = new Security();
                        security.NameFull = oneSec.symbol;

                        if (category == "linear"
                            || category == "inverse")
                        {
                            security.SecurityType = SecurityType.Futures;
                        }
                        else
                        {
                            security.SecurityType = SecurityType.CurrencyPair;
                        }

                        if (category == "spot")
                        {
                            security.Name = oneSec.symbol;
                            security.NameId = oneSec.symbol;
                            security.NameClass = "Spot_" + oneSec.quoteCoin;
                        }
                        else if (category == "linear")
                        {
                            security.Name = oneSec.symbol + ".P";
                            security.NameId = oneSec.symbol + ".P";

                            if (security.NameFull.EndsWith("PERP"))
                            {
                                security.NameClass = oneSec.contractType + "_USDC";
                            }
                            else
                            {
                                security.NameClass = oneSec.contractType;
                            }

                        }
                        else if (category == "inverse")
                        {
                            security.Name = oneSec.symbol + ".I";
                            security.NameId = oneSec.symbol + ".I";
                            security.NameClass = oneSec.contractType;
                        }
                        else
                        {
                            security.NameClass = oneSec.contractType;
                        }

                        security.PriceStep = 0;
                        security.PriceStepCost = 0;
                        security.State = SecurityStateType.Activ;
                        security.Exchange = ServerType.Bybit.ToString();
                        security.Lot = 1;

                        _securities.Add(security);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Securities convert error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private List<Portfolio> _myPortfolios = new List<Portfolio>();

        public void GetPortfolios()
        {
            Portfolio newPortfolio = new Portfolio();
            newPortfolio.Number = "BybitData Virtual Portfolio";
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
            try
            {
                if (actualTime < startTime || actualTime > endTime)
                {
                    return null;
                }

                string category = "spot";

                if (security.Name.EndsWith(".P"))
                {
                    category = "linear";
                }
                else if (security.Name.EndsWith(".I"))
                {
                    category = "inverse";
                }

                Dictionary<string, object> parametrs = new Dictionary<string, object>();
                parametrs["category"] = category;
                parametrs["symbol"] = security.Name.Split('.')[0];
                parametrs["interval"] = timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes.ToString();
                parametrs["limit"] = 1000;
                parametrs["start"] = TimeManager.GetTimeStampMilliSecondsToDateTime(startTime);
                parametrs["end"] = TimeManager.GetTimeStampMilliSecondsToDateTime(endTime);

                List<Candle> candles = new List<Candle>();

                do
                {
                    string candlesQuery = CreatePublicQuery(parametrs, HttpMethod.Get, "/v5/market/kline");

                    if (candlesQuery == null)
                    {
                        break;
                    }

                    List<Candle> newCandles = GetListCandles(candlesQuery);

                    if (newCandles != null && newCandles.Count > 0)
                    {
                        candles.InsertRange(0, newCandles);

                        if (candles[0].TimeStart > startTime)
                        {
                            parametrs["end"] = TimeManager.GetTimeStampMilliSecondsToDateTime(candles[0].TimeStart.AddMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * -1));
                        }
                        else
                        {
                            return candles;
                        }
                    }
                    else
                    {
                        break;
                    }

                } while (true);

                if (candles.Count > 0)
                {
                    return candles;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Candles request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }

            return null;
        }

        private List<Candle> GetListCandles(string candlesQuery)
        {
            List<Candle> candles = new List<Candle>();

            try
            {
                BybitDataCandlesResponse<List<string>> response = JsonConvert.DeserializeObject<BybitDataCandlesResponse<List<string>>>(candlesQuery);

                if (response != null
                        && response.retCode == "0"
                        && response.retMsg == "OK")
                {
                    for (int i = 0; i < response.result.list.Count; i++)
                    {
                        List<string> oneSec = response.result.list[i];

                        Candle candle = new Candle();

                        candle.TimeStart = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(oneSec[0].ToString())).UtcDateTime;
                        candle.Open = oneSec[1].ToString().ToDecimal();
                        candle.High = oneSec[2].ToString().ToDecimal();
                        candle.Low = oneSec[3].ToString().ToDecimal();
                        candle.Close = oneSec[4].ToString().ToDecimal();
                        candle.Volume = oneSec[5].ToString().ToDecimal();
                        candle.State = CandleState.Finished;

                        candles.Add(candle);
                    }

                    candles.Reverse();
                }
                else
                {
                    SendLogMessage($"GetListCandles>. Candles error. Code: {response.retCode}\n"
                            + $"Message: {response.retMsg}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"GetListCandles>. Candles request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return new List<Candle>();
            }
            return candles;
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

            string secGroup = "";

            if (security.SecurityType == SecurityType.CurrencyPair)
            {
                secGroup = "spot/";
            }
            else
            {
                secGroup = "trading/";
            }

            try
            {
                (List<string> archivesNames, DateTime earliest, DateTime latest) = ParseFilesByDate($"{_mainFtpUrl}{secGroup}{security.NameFull}/", startTime, endTime);

                if (archivesNames != null && (earliest != DateTime.MinValue || latest != DateTime.MinValue))
                {
                    string aboutDataMsg = string.Empty;

                    if (startTime < earliest)
                    {
                        startTime = earliest;

                        aboutDataMsg = $"The data on the {security.Name} starts from {earliest.ToShortDateString()} \n";
                    }

                    if (endTime > latest)
                    {
                        endTime = latest;

                        aboutDataMsg += $"The data on the {security.Name} ends on {latest.ToShortDateString()}";
                    }

                    if (aboutDataMsg != string.Empty)
                    {
                        SendLogMessage(aboutDataMsg, LogMessageType.System);
                    }
                }
                else
                {
                    SendLogMessage($"Error in getting the data period.", LogMessageType.Error);
                    return null;
                }

                for (int i = 0; i < archivesNames.Count; i++)
                {
                    string archivePath = $"{_mainFtpUrl}{secGroup}{security.NameFull}/{archivesNames[i]}";

                    string gzipArchivePath = DownloadGZipArchive(archivePath);

                    if (gzipArchivePath != null)
                    {
                        string csvFilePath = GetSCVFileFromArchive(gzipArchivePath);

                        trades.AddRange(ParseCsvFileToTrades(csvFilePath, security));
                    }
                    else
                    {
                        return null;
                    }
                }

                if (trades.Count == 0)
                {
                    return null;
                }

                trades.Sort(CompareTradesByTime);

                return trades;
            }
            catch (Exception error)
            {
                SendLogMessage($"Trades data downloading error: {error}", LogMessageType.Error);
                return null;
            }
        }

        private string DownloadGZipArchive(string path)
        {
            try
            {
                string tempGzipPath = @"Data\Temp\ByBitDataTempFiles\" + Path.GetRandomFileName();

                ConfigureHttpClientForFile();

                using (HttpResponseMessage response = _httpClient.GetAsync(path).GetAwaiter().GetResult())
                {
                    response.EnsureSuccessStatusCode();

                    using (Stream contentStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())

                    using (FileStream fileStream = new FileStream(tempGzipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        contentStream.CopyTo(fileStream);
                    }
                }

                return tempGzipPath;
            }
            catch (Exception ex)
            {
                SendLogMessage("Сouldn't upload zip archive.\n" + ex, LogMessageType.Error);
                return null;
            }
        }

        private string GetSCVFileFromArchive(string tempGzipPath)
        {
            string extractPath = @"Data\Temp\ByBitDataTempFiles\";

            ExtractGZip(tempGzipPath, extractPath);

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

        public void ExtractGZip(string gzPath, string extractPath)
        {
            string csvFilePath = extractPath + "temp.csv";

            try
            {
                using (FileStream originalFileStream = new FileStream(gzPath, FileMode.Open))
                using (FileStream decompressedFileStream = File.Create(csvFilePath))
                using (GZipStream decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
                {
                    decompressionStream.CopyTo(decompressedFileStream);
                }
            }
            catch (InvalidDataException ex)
            {
                SendLogMessage($"Couldn't extract archive\n" + ex, LogMessageType.Error);

                File.Delete(gzPath);
            }
        }

        private (List<string>, DateTime, DateTime) ParseFilesByDate(string url, DateTime startTime, DateTime endTime)
        {
            ConfigureHttpClientForString();

            string html = _httpClient.GetStringAsync(url).Result;

            Regex regex = new Regex(@"<a href=""([^""]+)"">([^<]+)</a>");
            MatchCollection matches = regex.Matches(html);

            List<string> dailyFiles = new List<string>();
            List<DateTime> allDailyDates = new List<DateTime>();

            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                if (match.Groups.Count < 3) continue;

                string fileName = match.Groups[1].Value;

                if (IsMonthlyFile(fileName))
                    continue;

                DateTime? date = ExtractDateFromFileName(fileName);
                if (date.HasValue)
                {
                    allDailyDates.Add(date.Value);

                    if (date.Value.Date >= startTime.Date && date.Value.Date <= endTime.Date)
                    {
                        dailyFiles.Add(fileName);
                    }
                }
            }

            if (allDailyDates.Count == 0)
            {
                return (new List<string>(), DateTime.MinValue, DateTime.MinValue);
            }

            DateTime earliestDate = allDailyDates[0];
            DateTime latestDate = allDailyDates[0];

            for (int i = 1; i < allDailyDates.Count; i++)
            {
                if (allDailyDates[i] < earliestDate)
                    earliestDate = allDailyDates[i];

                if (allDailyDates[i] > latestDate)
                    latestDate = allDailyDates[i];
            }

            return (dailyFiles, earliestDate, latestDate);
        }

        private bool IsMonthlyFile(string fileName)
        {
            Regex monthlyPattern = new Regex(@".*-\d{4}-\d{2}\.(csv\.gz|csv)");
            return monthlyPattern.IsMatch(fileName);
        }

        private DateTime? ExtractDateFromFileName(string fileName)
        {
            // ADAUSDT2021-03-18.csv.gz
            Regex datePattern = new Regex(@".*?(\d{4}-\d{2}-\d{2})\.");
            Match match = datePattern.Match(fileName);

            if (match.Success && match.Groups.Count > 1)
            {
                if (DateTime.TryParse(match.Groups[1].Value, out DateTime date))
                {
                    return date;
                }
            }

            return null;
        }

        private List<Trade> ParseCsvFileToTrades(string csvFilePath, Security security)
        {
            // SPOT day: id,timestamp,price,volume,side,rpi
            //  1,1756771201583,0.000009461,18057687,buy,0

            // FUT_Delivery: timestamp,symbol,side,size,price,tickDirection,trdMatchID,grossValue,homeNotional,foreignNotional
            //1741910443.7357,BTC-14MAR25,Buy,0.001,81095.00,MinusTick,26bb8b97-1991-503f-b2c2-f4e8d0b94721,8.1095e+09,0.001,81.095

            // fUT_USDT: timestamp,symbol,side,size,price,tickDirection,trdMatchID,grossValue,homeNotional,foreignNotional,RPI
            // 1756771201.5983,ATOMUSDT,Buy,0.1,4.393,PlusTick,5e580a5e-b412-5c7b-bb94-edef31ee0d0c,4.393e+07,0.1,0.4393,0

            List<Trade> trades = new List<Trade>();

            string[] lines = File.ReadAllLines(csvFilePath);

            for (int i = 0; i < lines.Length; i++)
            {
                if (char.IsLetter(lines[i][0]))
                    continue;

                string[] tradeParts = lines[i].Split(',', StringSplitOptions.RemoveEmptyEntries);

                Trade trade = new Trade();

                if (security.SecurityType == SecurityType.CurrencyPair)
                {
                    string newId = DateTime.UtcNow.Ticks.ToString();

                    trade.Time = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(tradeParts[1])).DateTime;
                    trade.Price = tradeParts[2].ToDecimal();
                    trade.MicroSeconds = 0;
                    trade.Id = newId + tradeParts[0];
                    trade.Volume = Math.Abs(tradeParts[3].ToDecimal());
                    trade.SecurityNameCode = security.Name;
                    trade.Side = tradeParts[4] == "sell" ? Side.Sell : Side.Buy;
                }
                else
                {
                    trade.Time = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(long.Parse(tradeParts[0].Split('.')[0]));
                    trade.Price = tradeParts[4].ToDecimal();
                    trade.MicroSeconds = 0;
                    trade.Id = tradeParts[6].Split('-')[^1];
                    trade.Volume = Math.Abs(tradeParts[3].ToDecimal());
                    trade.SecurityNameCode = security.Name;
                    trade.Side = tradeParts[2] == "Sell" ? Side.Sell : Side.Buy;
                }

                trades.Add(trade);
            }

            string[] files = Directory.GetFiles(@"Data\Temp\ByBitDataTempFiles\");

            if (files.Length > 0)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    File.Delete(files[i]);
                }
            }

            return trades;
        }

        private int CompareTradesByTime(Trade x, Trade y)
        {
            return x.Time.CompareTo(y.Time);
        }

        #endregion

        #region Queries

        private RateGate _rateGate = new RateGate(1, TimeSpan.FromMilliseconds(15));

        private string _restUrl = "https://api.bybit.com";

        private string _httpClientLocker = "httpClientLocker";

        private string CreatePublicQuery(Dictionary<string, object> parameters, HttpMethod httpMethod, string uri)
        {
            lock (_httpClientLocker)
            {
                _rateGate.WaitToProceed();
            }

            try
            {
                string jsonPayload = parameters.Count > 0 ? GenerateQueryString(parameters) : "";

                HttpRequestMessage request = new HttpRequestMessage(httpMethod, _restUrl + uri + $"?{jsonPayload}");
                HttpResponseMessage response = _httpClient?.SendAsync(request).Result;

                if (response == null)
                {
                    return null;
                }

                string response_msg = response.Content.ReadAsStringAsync().Result;

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return response_msg;
                }
                else
                {
                    if (response_msg.Contains("\"retCode\": 10006"))
                    {
                        SendLogMessage($"Limit 1000.Code:{response.StatusCode}, Message:{response_msg}", LogMessageType.Error);
                    }
                    else
                    {
                        SendLogMessage($"CreatePublicQuery> BybitUnified Client.Code:{response.StatusCode}, Message:{response_msg}", LogMessageType.Error);
                    }

                    return null;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        private string GenerateQueryString(Dictionary<string, object> parameters)
        {
            List<string> pairs = new List<string>();
            string[] keysArray = new string[parameters.Count];
            parameters.Keys.CopyTo(keysArray, 0);

            for (int i = 0; i < keysArray.Length; i++)
            {
                string key = keysArray[i];
                pairs.Add($"{key}={parameters[key]}");
            }

            string res = string.Join("&", pairs);

            return res;
        }

        private void ConfigureHttpClientForString()
        {
            _httpClient.DefaultRequestHeaders.Clear();

            _httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br, zstd");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            _httpClient.DefaultRequestHeaders.Add("Priority", "u=1, i");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.bybit.com/derivatives/en/history-data");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Not;A=Brand\";v=\"99\", \"Google Chrome\";v=\"139\", \"Chromium\";v=\"139\"");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
            _httpClient.DefaultRequestHeaders.Add("Upgrade-insecure-requests", "1");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("X-Kl-Saas-Ajax-Request", "Ajax_Request");
        }

        private void ConfigureHttpClientForFile()
        {
            _httpClient.DefaultRequestHeaders.Clear();

            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/ap");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br, zstd");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            _httpClient.DefaultRequestHeaders.Add("Cache-control", "max-age=0");
            _httpClient.DefaultRequestHeaders.Add("Priority", "u=0, i");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Not_A Brand\";v=\"9\", \"Chromium\";v=\"139\", \"Google Chrome\";v=\"139\"");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
            _httpClient.DefaultRequestHeaders.Add("Upgrade-insecure-requests", "1");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
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

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            return null;
        }

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

        public OrderStateType GetOrderStatus(Order order)
        {
            return OrderStateType.None;
        }

        public bool SubscribeNews()
        {
            return false;
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
