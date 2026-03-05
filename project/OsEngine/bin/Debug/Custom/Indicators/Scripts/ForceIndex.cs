using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    [Indicator("ForceIndex")]
    public class ForceIndex:Aindicator
    {
        private IndicatorParameterInt _period;

        private IndicatorDataSeries _series;

        private IndicatorParameterString _candlePoint;

        private List<decimal> _forceValues = new List<decimal>();

        private List<decimal> _emaValues = new List<decimal>();

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
            while (_forceValues.Count <= index)
            {
                _forceValues.Add(0);
            }

            while (_emaValues.Count <= index)
            {
                _emaValues.Add(0);
            }

            if (index == 0)
            {
                _forceValues[index] = 0;
                _emaValues[index] = 0;
                return 0;
            }

            _forceValues[index] = CalculateForceValue(candles, index);
            _emaValues[index] = CalculateEMA(index);

            return Math.Round(_emaValues[index], 2);
        }

        private decimal CalculateForceValue(List<Candle> candles, int index)
        {
            decimal priceValue = 0;
            decimal prevPriceValue = 0;

            if (index == 0)
            {
                return 0;
            }

            if (_candlePoint.ValueString == "Close")
            {
                priceValue = candles[index].Close;
                prevPriceValue = candles[index - 1].Close;
            }
            if (_candlePoint.ValueString == "Open")
            {
                priceValue = candles[index].Open;
                prevPriceValue = candles[index - 1].Open;
            }
            if (_candlePoint.ValueString == "High")
            {
                priceValue = candles[index].High;
                prevPriceValue = candles[index - 1].High;
            }
            if (_candlePoint.ValueString == "Low")
            {
                priceValue = candles[index].Low;
                prevPriceValue = candles[index - 1].Low;
            }

            if (priceValue == 0)
            {
                return 0;
            }

            return (1 - prevPriceValue / priceValue) * candles[index].Volume;
        }

        private decimal CalculateEMA(int index)
        {
            if (index < _period.ValueInt
                || index >= _forceValues.Count)
            {
                return 0;
            }

            if (index == _period.ValueInt)
            {
                decimal sum = 0;

                for (int i = index - _period.ValueInt + 1; i < index + 1; i++)
                {
                    sum += _forceValues[i];
                }

                decimal average = sum / _period.ValueInt;
                return Math.Round(average, 8);
            }

            if (index > _period.ValueInt)
            {
                decimal multiplier = Math.Round(2.0m / (_period.ValueInt + 1), 8);
                decimal previousEMA = _emaValues[index - 1];
                decimal result = previousEMA + multiplier * (_forceValues[index] - previousEMA);
                return Math.Round(result, 8);
            }

            return 0;
        }
    }
}