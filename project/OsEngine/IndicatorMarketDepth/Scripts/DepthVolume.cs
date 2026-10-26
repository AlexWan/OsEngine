/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using System.Drawing;

namespace OsEngine.IndicatorMarketDepth.Scripts
{
    /// <summary>
    /// Суммарная глубина: объём на ближайших N уровнях отдельно по покупкам и продажам.
    /// </summary>
    public class DepthVolume : AMarketDepth
    {
        public override string Description
        {
            get
            {
                string eng = "Depth Volume: how many orders stand on the nearest 'Levels' levels, separately for bids and asks. Series 'BidDepth' is the total bid volume, 'AskDepth' is the total ask volume. Large values mean dense liquidity (a wall of orders), small values mean a thin market where price is easy to move. Comparing BidDepth and AskDepth shows which side has more liquidity. Parameter 'Levels' is the number of levels.";
                string ru = "Суммарная глубина (Depth Volume) — сколько заявок стоит на ближайших «Levels» уровнях, отдельно по покупкам и продажам. Серия «BidDepth» — суммарный объём Bid-уровней, «AskDepth» — суммарный объём Ask-уровней. Большие значения — плотная ликвидность (стена заявок), маленькие — тонкий рынок, где цену легко сдвинуть. Сравнение BidDepth и AskDepth показывает, с какой стороны больше ликвидности. Параметр «Levels» — число уровней.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private ParameterMarketDepthInt _levels;

        private MarketDepthDataSeries _bidSeries;

        private MarketDepthDataSeries _askSeries;

        public override void OnStateChange(MarketDepthIndicatorState state)
        {
            if (state == MarketDepthIndicatorState.Configure)
            {
                _levels = CreateParameterInt("Levels", 10);
                _bidSeries = CreateSeries("BidDepth", Color.Green, IndicatorChartPaintType.Line, true);
                _askSeries = CreateSeries("AskDepth", Color.Red, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(MarketDepth depth)
        {
            int levels = _levels.ValueInt;

            if (levels <= 0)
            {
                levels = 10;
            }

            decimal bidDepth = 0;
            decimal askDepth = 0;

            for (int i = 0; i < depth.Bids.Count && i < levels; i++)
            {
                bidDepth += (decimal)depth.Bids[i].Bid;
            }

            for (int i = 0; i < depth.Asks.Count && i < levels; i++)
            {
                askDepth += (decimal)depth.Asks[i].Ask;
            }

            SetValue(_bidSeries, bidDepth);
            SetValue(_askSeries, askDepth);
        }
    }
}
