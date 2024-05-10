using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Attributes;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System;
using System.Windows.Forms.DataVisualization.Charting;
using OsEngine.Charts.CandleChart;

namespace OsEngine.Robots.HomeWork
{
    [Bot("ChartBot")]
    public class ChartBot: BotPanel
    {
        private BotTabSimple _tab;        
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _volume;
        private StrategyParameterDecimal _stopLoss;
        private StrategyParameterDecimal _takeProfit;

        WindowsFormsHost _host;

        public ChartBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _regime = CreateParameter("Regime", "Off", new string[] { "Off", "On" });
            _volume = CreateParameter("Volume of trade", 1, 0.1m, 10, 0.1m);
            _stopLoss = CreateParameter("Stop Loss, points of price step", 1m, 1, 10, 1);
            _takeProfit = CreateParameter("Take Profit, points of price ste", 1m, 1, 10, 1);

            this.ParamGuiSettings.Title = "TableBot Parameters";
            this.ParamGuiSettings.Height = 300;
            this.ParamGuiSettings.Width = 600;
            CustomTabToParametersUi customTab = ParamGuiSettings.CreateCustomTab("Table Parameters");
            //customTab.AddChildren(_host);

            CreateChart();

        }

        private void CreateChart()
        {
            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke(new Action(CreateChart));
                return;
            }

            _host = new WindowsFormsHost();

            WinFormsChartPainter chart = new WinFormsChartPainter("ChartBot", StartProgram.IsTester);

            
        }

        public override string GetNameStrategyType()
        {
            return "ChartBot";
        }

        public override void ShowIndividualSettingsDialog()
        {
           
        }
    }
}
