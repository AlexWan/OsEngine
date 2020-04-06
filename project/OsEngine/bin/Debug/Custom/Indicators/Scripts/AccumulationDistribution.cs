using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class AccumulationDistribution : Aindicator
    {
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
