using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Threading;


namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class Var_2_MarketDepth : AServerTester
    {
        public string SecNames;

        public string SecClassCode;

        public int MinutesToTest;

        public string SecuritiesSeparator = "_";

        private List<Security> _secToSubscrible = new List<Security>();

        public override void Process()
        {
            if(SecuritiesSeparator == null ||
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

            Server.NewMarketDepthEvent += Server_NewMarketDepthEvent;

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
            Server.NewMarketDepthEvent -= Server_NewMarketDepthEvent;

            SetNewServiceInfo("md сount analyzed: " + mdCount);
            SetNewServiceInfo("test time minutes: " + MinutesToTest);

            for (int i = 0; i < _secToSubscrible.Count; i++)
            {
                SetNewError("MD Error 1. No MD to Security: " + _secToSubscrible[i].Name);
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

        int mdCount = 0;

        List<MarketDepth> _md = new List<MarketDepth>();

        private void Server_NewMarketDepthEvent(MarketDepth md)
        {
            mdCount++;
            /*  5.1.	Требования к данным MarketDepth
5.1.1.	 Главный объект стакана котировок
5.1.2.	Bid никогда не должен быть равен Ask
5.1.3.	Bid не должен быть выше Ask
5.1.4.	SecurityNameCode – обязательное поле. Не может быть равен null или содержать пустые строки. 
5.1.5.	Time – поле содержащее в себе время обновления стакана. Каждый стакан должен быть маркирован временем. Либо своим, либо временем сервера
5.1.6.	В истории стакана не должно быть больше 25 уровней
5.1.7.	Стаканы не должны вызываться без смены времени стакана. Каждый должен быть уникальным.
5.1.8.	Bids – 0 индекс самый высокий. И далее, чем больше индекс тем меньше цена
5.1.9.	Asks  – 0 индекс самый низкий. И далее, чем больше индекс тем выше цена
5.1.10.	С одинаковой ценой не может быть несколько уровней
5.1.11.	Массивы не могут содержать в ячейках null



5.2.MarketDepthLevel
5.2.1.Объект одного уровня в стакане
5.2.2.Price – обязательное поле цены
5.2.3.Bid – назначается если это покупка
5.2.4.Ask – назначается если это продажа
5.2.5.Id – не обязательное сервисное поле. Применяется в некоторых коннекторах при сборке стакана.
5.2.6. Находясь в массиве bids - Ask должен быть равен 0
5.2.7. Находясь в массиве asks - Bid должен быть равен 0
*/
            for(int i = 0;i < _secToSubscrible.Count;i++)
            {
                if (_secToSubscrible[i].Name == md.SecurityNameCode)
                {
                    _secToSubscrible.RemoveAt(i);
                    break;
                }
            }

            // Базовые проверки
            if (md.Bids == null ||
                md.Asks == null)
            {
                SetNewError("MD Error 2. null in bids or asks array");
                return;
            }
            if (md.Bids.Count == 0 ||
                md.Asks.Count == 0)
            {
                SetNewError("MD Error 3. Zero count in bids or asks array");
                return;
            }

            if (md.Bids.Count > 25 ||
                md.Asks.Count > 25)
            {
                SetNewError("MD Error 4. Count in bids or asks more 25 lines");
                return;
            }

            if (string.IsNullOrEmpty(md.SecurityNameCode))
            {
                SetNewError("MD Error 5. Security name is null or empty");
                return;
            }

            for (int i = 0; i < md.Bids.Count; i++)
            {
                if (md.Bids[i] == null)
                {
                    SetNewError("MD Error 6. Bids array have null level");
                    return;
                }
                if (md.Bids[i].Ask != 0)
                {
                    SetNewError("MD Error 7. Ask in bids array is note zero");
                    return;
                }
                if (md.Bids[i].Bid == 0)
                {
                    SetNewError("MD Error 8. Bid in bids array is zero");
                    return;
                }
            }

            for (int i = 0; i < md.Asks.Count; i++)
            {
                if (md.Asks[i] == null)
                {
                    SetNewError("MD Error 9. Asks array have null level");
                    return;
                }
                if (md.Asks[i].Bid != 0)
                {
                    SetNewError("MD Error 10. Bid in asks array is note zero");
                    return;
                }
                if (md.Asks[i].Ask == 0)
                {
                    SetNewError("MD Error 11. Ask in asks array is zero");
                    return;
                }
            }

            // проверка времени

            if (md.Time == DateTime.MinValue)
            {
                SetNewError("MD Error 12. Time is min value");
            }

            MarketDepth oldDepth = null;

            for (int i = 0; i < _md.Count; i++)
            {
                if (_md[i].SecurityNameCode == md.SecurityNameCode)
                {
                    oldDepth = _md[i];
                }
            }

            if (oldDepth != null && oldDepth.Time == md.Time)
            {
                SetNewError("MD Error 13. Time in md is note change");
            }

            bool isSaved = false;

            for (int i = 0; i < _md.Count; i++)
            {
                if (_md[i].SecurityNameCode == md.SecurityNameCode)
                {
                    _md[i] = md;
                    isSaved = true;
                    break;
                }
            }

            if (isSaved == false)
            {
                _md.Add(md);
            }

            for (int i = 1; i < md.Bids.Count; i++)
            {
                if (md.Bids[i].Price == 0)
                {
                    SetNewError("MD Error 14. Bibs[i] price == 0");
                    return;
                }
            }

            for (int i = 1; i < md.Asks.Count; i++)
            {
                if (md.Asks[i].Price == 0)
                {
                    SetNewError("MD Error 15. Asks[i] price == 0");
                    return;
                }
            }

            // проверка массивов Bids и Asks на запутанность

            if (md.Bids[0].Price >= md.Asks[0].Price)
            {
                SetNewError("MD Error 16. Bib price >= Ask price");
                return;
            }

            for (int i = 1; i < md.Bids.Count; i++)
            {
                // Bids – уровни заявок на покупку.
                // 0 индекс самый высокий.И далее, чем больше индекс тем меньше цена

                if (md.Bids[i].Price == md.Bids[i - 1].Price)
                {
                    SetNewError("MD Error 17. Bibs[i] price == Bibs[i-1] price");
                }

                if (md.Bids[i].Price > md.Bids[i - 1].Price)
                {
                    SetNewError("MD Error 18. Bibs[i] price > Bibs[i-1] price");
                }
            }

            for (int i = 1; i < md.Asks.Count; i++)
            {
                // Asks – уровни заявок на продажу.
                // 0 индекс самый низкий.И далее, чем больше индекс тем выше цена

                if (md.Asks[i].Price == md.Asks[i - 1].Price)
                {
                    SetNewError("MD Error 19. Asks[i] price == Asks[i-1] price");
                }

                if (md.Asks[i].Price < md.Asks[i - 1].Price)
                {
                    SetNewError("MD Error 20. Asks[i] price < Asks[i-1] price");
                }
            }

            for (int i = 0; i < md.Bids.Count; i++)
            {
                // 5.1.10.	С одинаковой ценой не может быть несколько уровней

                MarketDepthLevel curLevel = md.Bids[i];

                for (int j = 0; j < md.Bids.Count; j++)
                {
                    if(j == i)
                    {
                        continue;
                    }

                    if (curLevel.Price == md.Bids[j].Price)
                    {
                        SetNewError("MD Error 21. bids with same price");
                    }
                }
            }

            for (int i = 0; i < md.Asks.Count; i++)
            {
                // 5.1.10.	С одинаковой ценой не может быть несколько уровней

                MarketDepthLevel curLevel = md.Asks[i];

                for (int j = 0; j < md.Asks.Count; j++)
                {
                    if (j == i)
                    {
                        continue;
                    }

                    if (curLevel.Price == md.Asks[j].Price)
                    {
                        SetNewError("MD Error 22. Asks with same price");
                    }
                }
            }
        }
    }
}