using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;

namespace OsEngine.Robots.Engines
{
    public class ClusterEngine : BotPanel
    {

        public ClusterEngine(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Cluster);
        }

        /// <summary>
        /// strategy name 
        /// имя стратегии
        /// </summary>
        /// <returns></returns>
        public override string GetNameStrategyType()
        {
            return "ClusterEngine";
        }

        /// <summary>
        /// show settings
        /// показать настройки
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            MessageBox.Show(OsLocalization.Trader.Label112);
        }
    }
}