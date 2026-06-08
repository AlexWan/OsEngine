using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    [Indicator("Momentum")]
    public class Momentum : Aindicator
    {
        private IndicatorParameterInt _length;

        private IndicatorParameterString _candlePoint;

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _length = CreateParameterInt("Length", 5);
                _candlePoint = CreateParameterStringCollection("Candle point", "Close", Entity.CandlePointsArray);
                _series = CreateSeries("Momentum", Color.Aqua, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValue(candles, index);
        }

        private decimal GetValue(List<Candle> candles, int index)
        {

            if (index < _length.ValueInt)
            {
                return 0;
            }

            decimal value = 0;

            if (_candlePoint.ValueString == "Close")
            {
                decimal divider = candles[index - _length.ValueInt].Close;

                if (divider != 0)
                {
                    value = (candles[index].Close / divider) * 100;
                }
            }
            if (_candlePoint.ValueString == "Open")
            {
                decimal divider = candles[index - _length.ValueInt].Open;

                if (divider != 0)
                {
                    value = (candles[index].Open / divider) * 100;
                }
            }
            if (_candlePoint.ValueString == "High")
            {
                decimal divider = candles[index - _length.ValueInt].High;

                if (divider != 0)
                {
                    value = (candles[index].High / divider) * 100;
                }
            }
            if (_candlePoint.ValueString == "Low")
            {
                decimal divider = candles[index - _length.ValueInt].Low;

                if (divider != 0)
                {
                    value = (candles[index].Low / divider) * 100;
                }
            }

            return Math.Round(value, 6);
        }
    }
}