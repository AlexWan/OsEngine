using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class WilliamsRange : Aindicator
    {
        private IndicatorParameterInt _period;
        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _series = CreateSeries("WR", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
                _period = CreateParameterInt("Period", 14);
            }
        }

        private List<decimal> _high = new List<decimal>();

        private List<decimal> _low = new List<decimal>();

        public List<decimal> Values;

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValue(candles, index);
        }
        private decimal GetValue(List<Candle> candles, int index)
        {
            if (index < _period.ValueInt)
            {
                return 0;
            }

            while (index + 1 > _high.Count)
            {
                _high.Add(0);
                _low.Add(0);
            }

            _high[index] = GetHigh(candles, index);
            _low[index] = GetLow(candles, index);

            if (_high[index] - _low[index] == 0)
            {
                return _series.Values[_series.Values.Count - 1];
            }
            return Math.Round(-100 * (_high[index] - candles[index].Close) / (_high[index] - _low[index]), 2);

        }
        private decimal GetHigh(List<Candle> candles, int index)
        {
            if (index < _period.ValueInt)
            {
                return 0;
            }

            decimal maxhigh = 0;

            for (int i = index - _period.ValueInt + 1; i < index + 1; i++)
            {

                if (maxhigh < candles[i].High)
                {
                    maxhigh = candles[i].High;
                }
            }
            return maxhigh;
        }

        private decimal GetLow(List<Candle> candles, int index)
        {
            if (index < _period.ValueInt)
            {
                return 0;
            }

            decimal maxlow = decimal.MaxValue;

            for (int i = index - _period.ValueInt + 1; i < index + 1; i++)
            {
                if (maxlow > candles[i].Low)
                {
                    maxlow = candles[i].Low;
                }
            }
            return maxlow;
        }
    }
}