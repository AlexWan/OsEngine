using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{
    [Indicator("Mass_Index_MI")]
    public class Mass_Index_MI : Aindicator
    {
        private IndicatorParameterInt _lengthEma;

        private IndicatorParameterInt _periodSum;

        private IndicatorParameterDecimal _paramSeriesUp;

        private IndicatorParameterDecimal _paramSeriesDown;

        private IndicatorDataSeries _series1;

        private IndicatorDataSeries _series2;

        private IndicatorDataSeries _seriesMI;

        private IndicatorDataSeries _series27;

        private IndicatorDataSeries _series26_5;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lengthEma = CreateParameterInt("Length EMA", 9);
                _periodSum = CreateParameterInt("Summing period EMA", 25);

                _paramSeriesUp = CreateParameterDecimal("Parameter line up", 27);
                _paramSeriesDown = CreateParameterDecimal("Parameter line down", 26.5m);

                _series1 = CreateSeries("Ema1", Color.Red, IndicatorChartPaintType.Line, false);
                _series2 = CreateSeries("Ema2", Color.Red, IndicatorChartPaintType.Line, false);
                _seriesMI = CreateSeries("MI", Color.Red, IndicatorChartPaintType.Line, true);
                _series27 = CreateSeries("Line 27 series", Color.LightBlue, IndicatorChartPaintType.Line, true);
                _series26_5 = CreateSeries("Line 26.5 series", Color.LightBlue, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series27.Values[index] = _paramSeriesUp.ValueDecimal;
            _series26_5.Values[index] = _paramSeriesDown.ValueDecimal;

            if (index < _lengthEma.ValueInt || index < _periodSum.ValueInt)
                return;

            CalcFirstEMA(candles, index);

            if (_series2.Values.Count < _periodSum.ValueInt)
                return;

            CalcSecondEMA(_series1.Values, index);

            if (_seriesMI.Values.Count < _periodSum.ValueInt + 40)
                return;

            decimal MI = SumEma(_series1.Values, _series2.Values, index);

            _seriesMI.Values[index] = Math.Round(MI, 3);
        }

        public void CalcFirstEMA(List<Candle> candles, int index)
        {
            decimal result = 0;

            if (index == _lengthEma.ValueInt)
            {
                decimal lastMoving = 0;

                for (int i = index - _lengthEma.ValueInt + 1; i < index + 1; i++)
                {
                    lastMoving += candles[i].High - candles[i].Low;
                }
                lastMoving = lastMoving / _lengthEma.ValueInt;
                result = lastMoving;
                _series1.Values[index] = lastMoving;
            }
            else if (index > _lengthEma.ValueInt)
            {
                decimal a = Math.Round(2.0m / (_lengthEma.ValueInt + 1), 8);
                decimal emaLast = _series1.Values[index - 1];
                decimal p = candles[index].High - candles[index].Low;
                result = emaLast + (a * (p - emaLast));
            }

            result = Math.Round(result, 8);

            _series1.Values[index] = result;
        }

        private void CalcSecondEMA(List<decimal> values, int index)
        {
            decimal result = 0;

            if (index == _lengthEma.ValueInt)
            {
                decimal lastMoving = 0;

                for (int i = index - _lengthEma.ValueInt + 1; i < index + 1; i++)
                {
                    lastMoving += values[i];
                }
                lastMoving = lastMoving / _lengthEma.ValueInt;
                result = lastMoving;
                _series2.Values[index] = lastMoving;
            }
            else if (index > _lengthEma.ValueInt)
            {
                decimal a = Math.Round(2.0m / (_lengthEma.ValueInt + 1), 8);
                decimal emaLast = _series2.Values[index - 1];
                decimal p = values[index];
                result = emaLast + (a * (p - emaLast));
            }

            result = Math.Round(result, 8);

            _series2.Values[index] = result;
        }

        public decimal SumEma(List<decimal> ema1, List<decimal> ema2, int index)
        {
            decimal result = 0;

            for (int i = index - _periodSum.ValueInt; i < index + 1; i++)
            {
                if (ema2[i] != 0)
                    result += ema1[i] / ema2[i];

                else
                    result += ema1[i];
            }
            return result;
        }
    }
}