using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.GateIoData.Entity;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace OsEngine.Market.Servers.GateIoData
{
    internal class GateIoDataServer : AServer
    {
        public GateIoDataServer()
        {
            GateIoDataServerRealization realization = new GateIoDataServerRealization();
            ServerRealization = realization;
            NeedToHideParameters = true;
        }
    }

    public class GateIoDataServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public GateIoDataServerRealization()
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
                RestRequest requestRest = new(Method.GET);
                RestClient client = new("https://www.gate.com/ru/developer/historical_quotes");

                IRestResponse response = client.Execute(requestRest);

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
            _notExistingArchives.Clear();

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
            get { return ServerType.GateIoData; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region 2 Properties

        public DateTime ServerTime { get; set; }

        public List<IServerParameter> ServerParameters { get; set; }

        private string _httpUrlApi = "https://api.gateio.ws/api/v4";

        HashSet<string> _notExistingArchives = new HashSet<string>();

        List<string> _dailyArchivesPaths = new List<string>();

        private string _tempDirectory = @"Data\Temp\GateIoDataTempFiles\";

        private string _currSecurity = string.Empty;

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
            string response = GetStringRequest("/spot/currency_pairs");

            if (response != null)
            {
                List<GateSecurityResponse> currencyPairs = JsonConvert.DeserializeAnonymousType<List<GateSecurityResponse>>(response, new List<GateSecurityResponse>());

                if (currencyPairs != null && currencyPairs.Count > 0)
                {
                    for (int i = 0; i < currencyPairs.Count; i++)
                    {
                        GateSecurityResponse current = currencyPairs[i];

                        if (current.trade_status != "tradable")
                        {
                            continue;
                        }

                        Security security = new Security();

                        security.Exchange = "Gate.io";
                        security.State = SecurityStateType.Activ;
                        security.Name = current.id;
                        security.NameFull = current.id;
                        security.NameClass = "Spot_" + current.quote;
                        security.NameId = current.id;
                        security.SecurityType = SecurityType.CurrencyPair;
                        security.PriceStep = 0;
                        security.PriceStepCost = 0;
                        security.Lot = 1;

                        _securities.Add(security);
                    }
                }
            }
            else
            {
                SendLogMessage($"Securities SPOT downloading error", LogMessageType.Error);
            }
        }

        private void GetFuturesSecurities()
        {
            string[] settleCurrency = { "usdt", "btc" };

            for (int k = 0; k < settleCurrency.Length; k++)
            {
                string response = GetStringRequest($"/futures/{settleCurrency[k]}/contracts");

                if (response != null)
                {
                    List<GateFutSecurityInfo> currencyPairs = JsonConvert.DeserializeAnonymousType<List<GateFutSecurityInfo>>(response, new List<GateFutSecurityInfo>());

                    if (currencyPairs != null && currencyPairs.Count > 0)
                    {
                        for (int i = 0; i < currencyPairs.Count; i++)
                        {
                            GateFutSecurityInfo current = currencyPairs[i];

                            if (current.in_delisting == "true")
                            {
                                continue;
                            }

                            string name = current.name.ToUpper();

                            Security security = new Security();

                            security.Exchange = "Gate.io";
                            security.State = SecurityStateType.Activ;
                            security.Name = name;
                            security.NameFull = name;
                            security.NameClass = "Futures_" + settleCurrency[k].ToUpper();
                            security.NameId = name;
                            security.SecurityType = SecurityType.Futures;
                            security.PriceStep = 0;
                            security.PriceStepCost = 0;
                            security.Lot = 1;

                            _securities.Add(security);
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
            newPortfolio.Number = "GateIoData Virtual Portfolio";
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

            if (!CheckTime(startTime, endTime, actualTime))
            {
                return null;
            }

            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

            if (!CheckTf(tfTotalMinutes))
            {
                return null;
            }

            NewSecurityCheck(security.Name);

            int timeRange = tfTotalMinutes * 10000;

            DateTime maxStartTime = DateTime.UtcNow.AddMinutes(-timeRange);

            string secGroup = "";

            if (security.SecurityType == SecurityType.CurrencyPair)
            {
                secGroup = "spot";
            }
            else
            {
                secGroup = "futures_" + security.NameClass.Split('_')[1].ToLower();
            }

            DateTime currMonthStart = DateTime.Now.Date.AddDays(1 - DateTime.Now.Day);

            List<Candle> allCandles = new List<Candle>();


            if (startTime > currMonthStart)   // запрашиваем свечи из текущего месяца только REST
            {
                if (maxStartTime > startTime)
                {
                    SendLogMessage("Maximum interval is 10,000 candles from today!", LogMessageType.Error);

                    startTime = maxStartTime;
                }

                SendLogMessage($"Atention! The loading of candles for the current month can be very long. " +
                    $"For fast loading, set the end time to {currMonthStart.ToShortDateString()}.", LogMessageType.Error);

                allCandles = GetCandlesCurrMonth(security, timeFrameBuilder.TimeFrameTimeSpan, startTime, endTime);

                if (allCandles != null && allCandles.Count > 0)
                {
                    return allCandles;
                }
            }
            else if (startTime < currMonthStart && endTime > currMonthStart)
            {
                if (maxStartTime > startTime)
                {
                    SendLogMessage("Maximum interval is 10,000 candles from today!", LogMessageType.Error);

                    startTime = maxStartTime;

                }

                SendLogMessage($"Atention! The loading of candles for the current month can be very long.\n" +
                   $"For fast loading, set end time to {currMonthStart.ToShortDateString()}.", LogMessageType.Error);

                allCandles = GetCandlesCurrMonth(security, timeFrameBuilder.TimeFrameTimeSpan, startTime, endTime);

                if (allCandles != null && allCandles.Count > 0)
                {
                    return allCandles;
                }
            }
            else if (startTime < currMonthStart && endTime < currMonthStart) // закачиваем месячный архив
            {
                string startArchiveDate = startTime.ToString("yyyyMM");

                string endArchiveDate = endTime.ToString("yyyyMM");

                if (startTime.Year < 2022)
                {
                    startTime = new DateTime(2022, 01, 01);
                    startArchiveDate = "202201";
                }

                string interval = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);

                if (startArchiveDate != endArchiveDate)
                {
                    List<string> archiveDates = GetMonthsList(startTime, endTime);

                    if (archiveDates.Count > 0)
                    {
                        for (int i = 0; i < archiveDates.Count; i++)
                        {
                            List<Candle> candles = GetCandlesByMonth(security, archiveDates[i], secGroup, interval);

                            if (candles != null && candles.Count > 0)
                            {
                                allCandles.AddRange(candles);
                            }
                        }
                    }
                }
                else if (startArchiveDate == endArchiveDate)
                {
                    // нужен архив одного месяца
                    List<Candle> candles = GetCandlesByMonth(security, startArchiveDate, secGroup, interval);

                    if (candles != null && candles.Count > 0)
                    {
                        allCandles.AddRange(candles);
                    }
                }

                return allCandles.FindAll(c => c.TimeStart >= startTime && c.TimeStart < endTime);
            }

            return null;
        }

        private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (endTime > DateTime.UtcNow ||
                startTime >= endTime ||
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
                timeFrameMinutes == 60 ||
                timeFrameMinutes == 240 ||
                timeFrameMinutes == 1440)
            {
                return true;
            }

            return false;
        }

        private List<Candle> GetCandlesByMonth(Security security, string archiveDate, string secGroup, string interval)
        {
            if (_notExistingArchives.Contains(security.Name + archiveDate)) // данного архива нет на сервере
                return null;

            try
            {
                string archivePath = "https://download.gatedata.org/";

                if (secGroup == "spot")
                {
                    archivePath += secGroup + $"/candlesticks_{interval}/{archiveDate}/{security.Name}-{archiveDate}.csv.gz";  // https://download.gatedata.org/spot/candlesticks_1h/202405/USDT_TRY-202405.csv.gz - ОК
                }
                else
                {
                    archivePath += secGroup + $"/candlesticks_{interval}/{archiveDate}/{security.Name}-{archiveDate}.csv.gz";
                }

                List<Candle> monthlyCandles = new List<Candle>();

                string gzipArchivePath = DownloadGZipArchive(archivePath, archiveDate, security.Name);

                if (gzipArchivePath != null)
                {
                    string csvFilePath = GetSCVFileFromArchive(gzipArchivePath);

                    if (csvFilePath != null)
                    {
                        monthlyCandles = ParseCsvFileToCandles(csvFilePath, security);

                        if (monthlyCandles != null && monthlyCandles.Count > 0)
                        {
                            return monthlyCandles;
                        }
                        else
                        {
                            SendLogMessage($"Candles data downloading error: list trades was not there", LogMessageType.Error);
                            return null;
                        }
                    }
                    else
                    {
                        SendLogMessage($"Candles data downloading error: csv file path was null", LogMessageType.Error);
                        return null;
                    }
                }
                else
                {
                    SendLogMessage($"Candles data downloading error: archive {security.Name} {archiveDate} path was null", LogMessageType.Error);
                    return null;
                }

            }
            catch (Exception error)
            {
                SendLogMessage($"Candles data downloading error: {error}", LogMessageType.Error);
                return null;
            }
        }

        private List<Candle> ParseCsvFileToCandles(string csvFilePath, Security security)
        {
            // spot - 1751328000,3128839.7,0.008892,0.009012,0.008863,0.008956 - Timestamp	Volume	Close	High Low Open
            // fut - 1751328000,1637,0.1651,0.1659,0.1644,0.1656 - Timestamp Size Close	High Low Open

            List<Candle> candles = new List<Candle>();

            string[] lines = File.ReadAllLines(csvFilePath);

            for (int i = 0; i < lines.Length; i++)
            {
                if (char.IsLetter(lines[i][0]))
                    continue;

                string[] candleParts = lines[i].Split(',', StringSplitOptions.RemoveEmptyEntries);

                Candle newCandle = new Candle();

                newCandle.State = CandleState.Finished;
                newCandle.TimeStart = TimeManager.GetDateTimeFromTimeStampSeconds(int.Parse(candleParts[0]));
                newCandle.Volume = candleParts[1].ToDecimal();
                newCandle.Close = candleParts[2].ToDecimal();
                newCandle.High = candleParts[3].ToDecimal();
                newCandle.Low = candleParts[4].ToDecimal();
                newCandle.Open = candleParts[5].ToDecimal();

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

        public List<string> GetMonthsList(DateTime startTime, DateTime endTime)
        {
            List<string> result = new List<string>();

            DateTime current = new DateTime(startTime.Year, startTime.Month, 1);

            DateTime end = new DateTime(endTime.Year, endTime.Month, 1);

            while (current <= end)
            {
                result.Add(current.ToString("yyyyMM"));
                current = current.AddMonths(1);
            }

            return result;
        }

        private List<Candle> GetCandlesCurrMonth(Security security, TimeSpan tf, DateTime startTime, DateTime endTime)
        {
            List<Candle> candlesResult = new List<Candle>();

            DateTime startTimeData = startTime;
            DateTime partEndTime = startTimeData.AddMinutes(tf.TotalMinutes * 500);

            do
            {
                int from = TimeManager.GetTimeStampSecondsToDateTime(startTimeData);
                int to = TimeManager.GetTimeStampSecondsToDateTime(partEndTime);

                string interval = GetInterval(tf);

                List<Candle> candles = GetCandlesHistory(security, interval, from, to);

                if (candles == null || candles.Count == 0)
                {
                    break;
                }

                Candle last = candles[candles.Count - 1];

                if (last.TimeStart >= endTime)
                {
                    for (int i = 0; i < candles.Count; i++)
                    {
                        if (candles[i].TimeStart <= endTime)
                        {
                            candlesResult.Add(candles[i]);
                        }
                    }
                    break;
                }

                candlesResult.AddRange(candles);

                startTimeData = partEndTime.AddMinutes(tf.TotalMinutes);
                partEndTime = startTimeData.AddMinutes(tf.TotalMinutes * 500);

                if (startTimeData >= DateTime.UtcNow)
                {
                    break;
                }

                if (partEndTime > DateTime.UtcNow)
                {
                    partEndTime = DateTime.UtcNow;
                }

            } while (true);

            return candlesResult;
        }

        private List<Candle> GetCandlesHistory(Security security, string interval, int from, int to)
        {
            string queryParam = "";
            string requestUri = "";

            try
            {
                if (security.SecurityType == SecurityType.Futures)
                {
                    queryParam = $"contract={security.Name}&";
                    queryParam += $"interval={interval}&";
                    queryParam += $"from={from}&";
                    queryParam += $"to={to}";

                    requestUri = $"/futures/{security.NameClass.Split('_')[1].ToLower()}/candlesticks?" + queryParam;
                }
                else
                {
                    queryParam = $"currency_pair={security.Name}&";
                    queryParam += $"interval={interval}&";
                    queryParam += $"from={from}&";
                    queryParam += $"to={to}";

                    requestUri = "/spot/candlesticks?" + queryParam;
                }

                string responseMessage = GetStringRequest(requestUri);

                if (!string.IsNullOrEmpty(responseMessage))
                {
                    List<Candle> candles = new List<Candle>();

                    if (security.SecurityType == SecurityType.CurrencyPair)
                    {
                        List<string[]> rawList = JsonConvert.DeserializeAnonymousType(responseMessage, new List<string[]>());

                        for (int i = 0; i < rawList.Count; i++)
                        {
                            string[] current = rawList[i];

                            Candle candle = new Candle();

                            candle.State = CandleState.Finished;
                            candle.TimeStart = TimeManager.GetDateTimeFromTimeStampSeconds(int.Parse(current[0]));
                            candle.Volume = current[1].ToDecimal();
                            candle.Close = current[2].ToDecimal();
                            candle.High = current[3].ToDecimal();
                            candle.Low = current[4].ToDecimal();
                            candle.Open = current[5].ToDecimal();

                            candles.Add(candle);
                        }

                        return candles;
                    }
                    else // futures
                    {
                        List<GateFutCandlesResp> responseData = JsonConvert.DeserializeAnonymousType(responseMessage, new List<GateFutCandlesResp>());

                        for (int i = 0; i < responseData.Count; i++)
                        {
                            GateFutCandlesResp current = responseData[i];

                            Candle candle = new Candle();

                            candle.State = CandleState.Finished;
                            candle.TimeStart = TimeManager.GetDateTimeFromTimeStampSeconds(int.Parse(current.t));
                            candle.Volume = current.sum.ToDecimal();
                            candle.Close = current.c.ToDecimal();
                            candle.High = current.h.ToDecimal();
                            candle.Low = current.l.ToDecimal();
                            candle.Open = current.o.ToDecimal();

                            candles.Add(candle);
                        }

                        return candles;
                    }
                }
                else
                {
                    SendLogMessage($"Candles getting error: response message = {responseMessage}", LogMessageType.Error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Candles getting error\n" + ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private string GetInterval(TimeSpan timeFrame)
        {
            if (timeFrame.Minutes != 0)
            {
                return $"{timeFrame.Minutes}m";
            }
            else if (timeFrame.Hours != 0)
            {
                return $"{timeFrame.Hours}h";
            }
            else
            {
                return $"{timeFrame.Days}d";
            }
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (actualTime > endTime)
            {
                return null;
            }

            NewSecurityCheck(security.Name);

            string secGroup = "";

            if (security.SecurityType == SecurityType.CurrencyPair)
            {
                secGroup = "spot";
            }
            else
            {
                secGroup = "futures_" + security.NameClass.Split('_')[1].ToLower();
            }

            DateTime currMonthStart = DateTime.Now.Date.AddDays(1 - DateTime.Now.Day);

            List<Trade> allTrades = new List<Trade>();

            string startArchiveDate = startTime.ToString("yyyyMM");

            string endArchiveDate = endTime.ToString("yyyyMM");

            if (startTime.Year < 2022)
            {
                startArchiveDate = "202201";
                endArchiveDate = "202201";
            }

            List<string> needsDayPaths = new List<string>();

            DateTime needDate = startTime;

            while (needDate < endTime)
            {
                needsDayPaths.Add(_tempDirectory + security.Name + needDate.ToString("yyyyMMdd") + ".csv");

                needDate = needDate.AddDays(1);
            }

            if (startTime > currMonthStart) // запрашиваем тики из текущего месяца только REST
            {
                SendLogMessage($"Atention! The loading of trades for the current month can be very long. " +
                    $"For fast loading, set the end time to {currMonthStart.ToShortDateString()}.", LogMessageType.Error);

                allTrades = GetTradesCurrMonth(security, startTime, endTime);
            }
            else if (startTime < currMonthStart && endTime > currMonthStart) // FTP + REST
            {
                SendLogMessage($"Atention! The loading of trades for the current month can be very long.\n" +
                   $"For fast loading, set end time to {currMonthStart.ToShortDateString()}.", LogMessageType.Error);

                List<Trade> trades = GetOneMonthArchives(security, startArchiveDate, secGroup, needsDayPaths);

                if (trades != null && trades.Count > 0)
                {
                    allTrades.AddRange(trades);
                }

                allTrades.AddRange(GetTradesCurrMonth(security, currMonthStart, endTime));
            }
            else if (startTime < currMonthStart && endTime < currMonthStart) // закачиваем месячный архив
            {
                if (startArchiveDate != endArchiveDate)
                {
                    // взять из двух месячных архивов
                    string[] archiveDates = [startArchiveDate, endArchiveDate];

                    for (int i = 0; i < archiveDates.Length; i++)
                    {
                        string needDay = _dailyArchivesPaths.Find(p => p.Contains(security.Name + archiveDates[i]));

                        if (needDay == null) // нет архивов за этот месяц
                        {
                            TryGetTradesByMonth(security, archiveDates[i], secGroup);
                        }
                        else
                        {
                            continue;
                        }
                    }

                    // пробуем скачать трейды из дневных архивов

                    for (int i = 0; i < needsDayPaths.Count; i++)
                    {
                        if (File.Exists(needsDayPaths[i]))
                        {
                            List<Trade> dailyTrades = GetTradesByDay(needsDayPaths[i], security);

                            allTrades.AddRange(dailyTrades);
                        }
                    }
                }
                else if (startArchiveDate == endArchiveDate)
                {
                    // нужен архив одного месяца

                    List<Trade> trades = GetOneMonthArchives(security, startArchiveDate, secGroup, needsDayPaths);

                    if (trades != null && trades.Count > 0)
                    {
                        allTrades.AddRange(trades);
                    }
                }
            }

            allTrades.Sort(CompareTradesByTime);

            return allTrades;
        }

        private List<Trade> GetOneMonthArchives(Security security, string startArchiveDate, string secGroup, List<string> needsDayPaths)
        {
            List<Trade> trades = new List<Trade>();

            string needDay = _dailyArchivesPaths.Find(p => p.Contains(security.Name + startArchiveDate));

            if (needDay != null) // уже есть дневные архивы за этот месяц
            {
                for (int i = 0; i < needsDayPaths.Count; i++)
                {
                    if (File.Exists(needsDayPaths[i]))
                    {
                        List<Trade> dailyTrades = GetTradesByDay(needsDayPaths[i], security);

                        trades.AddRange(dailyTrades);
                    }
                }
            }
            else // загрузить месячный архив
            {
                if (TryGetTradesByMonth(security, startArchiveDate, secGroup))
                {
                    for (int i = 0; i < needsDayPaths.Count; i++)
                    {
                        if (File.Exists(needsDayPaths[i]))
                        {
                            List<Trade> dailyTrades = GetTradesByDay(needsDayPaths[i], security);

                            trades.AddRange(dailyTrades);
                        }
                    }
                }
                else
                {
                    // не удалось скачать архив, распарсить
                    return null;
                }
            }

            return trades;
        }

        private void NewSecurityCheck(string securityName)
        {
            if (_currSecurity == string.Empty)
            {
                _currSecurity = securityName;
            }
            else if (securityName != _currSecurity)
            {
                // загрузка нового инструмента
                // почистить папку от лишних файлов
                string[] csvFiles = Directory.GetFiles(_tempDirectory);

                if (csvFiles.Length > 0)
                {
                    for (int i = 0; i < csvFiles.Length; i++)
                    {
                        if (!csvFiles[i].Contains(securityName))
                        {
                            File.Delete(csvFiles[i]);
                        }
                    }
                }

                _dailyArchivesPaths.Clear();

                _currSecurity = securityName;
            }
        }

        private List<Trade> GetTradesByDay(string dayArchivePath, Security security)
        {
            // Futures: 1754006401.649670,41205732,16.921,-675
            // Spot: 1754006464.010626,481539,0.006764,0.135,2 // Side: Направление торговли, 1: Продажа 2: Покупка

            List<Trade> trades = new List<Trade>();

            string[] lines = File.ReadAllLines(dayArchivePath);

            for (int i = 0; i < lines.Length; i++)
            {
                string[] tradeParts = lines[i].Split(',', StringSplitOptions.RemoveEmptyEntries);

                string[] timeData = tradeParts[0].Split('.');

                Trade trade = new Trade();

                trade.SecurityNameCode = security.Name;

                DateTime time = TimeManager.GetDateTimeFromTimeStampSeconds(long.Parse(timeData[0]));

                if (timeData.Length > 1)
                {
                    time = time.AddMicroseconds(double.Parse(timeData[1]));
                }

                trade.Time = time;

                trade.Id = tradeParts[1];
                trade.Price = tradeParts[2].ToDecimal();

                if (security.SecurityType == SecurityType.Futures)
                {
                    trade.Volume = Math.Abs(tradeParts[3].ToDecimal());
                    trade.Side = tradeParts[3].ToDecimal() > 0 ? Side.Buy : Side.Sell;
                }
                else
                {
                    trade.Volume = Math.Abs(tradeParts[3].ToDecimal());
                    trade.Side = tradeParts[4] == "1" ? Side.Sell : Side.Buy;
                }

                trades.Add(trade);
            }

            File.Delete(dayArchivePath);

            _dailyArchivesPaths.Remove(dayArchivePath);

            return trades;
        }

        private bool TryGetTradesByMonth(Security security, string archiveDate, string secGroup)
        {
            if (_notExistingArchives.Contains(security.Name + archiveDate)) // данного архива нет на сервере
                return false;

            try
            {
                string archivePath = "https://download.gatedata.org/";

                if (secGroup == "spot")
                {
                    archivePath += secGroup + $"/deals/{archiveDate}/{security.Name}-{archiveDate}.csv.gz";  // https://download.gatedata.org/spot/deals/202508/BTC_USDT-202508.csv.gz - ОК
                }
                else
                {
                    archivePath += secGroup + $"/trades/{archiveDate}/{security.Name}-{archiveDate}.csv.gz";  // https://download.gatedata.org/futures_usdt/trades/202508/ADA_USDT-202508.csv.gz - ОК
                }

                string gzipArchivePath = DownloadGZipArchive(archivePath, archiveDate, security.Name);

                if (gzipArchivePath != null)
                {
                    string csvFilePath = GetSCVFileFromArchive(gzipArchivePath);

                    if (csvFilePath != null)
                    {
                        List<string> dailyTradesPaths = ParseCsvFileToDailyArchives(csvFilePath, security);

                        if (dailyTradesPaths.Count > 0)
                        {
                            _dailyArchivesPaths.AddRange(dailyTradesPaths);

                            SendLogMessage($"Trades {security.Name} for {archiveDate} processed", LogMessageType.System);

                            return true;
                        }
                        else
                        {
                            SendLogMessage($"Trades data downloading error: list paths was not there", LogMessageType.Error);
                            return false;
                        }
                    }
                    else
                    {
                        SendLogMessage($"Trades data downloading error: csv file path was null", LogMessageType.Error);
                        return false;
                    }
                }
                else
                {
                    SendLogMessage($"Trades data downloading error: archive path was null", LogMessageType.Error);
                    return false;
                }
            }
            catch (Exception error)
            {
                SendLogMessage($"Trades data downloading error: {error}", LogMessageType.Error);
                return false;
            }
        }

        private List<Trade> GetTradesCurrMonth(Security security, DateTime startTime, DateTime endTime)
        {
            long initTimeStamp = TimeManager.GetTimeStampSecondsToDateTime(endTime);

            List<Trade> trades = GetTickDataFrom(security, initTimeStamp);

            if (trades == null)
            {
                return null;
            }

            List<Trade> allTrades = new List<Trade>(100000);

            allTrades.AddRange(trades);

            Trade firstRange = trades[trades.Count - 1];

            List<Trade> allNeedTrades = new List<Trade>();

            while (firstRange.Time > startTime)
            {
                int ts = TimeManager.GetTimeStampSecondsToDateTime(firstRange.Time);

                trades = GetTickDataFrom(security, ts);

                if (trades.Count == 0)
                {
                    break;
                }

                firstRange = trades[trades.Count - 1];
                allTrades.AddRange(trades);
            }

            allTrades.Reverse();

            for (int i = 0; i < allTrades.Count; i++)
            {
                if (allTrades[i].Time >= startTime
                    && allTrades[i].Time <= endTime)
                {
                    allNeedTrades.Add(allTrades[i]);
                }
            }

            return ClearTrades(allNeedTrades);
        }

        private List<Trade> GetTickDataFrom(Security security, long startTimeStamp)
        {
            string queryParam = "";
            string requestUri = "";

            try
            {
                if (security.SecurityType == SecurityType.Futures)
                {
                    queryParam = $"contract={security.Name}&";
                    queryParam += "limit=1000&";
                    queryParam += $"to={startTimeStamp}";

                    requestUri = $"/futures/{security.NameClass.Split('_')[1].ToLower()}/trades?" + queryParam;
                }
                else
                {
                    queryParam = $"currency_pair={security.Name}&";
                    queryParam += "limit=1000&";
                    queryParam += $"to={startTimeStamp}";

                    requestUri = "/spot/trades?" + queryParam;
                }

                string responseMessage = GetStringRequest(requestUri);

                if (!string.IsNullOrEmpty(responseMessage))
                {
                    List<GateDataTradeResponse> tradeResponse = JsonConvert.DeserializeAnonymousType(responseMessage, new List<GateDataTradeResponse>());

                    List<Trade> trades = new List<Trade>();

                    for (int i = 0; i < tradeResponse.Count; i++)
                    {
                        GateDataTradeResponse current = tradeResponse[i];

                        Trade trade = new Trade();

                        trade.Id = current.id;
                        trade.Price = current.price.ToDecimal();

                        if (security.SecurityType == SecurityType.Futures)
                        {
                            trade.Volume = Math.Abs(current.size.ToDecimal());
                            trade.SecurityNameCode = current.contract;
                            trade.Side = current.size.ToDecimal() > 0 ? Side.Buy : Side.Sell;
                            string[] timeData = current.create_time_ms.Split('.');
                            DateTime time = TimeManager.GetDateTimeFromTimeStampSeconds(long.Parse(timeData[0]));

                            if (timeData.Length > 1)
                            {
                                trade.Time = time.AddMilliseconds(double.Parse(timeData[1]));
                            }
                        }
                        else  // spot
                        {
                            trade.Volume = current.amount.ToDecimal();
                            trade.SecurityNameCode = current.currency_pair;
                            trade.Side = current.side == "buy" ? Side.Buy : Side.Sell;
                            long timeMs = long.Parse(current.create_time_ms.Split('.')[0]);

                            trade.Time = TimeManager.GetDateTimeFromTimeStamp(timeMs);
                        }

                        trades.Add(trade);
                    }

                    return trades;
                }
                else
                {
                    SendLogMessage($"Trades getting error: response message = {responseMessage}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Trades getting error\n" + ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<Trade> ClearTrades(List<Trade> trades)
        {
            List<Trade> newTrades = new List<Trade>();

            Trade last = null;

            for (int i = 0; i < trades.Count; i++)
            {
                Trade current = trades[i];

                if (last != null)
                {
                    if (current.Id == last.Id && current.Time == last.Time)
                    {
                        continue;
                    }
                }

                newTrades.Add(current);

                last = current;
            }

            return newTrades;
        }

        private string GetSCVFileFromArchive(string tempGzipPath)
        {
            ExtractGZip(tempGzipPath);

            // search first CSV file in archive
            string[] csvFiles = Directory.GetFiles(_tempDirectory, "*.csv");

            if (csvFiles.Length == 0)
            {
                SendLogMessage("The CSV file was not found in the archive", LogMessageType.Error);
                return null;
            }
            else
            {
                for (int i = 0; i < csvFiles.Length; i++)
                {
                    if (csvFiles[i] == _tempDirectory + "monthly.csv")
                    {
                        File.Delete(tempGzipPath);

                        return csvFiles[i];
                    }
                }

                return null;
            }
        }

        public void ExtractGZip(string gzPath)
        {
            string csvFilePath = _tempDirectory + "monthly.csv";

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

        private List<string> ParseCsvFileToDailyArchives(string csvFilePath, Security security)
        {
            List<string> createdFiles = new List<string>();

            using (StreamReader reader = new StreamReader(csvFilePath, Encoding.UTF8, true, 1024 * 1024)) // 1MB буфер чтения
            {
                StreamWriter currentDayWriter = null;
                DateTime currentDay = DateTime.MinValue;
                string line;
                int linesProcessed = 0;

                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(line) || char.IsLetter(line[0]))
                        continue;

                    string[] tradeParts = line.Split(',', StringSplitOptions.RemoveEmptyEntries);

                    if (tradeParts.Length < 1) continue;

                    string[] timeData = tradeParts[0].Split('.');

                    DateTime time = TimeManager.GetDateTimeFromTimeStampSeconds(long.Parse(timeData[0]));

                    DateTime date = time.Date;

                    // Если день сменился - закрываем предыдущий файл и открываем новый
                    if (currentDayWriter == null || date != currentDay)
                    {
                        // Закрываем предыдущий файл
                        if (currentDayWriter != null)
                        {
                            currentDayWriter.Flush();
                            currentDayWriter.Close();
                            currentDayWriter.Dispose();
                        }

                        // Открываем новый файл
                        string fileName = $"{security.Name}{date:yyyyMMdd}.csv";
                        string filePath = Path.Combine(_tempDirectory, fileName);

                        currentDayWriter = new StreamWriter(filePath, append: false, Encoding.UTF8, 1024 * 1024); // 1MB буфер записи
                        currentDay = date;
                        createdFiles.Add(filePath);
                        linesProcessed = 0;
                    }

                    // Пишем строку в текущий файл
                    currentDayWriter.WriteLine(line);
                    linesProcessed++;

                    // Периодически сбрасываем буфер на диск
                    if (linesProcessed % 10000 == 0)
                    {
                        currentDayWriter.Flush();
                    }
                }

                // Закрываем последний файл
                if (currentDayWriter != null)
                {
                    currentDayWriter.Flush();
                    currentDayWriter.Close();
                    currentDayWriter.Dispose();
                }
            }

            File.Delete(_tempDirectory + "monthly.csv");

            return createdFiles;
        }

        private int CompareTradesByTime(Trade x, Trade y)
        {
            return x.Time.CompareTo(y.Time);
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
                RestClient client = new(_httpUrlApi);

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

        private string DownloadGZipArchive(string path, string archiveDate, string secName)
        {
            try
            {
                string tempGzipPath = _tempDirectory + Path.GetRandomFileName();

                RestRequest request = new RestRequest(Method.GET);
                RestClient client = new RestClient(path);

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK && response.RawBytes != null && response.RawBytes.Length > 0)
                {
                    using (FileStream fileStream = new FileStream(tempGzipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        fileStream.Write(response.RawBytes, 0, response.RawBytes.Length);
                    }
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _notExistingArchives.Add(secName + archiveDate);

                    SendLogMessage($"Monthly archive {secName} {archiveDate} not found. The listing date may have been later.", LogMessageType.Error);
                    return null;
                }
                else
                {
                    SendLogMessage($"Сouldn't upload gzip archive. Http status: {response.StatusCode}", LogMessageType.Error);
                    return null;
                }

                return tempGzipPath;
            }
            catch (Exception ex)
            {
                SendLogMessage("Сouldn't upload gzip archive.\n" + ex, LogMessageType.Error);
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

        public void SetLeverage(Security security, decimal leverage) { }

        #endregion
    }
}
