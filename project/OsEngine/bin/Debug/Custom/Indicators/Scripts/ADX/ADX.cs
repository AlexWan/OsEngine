using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class ADX : Aindicator
    {
        public IndicatorParameterInt _length;
        public IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _length = CreateParameterInt("Length", 14);

                _series = CreateSeries("ADX", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
            }
            else if (state == IndicatorState.Dispose)
            {
                _dmjPlus = null;
                _dmjPlusAverage = null;
                _dmjMinus = null;
                _dmjMinusAverage = null;
                _trueRange = null;
                _trueRangeAverage = null;
                _sDIjPlus = null;
                _sDIjMinus = null;
                _dX = null;
                _adX = null;
            }
        }
        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValueStandart(candles, index);
        }

        public List<decimal> ValuesDiPlus
        {
            get
            {
                return new List<decimal>(_sDIjPlus);
            }
        }

        public List<decimal> ValuesDiMinus
        {
            get
            {
                return new List<decimal>(_sDIjMinus);
            }
        }

        private List<decimal> _dmjPlus;
        private List<decimal> _dmjPlusAverage;
        private List<decimal> _dmjMinus;
        private List<decimal> _dmjMinusAverage;
        private List<decimal> _trueRange;
        private List<decimal> _trueRangeAverage;
        private List<decimal> _sDIjPlus;
        private List<decimal> _sDIjMinus;
        private List<decimal> _dX;
        private List<decimal> _adX;

        public decimal GetValueStandart(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                _dmjPlus = null;
                _dmjMinus = null;
                _trueRange = null;
                _sDIjPlus = null;
                _sDIjMinus = null;
                _dX = null;
                _adX = null;
            }
            // 1 counting new directional movements
            // 1 рассчитываем новые направленные движения
            DmjReload(candles, index);

            _dmjPlusAverage = MovingAverageWild(_dmjPlus, _dmjPlusAverage, _length.ValueInt, index);
            _dmjMinusAverage = MovingAverageWild(_dmjMinus, _dmjMinusAverage, _length.ValueInt, index);
            // 2 calculate true range
            // 2 рассчитываем истинный диапазон

            TrueRangeReload(candles, index);

            _trueRangeAverage = MovingAverageWild(_trueRange, _trueRangeAverage, _length.ValueInt, index);
            // 3 smoothing movement through true range 
            // 3 сглаживаем движение через истинный диапазон 

            SdijReload(index);


            //_mdiPlus = MovingAverageWild(_sDIjPlus, _mdiPlus, Lenght, index);
            //_mdiMinus = MovingAverageWild(_sDIjMinus, _mdiMinus, Lenght, index);

            // 5 making an array DX
            // 5 делаем массив DX

            DxReload(index);

            if (_length.ValueInt == 0 || _length.ValueInt > _dX.Count)
            {
                // if it's not possible to calculate
                // если рассчёт не возможен
                return 0;
            }
            else
            {
                // calculating
                // рассчитываем
                _adX = MovingAverageWild(_dX, _adX, _length.ValueInt, index);
                return Math.Round(_adX[_adX.Count - 1], 4);
            }
        }

        private void DmjReload(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                _dmjMinus = new List<decimal>();
                _dmjPlus = new List<decimal>();
                _dmjMinus.Add(0);
                _dmjPlus.Add(0);
                return;
            }

            if (index > _dmjMinus.Count - 1)
            {
                _dmjMinus.Add(0);
                _dmjPlus.Add(0);
            }

            decimal upMove = candles[index].High - candles[index - 1].High;
            decimal downMove = candles[index - 1].Low - candles[index].Low;
            if (candles[index].High >= candles[index - 1].High
                &&
                candles[index].High - candles[index - 1].High >= candles[index - 1].Low - candles[index].Low
                )
            {
                _dmjPlus[_dmjPlus.Count - 1] = candles[index].High - candles[index - 1].High;
            }

            if (candles[index].Low <= candles[index - 1].Low
                &&
                candles[index].High - candles[index - 1].High <= candles[index - 1].Low - candles[index].Low
                )
            {
                _dmjMinus[_dmjMinus.Count - 1] = candles[index - 1].Low - candles[index].Low;
            }

        }

        private void TrueRangeReload(List<Candle> candles, int index)
        {

            // True range is the largest of following three values:/Истинный диапазон (True Range) есть наибольшая из следующих трех величин:
            // difference between current maximum and minimum;/разность между текущими максимумом и минимумом;
            // difference between previous closing price an current maximum/разность между предыдущей ценой закрытия и текущим максимумом;
            // difference between previous closing price and current minimum./разность между предыдущей ценой закрытия и текущим минимумом.

            if (index == 0)
            {
                _trueRange = new List<decimal>();
                _trueRange.Add(0);
                return;
            }

            if (index > _trueRange.Count - 1)
            {
                _trueRange.Add(0);
            }

            decimal hiToLow = Math.Abs(candles[index].High - candles[index].Low);
            decimal closeToHigh = Math.Abs(candles[index - 1].Close - candles[index].High);
            decimal closeToLow = Math.Abs(candles[index - 1].Close - candles[index].Low);

            _trueRange[_trueRange.Count - 1] = Math.Max(Math.Max(hiToLow, closeToHigh), closeToLow);
        }
        private void SdijReload(int index)
        {
            //if/если TRj не = 0, so/то +SDIj = +DMj / TRj; -SDIj = -DMj / TRj,
            // if/если TRj = 0, so/то +SDIj = 0, — SDIj = 0.

            if (index == 0)
            {
                _sDIjMinus = new List<decimal>();
                _sDIjPlus = new List<decimal>();
                _sDIjMinus.Add(0);
                _sDIjPlus.Add(0);
                return;
            }

            if (index > _sDIjMinus.Count - 1)
            {
                _sDIjMinus.Add(0);
                _sDIjPlus.Add(0);
            }

            decimal trueRange = _trueRange[index];
            decimal dmjiPlus = _dmjPlus[index];
            decimal dmjiMinus = _dmjMinus[index];

            trueRange = _trueRangeAverage[index];
            dmjiPlus = _dmjPlusAverage[index];
            dmjiMinus = _dmjMinusAverage[index];

            if (trueRange == 0)
            {
                _sDIjPlus[_sDIjPlus.Count - 1] = 0;
                _sDIjMinus[_sDIjMinus.Count - 1] = 0;
            }
            else
            {
                _sDIjPlus[_sDIjPlus.Count - 1] = Math.Round(100 * dmjiPlus / trueRange, 0);
                _sDIjMinus[_sDIjMinus.Count - 1] = Math.Round(100 * dmjiMinus / trueRange, 0);
            }
        }


        private List<decimal> MovingAverageWild(List<decimal> valuesSeries, List<decimal> moving, int length, int index)
        {
            length = _length.ValueInt;
            if (moving == null || length > valuesSeries.Count)
            {
                moving = new List<decimal>();
                for (int i = 0; i < index + 1; i++)
                {
                    moving.Add(0);
                }
            }
            else if (length == valuesSeries.Count)
            {
                // it's first value. Calculate as MA
                // это первое значение. Рассчитываем как простую машку

                decimal lastMoving = 0;

                for (int i = index; i > -1 && i > valuesSeries.Count - 1 - length; i--)
                {
                    lastMoving += valuesSeries[i];
                }
                if (lastMoving != 0)
                {
                    moving.Add(lastMoving / length);
                }
                else
                {
                    moving.Add(0);
                }
            }
            else
            {
                decimal lastValueMoving;
                decimal lastValueSeries = valuesSeries[valuesSeries.Count - 1];

                if (index > moving.Count - 1)
                {
                    lastValueMoving = moving[moving.Count - 1];
                    moving.Add(0);
                }
                else
                {
                    lastValueMoving = moving[moving.Count - 2];
                }

                moving[moving.Count - 1] = (lastValueMoving * (_length.ValueInt - 1) + lastValueSeries) / _length.ValueInt;
            }

            return moving;
        }

        private void DxReload(int index)
        {
            if (index == 0)
            {
                _dX = new List<decimal>();
                _dX.Add(0);
                return;
            }

            if (index > _dX.Count - 1)
            {
                _dX.Add(0);
            }

            if (_sDIjPlus[_sDIjPlus.Count - 1] == 0 ||
                _sDIjMinus[_sDIjMinus.Count - 1] == 0)
            {
                _dX[_dX.Count - 1] = 0;
            }
            else
            {
                _dX[_dX.Count - 1] = Math.Round((100 * Math.Abs(_sDIjPlus[_sDIjPlus.Count - 1] - _sDIjMinus[_sDIjMinus.Count - 1])) /
                                     Math.Abs(_sDIjPlus[_sDIjPlus.Count - 1] + _sDIjMinus[_sDIjMinus.Count - 1]));
            }
        }
    }
}