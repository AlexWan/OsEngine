using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class ForceIndex:Aindicator
    {
        private IndicatorParameterInt _period;
        private IndicatorDataSeries _series;
        private IndicatorParameterString _candlePoint;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _series = CreateSeries("FI", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
                _period = CreateParameterInt("Period", 13);
                _candlePoint = CreateParameterStringCollection("Candle point", "Close", Entity.CandlePointsArray);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValue(candles, index);
        }

        private decimal GetValue(List<Candle> candles, int index)
        {
            while (index >= _range.Count)
            {
                _range.Add(GetRange(candles, index));
            }
            while (index >= _movingAverage.Count)
            {
                _movingAverage.Add(GetEma1(index, _range));
            }

            if (index < _period.ValueInt || _movingAverage.Count < _period.ValueInt)
            {
                return 0;
            }

            if (_movingAverage[index] == 0 || _movingAverage[index - 1] == 0)
            {
                return 0;
            }

            return Math.Round(_movingAverage[index], 2);
        }

        private decimal GetRange(List<Candle> candles, int index)
        {
            decimal value = 0;

            if (index == 0)
            {
                return 0;
            }

            if (_candlePoint.ValueString == "Close")
            {
                value = (1 - candles[index - 1].Close / candles[index].Close) * candles[index].Volume;
            }
            if (_candlePoint.ValueString == "Open")
            {
                value = (1 - candles[index - 1].Open / candles[index].Open) * candles[index].Volume;
            }
            if (_candlePoint.ValueString == "High")
            {
                value = (1 - candles[index - 1].High / candles[index].High) * candles[index].Volume;
            }
            if (_candlePoint.ValueString == "Low")
            {
                value = (1 - candles[index - 1].Low / candles[index].Low) * candles[index].Volume;
            }

            return value;
        }

        private decimal GetEma1(int index, List<decimal> list)
        {
            decimal result = 0;
            if (index < _period.ValueInt)
            {
                return 0;
            }
            if (index == _period.ValueInt)
            {
                decimal lastMoving = 0;

                for (int i = index - _period.ValueInt + 1; i < index + 1; i++)
                {
                    lastMoving += list[i];
                }

                lastMoving = lastMoving / _period.ValueInt;
                result = lastMoving;
                return Math.Round(result, 8);
            }
            if (index > _period.ValueInt)
            {
                decimal a = Math.Round(2.0m / (_period.ValueInt + 1), 8);
                decimal emaLast = _movingAverage[index - 1];
                decimal p = list[index];
                result = emaLast + (a * (p - emaLast));
                return Math.Round(result, 8);
            }
            return Math.Round(result, 8);
        }

        private List<decimal> _movingAverage = new List<decimal>();
        private List<decimal> _range = new List<decimal>();

    }
}