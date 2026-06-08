using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    [Indicator("StochasticMomentumIndex")]
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
                Period1 = CreateParameterInt("Period 1", 13);
                Period2 = CreateParameterInt("Period 2", 25);
                Period3 = CreateParameterInt("Period 3", 2);
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

                    _dm1.Clear(); _dm1 = null;
                    _diff1.Clear(); _diff1 = null;

                    _dm2.Clear(); _dm2 = null;
                    _diff2.Clear(); _diff2 = null;

                    _diffS.Clear(); _diffS = null;
                    _diffS2.Clear(); _diffS2 = null;
                }
            }
        }

        private List<decimal> _dm;

        private List<decimal> _diff;

        private List<decimal> _dm1;

        private List<decimal> _diff1;

        private List<decimal> _dm2;

        private List<decimal> _diff2;

        private List<decimal> _diffS;

        private List<decimal> _diffS2;

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (_dm == null || index == 1)
            {
                _dm = new List<decimal>();
                _diff = new List<decimal>();
                _diff1 = new List<decimal>();
                _dm1 = new List<decimal>();
                _diff2 = new List<decimal>();
                _dm2 = new List<decimal>();
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

            while (index >= _dm1.Count)
            { _dm1.Add(0); }

            while (index >= _diff1.Count)
            { _diff1.Add(0); }

            while (index >= _dm2.Count)
            { _dm2.Add(0); }

            while (index >= _diff2.Count)
            { _diff2.Add(0); }

            while (index >= _diffS.Count)
            { _diffS.Add(0); }

            while (index >= _diffS2.Count)
            { _diffS2.Add(0); }

            _dm[index] = GetDM(candles, index);
            _diff[index] = GetDiff(candles, index);

            _dm1[index] = GetAverage(_dm, index, Period2.ValueInt);
            _diff1[index] = GetAverage(_diff, index, Period2.ValueInt);

            _dm2[index] = GetAverage(_dm1, index, Period3.ValueInt);
            _diff2[index] = GetAverage(_diff1, index, Period3.ValueInt);

            _diffS[index] = GetDiffS(index); 
            _diffS2[index] = GetAverage(_diffS, index, Period4.ValueInt);

            SeriesOne.Values[index] = Math.Round(_diffS[index], 2);
            SeriesTwo.Values[index] = Math.Round(_diffS2[index], 2);
        }

        private decimal GetAverage(List<decimal> list, int index, int length)
        {
            decimal lastMoving = 0;

            for (int i = index; i > -1 && i > list.Count - 1 - length; i--)
            {
                lastMoving += list[i];
            }
            return lastMoving / length;
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
            return candles[index].Close - (hi + low) /2;
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
            if (index < Period2.ValueInt + Period3.ValueInt + Period4.ValueInt + 3 
                || _dm2[index] == 0 ||  _diff2[index] == 0)
            {
                return 0;
            }

            return 100 * _dm2[index] / _diff2[index];
        }
    }
}