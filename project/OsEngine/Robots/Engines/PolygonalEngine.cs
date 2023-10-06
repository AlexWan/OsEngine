/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;

namespace OsEngine.Robots.Engines
{
    [Bot("PolygonalEngine")]
    public class PolygonalEngine : BotPanel
    {
        public PolygonalEngine(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Polygon);

            Description = "blank strategy for manual currency trading";
        }

        public override string GetNameStrategyType()
        {
            return "PolygonalEngine";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}