using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace OsEngine.Indicators
{
    [Indicator("EaseOfMovement_Watcher")]
    public class EaseOfMovement_Watcher : Aindicator
    {
        public IndicatorParameterInt _periodMA;

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

                _isUseStandartDeviation = CreateParameterBool("is Use Standart Deviation of EOM", true);

                _seriesEMVUp = CreateSeries("Series EaseOfMovement Up", Color.DarkGreen, IndicatorChartPaintType.Column, true);
                _seriesEMVDown = CreateSeries("Series EaseOfMovement Down", Color.DarkRed, IndicatorChartPaintType.Column, true);

                _seriesEmvStDevUp = CreateSeries("StDevUpLine", Color.Green, IndicatorChartPaintType.Line, true);
                _seriesEmvStDevDown = CreateSeries("StDevDownLine", Color.Red, IndicatorChartPaintType.Line, true);

                _seriesEomRaw = CreateSeries("Eom Raw", Color.Blue, IndicatorChartPaintType.Line, false);
            }
            else if (state == IndicatorState.Dispose)
            {
                if (standardDeviationList != null)
                {
                    standardDeviationList.Clear();
                    standardDeviationList = null;
                }
            }
            
        }

        /// <summary>
        /// an iterator method to fill the indicator
        /// </summary>
        /// <param name="candles">collection candles</param>
        /// <param name="index">index to use in the collection of candles</param>
        public override void OnProcess(List<Candle> candles, int index)
        {
            if (standardDeviationList == null || index == 1)
            {
                standardDeviationList = new List<decimal>();
            }
            
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
                    _seriesEmvStDevUp.Values[index] = emvStDev > 0 ? Math.Round(emvStDev, 5) : Math.Round(-emvStDev, 5);
                    _seriesEmvStDevDown.Values[index] = emvStDev < 0 ? Math.Round(emvStDev, 5) : Math.Round(-emvStDev, 5);
                }
            }
        }

        #region 
        decimal emvSMA;

        List<decimal> standardDeviationList;
        decimal emvStDev;
        #endregion
        /// <summary>
        /// Calculate Emv
        /// </summary>
        /// <param name="candles">collection candles</param>
        /// <param name="index">index to use in the collection of candles</param>
        public decimal CalcEmv(List<Candle> candles, int index)
        {
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

            decimal result = Math.Round((vol / 10000) / (high - low), 5);

            return result;
        }

        private decimal DistanceMoved(List<Candle> candles, int index)
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
        private decimal GetSma(List<decimal> values, int length, int index)
        {
            decimal result = 0;

            int lengthReal = 0;

            for (int i = index; i > 0 && i > index - length; i--)
            {
                result += values[i];
                lengthReal++;
            }

            return result / lengthReal; ;
        }

        /// <summary>
        ///calculate standard deviation
        /// </summary>
        /// <param name="standardDeviationList"></param>
        public void CalcStandardDeviation(List<decimal> standardDeviationList)
        {
            decimal sd = 0;
            int length2;
            if (standardDeviationList.Count <= _periodMA.ValueInt)
            {

                length2 = standardDeviationList.Count;
            }
            else
            {
                length2 = _periodMA.ValueInt;
            }

            if (standardDeviationList.Count > 1)
            {
                decimal average = 0;
                for (int i = standardDeviationList.Count - length2; i < standardDeviationList.Count; i++)
                {
                    average += standardDeviationList[i];
                }

                average = average / length2;

                for (int i = standardDeviationList.Count - length2; i < standardDeviationList.Count; i++)
                {
                    decimal x = standardDeviationList[i] - average;
                    double g = Math.Pow((double)x, 2.0);
                    sd += (decimal)g;
                }

                emvStDev = (decimal)Math.Sqrt((double)sd / (length2 - 1)) * 2.5m;
            }
        }
        /// <summary>
        /// add the StandardDev value to the collection for further calculations
        /// </summary>
        private void AddToListStandartDev()
        {
            standardDeviationList.Add(emvSMA);
        }
    }
}
