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
    public class Spread : AMarketDepth
    {
        public override string Description
        {
            get
            {
                string eng = "Spread is the distance between the best bid and the best ask. Series 'Spread' is the current value, 'SpreadMa' is the average over 'Period' seconds, 'SpreadVol' is the volatility over 'Period' seconds. Mode 'Absolute' is in price units (best ask minus best bid), mode 'Percent' is in percent of the best bid. A wide spread means expensive trading and often low liquidity, a narrow spread means cheap entry and a liquid market.";
                string ru = "Спред — расстояние между лучшей ценой покупки (Bid) и лучшей ценой продажи (Ask). Серия «Spread» — текущее значение, «SpreadMa» — среднее за «Period» секунд, «SpreadVol» — волатильность за «Period» секунд. Режим «Absolute» — в денежных единицах (цена Ask − цена Bid), «Percent» — в процентах от лучшего Bid. Широкий спред — дорогая торговля и часто низкая ликвидность, узкий — дешёвый вход и ликвидный рынок.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private ParameterMarketDepthString _mode;

        private ParameterMarketDepthInt _period;

        private MarketDepthDataSeries _series;

        private MarketDepthDataSeries _seriesMa;

        private MarketDepthDataSeries _seriesVol;

        private List<decimal> _history = new List<decimal>();

        public override void OnStateChange(MarketDepthIndicatorState state)
        {
            if (state == MarketDepthIndicatorState.Configure)
            {
                _mode = CreateParameterStringCollection("Mode", "Absolute", new List<string> { "Absolute", "Percent" });
                _period = CreateParameterInt("Period", 10);
                _series = CreateSeries("Spread", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
                _seriesMa = CreateSeries("SpreadMa", Color.DarkOrange, IndicatorChartPaintType.Line, true);
                _seriesVol = CreateSeries("SpreadVol", Color.Gray, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(MarketDepth depth)
        {
            if (depth.Asks == null || depth.Asks.Count == 0
                || depth.Bids == null || depth.Bids.Count == 0)
            {
                SetValue(_series, 0);
                SetValue(_seriesMa, 0);
                SetValue(_seriesVol, 0);
                return;
            }

            double bestAsk = depth.Asks[0].Price;
            double bestBid = depth.Bids[0].Price;

            if (bestAsk <= 0 || bestBid <= 0 || bestBid >= bestAsk)
            {
                SetValue(_series, 0);
                SetValue(_seriesMa, 0);
                SetValue(_seriesVol, 0);
                return;
            }

            decimal spread = (decimal)(bestAsk - bestBid);

            if (_mode.ValueString == "Percent")
            {
                spread = spread / ((decimal)bestBid / 100m);
            }

            int period = _period.ValueInt;

            if (period < 1)
            {
                period = 10;
            }

            _history.Add(spread);

            while (_history.Count > period)
            {
                _history.RemoveAt(0);
            }

            decimal average = Average(_history);
            decimal volatility = StdDev(_history, average);

            SetValue(_series, spread);
            SetValue(_seriesMa, average);
            SetValue(_seriesVol, volatility);
        }

        private static decimal Average(List<decimal> values)
        {
            if (values.Count == 0)
            {
                return 0;
            }

            decimal sum = 0;

            for (int i = 0; i < values.Count; i++)
            {
                sum += values[i];
            }

            return sum / values.Count;
        }

        private static decimal StdDev(List<decimal> values, decimal average)
        {
            if (values.Count < 2)
            {
                return 0;
            }

            decimal sum = 0;

            for (int i = 0; i < values.Count; i++)
            {
                decimal diff = values[i] - average;
                sum += diff * diff;
            }

            decimal variance = sum / values.Count;

            return (decimal)Math.Sqrt((double)variance);
        }
    }
}
