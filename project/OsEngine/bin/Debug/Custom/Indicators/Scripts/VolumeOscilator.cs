﻿using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class VolumeOscilator : Aindicator

    {
        private IndicatorDataSeries series1;
        private IndicatorParameterInt length1;
        private IndicatorParameterInt length2;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                length1 = CreateParameterInt("Length 1", 20);
                length2 = CreateParameterInt("Length 2", 10);
                series1 = CreateSeries("VO", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            series1.Values[index] = GetValueVolumeOscillator(candles, index);
        }

        private decimal GetValueVolumeOscillator(List<Candle> candles, int index)
        {
            if ((index < length1.ValueInt) || (index < length2.ValueInt))
            {
                return 0;
            }

            decimal sum1 = 0;
            for (int i = index; i > index - length1.ValueInt; i--)
            {
                sum1 += candles[i].Volume;  //GetPoint(candles, i);
            }
            var ma1 = sum1 / length1.ValueInt;

            decimal sum2 = 0;
            for (int i = index; i > index - length2.ValueInt; i--)
            {
                sum2 += candles[i].Volume;  //GetPoint(candles, i);
            }
            var ma2 = sum2 / length2.ValueInt;

            var vo = (100 * (ma2 - ma1) / ma1);
            return Math.Round(vo, 5);

        }
    }
}
