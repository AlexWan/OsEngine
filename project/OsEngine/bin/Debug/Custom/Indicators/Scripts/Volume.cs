using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    [Indicator("Volume")]
    public class Volume : Aindicator
    {
        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _series = CreateSeries("Volume", Color.DodgerBlue, IndicatorChartPaintType.Column, true);
                _series.CanReBuildHistoricalValues = true;
            }
            else if (state == IndicatorState.Dispose)
            {
                _series = null;
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = candles[index].Volume;

            if(index - 1 >= 0)
            {
                _series.Values[index - 1] = candles[index - 1].Volume;
            }
        }
    }
}