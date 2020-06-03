using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class IvashovRange: Aindicator
    {
        private IndicatorDataSeries _series;
        private IndicatorParameterInt _lengthMa;
        private IndicatorParameterInt _lengthAvg;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lengthMa = CreateParameterInt("Length MA", 100);
                _lengthAvg = CreateParameterInt("Length average range", 100);
                _series = CreateSeries("Ivashov", Color.Blue, IndicatorChartPaintType.Line, true);
            }
            else if (state == IndicatorState.Dispose)
            {
                if (averagelist != null)
                {
                    averagelist.Clear();
                }
                if (movinglist != null)
                {
                    movinglist.Clear();
                }
                if (range != null)
                {
                    range.Clear();
                }
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValue(candles, index);
        }
        private decimal GetValue(List<Candle> candles, int index)
        {
            if (index < 2)
            {
                if (averagelist != null)
                {
                    averagelist.Clear();
                }
                if (movinglist != null)
                {
                    movinglist.Clear();
                }
                if (range != null)
                {
                    range.Clear();
                }
            }

            while (index >= movinglist.Count)
            {
                movinglist.Add(CandlesMA(candles, index));
            }
            while (index>=range.Count)
            {
                range.Add(GetRange(candles, movinglist, index));
            }
            while (index >= averagelist.Count)
            {
                averagelist.Add(GetAvg(range, index));
            }

            if (index < _lengthAvg.ValueInt ||
                index < _lengthMa.ValueInt ||
                movinglist[index] == 0)
            {
                return 0;
            }
            return averagelist[index];
        }

        private decimal CandlesMA(List<Candle> candles, int index)
        {
            if (_lengthMa.ValueInt > index)
            {
                return 0;
            }
            return candles.Summ(index - _lengthMa.ValueInt, index, "Close") / _lengthMa.ValueInt;
        }
        private decimal GetRange(List<Candle> candles, List<decimal> moving, int index)
        {
            if (moving[index] == 0)
            {
                return 0;
            }
            return Math.Abs(moving[index] - candles[index].Close);
        }

        private decimal GetAvg(List<decimal> list, int index)
        {
            decimal value = 0;
            if (index >= _lengthAvg.ValueInt)
            {

                decimal var = 0;
                for (int i = index - _lengthAvg.ValueInt + 1; i < index + 1; i++)
                {
                    var += list[i];
                }
                var = var / _lengthAvg.ValueInt;
                value = var;
            }
            return Math.Round(value, 4);

        }

        private List<decimal> range = new List<decimal>();
        private List<decimal> movinglist = new List<decimal>();
        private List<decimal> averagelist = new List<decimal>();

    }
}
