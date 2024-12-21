using OsEngine.Entity;
using System.Collections.Generic;

namespace OsEngine.Indicators.Samples
{
    [Indicator("Sample3IndicatorDataSeries")]
    public class Sample3IndicatorDataSeries : Aindicator
    {
        public IndicatorDataSeries Series1;

        public override void OnStateChange(IndicatorState state)
        {
            if(state == IndicatorState.Configure)
            {

                Series1 = CreateSeries("Series 1", System.Drawing.Color.AliceBlue, IndicatorChartPaintType.Line, true);
            
            }
        }

        public override void OnProcess(List<Candle> source, int index)
        {

            Series1.Values[index] = source[index].Center;

        }
    }
}
