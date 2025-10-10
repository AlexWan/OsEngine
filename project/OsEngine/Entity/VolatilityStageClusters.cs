/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OsEngine.Entity
{
    public class VolatilityStageClusters
    {
        public List<BotTabSimple> ClusterOne = new List<BotTabSimple>();

        public List<BotTabSimple> ClusterTwo = new List<BotTabSimple>();

        public List<BotTabSimple> ClusterThree = new List<BotTabSimple>();

        public decimal ClusterOnePercent = 33m;

        public decimal ClusterTwoPercent = 33m;

        public decimal ClusterThreePercent = 34m;

        public int Length = 100;

        public void Calculate(List<BotTabSimple> sources, int candlesCount)
        {
            if(ClusterOne.Count != 0)
            {
                ClusterOne.Clear();
            }
            if (ClusterTwo.Count != 0)
            {
                ClusterTwo.Clear();
            }
            if (ClusterThree.Count != 0)
            {
                ClusterThree.Clear();
            }

            Length = candlesCount;

            CalculateClusters(sources);
        }

        public void Calculate(List<BotTabSimple> sources, int candlesCount,
            decimal clusterOnePercent, decimal clusterTwoPercent, decimal clusterThreePercent)
        {
            if (ClusterOne.Count != 0)
            {
                ClusterOne.Clear();
            }
            if (ClusterTwo.Count != 0)
            {
                ClusterTwo.Clear();
            }
            if (ClusterThree.Count != 0)
            {
                ClusterThree.Clear();
            }

            Length = candlesCount;

            decimal fullPercents = clusterOnePercent + clusterTwoPercent + clusterThreePercent;

            if (fullPercents != 100)
            {
                throw new Exception("VolatilityStageClusters error. Percent is not 100");
            }

            ClusterOnePercent = clusterOnePercent;
            ClusterTwoPercent = clusterTwoPercent;
            ClusterThreePercent = clusterThreePercent;

            CalculateClusters(sources);
        }

        private void CalculateClusters(List<BotTabSimple> sources)
        {
            if(sources.Count == 0)
            {
                return;
            }

            // разбиваем присланные источники и размещаем в три кластера по процентам. От самых волатильных до минимально

            // 1 оставляем источники со свечками

            List<SourceVolatility> sourcesWithCandles = new List<SourceVolatility>();

            for(int i = 0;i < sources.Count;i++)
            {
                List<Candle> candles = sources[i].CandlesFinishedOnly;

                if(candles == null 
                    || candles.Count == 0)
                {
                    continue;
                }

                SourceVolatility newVola = new SourceVolatility();
                newVola.Tab = sources[i];
                newVola.Candles = sources[i].CandlesAll;
                newVola.Calculate(Length);

                sourcesWithCandles.Add(newVola);
            }

            if(sourcesWithCandles.Count <= 1)
            {
                return;
            }

            sourcesWithCandles = sourcesWithCandles.OrderBy(x => x.Volatility).ToList();

            // 2 разбиваем по кластерам

            decimal oneLotInArray = Convert.ToDecimal(sourcesWithCandles.Count) / 100;

            for(int i = 0;i < sourcesWithCandles.Count;i++)
            {
                if ((i + 1) <= ClusterOnePercent * oneLotInArray)
                {
                    ClusterOne.Add(sourcesWithCandles[i].Tab);
                }
                else if ((i + 1) <= (ClusterOnePercent + ClusterTwoPercent) * oneLotInArray)
                {
                    ClusterTwo.Add(sourcesWithCandles[i].Tab);
                }
                else
                {
                    ClusterThree.Add(sourcesWithCandles[i].Tab);
                }
            }
        }
    }

    public class SourceVolatility
    {
        public BotTabSimple Tab;

        public List<Candle> Candles;

        public decimal Volatility;

        public void Calculate(int candlesCount)
        {
            if (Candles == null || Candles.Count == 0)
            {
                return;
            }

            decimal maxPrice = decimal.MinValue;
            decimal minPrice = decimal.MaxValue;

            for (int i = Candles.Count - 1; i >= 0 && i > Candles.Count -1-candlesCount; i--)
            {
                Candle curCandle = Candles[i];

                if(curCandle.High > maxPrice)
                {
                    maxPrice = curCandle.High;
                }
                if(curCandle.Low < minPrice)
                {
                    minPrice = curCandle.Low;
                }
            }

            if(maxPrice == decimal.MinValue
                || minPrice == decimal.MaxValue)
            {
                return;
            }

            decimal move = maxPrice - minPrice;

            decimal movePercent = move / (minPrice / 100);

            Volatility = movePercent;
        }
    }
}
