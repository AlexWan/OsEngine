using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class LinearRegressionChannel : Aindicator
    {
        private IndicatorParameterInt _period;
        private IndicatorParameterDecimal _upDeviation;
        private IndicatorParameterDecimal _downDeviation;

        private IndicatorDataSeries _seriesCentralLine;
        private IndicatorDataSeries _seriesUpperband;
        private IndicatorDataSeries _seriesLowerband;
        private IndicatorParameterString _candlePoint;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _period = CreateParameterInt("Lenght", 100);
                _candlePoint = CreateParameterStringCollection("Candle Point", "Close", Entity.CandlePointsArray);

                _upDeviation = CreateParameterDecimal("Up channel deviation", 2);
                _downDeviation = CreateParameterDecimal("Down channel deviation", -2);

                _seriesUpperband = CreateSeries("Up channel", Color.Aqua,
                    IndicatorChartPaintType.Line, true);
                _seriesUpperband.CanReBuildHistoricalValues = true;

                _seriesCentralLine = CreateSeries("Regression Line ", Color.Gold,
                    IndicatorChartPaintType.Line, true);
                _seriesCentralLine.CanReBuildHistoricalValues = true;

                _seriesLowerband = CreateSeries("Down channel", Color.OrangeRed,
                    IndicatorChartPaintType.Line, true);
                _seriesLowerband.CanReBuildHistoricalValues = true;
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index - _period.ValueInt <= 0)
            {
                return;
            }

            if (index - _period.ValueInt <= 0)
            {
                return;
            }

            DataClear();

            // variables
            decimal a, b, c,
                sumy = 0.0m,
                sumx = 0.0m,
                sumxy = 0.0m,
                sumx2 = 0.0m,
                h = 0.0m, l = 0.0m;
            int x;

            // calculate linear regression
            ;

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


            // Line equation    
            b = (sumxy * _period.ValueInt - sumx * sumy) / c;
            a = (sumy - sumx * b) / _period.ValueInt;

            // Linear regression line in buffer
            // Linear regression line in buffer
            for (int i = index - _period.ValueInt + 1; i < index + 1; i++)
            {
                _seriesCentralLine.Values[i] = a + b * -(index - _period.ValueInt + 1 - i);

            }

            decimal standartError = 0;
            for (int i = index - _period.ValueInt + 1; i < index + 1; i++)
            // Ќужно узнать рассто€ние от всех точек до линии регрессии за длину периода
            //Ќайденное рассто€ние сложить и поделить на длину периода
            {

                //Ќаходим точку(точку закрыти€ свечи)
                decimal point = candles[i].GetPoint(_candlePoint.ValueString);


                //Ќаходим точку на линии
                decimal pointLine = _seriesCentralLine.Values[i];


                //Ќаходим дистанцию между точками
                decimal distance = Math.Abs(point - pointLine);


                standartError = standartError + distance;

            }

            standartError = standartError / _period.ValueInt;

            for (int i = index - _period.ValueInt + 1; i < index + 1; i++)
            {
                _seriesUpperband.Values[i] = _seriesCentralLine.Values[i] +
                                             (standartError * _upDeviation.ValueDecimal);

                _seriesLowerband.Values[i] = _seriesCentralLine.Values[i] -
                                             (standartError * _downDeviation.ValueDecimal);

            }
        }

        private void DataClear()
        {
            for (int i = 0; i < _seriesCentralLine.Values.Count; i++)
            {
                _seriesCentralLine.Values[i] = 0;

            }

            for (int i = 0; i < _seriesUpperband.Values.Count; i++)
            {
                _seriesUpperband.Values[i] = 0;
            }


            for (int i = 0; i < _seriesLowerband.Values.Count; i++)
            {
                _seriesLowerband.Values[i] = 0;
            }
        }
    }
}
