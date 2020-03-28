using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class Bollinger : Aindicator
    {
        private IndicatorParameterInt _lenght;
        private IndicatorParameterDecimal _deviation;

        private IndicatorDataSeries _seriesUp;
        private IndicatorDataSeries _seriesDown;
        private IndicatorDataSeries _seriesCenter;

        private Aindicator _sma;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lenght = CreateParameterInt("Length", 21);
                _deviation = CreateParameterDecimal("Deviation", 2);

                _seriesUp = CreateSeries("Up line", Color.Green, IndicatorChartPaintType.Line, true);
                _seriesDown = CreateSeries("Down line", Color.Green, IndicatorChartPaintType.Line, true);

                _seriesCenter = CreateSeries("Centre line", Color.Green, IndicatorChartPaintType.Line, true);

                _sma = IndicatorsFactory.CreateIndicatorByName("Sma", Name + "Sma", false);
                ((IndicatorParameterInt)_sma.Parameters[0]).Bind(_lenght);
                ProcessIndicator("Central SMA", _sma);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index <= _lenght.ValueInt)
            {
                return;
            }

            decimal valueSma = _sma.DataSeries[0].Values[index];

            _seriesCenter.Values[index] = _sma.DataSeries[0].Values[index];

            decimal[] valueDev = new decimal[_lenght.ValueInt];

            for (int i = index - _lenght.ValueInt + 1, i2 = 0; i < index + 1; i++, i2++)
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

            if (_lenght.ValueInt > 30)
            {
                summ = summ / (_lenght.ValueInt - 1);
            }
            else
            {
                summ = summ / _lenght.ValueInt;
            }

            summ = Math.Sqrt(summ);

            _seriesUp.Values[index] = Math.Round(valueSma + Convert.ToDecimal(summ) * _deviation.ValueDecimal, 6);
            _seriesDown.Values[index] = Math.Round(valueSma - Convert.ToDecimal(summ) * _deviation.ValueDecimal, 6);
        }
    }
}