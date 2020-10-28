using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class CMO:Aindicator
    {
        private IndicatorDataSeries _series;
        private IndicatorParameterInt _period;
        

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _period = CreateParameterInt("Period", 14);
                _series = CreateSeries("Cmo", Color.Orange, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValue(candles, index);
        }

        private decimal GetValue(List<Candle> candles, int index)
        {
            decimal value = 0;

            decimal sum1 = 0;

            decimal sum2 = 0;
            while (index >= _cmo1.Count)
            {
                _cmo1.Add(GetCmo1(candles, index));

            }
            while (index >= _cmo2.Count)
            {
                _cmo2.Add(GetCmo1(candles, index));
            }

            _cmo1[index] = GetCmo1(candles, index);
            _cmo2[index] = GetCmo2(candles, index);

            if (index <= _period.ValueInt)
            {
                return 0;
            }

            for (int i = index - _period.ValueInt +1; i < index +1; i++)
            {
                sum1 = sum1 + _cmo1[i];
                sum2 = sum2 + _cmo2[i];
            }

            if (sum1 + sum2 == 0)
            {
                return 0;
            }
            else
            {
                value = (sum1 - sum2) / (sum1 + sum2) * 100;
            }

            return Math.Round(value,2);
        }

        private decimal GetCmo1(List<Candle> candles, int index)
        {
            decimal diff = 0;
            decimal cmo1 = 0;

            if (index > 1)
            {
                diff = candles[index].Close - candles[index - 1].Close;
            }

            if (diff > 0)
            {
                cmo1 = diff;
            }
            if (diff < 0)
            {
                cmo1 = 0;
            }
            if (diff == 0)
            {
                cmo1 = 0;
            }

            return cmo1;

        }

        private decimal GetCmo2(List<Candle> candles, int index)
        {
            decimal diff = 0;
            decimal cmo2 = 0;


            if (index > 1)
            {
                diff = candles[index].Close - candles[index - 1].Close;
            }

            if (diff > 0)
            {
                cmo2 = 0;
            }
            if (diff < 0)
            {
                cmo2 = -diff;
            }
            if (diff == 0)
            {
                cmo2 = 0;
            }

            return cmo2;

        }
        private List<decimal> _cmo1 = new List<decimal>();

        private List<decimal> _cmo2 = new List<decimal>();

       
    }
}
