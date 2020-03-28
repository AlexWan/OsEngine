using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class Sma : Aindicator
    {

        private IndicatorParameterInt _lenght;

        private IndicatorParameterString _candlePoint;

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lenght = CreateParameterInt("Length", 14);
                _candlePoint = CreateParameterStringCollection("Candle Point", "Close", Entity.CandlePointsArray);
                _series = CreateSeries("Ma", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (_lenght.ValueInt > index)
            {
                _series.Values[index] = 0;
                return;
            }

            _series.Values[index] = candles.Summ(index - _lenght.ValueInt, index, _candlePoint.ValueString) / _lenght.ValueInt;
        }
    }
}