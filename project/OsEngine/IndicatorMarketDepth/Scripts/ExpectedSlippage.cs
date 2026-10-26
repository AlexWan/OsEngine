/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.IndicatorMarketDepth.Scripts
{
    public class ExpectedSlippage : AMarketDepth
    {
        public override string Description
        {
            get
            {
                string eng = "Expected Slippage is the market impact estimate for a target order size. Series 'SlippageBuy' and 'SlippageSell' show, in percent, how much the average execution price moves away from the best price when filling 'Volume' from the order book. It reflects how expensive it is to execute a large order right now. Parameter 'Volume' is the target size.";
                string ru = "Ожидаемое проскальзывание (Expected Slippage) — оценка влияния на рынок для заданного размера заявки. Серии «SlippageBuy» и «SlippageSell» показывают в процентах, насколько средняя цена исполнения уходит от лучшей цены при исполнении «Volume» из стакана. Отражает, насколько дорого исполнить крупную заявку прямо сейчас. Параметр «Volume» — целевой объём.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private ParameterMarketDepthDecimal _volume;

        private MarketDepthDataSeries _slippageBuy;

        private MarketDepthDataSeries _slippageSell;

        public override void OnStateChange(MarketDepthIndicatorState state)
        {
            if (state == MarketDepthIndicatorState.Configure)
            {
                _volume = CreateParameterDecimal("Volume", 100);
                _slippageBuy = CreateSeries("SlippageBuy", Color.Green, IndicatorChartPaintType.Line, true);
                _slippageSell = CreateSeries("SlippageSell", Color.Red, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(MarketDepth depth)
        {
            decimal volume = _volume.ValueDecimal;

            decimal buySlippage = SlippagePercent(depth.Asks, volume, true);
            decimal sellSlippage = SlippagePercent(depth.Bids, volume, false);

            SetValue(_slippageBuy, buySlippage);
            SetValue(_slippageSell, sellSlippage);
        }

        private static decimal SlippagePercent(List<MarketDepthLevel> levels, decimal volume, bool isBuy)
        {
            if (volume <= 0 || levels == null || levels.Count == 0)
            {
                return 0;
            }

            double bestPrice = levels[0].Price;

            if (bestPrice <= 0)
            {
                return 0;
            }

            decimal remaining = volume;
            decimal cost = 0;

            for (int i = 0; i < levels.Count; i++)
            {
                double price = levels[i].Price;
                decimal levelVolume = (decimal)(isBuy ? levels[i].Ask : levels[i].Bid);

                if (price <= 0 || levelVolume <= 0)
                {
                    continue;
                }

                decimal fill = remaining < levelVolume ? remaining : levelVolume;
                cost += (decimal)price * fill;
                remaining -= fill;

                if (remaining <= 0)
                {
                    break;
                }
            }

            decimal filled = volume - remaining;

            if (filled <= 0)
            {
                return 0;
            }

            decimal averagePrice = cost / filled;
            decimal best = (decimal)bestPrice;

            if (isBuy)
            {
                return (averagePrice - best) / best * 100m;
            }

            return (best - averagePrice) / best * 100m;
        }
    }
}
