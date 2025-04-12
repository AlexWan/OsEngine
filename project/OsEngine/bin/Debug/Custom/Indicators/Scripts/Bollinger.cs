using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    [Indicator("Bollinger")]
    public class Bollinger : Aindicator
    {
        private IndicatorParameterInt _length;

        private IndicatorParameterDecimal _deviation;

        private IndicatorDataSeries _seriesUp;

        private IndicatorDataSeries _seriesDown;

        private IndicatorDataSeries _seriesCenter;

        private Aindicator _sma;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _length = CreateParameterInt("Length", 21);
                _deviation = CreateParameterDecimal("Deviation", 2);

                _seriesUp = CreateSeries("Up line", Color.Green, IndicatorChartPaintType.Line, true);
                _seriesDown = CreateSeries("Down line", Color.Green, IndicatorChartPaintType.Line, true);

                _seriesCenter = CreateSeries("Centre line", Color.Green, IndicatorChartPaintType.Line, true);

                _sma = IndicatorsFactory.CreateIndicatorByName("Sma", Name + "Sma", false);
                _sma.Parameters[0].Bind(_length);
                ProcessIndicator("Central SMA", _sma);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index <= _length.ValueInt)
            {
                return;
            }

            decimal valueSma = _sma.DataSeries[0].Values[index];

            if (valueSma == 0)
            {
                return;
            }

            _seriesCenter.Values[index] = _sma.DataSeries[0].Values[index];

            decimal[] valueDev = new decimal[_length.ValueInt];

            for (int i = index - _length.ValueInt + 1, i2 = 0; i < index + 1; i++, i2++)
            {
                valueDev[i2] = candles[i].Close - valueSma;
            }

            for (int i = 0; i < valueDev.Length; i++)
            {
                valueDev[i] = Convert.ToDecimal(Math.Pow(Convert.ToDouble(valueDev[i]), 2));
            }

            double summ = 0;

            for (int i = 0; i < valueDev.Length; i++)
            {
                summ += Convert.ToDouble(valueDev[i]);
            }

            if (_length.ValueInt > 30)
            {
                summ = summ / (_length.ValueInt - 1);
            }
            else
            {
                summ = summ / _length.ValueInt;
            }

            summ = Math.Sqrt(summ);

            _seriesUp.Values[index] = Math.Round(valueSma + Convert.ToDecimal(summ) * _deviation.ValueDecimal, 6);
            _seriesDown.Values[index] = Math.Round(valueSma - Convert.ToDecimal(summ) * _deviation.ValueDecimal, 6);
        }
    }
}