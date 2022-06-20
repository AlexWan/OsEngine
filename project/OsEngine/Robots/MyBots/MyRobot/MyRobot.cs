using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.MyBots.MyRobot

{
    [Bot("MyRobot")]
    public class MyRobot : BotPanel
    {
        public MyRobot(string name, StartProgram startProgram) : base(name, startProgram)
        {

            this.TabCreate(BotTabType.Simple);

            _tab = TabsSimple[0];

            Mode = this.CreateParameter("Mode", "Edit", new[] { "Edit", "Trade" });

            Lot = this.CreateParameter("Lot", 1, 1, 100, 1);
            Stop = this.CreateParameter("Stop", 1, 1, 100, 1);
            Take = this.CreateParameter("Take", 1, 1, 100, 1);

        }


        #region Fields ==============================

        private BotTabSimple _tab;

        public StrategyParameterString Mode;
        public StrategyParameterInt Lot;
        public StrategyParameterInt Stop;
        public StrategyParameterInt Take;

        #endregion

        public override string GetNameStrategyType()
        {
            return nameof(MyRobot);
        }

        public override void ShowIndividualSettingsDialog()
        {
            WindowMyRobot window = new WindowMyRobot();

            window.LotTextBlock.Text = "Lot = " + Lot.ValueInt;
            window.StopTextBlock.Text = "Stop = " + Stop.ValueInt;
            window.TakeTextBlock.Text = "Take = " + Take.ValueInt;

            window.ShowDialog();
        }
    }
}
