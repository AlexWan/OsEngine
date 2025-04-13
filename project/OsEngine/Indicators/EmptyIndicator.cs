using OsEngine.Entity;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{
    [Indicator("EmptyIndicator")]
    public class EmptyIndicator : Aindicator
    {
        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _series = CreateSeries("Series", Color.DarkGreen, IndicatorChartPaintType.Line, true);
                _series.CanReBuildHistoricalValues = true;
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            // do nothing

        }
    }
}