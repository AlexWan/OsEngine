using OsEngine.Entity;
using OsEngine.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Finam.Entity
{
    public class FinamDataSeries(FinamServerRealization server)
    {
        private readonly FinamServerRealization _server = server;

        private TimeFrame _timeFrame;

        /// <summary>
        /// timeframe in Finam format
        /// таймфрейм в формате финам
        /// </summary>
        private string _timeFrameFinam;

        /// <summary>
        /// timeframe in TimeSpan format
        /// таймфрейм в формате TimeSpan
        /// </summary>
        private TimeSpan _timeFrameSpan;

        /// <summary>
        /// prefix for the server address
        /// префикс для адреса сервера
        /// </summary>
        public string ServerPrefics { get; set; } = "http://export.finam.ru";

        /// <summary>
        /// security in the Finam specification
        /// контракт в финам спецификации
        /// </summary>
        public FinamSecurity SecurityFinam { get; set; }

        /// <summary>
        /// security in Os.Engine format
        /// контракт в формате Os.Engine
        /// </summary>
        public Security Security { get; set; }

        /// <summary>
        /// timeframe
        /// таймФрейм
        /// </summary>
        public TimeFrame TimeFrame
        {
            get { return _timeFrame; }
            set
            {
                _timeFrame = value;

                if (_timeFrame == TimeFrame.Day)
                {
                    _timeFrameFinam = 8.ToString();
                    _timeFrameSpan = new TimeSpan(0, 24, 0, 0);
                }
                else if (_timeFrame == TimeFrame.Hour1)
                {
                    _timeFrameFinam = 7.ToString();
                    _timeFrameSpan = new TimeSpan(0, 1, 0, 0);
                }
                else if (_timeFrame == TimeFrame.Min30)
                {
                    _timeFrameFinam = 6.ToString();
                    _timeFrameSpan = new TimeSpan(0, 0, 30, 0);
                }
                else if (_timeFrame == TimeFrame.Min15)
                {
                    _timeFrameFinam = 5.ToString();
                    _timeFrameSpan = new TimeSpan(0, 0, 15, 0);
                }
                else if (_timeFrame == TimeFrame.Min10)
                {
                    _timeFrameFinam = 4.ToString();
                    _timeFrameSpan = new TimeSpan(0, 0, 10, 0);
                }
                else if (_timeFrame == TimeFrame.Min5)
                {
                    _timeFrameFinam = 3.ToString();
                    _timeFrameSpan = new TimeSpan(0, 0, 5, 0);
                }
                else if (_timeFrame == TimeFrame.Min1)
                {
                    _timeFrameFinam = 2.ToString();
                    _timeFrameSpan = new TimeSpan(0, 0, 1, 0);
                }
            }
        }

        /// <summary>
        /// candle series
        /// серия свечек
        /// </summary>
        public List<Candle> Candles { get; set; }

        /// <summary>
        /// start time of the download
        /// время начала скачивания
        /// </summary>
        public DateTime TimeStart { get; set; }

        /// <summary>
        /// finish time of the download
        /// время завершения скачивания
        /// </summary>
        public DateTime TimeEnd { get; set; }

        /// <summary>
        /// current time
        /// актуальное время
        /// </summary>
        public DateTime TimeActual { get; set; }

        /// <summary>
        /// is current object a downloading tick
        /// является ли текущий объект скачивающим тики
        /// </summary>
        public bool IsTick { get; set; }

        public List<Trade> Trades { get; set; }

        /// <summary>
        /// update data
        /// обновить данные
        /// </summary>
        public void Process()
        {
            if (IsTick == false)
            {
                Candles = GetCandles();

            }
            else //if (IsTick == true)
            {
                List<string> trades = GetTradesPath();

                List<Trade> listTrades = new List<Trade>();

                for (int i = 0; trades != null && i < trades.Count; i++)
                {
                    if (trades[i] == null)
                    {
                        continue;
                    }
                    StreamReader reader = new StreamReader(trades[i]);

                    while (!reader.EndOfStream)
                    {
                        try
                        {
                            Trade newTrade = new Trade();
                            newTrade.SetTradeFromString(reader.ReadLine());
                            listTrades.Add(newTrade);
                            TimeActual = newTrade.Time;
                        }
                        catch
                        {
                            // ignore
                        }

                    }
                    reader.Close();
                }

                if (listTrades.Count == 0)
                {
                    return;
                }

                Trades = listTrades;
            }
        }

        /// <summary>
        /// update trades
        /// обновить трейды
        /// </summary>
        /// <returns></returns>
        private List<string> GetTradesPath()
        {
            DateTime timeStart = TimeStart;

            DateTime timeEnd = TimeEnd;

            if (timeEnd.Date > DateTime.Now.Date)
            {
                timeEnd = DateTime.Now;
            }

            if (TimeActual != DateTime.MinValue)
            {
                timeStart = TimeActual;
            }

            List<string> trades = new List<string>();

            while (timeStart.Date < timeEnd.Date)
            {
                string tradesOneDay = GetTrades(timeStart.Date, timeStart.Date, 1);
                timeStart = timeStart.AddDays(1);

                if (tradesOneDay != null)
                {
                    trades.Add(tradesOneDay);
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            string tradesToday = GetTrades(timeStart.Date, timeStart.Date, 1);

            if (tradesToday != null)
            {
                trades.Add(tradesToday);
            }

            return trades;
        }

        /// <summary>
        /// take trade for period
        /// взять трейды за период
        /// </summary>
        /// <param name="timeStart"></param>
        /// <param name="timeEnd"></param>
        /// <returns></returns>
        private string GetTrades(DateTime timeStart, DateTime timeEnd, int iteration)
        {
            //http://195.128.78.52/GBPUSD_141201_141206.csv?market=5&em=86&code=GBPUSD&df=1&mf=11&yf=2014&from=01.12.2014&dt=6&mt=11&yt=2014&to=06.12.2014&
            //p=2&f=GBPUSD_141201_141206&e=.csv&cn=GBPUSD&dtf=1&tmf=3&MSOR=1&mstime=on&mstimever=1&sep=3&sep2=1&datf=5&at=1

            string monthStart = "";
            string dayStart = "";

            if (timeStart.Month.ToString().Length == 1)
            {
                monthStart += "0" + timeStart.Month;
            }
            else
            {
                monthStart += timeStart.Month;
            }

            if (timeStart.Day.ToString().Length == 1)
            {
                dayStart += "0" + timeStart.Day;
            }
            else
            {
                dayStart += timeStart.Day;
            }

            string timeStartInStrToName =
                timeStart.Year.ToString()[2].ToString()
                + timeStart.Year.ToString()[3].ToString()
                + monthStart + dayStart;

            string monthEnd = "";
            string dayEnd = "";

            if (timeEnd.Month.ToString().Length == 1)
            {
                monthEnd += "0" + timeEnd.Month;
            }
            else
            {
                monthEnd += timeEnd.Month;
            }

            if (timeEnd.Day.ToString().Length == 1)
            {
                dayEnd += "0" + timeEnd.Day;
            }
            else
            {
                dayEnd += timeEnd.Day;
            }

            string timeEndInStrToName = timeEnd.Year.ToString()[2].ToString()
                                        + timeEnd.Year.ToString()[3].ToString()
                                        + monthEnd
                                        + dayEnd;

            string finamToken = _server.GetToken();
            string url = BuildFinamUrl(timeStart, timeEnd, 1, 12, SecurityFinam.Name + "_" + timeStartInStrToName + "_" + timeEndInStrToName, finamToken);

            // if we have already downloaded this trades series, try to get it from the general storage
            // если мы уже эту серию трейдов качали, пробуем достать её из общего хранилища

            string secName = SecurityFinam.Name;

            if (secName.Contains("/"))
            {
                secName = Extensions.RemoveExcessFromSecurityName(secName);
            }

            string fileName = @"Data\Temp\FinamTempFiles\" + secName + "_" + timeStart.ToShortDateString() + ".txt";

            if (timeStart.Date != DateTime.Now.Date &&
                File.Exists(fileName))
            {
                return fileName;
            }

            // request data
            // запрашиваем данные

            try
            {
                using (HttpResponseMessage response = _server.HttpClient.GetAsync(url).GetAwaiter().GetResult())
                {
                    response.EnsureSuccessStatusCode();

                    using (Stream contentStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())

                    using (FileStream fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        contentStream.CopyTo(fileStream);
                    }
                }

            }
            catch (Exception ex)
            {
                _server.SendLogMessage("Сouldn't upload trades file.\n" + ex, LogMessageType.Error);
                return null;

            }

            StringBuilder list = new StringBuilder();

            StreamReader reader = new StreamReader(fileName);

            while (!reader.EndOfStream)
            {
                string[] s = reader.ReadLine().Split(',');

                if (s.Length < 5)
                {
                    continue;
                }

                StringBuilder builder = new StringBuilder();

                builder.Append(s[0] + ",");
                builder.Append(s[1] + ",");
                builder.Append(s[2] + ",");
                builder.Append(s[3] + ",");

                if (s[5] == "S")
                {
                    builder.Append("Sell");
                }
                else
                {
                    builder.Append("Buy");
                }

                list.Append(builder + "\r\n");
            }

            reader.Close();

            StreamWriter writer = new StreamWriter(fileName);
            writer.Write(list);
            writer.Close();

            return fileName;
        }


        /// <summary>
        /// update candles
        /// обновить свечи
        /// </summary>
        /// <returns></returns>
        private List<Candle> GetCandles()
        {
            DateTime timeStart = TimeStart;

            DateTime timeEnd = TimeEnd;

            if (timeEnd.Date > DateTime.Now.Date)
            {
                timeEnd = DateTime.Now;
            }

            if (TimeActual != DateTime.MinValue)
            {
                timeStart = TimeActual;
            }

            List<Candle> candles = new List<Candle>();

            string finamToken = _server.GetToken();

            const int FinamDataMonthsAvailable = 3; // Финам позволяет грузить данные внутредневных свеч не более 4 месяцев на запрос. Ставим на месяц меньше для надежности.

            while (timeStart.AddMonths(FinamDataMonthsAvailable) < timeEnd && TimeFrame != TimeFrame.Day)
            {
                List<Candle> candlesOneDay = GetCandles(timeStart, timeStart.AddMonths(FinamDataMonthsAvailable), finamToken);

                timeStart = timeStart.AddMonths(FinamDataMonthsAvailable);

                if (candlesOneDay != null)
                {
                    candles.AddRange(candlesOneDay);
                }
            }

            List<Candle> candlesToday = GetCandles(timeStart, timeEnd, finamToken);

            if (candlesToday != null)
            {
                candles.AddRange(candlesToday);
            }

            if (candles.Count != 0)
            {
                TimeActual = candles[candles.Count - 1].TimeStart;
            }

            return candles;
        }

        /// <summary>
        /// take candles for period
        /// взять свечи за период
        /// </summary>
        /// <param name="timeStart"></param>
        /// <param name="timeEnd"></param>
        /// <returns></returns>
        private List<Candle> GetCandles(DateTime timeStart, DateTime timeEnd, string finamToken)
        {
            //http://195.128.78.52/GBPUSD_141201_141206.csv?market=5&em=86&code=GBPUSD&df=1&mf=11&yf=2014&from=01.12.2014&dt=6&mt=11&yt=2014&to=06.12.2014&
            //p=2&f=GBPUSD_141201_141206&e=.csv&cn=GBPUSD&dtf=1&tmf=3&MSOR=1&mstime=on&mstimever=1&sep=3&sep2=1&datf=5&at=1

            if (string.IsNullOrEmpty(_timeFrameFinam))
            {
                return null;
            }

            string timeStartInStrToName = timeStart.Year.ToString()[2].ToString() + timeStart.Year.ToString()[3].ToString() + timeStart.Month + timeStart.Day;

            string timeEndInStrToName = timeEnd.Year.ToString()[2].ToString() + timeEnd.Year.ToString()[3].ToString() + timeEnd.Month + timeEnd.Day;
            string fileName = SecurityFinam.Name + "_" + timeStartInStrToName + "_" + timeEndInStrToName;
            string url = BuildFinamUrl(timeStart, timeEnd, Convert.ToInt32(_timeFrameFinam), 5, fileName, finamToken);

            //url = "http://export.finam.ru/export9.out?market=1&em=16842&code=GAZP&df=26&mf=8&yf=2023&from=26.09.2023&dt=28&mt=8&yt=2023&to=28.09.2023&p=3&f=GAZP_20230926_20230928&e=.txt&cn=GAZP&dtf=1&tmf=1&MSOR=0&mstime=on&mstimever=1&sep=3&sep2=1&datf=5&at=0";

            string response = DownloadFinamStringWithRetry(url);

            if (string.IsNullOrEmpty(response))
            {
                return null;
            }

            List<Candle> candles = new List<Candle>();

            response = response.Replace("\r\n", "&");

            string[] tradesInStr = response.Split('&');

            if (tradesInStr.Length == 1)
            {
                return null;
            }

            for (int i = 0; i < tradesInStr.Length; i++)
            {
                if (tradesInStr[i] == "")
                {
                    continue;
                }

                string row = AdaptFinamCandleRowForOsEngine(tradesInStr[i]);
                if (row == null)
                {
                    continue;
                }

                try
                {
                    candles.Add(new Candle());
                    candles[candles.Count - 1].SetCandleFromString(row);
                    candles[candles.Count - 1].TimeStart = candles[candles.Count - 1].TimeStart.Add(-_timeFrameSpan);

                    if (candles.Count > 1)
                    {
                        if (candles[candles.Count - 1].TimeStart <= candles[candles.Count - 2].TimeStart)
                        {
                            candles.RemoveAt(candles.Count - 1);
                            continue;
                        }
                    }
                }
                catch
                {
                    candles.RemoveAt(candles.Count - 1);
                }
            }

            return candles;
        }

        /// <summary>
        /// Приводит строку свечи экспорта Finam (<c>datf=1</c>) к формату, который ожидает <see cref="Candle.SetCandleFromString"/>.
        /// </summary>
        private static string AdaptFinamCandleRowForOsEngine(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            string t = raw.Trim();
            if (t.Length != 0 && t[0] == '<')
            {
                return null;
            }

            string[] p = t.Split(',', StringSplitOptions.TrimEntries);

            if (p.Length >= 9)
            {
                return BuildOsEngineCandleRow(p, dateFieldIndex: 2);
            }

            if (p.Length >= 7)
            {
                return BuildOsEngineCandleRow(p, dateFieldIndex: 0);
            }

            return t;
        }

        private static string BuildOsEngineCandleRow(string[] parts, int dateFieldIndex)
        {
            string[] cols = new string[7];
            cols[0] = NormalizeFinamYyyyMmDd(parts[dateFieldIndex]);
            Array.Copy(parts, dateFieldIndex + 1, cols, 1, 6);
            return string.Join(",", cols);
        }

        /// <summary>
        /// Нормализует дату из Finam к полному календарному виду <c>YYYYMMDD</c>.
        /// </summary>
        private static string NormalizeFinamYyyyMmDd(string dateCell)
        {
            if (string.IsNullOrWhiteSpace(dateCell))
            {
                return dateCell;
            }

            ReadOnlySpan<char> d = dateCell.AsSpan().Trim();
            if (d.Length == 8)
            {
                return d.ToString();
            }

            if (d.Length == 6 && int.TryParse(d, NumberStyles.Integer, CultureInfo.InvariantCulture, out int yymmdd))
            {
                int yy = yymmdd / 10000;
                int mm = yymmdd / 100 % 100;
                int dd = yymmdd % 100;
                // Порог как у Finam/старых выгрузок: 70–99 → 19xx, 00–69 → 20xx (не стандартное окно двухзначного года BCL).
                int year = yy >= 70 ? 1900 + yy : 2000 + yy;
                return (year * 10000 + mm * 100 + dd).ToString();
            }

            return d.ToString();
        }

        /// <summary>
        /// Повторы при обрыве/лимитах: экспоненциальная задержка с потолком и лёгкий jitter.
        /// </summary>
        private string DownloadFinamStringWithRetry(string url)
        {
            const int maxAttempts = 8;
            int delayMs = 500;
            const int delayCapMs = 12000;
            Exception lastEx = null;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    return _server.HttpClient.GetStringAsync(url).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    if (attempt < maxAttempts - 1 && IsTransientFinamHttpError(ex))
                    {
                        int wait = Math.Min(delayCapMs, delayMs) + Random.Shared.Next(0, 280);
                        if (attempt == 0)
                        {
                            _server.SendLogMessage(
                                "Finam: загрузка данных временно приостановлена — ожидание снятия лимита запросов / восстановления связи, затем повтор.",
                                LogMessageType.System);
                        }

                        Thread.Sleep(wait);
                        delayMs = Math.Min(delayCapMs, delayMs * 2);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (lastEx != null)
            {
                _server.SendLogMessage("Candles data downloading error: " + lastEx, LogMessageType.Error);
            }

            return null;
        }

        private static bool IsTransientFinamHttpError(Exception ex)
        {
            if (ex == null)
            {
                return false;
            }

            if (ex is HttpRequestException || ex is IOException || ex is TaskCanceledException)
            {
                return true;
            }

            if (ex is OperationCanceledException)
            {
                return true;
            }

            return IsTransientFinamHttpError(ex.InnerException);
        }

        private string BuildFinamUrl(DateTime timeStart, DateTime timeEnd, int period, int dataFormat, string fileName, string finamToken)
        {
            string timeFrom = timeStart.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            string timeTo = timeEnd.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            string code = SecurityFinam.Code;
            string marketId = SecurityFinam.MarketId;
            string cnValue = SecurityFinam.Url != null && SecurityFinam.Url.Contains("undefined") ? code : SecurityFinam.Name;

            StringBuilder url = new StringBuilder();
            url.Append(ServerPrefics).Append("/export9.out?");
            url.Append("market=").Append(marketId).Append("&");
            url.Append("em=").Append(SecurityFinam.Id).Append("&");
            url.Append("code=").Append(Uri.EscapeDataString(code)).Append("&");
            url.Append("df=").Append(timeStart.Day).Append("&");
            url.Append("mf=").Append(timeStart.Month - 1).Append("&");
            url.Append("yf=").Append(timeStart.Year).Append("&");
            url.Append("from=").Append(Uri.EscapeDataString(timeFrom)).Append("&");
            url.Append("dt=").Append(timeEnd.Day).Append("&");
            url.Append("mt=").Append(timeEnd.Month - 1).Append("&");
            url.Append("yt=").Append(timeEnd.Year).Append("&");
            url.Append("to=").Append(Uri.EscapeDataString(timeTo)).Append("&");
            url.Append("apply=0&");
            url.Append("p=").Append(period).Append("&");
            url.Append("f=").Append(Uri.EscapeDataString(fileName)).Append("&");
            url.Append("e=.txt&");
            url.Append("cn=").Append(Uri.EscapeDataString(cnValue)).Append("&");
            url.Append("dtf=1&");
            url.Append("tmf=1&");
            url.Append("MSOR=1&");
            url.Append("mstime=on&");
            url.Append("mstimever=1&");
            url.Append("sep=1&");
            url.Append("sep2=1&");
            url.Append("datf=").Append(dataFormat).Append("&");
            url.Append("at=0");

            if (string.IsNullOrWhiteSpace(finamToken) == false)
            {
                url.Append("&finam_token=").Append(Uri.EscapeDataString(finamToken.Trim()));
            }

            return url.ToString();
        }
    }
}