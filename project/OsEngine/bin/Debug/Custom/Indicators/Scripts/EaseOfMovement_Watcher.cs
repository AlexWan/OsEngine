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

        public IndicatorParameterBool _isUseStandardDeviation;

        public IndicatorDataSeries _seriesEMVUp;

        public IndicatorDataSeries _seriesEMVDown;

        public IndicatorDataSeries _seriesEmvStDevUp;

        public IndicatorDataSeries _seriesEmvStDevDown;

        private IndicatorDataSeries _seriesEomRaw;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _periodMA = CreateParameterInt("Period MA", 10);

                _isUseStandardDeviation = CreateParameterBool("is Use Standard Deviation of EOM", true);

                _seriesEMVUp = CreateSeries("Series EaseOfMovement Up", Color.DarkGreen, IndicatorChartPaintType.Column, true);
                _seriesEMVDown = CreateSeries("Series EaseOfMovement Down", Color.DarkRed, IndicatorChartPaintType.Column, true);

                _seriesEmvStDevUp = CreateSeries("StDevUpLine", Color.Green, IndicatorChartPaintType.Line, true);
                _seriesEmvStDevDown = CreateSeries("StDevDownLine", Color.Red, IndicatorChartPaintType.Line, true);

                _seriesEomRaw = CreateSeries("Eom Raw", Color.Blue, IndicatorChartPaintType.Line, false);
            }
            else if (state == IndicatorState.Dispose)
            {
                if (_standardDeviationList != null)
                {
                    _standardDeviationList.Clear();
                    _standardDeviationList = null;
                }
            }
            
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (_standardDeviationList == null || index == 1)
            {
                _standardDeviationList = new List<decimal>();
            }
            
            if (_periodMA.ValueInt > index + 1)
            {
                return;
            }

            _emvSMA = CalcEmv(candles, index);

            if (_emvSMA > 0)
            {
                _seriesEMVUp.Values[index] = Math.Round(_emvSMA, 5);
            }
            else if (_emvSMA < 0)
            {
                _seriesEMVDown.Values[index] = Math.Round(_emvSMA, 5);
            }

            if (_isUseStandardDeviation.ValueBool)
            {
                AddToListStandardDev();
                if (_standardDeviationList.Count > 1)
                {
                    CalcStandardDeviation(_standardDeviationList);
                    _seriesEmvStDevUp.Values[index] = _emvStDev > 0 ? Math.Round(_emvStDev, 5) : Math.Round(-_emvStDev, 5);
                    _seriesEmvStDevDown.Values[index] = _emvStDev < 0 ? Math.Round(_emvStDev, 5) : Math.Round(-_emvStDev, 5);
                }
            }
        }

        private decimal _emvSMA;

        private List<decimal> _standardDeviationList;

        private decimal _emvStDev;

        private decimal CalcEmv(List<Candle> candles, int index)
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

            _emvSMA = GetSma(_seriesEomRaw.Values, _periodMA.ValueInt, index);

            return _emvSMA;
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

        private void CalcStandardDeviation(List<decimal> standardDeviationList)
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

                _emvStDev = (decimal)Math.Sqrt((double)sd / (length2 - 1)) * 2.5m;
            }
        }

        private void AddToListStandardDev()
        {
            _standardDeviationList.Add(_emvSMA);
        }
    }
}
