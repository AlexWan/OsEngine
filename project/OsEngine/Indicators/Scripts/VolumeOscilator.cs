using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("VolumeOscilator")]
    public class VolumeOscilator : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "Volume Oscillator calculates the percentage difference between fast and slow moving averages of volume, showing how current activity changes relative to its average level. " +
                             "Traders use the oscillator crossing the zero line to assess strengthening or weakening volume support for the trend.";

                string ru = "Volume Oscillator рассчитывает разницу между быстрой и медленной скользящими средними объёма в процентах, показывая изменение текущей активности относительно её среднего уровня. " +
                            "Трейдеры используют пересечение осциллятора нулевой линии для оценки усиления или ослабления объёмной поддержки тренда.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorDataSeries _series1;

        private IndicatorParameterInt _length1;

        private IndicatorParameterInt _length2;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _length1 = CreateParameterInt("Length 1", 20);
                _length2 = CreateParameterInt("Length 2", 10);
                _series1 = CreateSeries("VO", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series1.Values[index] = GetValueVolumeOscillator(candles, index);
        }

        private decimal GetValueVolumeOscillator(List<Candle> candles, int index)
        {
            if ((index < _length1.ValueInt) || (index < _length2.ValueInt))
            {
                return 0;
            }

            decimal sum1 = 0;
            for (int i = index; i > index - _length1.ValueInt; i--)
            {
                sum1 += candles[i].Volume;  //GetPoint(candles, i);
            }
            var ma1 = sum1 / _length1.ValueInt;

            decimal sum2 = 0;
            for (int i = index; i > index - _length2.ValueInt; i--)
            {
                sum2 += candles[i].Volume;  //GetPoint(candles, i);
            }
            var ma2 = sum2 / _length2.ValueInt;

            var vo = (100 * (ma2 - ma1) / ma1);
            return Math.Round(vo, 5);

        }
    }
}