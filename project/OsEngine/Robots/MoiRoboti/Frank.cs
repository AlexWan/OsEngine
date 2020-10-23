using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Charts.CandleChart.Indicators;
using System.Threading;
//using OsEngine.Robots.MoiRoboti.MyBlanks;

namespace OsEngine.Robots.MoiRoboti
{
    public class Frank : BotPanel
    {
        private BotTabSimple _tab; // поле хранения вкладки робота 

        public Frank(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);  // создание простой вкладки
            _tab = TabsSimple[0]; // записываем первую вкладку в поле
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
