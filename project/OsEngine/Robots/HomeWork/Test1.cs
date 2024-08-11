using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Attributes;
using System.Threading;

namespace OsEngine.Robots.HomeWork
{
    [Bot("Test1")]
    public class Test1 : BotPanel
    {
        private BotTabSimple _tab;
       

        public Test1(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

           


            StartThread();
        }

       

        private void StartThread()
        {
            Thread worker = new Thread(StartPaintChart) { IsBackground = true };
            worker.Start();
        }

        private void StartPaintChart()
        {
            
        }

        

        public override string GetNameStrategyType()
        {
            return "Test1";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

    }
}

