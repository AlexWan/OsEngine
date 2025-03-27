using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    [Indicator("LinearRegressionChannelFast_Indicator")]
    public class LinearRegressionChannelFast_Indicator : Aindicator
    {
        private IndicatorParameterInt _period;

        private IndicatorParameterDecimal _upDeviation;

        private IndicatorParameterDecimal _downDeviation;

        private IndicatorDataSeries _seriesCentralLine;

        private IndicatorDataSeries _seriesUpperBand;

        private IndicatorDataSeries _seriesLowerBand;

        private IndicatorParameterString _candlePoint;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _period = CreateParameterInt("Length", 100);
                _candlePoint = CreateParameterStringCollection("Candle Point", "Close", OsEngine.Indicators.Entity.CandlePointsArray);

                _upDeviation = CreateParameterDecimal("Up channel deviation", 2);
                _downDeviation = CreateParameterDecimal("Down channel deviation", 2);

                _seriesUpperBand = CreateSeries("Up channel", Color.Aqua,
                    IndicatorChartPaintType.Line, true);
                _seriesUpperBand.CanReBuildHistoricalValues = false;

                _seriesCentralLine = CreateSeries("Regression Line ", Color.Gold,
                    IndicatorChartPaintType.Line, false);
                _seriesCentralLine.CanReBuildHistoricalValues = false;

                _seriesLowerBand = CreateSeries("Down channel", Color.OrangeRed,
                    IndicatorChartPaintType.Line, true);
                _seriesLowerBand.CanReBuildHistoricalValues = false;
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

            try
            {
                // Linear regression line in buffer
                // Linear regression line in buffer
                for (int i = index - _period.ValueInt + 1; i < index + 1; i++)
                {
                    _seriesCentralLine.Values[i] = a + b * -(index - _period.ValueInt + 1 - i);
                }

                decimal standartError = 0;
                for (int i = index - _period.ValueInt + 1; i < index + 1; i++)
                // Нужно узнать расстояние от всех точек до линии регрессии за длину периода
                //Найденное расстояние сложить и поделить на длину периода
                {
                    //Находим точку(точку закрытия свечи)
                    decimal point = candles[i].GetPoint(_candlePoint.ValueString);

                    //Находим точку на линии
                    decimal pointLine = _seriesCentralLine.Values[i];

                    //Находим дистанцию между точками
                    decimal distance = Math.Abs(point - pointLine);

                    standartError = standartError + distance;
                }

                standartError = standartError / _period.ValueInt;

                _seriesUpperBand.Values[index] = _seriesCentralLine.Values[index] +
                                                 (standartError * _upDeviation.ValueDecimal);

                _seriesLowerBand.Values[index] = _seriesCentralLine.Values[index] -
                                                 (standartError * _downDeviation.ValueDecimal);
            }
            catch
            {
                return;
            }
        }
    }
}