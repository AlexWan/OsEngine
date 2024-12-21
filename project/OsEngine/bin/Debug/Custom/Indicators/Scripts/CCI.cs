using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    [Indicator("CCI")]
    public class CCI : Aindicator
    {
        private IndicatorParameterInt _length;

        private IndicatorParameterString _candlePoint;

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _length = CreateParameterInt("Length", 20);
                _candlePoint = CreateParameterStringCollection("Candle Point", "Typical", Entity.CandlePointsArray);
                _series = CreateSeries("Cci", Color.CadetBlue, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index - _length.ValueInt <= 0)
            {
                return;
            }

            decimal sum = 0;
            for (int i = index; i > index - _length.ValueInt; i--)
            {
                sum += candles[i].GetPoint(_candlePoint.ValueString);
            }
            // average count
            // подсчет средней
            var ma = sum / _length.ValueInt;

            decimal md = 0;
            for (int i = index; i > index - _length.ValueInt; i--)
            {
                md += Math.Abs(ma - candles[i].GetPoint(_candlePoint.ValueString));
            }

            if(md == 0)
            {
                return;
            }

            var cciP = (candles[index].GetPoint(_candlePoint.ValueString) - ma) / (md * 0.015m / _length.ValueInt);

            _series.Values[index] = Math.Round(cciP, 5);
        }
    }
}