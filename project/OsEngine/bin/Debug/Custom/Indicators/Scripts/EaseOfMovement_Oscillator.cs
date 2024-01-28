using OsEngine.Entity;
using OsEngine.Indicators;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace OsEngine.Charts.CandleChart.Indicators.Indicator
{
   // [IndicatorAttribute("EaseOfMovement_Oscillator")]
    internal class EaseOfMovement_Oscillator : Aindicator
    {
        public IndicatorParameterInt _periodMA;
        public IndicatorParameterInt _div;

        /// <summary>
        ///_isUseStandartDeviation - use standard deviation
        /// </summary>
        public IndicatorParameterBool _isUseStandartDeviation;

        public IndicatorDataSeries _seriesEMVUp;
        public IndicatorDataSeries _seriesEMVDown;
        public IndicatorDataSeries _seriesEmvStDevUp;
        public IndicatorDataSeries _seriesEmvStDevDown;
        private IndicatorDataSeries _seriesEomRaw;
        /// <summary>
        /// initialization
        /// </summary>
        /// <param name="state">Indicator Configure</param>     
        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _periodMA = CreateParameterInt("Period MA", 10);
                _div = CreateParameterInt("volumeDiv", 100000);

                _isUseStandartDeviation = CreateParameterBool("is Use Standart Deviation of EaseOfMovement SMA", true);

                _seriesEMVUp = CreateSeries("Series EaseOfMovement Up", Color.DarkGreen, IndicatorChartPaintType.Column, true);
                _seriesEMVDown = CreateSeries("Series EaseOfMovement Down", Color.DarkRed, IndicatorChartPaintType.Column, true);

                _seriesEmvStDevUp = CreateSeries("StDevUpLine", Color.Green, IndicatorChartPaintType.Line, true);
                _seriesEmvStDevDown = CreateSeries("StDevDownLine", Color.Red, IndicatorChartPaintType.Line, true);

                _seriesEomRaw = CreateSeries("Eom Raw", Color.Blue, IndicatorChartPaintType.Line, false);
            }
        }
        /// <summary>
        /// an iterator method to fill the indicator
        /// </summary>
        /// <param name="candles">collection candles</param>
        /// <param name="index">index to use in the collection of candles</param>
        public override void OnProcess(List<Candle> candles, int index)
        {
            if (_periodMA.ValueInt > index + 1)
            {
                return;
            }

            emvSMA = CalcEmv(candles, index);

            if (emvSMA > 0)
            {
                _seriesEMVUp.Values[index] = Math.Round(emvSMA, 5);
            }
            else if (emvSMA < 0)
            {
                _seriesEMVDown.Values[index] = Math.Round(emvSMA, 5);
            }

            if (_isUseStandartDeviation.ValueBool)
            {
                AddToListStandartDev();
                if (standardDeviationList.Count > 1)
                {
                    CalcStandardDeviation(standardDeviationList);
                    _seriesEmvStDevUp.Values[index] = (decimal)(emvStDev > 0 ? Math.Round(emvStDev, 5) : Math.Round(-emvStDev, 5));
                    _seriesEmvStDevDown.Values[index] = (decimal)(emvStDev < 0 ? Math.Round(emvStDev, 5) : Math.Round(-emvStDev, 5));
                }
            }
        }

        #region 
        decimal emvSMA;

        List<double> standardDeviationList = new List<double>();
        double emvStDev;
        #endregion
        /// <summary>
        /// Calculate Emv
        /// </summary>
        /// <param name="candles">collection candles</param>
        /// <param name="index">index to use in the collection of candles</param>
        public decimal CalcEmv(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                return 0;
            }

            decimal distMoved = DistanceMoved(candles, index);
            decimal boxRatio = GetBoxRatio(candles, index);

            if (boxRatio == 0)
            {
                return 0;
            }
            else
            {
                _seriesEomRaw.Values[index] = distMoved / boxRatio;
            }

            emvSMA = GetSma(_seriesEomRaw.Values, _periodMA.ValueInt, index);

            return emvSMA;
        }
 
        private decimal GetBoxRatio(List<Candle> candles, int index)
        {
            //getBoxRatio
            decimal vol = candles[index].Volume;

            decimal high = candles[index].High;
            decimal low = candles[index].Low;

            if (high - low == 0)
            {
                return 0;
            }

            decimal result = Math.Round((vol / _div.ValueInt) / (high - low), 5);

            return result;
        }

        private  decimal DistanceMoved(List<Candle> candles, int index)
        {
            //distanceMoved
            decimal high = candles[index].High;
            decimal low = candles[index].Low;

            decimal highMinOne = candles[index - 1].High;
            decimal lowMinOne = candles[index - 1].Low;

            return (high + low) / 2 - (highMinOne + lowMinOne) / 2;
        }

        /// <summary>
        /// Simple Moving Avg. of EaseOfMovement_Oscillator Values.
        /// </summary>
        private decimal GetSma(List<decimal> values, int lenght, int index)
        {
            decimal result = 0;

            int lenghtReal = 0;

            for (int i = index; i > 0 && i > index - lenght; i--)
            {
                result += values[i];
                lenghtReal++;
            }

            return result / lenghtReal; ;
        }
       
        /// <summary>
        ///calculate standard deviation
        /// </summary>
        /// <param name="standardDeviationList"></param>
        public void CalcStandardDeviation(IEnumerable<double> standardDeviationList)
        {
            if (standardDeviationList.Any())
            {
                double average = standardDeviationList.Average();
                double sum = standardDeviationList.Sum(d => Math.Pow(d - average, 2));
                emvStDev = Math.Sqrt((sum) / (standardDeviationList.Count() - 1)) * 2.5;
            }
        }
        /// <summary>
        /// add the StandardDev value to the collection for further calculations
        /// </summary>
        private void AddToListStandartDev()
        {
            if (standardDeviationList.Count < _periodMA.ValueInt)
            {
                standardDeviationList.Add((double)emvSMA);
            }
            else if (standardDeviationList.Count >= _periodMA.ValueInt)
            {
                standardDeviationList.RemoveAt(0);
                standardDeviationList.Add((double)emvSMA);
            }
        }
    }
}
