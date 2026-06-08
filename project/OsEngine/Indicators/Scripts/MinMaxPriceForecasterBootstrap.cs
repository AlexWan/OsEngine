using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace OsEngine.Indicators
{
    [Indicator("MinMaxPriceForecasterBootstrap")]
    public class MinMaxPriceForecasterBootstrap : Aindicator
    {
        private IndicatorParameterInt _lookBack;
        private IndicatorParameterInt _smoothPeriod;
        private IndicatorParameterInt _barsForecast;
        private IndicatorParameterDecimal _probability;
        private IndicatorParameterInt _iterations;
        private IndicatorParameterBool _useSmoothedPrice;
        private IndicatorParameterInt _periodPriceSmooth;
        private IndicatorDataSeries _seriesMin;
        private IndicatorDataSeries _seriesMax;
        private IndicatorDataSeries _seriesMinSmooth;
        private IndicatorDataSeries _seriesMaxSmooth;
        private readonly List<double> _logReturns = new List<double>();
        private int _n;
        private DateTime _lastTime = DateTime.MinValue;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lookBack = CreateParameterInt("Length LookBack", 300);
                _barsForecast = CreateParameterInt("Bars To Forecast", 3);
                _probability = CreateParameterDecimal("Probability", 0.95m);
                _smoothPeriod = CreateParameterInt("Smooth Period", 5);
                _iterations = CreateParameterInt("Bootstrap iterations", 1000);
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
            
            if (index <= 1)
                return;

            // Skip if we've already processed this candle
            if (candles[index].TimeStart == _lastTime)
                return;

            _lastTime = candles[index].TimeStart;

            decimal currentPrice = candles[index].Open;
            decimal previousPrice = candles[index - 1].Open;

            // Calculate log return
            if (previousPrice != 0)
            {
                double logReturn = Math.Log((double)(currentPrice / previousPrice));
                _logReturns.Add(logReturn);
            }

            _n = _barsForecast.ValueInt;

            // Not enough data yet
            if (_lookBack.ValueInt > index || _logReturns.Count < _lookBack.ValueInt)
            {
                SetZeroValues(index);
                return;
            }

            // Trim log returns to lookback period
            if (_logReturns.Count > _lookBack.ValueInt)
                _logReturns.RemoveRange(0, _logReturns.Count - _lookBack.ValueInt);

            if (_useSmoothedPrice.ValueBool)
            {
                currentPrice = candles.GetRange(index - _periodPriceSmooth.ValueInt, _periodPriceSmooth.ValueInt).Average(c => c.Close);
            }
            // Calculate both methods for comparison
            CalculateBootstrapForecast(currentPrice, index);

            // Calculate smoothed values
            CalculateSmoothedValues(index);
        }

        private void SetZeroValues(int index)
        {
            _seriesMin.Values[index] = 0;
            _seriesMax.Values[index] = 0;
            _seriesMinSmooth.Values[index] = 0;
            _seriesMaxSmooth.Values[index] = 0;
        }

        private void CalculateBootstrapForecast(decimal currentPrice, int index)
        {
            // Get actual number of iterations to use
            int bootstrapIterations = Math.Max(100, _iterations.ValueInt);

            // Pre-allocate the array for better performance
            double[] simulatedPrices = new double[bootstrapIterations];

            // Create ParallelOptions and set MaxDegreeOfParallelism
            ParallelOptions parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            // Bootstrap simulation
            Parallel.For(0, bootstrapIterations, parallelOptions, () => new Random(), (i, state, localRandom) =>
            {
                double totalLogReturn = 0;
                for (int j = 0; j < _n; j++)
                {
                    int randomIndex = localRandom.Next(_logReturns.Count);
                    totalLogReturn += _logReturns[randomIndex];
                }
                simulatedPrices[i] = (double)currentPrice * Math.Exp(totalLogReturn);
                return localRandom;
            }, _ => { });

            // Sort the simulated prices
            Array.Sort(simulatedPrices);

            // Calculate quantiles
            decimal lowerQuantile = (1 - _probability.ValueDecimal) / 2;
            decimal upperQuantile = 1 - lowerQuantile;

            int lowerIndex = (int)(bootstrapIterations * lowerQuantile);
            int upperIndex = (int)(bootstrapIterations * upperQuantile) - 1;

            // Ensure indices are within bounds
            lowerIndex = Math.Max(0, Math.Min(lowerIndex, bootstrapIterations - 1));
            upperIndex = Math.Max(0, Math.Min(upperIndex, bootstrapIterations - 1));

            _seriesMin.Values[index] = (decimal)simulatedPrices[lowerIndex];
            _seriesMax.Values[index] = (decimal)simulatedPrices[upperIndex];
        }

        private void CalculateSmoothedValues(int index)
        {
            if (_smoothPeriod.ValueInt <= 0 || index < _smoothPeriod.ValueInt)
                return;

            decimal smoothMin = 0, smoothMax = 0;
            int count = 0;

            for (int i = index; i > index - _smoothPeriod.ValueInt && i >= 0; i--)
            {
                if (_seriesMax.Values[i] == 0 || _seriesMin.Values[i] == 0)
                    continue;

                smoothMax += _seriesMax.Values[i];
                smoothMin += _seriesMin.Values[i];
                count++;
            }

            if (count <= 0) return;
            _seriesMaxSmooth.Values[index] = smoothMax / count;
            _seriesMinSmooth.Values[index] = smoothMin / count;
        }
    }
}