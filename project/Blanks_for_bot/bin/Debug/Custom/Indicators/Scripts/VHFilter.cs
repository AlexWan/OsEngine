using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class VHFilter: Aindicator
    {
        private IndicatorDataSeries _series;
        private IndicatorParameterInt _period;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _period = CreateParameterInt("Period", 28);
                _series = CreateSeries("VHFilter", Color.Blue, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValue(candles, index);
        }

        private decimal GetValue(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                _highlow.Clear();
            }
            while(index >= _highlow.Count)
            {
                _highlow.Add(GetHighLow(candles,index));
            }

            if (index < _period.ValueInt)
            {
                return 0;
            }
            decimal value;
            decimal sum = 0;

            for (int i = index - _period.ValueInt + 1; i < index + 1; i++)
            {
                sum = sum + Math.Abs(candles[i].Close - candles[i - 1].Close);
            }

            if (sum == 0)
            {
                return 0;
            }
            else
            {
                value = _highlow[index] / sum;
            }

            return Math.Round(value, 2);
        }

        private decimal GetHighLow(List<Candle> candles, int index)
        {
            if (index < _period.ValueInt)
            {
                return 0;
            }

            decimal maxhigh = 0;
            decimal maxlow = decimal.MaxValue;


            for (int i = index - _period.ValueInt + 1; i < index + 1; i++)
            {

                if (maxhigh < candles[i].Close)
                {
                    maxhigh = candles[i].Close;
                }

                if (maxlow > candles[i].Close)
                {
                    maxlow = candles[i].Close;
                }
            }
            return (maxhigh - maxlow);
        }

        private List<decimal> _highlow = new List<decimal>();
    }
}
