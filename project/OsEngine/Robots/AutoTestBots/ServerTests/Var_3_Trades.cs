using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Threading;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class Var_3_Trades : AServerTester
    {
        public string SecNames;

        public string SecClassCode;

        public int MinutesToTest;

        public string SecuritiesSeparator = "_";

        private List<Security> _secToSubscrible = new List<Security>();

        public override void Process()
        {
            if (SecuritiesSeparator == null ||
                SecuritiesSeparator.Length == 0)
            {
                SetNewError("Error -1. Security separator is null or empty" + SecNames);
                TestEnded();
                return;
            }

            List<Security> securities = GetActivateSecurities(SecNames, SecClassCode);

            if (securities == null ||
                securities.Count == 0 ||
                securities.Count < 5)
            {
                SetNewError("Error 0. Security set user is not found, or securities count < 5 " + SecNames);
                TestEnded();
                return;
            }

            Server.ServerRealization.NewTradesEvent += ServerRealization_NewTradesEvent;

            for (int i = 0; i < securities.Count; i++)
            {
                Server.ServerRealization.Subscrible(securities[i]);
                _secToSubscrible.Add(securities[i]);
                _securities.Add(securities[i]);
                SetNewServiceInfo("Start sec: " + securities[i].Name);
            }


            DateTime timeEndTest = DateTime.Now.AddMinutes(MinutesToTest);

            while (timeEndTest > DateTime.Now)
            {
                Thread.Sleep(1000);
            }
            Server.ServerRealization.NewTradesEvent -= ServerRealization_NewTradesEvent;

            SetNewServiceInfo("trades сount analyzed: " + _tradesCount);
            SetNewServiceInfo("test time minutes: " + MinutesToTest);

            for (int i = 0; i < _secToSubscrible.Count; i++)
            {
                SetNewError("Trades Error 1. No trades to Security: " + _secToSubscrible[i].Name);
            }

            TestEnded();
        }

        private List<Security> GetActivateSecurities(string securitiesInStr, string classCode)
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
                    if (curSec == secInArray[j] &&
                        curClass == classCode)
                    {
                        securitiesActivated.Add(securitiesFromServer[i]);
                        break;
                    }
                }
            }

            return securitiesActivated;
        }

        List<Security> _securities = new List<Security>();

        int _tradesCount = 0;

        List<List<Trade>> _trades = new List<List<Trade>>();

        private void ServerRealization_NewTradesEvent(Trade newTrade)
        {
            _tradesCount++;
            /*  Требования к данным Trade
             *  
             *  1. Последовательность - нельзя высылать устаревшие данные. 
             *  2. Время трейдов не должно совпадать 
             *  3. ID трейдов не должно быть null
             *  4. Цена трейда не должна быть 0
             *  5. Объём трейда не должен быть 0
             *  6. У трейда должна быть сторона. Sell / Buy
             *  7. У трейда обязательно должно быть имя бумаги
             *  8. 
            */

            if(string.IsNullOrEmpty(newTrade.SecurityNameCode))
            {
                SetNewError("Trades Error 2. No Security Name");
                return;
            }

            // Убираем бумагу из списка бумаг по которым трейды вообще не пришли.
            for (int i = 0; i < _secToSubscrible.Count; i++)
            {
                if (_secToSubscrible[i].Name == newTrade.SecurityNameCode)
                {
                    _secToSubscrible.RemoveAt(i);
                    break;
                }
            }

            if (newTrade.Side == Side.None)
            {
                SetNewError("Trades Error 3. No Trade SIDE. Sec name " + newTrade.SecurityNameCode);
                return;
            }

            if (string.IsNullOrEmpty(newTrade.Id))
            {
                SetNewError("Trades Error 4. No Trade Id. Sec name " + newTrade.SecurityNameCode);
                return;
            }

            if (newTrade.Price <= 0)
            {
                SetNewError("Trades Error 5. Bad Trade Price. Sec name " 
                    + newTrade.SecurityNameCode
                    + " Price: " + newTrade.Price);
                return;
            }

            if (newTrade.Volume <= 0)
            {
                SetNewError("Trades Error 6. Bad Trade Volume. Sec name " 
                    + newTrade.SecurityNameCode
                    + " Volume: " + newTrade.Volume);
                return;
            }

            if (newTrade.Time == DateTime.MinValue)
            {
                SetNewError("Trades Error 7. Bad Trade TIME. Sec name "
                    + newTrade.SecurityNameCode);
                return;
            }

            // проверяем последние данные по этой бумаге

            Trade previousTrade = null;

            for(int i = 0;i < _trades.Count;i++)
            {
                if (_trades[i][0].SecurityNameCode == newTrade.SecurityNameCode)
                {
                    previousTrade = _trades[i][0];
                    break;
                }
            }

            if(previousTrade != null)
            {
                if (previousTrade.Time > newTrade.Time)
                {
                    SetNewError("Trades Error 8. Previous trade time is greater than the current time. Sec name "
                        + newTrade.SecurityNameCode);
                    return;
                }

                for (int i = 0; i < _trades.Count; i++)
                {
                    if (_trades[i][0].SecurityNameCode == newTrade.SecurityNameCode)
                    {
                        _trades[i].Add(newTrade);
                        break;
                    }
                }

            }
            else
            {
                _trades.Add(new List<Trade> { newTrade });
            }
        }
    }
}