using OsEngine.Entity;
using System.Collections.Generic;

namespace OsEngine.Indicators.Samples
{
    [Indicator("Sample1Blank")]
    public class Sample1Blank : Aindicator
    {

        public override void OnStateChange(IndicatorState state)
        {
            if(state == IndicatorState.Configure)
            { // Instead of a constructor

            

            }
        }

        public override void OnProcess(List<Candle> source, int index)
        {


        }
    }
}