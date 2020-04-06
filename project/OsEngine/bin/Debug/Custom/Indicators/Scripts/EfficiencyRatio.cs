using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class EfficiencyRatio:Aindicator
    {
        private IndicatorDataSeries _series;
        private IndicatorParameterInt _lenght;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lenght = CreateParameterInt("Length", 14);
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
            int Lenght = _lenght.ValueInt;

            if (index - Lenght > 0)
            {
                eR1 = Math.Abs(candles[index].Close - candles[index - Lenght].Close);

                eR2 = 0;

                for (int i = 0; i <= Lenght; i++)
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
