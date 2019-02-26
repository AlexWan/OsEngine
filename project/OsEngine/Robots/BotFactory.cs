/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.Robots.CounterTrend;
using OsEngine.Robots.High_Frequency;
using OsEngine.Robots.MarketMaker;
using OsEngine.Robots.Patterns;
using OsEngine.Robots.Trend;

namespace OsEngine.Robots
{
    public class BotFactory
    {
        /// <summary>
        /// list robots name / 
        /// список доступных роботов
        /// </summary>
        public static List<string> GetNamesStrategy()
        {
            List<string> result = new List<string>();

            result.Add("MarketMakerBot");
            result.Add("PatternTrader");
            result.Add("HighFrequencyTrader");
            result.Add("Bollinger");
            result.Add("Williams Band");
            result.Add("TwoLegArbitrage");
            result.Add("ThreeSoldier");
            result.Add("PriceChannelTrade");
            result.Add("SmaStochastic");
            result.Add("ClusterCountertrend");
            result.Add("PairTraderSpreadSma");
            result.Add("WilliamsRangeTrade");
            result.Add("ParabolicSarTrade");
            result.Add("PivotPointsRobot");
            result.Add("RsiContrtrend");
            result.Add("PinBarTrade");

            return result;
        }

        /// <summary>
        /// create robot
        /// создать робота
        /// </summary>
        public static BotPanel GetStrategyForName(string nameClass, string name, StartProgram startProgram)
        {
            BotPanel bot = null;
            // примеры и бесплатные боты

            if (nameClass == "ClusterCountertrend")
            {
                bot = new ClusterCountertrend(name, startProgram);
            }
            if (nameClass == "PatternTrader")
            {
                bot = new PatternTrader(name, startProgram);
            }
            if (nameClass == "HighFrequencyTrader")
            {
                bot = new HighFrequencyTrader(name, startProgram);
            }
            if (nameClass == "PivotPointsRobot")
            {
                bot = new PivotPointsRobot(name, startProgram);
            }
            if (nameClass == "Williams Band")
            {
                bot = new StrategyBillWilliams(name, startProgram);
            }
            if (nameClass == "MarketMakerBot")
            {
                bot = new MarketMakerBot(name, startProgram);
            }
            if (nameClass == "Bollinger")
            {
                bot = new StrategyBollinger(name, startProgram);
            }
            if (nameClass == "ParabolicSarTrade")
            {
                bot = new ParabolicSarTrade(name, startProgram);
            }
            if (nameClass == "PriceChannelTrade")
            {
                bot = new PriceChannelTrade(name, startProgram);
            }
            if (nameClass == "WilliamsRangeTrade")
            {
                bot = new WilliamsRangeTrade(name, startProgram);
            }
            if (nameClass == "SmaStochastic")
            {
                bot = new SmaStochastic(name, startProgram);
            }
            if (nameClass == "PinBarTrade")
            {
                bot = new PinBarTrade(name, startProgram);
            }
            if (nameClass == "TwoLegArbitrage")
            {
                bot = new TwoLegArbitrage(name, startProgram);
            }
            if (nameClass == "ThreeSoldier")
            {
                bot = new ThreeSoldier(name, startProgram);
            }
            if (nameClass == "RsiContrtrend")
            {
                bot = new RsiContrtrend(name, startProgram);
            }
            if (nameClass == "PairTraderSpreadSma")
            {
                bot = new PairTraderSpreadSma(name, startProgram);
            }


            return bot;
        }
    }
}
