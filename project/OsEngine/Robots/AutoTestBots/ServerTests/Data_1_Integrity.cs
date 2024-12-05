using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    internal class Data_1_Integrity : AServerTester
    {
        public string SecName;

        public string SecClass;

        public DateTime StartDate;

        public override void Process()
        {
            List<Security> securities = Server.Securities;

            if (securities != null &&
                securities.Count > 0)
            {
                Security mySecurity = null;

                for (int i = 0; i < securities.Count; i++)
                {
                    if (securities[i].Name == SecName
                        && securities[i].NameClass == SecClass)
                    {
                        mySecurity = securities[i];
                        break;
                    }
                }

                if (mySecurity == null)
                {
                    SetNewError("Error 0. Security set user is not found " + SecName);
                    TestEnded();
                    return;
                }

                StartTestSecurity(mySecurity);
            }
            else
            {
                SetNewError("Error 1. No securities found");
            }

            TestEnded();
        }

        private void StartTestSecurity(Security security)
        {
            //6.1.Тесты на коротком периоде. 2 дня
            //6.1.1.Выкачивать все данные которые есть в ServerPermission как разрешённые к скачке
            //Взять один инструмент и попробовать скачать все за два дня. И по каждому источнику должно быть именно 2 дня.
            //6.1.2.Уметь скачивать трейды
            //6.1.3.Дата старта запроса должна совпадать с данными первых свечей, если данные точно есть.
            //6.1.4.Дата конца запроса должна совпадать с данными последней свечи, если данные точно есть.

            DateTime lastMidnightTime = StartDate.Date + new TimeSpan(0, 0, 1, 1); // 1 минута и 1 секунда

            DateTime startTime = lastMidnightTime.AddDays(-3);
            DateTime endTime = lastMidnightTime.AddDays(-1).AddHours(-1);

            SetNewServiceInfo("Security to load: " + security.Name);
            SetNewServiceInfo("Start time: " + startTime.ToString());
            SetNewServiceInfo("End time: " + endTime.ToString());

            IServerPermission permission = ServerMaster.GetServerPermission(Server.ServerType);

            if (permission == null)
            {
                SetNewError("Error 2. No server permission to server. Type: " + Server.ServerType.ToString());
                return;
            }

            if (permission.DataFeedTfTickCanLoad)
            {
                CheckTradeData(TimeFrame.Tick, security, startTime, endTime);
            }
            else
            {
                CheckTradeNullData(TimeFrame.Tick, security, startTime, endTime);
            }

            if (permission.DataFeedTf1MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min1, security, startTime, endTime);
            }
            else
            {
                CheckNullCandleData(TimeFrame.Min1, security, startTime, endTime);
            }

            if (permission.DataFeedTf2MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min2, security, startTime, endTime);
            }
            else
            {
                CheckNullCandleData(TimeFrame.Min2, security, startTime, endTime);
            }

            if (permission.DataFeedTf5MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min5, security, startTime, endTime);
            }
            else
            {
                CheckNullCandleData(TimeFrame.Min5, security, startTime, endTime);
            }

            if (permission.DataFeedTf10MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min10, security, startTime, endTime);
            }
            else
            {
                CheckNullCandleData(TimeFrame.Min10, security, startTime, endTime);
            }

            if (permission.DataFeedTf15MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min15, security, startTime, endTime);
            }
            else
            {
                CheckNullCandleData(TimeFrame.Min15, security, startTime, endTime);
            }

            if (permission.DataFeedTf30MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min30, security, startTime, endTime);
            }
            else
            {
                CheckNullCandleData(TimeFrame.Min30, security, startTime, endTime);
            }

            if (permission.DataFeedTf1HourCanLoad)
            {
                CheckCandleData(TimeFrame.Hour1, security, startTime, endTime);
            }
            else
            {
                CheckNullCandleData(TimeFrame.Hour1, security, startTime, endTime);
            }

            if (permission.DataFeedTf2HourCanLoad)
            {
                CheckCandleData(TimeFrame.Hour2, security, startTime, endTime);
            }
            else
            {
                CheckNullCandleData(TimeFrame.Hour2, security, startTime, endTime);
            }

            if (permission.DataFeedTf4HourCanLoad)
            {
                CheckCandleData(TimeFrame.Hour4, security, startTime, endTime);
            }
            else
            {
                CheckNullCandleData(TimeFrame.Hour4, security, startTime, endTime);
            }
        }

        private void CheckCandleData(TimeFrame timeFrame, Security security, DateTime startTime, DateTime endTime)
        {
            SetNewServiceInfo("Tf checked: " + timeFrame.ToString());

            string secName = security.Name;
            string secClass = security.NameClass;

            TimeFrameBuilder builder = GetTfBuilder(security, timeFrame);

            List<Candle> candles = null;

            try
            {
                candles = Server.GetCandleDataToSecurity(secName, secClass, builder, startTime, endTime, startTime, false);
            }
            catch (Exception ex)
            {
                SetNewError("Error 3. Exception on server request. " + timeFrame.ToString() + "\n" + ex.ToString());
                return;
            }

            if (candles == null)
            {
                SetNewError("Error 4. Null in array. " + timeFrame.ToString());
                return;
            }

            if (candles.Count == 0)
            {
                SetNewError("Error 5. Zero count elements in array. " + timeFrame.ToString());
                return;
            }

            // И по каждому источнику должно быть именно 2 дня.

            int daysCount = 1;

            DateTime startTimeReal = DateTime.Now;
            DateTime endTimeReal = DateTime.Now;

            int curDay = candles[0].TimeStart.Day;
            startTimeReal = candles[0].TimeStart;
            endTimeReal = candles[candles.Count - 1].TimeStart;

            for (int i = 0; i < candles.Count; i++)
            {
                if (candles[i].TimeStart.Day != curDay)
                {
                    daysCount++;
                    curDay = candles[i].TimeStart.Day;
                }
            }

            if (daysCount != 2)
            {
                SetNewError("Error 6. Days in candles is not 2. " + timeFrame.ToString() + "\n"
                    + " StartTime: " + startTimeReal.ToString()
                    + " EndTime: " + endTimeReal.ToString());
            }
        }

        private void CheckTradeData(TimeFrame timeFrame, Security security, DateTime startTime, DateTime endTime)
        {
            SetNewServiceInfo("Tf checked: " + timeFrame.ToString());

            string secName = security.Name;
            string secClass = security.NameClass;

            List<Trade> trades = null;

            try
            {
                trades = Server.GetTickDataToSecurity(secName, secClass, startTime, endTime, startTime, false);
            }
            catch (Exception ex)
            {
                SetNewError("Error 7. Exception on server request. " + timeFrame.ToString() + "\n" + ex.ToString());
                return;
            }

            if (trades == null)
            {
                SetNewError("Error 8. Null in array. " + timeFrame.ToString());
                return;
            }

            if (trades.Count == 0)
            {
                SetNewError("Error 9. Zero count elements in array. " + timeFrame.ToString());
                return;
            }

            // И по каждому источнику должно быть именно 2 дня.

            int daysCount = 1;

            DateTime startTimeReal = DateTime.Now;
            DateTime endTimeReal = DateTime.Now;

            int curDay = trades[0].Time.Day;
            startTimeReal = trades[0].Time;
            endTimeReal = trades[trades.Count - 1].Time;

            for (int i = 0; i < trades.Count; i++)
            {
                if (trades[i].Time.Day != curDay)
                {
                    daysCount++;
                    curDay = trades[i].Time.Day;
                }
            }

            if (daysCount != 2)
            {
                SetNewError("Error 10. Days in trades is not 2. " + timeFrame.ToString() + "\n"
                    + " StartTime: " + startTimeReal.ToString()
                    + " EndTime: " + endTimeReal.ToString());
            }
        }

        private void CheckNullCandleData(TimeFrame timeFrame, Security security, DateTime startTime, DateTime endTime)
        {
            SetNewServiceInfo("Timeframe prohibited. Waiting for null response: " + timeFrame.ToString());

            string secName = security.Name;
            string secClass = security.NameClass;

            TimeFrameBuilder builder = GetTfBuilder(security, timeFrame);

            List<Candle> candles = null;

            try
            {
                candles = Server.GetCandleDataToSecurity(secName, secClass, builder, startTime, endTime, startTime, false);
            }
            catch (Exception ex)
            {
                SetNewError("Error 11. Exception on server request. " + timeFrame.ToString() + "\n" + ex.ToString());
                return;
            }

            if (candles != null)
            {
                SetNewError("Error 12. The array of forbidden data is not equal to null. " + timeFrame.ToString());
                return;
            }
        }

        private void CheckTradeNullData(TimeFrame timeFrame, Security security, DateTime startTime, DateTime endTime)
        {
            SetNewServiceInfo("Timeframe prohibited. Waiting for null response: " + timeFrame.ToString());

            string secName = security.Name;
            string secClass = security.NameClass;

            List<Trade> trades = null;

            try
            {
                trades = Server.GetTickDataToSecurity(secName, secClass, startTime, endTime, startTime, false);
            }
            catch (Exception ex)
            {
                SetNewError("Error 13. Exception on server request. " + timeFrame.ToString() + "\n" + ex.ToString());
                return;
            }

            if (trades != null)
            {
                SetNewError("Error 14. Null in array. " + timeFrame.ToString());
                return;
            }
        }

        private TimeFrameBuilder GetTfBuilder(Security security, TimeFrame timeFrame)
        {
            TimeFrameBuilder timeFrameBuilder = new TimeFrameBuilder(StartProgram.IsOsTrader);
            timeFrameBuilder.TimeFrame = timeFrame;

            return timeFrameBuilder;
        }
    }
}