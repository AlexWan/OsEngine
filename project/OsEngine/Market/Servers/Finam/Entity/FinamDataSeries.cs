using OsEngine.Entity;
using OsEngine.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;

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

            if (TimeStart.Day.ToString().Length == 1)
            {
                dayStart += "0" + TimeStart.Day;
            }
            else
            {
                dayStart += TimeStart.Day;
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

            string timeFrom = timeStart.ToShortDateString();
            string timeTo = timeEnd.ToShortDateString();

            string urlToSec = SecurityFinam.Name + "_" + timeStartInStrToName + "_" + timeEndInStrToName;

            string url = ServerPrefics + "/" + "export9.out?";

            url += "market=" + SecurityFinam.MarketId + "&";
            url += "em=" + SecurityFinam.Id + "&";
            url += "code=" + SecurityFinam.Code + "&";
            url += "df=" + (timeStart.Day) + "&";
            url += "mf=" + (timeStart.Month - 1) + "&";
            url += "yf=" + (timeStart.Year) + "&";
            url += "from=" + timeFrom + "&";
            url += "apply=0&";
            url += "dt=" + (timeEnd.Day) + "&";
            url += "mt=" + (timeEnd.Month - 1) + "&";
            url += "yt=" + (timeEnd.Year) + "&";
            url += "to=" + timeTo + "&";

            url += "p=" + 1 + "&";
            url += "f=" + urlToSec + "&";
            url += "e=" + ".txt" + "&";
            url += "cn=" + SecurityFinam.Name + "&";
            url += "dtf=" + 1 + "&";
            url += "tmf=" + 1 + "&";
            url += "MSOR=" + 1 + "&";
            url += "mstime=" + "on" + "&";
            url += "mstimever=" + "1" + "&";
            url += "sep=" + "1" + "&";
            url += "sep2=" + "1" + "&";
            url += "datf=" + "12" + "&";
            url += "at=" + "0";

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
                TimeActual = timeStart;
            }

            List<Candle> candles = new List<Candle>();

            const int FinamDataMonthsAvailable = 3; // Финам позволяет грузить данные внутредневных свеч не более 4 месяцев на запрос. Ставим на месяц меньше для надежности.

            while (timeStart.AddMonths(FinamDataMonthsAvailable) < timeEnd && TimeFrame != TimeFrame.Day)
            {
                List<Candle> candlesOneDay = GetCandles(timeStart, timeStart.AddMonths(FinamDataMonthsAvailable));

                timeStart = timeStart.AddMonths(FinamDataMonthsAvailable);

                if (candlesOneDay != null)
                {
                    candles.AddRange(candlesOneDay);
                }
            }

            List<Candle> candlesToday = GetCandles(timeStart, timeEnd);

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
        private List<Candle> GetCandles(DateTime timeStart, DateTime timeEnd)
        {
            //http://195.128.78.52/GBPUSD_141201_141206.csv?market=5&em=86&code=GBPUSD&df=1&mf=11&yf=2014&from=01.12.2014&dt=6&mt=11&yt=2014&to=06.12.2014&
            //p=2&f=GBPUSD_141201_141206&e=.csv&cn=GBPUSD&dtf=1&tmf=3&MSOR=1&mstime=on&mstimever=1&sep=3&sep2=1&datf=5&at=1

            if (string.IsNullOrEmpty(_timeFrameFinam))
            {
                return null;
            }

            string timeStartInStrToName = timeStart.Year.ToString()[2].ToString() + timeStart.Year.ToString()[3].ToString() + timeStart.Month + TimeStart.Day;

            string timeEndInStrToName = timeEnd.Year.ToString()[2].ToString() + timeEnd.Year.ToString()[3].ToString() + timeEnd.Month + timeEnd.Day;

            string timeFrom = timeStart.ToShortDateString();
            string timeTo = timeEnd.ToShortDateString();

            string fileName = SecurityFinam.Name + "_" + timeStartInStrToName + "_" + timeEndInStrToName;

            string url = ServerPrefics + "/" + "export9.out?";

            // https://export.finam.ru/export9.out?
            // market=91
            // &em=420446
            // &token=03AFcWeA7iP0so0-DqoyTulIsrxzG29wc4FscBa3E3NoHLgjLCK3bUKBwOCxew7UMS2i49RQRtE2q7YlyeXAIc6Z5BnoMEoiK5Xc_P8bJTOHp7FCATG7j5iZOI3tm75ZvMrxUI3_NYv3h608s6dYxZoHI_e1azzBfyU_0cvUw57Oeccgx24axPdtiebt5LIXgUbUI7g39w57HXNXlqdE3HFxw7n6JzeqXbZX0dVfD0mip3UjBCexOnjR8anpM4kAgHfqtEan1w-oARO8jW_1ud8zK7liOKqiuLWMB4RDf_BWzue4zPRF9LplZJPZ2ZF04rTWRIig2tP6xru5H9HbfFR88PqLRPX_2J-yE4DcYCabh4QVco43H9gJMUEb4ZF6i5qsTLn0RyDcBGALK4Ykrdu8a2fgM2zFm2cKA0inzr3324WgqV4dcdglljybW8BXQNMETn4Ee8hsjsqOzsdWONT79UMVNixPcjPA4JV_fPrfb_js5lq2z0mj14QEzLy0-1r7r_AEfwQD-jvWyX6eeLDbHQ6NoZYotSvEvkZfnggMr6eAGpmXw6bvDCNI1DSeNZVTdcTFis_xV5J2H3dxQTIS8zRv2GSFRSbMsrizkPGva-mi3A4Q2ySiArzxENC60befDJvq7Rbn7NZF6PXebZG_Np4DiimkCbISQkUMzS8SUNj5zBW79YtnJxPAKXMcj8WQDYfnaA33lUAIH6g7Y169hpwlE4snDFxjfyJZbDC15z8cH80XeI5G1sFBj552PQDUMgl53sRU7wcsfbv5ezsMvN3_OsiixmpHam9D3LtDtN8sQVvJ9cLPiFmQI6iUVLR_WnVvXyLUMYWLn9OqK-SBiPRgCIvAGGV0LK3lvrhnqJjwPhCyAXEPVGK3HL9_hcCkBPoaaUDjtocmpfJ-J3b5Wmcx3wy0f1kw
            // &code=RI.MOEXBC
            // &apply=0
            // &df=1
            // &mf=2
            // &yf=2025
            // &from=01.03.2025&dt=1&mt=3&yt=2025&to=01.04.2025&p=7&f=RI.MOEXBC_250301_250401&e=.txt&cn=RI.MOEXBC&dtf=1&tmf=1&MSOR=1&mstime=on&mstimever=1&sep=1&sep2=1&datf=2&at=1

            url += "market=" + SecurityFinam.MarketId + "&";
            url += "em=" + SecurityFinam.Id + "&";
            url += "code=" + SecurityFinam.Code + "&";
            url += "df=" + (timeStart.Day) + "&";
            url += "mf=" + (timeStart.Month - 1) + "&";
            url += "yf=" + (timeStart.Year) + "&";
            url += "from=" + timeFrom + "&";

            url += "dt=" + (timeEnd.Day) + "&";
            url += "mt=" + (timeEnd.Month - 1) + "&";
            url += "yt=" + (timeEnd.Year) + "&";
            url += "to=" + timeTo + "&";
            url += "apply=0&";
            url += "p=" + _timeFrameFinam + "&";
            url += "f=" + fileName + "&";
            url += "e=" + ".txt" + "&";
            url += "cn=" + SecurityFinam.Name + "&";
            url += "dtf=" + 1 + "&";
            url += "tmf=" + 1 + "&";
            url += "MSOR=" + 1 + "&";
            url += "mstime=" + "on" + "&";
            url += "mstimever=" + "1" + "&";
            url += "sep=" + "1" + "&";
            url += "sep2=" + "1" + "&";
            url += "datf=" + "5" + "&";
            url += "at=" + "0";


            //url = "http://export.finam.ru/export9.out?market=1&em=16842&code=GAZP&df=26&mf=8&yf=2023&from=26.09.2023&dt=28&mt=8&yt=2023&to=28.09.2023&p=3&f=GAZP_20230926_20230928&e=.txt&cn=GAZP&dtf=1&tmf=1&MSOR=0&mstime=on&mstimever=1&sep=3&sep2=1&datf=5&at=0";

            string response = _server.HttpClient.GetStringAsync(url).GetAwaiter().GetResult();

            if (response != "")
            {
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
                    candles.Add(new Candle());
                    candles[candles.Count - 1].SetCandleFromString(tradesInStr[i]);
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
                return candles;
            }

            return null;
        }


    }
}