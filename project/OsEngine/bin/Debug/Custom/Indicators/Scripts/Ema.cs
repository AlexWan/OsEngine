using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    [Indicator("Ema")]
    public class Ema : Aindicator
    {
        private IndicatorParameterInt _length;

        private IndicatorParameterString _candlePoint;

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            _length = CreateParameterInt("Length", 14);
            _candlePoint = CreateParameterStringCollection("Candle Point", "Close", Entity.CandlePointsArray);
            _series = CreateSeries("Ema", Color.DarkRed, IndicatorChartPaintType.Line, true);
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            decimal result = 0;

            if (index == _length.ValueInt)
            {
                decimal lastMoving = 0;

                for (int i = index - _length.ValueInt + 1; i < index + 1; i++)
                {
                    lastMoving += candles[i].GetPoint(_candlePoint.ValueString);
                }

                lastMoving = lastMoving / _length.ValueInt;
                result = lastMoving;
            }
            else if (index > _length.ValueInt)
            {
                decimal a = Math.Round(2.0m / (_length.ValueInt + 1), 8);
                decimal emaLast = _series.Values[index - 1];
                decimal p = candles[index].GetPoint(_candlePoint.ValueString);
                result = emaLast + (a * (p - emaLast));
            }

            _series.Values[index] = Math.Round(result, 8);
        }
    }
}