/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using OsEngine.Language;
using OsEngine.Market;

namespace OsEngine.OsTrader.Panels.SingleRobots
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
