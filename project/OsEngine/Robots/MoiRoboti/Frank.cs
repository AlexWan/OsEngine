using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Charts.CandleChart.Indicators;
using System.Threading;

namespace OsEngine.Robots.MoiRoboti
{
    public class Frank : BotPanel
    {
        public Frank(string name, StartProgram startProgram) : base(name, startProgram)
        {
        }

        public override string GetNameStrategyType()
        {
            return "Frank";
        }

        public override void ShowIndividualSettingsDialog()
        {
            
        }
    }
}
