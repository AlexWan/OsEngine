using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{
    [Indicator("LastDayMiddle")]
    public class LastDayMiddle : Aindicator
    {
        private decimal _high;

        private decimal _low;

        private decimal _dayMid;

        private DateTime _lastHandledCandleTime;

        private IndicatorDataSeries _series;

        private IndicatorDataSeries _deviationUp;

        private IndicatorDataSeries _deviationDownp;

        private IndicatorParameterDecimal _deviationPercent;

        private IndicatorParameterString _calcMethodType;

        public override void OnStateChange(IndicatorState state)
        {
            _series = CreateSeries("Middle", Color.DarkBlue, IndicatorChartPaintType.Point, true);

            _deviationUp = CreateSeries("Up deviation", Color.Green, IndicatorChartPaintType.Point, true);

            _deviationDownp = CreateSeries("Downp deviation", Color.Red, IndicatorChartPaintType.Point, true);

            _deviationPercent = CreateParameterDecimal("Deviation %", 1);

            List<string> methods = new List<string>() { "HighLow", "Close" };

            _calcMethodType = CreateParameterStringCollection("Calculation method", methods[0], methods);

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

            CalcMaximumMinimum(lastCandle);

            _series.Values[index] = _dayMid;

            _deviationUp.Values[index] = _dayMid + CalcDeviation();

            _deviationDownp.Values[index] = _dayMid - CalcDeviation();

            _lastHandledCandleTime = lastCandle.TimeStart;
        }

        private void CalcMaximumMinimum(Candle lastCandle)
        {
            if (_calcMethodType.ValueString == "HighLow")
            {
                if (lastCandle.High > _high)
                {
                    _high = lastCandle.High;
                }

                if (lastCandle.Low < _low)
                {
                    _low = lastCandle.Low;
                }
            }
            else if (_calcMethodType.ValueString == "Close")
            {
                if (lastCandle.Close > _high)
                {
                    _high = lastCandle.Close;
                }

                if (lastCandle.Close < _low)
                {
                    _low = lastCandle.Close;
                }
            }
        }

        private decimal CalcDeviation()
        {
            return _dayMid / 100 * _deviationPercent.ValueDecimal;
        }

        private void SetDefoltHighLow()
        {
            _high = Decimal.MinValue;
            _low = Decimal.MaxValue;
        }
    }
}
