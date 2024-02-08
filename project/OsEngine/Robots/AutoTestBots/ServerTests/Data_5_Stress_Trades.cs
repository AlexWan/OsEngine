using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class Data_5_Stress_Trades : AServerTester
    {
        public string SecNames;

        public string SecClass;

        public string SecuritiesSeparator = "_";

        public override void Process()
        {
            if(string.IsNullOrEmpty(SecuritiesSeparator))
            {
                SetNewError("Error -1. Securities separator is null or empty");
                TestEnded();
                return;
            }

            IServerPermission serverPermission = ServerMaster.GetServerPermission(_myServer.ServerType);

            if (serverPermission == null)
            {
                SetNewError("Error. No server permission.");
                TestEnded();
                return;
            }

            if (serverPermission.DataFeedTfTickCanLoad == false)
            {
                SetNewServiceInfo("No permission. Server can`t download trades. Test over");
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
            string[] secInArray = securitiesInStr.Split('_');

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
                    if (curSec == secInArray[j] &&
                        curClass == SecClass)
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
            //3.Скачать по 2 инструментам трейды. За последние 10 дней.Просмотреть входящие в сет данные.Проверить время старта и конца данных.
            //4.Скачать трейды по 2 инструментам за 10 дней три месяца назад. Просмотреть входящие в сет данные.Проверить время старта и конца данных.

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


            // трейды за последние 10 дней

            if (permission.DataFeedTfTickCanLoad)
            {
                DateTime startTime = lastMidnightTime.AddDays(-10);
                DateTime endTime = lastMidnightTime.AddDays(-1).AddHours(-1);

                SetNewServiceInfo("Start time trades: " + startTime.ToString());
                SetNewServiceInfo("End time trades: " + endTime.ToString());

                CheckTradeData(TimeFrame.Tick, security, startTime, endTime);
            }

            // трейды за последние 10 дней 20 дней назад

            if (permission.DataFeedTfTickCanLoad)
            {
                DateTime startTime = lastMidnightTime.AddDays(-20);
                DateTime endTime = lastMidnightTime.AddDays(-1).AddHours(-1).AddDays(-10);

                SetNewServiceInfo("Start time trades: " + startTime.ToString());
                SetNewServiceInfo("End time trades: " + endTime.ToString());

                CheckTradeData(TimeFrame.Tick, security, startTime, endTime);
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
                SetNewError("Error 3. Exception on server request. " + timeFrame.ToString() + "\n" + ex.ToString());
                return;
            }

            if (trades == null)
            {
                SetNewError("Error 4. Null in array. " + timeFrame.ToString());
                return;
            }

            if (trades.Count == 0)
            {
                SetNewError("Error 5. Zero count elements in array. " + timeFrame.ToString());
                return;
            }

            DateTime startTimeReal = trades[0].Time;
            DateTime endTimeReal = trades[trades.Count - 1].Time;

            if (startTime.Date != startTimeReal.Date ||
                endTime.Date != endTimeReal.Date)
            {
                SetNewError("Error 6. Time Start on time End in real data is wrong. Tf " + timeFrame.ToString() + "\n"
                    + " StartTimeReal: " + startTimeReal.ToString()
                    + " EndTimeReal: " + endTimeReal.ToString());
            }
        }

    
    }
}
