using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;

namespace OsEngine.Robots.BotsFromStartLessons
{
    [Bot("Lesson2Bot2")]
    public class Lesson2Bot2 : BotPanel
    {
        public Lesson2Bot2(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // 1 создаём параметр String

            StrategyParameterString regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });

            // 2 создаём параметр Int

            StrategyParameterInt smaLen = CreateParameter("Sma len", 15, 1, 20, 1);

            // 3 создаём параметр Bool

            StrategyParameterBool isUpCandleToEntry = CreateParameter("Is up candle", true);

            // 4 создаём параметр Decimal

            StrategyParameterDecimal bollingerDeviation = CreateParameter("Bollinger deviation", 1.4m, 1, 2, 0.1m);

            // 5 создаём параметр TimeOfDay

            StrategyParameterTimeOfDay startToTrade = CreateParameterTimeOfDay("Start to trade", 11, 00, 00, 00);

        }

        public override string GetNameStrategyType()
        {
            return "Lesson2Bot2";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}