using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Drawing;

namespace OsEngine.Robots.Funding
{
    [Bot("ArbitrageFunding")]
    public class ArbitrageFunding : BotPanel
    {
        private BotTabSimple _tab1;
        private BotTabSimple _tab2;
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _spreadFundingOpen;
        private StrategyParameterDecimal _spreadFundingClose;
        private StrategyParameterDecimal _volume;

        private decimal _funding1;
        private decimal _funding2;
        private decimal _currentSpread;

        public ArbitrageFunding(string name, StartProgram startProgram) : base(name, startProgram)
        {
            if (startProgram == StartProgram.IsOsOptimizer
               || startProgram == StartProgram.IsTester)
            {
                return;
            }

            TabCreate(BotTabType.Simple);
            _tab1 = TabsSimple[0];
            TabCreate(BotTabType.Simple);
            _tab2 = TabsSimple[1];

            ParamGuiSettings.Title = "Arbitrage Funding";
            ParamGuiSettings.Height = 400;
            ParamGuiSettings.Width = 300;

            string tabName = " Настройки ";
            _regime = CreateParameter("Режим", "Off", new string[] {"Off", "On"}, tabName);
            _spreadFundingOpen = CreateParameter("Спред между фандингами для открытия позиции", 0.02m, 0m, 0m, 0m, tabName);
            _spreadFundingClose = CreateParameter("Спред между фандингами для закрытия позиции", 0m, 0m, 0m, 0m, tabName);
            _volume = CreateParameter("Объем позиции", 1m, 0m, 0m, 0m, tabName);

            CustomTabToParametersUi tabFunding = ParamGuiSettings.CreateCustomTab(" Фандинг ");

            CreateTable();
            tabFunding.AddChildren(_host);

            Thread worker = new Thread(ThreadMain) { IsBackground = true };
            worker.Start();
        }

        private WindowsFormsHost _host;
        private DataGridView _grid;

        private void CreateTable()
        {
            _host = new WindowsFormsHost();

            DataGridView dataGridView =
                DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCells);

            dataGridView.ScrollBars = ScrollBars.Both;
            dataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView.GridColor = Color.Gray;

            dataGridView.ColumnCount = 4;
            dataGridView.RowCount = 2;

            foreach (DataGridViewColumn column in dataGridView.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
                column.ReadOnly = true;
                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }

            dataGridView[1, 0].Value = _tab1.Security?.Exchange;
            dataGridView[2, 0].Value = _tab2.Security?.Exchange;
            dataGridView[3, 0].Value = "Текущий спред";
            dataGridView[0, 1].Value = "Фандинг";

            _host.Child = dataGridView;
            _grid = dataGridView;
        }

        private void ThreadMain()
        {
            while (true)
            {
                try
                {
                    GetFunding();
                    UpdateTable();
                    TradeLogic();

                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void GetFunding()
        {
            _funding1 = _tab1.Funding.CurrentValue;
            _funding2 = _tab2.Funding.CurrentValue;
            _currentSpread = Math.Abs(_funding1 - _funding2);
        }

        private void UpdateTable()
        {
            _grid[1, 0].Value = _tab1.Security?.Exchange;
            _grid[2, 0].Value = _tab2.Security?.Exchange;
            _grid[1, 1].Value = _funding1;
            _grid[2, 1].Value = _funding2;
            _grid[3, 1].Value = _currentSpread;
        }

        private void TradeLogic()
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            // логика открытия позиции
            if (_tab1.PositionsOpenAll.Count == 0 && _tab2.PositionsOpenAll.Count == 0)
            {
                if (_currentSpread > _spreadFundingOpen.ValueDecimal)
                {
                    if (_funding1 > _funding2)
                    {
                        _tab1.SellAtMarket(_volume.ValueDecimal);
                        _tab2.BuyAtMarket(_volume.ValueDecimal);
                    }
                    else if (_funding1 < _funding2)
                    {
                        _tab1.BuyAtMarket(_volume.ValueDecimal);
                        _tab2.SellAtMarket(_volume.ValueDecimal);
                    }
                }
            }

            // логика закрытия позиций
            if (_tab1.PositionsOpenAll.Count > 0 && _tab2.PositionsOpenAll.Count > 0)
            {
                if (_currentSpread < _spreadFundingClose.ValueDecimal)
                {
                    _tab1.CloseAllAtMarket();
                }                
            }
        }

        public override string GetNameStrategyType()
        {
            return "ArbitrageFunding";
        }

        public override void ShowIndividualSettingsDialog()
        {
        }
    }
}
