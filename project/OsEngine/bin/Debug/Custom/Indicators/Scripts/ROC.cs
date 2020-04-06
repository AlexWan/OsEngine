using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class ROC:Aindicator
    {
        private IndicatorParameterInt _period;

        private IndicatorParameterString _candlePoint;

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _period = CreateParameterInt("Period", 13);
                _candlePoint = CreateParameterStringCollection("Candle point", "Close", Entity.CandlePointsArray);
                _series = CreateSeries("ROC", Color.Aqua, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValue(candles, index);
        }


        private decimal GetValue(List<Candle> candles, int index)
        {
            if (index < _period.ValueInt)
            {
                return 0;
            }

            decimal value = 0;

            if (_candlePoint.ValueString == "Close")
            {
                value = (candles[index].Close - candles[index - _period.ValueInt].Close) / candles[index - _period.ValueInt].Close * 100;
            }
            if (_candlePoint.ValueString == "Open")
            {
                value = (candles[index].Open - candles[index - _period.ValueInt].Open) / candles[index - _period.ValueInt].Open * 100;
            }
            if (_candlePoint.ValueString == "High")
            {
                value = (candles[index].High - candles[index - _period.ValueInt].High) / candles[index - _period.ValueInt].High * 100;
            }
            if (_candlePoint.ValueString == "Low")
            {
                value = (candles[index].Low - candles[index - _period.ValueInt].Low) / candles[index - _period.ValueInt].Low * 100;
            }

            return Math.Round(value, 3);
        }
    }
}
