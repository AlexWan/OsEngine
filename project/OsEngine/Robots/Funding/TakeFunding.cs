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
    [Bot("TakeFunding")]
    public class TakeFunding : BotPanel
    {
        private BotTabSimple _tab1;
        private BotTabSimple _tab2;
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _fungingValue;
        private StrategyParameterDecimal _timeToOpen;
        private StrategyParameterDecimal _timeToClose;
        private StrategyParameterDecimal _volume;

        private decimal _currentFunding;
        private DateTime _nextFungingTime;
        private TimeSpan _timerOpenPosition;
        private DateTime _timerClosePosition;

        public TakeFunding(string name, StartProgram startProgram) : base(name, startProgram)
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

            ParamGuiSettings.Title = "Take Funding";
            ParamGuiSettings.Height = 400;
            ParamGuiSettings.Width = 300;

            string tabName = " Настройки ";
            _regime = CreateParameter("Режим", "Off", new string[] { "Off", "On" }, tabName);
            _fungingValue = CreateParameter("Значение фандинга для входа в позицию", 0.01m, 0m, 0m, 0m, tabName);
            _timeToOpen = CreateParameter("Для открытия позиции, время до окончания периода (мин)", 5m, 0m, 0m, 0m, tabName);
            _timeToClose = CreateParameter("Для закрытия позиции, после окончания периода (мин)", 0m, 0m, 0m, 0m, tabName);
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

            dataGridView.ColumnCount = 5;
            dataGridView.RowCount = 2;

            foreach (DataGridViewColumn column in dataGridView.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
                column.ReadOnly = true;
                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
                       
            dataGridView[1, 0].Value = "Фандинг";
            dataGridView[2, 0].Value = "Время до нового периода фандинга";
            dataGridView[3, 0].Value = "Набранная позиция по фьючерсу";
            dataGridView[4, 0].Value = "Набранная позиция по споту";

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
            _currentFunding = _tab1.Funding.CurrentValue;
            _nextFungingTime = _tab1.Funding.NextFundingTime;
        }

        private void UpdateTable()
        {
            _timerOpenPosition = _nextFungingTime - DateTime.UtcNow;

            _grid[0, 1].Value = _tab1.Security?.Name;
            _grid[1, 1].Value = _currentFunding;
            _grid[2, 1].Value = _timerOpenPosition.TotalMilliseconds > 0 ? _timerOpenPosition.ToString(@"hh\:mm\:ss") : 0;

            if (_tab1.PositionsOpenAll?.Count > 0)
            {
                _grid[3, 1].Value = _tab1.PositionsOpenAll[0].Direction + " - " + _tab1.PositionsOpenAll[0].OpenVolume;
            }

            if (_tab2.PositionsOpenAll?.Count > 0)
            {
                _grid[4, 1].Value = _tab2.PositionsOpenAll[0].Direction + " - " + _tab2.PositionsOpenAll[0].OpenVolume;
            }
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
                if (_timerOpenPosition.Minutes <= _timeToOpen.ValueDecimal)
                {
                    if (Math.Abs(_currentFunding) > Math.Abs(_fungingValue.ValueDecimal))
                    {
                        if (_currentFunding > 0)
                        {
                            _tab1.SellAtMarket(_volume.ValueDecimal);
                            _tab2.BuyAtMarket(_volume.ValueDecimal);

                            _timerClosePosition = _nextFungingTime.AddMinutes((double)_timeToClose.ValueDecimal);
                        }
                        else if (_currentFunding < 0)
                        {
                            _tab1.BuyAtMarket(_volume.ValueDecimal);
                            _tab2.SellAtMarket(_volume.ValueDecimal);

                            _timerClosePosition = _nextFungingTime.AddMinutes((double)_timeToClose.ValueDecimal);
                        }
                    }                   
                }
            }

            // логика закрытия позиций
            if (_tab1.PositionsOpenAll.Count > 0 && _tab2.PositionsOpenAll.Count > 0)
            {
                if (_timerClosePosition <= DateTime.UtcNow)
                {
                    _tab1.CloseAllAtMarket();
                    _tab2.CloseAllAtMarket();
                }
            }
        }

        public override string GetNameStrategyType()
        {
            return "TakeFunding";
        }

        public override void ShowIndividualSettingsDialog()
        {
        }
    }
}
