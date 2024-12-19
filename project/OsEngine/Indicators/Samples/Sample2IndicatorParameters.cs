using OsEngine.Entity;
using System.Collections.Generic;

namespace OsEngine.Indicators.Samples
{
    [Indicator("Sample2IndicatorParameters")]
    public class Sample2IndicatorParameters : Aindicator
    {
        public IndicatorParameterInt ParameterLen;

        public IndicatorParameterDecimal ParameterDeviationPercent;

        public IndicatorParameterBool ParameterAverageIsOn;

        public IndicatorParameterString ParameterRegime;

        public IndicatorParameterString ParameterPathToFolder;

        public override void OnStateChange(IndicatorState state)
        {
            if(state == IndicatorState.Configure)
            { // Instead of a constructor

                ParameterLen = CreateParameterInt("Length", 15);

                ParameterDeviationPercent = CreateParameterDecimal("Deviation percent", 0.3m);

                ParameterAverageIsOn = CreateParameterBool("Average is on", true);

                ParameterRegime = CreateParameterStringCollection("Regime", "First", new List<string>() { "First", "Second" });

                ParameterPathToFolder = CreateParameterString("Path to folder", "C:/Program files");

            }
        }

        public override void OnProcess(List<Candle> source, int index)
        {

            int parameterInt = ParameterLen.ValueInt;

            decimal parameterDecimal = ParameterDeviationPercent.ValueDecimal;

            bool parameterBool = ParameterAverageIsOn.ValueBool;

            if(parameterBool == true)
            {
                // do something
            }

            string parameterString = ParameterRegime.ValueString;

            string parameterStringAlone = ParameterPathToFolder.ValueString;
        }
    }
}