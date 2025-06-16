/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Robots.Grids
{
    public class GridLinearRegression : BotPanel
    {
        public GridLinearRegression(string name, StartProgram startProgram) : base(name, startProgram)
        {
            



        }

        public override string GetNameStrategyType()
        {
            return "GridLinearRegression";
        }

        public override void ShowIndividualSettingsDialog()
        {
           


        }
    }
}
