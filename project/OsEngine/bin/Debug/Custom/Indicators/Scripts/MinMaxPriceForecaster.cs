using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace OsEngine.Indicators
{
    [Indicator("MinMaxPriceForecaster")]
    public class MinMaxPriceForecaster : Aindicator
    {
        private IndicatorParameterInt _lookBack;
        private IndicatorParameterInt _smoothPeriod;
        private IndicatorParameterInt _barsForecast;
        private IndicatorParameterDecimal _probability;
        private IndicatorParameterBool _useSmoothedPrice;
        private IndicatorParameterInt _periodPriceSmooth;
        private IndicatorDataSeries _seriesMin;
        private IndicatorDataSeries _seriesMax;
        private IndicatorDataSeries _seriesMinSmooth;
        private IndicatorDataSeries _seriesMaxSmooth;

        private List<double> _logReturns = new List<double>();
        private int _n;
        private DateTime _lastTime = DateTime.MinValue;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lookBack = CreateParameterInt("Length LookBack", 200);
                _barsForecast = CreateParameterInt("Bars Forecast", 6);
                _probability = CreateParameterDecimal("Probability", 0.95m);
                _smoothPeriod = CreateParameterInt("Smooth Period", 5);
                _useSmoothedPrice = CreateParameterBool("Use Smoothed Price", false);
                _periodPriceSmooth = CreateParameterInt("Period Price Smooth", 12);
                _seriesMin = CreateSeries("MinPrice", Color.Red, IndicatorChartPaintType.Line, true);
                _seriesMax = CreateSeries("MaxPrice", Color.Green, IndicatorChartPaintType.Line, true);
                _seriesMinSmooth = CreateSeries("MinPriceSmooth", Color.Orange, IndicatorChartPaintType.Line, true);
                _seriesMaxSmooth = CreateSeries("MaxPriceSmooth", Color.LightGreen, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (_barsForecast.ValueInt < 1)
                throw new ArgumentException("Prediction interval must be greater or equal then 1");
            if (_probability.ValueDecimal <= 0 || _probability.ValueDecimal >= 1)
                throw new ArgumentException("Confidence level must be between 0 and 1.");
            if (index <= 0)
                return;

            // Skip if we've already processed this candle
            if (candles[index].TimeStart == _lastTime)
                return;

            _lastTime = candles[index].TimeStart;

            decimal currentPrice = candles[index].Close;
            decimal previousPrice = candles[index - 1].Close;

            // Calculate log return
            if (previousPrice != 0)
            {
                double logReturn = Math.Log((double)(currentPrice / previousPrice));
                _logReturns.Add(logReturn);
            }

            _n = _barsForecast.ValueInt;

            // Not enough data yet
            if (_lookBack.ValueInt > index || _logReturns.Count < 2)
            {
                _seriesMin.Values[index] = 0;
                _seriesMax.Values[index] = 0;
                _seriesMinSmooth.Values[index] = 0;
                _seriesMaxSmooth.Values[index] = 0;
                return;
            }

            // Trim log returns to lookback period
            if (_logReturns.Count > _lookBack.ValueInt)
                _logReturns.RemoveRange(0, _logReturns.Count - _lookBack.ValueInt);

            // Calculate statistics
            double mean = _logReturns.Average();
            double stdDev = CalculateStandardDeviation(_logReturns);

            double totalMean = mean * _n;
            double totalStd = stdDev * Math.Sqrt(_n);

            // Calculate normal distribution quantiles
            double alpha = (1 - (double)_probability.ValueDecimal) / 2;
            double z = InverseNormalCdf(alpha);

            // Calculate price boundaries
            double lowerLogReturn = totalMean - z * totalStd;
            double upperLogReturn = totalMean + z * totalStd;

            if (_useSmoothedPrice.ValueBool)
            {
                currentPrice = candles.GetRange(index - _periodPriceSmooth.ValueInt, _periodPriceSmooth.ValueInt).Average(c => c.Close);
            }
            
            decimal lowerPrice = currentPrice * (decimal)Math.Exp(lowerLogReturn);
            decimal upperPrice = currentPrice * (decimal)Math.Exp(upperLogReturn);

            _seriesMax.Values[index] = upperPrice;
            _seriesMin.Values[index] = lowerPrice;

            // Calculate smoothed values
            if (_smoothPeriod.ValueInt <= 0 || index < _smoothPeriod.ValueInt) return;
            decimal smoothMin = 0, smoothMax = 0;
            int count = 0;

            for (int i = index; i > index - _smoothPeriod.ValueInt && i >= 0; i--)
            {
                if (_seriesMax.Values[i] == 0 || _seriesMin.Values[i] == 0) continue;
                smoothMax += _seriesMax.Values[i];
                smoothMin += _seriesMin.Values[i];
                count++;
            }

            if (count <= 0) return;
            _seriesMaxSmooth.Values[index] = smoothMax / count;
            _seriesMinSmooth.Values[index] = smoothMin / count;
        }

        // Calculate standard deviation
        private static double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count <= 1)
                return 0;

            double mean = values.Average();
            double sumOfSquares = values.Sum(v => (v - mean) * (v - mean));
            double variance = sumOfSquares / (values.Count - 1); // Unbiased estimator
            return Math.Sqrt(variance);
        }

        // Approximation of inverse normal CDF (Acklam's method)
        private static double InverseNormalCdf(double p)
        {
            if (p <= 0 || p >= 1)
                return p <= 0 ? double.NegativeInfinity : double.PositiveInfinity;

            const double a1 = -39.6968302866538;
            const double a2 = 220.946098424521;
            const double a3 = -275.928510446969;
            const double a4 = 138.357751867269;
            const double a5 = -30.6647980661472;
            const double a6 = 2.50662827745924;

            const double b1 = -54.4760987982241;
            const double b2 = 161.585836858041;
            const double b3 = -155.698979859887;
            const double b4 = 66.8013118877197;
            const double b5 = -13.2806815528857;

            const double c1 = -0.00778489400243029;
            const double c2 = -0.322396458041136;
            const double c3 = -2.40075827716184;
            const double c4 = -2.54973253934373;
            const double c5 = 4.37466414146497;
            const double c6 = 2.93816398269878;

            const double d1 = 0.00778469570904146;
            const double d2 = 0.32246712907004;
            const double d3 = 2.445134137143;
            const double d4 = 3.75440866190742;

            double q = p - 0.5;

            if (Math.Abs(q) <= 0.425)
            {
                double r = 0.180625 - q * q;
                return q * (((((((a1 * r + a2) * r + a3) * r + a4) * r + a5) * r + a6) * r) /
                                (((((((b1 * r + b2) * r + b3) * r + b4) * r + b5) * r + 1))));
            }

            double r2 = q < 0 ? p : 1 - p;
            r2 = Math.Sqrt(-Math.Log(r2));

            double result = (((((((c1 * r2 + c2) * r2 + c3) * r2 + c4) * r2 + c5) * r2 + c6) /
                                  (((((((d1 * r2 + d2) * r2 + d3) * r2 + d4) * r2 + 1))))));
            return q < 0 ? -result : result;
        }
    }
}