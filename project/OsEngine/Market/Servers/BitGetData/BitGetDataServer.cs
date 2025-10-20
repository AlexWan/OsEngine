using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.BitGetData.Entity;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Xml;


namespace OsEngine.Market.Servers.BitGetData
{
    public class BitGetDataServer : AServer
    {
        public BitGetDataServer()
        {
            BitGetDataServerRealization realization = new BitGetDataServerRealization();
            ServerRealization = realization;
            NeedToHideParameters = true;
        }
    }

    public class BitGetDataServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public BitGetDataServerRealization()
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
                string testConnect = DownloadZipArchive("/online/kline/ETHUSDT/SP/20251003.zip");

                if (testConnect != null)
                {
                    ServerStatus = ServerConnectStatus.Connect;
                    ConnectEvent();
                }
                else
                {
                    SendLogMessage($"Connect server error", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Connect server error\n{ex.Message}", LogMessageType.Error);
            }
        }

        public void Dispose()
        {
            _notExistingArchives.Clear();

            CleanTempFiles();

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void CleanTempFiles()
        {
            try
            {
                string[] files = Directory.GetFiles(_tempDirectory);

                if (files.Length > 0)
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        File.Delete(files[i]);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error cleaning temp files: {ex.Message}", LogMessageType.Error);
            }
        }

        public ServerType ServerType
        {
            get { return ServerType.BitGetData; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;
        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public DateTime ServerTime { get; set; }

        public List<IServerParameter> ServerParameters { get; set; }

        private string _tempDirectory = @"Data\Temp\BitGetDataTempFiles\";

        private string _restUrl = "https://api.bitget.com";

        private string _archivesBaseUrl = "https://img.bitgetimg.com";

        private TimeSpan _lastTradeTime = new TimeSpan(15, 55, 00);

        private DateTime _dateDifferentUrlFormat = new DateTime(2024, 04, 19);

        private HashSet<string> _notExistingArchives = new HashSet<string>();

        private List<string> _listCoin = new List<string>() { "USDT-FUTURES", "COIN-FUTURES", "USDC-FUTURES" };

        #endregion

        #region 3 Securities

        List<Security> _securities = new List<Security>();

        public void GetSecurities()
        {
            SendLogMessage("Downloading the list of securities...", LogMessageType.System);

            try
            {
                GetSpotSecurities();

                GetFuturesSecurities();

            }
            catch (Exception ex)
            {

                SendLogMessage($"Securities downloading error. Error: {ex.Message}", LogMessageType.Error);
            }

            if (_securities.Count > 0)
            {
                SendLogMessage($"{_securities.Count} securities loaded", LogMessageType.System);
            }

            SecurityEvent?.Invoke(_securities);
        }

        private void GetSpotSecurities()
        {
            string response = GetStringRequest("/api/v2/spot/public/symbols");

            if (response != null)
            {
                BitGetDataSecurityResp<List<BitGetDataSymbol>> symbols = JsonConvert.DeserializeAnonymousType(response, new BitGetDataSecurityResp<List<BitGetDataSymbol>>());

                if (symbols.code == "00000")
                {
                    for (int i = 0; i < symbols.data.Count; i++)
                    {
                        BitGetDataSymbol item = symbols.data[i];

                        if (item.status.Equals("online"))
                        {
                            Security newSecurity = new Security();

                            newSecurity.Exchange = "BitGet";
                            newSecurity.Lot = 1;
                            newSecurity.Name = item.symbol;
                            newSecurity.NameFull = item.symbol;
                            newSecurity.NameClass = "Spot_" + item.quoteCoin;
                            newSecurity.NameId = item.symbol;
                            newSecurity.SecurityType = SecurityType.CurrencyPair;
                            newSecurity.PriceStep = 0;
                            newSecurity.PriceStepCost = 0;
                            newSecurity.Lot = 1;

                            _securities.Add(newSecurity);
                        }
                    }
                }
                else
                {
                    SendLogMessage($"Securities error. {symbols.code} || msg: {symbols.msg}", LogMessageType.Error);
                }
            }
            else
            {
                SendLogMessage($"Securities SPOT downloading error", LogMessageType.Error);
            }
        }

        private void GetFuturesSecurities()
        {
            for (int indCoin = 0; indCoin < _listCoin.Count; indCoin++)
            {

                string response = GetStringRequest($"/api/v2/mix/market/contracts?productType={_listCoin[indCoin]}");

                if (response != null)
                {
                    BitGetDataSecurityResp<List<BitGetDataSymbol>> symbols = JsonConvert.DeserializeAnonymousType(response, new BitGetDataSecurityResp<List<BitGetDataSymbol>>());

                    List<Security> securities = new List<Security>();

                    if (symbols.data.Count == 0)
                    {
                        continue;
                    }

                    for (int i = 0; i < symbols.data.Count; i++)
                    {
                        BitGetDataSymbol item = symbols.data[i];

                        if (item.symbolStatus.Equals("normal"))
                        {
                            Security newSecurity = new Security();

                            newSecurity.Exchange = "BitGet";
                            newSecurity.Lot = 1;
                            newSecurity.Name = item.symbol;
                            newSecurity.NameFull = item.symbol;
                            newSecurity.NameClass = _listCoin[indCoin];
                            newSecurity.NameId = item.symbol;
                            newSecurity.SecurityType = SecurityType.Futures;
                            newSecurity.State = SecurityStateType.Activ;
                            newSecurity.PriceStep = 0;
                            newSecurity.PriceStepCost = 0;
                            newSecurity.Lot = 1;

                            _securities.Add(newSecurity);
                        }
                    }
                }
                else
                {
                    SendLogMessage($"Securities futures downloading error", LogMessageType.Error);
                }
            }
        }

        public event Action<List<Security>> SecurityEvent;
        #endregion

        #region 4 Portfolios

        private List<Portfolio> _myPortfolios = new List<Portfolio>();

        public void GetPortfolios()
        {
            Portfolio newPortfolio = new Portfolio();
            newPortfolio.Number = "BitGetData Virtual Portfolio";
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

                int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

                if (tfTotalMinutes != 1)
                {
                    return null;
                }

                if (actualTime > endTime)
                {
                    return null;
                }

                endTime = endTime.Date.AddDays(1); // данные одного дня находятся в двух архивах

                DateTime lastArchiveDate = DateTime.UtcNow.Date.AddDays(-3);

                if (startTime.Date > lastArchiveDate)
                {
                    SendLogMessage($"The data candles is available until {lastArchiveDate}", LogMessageType.Error);
                    return null;
                }

                if (endTime.Date > lastArchiveDate.AddDays(1))
                {
                    SendLogMessage($"The data candles is available until {lastArchiveDate}", LogMessageType.System);

                    endTime = lastArchiveDate.AddDays(1);
                }

                List<Candle> candles = new List<Candle>();

                List<DateTime> dates = GetDateRangeInclusive(startTime, endTime);

                if (dates.Count == 0)
                    return null;

                //  https://img.bitgetimg.com/online/kline/ETHUSDT/SP/20251003.zip - spot
                //  https://img.bitgetimg.com/online/kline/BTCUSDT/UMCBL/20250709.zip - fut
                // https://img.bitgetimg.com/online/kline/ETHUSDT/ETHUSDT_UMCBL_1min_20231225.zip - до 19.04.24
                // https://img.bitgetimg.com/online/kline/BTCUSDT/BTCUSDT_SP_1min_20231214.zip

                for (int i = 0; i < dates.Count; i++)
                {
                    string dayStr = dates[i].ToString("yyyyMMdd");

                    string prefix = "";

                    if (security.SecurityType == SecurityType.CurrencyPair)
                    {
                        if (dates[i] < _dateDifferentUrlFormat)
                            prefix = $"/online/kline/{security.Name}/{security.Name}_1min_{dayStr}.zip";
                        else
                            prefix = $"/online/kline/{security.Name}/SP/{dayStr}.zip";
                    }
                    else
                    {
                        if (dates[i] < _dateDifferentUrlFormat)
                            prefix = $"/online/kline/{security.Name}/{security.Name}_UMCBL_1min_{dayStr}.zip";
                        else
                            prefix = $"/online/kline/{security.Name}/UMCBL/{dayStr}.zip";
                    }

                    if (_notExistingArchives.Contains(prefix)) // данного архива нет на сервере
                        continue;

                    string zipArchivePath = DownloadZipArchive(prefix);

                    if (zipArchivePath != null)
                    {
                        string xlsxFilePath = GetXlsxFileFromArchive(zipArchivePath);

                        List<Candle> dayCandles = ParseXlsxFileToCandles(xlsxFilePath);

                        candles.AddRange(dayCandles);
                    }
                    else
                    {
                        continue;
                    }
                }

                if (candles.Count > 0)
                {
                    return candles.FindAll(p => p.TimeStart >= startTime && p.TimeStart < endTime);
                }

                return null;

            }
            catch (Exception error)
            {
                SendLogMessage($"Candles data downloading error: {error}", LogMessageType.Error);
                return null;
            }
        }

        private string GetXlsxFileFromArchive(string tempZipPath)
        {
            string extractPath = _tempDirectory;

            SafeExtractZip(tempZipPath, extractPath);

            // search first XLSX file in archive
            string[] xlsxFiles = Directory.GetFiles(extractPath, "*.xlsx");

            if (xlsxFiles.Length == 0)
            {
                SendLogMessage("The XLSX file was not found in the archive", LogMessageType.Error);
                return null;
            }
            else
            {
                string xlsxFilePath = xlsxFiles[0];
                return xlsxFilePath;
            }
        }

        private List<Candle> ParseXlsxFileToCandles(string xlsxFilePath)
        {
            List<Candle> candles = new List<Candle>();

            string tempExtractPath = Path.Combine(_tempDirectory, "xlsx_extract");

            try
            {
                ZipFile.ExtractToDirectory(xlsxFilePath, tempExtractPath, true);

                ReadWorksheetData(tempExtractPath, candles);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error parsing XLSX: {ex.Message}", LogMessageType.Error);
            }
            finally
            {
                if (Directory.Exists(tempExtractPath))
                {
                    Directory.Delete(tempExtractPath, true);
                }
            }

            CleanTempFiles();

            return candles;
        }

        private void ReadWorksheetData(string extractPath, List<Candle> candles)
        {
            string worksheetPath = Path.Combine(extractPath, "xl", "worksheets", "sheet1.xml");

            if (!File.Exists(worksheetPath))
            {
                // Пробуем найти любой sheet
                string[] sheetFiles = Directory.GetFiles(Path.Combine(extractPath, "xl", "worksheets"), "sheet*.xml");

                if (sheetFiles.Length == 0)
                {
                    SendLogMessage("No worksheet found in XLSX file", LogMessageType.Error);
                }

                worksheetPath = sheetFiles[0];
            }

            XmlDocument doc = new XmlDocument();

            doc.Load(worksheetPath);

            XmlNamespaceManager nsManager = new XmlNamespaceManager(doc.NameTable);

            nsManager.AddNamespace("x", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");

            XmlNodeList rowNodes = doc.SelectNodes("//x:row", nsManager);

            if (rowNodes == null)
                return;

            bool isFirstRow = true;

            for (int i = 0; i < rowNodes.Count; i++)
            {
                if (isFirstRow)
                {
                    isFirstRow = false;
                    continue;
                }

                XmlNodeList cellNodes = rowNodes[i].SelectNodes("x:c", nsManager);

                if (cellNodes == null || cellNodes.Count < 6)
                    continue;

                // timestamp	open	  high	   low	      close	      basevolume	usdtvolume
                // 1751990400  108220.2    108250  108214.3    108224.3    34.77177809 3763285.841431255

                Candle candle = new Candle();
                candle.State = CandleState.Finished;

                // timestamp (column A)
                string timestampValue = cellNodes[0].InnerText;

                if (!string.IsNullOrEmpty(timestampValue) && int.TryParse(timestampValue, out int timestamp))
                {
                    candle.TimeStart = TimeManager.GetDateTimeFromTimeStampSeconds(timestamp);
                }

                // open (column B)
                string openValue = cellNodes[1].InnerText;

                if (!string.IsNullOrEmpty(openValue))
                {
                    candle.Open = openValue.ToDecimal();
                }

                // high (column C)
                string highValue = cellNodes[2].InnerText;

                if (!string.IsNullOrEmpty(highValue))
                {
                    candle.High = highValue.ToDecimal();
                }

                // low (column D)
                string lowValue = cellNodes[3].InnerText;

                if (!string.IsNullOrEmpty(lowValue))
                {
                    candle.Low = lowValue.ToDecimal();
                }

                // close (column E)
                string closeValue = cellNodes[4].InnerText;

                if (!string.IsNullOrEmpty(closeValue))
                {
                    candle.Close = closeValue.ToDecimal();
                }

                // volume (column F)
                string volumeValue = cellNodes[5].InnerText;

                if (!string.IsNullOrEmpty(volumeValue))
                {
                    candle.Volume = volumeValue.ToDecimal();
                }

                candles.Add(candle);
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

            List<Trade> allTrades = new List<Trade>();

            try
            {
                List<DateTime> dates = GetDateRangeInclusive(startTime, endTime);

                if (dates.Count == 0)
                    return null;

                // https://img.bitgetimg.com/online/trades/UMCBL/XRPUSDT/XRPUSDT_UMCBL_20240418_001.zip - до 19.04.24
                // https://img.bitgetimg.com/online/trades/UMCBL/DEGENUSDT/20251002_001.zip - фьючи
                // https://img.bitgetimg.com/online/trades/SPBL/AEROUSDT/20250205_001.zip - спот

                for (int i = 0; i < dates.Count; i++)
                {
                    bool isLastArchiveByCurrDay = false;

                    int dailyArchiveCount = 1;

                    string dayStr = dates[i].ToString("yyyyMMdd");

                    while (!isLastArchiveByCurrDay)
                    {
                        string prefix = "";

                        if (security.SecurityType == SecurityType.CurrencyPair)
                        {
                            if (dates[i] < _dateDifferentUrlFormat)
                                prefix = $"/online/trades/SPBL/{security.Name}/{security.Name}_SPBL_{dayStr}_{dailyArchiveCount:D3}.zip";
                            else
                                prefix = $"/online/trades/SPBL/{security.Name}/{dayStr}_{dailyArchiveCount:D3}.zip";
                        }
                        else
                        {
                            if (dates[i] < _dateDifferentUrlFormat)
                                prefix = $"/online/trades/UMCBL/{security.Name}/{security.Name}_UMCBL_{dayStr}_{dailyArchiveCount:D3}.zip";
                            else
                                prefix = $"/online/trades/UMCBL/{security.Name}/{dayStr}_{dailyArchiveCount:D3}.zip";
                        }

                        if (_notExistingArchives.Contains(prefix)) // данного архива нет на сервере
                            return null;

                        string zipArchivePath = DownloadZipArchive(prefix);

                        if (zipArchivePath != null)
                        {
                            string csvFilePath = GetSCVFileFromArchive(zipArchivePath);

                            List<Trade> trades = ParseCsvFileToTrades(csvFilePath, security.Name);


                            if (trades[^1].Time.Date < dates[i].Date || trades[^1].Time.TimeOfDay < _lastTradeTime)
                            {
                                // возможно за эту дату есть ещё архив

                                allTrades.AddRange(trades);

                                dailyArchiveCount++;

                            }
                            else // последний тик в 16:59
                            {
                                allTrades.AddRange(trades);

                                isLastArchiveByCurrDay = true;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (allTrades.Count > 0)
                {
                    allTrades.Sort(CompareTradesByTime);

                    return allTrades.FindAll(p => p.Time >= startTime && p.Time < endTime);
                }

                return null;
            }
            catch (Exception error)
            {
                SendLogMessage($"Trades data downloading error: {error}", LogMessageType.Error);

                CleanTempFiles();

                return null;
            }
        }

        private List<DateTime> GetDateRangeInclusive(DateTime startTime, DateTime endTime)
        {
            List<DateTime> dates = new List<DateTime>();

            // Добавляем даты в диапазон включительно
            DateTime currentDate = startTime.Date;
            DateTime endDate = endTime.Date;

            while (currentDate <= endDate)
            {
                dates.Add(currentDate);
                currentDate = currentDate.AddDays(1);
            }

            return dates;
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

                // SPOT - trade_id,    timestamp,    price, side,volume(quote),size(base) 
                // 1125384109708226660,1704038401000,73.942,sell,17.339399,    0.2345

                // futures - trade_id, timestamp,    price,   side,volume(quote),size(base)
                // 1356949983127531576,1759248007000,0.002728,buy, 74.28344,     27230

                trade.SecurityNameCode = secName;
                trade.Id = tradeParts[0];
                trade.Time = TimeManager.GetDateTimeFromTimeStamp(long.Parse(tradeParts[1]));
                trade.Price = tradeParts[2].ToDecimal();
                trade.Side = tradeParts[3] == "sell" ? Side.Sell : Side.Buy;
                trade.Volume = Math.Abs(tradeParts[5].ToDecimal());

                trades.Add(trade);
            }

            CleanTempFiles();

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

        #endregion

        #region 6 Queries

        private RateGate _rateGateData = new RateGate(2, TimeSpan.FromMilliseconds(100));

        private string GetStringRequest(string url)
        {
            _rateGateData.WaitToProceed();

            try
            {
                RestRequest requestRest = new(url, Method.GET);
                RestClient client = new(_restUrl);

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
                string tempGzipPath = _tempDirectory + Path.GetRandomFileName();

                RestRequest request = new RestRequest(path, Method.GET);

                RestClient client = new RestClient(_archivesBaseUrl);

                request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36");
                request.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                request.AddHeader("Accept-Encoding", "gzip, deflate, br, zstd");
                request.AddHeader("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
                request.AddHeader("Priority", "u=0, i");
                request.AddHeader("Upgrade-Insecure-Requests", "1");
                request.AddHeader("Referer", "https://www.bitget.com/ru/data-download");
                request.AddHeader("Sec-Ch-Ua", "\"Google Chrome\";v=\"141\", \"Not?A_Brand\";v=\"8\", \"Chromium\";v=\"141\"");
                request.AddHeader("Sec-Ch-Ua-Mobile", "?0");
                request.AddHeader("Sec-Ch-Ua-Platform", "Windows");
                request.AddHeader("Sec-Fetch-Dest", "iframe");
                request.AddHeader("Sec-Fetch-Mode", "navigate");
                request.AddHeader("Sec-Fetch-Site", "cross-site");
                request.AddHeader("Sec-Fetch-Storage-Access", "active");
                request.AddHeader("Sec-Fetch-User", "?1");
                request.AddHeader("Upgrade-insecure-requests", "1");

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK && response.RawBytes != null && response.RawBytes.Length > 0)
                {
                    using (FileStream fileStream = new FileStream(tempGzipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        fileStream.Write(response.RawBytes, 0, response.RawBytes.Length);
                    }
                }
                else if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.NotFound)
                {
                    _notExistingArchives.Add(path);

                    SendLogMessage($"Daily archive {path.Split('/')[3]} {path.Split('/')[4]} {path.Split('/')[5].Split(".")[0]} not found.", LogMessageType.System);
                    return null;
                }
                else
                {
                    SendLogMessage($"Сouldn't upload zip archive {path.Split('/')[3]} {path.Split('/')[4]} {path.Split('/')[5].Split(".")[0]}\n. Http status: {response.StatusCode} - {response.ErrorMessage}", LogMessageType.Error);
                    return null;
                }

                return tempGzipPath;
            }
            catch (Exception ex)
            {
                SendLogMessage("Сouldn't upload zip archive.\n" + ex, LogMessageType.Error);
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

        public event Action<News> NewsEvent;
        public event Action<MarketDepth> MarketDepthEvent;
        public event Action<Trade> NewTradesEvent;
        public event Action<Order> MyOrderEvent;
        public event Action<MyTrade> MyTradeEvent;
        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;
        public event Action<Funding> FundingUpdateEvent;
        public event Action<SecurityVolumes> Volume24hUpdateEvent;

        #endregion
    }
}
