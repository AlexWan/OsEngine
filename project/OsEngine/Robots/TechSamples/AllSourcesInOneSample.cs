using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;

namespace OsEngine.Robots.TechSamples
{
    [Bot("AllSourcesInOneSample")]
    public class AllSourcesInOneSample : BotPanel
    {
        public AllSourcesInOneSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            TabCreate(BotTabType.Index);
            TabCreate(BotTabType.Pair);
            TabCreate(BotTabType.Screener);
            TabCreate(BotTabType.Polygon);
            TabCreate(BotTabType.Cluster);

            // The source can be accessed through these arrays:
            // TabsSimple[0].
            // TabsIndex[0].
            // TabsPair[0].
            // TabsScreener[0].
            // TabsPolygon[0].
            // TabsCluster[0].


            Description = "Example in which all types of sources are created in OsEngine";
        }

        public override string GetNameStrategyType()
        {
            return "AllSourcesInOneSample";
        }

        public override void ShowIndividualSettingsDialog()
        {
            

        }
    }
}
