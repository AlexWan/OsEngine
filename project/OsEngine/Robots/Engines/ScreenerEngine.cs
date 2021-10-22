using OsEngine.Entity;
using OsEngine.OsTrader.Panels;


namespace OsEngine.Robots.Engines
{
    public class ScreenerEngine : BotPanel
    {
        public ScreenerEngine(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
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
