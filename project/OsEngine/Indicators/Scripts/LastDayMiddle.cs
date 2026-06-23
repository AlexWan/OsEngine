using OsEngine.Entity;
using OsEngine.Language;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{
    [Indicator("LastDayMiddle")]
    public class LastDayMiddle : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "LastDayMiddle calculates the midpoint of the previous trading day's range — based on high/low or close — and builds deviation zones around it in percentage terms, carrying yesterday's levels onto the current chart. " +
                             "Traders use it to identify key support and resistance levels at the open of a new day and to find reversals near these zones.";

                string ru = "LastDayMiddle вычисляет середину торгового диапазона предыдущего дня — по high/low или close — и строит вокруг неё зоны отклонения в процентах, перенося уровни прошлой сессии на текущий график. " +
                            "Трейдеры применяют индикатор для определения ключевых уровней поддержки и сопротивления на открытии нового дня и поиска разворотов около этих зон.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private decimal _high;

        private decimal _low;

        private decimal _dayMid;

        private DateTime _lastHandledCandleTime;

        private IndicatorDataSeries _series;

        private IndicatorDataSeries _deviationUp;

        private IndicatorDataSeries _deviationDown;

        private IndicatorParameterDecimal _deviationPercent;

        private IndicatorParameterString _calcMethodType;

        public override void OnStateChange(IndicatorState state)
        {
            _series = CreateSeries("Middle", Color.DarkBlue, IndicatorChartPaintType.Point, true);

            _deviationUp = CreateSeries("Up deviation", Color.Green, IndicatorChartPaintType.Point, true);

            _deviationDown = CreateSeries("Downp deviation", Color.Red, IndicatorChartPaintType.Point, true);

            _deviationPercent = CreateParameterDecimal("Deviation %", 1);

            List<string> methods = new List<string>() { "HighLow", "Close" };

            _calcMethodType = CreateParameterStringCollection("Calculation method", methods[0], methods);

            SetDefaultHighLow();
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
                SetDefaultHighLow();
            }

            if (lastCandle.TimeStart.Day != prevCandle.TimeStart.Day)
            {
                _dayMid = (_low + _high) / 2;
                SetDefaultHighLow();
            }

            CalcMaximumMinimum(lastCandle);

            _series.Values[index] = _dayMid;

            _deviationUp.Values[index] = _dayMid + CalcDeviation();

            _deviationDown.Values[index] = _dayMid - CalcDeviation();

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

        private void SetDefaultHighLow()
        {
            _high = Decimal.MinValue;
            _low = Decimal.MaxValue;
        }
    }
}