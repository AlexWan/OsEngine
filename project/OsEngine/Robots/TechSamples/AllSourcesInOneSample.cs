/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;

/* Description
Tech sample for OsEngine.
Example bot that initializes all available source types in OsEngine: Simple, Index, Pair, Screener, Polygon, Cluster, and News.
*/

namespace OsEngine.Robots.TechSamples
{
    // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
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
            TabCreate(BotTabType.News);

            // The source can be accessed through these arrays:
            // TabsSimple[0].
            // TabsIndex[0].
            // TabsPair[0].
            // TabsScreener[0].
            // TabsPolygon[0].
            // TabsCluster[0].
            // TabsNews[0].

            Description = OsLocalization.Description.DescriptionLabel96;
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