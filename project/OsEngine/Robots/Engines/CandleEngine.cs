using System.Windows;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
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