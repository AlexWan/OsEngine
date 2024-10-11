using OsEngine;
using OsEngine.Entity;
using OsEngine.Market.Connectors;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Robots.HomeWork;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace OsEngine.Robots.HomeWork
{
    [Bot("MoexSpreadPairs")]
    public class MoexSpreadPairs : BotPanel
    {
        private BotTabPair _tab;
        
        public MoexSpreadPairs(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Pair);
            _tab = TabsPair[0];

            

        }

       

        public override string GetNameStrategyType()
        {
            return "MoexSpreadPairs";
        }

        public override void ShowIndividualSettingsDialog()
        {
        }

        
        
    }
}

