using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class LinearRegressionLine : Aindicator
    {
        private IndicatorParameterInt _period;

        private IndicatorDataSeries _seriesRegressionLine;

        private IndicatorParameterString _candlePoint;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _period = CreateParameterInt("Period", 14);
                _candlePoint = CreateParameterStringCollection("Candle Point", "Close", Entity.CandlePointsArray);

                _seriesRegressionLine = CreateSeries("Regression Line ", Color.HotPink,
                    IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index - _period.ValueInt <= 0)
            {
                return;
            }

            // variables
            decimal a,
                b,
                c,
                sumy = 0.0m,
                sumx = 0.0m,
                sumxy = 0.0m,
                sumx2 = 0.0m,
                h = 0.0m,
                l = 0.0m;
            int x;


            for (int i = index - _period.ValueInt + 1, g = 0; i < index + 1; i++, g++)
            {
                sumy += candles[i].GetPoint(_candlePoint.ValueString);
                sumxy += candles[i].GetPoint(_candlePoint.ValueString) * g;
                sumx += g;
                sumx2 += g * g;
            }

            c = sumx2 * _period.ValueInt - sumx * sumx;

            if (c == 0.0m)
            {
                return;
            }

            b = (sumxy * _period.ValueInt - sumx * sumy) / c;
            a = (sumy - sumx * b) / _period.ValueInt;

            _seriesRegressionLine.Values[index] = a + b * -(index - _period.ValueInt + 1 - index);

        }
    }
}
