using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class Data_2_Validation_Candles : AServerTester
    {
        public string SecName;

        public string SecClass;

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
            //7.2.Странные запросы
            //7.2.1.Не падать / зависать если запрашивают очень старые данные. И данные из будущего.
            //7.2.2.Время старта больше время конца.
            //7.2.3.Актуальное время больше конца


            DateTime lastMidnightTime = DateTime.Now;

            while (lastMidnightTime.Hour != 0)
            {
                lastMidnightTime = lastMidnightTime.AddMinutes(-1);
            }

            while (lastMidnightTime.Minute != 1)
            {
                lastMidnightTime = lastMidnightTime.AddMinutes(-1);
            }

            while (lastMidnightTime.Second != 1)
            {
                lastMidnightTime = lastMidnightTime.AddSeconds(-1);
            }

            IServerPermission permission = ServerMaster.GetServerPermission(Server.ServerType);

            if (permission == null)
            {
                SetNewError("Error 2. No server permission to server. Type: " + Server.ServerType.ToString());
                return;
            }

            SetNewServiceInfo("Security to load: " + security.Name);

            // шлём странные запросы, ожидая в ответ null или пустые массивы

            CheckOldData(permission, security, lastMidnightTime);
            CheckFutureData(permission, security, lastMidnightTime);
            CheckStartFakeData(permission, security, lastMidnightTime);
            CheckActualFakeData(permission, security, lastMidnightTime);

            // запрашиваем данные и проверяем свечи на задвоенность и прочие странности

            ValidateCandles(permission, security, lastMidnightTime);
        }

        // странные вопросы

        private void CheckActualFakeData(IServerPermission permission, Security security, DateTime lastMidnightTime)
        {
            //7.2.2.Время старта больше время конца.
            DateTime startTime = lastMidnightTime.AddDays(-3);
            DateTime endTime = lastMidnightTime.AddDays(-1).AddHours(-1);
            DateTime actualTime = endTime.AddDays(50);

            SetNewServiceInfo("Fake actualTime. Start time: " + startTime.ToString());
            SetNewServiceInfo("Fake actualTime. End time: " + endTime.ToString());
            SetNewServiceInfo("Fake actualTime. Actual time: " + endTime.AddDays(1).ToString());

            if (permission.DataFeedTf1MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min1, security, startTime, endTime, actualTime, "FakeActualDate");
            }
            if (permission.DataFeedTf2MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min2, security, startTime, endTime, actualTime, "FakeActualDate");
            }
            if (permission.DataFeedTf5MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min5, security, startTime, endTime, actualTime, "FakeActualDate");
            }
            if (permission.DataFeedTf10MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min10, security, startTime, endTime, actualTime, "FakeActualDate");
            }
            if (permission.DataFeedTf15MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min15, security, startTime, endTime, actualTime, "FakeActualDate");
            }
            if (permission.DataFeedTf30MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min30, security, startTime, endTime, actualTime, "FakeActualDate");
            }
            if (permission.DataFeedTf1HourCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Hour1, security, startTime, endTime, actualTime, "FakeActualDate");
            }
            if (permission.DataFeedTf2HourCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Hour2, security, startTime, endTime, actualTime, "FakeActualDate");
            }
            if (permission.DataFeedTf4HourCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Hour4, security, startTime, endTime, actualTime, "FakeActualDate");
            }
        }

        private void CheckStartFakeData(IServerPermission permission, Security security, DateTime lastMidnightTime)
        {
            //7.2.2.Время старта больше время конца.
            DateTime startTime = lastMidnightTime.AddDays(-3).AddMonths(1);
            DateTime endTime = lastMidnightTime.AddDays(-1).AddHours(-1).AddMonths(-1);

            SetNewServiceInfo("Fake startTime. Start time: " + startTime.ToString());
            SetNewServiceInfo("Fake startTime. End time: " + endTime.ToString());

            if (permission.DataFeedTf1MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min1, security, startTime, endTime, startTime, "FakeStartTime");
            }
            if (permission.DataFeedTf2MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min2, security, startTime, endTime, startTime, "FakeStartTime");
            }
            if (permission.DataFeedTf5MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min5, security, startTime, endTime, startTime, "FakeStartTime");
            }
            if (permission.DataFeedTf10MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min10, security, startTime, endTime, startTime, "FakeStartTime");
            }
            if (permission.DataFeedTf15MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min15, security, startTime, endTime, startTime, "FakeStartTime");
            }
            if (permission.DataFeedTf30MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min30, security, startTime, endTime, startTime, "FakeStartTime");
            }
            if (permission.DataFeedTf1HourCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Hour1, security, startTime, endTime, startTime, "FakeStartTime");
            }
            if (permission.DataFeedTf2HourCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Hour2, security, startTime, endTime, startTime, "FakeStartTime");
            }
            if (permission.DataFeedTf4HourCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Hour4, security, startTime, endTime, startTime, "FakeStartTime");
            }
        }

        private void CheckFutureData(IServerPermission permission, Security security, DateTime lastMidnightTime)
        {
            DateTime startTime = lastMidnightTime.AddDays(-3).AddYears(20);
            DateTime endTime = lastMidnightTime.AddDays(-1).AddHours(-1).AddYears(20);

            SetNewServiceInfo("Future Start time: " + startTime.ToString());
            SetNewServiceInfo("Future End time: " + endTime.ToString());

            if (permission.DataFeedTf1MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min1, security, startTime, endTime, startTime, "FutureDate");
            }
            if (permission.DataFeedTf2MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min2, security, startTime, endTime, startTime, "FutureDate");
            }
            if (permission.DataFeedTf5MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min5, security, startTime, endTime, startTime, "FutureDate");
            }
            if (permission.DataFeedTf10MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min10, security, startTime, endTime, startTime, "FutureDate");
            }
            if (permission.DataFeedTf15MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min15, security, startTime, endTime, startTime, "FutureDate");
            }
            if (permission.DataFeedTf30MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min30, security, startTime, endTime, startTime, "FutureDate");
            }
            if (permission.DataFeedTf1HourCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Hour1, security, startTime, endTime, startTime, "FutureDate");
            }
            if (permission.DataFeedTf2HourCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Hour2, security, startTime, endTime, startTime, "FutureDate");
            }
            if (permission.DataFeedTf4HourCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Hour4, security, startTime, endTime, startTime, "FutureDate");
            }
        }

        private void CheckOldData(IServerPermission permission, Security security, DateTime lastMidnightTime)
        {
            DateTime startTime = lastMidnightTime.AddDays(-3).AddYears(-20);
            DateTime endTime = lastMidnightTime.AddDays(-1).AddHours(-1).AddYears(-20);

            SetNewServiceInfo("Old Start time: " + startTime.ToString());
            SetNewServiceInfo("Old End time: " + endTime.ToString());

            if (permission.DataFeedTf1MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min1, security, startTime, endTime, startTime, "oldDate");
            }
            if (permission.DataFeedTf2MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min2, security, startTime, endTime, startTime, "oldDate");
            }
            if (permission.DataFeedTf5MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min5, security, startTime, endTime, startTime, "oldDate");
            }
            if (permission.DataFeedTf10MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min10, security, startTime, endTime, startTime, "oldDate");
            }
            if (permission.DataFeedTf15MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min15, security, startTime, endTime, startTime, "oldDate");
            }
            if (permission.DataFeedTf30MinuteCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Min30, security, startTime, endTime, startTime, "oldDate");
            }
            if (permission.DataFeedTf1HourCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Hour1, security, startTime, endTime, startTime, "oldDate");
            }
            if (permission.DataFeedTf2HourCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Hour2, security, startTime, endTime, startTime, "oldDate");
            }
            if (permission.DataFeedTf4HourCanLoad)
            {
                CheckCandleFakeData(TimeFrame.Hour4, security, startTime, endTime, startTime, "oldDate");
            }
        }

        private void CheckCandleFakeData(TimeFrame timeFrame, Security security, DateTime startTime, DateTime endTime, DateTime actualTime, string typeRequest)
        {
            //SetNewServiceInfo("Type request: " +  typeRequest + ". Tf checked: " + timeFrame.ToString());

            string secName = security.Name;
            string secClass = security.NameClass;

            TimeFrameBuilder builder = GetTfBuilder(security, timeFrame);

            List<Candle> candles = null;

            try
            {
                candles = Server.GetCandleDataToSecurity(secName, secClass, builder, startTime, endTime, actualTime, false);
            }
            catch (Exception ex)
            {
                SetNewError("RequestType: " + typeRequest + ". Error 3. Exception on server request. " + timeFrame.ToString() + "\n" + ex.ToString());
                return;
            }

            if (candles != null
                && candles.Count != 0)
            {
                SetNewError("RequestType: " + typeRequest + ".  Error 4. Array is note null. " + timeFrame.ToString());
                return;
            }
        }

        private TimeFrameBuilder GetTfBuilder(Security security, TimeFrame timeFrame)
        {
            TimeFrameBuilder timeFrameBuilder = new TimeFrameBuilder(StartProgram.IsOsTrader);
            timeFrameBuilder.TimeFrame = timeFrame;

            return timeFrameBuilder;
        }

        // валидация свечек

        private void ValidateCandles(IServerPermission permission, Security security, DateTime lastMidnightTime)
        {
            DateTime startTime = lastMidnightTime.AddDays(-30);
            DateTime endTime = lastMidnightTime.AddDays(-1);

            SetNewServiceInfo("Validation Candles startTime: " + startTime.ToString());
            SetNewServiceInfo("Validation Candles endTime: " + endTime.ToString());

            if (permission.DataFeedTf1MinuteCanLoad)
            {
                List<Candle> candles = GetCandles(TimeFrame.Min1, security, startTime, endTime);
                ValidateCandles(candles, TimeFrame.Min1);
            }
            if (permission.DataFeedTf2MinuteCanLoad)
            {
                List<Candle> candles = GetCandles(TimeFrame.Min2, security, startTime, endTime);
                ValidateCandles(candles, TimeFrame.Min2);
            }
            if (permission.DataFeedTf5MinuteCanLoad)
            {
                List<Candle> candles = GetCandles(TimeFrame.Min5, security, startTime, endTime);
                ValidateCandles(candles, TimeFrame.Min5);
            }
            if (permission.DataFeedTf10MinuteCanLoad)
            {
                List<Candle> candles = GetCandles(TimeFrame.Min10, security, startTime, endTime);
                ValidateCandles(candles, TimeFrame.Min10);
            }
            if (permission.DataFeedTf15MinuteCanLoad)
            {
                List<Candle> candles = GetCandles(TimeFrame.Min15, security, startTime, endTime);
                ValidateCandles(candles, TimeFrame.Min15);
            }
            if (permission.DataFeedTf30MinuteCanLoad)
            {
                List<Candle> candles = GetCandles(TimeFrame.Min30, security, startTime, endTime);
                ValidateCandles(candles, TimeFrame.Min30);
            }
            if (permission.DataFeedTf1HourCanLoad)
            {
                List<Candle> candles = GetCandles(TimeFrame.Hour1, security, startTime, endTime);
                ValidateCandles(candles, TimeFrame.Hour1);
            }
            if (permission.DataFeedTf2HourCanLoad)
            {
                List<Candle> candles = GetCandles(TimeFrame.Hour2, security, startTime, endTime);
                ValidateCandles(candles, TimeFrame.Hour2);
            }
            if (permission.DataFeedTf4HourCanLoad)
            {
                List<Candle> candles = GetCandles(TimeFrame.Hour4, security, startTime, endTime);
                ValidateCandles(candles, TimeFrame.Hour4);
            }
        }

        private List<Candle> GetCandles(TimeFrame timeFrame, Security security, DateTime startTime, DateTime endTime)
        {
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
                SetNewError("Error 5. Exception on server request. " + timeFrame.ToString() + "\n" + ex.ToString());
                return null;
            }

            return candles;
        }

        private void ValidateCandles(List<Candle> candles, TimeFrame timeFrame)
        {
            // 1 null быть не должно. Это должны быть свечи

            if (candles == null)
            {
                SetNewError("Error 6. Array is null. " + timeFrame.ToString());
                return;
            }

            if (candles.Count == 0)
            {
                SetNewError("Error 7. Array is empty. " + timeFrame.ToString());
                return;
            }

            // 2 правильно ли расположено время в массиве. Сначала - старые данные. К концу массива - новые.
            // 3 нет ли задвоения свечек

            for (int i = 1; i < candles.Count; i++)
            {
                Candle candleNow = candles[i];
                Candle candleLast = candles[i - 1];

                if (candleLast.TimeStart > candleNow.TimeStart)
                {
                    SetNewError("Error 8. The time in the old candle is big than in the current candle " + timeFrame.ToString());
                    return;
                }

                if (candleLast.TimeStart == candleNow.TimeStart)
                {
                    SetNewError("Error 9. Candle time is equal!" + timeFrame.ToString());
                    return;
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
                    SetNewError("Error 10. Candle open above the high" + timeFrame.ToString());
                    return;
                }
                if (candleNow.Open < candleNow.Low)
                {
                    SetNewError("Error 11. Candle open below the low" + timeFrame.ToString());
                    return;
                }

                if (candleNow.Close > candleNow.High)
                {
                    SetNewError("Error 12. Candle Close above the high" + timeFrame.ToString());
                    return;
                }
                if (candleNow.Close < candleNow.Low)
                {
                    SetNewError("Error 13. Candle Close below the low" + timeFrame.ToString());
                    return;
                }

                if (candleNow.Open == 0)
                {
                    SetNewError("Error 14. Candle Open is zero" + timeFrame.ToString());
                    return;
                }

                if (candleNow.High == 0)
                {
                    SetNewError("Error 15. Candle High is zero" + timeFrame.ToString());
                    return;
                }

                if (candleNow.Low == 0)
                {
                    SetNewError("Error 16. Candle Low is zero" + timeFrame.ToString());
                    return;
                }

                if (candleNow.Close == 0)
                {
                    SetNewError("Error 17. Candle Close is zero" + timeFrame.ToString());
                    return;
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
                    SetNewError("Error 18. Candle Volume is zero" + timeFrame.ToString());
                    return;
                }

            }

            // 6 правильное ли расстояние между свечками по времени, учитывая данный ТФ

            TimeSpan goodTimeSpan = GetTimeSpanFromTimeFrame(timeFrame);

            for (int i = 1; i < candles.Count; i++)
            {
                Candle candleNow = candles[i];
                Candle candleLast = candles[i - 1];

                if (candleNow.TimeStart.Date != candleLast.TimeStart.Date)
                {
                    continue;
                }

                TimeSpan span = candleNow.TimeStart - candleLast.TimeStart;

                if (span != goodTimeSpan)
                {

                    SetNewError(
                        "Error 19. The time distance between the candles is wrong. TimeFrame: " + timeFrame.ToString() +
                        " Good distance: " + goodTimeSpan.ToString() +
                        " Real distance: " + span.ToString() +
                        " CandleTime: " + candleNow.TimeStart.ToString());
                    return;

                }
            }
        }

        private TimeSpan GetTimeSpanFromTimeFrame(TimeFrame timeFrame)
        {
            TimeSpan timeFrameSpan = new TimeSpan();

            if (timeFrame == TimeFrame.Min1)
            {
                timeFrameSpan = new TimeSpan(0, 0, 1, 0);
            }
            else if (timeFrame == TimeFrame.Min2)
            {
                timeFrameSpan = new TimeSpan(0, 0, 2, 0);
            }
            else if (timeFrame == TimeFrame.Min3)
            {
                timeFrameSpan = new TimeSpan(0, 0, 3, 0);
            }
            else if (timeFrame == TimeFrame.Min5)
            {
                timeFrameSpan = new TimeSpan(0, 0, 5, 0);
            }
            else if (timeFrame == TimeFrame.Min10)
            {
                timeFrameSpan = new TimeSpan(0, 0, 10, 0);
            }
            else if (timeFrame == TimeFrame.Min15)
            {
                timeFrameSpan = new TimeSpan(0, 0, 15, 0);
            }
            else if (timeFrame == TimeFrame.Min20)
            {
                timeFrameSpan = new TimeSpan(0, 0, 20, 0);
            }
            else if (timeFrame == TimeFrame.Min30)
            {
                timeFrameSpan = new TimeSpan(0, 0, 30, 0);
            }
            else if (timeFrame == TimeFrame.Min45)
            {
                timeFrameSpan = new TimeSpan(0, 0, 45, 0);
            }
            else if (timeFrame == TimeFrame.Hour1)
            {
                timeFrameSpan = new TimeSpan(0, 1, 0, 0);
            }
            else if (timeFrame == TimeFrame.Hour2)
            {
                timeFrameSpan = new TimeSpan(0, 2, 0, 0);
            }
            else if (timeFrame == TimeFrame.Hour4)
            {
                timeFrameSpan = new TimeSpan(0, 4, 0, 0);
            }
            else if (timeFrame == TimeFrame.Day)
            {
                timeFrameSpan = new TimeSpan(0, 24, 0, 0);
            }
            else if (timeFrame == TimeFrame.Sec1)
            {
                timeFrameSpan = new TimeSpan(0, 0, 0, 1);
            }
            else if (timeFrame == TimeFrame.Sec2)
            {
                timeFrameSpan = new TimeSpan(0, 0, 0, 2);
            }
            else if (timeFrame == TimeFrame.Sec5)
            {
                timeFrameSpan = new TimeSpan(0, 0, 0, 5);
            }
            else if (timeFrame == TimeFrame.Sec10)
            {
                timeFrameSpan = new TimeSpan(0, 0, 0, 10);
            }
            else if (timeFrame == TimeFrame.Sec15)
            {
                timeFrameSpan = new TimeSpan(0, 0, 0, 15);
            }
            else if (timeFrame == TimeFrame.Sec20)
            {
                timeFrameSpan = new TimeSpan(0, 0, 0, 20);
            }
            else if (timeFrame == TimeFrame.Sec30)
            {
                timeFrameSpan = new TimeSpan(0, 0, 0, 30);
            }

            return timeFrameSpan;
        }
    }
}