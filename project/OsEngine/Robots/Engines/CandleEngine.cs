/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;

namespace OsEngine.Robots.Engines
{
    /// <summary>
    /// blank strategy for manual trading
    /// пустая стратегия для ручной торговли
    /// </summary>
    public class CandleEngine : BotPanel
    {
        public CandleEngine(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            //создание вкладки
            TabCreate(BotTabType.Simple);

            Description = "blank strategy for manual trading";
        }

        /// <summary>
        /// uniq name
        /// униальное имя стратегии
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "Engine";
        }

        /// <summary>
        /// show settings GUI
        /// показать настройки
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            MessageBox.Show(OsLocalization.Trader.Label57);
        }
    }
}