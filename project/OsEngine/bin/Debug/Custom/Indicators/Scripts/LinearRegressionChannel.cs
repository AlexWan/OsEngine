﻿using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace OsEngine.Charts.CandleChart.Indicators.Indicator
{
   //[IndicatorAttribute("LinearRegressionChannel")]
    internal class LinearRegressionChannel : Aindicator
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
                _period = CreateParameterInt("Length", 100);
                _candlePoint = CreateParameterStringCollection("Candle Point", "Close", OsEngine.Indicators.Entity.CandlePointsArray);

                _upDeviation = CreateParameterDecimal("Up channel deviation", 2);
                _downDeviation = CreateParameterDecimal("Down channel deviation", 2);

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

            DataClear(index);

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
            // We need to find out the distance from all points to the regression line over the length of the period
            //Add the found distance and divide by the length of the period
            {
                if (i < 0 ||
                    i >= candles.Count)
                {
                    continue;
                }
                //Finding the point (closing point of the candle)
                decimal point = candles[i].GetPoint(_candlePoint.ValueString);

                //Finding a point on the line
                decimal pointLine = _seriesCentralLine.Values[i];

                //Finding the distance between points
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

        private void DataClear(int index)
        {
            for (int i = index - _period.ValueInt + 1; i < _seriesCentralLine.Values.Count; i++)
            {
                _seriesCentralLine.Values[i] = 0;
            }

            for (int i = index - _period.ValueInt + 1; i < _seriesUpperband.Values.Count; i++)
            {
                _seriesUpperband.Values[i] = 0;
            }

            for (int i = index - _period.ValueInt + 1; i < _seriesLowerband.Values.Count; i++)
            {
                _seriesLowerband.Values[i] = 0;
            }
        }
    }
}
