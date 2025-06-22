/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Attributes;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace OsEngine.Robots.Funding
{
    [Bot("TestBotFunding")]
    public class TestBotFunding : BotPanel
    {
        private BotTabScreener _screener;
        private StrategyParameterString _regime;
        private WindowsFormsHost _host;
        private DataGridView _grid;

        public TestBotFunding(string name, StartProgram startProgram) : base(name, startProgram)
        {
            if (startProgram == StartProgram.IsOsOptimizer
                || startProgram == StartProgram.IsTester)
            {
                return;
            }

            TabCreate(BotTabType.Screener);
            _screener = TabsScreener[0];

            _regime = CreateParameter("Regime", "Off", new string[] { "Off", "On" });

            ParamGuiSettings.Title = "Funding";
            ParamGuiSettings.Height = 800;
            ParamGuiSettings.Width = 1200;

            CustomTabToParametersUi customTab = ParamGuiSettings.CreateCustomTab("Funding");

            CreateTable();
            customTab.AddChildren(_host);

            _screener.NewTabCreateEvent += _tab_NewTabCreateEvent;

            Thread worker1 = new Thread(ThreadRefreshTable) { IsBackground = true };
            worker1.Start();
        }

        private void ThreadRefreshTable()
        {
            while (true)
            {
                Thread.Sleep(2000);
                AddDataToGrid();
            }
        }

        private void AddDataToGrid()
        {
            try
            {
                if (_regime.ValueString == "Off")
                {
                    return;
                }

                if (MainWindow.GetDispatcher.CheckAccess() == false)
                {
                    MainWindow.GetDispatcher.Invoke(new Action(AddDataToGrid));
                    return;
                }

                int startRow = _grid.FirstDisplayedScrollingRowIndex;

                if (startRow < 0)
                {
                    startRow = 0;
                }

                int lastRow = startRow + _grid.DisplayedRowCount(false);

                if (lastRow < 0 ||
                    lastRow > _grid.Rows.Count)
                {
                    lastRow = _grid.Rows.Count;
                }

                if (_grid.Rows.Count != 0)
                {
                    for (int i = startRow; i < lastRow; i++)
                    {
                        if (_grid.Rows[i].Cells[0].Value == null)
                        {
                            continue;
                        }

                        for (int j = 0; j < _screener.Tabs.Count; j++)
                        {
                            if (_screener.Tabs[j].Security != null &&
                                _screener.Tabs[j].Security.Name == _grid.Rows[i].Cells[0].Value.ToString())
                            {
                                SetDataInTable(_screener.Tabs[j], i);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                Thread.Sleep(5000);
            }
        }

        private void CreateTable()
        {
            _host = new WindowsFormsHost();

            DataGridView newGrid =
                DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCells);

            newGrid.ScrollBars = ScrollBars.Both;
            newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = newGrid.DefaultCellStyle;

            string[] columns =
            [
                "Name", "Class", "Full Name", "Name ID", "Current Funding", "Time next funding", "Funding Interval Hours", "Max Funding Rate", "Min Funding Rate",
                "Volume 24h", "Volume 24h USDT", "Open Interest", "Time Update"
            ];

            for (int i = 0; i < columns.Length; i++)
            {
                DataGridViewColumn column = new DataGridViewColumn();
                column.CellTemplate = cell0;
                column.HeaderText = columns[i];
                column.ReadOnly = true;
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                newGrid.Columns.Add(column);
            }

            _host.Child = newGrid;
            _grid = newGrid;
        }

        private void _tab_NewTabCreateEvent(BotTabSimple tab)
        {
            if (tab.Connector == null)
            {
                return;
            }

            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke(_tab_NewTabCreateEvent, tab);
                return;
            }

            string securityName = tab.Connector.SecurityName;

            DataGridViewRow newRow = new DataGridViewRow();

            for (int j = 0; j < _grid.Columns.Count; j++)
            {
                newRow.Cells.Add(new DataGridViewTextBoxCell());
            }

            _grid.Rows.Add(newRow);

            _grid.Rows[_grid.Rows.Count - 1].Cells[0].Value = securityName;            
        }

        private void SetDataInTable(BotTabSimple tab, int row)
        {
            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke(SetDataInTable, tab, row);
                return;
            }

            _grid.Rows[row].Cells[1].Value = tab.Connector.Security?.NameClass;
            _grid.Rows[row].Cells[2].Value = tab.Connector.Security?.NameFull;
            _grid.Rows[row].Cells[3].Value = tab.Connector.Security?.NameId;


            if (_grid.Rows[row].Cells[4].Value == null ||
                _grid.Rows[row].Cells[4].Value.ToString() != tab.Funding.CurrentValue.ToString())
            {
                _grid.Rows[row].Cells[4].Value = tab.Funding.CurrentValue;
            }

            if (_grid.Rows[row].Cells[5].Value == null ||
                _grid.Rows[row].Cells[5].Value.ToString() != tab.Funding.NextFundingTime.ToString())
            {
                _grid.Rows[row].Cells[5].Value = tab.Funding.NextFundingTime;
            }

            if (_grid.Rows[row].Cells[6].Value == null ||
                _grid.Rows[row].Cells[6].Value.ToString() != tab.Funding.FundingIntervalHours.ToString())
            {
                _grid.Rows[row].Cells[6].Value = tab.Funding.FundingIntervalHours;
            }

            if (_grid.Rows[row].Cells[7].Value == null ||
                _grid.Rows[row].Cells[7].Value.ToString() != tab.Funding.MaxFundingRate.ToString())
            {
                _grid.Rows[row].Cells[7].Value = tab.Funding.MaxFundingRate;
            }

            if (_grid.Rows[row].Cells[8].Value == null ||
                _grid.Rows[row].Cells[8].Value.ToString() != tab.Funding.MinFundingRate.ToString())
            {
                _grid.Rows[row].Cells[8].Value = tab.Funding.MinFundingRate;
            }

            if (_grid.Rows[row].Cells[9].Value == null ||
                _grid.Rows[row].Cells[9].Value.ToString() != tab.SecurityVolumes.Volume24h.ToString())
            {
                _grid.Rows[row].Cells[9].Value = tab.SecurityVolumes.Volume24h;
            }

            if (_grid.Rows[row].Cells[10].Value == null ||
                _grid.Rows[row].Cells[10].Value.ToString() != tab.SecurityVolumes.Volume24hUSDT.ToString())
            {
                _grid.Rows[row].Cells[10].Value = tab.SecurityVolumes.Volume24hUSDT;
            }

            if (_grid.Rows[row].Cells[11].Value == null ||
                _grid.Rows[row].Cells[11].Value.ToString() != tab.Trades[^1]?.OpenInterest.ToString())
            {
                if (tab.Trades?.Count > 0)
                {
                    _grid.Rows[row].Cells[11].Value = tab.Trades[^1]?.OpenInterest;
                }
            }

            if (_grid.Rows[row].Cells[12].Value == null ||
                _grid.Rows[row].Cells[12].Value.ToString() != tab.Funding.TimeUpdate.ToString())
            {
                _grid.Rows[row].Cells[12].Value = tab.Funding.TimeUpdate;
            }
        }

        public override string GetNameStrategyType()
        {
            return "TestBotFunding";
        }

        public override void ShowIndividualSettingsDialog()
        {
        }
    }
}