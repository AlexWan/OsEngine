using OsEngine.Entity;
using OsEngine.Indicators;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Charts.CandleChart.Indicators.Indicator
{
   // [IndicatorAttribute("LastDayMiddle")]
    internal class LastDayMiddle : Aindicator
    {
        private decimal _high;

        private decimal _low;

        private decimal _dayMid;

        private DateTime _lastHandledCandleTime;

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            _series = CreateSeries("Middle", Color.DarkBlue, IndicatorChartPaintType.Point, true);

            SetDefoltHighLow();
        }

        public override void OnProcess(List<Candle> source, int index)
        {
            if (index < 2)
                return;

            Candle lastCandle = source[index];

            Candle prevCandle = source[index - 1];

            if (lastCandle.TimeStart < _lastHandledCandleTime)
            {
                _dayMid = 0;
                SetDefoltHighLow();
            }

            if (lastCandle.TimeStart.Day != prevCandle.TimeStart.Day)
            {
                _dayMid = (_low + _high) / 2;
                SetDefoltHighLow();
            }

            if (lastCandle.High > _high)
            {
                _high = lastCandle.High;
            }

            if (lastCandle.Low < _low)
            {
                _low = lastCandle.Low;
            }

            _series.Values[index] = _dayMid;

            _lastHandledCandleTime = lastCandle.TimeStart;
        }

        private void SetDefoltHighLow()
        {
            _high = Decimal.MinValue;
            _low = Decimal.MaxValue;
        }
    }
}
