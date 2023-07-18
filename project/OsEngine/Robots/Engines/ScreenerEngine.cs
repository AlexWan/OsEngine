/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.OsTrader.Panels;


namespace OsEngine.Robots.Engines
{
    public class ScreenerEngine : BotPanel
    {
        public ScreenerEngine(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);

            Description = "blank strategy for manual trading";
        }

        public override string GetNameStrategyType()
        {
            return "ScreenerEngine";
        }

        public override void ShowIndividualSettingsDialog()
        {
            
        }
    }
}
