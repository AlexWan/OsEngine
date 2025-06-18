using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;

namespace OsEngine.Robots.Engines
{
    public class ClusterEngine : BotPanel
    {
        public ClusterEngine(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            // Create tabs
            TabCreate(BotTabType.Cluster);

            Description = "blank strategy for manual trading";
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "ClusterEngine";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {
            MessageBox.Show(OsLocalization.Trader.Label112);
        }
    }
}