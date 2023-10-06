/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;


namespace OsEngine.Robots.Engines
{
    [Bot("EnginePair")]
    public class EnginePair : BotPanel
    {
       
        public EnginePair(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Pair);

            Description = "blank strategy for manual pair trading";
        }

        /// <summary>
        /// strategy name 
        /// имя стратегии
        /// </summary>
        /// <returns></returns>
        public override string GetNameStrategyType()
        {
            return "EnginePair";
        }

        /// <summary>
        /// show settings
        /// показать настройки
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
           
        }
    }
}