using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsEngine.OsTrader.Panels;

namespace OsEngine.Entity
{
    public class GuiLockationMaster
    {
        public static void Activate()
        {

        }

        private static void Load()
        {

        }

        private static void Save()
        {

        }

        public static void CheckStartapLockation(BotPanelChartUi ui)
        {

        }

        public static void SaveStartapLockation(BotPanelChartUi ui)
        {

        }

        public static List<BotPanelIndividualUiLockation> BotPanelLockation;




    }

    public class BotPanelIndividualUiLockation
    {
        public string Uid;

        public double Left;

        public double Right;

        public double Bottom;

        public bool RightPanelIsActive;

        public bool BottomPanelIsActive;
    }

}
