using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class CCI : Aindicator
    {

        private IndicatorParameterInt _lenght;

        private IndicatorParameterString _candlePoint;

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lenght = CreateParameterInt("Length", 20);
                _candlePoint = CreateParameterStringCollection("Candle Point", "Typical", Entity.CandlePointsArray);
                _series = CreateSeries("Cci", Color.CadetBlue, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index - _lenght.ValueInt <= 0)
            {
                return;
            }

            decimal sum = 0;
            for (int i = index; i > index - _lenght.ValueInt; i--)
            {
                sum += candles[i].GetPoint(_candlePoint.ValueString);
            }
            // average count
            // подсчет средней
            var ma = sum / _lenght.ValueInt;

            decimal md = 0;
            for (int i = index; i > index - _lenght.ValueInt; i--)
            {
                md += Math.Abs(ma - candles[i].GetPoint(_candlePoint.ValueString));
            }

            var cciP = (candles[index].GetPoint(_candlePoint.ValueString) - ma) / (md * 0.015m / _lenght.ValueInt);

            _series.Values[index] = Math.Round(cciP, 5);
        }
    }
}