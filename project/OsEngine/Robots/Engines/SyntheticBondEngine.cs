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

            Description = OsLocalization.Description.DescriptionLabel30;

        }

        public override string GetNameStrategyType()
        {
            return "SyntheticBondEngine";
        }

        public override void ShowIndividualSettingsDialog() { }
    }
}
