using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    [Indicator("VWMA")]
    public class VWMA:Aindicator
    {
        private IndicatorParameterInt _length;

        private IndicatorParameterString _candlePoint;

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _length = CreateParameterInt("Length", 14);
                _candlePoint = CreateParameterStringCollection("Candle point", "Close", Entity.CandlePointsArray);
                _series = CreateSeries("Vwma", Color.Aqua, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (_length.ValueInt > index)
            {
                return;
            }

            decimal average = 0;
            decimal weights = 0;

            for (int i = index; i > index - _length.ValueInt; i--)
            {
                average += candles[i].GetPoint(_candlePoint.ValueString) * candles[i].Volume;
                weights += candles[i].Volume;
            }

            if(weights != 0)
            {
                average = average / weights;
            }
            
            _series.Values[index] = average;
        }
    }
}