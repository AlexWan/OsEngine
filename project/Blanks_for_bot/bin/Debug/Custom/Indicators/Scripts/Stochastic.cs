using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class Stochastic : Aindicator
    {
        public IndicatorParameterInt Period1;
        public IndicatorParameterInt Period2;
        public IndicatorParameterInt Period3;

        public IndicatorDataSeries SeriesOne;
        public IndicatorDataSeries SeriesTwo;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                Period1 = CreateParameterInt("Period 1", 5);
                Period2 = CreateParameterInt("Period 2", 3);
                Period3 = CreateParameterInt("Period 3", 3);

                SeriesOne = CreateSeries("K value", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
                SeriesTwo = CreateSeries("K average", Color.DarkRed, IndicatorChartPaintType.Line, true);
            }
            else if (state == IndicatorState.Dispose)
            {
                if (_t1 != null)
                {
                    _t1.Clear(); _t1 = null;
                    _t2.Clear(); _t2 = null;

                    _tM1.Clear(); _tM1 = null;
                    _tM2.Clear(); _tM2 = null;

                    _k.Clear(); _k = null;
                    _kM.Clear(); _kM = null;
                }
            }
        }

        /// <summary>
        /// to keep the difference Close- Low
        /// для хранения разницы клоуз - лоу
        /// </summary>
        private List<decimal> _t1;

        /// <summary>
        /// to keep the difference High - Low
        /// для хранения разницы хай - лоу
        /// </summary>
        private List<decimal> _t2;

        /// <summary>
        /// ma for smoothing Close - Low
        /// машка для сглаживания клоуз - лоу
        /// </summary>
        private List<decimal> _tM1;

        /// <summary>
        /// ma for smoothing High - low
        /// машка для сглаживания хай - лоу
        /// </summary>
        private List<decimal> _tM2;

        /// <summary>
        /// first line
        /// первая линия
        /// </summary>
        private List<decimal> _k;

        /// <summary>
        /// ma for smoothing K
        /// машкая для сглаживания К
        /// </summary>
        private List<decimal> _kM;

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (_t1 == null || index == 1)
            {
                _t1 = new List<decimal>();
                _t2 = new List<decimal>();
                _tM1 = new List<decimal>();
                _tM2 = new List<decimal>();
                _k = new List<decimal>();
                _kM = new List<decimal>();
            }

            if (index <= Period1.ValueInt
                || index <= Period2.ValueInt
                || index <= Period3.ValueInt)
            {
                return;
            }

            while (index >= _t1.Count)
            { _t1.Add(0);}

            while (index >= _t2.Count)
            { _t2.Add(0); }

            while (index >= _tM1.Count)
            { _tM1.Add(0); }

            while (index >= _tM2.Count)
            { _tM2.Add(0); }

            while (index >= _k.Count)
            { _k.Add(0); }

            while (index >= _kM.Count)
            { _kM.Add(0); }

            _t1[index] = GetT1(candles, index);
            _t2[index] = GetT2(candles, index);

            _tM1[index] = GetAverage(_t1, index, Period2.ValueInt);
            _tM2[index] = GetAverage(_t2, index, Period2.ValueInt);

            _k[index] = GetK(index);
            _kM[index] = GetAverage(_k, index, Period3.ValueInt);

            SeriesOne.Values[index] = Math.Round(_k[index], 2);
            SeriesTwo.Values[index] = Math.Round(_kM[index], 2);
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

        private decimal GetT1(List<Candle> candles, int index)
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

            return candles[index].Close - low;
        }

        private decimal GetT2(List<Candle> candles, int index)
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

        private decimal GetK(int index)
        {
            if (index < Period2.ValueInt + Period3.ValueInt + 3 ||
                _tM2[index] == 0 ||
                _tM1[index] == 0)
            {
                return 0;
            }

            return 100 * _tM1[index] / _tM2[index];
        }
    }
}