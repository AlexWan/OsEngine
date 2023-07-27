using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    internal class Data_2 : AServerTester
    {
        public string SecName;

        public override void Process()
        {
            List<Security> securities = Server.Securities;

            if (securities != null &&
                securities.Count > 0)
            {
                Security mySecurity = null;

                for (int i = 0; i < securities.Count; i++)
                {
                    if (securities[i].Name == SecName)
                    {
                        mySecurity = securities[i];
                        break;
                    }
                }

                if (mySecurity == null)
                {
                    SetNewError("Data1. Error 0. Security set user is not found " + SecName);
                    return;
                }

                StartTestSecurity(mySecurity);
            }
            else
            {
                SetNewError("Data1. Error 1. No securities found");
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
                SetNewError("Data2. Error 0. No server permission to server. Type: " + Server.ServerType.ToString());
                return;
            }

            SetNewServiceInfo("Security to load: " + security.Name);

            // запрашиваем очень старые данные 20 лет назад.

            CheckOldData(permission, security,lastMidnightTime);
            CheckFutureData(permission, security, lastMidnightTime);
            CheckStartFakeData(permission, security, lastMidnightTime);
            CheckActualFakeData(permission, security, lastMidnightTime);
        }

        private void CheckActualFakeData(IServerPermission permission, Security security, DateTime lastMidnightTime)
        {
            //7.2.2.Время старта больше время конца.
            DateTime startTime = lastMidnightTime.AddDays(-3);
            DateTime endTime = lastMidnightTime.AddDays(-1).AddHours(-1);
            DateTime actualTime = endTime.AddDays(50);

            SetNewServiceInfo("Fake actualTime. Start time: " + startTime.ToString());
            SetNewServiceInfo("Fake actualTime. End time: " + endTime.ToString());
            SetNewServiceInfo("Fake actualTime. Actual time: " + endTime.AddDays(1).ToString());

            if (permission.DataFeedTfTickCanLoad)
            {
                CheckTradeData(TimeFrame.Tick, security, startTime, endTime, actualTime, "FakeActualDate");
            }
            if (permission.DataFeedTf1MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min1, security, startTime, endTime, actualTime, "FakeActualDate");
            }
            if (permission.DataFeedTf2MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min2, security, startTime, endTime, actualTime, "FakeActualDate");
            }
            if (permission.DataFeedTf5MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min5, security, startTime, endTime, actualTime, "FakeActualDate");
            }
            if (permission.DataFeedTf10MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min10, security, startTime, endTime, actualTime, "FakeActualDate");
            }
            if (permission.DataFeedTf15MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min15, security, startTime, endTime, actualTime, "FakeActualDate");
            }
            if (permission.DataFeedTf30MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min30, security, startTime, endTime, actualTime, "FakeActualDate");
            }
            if (permission.DataFeedTf1HourCanLoad)
            {
                CheckCandleData(TimeFrame.Hour1, security, startTime, endTime, actualTime, "FakeActualDate");
            }
            if (permission.DataFeedTf2HourCanLoad)
            {
                CheckCandleData(TimeFrame.Hour2, security, startTime, endTime, actualTime, "FakeActualDate");
            }
            if (permission.DataFeedTf4HourCanLoad)
            {
                CheckCandleData(TimeFrame.Hour4, security, startTime, endTime, actualTime, "FakeActualDate");
            }
        }

        private void CheckStartFakeData(IServerPermission permission, Security security, DateTime lastMidnightTime)
        {
            //7.2.2.Время старта больше время конца.
            DateTime startTime = lastMidnightTime.AddDays(-3).AddMonths(1);
            DateTime endTime = lastMidnightTime.AddDays(-1).AddHours(-1).AddMonths(-1);

            SetNewServiceInfo("Fake startTime. Start time: " + startTime.ToString());
            SetNewServiceInfo("Fake startTime. End time: " + endTime.ToString());

            if (permission.DataFeedTfTickCanLoad)
            {
                CheckTradeData(TimeFrame.Tick, security, startTime, endTime, startTime, "FakeStartTime");
            }
            if (permission.DataFeedTf1MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min1, security, startTime, endTime, startTime, "FakeStartTime");
            }
            if (permission.DataFeedTf2MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min2, security, startTime, endTime, startTime, "FakeStartTime");
            }
            if (permission.DataFeedTf5MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min5, security, startTime, endTime, startTime, "FakeStartTime");
            }
            if (permission.DataFeedTf10MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min10, security, startTime, endTime, startTime, "FakeStartTime");
            }
            if (permission.DataFeedTf15MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min15, security, startTime, endTime, startTime, "FakeStartTime");
            }
            if (permission.DataFeedTf30MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min30, security, startTime, endTime, startTime, "FakeStartTime");
            }
            if (permission.DataFeedTf1HourCanLoad)
            {
                CheckCandleData(TimeFrame.Hour1, security, startTime, endTime, startTime, "FakeStartTime");
            }
            if (permission.DataFeedTf2HourCanLoad)
            {
                CheckCandleData(TimeFrame.Hour2, security, startTime, endTime, startTime, "FakeStartTime");
            }
            if (permission.DataFeedTf4HourCanLoad)
            {
                CheckCandleData(TimeFrame.Hour4, security, startTime, endTime, startTime, "FakeStartTime");
            }
        }

        private void CheckFutureData(IServerPermission permission, Security security, DateTime lastMidnightTime)
        {
            DateTime startTime = lastMidnightTime.AddDays(-3).AddYears(20);
            DateTime endTime = lastMidnightTime.AddDays(-1).AddHours(-1).AddYears(20);

            SetNewServiceInfo("Future Start time: " + startTime.ToString());
            SetNewServiceInfo("Future End time: " + endTime.ToString());

            if (permission.DataFeedTfTickCanLoad)
            {
                CheckTradeData(TimeFrame.Tick, security, startTime, endTime, startTime, "FutureDate");
            }
            if (permission.DataFeedTf1MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min1, security, startTime, endTime, startTime, "FutureDate");
            }
            if (permission.DataFeedTf2MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min2, security, startTime, endTime, startTime, "FutureDate");
            }
            if (permission.DataFeedTf5MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min5, security, startTime, endTime, startTime, "FutureDate");
            }
            if (permission.DataFeedTf10MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min10, security, startTime, endTime, startTime, "FutureDate");
            }
            if (permission.DataFeedTf15MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min15, security, startTime, endTime, startTime, "FutureDate");
            }
            if (permission.DataFeedTf30MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min30, security, startTime, endTime, startTime, "FutureDate");
            }
            if (permission.DataFeedTf1HourCanLoad)
            {
                CheckCandleData(TimeFrame.Hour1, security, startTime, endTime, startTime, "FutureDate");
            }
            if (permission.DataFeedTf2HourCanLoad)
            {
                CheckCandleData(TimeFrame.Hour2, security, startTime, endTime, startTime, "FutureDate");
            }
            if (permission.DataFeedTf4HourCanLoad)
            {
                CheckCandleData(TimeFrame.Hour4, security, startTime, endTime, startTime, "FutureDate");
            }
        }

        private void CheckOldData(IServerPermission permission, Security security, DateTime lastMidnightTime)
        {
            DateTime startTime = lastMidnightTime.AddDays(-3).AddYears(-20);
            DateTime endTime = lastMidnightTime.AddDays(-1).AddHours(-1).AddYears(-20);

            SetNewServiceInfo("Old Start time: " + startTime.ToString());
            SetNewServiceInfo("Old End time: " + endTime.ToString());

            if (permission.DataFeedTfTickCanLoad)
            {
                CheckTradeData(TimeFrame.Tick, security, startTime, endTime, startTime, "oldDate");
            }
            if (permission.DataFeedTf1MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min1, security, startTime, endTime, startTime, "oldDate");
            }
            if (permission.DataFeedTf2MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min2, security, startTime, endTime, startTime, "oldDate");
            }
            if (permission.DataFeedTf5MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min5, security, startTime, endTime, startTime, "oldDate");
            }
            if (permission.DataFeedTf10MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min10, security, startTime, endTime, startTime, "oldDate");
            }
            if (permission.DataFeedTf15MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min15, security, startTime, endTime, startTime, "oldDate");
            }
            if (permission.DataFeedTf30MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min30, security, startTime, endTime, startTime, "oldDate");
            }
            if (permission.DataFeedTf1HourCanLoad)
            {
                CheckCandleData(TimeFrame.Hour1, security, startTime, endTime, startTime, "oldDate");
            }
            if (permission.DataFeedTf2HourCanLoad)
            {
                CheckCandleData(TimeFrame.Hour2, security, startTime, endTime, startTime, "oldDate");
            }
            if (permission.DataFeedTf4HourCanLoad)
            {
                CheckCandleData(TimeFrame.Hour4, security, startTime, endTime, startTime, "oldDate");
            }
        }

        private void CheckCandleData(TimeFrame timeFrame, Security security, DateTime startTime, DateTime endTime, DateTime actualTime, string typeRequest)
        {
            SetNewServiceInfo("Type request: " +  typeRequest + ". Tf checked: " + timeFrame.ToString());

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
                SetNewError("Data2. RequestType: " + typeRequest + ". Error 2. Exception on server request. " + timeFrame.ToString() + "\n" + ex.ToString());
                return;
            }

            if (candles != null)
            {
                SetNewError("Data2. RequestType: " + typeRequest + ".  Error 3. Array is note null. " + timeFrame.ToString());
                return;
            }
        }

        private void CheckTradeData(TimeFrame timeFrame, Security security, DateTime startTime, DateTime endTime, DateTime actualTime, string typeRequest)
        {
            SetNewServiceInfo("Type request: " + typeRequest + ". Tf checked: " + timeFrame.ToString());

            string secName = security.Name;
            string secClass = security.NameClass;

            List<Trade> trades = null;

            try
            {
                trades = Server.GetTickDataToSecurity(secName, secClass, startTime, endTime, actualTime, false);
            }
            catch (Exception ex)
            {
                SetNewError("Data2. RequestType: " + typeRequest + ". Error 4. Exception on server request. " + timeFrame.ToString() + "\n" + ex.ToString());
                return;
            }

            if (trades != null)
            {
                SetNewError("Data2. RequestType: " + typeRequest + ".  Error 5. Array is note null. " + timeFrame.ToString());
                return;
            }
        }

        private TimeFrameBuilder GetTfBuilder(Security security, TimeFrame timeFrame)
        {
            TimeFrameBuilder timeFrameBuilder = new TimeFrameBuilder();
            timeFrameBuilder.TimeFrame = timeFrame;

            return timeFrameBuilder;
        }
    }
}