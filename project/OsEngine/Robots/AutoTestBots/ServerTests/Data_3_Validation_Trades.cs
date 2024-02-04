using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class Data_3_Validation_Trades : AServerTester
    {
        public string SecName;

        public string SecClass;

        public override void Process()
        {
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


            // запрашиваем данные и проверяем трейды на всякие странности

            ValidateTrades(permission, security, lastMidnightTime);

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

            if (permission.DataFeedTfTickCanLoad)
            {
                CheckTradesFakeData(TimeFrame.Tick, security, startTime, endTime, actualTime, "FakeActualDate");
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
                CheckTradesFakeData(TimeFrame.Tick, security, startTime, endTime, startTime, "FakeStartTime");
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
                CheckTradesFakeData(TimeFrame.Tick, security, startTime, endTime, startTime, "FutureDate");
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
                CheckTradesFakeData(TimeFrame.Tick, security, startTime, endTime, startTime, "oldDate");
            }
        }

        private void CheckTradesFakeData(TimeFrame timeFrame, Security security, DateTime startTime, DateTime endTime, DateTime actualTime, string typeRequest)
        {
            //SetNewServiceInfo("Type request: " + typeRequest + ". Tf checked: " + timeFrame.ToString());

            string secName = security.Name;
            string secClass = security.NameClass;

            List<Trade> trades = null;

            try
            {
                trades = Server.GetTickDataToSecurity(secName, secClass, startTime, endTime, actualTime, false);
            }
            catch (Exception ex)
            {
                SetNewError("RequestType: " + typeRequest + ". Error 3. Exception on server request. " + timeFrame.ToString() + "\n" + ex.ToString());
                return;
            }

            if (trades != null)
            {
                SetNewError("RequestType: " + typeRequest + ".  Error 4. Array is note null. " + timeFrame.ToString());
                return;
            }
        }

        // валидация трейдов

        private void ValidateTrades(IServerPermission permission, Security security, DateTime lastMidnightTime)
        {
            DateTime startTime = lastMidnightTime.AddDays(-15);
            DateTime endTime = lastMidnightTime.AddDays(-1);

            SetNewServiceInfo("Validation Trades startTime: " + startTime.ToString());
            SetNewServiceInfo("Validation Trades endTime: " + endTime.ToString());

            string secName = security.Name;
            string secClass = security.NameClass;

            List<Trade> trades = null;

            try
            {
                trades = Server.GetTickDataToSecurity(secName, secClass, startTime, endTime, startTime, false);
            }
            catch (Exception ex)
            {
                SetNewError("Error 5. Trades validate. Exception on server request. " + ex.ToString());
                return;
            }

            // 1 null быть не должно

            if (trades == null)
            {
                SetNewError("Error 6. Array is null. Trades");
                return;
            }

            if (trades.Count == 0)
            {
                SetNewError("Error 7. Array is empty. Trades");
                return;
            }

            // 2 правильно ли расположено время в массиве. Сначала - старые данные. К концу массива - новые.
            // 3 нет ли задвоения трейдов


            for (int i = 1; i < trades.Count; i++)
            {
                Trade tradeNow = trades[i];
                Trade tradeLast = trades[i - 1];

                if (tradeLast.Time > tradeNow.Time)
                {
                    SetNewError("Error 8. The time in the old trade is big than in the current trade ");
                    return;
                }

                if (tradeLast.Time == tradeNow.Time)
                {
                    SetNewError("Error 9. Trades time is equal!");
                    return;
                }
            }

            for (int i = 0; i < trades.Count; i++)
            {
                Trade tradeNow = trades[i];

                if (tradeNow.Price == 0)
                {
                    SetNewError("Error 10. Trade Price is zero");
                    return;
                }
                if (tradeNow.Volume == 0)
                {
                    SetNewError("Error 11. Trade Volume is zero");
                    return;
                }
                if (tradeNow.Side == Side.None)
                {
                    SetNewError("Error 12. Trade Side is None");
                    return;
                }
            }
        }
    }
}