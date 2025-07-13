/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;

namespace OsEngine.Robots.Engines
{
    [Bot("NewsEngine")] // We create an attribute so that we don't write anything to the BotFactory
    public class NewsEngine : BotPanel
    {
        public NewsEngine(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            // Create tabs
            TabCreate(BotTabType.News);
            TabsNews[0].NewsEvent += NewsEngine_NewsEvent;

            Description = OsLocalization.Description.DescriptionLabel31;
        }

        private void NewsEngine_NewsEvent(News news)
        {
            // Do something
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "NewsEngine";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}