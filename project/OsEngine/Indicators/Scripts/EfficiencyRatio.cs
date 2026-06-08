using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    [Indicator("EfficiencyRatio")]
    public class EfficiencyRatio:Aindicator
    {
        private IndicatorDataSeries _series;

        private IndicatorParameterInt _length;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _length = CreateParameterInt("Length", 14);
                _series = CreateSeries("ER", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValue(candles, index);
        }

        private decimal GetValue(List<Candle> candles, int index)
        {
            decimal eR1 = 0;
            decimal eR2 = 0;
            int Length = _length.ValueInt;

            if (index - Length > 0)
            {
                eR1 = Math.Abs(candles[index].Close - candles[index - Length].Close);

                eR2 = 0;

                for (int i = 0; i <= Length; i++)
                {
                    eR2 += Math.Abs(candles[index - i].Close - candles[index - i - 1].Close);
                }
            }

            if (eR2 != 0)
            {
                return Math.Round(eR1 / eR2, 4);
            }

            return 0;
        }
    }
}