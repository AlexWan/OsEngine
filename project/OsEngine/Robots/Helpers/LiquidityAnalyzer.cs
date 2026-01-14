/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

/*

Робот-анализатор мгновенной ликвидности

Составляет таблицу с данными за указанное время

Один инструмент - одна строка

Данные по инструменту собираются за указанное время, раз в пять секунд

Данные о том, на каком расстоянии от центра стакана можно войти в лонг и шорт указанной суммой

Данные после тесты выводятся в экстренный лог, с сортировкой бумаг по ликвидности.

*/

namespace OsEngine.Robots.Helpers
{
    [Bot("LiquidityAnalyzer")]
    public class LiquidityAnalyzer : BotPanel
    {
        private BotTabScreener _tabScreener;

        private StrategyParameterInt _minutesToAnalysis;

        private StrategyParameterDecimal _moneyToEntry;

        private StrategyParameterButton _buttonStart;

        public LiquidityAnalyzer(string name, StartProgram startProgram) : base(name, startProgram)
        {
            _tabScreener = TabCreate<BotTabScreener>();
            _minutesToAnalysis = CreateParameter("Minutes to work", 5, 1, 10, 1);
            _moneyToEntry = CreateParameter("Money to entry position", 1000000m, 10000, 100000, 10);

            _buttonStart = CreateParameterButton("Start");
            _buttonStart.UserClickOnButtonEvent += _buttonStart_UserClickOnButtonEvent;

            Description = OsLocalization.ConvertToLocString(
            "Eng:Robot for collecting statistics on instant liquidity_" +
            "Ru:Робот для сбора статистики по мгновенной ликвидности_");
        }

        private void _buttonStart_UserClickOnButtonEvent()
        {
            if(_workInProgress == true)
            {
                SendNewLogMessage(OsLocalization.ConvertToLocString(
                  "Eng:Data collection is already in progress. Wait for the previous work to finish_" +
                  "Ru:Сбор данных уже идёт. Дождитесь окончания предыдущей работы_"), Logging.LogMessageType.Error);
                return;
            }

            if(_tabScreener.IsConnected == false
                || _tabScreener.Tabs.Count == 0)
            {
                SendNewLogMessage(OsLocalization.ConvertToLocString(
                  "Eng:There are no securities connected to the screener_" +
                  "Ru:К скринеру не подключены инструменты_"), Logging.LogMessageType.Error);
                return;
            }

            _workInProgress = true;
            _spreadValues = new List<SpreadValues>();

            Thread worker = new Thread(WorkArea);
            worker.Start();
        }

        bool _workInProgress;

        private List<SpreadValues> _spreadValues = new List<SpreadValues>();

        private void WorkArea()
        {
            try
            {
                DateTime startTestTime = DateTime.Now;

                while (true)
                {
                    Thread.Sleep(5000);

                    if (startTestTime.AddMinutes(_minutesToAnalysis.ValueInt) < DateTime.Now)
                    {
                        _workInProgress = false;
                        SendNewLogMessage(GetReport(), Logging.LogMessageType.Error);
                        return;
                    }

                    SendNewLogMessage("Liquidity analyzer. Get values...", Logging.LogMessageType.System);

                    for (int i = 0; i < _tabScreener.Tabs.Count; i++)
                    {
                        BotTabSimple tab = _tabScreener.Tabs[i];

                        if (tab.IsConnected == false)
                        {
                            continue;
                        }

                        Security security = tab.Security;

                        if (security == null)
                        {
                            continue;
                        }

                        MarketDepth depth = tab.MarketDepth;

                        if (depth == null)
                        {
                            continue;
                        }

                        SetNewSpread(security, depth);
                    }
                }
            }
            catch (Exception e)
            {
                SendNewLogMessage("Erorr in logic. Process stop. Error: \n" 
                    + e.ToString(), Logging.LogMessageType.Error);
            }

            _workInProgress = false;
        }

        private void SetNewSpread(Security security, MarketDepth depth)
        {
            SpreadValues myValues = _spreadValues.Find(s => s.Security == security.Name);

            if(myValues == null)
            {
                myValues = new SpreadValues();
                myValues.Security = security.Name;
                _spreadValues.Add(myValues);
            }

            // бид аск процент

            decimal spreadBidAsk = (decimal)depth.SpreadPercent;

            if(spreadBidAsk > 0)
            {
                myValues.SpreadBidAskInPercent.Add(spreadBidAsk);
            }

            // проскальзывание для покупки

            decimal slippageBuy =
                (decimal)depth.GetSlippagePercentToEntry(Side.Buy, security, _moneyToEntry.ValueDecimal);

            if(slippageBuy > 0)
            {
                myValues.SlippageToLong.Add(slippageBuy);
            }

            // проскальзывание для продажи

            decimal slippageSell =
               (decimal)depth.GetSlippagePercentToEntry(Side.Sell, security, _moneyToEntry.ValueDecimal);

            if (slippageSell > 0)
            {
                myValues.SlippageToShort.Add(slippageSell);
            }
        }

        private string GetReport()
        {
            string report = "Report Liquidity analyzer \n";

            if(_spreadValues.Count == 0)
            {
                report += "No values... Error";
                return report;
            }

            for(int i = 0;i < _spreadValues.Count;i++)
            {
                _spreadValues[i].CalculateMiddleValues();
            }

            // Сортируем

            if(_spreadValues.Count > 1)
            {
                _spreadValues = _spreadValues.OrderBy(x => x.SlippageBothValue).ToList();
            }

            for (int i = 0; i < _spreadValues.Count; i++)
            {
                SpreadValues currentValue = _spreadValues[i];

                string repSecurity = "\n" + currentValue.Security
                    + ", Both slippage: " + currentValue.SlippageBothValue
                    + ", Long slippage: " + currentValue.SlippageToLongMiddleValue
                    + ", Short slippage: " + currentValue.SlippageToShortMiddleValue
                    + ", Spread: " + currentValue.SpreadPercentMiddleValue;

                report += repSecurity;
            }

            return report;
        }
    }

    public class SpreadValues
    {
        public string Security;

        public List<decimal> SpreadBidAskInPercent = new List<decimal>();

        public List<decimal> SlippageToLong = new List<decimal>();

        public List<decimal> SlippageToShort = new List<decimal>();

        public decimal SpreadPercentMiddleValue;

        public decimal SlippageToLongMiddleValue;

        public decimal SlippageToShortMiddleValue;

        public decimal SlippageBothValue;

        public void CalculateMiddleValues()
        {
            // 1 центры стаканов

             decimal centreMiddle = 0;
             decimal centreValueCount = 0;

             for(int i = 0;i < SpreadBidAskInPercent.Count;i++)
             {
                 if(SpreadBidAskInPercent[i] > 0)
                 {
                     centreMiddle += SpreadBidAskInPercent[i];
                     centreValueCount++;
                 }
             }

             if(centreValueCount != 0)
             {
                 centreMiddle = centreMiddle / centreValueCount;
                 SpreadPercentMiddleValue = Math.Round(centreMiddle,4);
             }

            // 2 покупка

            decimal buyMiddle = 0;
            decimal buyValueCount = 0;

            for (int i = 0; i < SlippageToLong.Count; i++)
            {
                if (SlippageToLong[i] > 0)
                {
                    buyMiddle += SlippageToLong[i];
                    buyValueCount++;
                }
            }

            if (buyMiddle != 0)
            {
                buyMiddle = buyMiddle / buyValueCount;
                SlippageToLongMiddleValue = Math.Round(buyMiddle,4);
            }

            // 3 продажа

            decimal sellMiddle = 0;
            decimal sellValueCount = 0;

            for (int i = 0; i < SlippageToShort.Count; i++)
            {
                if (SlippageToShort[i] > 0)
                {
                    sellMiddle += SlippageToShort[i];
                    sellValueCount++;
                }
            }

            if (sellMiddle != 0)
            {
                sellMiddle = sellMiddle / sellValueCount;
                SlippageToShortMiddleValue = Math.Round(sellMiddle, 4);
            }

            // 4 в обе стороны

            decimal bothMiddle = 0;
            decimal bothValueCount = 0;

            for (int i = 0; i < SlippageToShort.Count; i++)
            {
                if (SlippageToShort[i] > 0)
                {
                    bothMiddle += SlippageToShort[i];
                    bothValueCount++;
                }
                if (SlippageToLong[i] > 0)
                {
                    bothMiddle += SlippageToLong[i];
                    bothValueCount++;
                }
            }

            if (bothMiddle != 0)
            {
                bothMiddle = bothMiddle / bothValueCount;
                SlippageBothValue = Math.Round(bothMiddle, 4);
            }
        }
    }
}
