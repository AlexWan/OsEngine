/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;

namespace OsEngine.Robots.Engines
{
    [Bot("NewsEngine")]
    public class NewsEngine : BotPanel
    {
        public NewsEngine(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.News);
            TabsNews[0].NewsEvent += NewsEngine_NewsEvent;

            Description = "blank strategy for news trading";
        }

        private void NewsEngine_NewsEvent(News news)
        {
            // do something

        }

        public override string GetNameStrategyType()
        {
            return "NewsEngine";
        }


        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}