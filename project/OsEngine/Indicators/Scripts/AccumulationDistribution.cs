using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("AccumulationDistribution")]
    public class AccumulationDistribution : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "The Accumulation/Distribution indicator sums candle volumes weighted by the position of the close within the bar's range, revealing whether money is flowing into or out of the asset. " +
                             "Traders compare its dynamics with the price chart: a divergence often signals a potential reversal or weakening trend.";

                string ru = "Индикатор накопления/распределения суммирует объёмы свечей с весом, зависящим от положения close внутри диапазона бара, и показывает, куда фактически уходят деньги — в актив или из него. " +
                            "Трейдеры сравнивают его динамику с графиком цены: расхождение (дивергенция) часто сигнализирует о возможном развороте или ослаблении тренда.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _series = CreateSeries("A D", Color.Aqua, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValue(candles, index);
        }

        private decimal GetValue(List<Candle> candles, int index)
        {
            if (index == 0 ||
                candles.Count <= index)
            {
                return 0;
            }

            Candle c = candles[index];

            if ((c.High - c.Low) == 0)
            {
                return _series.Values[index - 1];
            }

            return Math.Round(c.Volume * ((c.Close - c.Low) - (c.High - c.Close)) / (c.High - c.Low) + _series.Values[index - 1], 0);
        }
    }
}
