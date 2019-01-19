using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using OsEngine.Market;

namespace OsEngine.OsTrader.Panels.SingleRobots
{
    class ClusterEngine : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public ClusterEngine(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            //создание вкладки
            TabCreate(BotTabType.Cluster);
        }

        /// <summary>
        /// униальное имя стратегии
        /// </summary>
        /// <returns></returns>
        public override string GetNameStrategyType()
        {
            return "ClusterEngine";
        }

        /// <summary>
        /// показать настройки
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            MessageBox.Show("У данной стратегии нет настроек. Это ж привод и сам он ничего не делает.");
        }
    }
}
