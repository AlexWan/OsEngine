using System;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Attributes;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Language;

/* Description
TestBot for OsEngine.

Do not turn on - robot for Option testing.
*/

namespace OsEngine.Robots.AutoTestBots
{
    [Bot("TestBotOption")] //We create an attribute so that we don't write anything in the Boot factory
    public class TestBotOption : BotPanel
    {
        private BotTabScreener _screener;

        private StrategyParameterString _regime;

        private WindowsFormsHost _host;

        private DataGridView _grid;    
        
        public TestBotOption(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _screener = TabsScreener[0];

            Description = OsLocalization.Description.DescriptionLabel3;

            if(startProgram == StartProgram.IsOsOptimizer 
                || startProgram == StartProgram.IsTester)
            {
                return;
            }

            _regime = CreateParameter("Regime", "Off", new string[] { "Off", "On" });

            this.ParamGuiSettings.Title = "Options Greeks";
            this.ParamGuiSettings.Height = 800;
            this.ParamGuiSettings.Width = 1200;

            CustomTabToParametersUi customTab = ParamGuiSettings.CreateCustomTab("Options Greeks");

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
                    lastRow> _grid.Rows.Count)
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
                                _screener.Tabs[j].Security.Name == _grid.Rows[i].Cells[0].Value.ToString())/* &&
                            _grid.Rows[i].Cells[0].Value.ToString().Contains("BTC-28FEB25"))*/
                            {
                                SetDataInTable(_screener.Tabs[j].Connector.OptionMarketData, i);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(),Logging.LogMessageType.Error);
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

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = "Security Name";
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colum01 = new DataGridViewColumn();
            colum01.CellTemplate = cell0;
            colum01.HeaderText = "Underlying Asset";
            colum01.ReadOnly = true;
            colum01.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum01);

            DataGridViewColumn colum02 = new DataGridViewColumn();
            colum02.CellTemplate = cell0;
            colum02.HeaderText = "Underlying Price";
            colum02.ReadOnly = true;
            colum02.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum02);

            DataGridViewColumn colum03 = new DataGridViewColumn();
            colum03.CellTemplate = cell0;
            colum03.HeaderText = "Mark Price";
            colum03.ReadOnly = true;
            colum03.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum03);

            DataGridViewColumn colum04 = new DataGridViewColumn();
            colum04.CellTemplate = cell0;
            colum04.HeaderText = "Mark IV";
            colum04.ReadOnly = true;
            colum04.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum04);

            DataGridViewColumn colum05 = new DataGridViewColumn();
            colum05.CellTemplate = cell0;
            colum05.HeaderText = "Bid IV";
            colum05.ReadOnly = true;
            colum05.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum05);

            DataGridViewColumn colum06 = new DataGridViewColumn();
            colum06.CellTemplate = cell0;
            colum06.HeaderText = "Ask IV";
            colum06.ReadOnly = true;
            colum06.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum06);

            DataGridViewColumn colum07 = new DataGridViewColumn();
            colum07.CellTemplate = cell0;
            colum07.HeaderText = "Delta";
            colum07.ReadOnly = true;
            colum07.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum07);

            DataGridViewColumn colum08 = new DataGridViewColumn();
            colum08.CellTemplate = cell0;
            colum08.HeaderText = "Gamma";
            colum08.ReadOnly = true;
            colum08.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum08);

            DataGridViewColumn colum09 = new DataGridViewColumn();
            colum09.CellTemplate = cell0;
            colum09.HeaderText = "Vega";
            colum09.ReadOnly = true;
            colum09.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum09);

            DataGridViewColumn colum10 = new DataGridViewColumn();
            colum10.CellTemplate = cell0;
            colum10.HeaderText = "Theta";
            colum10.ReadOnly = true;
            colum10.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum10);

            DataGridViewColumn colum11 = new DataGridViewColumn();
            colum11.CellTemplate = cell0;
            colum11.HeaderText = "Rho";
            colum11.ReadOnly = true;
            colum11.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum11);

            DataGridViewColumn colum12 = new DataGridViewColumn();
            colum12.CellTemplate = cell0;
            colum12.HeaderText = "Open Interest";
            colum12.ReadOnly = true;
            colum12.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum12);

            DataGridViewColumn colum13 = new DataGridViewColumn();
            colum13.CellTemplate = cell0;
            colum13.HeaderText = "Change Time";
            colum13.ReadOnly = true;
            colum13.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum13);

            _host.Child = newGrid;
            _grid = newGrid;
        }

        private void _tab_NewTabCreateEvent(BotTabSimple tab)
        {
            if(tab.Connector == null)
            {
                return;
            }

            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke((Action<BotTabSimple>)_tab_NewTabCreateEvent, tab);
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

        private void SetDataInTable(OptionMarketData obj, int row)
        {
            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke((Action<OptionMarketData, int>)SetDataInTable, obj, row);
                return;
            }

            if ((_grid.Rows[row].Cells[13].Value == null ||
                _grid.Rows[row].Cells[13].Value.ToString() != obj.TimeCreate.ToString()))
            {                
                _grid.Rows[row].Cells[13].Value = obj.TimeCreate.TimeOfDay.ToString();
            }
            else
            {
                return;
            }

            if (_grid.Rows[row].Cells[1].Value == null ||
                _grid.Rows[row].Cells[1].Value.ToString() != obj.UnderlyingAsset)
            {
                _grid.Rows[row].Cells[1].Value = obj.UnderlyingAsset;
            }

            if (_grid.Rows[row].Cells[2].Value == null ||
                _grid.Rows[row].Cells[2].Value.ToString() != obj.UnderlyingPrice.ToString())
            {
                _grid.Rows[row].Cells[2].Value = obj.UnderlyingPrice;
            }

            if (_grid.Rows[row].Cells[3].Value == null ||
                _grid.Rows[row].Cells[3].Value.ToString() != obj.MarkPrice.ToString())
            {
                _grid.Rows[row].Cells[3].Value = obj.MarkPrice;
            }

            if (_grid.Rows[row].Cells[4].Value == null ||
                _grid.Rows[row].Cells[4].Value.ToString() != obj.MarkIV.ToString())
            {
                _grid.Rows[row].Cells[4].Value = obj.MarkIV;
            }    
            
            if (_grid.Rows[row].Cells[5].Value == null ||
                _grid.Rows[row].Cells[5].Value.ToString() != obj.BidIV.ToString())
            {
                _grid.Rows[row].Cells[5].Value = obj.BidIV;
            }

            if (_grid.Rows[row].Cells[6].Value == null ||
                _grid.Rows[row].Cells[6].Value.ToString() != obj.AskIV.ToString())
            {
                _grid.Rows[row].Cells[6].Value = obj.AskIV;
            }    
            
            if (_grid.Rows[row].Cells[7].Value == null ||
                _grid.Rows[row].Cells[7].Value.ToString() != obj.Delta.ToString())
            {
                _grid.Rows[row].Cells[7].Value = obj.Delta;
            }

            if (_grid.Rows[row].Cells[8].Value == null ||
                _grid.Rows[row].Cells[8].Value.ToString() != obj.Gamma.ToString())
            {
                _grid.Rows[row].Cells[8].Value = obj.Gamma;
            }

            if (_grid.Rows[row].Cells[9].Value == null ||
                _grid.Rows[row].Cells[9].Value.ToString() != obj.Vega.ToString())
            {
                _grid.Rows[row].Cells[9].Value = obj.Vega;
            }

            if (_grid.Rows[row].Cells[10].Value == null ||
                _grid.Rows[row].Cells[10].Value.ToString() != obj.Theta.ToString())
            {
                _grid.Rows[row].Cells[10].Value = obj.Theta;
            }

            if (_grid.Rows[row].Cells[11].Value == null ||
                _grid.Rows[row].Cells[11].Value.ToString() != obj.Rho.ToString())
            {
                _grid.Rows[row].Cells[11].Value = obj.Rho;
            }

            if (_grid.Rows[row].Cells[12].Value == null ||
                _grid.Rows[row].Cells[12].Value.ToString() != obj.OpenInterest.ToString())
            {
                _grid.Rows[row].Cells[12].Value = obj.OpenInterest;
            }                       
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "TestBotOption";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}