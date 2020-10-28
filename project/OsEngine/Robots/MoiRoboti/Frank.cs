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
        MyBlanks blanks = new MyBlanks("Frank", StartProgram.IsOsTrader); // создание экземпляра класса MyBlanks
        private BotTabSimple _tab; // поле хранения вкладки робота 
        

        public Frank(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);  // создание простой вкладки
            _tab = TabsSimple[0]; // записываем первую вкладку в поле

            _tab.NewTickEvent += _tab_NewTickEvent; // тест 

        }

        private void _tab_NewTickEvent(Trade trade) // тест 
        {
           
            // Console.WriteLine(" "); 
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
