/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;

namespace OsEngine.Robots.Engines
{
    public class ScreenerEngine : BotPanel
    {
        public ScreenerEngine(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create tabs
            TabCreate(BotTabType.Screener);

            Description = OsLocalization.Description.DescriptionLabel33;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "ScreenerEngine";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {
            
        }
    }
}