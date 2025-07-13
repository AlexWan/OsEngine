/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;

namespace OsEngine.Robots.Engines
{
    // Blank strategy for manual trading
    public class CandleEngine : BotPanel
    {
        public CandleEngine(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            // Create tabs
            TabCreate(BotTabType.Simple);

            Description = OsLocalization.Description.DescriptionLabel28;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "Engine";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {
            MessageBox.Show(OsLocalization.Trader.Label57);
        }
    }
}