using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;
using System.Threading;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class Conn_4_Validation_Candles : AServerTester
    {
        public string SecutiesToSubscrible = "BTCUSDT_BNBUSDT_ETHUSDT_ADAUSDT";

        public string SecuritiesClass = "Futures";

        public string SecuritiesSeparator = "_";

        public override void Process()
        {
            if (string.IsNullOrEmpty(SecuritiesSeparator))
            {
                this.SetNewError("Error -1. Securities separator is null or empty");
                TestEnded();
                return;
            }

            AServer myServer = Server;

            myServer.LogMessageEvent += MyServer_LogMessageEvent;

            Test();
            TestEnded();
        }

        private void MyServer_LogMessageEvent(string arg1, Logging.LogMessageType arg2)
        {
            if (arg2 != Logging.LogMessageType.Error)
            {
                return;
            }

            this.SetNewError("Error 1. Error in Server: " + arg1);
        }

        private void Test()
        {
            Thread.Sleep(10000);

            ServerConnectStatus _lastStatus = ServerConnectStatus.Disconnect;

            AServer myServer = Server;

            for (int i = 0; i < 2; i++)
            {
                Thread.Sleep(1000);

                if (i != 0 && _lastStatus == myServer.ServerStatus)
                {
                    this.SetNewError(
                        "Error 2. ServerStatus don`t change after 10 seconds before it`s start or stop. Iteration: " + i);
                    return;
                }

                if (myServer.ServerStatus == ServerConnectStatus.Disconnect)
                {
                    _lastStatus = ServerConnectStatus.Disconnect;
                    myServer.StartServer();
                    Thread.Sleep(10000);
                    List<CandleSeries> series = Subscrible();

                    if (series == null ||
                        series.Count == 0)
                    {
                        return;
                    }

                    CheckTfInSeries(series);
                    return;
                }

                if (myServer.ServerStatus == ServerConnectStatus.Connect)
                {
                    _lastStatus = ServerConnectStatus.Connect;
                    myServer.StopServer();
                    Thread.Sleep(10000);
                }
            }
        }

        private List<CandleSeries> Subscrible()
        {
            List<Security> secs = Server.Securities;

            if (secs == null || secs.Count == 0)
            {
                this.SetNewError(
                "Error 3. No securities in server!");
                return null;
            }

            string[] secsInString = SecutiesToSubscrible.Split(SecuritiesSeparator[0]);

            List<Security> secsToTest = new List<Security>();

            for (int i = 0; i < secs.Count; i++)
            {
                for (int i2 = 0; i2 < secsInString.Length; i2++)
                {
                    if (secs[i].Name == secsInString[i2] &&
                        secs[i].NameClass == SecuritiesClass)
                    {
                        secsToTest.Add(secs[i]);
                        break;
                    }
                }
            }

            if (secsToTest.Count == 0)
            {
                this.SetNewError(
                 "Error 4. No securities found by your settings!");
                return null;
            }

            if (secsToTest.Count < 5)
            {
                this.SetNewError(
                 "Error 5. Securities count < 5!");
                return null;
            }

            DateTime endWaitTime = DateTime.Now.AddMinutes(10);

            List<CandleSeries> seriesReady = new List<CandleSeries>();

            for (int i = 0; i < secsToTest.Count; i++)
            {
                if (endWaitTime < DateTime.Now)
                {
                    this.SetNewError(
                      "Error 6. Subscrible time is over! 10 minutes");
                    break;
                }

                try
                {

                    List<CandleSeries> series = GetSeriesFromSecurity(secsToTest[i]);

                    if (series == null
                        || series.Count == 0)
                    {
                        i--;
                    }
                    else
                    {
                        seriesReady.AddRange(series);
                        this.SetNewServiceInfo(
                            "Security subscrible: " + secsToTest[i].Name);
                    }
                }
                catch (Exception ex)
                {
                    this.SetNewError("Error 7. Error on subscrible: " + ex.ToString());
                    return null;
                }
            }

            return seriesReady;
        }

        private List<CandleSeries> GetSeriesFromSecurity(Security sec)
        {
            IServerPermission permission = ServerMaster.GetServerPermission(Server.ServerType);

            List<CandleSeries> seriesReady = new List<CandleSeries>();

            if (permission.TradeTimeFramePermission.TimeFrameMin1IsOn)
            {
                CandleSeries series = Subscrible(sec, TimeFrame.Min1);
                if (series != null)
                {
                    seriesReady.Add(series);
                }
            }
            if (permission.TradeTimeFramePermission.TimeFrameMin2IsOn)
            {
                CandleSeries series = Subscrible(sec, TimeFrame.Min2);
                if (series != null)
                {
                    seriesReady.Add(series);
                }
            }
            if (permission.TradeTimeFramePermission.TimeFrameMin5IsOn)
            {
                CandleSeries series = Subscrible(sec, TimeFrame.Min5);
                if (series != null)
                {
                    seriesReady.Add(series);
                }
            }
            if (permission.TradeTimeFramePermission.TimeFrameMin10IsOn)
            {
                CandleSeries series = Subscrible(sec, TimeFrame.Min10);
                if (series != null)
                {
                    seriesReady.Add(series);
                }
            }
            if (permission.TradeTimeFramePermission.TimeFrameMin15IsOn)
            {
                CandleSeries series = Subscrible(sec, TimeFrame.Min15);
                if (series != null)
                {
                    seriesReady.Add(series);
                }
            }
            if (permission.TradeTimeFramePermission.TimeFrameMin30IsOn)
            {
                CandleSeries series = Subscrible(sec, TimeFrame.Min30);
                if (series != null)
                {
                    seriesReady.Add(series);
                }
            }
            if (permission.TradeTimeFramePermission.TimeFrameHour1IsOn)
            {
                CandleSeries series = Subscrible(sec, TimeFrame.Hour1);
                if (series != null)
                {
                    seriesReady.Add(series);
                }
            }
            if (permission.TradeTimeFramePermission.TimeFrameHour2IsOn)
            {
                CandleSeries series = Subscrible(sec, TimeFrame.Hour2);
                if (series != null)
                {
                    seriesReady.Add(series);
                }
            }
            if (permission.TradeTimeFramePermission.TimeFrameHour4IsOn)
            {
                CandleSeries series = Subscrible(sec, TimeFrame.Hour4);
                if (series != null)
                {
                    seriesReady.Add(series);
                }
            }

            return seriesReady;
        }

        private CandleSeries Subscrible(Security sec, TimeFrame frame)
        {
            DateTime endWaitTime = DateTime.Now.AddMinutes(5);

            TimeFrameBuilder time = new TimeFrameBuilder(StartProgram.IsOsTrader);
            time.TimeFrame = frame;
            CandleSeries series;

            while (true)
            {
                series = Server.StartThisSecurity(sec.Name, time, sec.NameClass);

                if (series != null)
                {
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }

                if (endWaitTime < DateTime.Now)
                {
                    this.SetNewError(
                      "Error 8. Subscrible time is over! 5 minutes. TF: " + frame.ToString());
                    break;
                }
            }

            return series;
        }

        private void CheckTfInSeries(List<CandleSeries> series)
        {
            DateTime timeEnd = DateTime.Now.AddMinutes(5);

            while (true)
            {
                if (series.Count == 0)
                {
                    return;
                }

                if (timeEnd < DateTime.Now)
                {
                    return;
                }

                for (int i = 0; i < series.Count; i++)
                {
                    if (SeriesIsOk(series[i]))
                    {
                        this.SetNewServiceInfo(
                        "Candles OK: " + series[i].Security.Name +
                        " TF: " + series[i].TimeFrame.ToString() +
                        " Count: " + series[i].CandlesAll.Count.ToString());
                        series.RemoveAt(i);
                        break;
                    }
                }

                Thread.Sleep(1000);
            }
        }

        private bool SeriesIsOk(CandleSeries series)
        {
            List<Candle> candles = series.CandlesAll;

            if (candles == null || candles.Count == 0)
            {
                return false;
            }

            if (CandlesIsOk(candles, series.TimeFrame, series.Security.Name))
            {
                return true;
            }

            return false;
        }

        private bool CandlesIsOk(List<Candle> candles, TimeFrame timeFrame, string sec)
        {
            // 1 null быть не должно. Это должны быть свечи

            // 2 правильно ли расположено время в массиве. Сначала - старые данные. К концу массива - новые.
            // 3 нет ли задвоения свечек

            for (int i = 1; i < candles.Count; i++)
            {
                Candle candleNow = candles[i];
                Candle candleLast = candles[i - 1];

                if (candleLast.TimeStart > candleNow.TimeStart)
                {
                    SetNewError("Error 9. The time in the old candle is big than in the current candle " + timeFrame.ToString() + "Security: " + sec);
                    return false;
                }

                if (candleLast.TimeStart == candleNow.TimeStart)
                {
                    SetNewError("Error 10. Candle time is equal! " + timeFrame.ToString() + "Security: " + sec);
                    return false;
                }
            }

            // 4 ошибка если open ниже лоя или выше хая
            // 5 ошибка если close ниже лоя или выше хая
            // 6 ошибка если OHLC равен нулю

            for (int i = 0; i < candles.Count; i++)
            {
                Candle candleNow = candles[i];

                if (candleNow.Open > candleNow.High)
                {
                    SetNewError("Error 11. Candle open above the high " + timeFrame.ToString() + "Security: " + sec);
                    return false;
                }
                if (candleNow.Open < candleNow.Low)
                {
                    SetNewError("Error 12. Candle open below the low " + timeFrame.ToString() + "Security: " + sec);
                    return false;
                }

                if (candleNow.Close > candleNow.High)
                {
                    SetNewError("Error 13. Candle Close above the high " + timeFrame.ToString() + "Security: " + sec);
                    return false;
                }
                if (candleNow.Close < candleNow.Low)
                {
                    SetNewError("Error 14. Candle Close below the low " + timeFrame.ToString() + "Security: " + sec);
                    return false;
                }

                if (candleNow.Open == 0)
                {
                    SetNewError("Error 15. Candle Open is zero " + timeFrame.ToString() + "Security: " + sec);
                    return false;
                }

                if (candleNow.High == 0)
                {
                    SetNewError("Error 16. Candle High is zero " + timeFrame.ToString() + "Security: " + sec);
                    return false;
                }

                if (candleNow.Low == 0)
                {
                    SetNewError("Error 17. Candle Low is zero " + timeFrame.ToString() + "Security: " + sec);
                    return false;
                }

                if (candleNow.Close == 0)
                {
                    SetNewError("Error 18. Candle Close is zero " + timeFrame.ToString() + "Security: " + sec);
                    return false;
                }

                if (candleNow.Open == candleNow.High
                    && candleNow.High == candleNow.Low
                    && candleNow.Low == candleNow.Close
                    && candleNow.Volume == 0)
                {
                    // всё нормально. Некоторые биржи так закрывают пробелы в данных
                }
                else if (candleNow.Volume == 0)
                {
                    SetNewError("Error 19. Candle Volume is zero " + timeFrame.ToString() + "Security: " + sec);
                    return false;
                }
            }

            return true;
        }
    }
}