using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class RVI : Aindicator
    {
        private IndicatorParameterInt _p1;

        private IndicatorDataSeries _seriesOne;
        private IndicatorDataSeries _seriesTwo;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _p1 = CreateParameterInt("Period", 5);

                _seriesOne = CreateSeries("Value 1", Color.DarkRed, IndicatorChartPaintType.Line, true);
                _seriesTwo = CreateSeries("Value 2", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
            }
            else if (state == IndicatorState.Dispose)
            {
                if (_moveAverage != null)
                {
                    _moveAverage = null;
                    _rangeAverage = null;
                    _rvi = null;
                }
            }
        }

        private List<decimal> _moveAverage;
        private List<decimal> _rangeAverage;
        private List<decimal> _rvi;

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (_moveAverage == null || index == 1)
            {
                _moveAverage = new List<decimal>();
                _rangeAverage = new List<decimal>();
                _rvi = new List<decimal>();
            }

            if (index <= _p1.ValueInt)
            {
                return;
            }

            while (index >= _moveAverage.Count)
            { _moveAverage.Add(0); }

            while (index >= _rangeAverage.Count)
            { _rangeAverage.Add(0); }

            while (index >= _rvi.Count)
            { _rvi.Add(0); }

            _moveAverage[index] = GetMoveAverage(candles, index);
            _rangeAverage[index] = GetRangeAverage(candles, index);
            _rvi[_rvi.Count - 1] = (GetRvi(index));

            _seriesOne.Values[index] = GetRvi(index);
            _seriesTwo.Values[index] = GetValueSecond(index);
        }

        private decimal GetValueSecond(int index)
        {
            if (index >= _p1.ValueInt + 6)
            {
                return Math.Round((_rvi[index] + 2 * _rvi[index - 1] + 2 * _rvi[index - 2] + _rvi[index - 3]) / 6, 2);
            }
            else
            {
                return 0;
            }

        }

        private decimal GetMoveAverage(List<Candle> candles, int index)
        {
            if (index > 3)
            {
                return (candles[index].Close - candles[index].Open) +
                   2 * (candles[index - 1].Close - candles[index - 1].Open) +
                   2 * (candles[index - 2].Close - candles[index - 2].Open) +
                     (candles[index - 3].Close - candles[index - 3].Open);
            }
            else
            {
                return 0;
            }

        }

        private decimal GetRangeAverage(List<Candle> candles, int index)
        {
            if (index > 3)
            {
                return (candles[index].High - candles[index].Low) +
                   2 * (candles[index - 1].High - candles[index - 1].Low) +
                   2 * (candles[index - 2].High - candles[index - 2].Low) +
                     (candles[index - 3].High - candles[index - 3].Low);
            }
            else
            {
                return 0;
            }
        }

        private decimal GetRvi(int index)
        {

            if (index - _p1.ValueInt + 1<= 0)
            {
                return 0;
            }

            decimal sumMa = 0;
            decimal sumRa = 0;

            for (int i = index - _p1.ValueInt+1; i < index+1; i++)
            {
                sumMa = sumMa + _moveAverage[i];
                sumRa = sumRa + _rangeAverage[i];
            }

            if (sumRa == 0 || sumMa == 0)
            {
                return 0;
            }

            return Math.Round(sumMa / sumRa, 2);
        }
    }
}