using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Charts.CandleChart.Indicators;
using System.Threading;


namespace OsEngine.Robots.MoiRoboti
{
    class HFT_1 : BotPanel
    {
        public HFT_1(string name, StartProgram startProgram) : base(name, startProgram)
        {

        }

        public override string GetNameStrategyType()
        {
            return "HFT_1";
        }

        public override void ShowIndividualSettingsDialog()
        {
            
        }
    }
}
