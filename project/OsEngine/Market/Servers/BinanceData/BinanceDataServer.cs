using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.BinanceData.Entity;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Serialization;


namespace OsEngine.Market.Servers.BinanceData
{
    public class BinanceDataServer : AServer
    {
        public BinanceDataServer()
        {
            BinanceDataServerRealization realization = new BinanceDataServerRealization();
            ServerRealization = realization;
            NeedToHideParameters = true;
        }
    }

    public class BinanceDataServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public BinanceDataServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            if (!Directory.Exists(_tempDirectory))
            {
                Directory.CreateDirectory(_tempDirectory);
            }
        }

        public void Connect(WebProxy proxy)
        {
            try
            {
                RestRequest request = new(Method.GET);
                RestClient client = new("https://data.binance.vision/?prefix=data/");
                request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36");

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ServerStatus = ServerConnectStatus.Connect;
                    ConnectEvent();
                }
                else
                {
                    SendLogMessage($"Connect server error: {response.StatusCode}", LogMessageType.Error);
                }

            }
            catch (Exception ex)
            {
                SendLogMessage($"Connect server error\n{ex.Message}", LogMessageType.Error);
            }
        }

        public void Dispose()
        {
            string[] files = Directory.GetFiles(_tempDirectory);

            if (files.Length > 0)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    File.Delete(files[i]);
                }
            }

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public ServerType ServerType
        {
            get { return ServerType.BinanceData; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region 2 Properties

        public DateTime ServerTime { get; set; }

        public List<IServerParameter> ServerParameters { get; set; }

        private string _tempDirectory = @"Data\Temp\BinanceDataTempFiles\";

        #endregion

        #region 3 Securities

        List<Security> _securities = new List<Security>();

        public void GetSecurities()
        {
            SendLogMessage("Downloading the list of securities...", LogMessageType.System);

            GetSpotSecurities();

            GetFutCOINMSecurities();

            GetFutUSDTMSecurities();

            if (_securities.Count > 0)
            {
                SendLogMessage($"{_securities.Count} securities loaded", LogMessageType.System);
            }

            SecurityEvent(_securities);
        }

        private void GetSpotSecurities()
        {
            try
            {
                string response = GetStringRequest("https://api.binance.com", "/api/v3/exchangeInfo");

                BinanceSecurityResponse securityInfo = JsonConvert.DeserializeAnonymousType(response, new BinanceSecurityResponse());

                if (securityInfo.symbols != null && securityInfo.symbols.Count > 0)
                {
                    for (int i = 0; i < securityInfo.symbols.Count; i++)
                    {
                        BinanceSecurityInfo sec = securityInfo.symbols[i];

                        Security security = new Security();
                        security.Name = sec.symbol;
                        security.NameFull = sec.symbol;
                        security.NameClass = "SPOT_" + sec.quoteAsset;
                        security.NameId = sec.symbol + ".S";
                        security.PriceStep = 0;
                        security.PriceStepCost = 0;
                        security.SecurityType = SecurityType.CurrencyPair;

                        _securities.Add(security);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Spot securities downloading error: {ex}", LogMessageType.Error);
            }
        }

        private void GetFutUSDTMSecurities()
        {
            try
            {
                string response = GetStringRequest("https://fapi.binance.com", "/fapi/v1/exchangeInfo");

                BinanceSecurityResponse securityInfo = JsonConvert.DeserializeAnonymousType(response, new BinanceSecurityResponse());

                if (securityInfo.symbols != null && securityInfo.symbols.Count > 0)
                {
                    for (int i = 0; i < securityInfo.symbols.Count; i++)
                    {
                        BinanceSecurityInfo sec = securityInfo.symbols[i];

                        Security security = new Security();
                        security.Name = sec.symbol;
                        security.NameFull = sec.symbol;
                        security.NameClass = "FUT_USDT-M_" + sec.quoteAsset;
                        security.NameId = sec.symbol + ".F";
                        security.PriceStep = 0;
                        security.PriceStepCost = 0;
                        security.SecurityType = SecurityType.Futures;

                        _securities.Add(security);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Futures USDT-M securities downloading error: {ex}", LogMessageType.Error);
            }
        }

        private void GetFutCOINMSecurities()
        {
            try
            {
                string response = GetStringRequest("https://dapi.binance.com", "/dapi/v1/exchangeInfo");

                BinanceSecurityResponse securityInfo = JsonConvert.DeserializeAnonymousType(response, new BinanceSecurityResponse());

                if (securityInfo.symbols != null && securityInfo.symbols.Count > 0)
                {
                    for (int i = 0; i < securityInfo.symbols.Count; i++)
                    {
                        BinanceSecurityInfo sec = securityInfo.symbols[i];

                        Security security = new Security();
                        security.Name = sec.symbol;
                        security.NameFull = sec.symbol;

                        if (sec.symbol.EndsWith("PERP"))
                            security.NameClass = "FUT_COIN-M_PERP";
                        else
                            security.NameClass = "FUT_COIN-M_Delivery";

                        security.NameId = sec.symbol + ".F";
                        security.PriceStep = 0;
                        security.PriceStepCost = 0;
                        security.SecurityType = SecurityType.Futures;

                        _securities.Add(security);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Futures COIN-M securities downloading error: {ex}", LogMessageType.Error);
            }
        }


        public event Action<List<Security>> SecurityEvent;
        #endregion

        #region 4 Portfolios

        private List<Portfolio> _myPortfolios = new List<Portfolio>();

        public void GetPortfolios()
        {
            Portfolio newPortfolio = new Portfolio();
            newPortfolio.Number = "BinanceData Virtual Portfolio";
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
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    return null;
                }

                List<Candle> candles = new List<Candle>();

                if (actualTime > endTime)
                {
                    return null;
                }

                actualTime = startTime;

                string needTf = "";

                switch ((int)timeFrameBuilder.TimeFrame)
                {
                    case 7:
                        needTf = "1m";
                        break;
                    case 10:
                        needTf = "5m";
                        break;
                    case 12:
                        needTf = "15m";
                        break;
                    case 14:
                        needTf = "30m";
                        break;
                    case 16:
                        needTf = "1h";
                        break;
                    case 17:
                        needTf = "2h";
                        break;
                    case 18:
                        needTf = "4h";
                        break;
                    case 19:
                        needTf = "1d";
                        break;
                }

                string secGroup = "";

                if (security.SecurityType == SecurityType.CurrencyPair)
                {
                    secGroup = "spot/";
                }
                else
                {
                    if (security.NameClass.Contains("FUT_USDT-M"))
                    {
                        secGroup = "futures/um/";
                    }
                    else if (security.NameClass.Contains("FUT_COIN-M"))
                    {
                        secGroup = "futures/cm/";
                    }
                }


                // Find out how much data is on the server

                string prefix = "data/" + secGroup + "daily/klines/" + security.Name + "/" + needTf + "/";

                Tuple<DateTime, DateTime> period = FindDataPeriod(prefix);

                if (period != null && (period.Item1 != DateTime.MinValue || period.Item2 != DateTime.MinValue))
                {
                    string aboutDataMsg = string.Empty;

                    if (startTime < period.Item1)
                    {
                        startTime = period.Item1;

                        aboutDataMsg = $"The data on the {security.Name} starts from {period.Item1.ToShortDateString()} \n";
                    }

                    if (endTime > period.Item2)
                    {
                        endTime = period.Item2;

                        aboutDataMsg += $"The data on the {security.Name} ends on {period.Item2.ToShortDateString()}";
                    }

                    if (aboutDataMsg != string.Empty)
                    {
                        SendLogMessage(aboutDataMsg, LogMessageType.System);
                    }
                }
                else
                {
                    return null;
                }

                Tuple<List<string>, List<string>> timeRanges = SplitTimeRangeIntoMonthsAndDays(startTime, endTime); // item1 - Full months, item2 - Remaining days

                if (timeRanges == null)
                    return null;


                if (timeRanges.Item1.Count > 0)
                {
                    // download the monthly archive

                    for (int i = 0; i < timeRanges.Item1.Count; i++)
                    {
                        string path = $"/data/{secGroup}monthly/klines/{security.Name}/{needTf}/{security.Name}-{needTf}-{timeRanges.Item1[i]}.zip";

                        string zipArchivePath = DownloadZipArchive(path);

                        if (zipArchivePath != null)
                        {
                            string csvFilePath = GetSCVFileFromArchive(zipArchivePath);

                            candles.AddRange(ParseCsvFileToCandles(csvFilePath));
                        }
                        else
                        {
                            continue;
                        }
                    }
                }

                if (timeRanges.Item2.Count > 0)
                {
                    // download the dayly archive

                    for (int i = 0; i < timeRanges.Item2.Count; i++)
                    {
                        string path = $"/data/{secGroup}daily/klines/{security.Name}/{needTf}/{security.Name}-{needTf}-{timeRanges.Item2[i]}.zip";

                        string zipArchivePath = DownloadZipArchive(path);

                        if (zipArchivePath != null)
                        {
                            string csvFilePath = GetSCVFileFromArchive(zipArchivePath);

                            candles.AddRange(ParseCsvFileToCandles(csvFilePath));
                        }
                        else
                        {
                            continue;
                        }
                    }
                }

                candles.Sort(CompareCandlesByTimeStart);

                return candles;

            }
            catch (Exception error)
            {
                SendLogMessage($"Candles data downloading error: {error}", LogMessageType.Error);
                return null;
            }
        }

        private List<Candle> ParseCsvFileToCandles(string csvFilePath)
        {
            List<Candle> candles = new List<Candle>();

            string[] lines = File.ReadAllLines(csvFilePath);

            for (int i = 0; i < lines.Length; i++)
            {
                if (char.IsLetter(lines[i][0]))
                    continue;

                string[] candleParts = lines[i].Split(',', StringSplitOptions.RemoveEmptyEntries);

                Candle newCandle;

                newCandle = new Candle();

                newCandle.TimeStart = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(candleParts[0][..13])).DateTime;
                newCandle.Open = candleParts[1].ToDecimal();
                newCandle.High = candleParts[2].ToDecimal();
                newCandle.Low = candleParts[3].ToDecimal();
                newCandle.Close = candleParts[4].ToDecimal();
                newCandle.Volume = candleParts[5].ToDecimal();

                candles.Add(newCandle);
            }

            string[] files = Directory.GetFiles(_tempDirectory);

            if (files.Length > 0)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    File.Delete(files[i]);
                }
            }

            return candles;
        }

        private int CompareCandlesByTimeStart(Candle x, Candle y)
        {
            return x.TimeStart.CompareTo(y.TimeStart);
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

            try
            {
                string secGroup = "";

                if (security.SecurityType == SecurityType.CurrencyPair)
                {
                    secGroup = "spot/";
                }
                else
                {
                    if (security.NameClass.Contains("FUT_USDT-M"))
                    {
                        secGroup = "futures/um/";
                    }
                    else if (security.NameClass.Contains("FUT_COIN-M"))
                    {
                        secGroup = "futures/cm/";
                    }
                }


                // Find out how much data is on the server

                string prefix = "data/" + secGroup + "daily/aggTrades/" + security.Name + "/";

                Tuple<DateTime, DateTime> period = FindDataPeriod(prefix);

                if (period != null && (period.Item1 != DateTime.MinValue || period.Item2 != DateTime.MinValue))
                {
                    string aboutDataMsg = string.Empty;

                    if (startTime < period.Item1)
                    {
                        startTime = period.Item1;

                        aboutDataMsg = $"The data on the {security.Name} starts from {period.Item1.ToShortDateString()} \n";
                    }

                    if (endTime > period.Item2)
                    {
                        endTime = period.Item2;

                        aboutDataMsg += $"The data on the {security.Name} ends on {period.Item2.ToShortDateString()}";
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

                Tuple<List<string>, List<string>> timeRanges = SplitTimeRangeIntoMonthsAndDays(startTime, endTime); // item1 - Full months, item2 - Remaining days

                if (timeRanges == null)
                    return null;

                if (timeRanges.Item1.Count > 0)
                {
                    // download the monthly archive

                    for (int i = 0; i < timeRanges.Item1.Count; i++)
                    {
                        string path = $"/data/{secGroup}monthly/aggTrades/{security.Name}/{security.Name}-aggTrades-{timeRanges.Item1[i]}.zip";

                        string zipArchivePath = DownloadZipArchive(path);

                        if (zipArchivePath != null)
                        {
                            string csvFilePath = GetSCVFileFromArchive(zipArchivePath);

                            trades.AddRange(ParseCsvFileToTrades(csvFilePath, security.Name));
                        }
                        else
                        {
                            continue;
                        }
                    }
                }

                if (timeRanges.Item2.Count > 0)
                {
                    // download the dayly archive

                    for (int i = 0; i < timeRanges.Item2.Count; i++)
                    {
                        string path = $"/data/{secGroup}daily/aggTrades/{security.Name}/{security.Name}-aggTrades-{timeRanges.Item2[i]}.zip";

                        string zipArchivePath = DownloadZipArchive(path);

                        if (zipArchivePath != null)
                        {
                            string csvFilePath = GetSCVFileFromArchive(zipArchivePath);

                            trades.AddRange(ParseCsvFileToTrades(csvFilePath, security.Name));
                        }
                        else
                        {
                            continue;
                        }
                    }
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

                // SPOT - 60968,0.00001060,1557.50000000,63903,63903,1752368178975926,True,True 

                // futures USDT-M/COIN-M - agg_trade_id,price,quantity,first_trade_id,last_trade_id,transact_time,is_buyer_maker 
                // 513613220,0.6224,2714.0,1603729958,1603729969,1752105604847,false

                trade.Time = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(tradeParts[5][..13])).DateTime;
                trade.Price = tradeParts[1].ToDecimal();
                trade.MicroSeconds = 0;
                trade.Id = tradeParts[0];
                trade.Volume = Math.Abs(tradeParts[2].ToDecimal());
                trade.SecurityNameCode = secName;

                if (tradeParts[6].ToLower() == "true")
                {
                    trade.Side = Side.Buy;
                    trade.Ask = 0;
                    trade.AsksVolume = 0;
                    trade.Bid = trade.Price;
                    trade.BidsVolume = trade.Volume;
                }
                else
                {
                    trade.Side = Side.Sell;
                    trade.Ask = trade.Price;
                    trade.AsksVolume = trade.Volume;
                    trade.Bid = 0;
                    trade.BidsVolume = 0;
                }

                trades.Add(trade);
            }

            string[] files = Directory.GetFiles(_tempDirectory);

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

        private string GetSCVFileFromArchive(string tempZipPath)
        {
            string extractPath = _tempDirectory;

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

        private Tuple<DateTime, DateTime> FindDataPeriod(string prefix)
        {
            DateTime start = DateTime.MinValue;
            DateTime end = DateTime.MinValue;

            string baseS3Url = "https://s3-ap-northeast-1.amazonaws.com";

            string s3Url = $"/data.binance.vision?delimiter=/&prefix={prefix}";

            bool isArchiveTruncated = true;
            ListBucketResult listBucket = null;
            Stream response = null;
            XmlSerializer serializer = null;

            try
            {

                response = GetStreamS3(baseS3Url, s3Url);

                serializer = new XmlSerializer(typeof(ListBucketResult));

                listBucket = (ListBucketResult)serializer.Deserialize(response);

                if (listBucket != null)
                {
                    if (listBucket.Contents != null && listBucket.Contents.Count > 0)
                    {
                        start = ExtractDateFromString(listBucket.Contents[0].Key);
                        end = ExtractDateFromString(listBucket.Contents[^1].Key);
                    }

                    isArchiveTruncated = listBucket.IsTruncated;

                    while (isArchiveTruncated)
                    {
                        string markerUrl = listBucket.NextMarker.Replace("/", "%2F");

                        string additionalListUrl = s3Url + "&marker=" + markerUrl;

                        response = GetStreamS3(baseS3Url, additionalListUrl);

                        serializer = new XmlSerializer(typeof(ListBucketResult));

                        listBucket = (ListBucketResult)serializer.Deserialize(response);

                        if (listBucket != null)
                        {
                            if (listBucket.IsTruncated == true)
                            {
                                isArchiveTruncated = listBucket.IsTruncated;
                                continue;
                            }
                            else if (!listBucket.IsTruncated && listBucket.Contents != null && listBucket.Contents.Count > 0)
                            {
                                end = ExtractDateFromString(listBucket.Contents[^1].Key);
                                isArchiveTruncated = listBucket.IsTruncated;
                            }
                            else
                            {
                                SendLogMessage($"Error in getting the data period: list bucket is null", LogMessageType.Error);
                                return null;
                            }
                        }
                        else
                        {
                            SendLogMessage($"Error in getting the data period: list bucket is null", LogMessageType.Error);
                            return null;
                        }
                    }

                    return Tuple.Create(start, end);
                }
                else
                {
                    SendLogMessage($"Error in getting the data period: list bucket is null", LogMessageType.Error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error in getting the data period: {ex.Message}", LogMessageType.Error);
                return null;
            }
        }

        private DateTime ExtractDateFromString(string input)
        {
            Match match = Regex.Match(input, @"(\d{4})-(\d{2})-(\d{2})(?=\.zip)|(\d{4})-(\d{2})(?=\.zip)");

            if (!match.Success)
            {
                SendLogMessage("The string does not contain the date in the YYYY-MM or YYYY-MM-DD format before the .zip", LogMessageType.Error);
                return DateTime.MinValue;
            }

            if (!string.IsNullOrEmpty(match.Groups[3].Value))
            {
                int year = int.Parse(match.Groups[1].Value);
                int month = int.Parse(match.Groups[2].Value);
                int day = int.Parse(match.Groups[3].Value);
                return new DateTime(year, month, day);
            }
            else
            {
                int year = int.Parse(match.Groups[4].Value);
                int month = int.Parse(match.Groups[5].Value);
                return new DateTime(year, month, 1);
            }
        }

        private Tuple<List<string>, List<string>> SplitTimeRangeIntoMonthsAndDays(DateTime startTime, DateTime endTime)
        {
            List<string> fullMonths = new List<string>();
            List<string> remainingDays = new List<string>();

            if (startTime >= endTime)
            {
                return null;
            }

            // Find full month
            DateTime currentMonthStart = new DateTime(startTime.Year, startTime.Month, 1);
            if (startTime.Day > 1)
            {
                currentMonthStart = currentMonthStart.AddMonths(1);
            }

            while (currentMonthStart <= endTime)
            {
                DateTime currentMonthEnd = new DateTime(
                    currentMonthStart.Year,
                    currentMonthStart.Month,
                    DateTime.DaysInMonth(currentMonthStart.Year, currentMonthStart.Month));

                if (currentMonthEnd <= endTime)
                {
                    fullMonths.Add(currentMonthStart.ToString("yyyy-MM"));
                    currentMonthStart = currentMonthStart.AddMonths(1);
                }
                else
                {
                    break;
                }
            }

            // Processing of days before first full month 
            DateTime tempDate = startTime;

            DateTime firstFullMonthStart = fullMonths.Count > 0
                ? DateTime.ParseExact(fullMonths[0] + "-01", "yyyy-MM-dd", null)
                : endTime.AddDays(1);

            while (tempDate < firstFullMonthStart && tempDate <= endTime)
            {
                remainingDays.Add(tempDate.ToString("yyyy-MM-dd"));

                tempDate = tempDate.AddDays(1);
            }

            //  Processing of days after last full month
            if (fullMonths.Count > 0)
            {
                DateTime lastFullMonthEnd = DateTime.ParseExact(
                    fullMonths[fullMonths.Count - 1] + "-" +
                    DateTime.DaysInMonth(
                        int.Parse(fullMonths[fullMonths.Count - 1].Split('-')[0]),
                        int.Parse(fullMonths[fullMonths.Count - 1].Split('-')[1])),
                    "yyyy-MM-dd", null);

                if (lastFullMonthEnd < endTime)
                {
                    DateTime dayToAdd = lastFullMonthEnd.AddDays(1);

                    while (dayToAdd <= endTime)
                    {
                        remainingDays.Add(dayToAdd.ToString("yyyy-MM-dd"));

                        dayToAdd = dayToAdd.AddDays(1);
                    }
                }
            }

            return Tuple.Create(fullMonths, remainingDays);
        }

        #endregion

        #region 6 Queries

        private RateGate _rateGateData = new RateGate(1, TimeSpan.FromMilliseconds(100));

        private string GetStringRequest(string baseUrl, string url)
        {
            _rateGateData.WaitToProceed();

            try
            {
                RestRequest requestRest = new(url, Method.GET);
                RestClient client = new(baseUrl);

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return response.Content;
                }
                else
                {
                    SendLogMessage($"Error response: {response.StatusCode}-{response.Content}", LogMessageType.Error);
                    return null;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);

                return null;
            }
        }

        private string DownloadZipArchive(string path)
        {
            try
            {
                string tempZipPath = _tempDirectory + Path.GetRandomFileName();

                RestRequest request = new RestRequest(path, Method.GET);

                RestClient client = new RestClient("https://data.binance.vision");

                request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36");

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK && response.RawBytes != null && response.RawBytes.Length > 0)
                {
                    using (FileStream fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        fileStream.Write(response.RawBytes, 0, response.RawBytes.Length);
                    }
                }
                else
                {
                    SendLogMessage($"Сouldn't upload zip archive\n. Http status: {response.StatusCode} - {response.ErrorMessage}", LogMessageType.Error);
                    return null;
                }

                return tempZipPath;
            }
            catch (Exception ex)
            {
                SendLogMessage("Сouldn't upload zip archive.\n" + ex, LogMessageType.Error);
                return null;
            }
        }

        private Stream GetStreamS3(string baseS3Url, string url)
        {
            try
            {
                RestClient client = new RestClient(baseS3Url);
                RestRequest request = new RestRequest(url);

                request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36");

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK && response.RawBytes != null && response.RawBytes.Length > 0)
                {
                    return new MemoryStream(response.RawBytes);
                }
                else
                {
                    SendLogMessage($"Сouldn't get stream s3\n. Http status: {response.StatusCode} - {response.ErrorMessage}", LogMessageType.Error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Сouldn't get stream s3.\n" + ex, LogMessageType.Error);
                return null;
            }
        }

        #endregion

        #region 7 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;
        #endregion

        #region 8 Unused methods

        public void Subscribe(Security security) { }

        public void SendOrder(Order order) { }

        public void CancelAllOrders() { }

        public void CancelAllOrdersToSecurity(Security security) { }

        public bool CancelOrder(Order order) { return false; }

        public void ChangeOrderPrice(Order order, decimal newPrice) { }

        public void GetAllActivOrders() { }

        public OrderStateType GetOrderStatus(Order order) { return OrderStateType.None; }

        public bool SubscribeNews() { return false; }

        public List<Order> GetActiveOrders(int startIndex, int count) { return null; }

        public List<Order> GetHistoricalOrders(int startIndex, int count) { return null; }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount) { return null; }

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
