using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    class ATR : Aindicator
    {

        private IndicatorParameterInt _lenght;


        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lenght = CreateParameterInt("Lenght", 14);
                _series = CreateSeries("Atr value", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
            }
            else if (state == IndicatorState.Dispose)
            {
                if (_moving != null)
                {
                    _moving.Clear();
                }
                if (_trueRange != null)
                {
                    _trueRange.Clear();
                }
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            TrueRangeReload(candles, index);
            _moving = MovingAverageWild(_trueRange, _moving, _lenght.ValueInt, index);
            _series.Values[index] = Math.Round(_moving[index], 7);
        }

        private List<decimal> _moving = new List<decimal>();

        private List<decimal> _trueRange = new List<decimal>();

        private void TrueRangeReload(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                _trueRange = new List<decimal>();
                _trueRange.Add(0);
                return;
            }

            while (_trueRange.Count - 1 < index)
            {
                _trueRange.Add(0);
            }

            decimal hiToLow = Math.Abs(candles[index].High - candles[index].Low);
            decimal closeToHigh = Math.Abs(candles[index - 1].Close - candles[index].High);
            decimal closeToLow = Math.Abs(candles[index - 1].Close - candles[index].Low);

            _trueRange[index] = Math.Max(Math.Max(hiToLow, closeToHigh), closeToLow);
        }

        private List<decimal> MovingAverageWild(List<decimal> valuesSeries, List<decimal> moving, int length, int index)
        {
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
                decimal lastValueSeries = Math.Round(valuesSeries[index], 7);
                decimal lastValueMoving = moving[index - 1];

                if (index > moving.Count - 1)
                {
                    moving.Add(0);
                }
                moving[index] = Math.Round((lastValueMoving * (_lenght.ValueInt - 1) + lastValueSeries) / _lenght.ValueInt, 7);
            }

            return moving;
        }
    }
}