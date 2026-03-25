/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;

namespace OsEngine.Robots.Engines
{
    [Bot("SyntheticBondEngine")]
    public class SyntheticBondEngine : BotPanel
    {
        public SyntheticBondEngine(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.SyntheticBond);

            Description = OsLocalization.Description.DescriptionLabel329;
        }

        public override string GetNameStrategyType()
        {
            return "SyntheticBondEngine";
        }

        public override void ShowIndividualSettingsDialog() { }
    }
}
