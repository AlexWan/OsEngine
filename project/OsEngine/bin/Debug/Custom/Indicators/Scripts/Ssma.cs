using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class Ssma : Aindicator
    {
        private IndicatorParameterInt _lenght;

        private IndicatorParameterString _candlePoint;

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            _lenght = CreateParameterInt("Length", 14);
            _candlePoint = CreateParameterStringCollection("Candle Point", "Close", Entity.CandlePointsArray);
            _series = CreateSeries("Ma", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            decimal result = 0;

            if (index == _lenght.ValueInt)
            {
                decimal lastMoving = 0;

                for (int i = index - _lenght.ValueInt + 1; i < index + 1; i++)
                {
                    lastMoving += candles[i].GetPoint(_candlePoint.ValueString);
                }

                lastMoving = lastMoving / _lenght.ValueInt;

                result = lastMoving;
            }
            else if (index > _lenght.ValueInt)
            {
                decimal emaLast = _series.Values[index - 1];

                decimal p = candles[index].GetPoint(_candlePoint.ValueString);

                result = (emaLast * (_lenght.ValueInt - 1) + p) / _lenght.ValueInt;
            }

            _series.Values[index] = result;
        }
    }
}