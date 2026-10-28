/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.IndicatorMarketDepth.Scripts
{
    /// <summary>
    /// Устойчивость / Восстановление (Resilience / Replenishment): как быстро
    /// ликвидность восстанавливается после агрессивных сделок.
    /// </summary>
    public class Resilience : AMarketDepth
    {
        public override string Description
        {
            get
            {
                string eng = "Resilience / Replenishment shows how fast liquidity recovers after aggressive trades. Series 'Depth' is the total volume of the first 'Depth' levels on both sides. Series 'Resilience' is the current depth divided by the maximum depth over the last 'Window' seconds, from 0 to 1. A value near 1 means liquidity stays near its recent peak (fast replenishment), a value well below 1 means the book has thinned out after a hit. Series 'Shocks' is the number of trades with volume at least 'TradeThreshold' in the current second. A drop in Resilience together with a rise in Shocks means an aggressive player is draining liquidity.";
                string ru = "Устойчивость / Восстановление (Resilience / Replenishment) показывает, как быстро ликвидность восстанавливается после агрессивных сделок. Серия «Depth» — суммарный объём первых «Depth» уровней обеих сторон. Серия «Resilience» — текущая глубина, делённая на максимум глубины за последние «Window» секунд, от 0 до 1. Значение около 1 — ликвидность держится возле недавнего пика (быстрое пополнение), заметно ниже 1 — книга просела после удара. Серия «Shocks» — число сделок объёмом не меньше «TradeThreshold» за текущую секунду. Спад Resilience вместе с ростом Shocks означает, что агрессивный игрок выбивает ликвидность.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        public override MarketDepthIndicatorDataSource DataSource
        {
            get { return MarketDepthIndicatorDataSource.Both; }
        }

        private ParameterMarketDepthInt _window;

        private ParameterMarketDepthInt _depthLevels;

        private ParameterMarketDepthDecimal _tradeThreshold;

        private MarketDepthDataSeries _depthSeries;

        private MarketDepthDataSeries _resilienceSeries;

        private MarketDepthDataSeries _shocksSeries;

        private readonly List<decimal> _depthBuffer = new List<decimal>();

        private DateTime _lastShockSecond = DateTime.MinValue;

        private decimal _curShocks;

        public override void OnStateChange(MarketDepthIndicatorState state)
        {
            if (state == MarketDepthIndicatorState.Configure)
            {
                _window = CreateParameterInt("Window", 10);
                _depthLevels = CreateParameterInt("Depth", 5);
                _tradeThreshold = CreateParameterDecimal("TradeThreshold", 100);

                _depthSeries = CreateSeries("Depth", Color.SteelBlue, IndicatorChartPaintType.Column, true);
                _resilienceSeries = CreateSeries("Resilience", Color.MediumPurple, IndicatorChartPaintType.Line, true);
                _shocksSeries = CreateSeries("Shocks", Color.OrangeRed, IndicatorChartPaintType.Column, true);
            }
        }

        public override void OnProcess(MarketDepth depth)
        {
            if (depth.Bids == null || depth.Bids.Count == 0
                || depth.Asks == null || depth.Asks.Count == 0)
            {
                return;
            }

            int levels = _depthLevels.ValueInt;

            if (levels < 1)
            {
                levels = 5;
            }

            decimal totalDepth = 0;

            for (int i = 0; i < depth.Bids.Count && i < levels; i++)
            {
                totalDepth += (decimal)depth.Bids[i].Bid;
            }

            for (int i = 0; i < depth.Asks.Count && i < levels; i++)
            {
                totalDepth += (decimal)depth.Asks[i].Ask;
            }

            int window = _window.ValueInt;

            if (window < 1)
            {
                window = 10;
            }

            _depthBuffer.Add(totalDepth);

            while (_depthBuffer.Count > window)
            {
                _depthBuffer.RemoveAt(0);
            }

            decimal depthMax = 0;

            for (int i = 0; i < _depthBuffer.Count; i++)
            {
                if (_depthBuffer[i] > depthMax)
                {
                    depthMax = _depthBuffer[i];
                }
            }

            decimal resilience = 0;

            if (depthMax > 0)
            {
                resilience = totalDepth / depthMax;
            }

            SetValue(_depthSeries, totalDepth);
            SetValue(_resilienceSeries, resilience);
        }

        public override void OnProcessTrade(Trade trade)
        {
            if (_lastShockSecond == DateTime.MinValue
                || trade.Time >= _lastShockSecond.AddSeconds(1))
            {
                _lastShockSecond = trade.Time;
                _curShocks = 0;
            }

            decimal threshold = _tradeThreshold.ValueDecimal;

            if (trade.Volume >= threshold)
            {
                _curShocks += 1;
            }

            SetValue(_shocksSeries, _curShocks);
        }
    }
}
