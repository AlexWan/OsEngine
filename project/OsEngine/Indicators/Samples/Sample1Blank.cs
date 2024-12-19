using OsEngine.Entity;
using System.Collections.Generic;

namespace OsEngine.Indicators.Samples
{
    [Indicator("Sample1Blank")]
    public class Sample1Blank : Aindicator
    {
        public IndicatorDataSeries Series;

        public override void OnStateChange(IndicatorState state)
        {
            if(state == IndicatorState.Configure)
            { // Instead of a constructor

                Series = CreateSeries("Series 1", System.Drawing.Color.Blue, IndicatorChartPaintType.Line, true);

            }
        }

        public override void OnProcess(List<Candle> source, int index)
        {

            Series.Values[index] = source[index].Center;

        }
    }
}