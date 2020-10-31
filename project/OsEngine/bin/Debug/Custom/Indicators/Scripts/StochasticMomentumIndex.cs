using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class StochasticMomentumIndex : Aindicator
    {
        public IndicatorParameterInt Period1;
        public IndicatorParameterInt Period2;
        public IndicatorParameterInt Period3;
        public IndicatorParameterInt Period4;

        public IndicatorDataSeries SeriesOne;
        public IndicatorDataSeries SeriesTwo;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                Period1 = CreateParameterInt("Period 1", 5);
                Period2 = CreateParameterInt("Period 2", 3);
                Period3 = CreateParameterInt("Period 3", 3);
                Period4 = CreateParameterInt("Period 4", 3);

                SeriesOne = CreateSeries("Stochastic", Color.BlueViolet, IndicatorChartPaintType.Line, true);
                SeriesTwo = CreateSeries("Stochastic Signal Line", Color.Green, IndicatorChartPaintType.Line, true);
            }
            else if (state == IndicatorState.Dispose)
            {
                if (_dm != null)
                {
                    _dm.Clear(); _dm = null;
                    _diff.Clear(); _diff = null;

                    _dms.Clear(); _dms = null;
                    _dms2.Clear(); _dms2 = null;

                    _diffS.Clear(); _diffS = null;
                    _diffS2.Clear(); _diffS2 = null;
                }
            }
        }


        //Close- High + Low
        private List<decimal> _dm;

        // High - Low
        private List<decimal> _diff;

        //сглаживание Close - High + Low
        private List<decimal> _dms;

        //средн€€ дл€ сглаживани€ High - low
        private List<decimal> _dms2;

        // перва€ лини€
        private List<decimal> _diffS;

        //средн€€ дл€ сглаживани€ _diffs
        private List<decimal> _diffS2;

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (_dm == null || index == 1)
            {
                _dm = new List<decimal>();
                _diff = new List<decimal>();
                _dms2 = new List<decimal>();
                _dms = new List<decimal>();
                _diffS = new List<decimal>();
                _diffS2 = new List<decimal>();
            }

            if (index <= Period1.ValueInt
                || index <= Period2.ValueInt
                || index <= Period3.ValueInt)
            {
                return;
            }

            while (index >= _dm.Count)
            { _dm.Add(0); }

            while (index >= _diff.Count)
            { _diff.Add(0); }

            while (index >= _dms.Count)
            { _dms.Add(0); }

            while (index >= _dms2.Count)
            { _dms2.Add(0); }

            while (index >= _diffS.Count)
            { _diffS.Add(0); }

            while (index >= _diffS2.Count)
            { _diffS2.Add(0); }

            _dm[index] = GetDM(candles, index);
            _diff[index] = GetDiffS(index);

            _dms[index] = GetAverage(_dm, index, Period2.ValueInt, Period1.ValueInt);
            _dms2[index] = GetAverage(_dms, index, Period2.ValueInt, Period4.ValueInt);

            _diffS[index] = GetDiff(candles, index);
            _diffS2[index] = GetAverage(_diffS, index, Period3.ValueInt, Period4.ValueInt);

            SeriesOne.Values[index] = Math.Round(_diffS[index], 2);
            SeriesTwo.Values[index] = Math.Round(_diffS2[index], 2);
        }

        private decimal GetAverage(List<decimal> list, int index, int length, int length1)
        {
            decimal lastMoving = 0;

            for (int g = index; g > -1 && g > list.Count - 1 - length1; g--)
            {
                lastMoving += list[g];
            }

            for (int i = index; i > -1 && i > list.Count - 1 - length; i--)
            {
                lastMoving += list[i];
            }
            return lastMoving / length / length1;
        }

        private decimal GetDM(List<Candle> candles, int index)
        {
            if (index - Period1.ValueInt + 1 <= 0)
            {
                return 0;
            }

            decimal low = decimal.MaxValue;

            for (int i = index - Period1.ValueInt + 1; i < index + 1; i++)
            {
                if (candles[i].Low < low)
                {
                    low = candles[i].Low;
                }
            }

            decimal hi = 0;

            for (int i = index - Period1.ValueInt + 1; i < index + 1; i++)
            {
                if (candles[i].High > hi)
                {
                    hi = candles[i].High;
                }
            }
            return candles[index].Close - hi + low;
        }

        private decimal GetDiff(List<Candle> candles, int index)
        {
            if (index - Period1.ValueInt + 1 <= 0)
            {
                return 0;
            }

            decimal low = decimal.MaxValue;

            for (int i = index - Period1.ValueInt + 1; i < index + 1; i++)
            {
                if (candles[i].Low < low)
                {
                    low = candles[i].Low;
                }
            }

            decimal hi = 0;

            for (int i = index - Period1.ValueInt + 1; i < index + 1; i++)
            {
                if (candles[i].High > hi)
                {
                    hi = candles[i].High;
                }
            }
            return hi - low;
        }

        private decimal GetDiffS(int index)
        {
            if (index < Period2.ValueInt + Period3.ValueInt + Period4.ValueInt + 3 ||
                _dms2[index] == 0 ||
                _diffS2[index] == 0)
            {
                return 0;
            }

            return (100 * _dms2[index]) / (0.5m * _diffS2[index]);
        }
    }
}
