using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{
    [Indicator("ADX")]
    public class ADX : Aindicator
    {
        public IndicatorParameterInt _length;

        public IndicatorDataSeries _series;

        public IndicatorDataSeries _seriesPlus;

        public IndicatorDataSeries _seriesMinus;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _length = CreateParameterInt("Length", 14);

                _series = CreateSeries("ADX", Color.ForestGreen, IndicatorChartPaintType.Line, true);
                _seriesPlus = CreateSeries("Plus", Color.Blue, IndicatorChartPaintType.Line, true);
                _seriesMinus = CreateSeries("Minus", Color.Red, IndicatorChartPaintType.Line, true);
            }
            else if (state == IndicatorState.Dispose)
            {
                _dmPlus?.Clear();
                _dmMinus?.Clear();
                _tr?.Clear();
                _smoothedTR?.Clear();
                _smoothedDMPlus?.Clear();
                _smoothedDMMinus?.Clear();
                _diPlus?.Clear();
                _diMinus?.Clear();
                _dx?.Clear();
                _adx?.Clear();
            }
        }

        private List<decimal> _dmPlus;
        private List<decimal> _dmMinus;
        private List<decimal> _tr;
        private List<decimal> _smoothedTR;
        private List<decimal> _smoothedDMPlus;
        private List<decimal> _smoothedDMMinus;
        private List<decimal> _diPlus;
        private List<decimal> _diMinus;
        private List<decimal> _dx;
        private List<decimal> _adx;

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                _dmPlus = new List<decimal>();
                _dmMinus = new List<decimal>();
                _tr = new List<decimal>();
                _smoothedTR = new List<decimal>();
                _smoothedDMPlus = new List<decimal>();
                _smoothedDMMinus = new List<decimal>();
                _diPlus = new List<decimal>();
                _diMinus = new List<decimal>();
                _dx = new List<decimal>();
                _adx = new List<decimal>();
            }

            while (_dmPlus.Count <= index)
            {
                _dmPlus.Add(0);
            }

            while (_dmMinus.Count <= index)
            {
                _dmMinus.Add(0);
            }

            while (_tr.Count <= index)
            {
                _tr.Add(0);
            }

            while (_smoothedTR.Count <= index)
            {
                _smoothedTR.Add(0);
            }

            while (_smoothedDMPlus.Count <= index)
            {
                _smoothedDMPlus.Add(0);
            }

            while (_smoothedDMMinus.Count <= index)
            {
                _smoothedDMMinus.Add(0);
            }

            while (_diPlus.Count <= index)
            {
                _diPlus.Add(0);
            }

            while (_diMinus.Count <= index)
            {
                _diMinus.Add(0);
            }

            while (_dx.Count <= index)
            {
                _dx.Add(0);
            }

            while (_adx.Count <= index)
            {
                _adx.Add(0);
            }

            CalculateADX(candles, index);

            _series.Values[index] = Math.Round(_adx[index], 4);
            _seriesPlus.Values[index] = Math.Round(_diPlus[index], 4);
            _seriesMinus.Values[index] = Math.Round(_diMinus[index], 4);
        }

        public List<decimal> ValuesDiPlus
        {
            get { return new List<decimal>(_diPlus); }
        }

        public List<decimal> ValuesDiMinus
        {
            get { return new List<decimal>(_diMinus); }
        }

        public List<decimal> ValuesADX
        {
            get { return new List<decimal>(_adx); }
        }

        private void CalculateADX(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                _dmPlus[0] = 0;
                _dmMinus[0] = 0;
                _tr[0] = 0;
                return;
            }

            // 1. Calculation of +DM and -DM
            decimal upMove = candles[index].High - candles[index - 1].High;
            decimal downMove = candles[index - 1].Low - candles[index].Low;

            if (upMove > downMove && upMove > 0)
            {
                _dmPlus[index] = upMove;
                _dmMinus[index] = 0;
            }
            else if (downMove > upMove && downMove > 0)
            {
                _dmMinus[index] = downMove;
                _dmPlus[index] = 0;
            }
            else
            {
                _dmPlus[index] = 0;
                _dmMinus[index] = 0;
            }

            // 2. True Range Calculation
            decimal highLow = candles[index].High - candles[index].Low;
            decimal highPrevClose = Math.Abs(candles[index].High - candles[index - 1].Close);
            decimal lowPrevClose = Math.Abs(candles[index].Low - candles[index - 1].Close);

            _tr[index] = Math.Max(highLow, Math.Max(highPrevClose, lowPrevClose));

            // 3. Smoothing values
            int length = _length.ValueInt;

            if (index < length)
            {
                if (index == 0)
                {
                    _smoothedTR[0] = _tr[0];
                    _smoothedDMPlus[0] = _dmPlus[0];
                    _smoothedDMMinus[0] = _dmMinus[0];
                }
                else
                {
                    decimal sumTR = 0;
                    decimal sumDMPlus = 0;
                    decimal sumDMMinus = 0;

                    for (int i = 0; i <= index; i++)
                    {
                        sumTR += _tr[i];
                        sumDMPlus += _dmPlus[i];
                        sumDMMinus += _dmMinus[i];
                    }

                    _smoothedTR[index] = sumTR / (index + 1);
                    _smoothedDMPlus[index] = sumDMPlus / (index + 1);
                    _smoothedDMMinus[index] = sumDMMinus / (index + 1);
                }
            }
            else
            {
                decimal prevSmoothedTR = _smoothedTR[index - 1];
                decimal prevSmoothedDMPlus = _smoothedDMPlus[index - 1];
                decimal prevSmoothedDMMinus = _smoothedDMMinus[index - 1];

                _smoothedTR[index] = (prevSmoothedTR * (length - 1) + _tr[index]) / length;
                _smoothedDMPlus[index] = (prevSmoothedDMPlus * (length - 1) + _dmPlus[index]) / length;
                _smoothedDMMinus[index] = (prevSmoothedDMMinus * (length - 1) + _dmMinus[index]) / length;
            }

            // 4. Calculation of +DI and -DI
            if (_smoothedTR[index] > 0)
            {
                _diPlus[index] = 100 * (_smoothedDMPlus[index] / _smoothedTR[index]);
                _diMinus[index] = 100 * (_smoothedDMMinus[index] / _smoothedTR[index]);
            }
            else
            {
                _diPlus[index] = 0;
                _diMinus[index] = 0;
            }

            // 5. DX calculation
            decimal diSum = _diPlus[index] + _diMinus[index];
            decimal diDiff = Math.Abs(_diPlus[index] - _diMinus[index]);

            if (diSum > 0)
            {
                _dx[index] = 100 * (diDiff / diSum);
            }
            else
            {
                _dx[index] = 0;
            }

            // 6. ADX calculation
            if (index < length * 2 - 1)
            {
                _adx[index] = 0;
            }
            else if (index == length * 2 - 1)
            {
                decimal sumDX = 0;

                for (int i = length; i <= index; i++)
                {
                    sumDX += _dx[i];
                }
                _adx[index] = sumDX / length;
            }
            else
            {
                decimal prevADX = _adx[index - 1];
                _adx[index] = (prevADX * (length - 1) + _dx[index]) / length;
            }
        }
    }
}