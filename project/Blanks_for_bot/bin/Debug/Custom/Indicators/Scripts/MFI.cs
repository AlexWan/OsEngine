using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class MFI : Aindicator
    {
        private IndicatorParameterInt _period;
        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _series = CreateSeries("MFI", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
                _period = CreateParameterInt("Period", 3);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValue(candles, index);
        }

        private decimal GetValue(List<Candle> candles, int index)
        {
            decimal mf;

            if (index <= _period.ValueInt)
            {
                return 0;
            }

            decimal value = 0;

            while (index >= _tp.Count)
            {
                _tp.Add(GetTypicalPrice(candles, index));
                _fn.Add(0);
                _fp.Add(0);
            }

            _tp[index] = GetTypicalPrice(candles, index);

            mf = _tp[index] * candles[index].Volume;

            if (index > 1)
            {
                if (_tp[index] > _tp[index - 1])
                {
                    _fp[index] = mf;
                }
                if (_tp[index] < _tp[index - 1])
                {
                    _fn[index] = mf;
                }
            }

            if (index > _period.ValueInt)
            {
                decimal sumFp = 0;
                decimal sumFn = 0;
                decimal ratio;

                for (int i = index - _period.ValueInt + 1; i < index; i++)
                {
                    sumFp = sumFp + _fp[i];
                    sumFn = sumFn + _fn[i];
                }
                if (sumFn == 0)
                {
                    ratio = 0;
                }
                else
                {
                    ratio = sumFp / sumFn;
                }

                value = 100 - 100 / (1 + ratio);
            }

            return Math.Round(value, 2);
        }

        private decimal GetTypicalPrice(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                return 0;
            }
            return (candles[index].High + candles[index].Low + candles[index].Close) / 3;
        }

        /// <summary>
        /// pozitive flow
        /// </summary>
        private List<decimal> _fp = new List<decimal>();

        /// <summary>
        /// negative flow
        /// </summary>
        private List<decimal> _fn = new List<decimal>();

        /// <summary>
        /// typical prive
        /// </summary>
        private List<decimal> _tp = new List<decimal>();


    }
}
