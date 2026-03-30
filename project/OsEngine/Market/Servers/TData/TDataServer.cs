/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.TData.Entity;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;


namespace OsEngine.Market.Servers.TData
{
    public class TDataServer : AServer
    {
        public TDataServer()
        {
            TDataServerRealization realization = new TDataServerRealization();
            ServerRealization = realization;
            NeedToHideParameters = true;
        }
    }

    public class TDataServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public TDataServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            if (!Directory.Exists(_tradesDirectory))
            {
                Directory.CreateDirectory(_tradesDirectory);
            }

            if (!Directory.Exists(_candlesDirectory))
            {
                Directory.CreateDirectory(_candlesDirectory);
            }

            if (!Directory.Exists(_tempDirectory))
            {
                Directory.CreateDirectory(_tempDirectory);
            }
        }

        public void Connect(WebProxy proxy)
        {
            try
            {
                if (TryGetTradesArchivesDates())
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

                int currentYear = DateTime.Now.Year;

                string[] directories = Directory.GetDirectories(_candlesDirectory);

                for (int i = 0; i < directories.Length; i++)
                {
                    string dir = directories[i];
                    string dirName = Path.GetFileName(dir);

                    if (dirName.EndsWith(currentYear.ToString()))
                    {
                        Directory.Delete(dir, recursive: true); // годовой архив текущего года обновляется каждый день, его удаляем
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Dispose directories error\n{ex.Message}", LogMessageType.Error);
            }

            _candlesArchivesInfoCur.Clear();
            _candlesArchivesInfoFut.Clear();
            _candlesArchivesInfoSpot.Clear();
            _candlesArchivesInfoBond.Clear();
            _datesArchives.Clear();

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private bool TryGetTradesArchivesDates()
        {
            for (int i = 2018; i <= DateTime.Now.Year; i++)
            {
                string response = GetStringRequest($"/trades-days-index/{i}");

                if (response != null)
                {
                    ArchiveResponse dates = System.Text.Json.JsonSerializer.Deserialize<ArchiveResponse>(response);

                    for (int k = 0; k < dates.Entries.Count; k++)
                    {
                        _datesArchives.Add(dates.Entries[k].Date);
                    }
                }
                else
                {
                    SendLogMessage($"Archives dates downloading error", LogMessageType.Error);
                    return false;
                }
            }

            return true;
        }

        public ServerType ServerType
        {
            get { return ServerType.TData; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        public bool IsCompletelyDeleted { get; set; }

        #endregion

        #region 2 Properties

        public DateTime ServerTime { get; set; }

        public List<IServerParameter> ServerParameters { get; set; }

        private string _baseUrl = "https://invest-public-api.tinkoff.ru";

        private Dictionary<string, TSecurityResponse> _candlesArchivesInfoSpot = [];

        private Dictionary<string, TSecurityResponse> _candlesArchivesInfoFut = [];

        private Dictionary<string, TSecurityResponse> _candlesArchivesInfoCur = [];

        private Dictionary<string, TSecurityResponse> _candlesArchivesInfoBond = [];

        private DateTime _startTimeCandleArchive = new DateTime(2018, 03, 07);

        private DateTime _startTimeTradeArchive = new DateTime(2018, 12, 19);

        private HashSet<string> _datesArchives = new(200); // "2018-01-23"

        private string _tradesDirectory = @"Data\TDataStorage\Trades\";

        private string _candlesDirectory = @"Data\TDataStorage\Candles\";

        private string _tempDirectory = @"Data\Temp\TDataTempFiles\";

        private Dictionary<string, decimal> _securitiesLots = [];

        #endregion

        #region 3 Securities

        private List<Security> _securities = new List<Security>();

        public void GetSecurities()
        {
            SendLogMessage("Downloading the list of securities...", LogMessageType.System);

            try
            {
                GetLotsBySecurities();

                GetSpotSecurities();

                GetFuturesSecurities();

                GetCurrencySecurities();

                GetBondSecurities();
            }
            catch (Exception ex)
            {

                SendLogMessage($"Securities downloading error. Error: {ex.Message}", LogMessageType.Error);
            }

            if (_securities.Count > 0)
            {
                _securities.Sort((x, y) => string.Compare(x.NameFull, y.NameFull, StringComparison.OrdinalIgnoreCase));

                SendLogMessage($"{_securities.Count} securities loaded", LogMessageType.System);
            }

            SecurityEvent?.Invoke(_securities);
        }

        private void GetLotsBySecurities()
        {
            try
            {
                string[] lotsLines = File.ReadAllLines("TDataSecuritiesLots.txt");

                for (int i = 0; i < lotsLines.Length; i++)
                {
                    string[] nameAndLot = lotsLines[i].Split('#', StringSplitOptions.RemoveEmptyEntries);

                    decimal lot = nameAndLot[1].ToDecimal();

                    _securitiesLots.Add(nameAndLot[0], lot);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Securities lots parse error: {ex.Message}", LogMessageType.Error);
            }
        }

        private void CombineDataBySecurities(Dictionary<string, TSecurityResponse> targetDict, List<TSecurityResponse> candlesResponse)
        {
            try
            {
                Dictionary<string, TSecurityResponse> sourceDict = new Dictionary<string, TSecurityResponse>(candlesResponse.Count);

                for (int i = 0; i < candlesResponse.Count; i++)
                {
                    TSecurityResponse secWithArch = candlesResponse[i];

                    string newKey = secWithArch.Ticker + "_" + secWithArch.ClassCode;

                    sourceDict.TryAdd(newKey, secWithArch);
                }

                foreach (KeyValuePair<string, TSecurityResponse> pair in sourceDict)
                {
                    targetDict[pair.Key] = pair.Value;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Dictionaries combine error: {ex.Message}", LogMessageType.Error);
            }
        }

        private void GetSpotSecurities()
        {
            string response = GetStringRequest("/trades-instruments-index/instrument-type/share");

            if (response != null)
            {
                RootResponse root = System.Text.Json.JsonSerializer.Deserialize<RootResponse>(response);

                Dictionary<string, TSecurityResponse>.Enumerator enumerator = root.Instruments.GetEnumerator();

                while (enumerator.MoveNext())
                {
                    if (enumerator.Current.Value.ClassCode != "TQBR")
                        root.Instruments.Remove(enumerator.Current.Key);
                }

                List<TSecurityResponse> tqbrInstruments = new List<TSecurityResponse>(root.Instruments.Values);

                for (int i = 0; i < tqbrInstruments.Count; i++)
                {
                    TSecurityResponse item = tqbrInstruments[i];

                    Security newSecurity = new Security();

                    newSecurity.Exchange = "MOEX";
                    newSecurity.Name = item.Ticker;
                    newSecurity.NameFull = item.Name;
                    newSecurity.NameClass = item.ClassCode;
                    newSecurity.NameId = item.Ticker;
                    newSecurity.SecurityType = SecurityType.Stock;
                    newSecurity.PriceStep = 0;
                    newSecurity.PriceStepCost = 0;
                    newSecurity.Lot = _securitiesLots.TryGetValue(item.Ticker, out decimal lot) ? lot : 1;

                    _securities.Add(newSecurity);
                }

                // получить информацию по свечным архивам
                string candlesInfoResponse = GetStringRequest("/candles-instruments-index/instrument-type/share");

                if (candlesInfoResponse != null)
                {
                    RootResponse candlesRoot = System.Text.Json.JsonSerializer.Deserialize<RootResponse>(candlesInfoResponse);

                    List<TSecurityResponse> tqbrSecCandles = new List<TSecurityResponse>(candlesRoot.Instruments.Values)
                        .FindAll(instrument => instrument.ClassCode == "TQBR");

                    if (tqbrSecCandles.Count > 0)
                    {
                        _candlesArchivesInfoSpot = root.Instruments;

                        CombineDataBySecurities(_candlesArchivesInfoSpot, tqbrSecCandles);
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
            string response = GetStringRequest("/trades-instruments-index/instrument-type/futures");

            if (response != null)
            {
                RootResponse root = System.Text.Json.JsonSerializer.Deserialize<RootResponse>(response);

                List<TSecurityResponse> futures = new List<TSecurityResponse>(root.Instruments.Values);

                for (int i = 0; i < futures.Count; i++)
                {
                    TSecurityResponse item = futures[i];

                    Security newSecurity = new Security();

                    newSecurity.Exchange = "MOEX";
                    newSecurity.Name = item.Ticker;
                    newSecurity.NameFull = item.Name;
                    newSecurity.NameClass = item.ClassCode;
                    newSecurity.NameId = item.Ticker;
                    newSecurity.SecurityType = SecurityType.Futures;
                    newSecurity.PriceStep = 0;
                    newSecurity.PriceStepCost = 0;
                    newSecurity.Lot = 1;

                    _securities.Add(newSecurity);
                }

                // получить информацию по свечным архивам
                string candlesInfoResponse = GetStringRequest("/candles-instruments-index/instrument-type/futures");

                if (candlesInfoResponse != null)
                {
                    RootResponse candlesRoot = System.Text.Json.JsonSerializer.Deserialize<RootResponse>(candlesInfoResponse);

                    List<TSecurityResponse> futSecCandles = new List<TSecurityResponse>(candlesRoot.Instruments.Values);

                    if (futSecCandles.Count > 0)
                    {
                        _candlesArchivesInfoFut = root.Instruments;

                        CombineDataBySecurities(_candlesArchivesInfoFut, futSecCandles);
                    }
                }
            }
            else
            {
                SendLogMessage($"Securities futures downloading error", LogMessageType.Error);
            }
        }

        private void GetCurrencySecurities()
        {
            string response = GetStringRequest("/trades-instruments-index/instrument-type/currency");

            if (response != null)
            {
                RootResponse root = System.Text.Json.JsonSerializer.Deserialize<RootResponse>(response);

                Dictionary<string, TSecurityResponse>.Enumerator enumerator = root.Instruments.GetEnumerator();

                while (enumerator.MoveNext())
                {
                    if (enumerator.Current.Value.ClassCode != "CETS")
                        root.Instruments.Remove(enumerator.Current.Key);
                }

                List<TSecurityResponse> tqbrInstruments = new List<TSecurityResponse>(root.Instruments.Values);

                for (int i = 0; i < tqbrInstruments.Count; i++)
                {
                    TSecurityResponse item = tqbrInstruments[i];

                    Security newSecurity = new Security();

                    newSecurity.Exchange = "MOEX";
                    newSecurity.Name = item.Ticker;
                    newSecurity.NameFull = item.Name;
                    newSecurity.NameClass = item.ClassCode;
                    newSecurity.NameId = item.Ticker;
                    newSecurity.SecurityType = SecurityType.CurrencyPair;
                    newSecurity.PriceStep = 0;
                    newSecurity.PriceStepCost = 0;
                    newSecurity.Lot = _securitiesLots.TryGetValue(item.Ticker, out decimal lot) ? lot : 1;

                    _securities.Add(newSecurity);
                }

                // получить информацию по свечным архивам
                string candlesInfoResponse = GetStringRequest("/candles-instruments-index/instrument-type/currency");

                if (candlesInfoResponse != null)
                {
                    RootResponse candlesRoot = System.Text.Json.JsonSerializer.Deserialize<RootResponse>(candlesInfoResponse);

                    List<TSecurityResponse> cetsSecCandles = new List<TSecurityResponse>(candlesRoot.Instruments.Values)
                        .FindAll(instrument => instrument.ClassCode == "CETS");

                    if (cetsSecCandles.Count > 0)
                    {
                        _candlesArchivesInfoCur = root.Instruments;

                        CombineDataBySecurities(_candlesArchivesInfoCur, cetsSecCandles);
                    }
                }
            }
            else
            {
                SendLogMessage($"Securities currency downloading error", LogMessageType.Error);
            }
        }

        private void GetBondSecurities()
        {
            string response = GetStringRequest("/trades-instruments-index/instrument-type/bond");

            if (response != null)
            {
                RootResponse root = System.Text.Json.JsonSerializer.Deserialize<RootResponse>(response);

                Dictionary<string, TSecurityResponse>.Enumerator enumerator = root.Instruments.GetEnumerator();

                while (enumerator.MoveNext())
                {
                    if (enumerator.Current.Value.ClassCode != "TQCB" && enumerator.Current.Value.ClassCode != "TQOB")
                        root.Instruments.Remove(enumerator.Current.Key);
                }

                List<TSecurityResponse> tqbrInstruments = new List<TSecurityResponse>(root.Instruments.Values);

                Dictionary<string, decimal> someBondLots = new()
                {
                    {"RU000A10A8E8", 1000},
                    {"RU000A0ZYWZ2", 100 },
                    {"XS0114288789", 1000 }
                };

                for (int i = 0; i < tqbrInstruments.Count; i++)
                {
                    TSecurityResponse item = tqbrInstruments[i];

                    Security newSecurity = new Security();

                    newSecurity.Exchange = "MOEX";
                    newSecurity.Name = item.Ticker;
                    newSecurity.NameFull = item.Name;
                    newSecurity.NameClass = item.ClassCode;
                    newSecurity.NameId = item.Ticker;
                    newSecurity.SecurityType = SecurityType.Bond;
                    newSecurity.PriceStep = 0;
                    newSecurity.PriceStepCost = 0;
                    newSecurity.Lot = someBondLots.TryGetValue(item.Ticker, out decimal lot) ? lot : 1;

                    _securities.Add(newSecurity);
                }

                // получить информацию по свечным архивам
                string candlesInfoResponse = GetStringRequest("/candles-instruments-index/instrument-type/bond");

                if (candlesInfoResponse != null)
                {
                    RootResponse candlesRoot = System.Text.Json.JsonSerializer.Deserialize<RootResponse>(candlesInfoResponse);

                    List<TSecurityResponse> tqcbSecCandles = new List<TSecurityResponse>(candlesRoot.Instruments.Values)
                        .FindAll(instrument => instrument.ClassCode == "TQCB" || instrument.ClassCode == "TQOB");

                    if (tqcbSecCandles.Count > 0)
                    {
                        _candlesArchivesInfoBond = root.Instruments;

                        CombineDataBySecurities(_candlesArchivesInfoBond, tqcbSecCandles);
                    }
                }
            }
            else
            {
                SendLogMessage($"Securities bonds downloading error", LogMessageType.Error);
            }
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private List<Portfolio> _myPortfolios = new List<Portfolio>();

        public void GetPortfolios()
        {
            Portfolio newPortfolio = new Portfolio();
            newPortfolio.Number = "TInvestData Virtual Portfolio";
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
            if (startTime != actualTime)
            {
                startTime = actualTime;
            }
            if (startTime >= endTime || startTime >= DateTime.Now)
            {
                return null;
            }

            if (startTime < _startTimeCandleArchive && endTime < _startTimeCandleArchive)
            {
                SendLogMessage($"Attention! The data starts from {_startTimeCandleArchive.ToShortDateString()}", LogMessageType.System);
                return null;
            }
            else if (startTime < _startTimeCandleArchive && endTime >= _startTimeCandleArchive)
            {
                SendLogMessage($"Attention! The data starts from {_startTimeCandleArchive.ToShortDateString()}", LogMessageType.System);
                startTime = _startTimeCandleArchive;
            }

            try
            {
                string secKey = security.Name + "_" + security.NameClass;

                TSecurityResponse securityCandlesInfo = null;

                // проверка наличия архивов на сервере
                if (security.SecurityType == SecurityType.Stock)
                {
                    if (!_candlesArchivesInfoSpot.TryGetValue(secKey, out securityCandlesInfo))
                    {
                        SendLogMessage($"No archive with candles for {secKey}", LogMessageType.System);
                        return null;
                    }
                }
                else if (security.SecurityType == SecurityType.Futures)
                {
                    if (!_candlesArchivesInfoFut.TryGetValue(secKey, out securityCandlesInfo))
                    {
                        SendLogMessage($"No archive with candles for {secKey}", LogMessageType.System);
                        return null;
                    }
                }
                else if (security.SecurityType == SecurityType.CurrencyPair)
                {
                    if (!_candlesArchivesInfoCur.TryGetValue(secKey, out securityCandlesInfo))
                    {
                        SendLogMessage($"No archive with candles for {secKey}", LogMessageType.System);
                        return null;
                    }
                }
                else if (security.SecurityType == SecurityType.Bond)
                {
                    if (!_candlesArchivesInfoBond.TryGetValue(secKey, out securityCandlesInfo))
                    {
                        SendLogMessage($"No archive with candles for {secKey}", LogMessageType.System);
                        return null;
                    }
                }

                // получить url архива
                List<Archive> urls = securityCandlesInfo.Archives.FindAll(p => p.Year == startTime.Year || p.Year == endTime.Year);

                if (urls.Count == 0)
                {
                    SendLogMessage($"No archive with candles {secKey} for {startTime.Year} and {endTime.Year}", LogMessageType.System);
                    return null;
                }

                //  качаем годовые архивы
                List<Candle> allCandles = new List<Candle>();

                for (int i = 0; i < urls.Count; i++)
                {
                    // поиск папок по бумаге в локальном хранилище "Data\TDataStorage\Candles\SBER_TQBR_2018"

                    string dirName = secKey + "_" + urls[i].Year;

                    if (Directory.Exists(_candlesDirectory + dirName)) // есть папка со свечками за этот год
                    {
                        string[] dailyFiles = Directory.GetFiles(_candlesDirectory + dirName);

                        if (dailyFiles.Length > 0)
                        {
                            for (int j = 0; j < dailyFiles.Length; j++)
                            {
                                bool hasDate = TryGetDateFromFileName(dailyFiles[j], out DateTime fileDate);

                                if (hasDate && fileDate.Date < startTime.Date || fileDate.Date > endTime.Date)
                                    continue;

                                List<Candle> dailyCandles = GetCandlesFromCsv(dailyFiles[j], security);   // парсим файлы из папки 

                                if (dailyCandles.Count > 0)
                                    allCandles.AddRange(dailyCandles);
                            }
                        }
                    }
                    else
                    {
                        string zipArchivePath = DownloadGZipArchive($"/history-data{urls[i].Uri}", urls[i].Year.ToString(), security.Name);  //https://invest-public-api.tinkoff.ru/history-data?instrumentId=0e2412d4-2b57-46e7-a8b8-408c9fec6bc9&year=2021

                        if (zipArchivePath != null)
                        {
                            SafeExtractZip(zipArchivePath, _candlesDirectory + dirName);

                            string[] dailyFiles = Directory.GetFiles(_candlesDirectory + dirName);

                            if (dailyFiles.Length > 0)
                            {
                                for (int j = 0; j < dailyFiles.Length; j++)
                                {
                                    bool hasDate = TryGetDateFromFileName(dailyFiles[j], out DateTime fileDate);

                                    if (hasDate && fileDate.Date < startTime.Date || fileDate.Date > endTime.Date)
                                        continue;

                                    List<Candle> dailyCandles = GetCandlesFromCsv(dailyFiles[j], security);

                                    if (dailyCandles.Count > 0)
                                        allCandles.AddRange(dailyCandles);
                                }
                            }
                        }
                        else
                        {
                            SendLogMessage($"Candles data {security.NameFull} downloading error: archive path was null", LogMessageType.Error);
                            continue;
                        }
                    }
                }

                return ConvertCandlesToRequestedTimeFrame(allCandles, timeFrameBuilder);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Candles data downloading error: {ex}", LogMessageType.Error);
                return null;
            }
        }

        private static bool TryGetDateFromFileName(string filePath, out DateTime fileDateTime)
        {
            fileDateTime = default;

            string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
            int lastUnderscoreIndex = fileName.LastIndexOf('_');

            if (lastUnderscoreIndex < 0 || lastUnderscoreIndex >= fileName.Length - 1)
            {
                return false;
            }

            string dateString = fileName.Substring(lastUnderscoreIndex + 1);

            if (DateTime.TryParseExact(dateString, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
            {
                fileDateTime = parsedDate;
                return true;
            }

            return false;
        }

        private List<Candle> GetCandlesFromCsv(string csvFilePath, Security security)
        {
            // e6123145-9665-43e0-8413-cd61b8aa9b13;2026-01-01T23:00:00Z;300.53;300.53;300.53;299.9;182; OCHLV

            List<Candle> candles = new List<Candle>();

            string[] lines = File.ReadAllLines(csvFilePath);

            for (int i = 0; i < lines.Length; i++)
            {

                string[] candleParts = lines[i].Split(';', StringSplitOptions.RemoveEmptyEntries);

                if (candleParts.Length < 7)
                    continue;

                Candle newCandle = new Candle();

                newCandle.TimeStart = DateTime.ParseExact(candleParts[1], "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).AddHours(3);

                newCandle.Open = security.SecurityType == SecurityType.Bond ? candleParts[2].ToDecimal() * 10 : candleParts[2].ToDecimal();
                newCandle.Close = security.SecurityType == SecurityType.Bond ? candleParts[3].ToDecimal() * 10 : candleParts[3].ToDecimal();
                newCandle.High = security.SecurityType == SecurityType.Bond ? candleParts[4].ToDecimal() * 10 : candleParts[4].ToDecimal();
                newCandle.Low = security.SecurityType == SecurityType.Bond ? candleParts[5].ToDecimal() * 10 : candleParts[5].ToDecimal();
                newCandle.Volume = candleParts[6].ToDecimal() * security.Lot;

                candles.Add(newCandle);
            }

            return candles;
        }

        private List<Candle> ConvertCandlesToRequestedTimeFrame(List<Candle> oneMinCandles, TimeFrameBuilder timeFrameBuilder)
        {
            if (oneMinCandles == null || oneMinCandles.Count == 0 || timeFrameBuilder == null)
            {
                return oneMinCandles;
            }

            int tfMinutes = GetTimeFrameMinutes(timeFrameBuilder.TimeFrame);

            if (tfMinutes <= 1)
            {
                return oneMinCandles;
            }

            oneMinCandles.Sort((a, b) => a.TimeStart.CompareTo(b.TimeStart));

            List<Candle> resultCandles = new List<Candle>();

            Candle currentCandle = null;
            DateTime currentBucketStart = DateTime.MinValue;

            for (int i = 0; i < oneMinCandles.Count; i++)
            {
                Candle minuteCandle = oneMinCandles[i];
                DateTime bucketStart = GetBucketStartTime(minuteCandle.TimeStart, tfMinutes);

                if (currentCandle == null || bucketStart != currentBucketStart)
                {
                    if (currentCandle != null)
                    {
                        resultCandles.Add(currentCandle);
                    }

                    currentBucketStart = bucketStart;
                    currentCandle = new Candle();
                    currentCandle.TimeStart = bucketStart;
                    currentCandle.Open = minuteCandle.Open;
                    currentCandle.High = minuteCandle.High;
                    currentCandle.Low = minuteCandle.Low;
                    currentCandle.Close = minuteCandle.Close;
                    currentCandle.Volume = minuteCandle.Volume;
                    currentCandle.State = CandleState.Finished;
                }
                else
                {
                    if (minuteCandle.High > currentCandle.High)
                    {
                        currentCandle.High = minuteCandle.High;
                    }

                    if (minuteCandle.Low < currentCandle.Low)
                    {
                        currentCandle.Low = minuteCandle.Low;
                    }

                    currentCandle.Close = minuteCandle.Close;
                    currentCandle.Volume += minuteCandle.Volume;
                }
            }

            if (currentCandle != null)
            {
                resultCandles.Add(currentCandle);
            }

            return resultCandles;
        }

        private int GetTimeFrameMinutes(TimeFrame timeFrame)
        {
            if (timeFrame == TimeFrame.Min1)
            {
                return 1;
            }
            if (timeFrame == TimeFrame.Min2)
            {
                return 2;
            }
            if (timeFrame == TimeFrame.Min5)
            {
                return 5;
            }
            if (timeFrame == TimeFrame.Min15)
            {
                return 15;
            }
            if (timeFrame == TimeFrame.Min30)
            {
                return 30;
            }
            if (timeFrame == TimeFrame.Hour1)
            {
                return 60;
            }
            if (timeFrame == TimeFrame.Hour2)
            {
                return 120;
            }
            if (timeFrame == TimeFrame.Hour4)
            {
                return 240;
            }
            if (timeFrame == TimeFrame.Day)
            {
                return 1440;
            }

            return 1;
        }

        private DateTime GetBucketStartTime(DateTime sourceTime, int tfMinutes)
        {
            if (tfMinutes >= 1440)
            {
                return sourceTime.Date;
            }

            DateTime dayStart = sourceTime.Date;
            int minuteFromDayStart = sourceTime.Hour * 60 + sourceTime.Minute;
            int roundedMinute = minuteFromDayStart - minuteFromDayStart % tfMinutes;

            return dayStart.AddMinutes(roundedMinute);
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (ServerStatus != ServerConnectStatus.Connect)
            {
                return null;
            }

            if (actualTime > endTime || startTime > DateTime.Now.AddDays(-1))
            {
                return null;
            }

            if (startTime < _startTimeTradeArchive && endTime < _startTimeTradeArchive)
            {
                SendLogMessage($"Attention! The data starts from {_startTimeTradeArchive.ToShortDateString()}", LogMessageType.System);
                return null;
            }
            else if (startTime < _startTimeTradeArchive && endTime >= _startTimeTradeArchive)
            {
                SendLogMessage($"Attention! The data starts from {_startTimeTradeArchive.ToShortDateString()}", LogMessageType.System);
                startTime = _startTimeTradeArchive;
            }

            List<Trade> allTrades = new List<Trade>();

            try
            {
                List<DateTime> dates = GetDateRangeInclusive(startTime, endTime);

                if (dates.Count == 0)
                    return null;

                string[] archives = Directory.GetFiles(_tradesDirectory);

                CsvFastParser csvFastParser = new CsvFastParser();

                for (int i = 0; i < dates.Count; i++)
                {
                    string dayStr = dates[i].ToString("yyyy-MM-dd");

                    // поиск архива в локальном хранилище
                    int index = Array.IndexOf(archives, _tradesDirectory + dayStr + ".csv.gz");

                    if (index != -1)
                    {
                        string csvFilePath = GetSCVFileFromArchive(archives[index], dayStr);

                        if (csvFilePath != null)
                        {
                            List<Trade> dailyTrades = csvFastParser.GetTradesFromCsv(csvFilePath, security);

                            if (dailyTrades.Count > 0)
                            {
                                allTrades.AddRange(dailyTrades);
                                continue;
                            }
                            else
                            {
                                SendLogMessage($"No trades {security.NameFull} for {dayStr} in local archive", LogMessageType.System);
                                continue;
                            }
                        }
                        else
                        {
                            SendLogMessage($"Trades data downloading error: csv file path from local archive was null", LogMessageType.Error);
                            continue;
                        }
                    }

                    // поиск архива на сервере за эту дату
                    if (!_datesArchives.Contains(dayStr))
                    {
                        SendLogMessage($"There is no archive for {dates[i].ToShortDateString()}", LogMessageType.System);
                        continue;
                    }

                    // архив есть, качаем
                    string gzipArchivePath = DownloadGZipArchive($"/history-trades/{dayStr}", dayStr, security.Name, true);  //https://invest-public-api.tinkoff.ru/history-trades/2026-03-21

                    if (gzipArchivePath != null)
                    {
                        string csvFilePath = GetSCVFileFromArchive(gzipArchivePath, dayStr);

                        if (csvFilePath != null)
                        {
                            List<Trade> dailyTrades = csvFastParser.GetTradesFromCsv(csvFilePath, security);

                            if (dailyTrades.Count > 0)
                            {
                                allTrades.AddRange(dailyTrades);
                            }
                            else
                            {
                                SendLogMessage($"No trades {security.NameFull} for {dayStr} in the new archive", LogMessageType.System);
                                continue;
                            }
                        }
                        else
                        {
                            SendLogMessage($"Trades data downloading error: csv file path was null", LogMessageType.Error);
                            continue;
                        }
                    }
                    else
                    {
                        SendLogMessage($"Trades data downloading error: archive path was null", LogMessageType.Error);
                        continue;
                    }
                }

                string[] files = Directory.GetFiles(_tempDirectory);

                if (files.Length > 0)
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        File.Delete(files[i]);
                    }
                }

                allTrades.Sort(CompareTradesByTime);

                return allTrades;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Trades data downloading error: {ex}", LogMessageType.Error);
                return null;
            }
        }

        private int CompareTradesByTime(Trade x, Trade y)
        {
            return x.Time.CompareTo(y.Time);
        }

        private List<DateTime> GetDateRangeInclusive(DateTime startTime, DateTime endTime)
        {
            List<DateTime> dates = new List<DateTime>();

            DateTime currentDate = startTime.Date;
            DateTime endDate = endTime.Date;

            while (currentDate <= endDate)
            {
                dates.Add(currentDate);
                currentDate = currentDate.AddDays(1);
            }

            return dates;
        }

        private string GetSCVFileFromArchive(string archivePathTrades, string nameFile)
        {
            string csvFilePath = _tempDirectory + nameFile + ".csv";

            ExtractGZip(archivePathTrades, csvFilePath);

            if (!File.Exists(csvFilePath))
            {
                SendLogMessage("The CSV file was not found in the archive", LogMessageType.Error);
                return null;
            }
            else
            {
                return csvFilePath;
            }
        }

        private void SafeExtractZip(string zipPath, string extractPath)
        {
            try
            {
                if (!File.Exists(zipPath))
                {
                    SendLogMessage("ZIP not found", LogMessageType.Error);
                    return;
                }

                if (new FileInfo(zipPath).Length == 0)
                {
                    SendLogMessage("ZIP is empty", LogMessageType.Error);
                    return;
                }

                // Создаем директорию для извлечения
                Directory.CreateDirectory(extractPath);

                // Извлекаем все файлы с перезаписью
                ZipFile.ExtractToDirectory(zipPath, extractPath, true);

                SendLogMessage($"Successfully extracted archive to: {Path.GetDirectoryName(extractPath)}", LogMessageType.System);
            }
            catch (InvalidDataException ex)
            {
                SendLogMessage($"Invalid ZIP archive: {zipPath}\n{ex.Message}", LogMessageType.Error);
                File.Delete(zipPath);
            }
            catch (IOException ex)
            {
                SendLogMessage($"IO error while extracting: {ex.Message}", LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Unexpected error: {ex.Message}", LogMessageType.Error);
            }
        }

        public void ExtractGZip(string gzipPath, string csvFilePath)
        {
            try
            {
                using (FileStream originalFileStream = new FileStream(gzipPath, FileMode.Open))
                using (FileStream decompressedFileStream = File.Create(csvFilePath))
                using (GZipStream decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
                {
                    decompressionStream.CopyTo(decompressedFileStream);
                }
            }
            catch (InvalidDataException ex)
            {
                SendLogMessage($"Couldn't extract archive\n" + ex, LogMessageType.Error);
                File.Delete(gzipPath);
            }
        }

        #endregion

        #region 6 Queries

        private RateGate _rateGateData = new RateGate(1, TimeSpan.FromMilliseconds(2000));

        private string GetStringRequest(string url)
        {
            _rateGateData.WaitToProceed();

            try
            {
                RestRequest requestRest = new(url, RestSharp.Method.GET);

                ConfigureRestQuery(requestRest, false);

                RestClient client = new(_baseUrl);

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

        private string DownloadGZipArchive(string path, string archiveDate, string secName, bool isTrades = false)
        {
            _rateGateData.WaitToProceed();

            try
            {
                string archivePath = isTrades ? _tradesDirectory + archiveDate + ".csv.gz" : _tempDirectory + Path.GetRandomFileName();

                RestRequest request = new RestRequest(Method.GET);

                ConfigureRestQuery(request, true);

                RestClient client = new RestClient(_baseUrl + path);

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK && response.RawBytes != null && response.RawBytes.Length > 0)
                {
                    using (FileStream fileStream = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        fileStream.Write(response.RawBytes, 0, response.RawBytes.Length);
                    }
                }
                else
                {
                    SendLogMessage($"Сouldn't upload gzip archive {secName} {archiveDate}. Http status: {response.StatusCode}", LogMessageType.Error);
                    return null;
                }

                return archivePath;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Сouldn't upload gzip archive {secName} {archiveDate}.\n" + ex, LogMessageType.Error);
                return null;
            }
        }

        private void ConfigureRestQuery(RestRequest request, bool getFile)
        {
            request.AddHeader("Accept-Encoding", "gzip, deflate, br, zstd");
            request.AddHeader("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            request.AddHeader("Referer", "https://developer.tbank.ru/");
            request.AddHeader("Sec-Ch-Ua", "\"Not(A:Brand\";v=\"8\", \"Chromium\";v=\"144\", \"YaBrowser\";v=\"26.3\", \"Yowser\";v=\"2.5\"");
            request.AddHeader("Sec-Ch-Ua-Mobile", "?0");
            request.AddHeader("Sec-Ch-Ua-Platform", "Windows");
            request.AddHeader("Sec-Fetch-Site", "cross-site");

            if (getFile)
            {
                request.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                request.AddHeader("Priority", "u=0, i");
                request.AddHeader("Sec-Fetch-Dest", "document");
                request.AddHeader("Sec-Fetch-Mode", "navigate");
                request.AddHeader("Sec-Fetch-User", "?1");
                request.AddHeader("Upgrade-insecure-requests", "1");
            }
            else
            {
                request.AddHeader("Accept", "*/*");
                request.AddHeader("Priority", "u=1, i");
                request.AddHeader("Origin", "https://developer.tbank.ru");
                request.AddHeader("Sec-Fetch-Dest", "empty");
                request.AddHeader("Sec-Fetch-Mode", "cors");
            }

            request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36");
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
