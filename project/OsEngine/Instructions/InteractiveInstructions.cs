/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Instructions;

namespace OsEngine
{
    public class InteractiveInstructions
    {
        public static GridInstructions Grids = new GridInstructions();

        public static MainMenuInstructions MainMenu = new MainMenuInstructions();

        public static OsDataInstructions Data = new OsDataInstructions();

        public static ConverterInstructions Converter = new ConverterInstructions();

        public static Journal2Instructions Journal2Posts = new Journal2Instructions();

        public static TesterLightInstructions TesterLightPosts = new TesterLightInstructions();

        public static BotStationLightInstructions BotStationLightPosts = new BotStationLightInstructions();

        public static PositionComparisonInstructions PositionComparisonPosts = new PositionComparisonInstructions();
    }
}
