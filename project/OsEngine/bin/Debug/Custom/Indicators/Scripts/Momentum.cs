using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class Momentum:Aindicator
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
                value = (candles[index].Close / candles[index - _length.ValueInt].Close) * 100;
            }
            if (_candlePoint.ValueString == "Open")
            {
                value = candles[index].Open / candles[index - _length.ValueInt].Open * 100;
            }
            if (_candlePoint.ValueString == "High")
            {
                value = candles[index].High / candles[index - _length.ValueInt].High * 100;
            }
            if (_candlePoint.ValueString == "Low")
            {
                value = candles[index].Low / candles[index - _length.ValueInt].Low * 100;
            }


            return Math.Round(value, 2);
        }
    }
}
