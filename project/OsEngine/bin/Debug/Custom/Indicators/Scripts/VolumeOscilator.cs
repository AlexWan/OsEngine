using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class VolumeOscilator : Aindicator

    {
        private IndicatorDataSeries series1;
        private IndicatorParameterInt lenght1;
        private IndicatorParameterInt lenght2;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                lenght1 = CreateParameterInt("Lenght 1", 20);
                lenght2 = CreateParameterInt("Lenght 2", 10);
                series1 = CreateSeries("VO", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            series1.Values[index] = GetValueVolumeOscillator(candles, index);
        }

        private decimal GetValueVolumeOscillator(List<Candle> candles, int index)
        {
            if ((index < lenght1.ValueInt) || (index < lenght2.ValueInt))
            {
                return 0;
            }

            decimal sum1 = 0;
            for (int i = index; i > index - lenght1.ValueInt; i--)
            {
                sum1 += candles[i].Volume;  //GetPoint(candles, i);
            }
            var ma1 = sum1 / lenght1.ValueInt;

            decimal sum2 = 0;
            for (int i = index; i > index - lenght2.ValueInt; i--)
            {
                sum2 += candles[i].Volume;  //GetPoint(candles, i);
            }
            var ma2 = sum2 / lenght2.ValueInt;

            var vo = (100 * (ma2 - ma1) / ma1);
            return Math.Round(vo, 5);

        }
    }
}
