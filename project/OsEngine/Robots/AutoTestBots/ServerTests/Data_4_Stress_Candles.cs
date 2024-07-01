using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class Data_4_Stress_Candles : AServerTester
    {
        public string SecNames;

        public string SecClass;

        public string SecuritiesSeparator = "_";

        public override void Process()
        {
            if (string.IsNullOrEmpty(SecuritiesSeparator))
            {
                SetNewError("Error -1. Securities separator is null or empty ");
                TestEnded();
                return;
            }

            List<Security> securities = Server.Securities;

            if (securities != null &&
                securities.Count > 0)
            {
                List<Security> securitiesActivated = GetActivateSecurities(SecNames);

                if (securitiesActivated == null ||
                    securitiesActivated.Count == 0)
                {
                    SetNewError("Error 0. Security set user is not found " + SecNames);
                    TestEnded();
                    return;
                }

                for (int i = 0; i < securitiesActivated.Count; i++)
                {
                    StartTestSecurity(securitiesActivated[i]);
                }
            }
            else
            {
                SetNewError("Error 1. No securities in server found");
            }

            TestEnded();
        }

        private List<Security> GetActivateSecurities(string securitiesInStr)
        {
            string[] secInArray = securitiesInStr.Split(SecuritiesSeparator[0]);

            List<Security> securitiesFromServer = Server.Securities;

            if (secInArray.Length == 0)
            {
                return null;
            }
            if (securitiesFromServer == null)
            {
                return null;
            }

            List<Security> securitiesActivated = new List<Security>();

            for (int i = 0; i < securitiesFromServer.Count; i++)
            {
                string curSec = securitiesFromServer[i].Name;

                string curClass = securitiesFromServer[i].NameClass;

                for (int j = 0; j < secInArray.Length; j++)
                {
                    if (curSec == secInArray[j]
                        && curClass == SecClass)
                    {
                        securitiesActivated.Add(securitiesFromServer[i]);
                        break;
                    }
                }
            }

            return securitiesActivated;
        }

        private void StartTestSecurity(Security security)
        {
            //1.Скачать по N инструментам все имеющиеся свечи за 1 год.Просмотреть входящие в сет данные.Проверить время старта и конца данных.
            //2.Скачать по N инструментам все имеющиеся свечи за ПРОШЛЫЙ год.Просмотреть входящие в сет данные.Проверить время старта и конца данных.

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

            SetNewServiceInfo("Security to load: " + security.Name);

            IServerPermission permission = ServerMaster.GetServerPermission(Server.ServerType);

            if (permission == null)
            {
                SetNewError("Error 2. No server permission to server. Type: " + Server.ServerType.ToString());
                return;
            }

            // свечки за прошлый год

            DateTime startTime = lastMidnightTime.AddDays(-365);
            DateTime endTime = lastMidnightTime.AddDays(-1).AddHours(-1);

            CheckCandlesToSeciruty(security, startTime, endTime, permission);

            // свечки за год назад

            startTime = lastMidnightTime.AddDays(-730);
            endTime = lastMidnightTime.AddDays(-1).AddHours(-1).AddDays(-365);

            CheckCandlesToSeciruty(security, startTime, endTime, permission);
        }

        private void CheckCandlesToSeciruty(Security security, DateTime startTime, DateTime endTime, IServerPermission permission)
        {
            SetNewServiceInfo("Start time candle: " + startTime.ToString());
            SetNewServiceInfo("End time candle: " + endTime.ToString());

            if (permission.DataFeedTf1MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min1, security, startTime, endTime);
            }
            if (permission.DataFeedTf2MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min2, security, startTime, endTime);
            }
            if (permission.DataFeedTf5MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min5, security, startTime, endTime);
            }
            if (permission.DataFeedTf10MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min10, security, startTime, endTime);
            }
            if (permission.DataFeedTf15MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min15, security, startTime, endTime);
            }
            if (permission.DataFeedTf30MinuteCanLoad)
            {
                CheckCandleData(TimeFrame.Min30, security, startTime, endTime);
            }
            if (permission.DataFeedTf1HourCanLoad)
            {
                CheckCandleData(TimeFrame.Hour1, security, startTime, endTime);
            }
            if (permission.DataFeedTf2HourCanLoad)
            {
                CheckCandleData(TimeFrame.Hour2, security, startTime, endTime);
            }
            if (permission.DataFeedTf4HourCanLoad)
            {
                CheckCandleData(TimeFrame.Hour4, security, startTime, endTime);
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

            DateTime startTimeReal = candles[0].TimeStart;
            DateTime endTimeReal = candles[candles.Count - 1].TimeStart;

            if (endTime.Date != endTimeReal.Date)
            {
                SetNewError("Error 6. Time End problem. In real data is wrong. Tf " + timeFrame.ToString() + "\n"
                + " EndTimeReal: " + endTimeReal.ToString()
                + " EndTimeSend: " + endTime.ToString());
            }

            if (startTime.Date != startTimeReal.Date)
            {
                SetNewError("Error 7. Time Start problem. In real data is wrong. Tf " + timeFrame.ToString() + "\n"
                + " StartTimeReal: " + startTimeReal.ToString()
                + " StartTimeSend: " + startTime.ToString());
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