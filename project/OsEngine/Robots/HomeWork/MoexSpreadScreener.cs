using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;

namespace OsEngine.Robots.HomeWork
{
    [Bot("MoexSpreadScreener")]
    public class MoexSpreadScreener : BotPanel
    {
        private BotTabScreener _tab;
        private StrategyParameterString _regime;
        private List<string> _listSecurities;
        private List<string> _listMathSign = new List<string> { @"/", @"-" };
        private int _countCell = 7;

        public MoexSpreadScreener(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _tab = TabsScreener[0];

            _regime = CreateParameter("Regime", "Off", new string[] { "Off", "On" });

            this.ParamGuiSettings.Title = "TableBot Parameters";
            this.ParamGuiSettings.Height = 300;
            this.ParamGuiSettings.Width = 600;

            CustomTabToParametersUi customTab = ParamGuiSettings.CreateCustomTab("Table Parameters");

            CreateTable();
           //CreateChart();
            customTab.AddChildren(_host);            

            _tab.CandleUpdateEvent += _tab_CandleUpdateEvent;
            _tab.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent;

           
        }

        bool isLoad = false;

        private void _tab_MarketDepthUpdateEvent(MarketDepth arg1, BotTabSimple arg2)
        {
            if (!isLoad)
            {
                _listSecurities = GetListSecuruties();
                CreateButtonAddRow();
                CreateChart();
                LoadTable();
                isLoad = true;
            }
        }

        public override string GetNameStrategyType()
        {
            return "MoexSpreadScreener";
        }

        public override void ShowIndividualSettingsDialog()
        {
        }

        private void CreateCustomContainer()
        {
            var container = new TableLayoutPanel();
            container.Dock = DockStyle.Fill;

            CreateTable();

            // Настройка размеров
            container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            container.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            container.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
            container.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        private void CreateButtonAddRow()
        {
            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke(new Action(CreateButtonAddRow));
                return;
            }

            DataGridViewRow newRow = new DataGridViewRow();

            DataGridViewButtonCell buttonCell = new DataGridViewButtonCell();
            buttonCell.Value = "Add Row";

            newRow.Cells.Add(buttonCell);

            for (int i = 0; i < _countCell; i++)
            {
                newRow.Cells.Add(new DataGridViewTextBoxCell());
            }
                       
            newRow.ReadOnly = true;
            _grid.Rows.Add(newRow);

           
        }

        private Chart _chart;

        private void CreateChart()
        {
            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke(new Action(CreateChart));
                return;
            }

            DataGridViewRow newRow = new DataGridViewRow();
            newRow.Cells.Clear();/*
            newRow.Cells.Add(new DataGridViewTextBoxCell());
            
            newRow.Cells[0].Value = "Width Column";*/
            
            newRow.ReadOnly = true;
            _grid.Rows.Add(newRow);

        }

        private WindowsFormsHost _host;

        private DataGridView _grid;

        private void CreateTable()
        {
            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke(new Action(CreateTable));
                return;
            }

            _host = new WindowsFormsHost();

            DataGridView newGrid =
                DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCells);

            newGrid.CellClick += NewGrid_CellClick;
            newGrid.CellValueChanged += NewGrid_CellValueChanged;

            newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = newGrid.DefaultCellStyle;

            DataGridViewComboBoxColumn colum0 = new DataGridViewComboBoxColumn();
            //colum0.CellTemplate = cell0;
            colum0.HeaderText = "First Security";
            colum0.ReadOnly = false;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum0);

            DataGridViewComboBoxColumn colum01 = new DataGridViewComboBoxColumn();
            //colum01.CellTemplate = cell0;
            colum01.HeaderText = "Second Security";
            colum01.ReadOnly = false;
            colum01.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum01);

            DataGridViewColumn colum02 = new DataGridViewColumn();
            colum02.CellTemplate = cell0;
            colum02.HeaderText = "Price First Security";
            colum02.ReadOnly = false;
            colum02.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum02);

            DataGridViewColumn colum03 = new DataGridViewColumn();
            colum03.CellTemplate = cell0;
            colum03.HeaderText = "Price Second Security";
            colum03.ReadOnly = false;
            colum03.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum03);

            DataGridViewComboBoxColumn columMathSign = new DataGridViewComboBoxColumn();            
            columMathSign.HeaderText = "Math Sign";
            columMathSign.ReadOnly = false;
            columMathSign.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(columMathSign);

            DataGridViewColumn columMultiplier = new DataGridViewColumn();
            columMultiplier.CellTemplate = cell0;
            columMultiplier.HeaderText = "Muliplier Second Security";
            columMultiplier.ReadOnly = false;
            columMultiplier.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(columMultiplier);

            DataGridViewColumn colum04 = new DataGridViewColumn();
            colum04.CellTemplate = cell0;
            colum04.HeaderText = "Spread";
            colum04.ReadOnly = false;
            colum04.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum04);

            DataGridViewButtonColumn colum05 = new DataGridViewButtonColumn();
            colum05.ReadOnly = true;
            colum05.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colum05.UseColumnTextForButtonValue = true;
            colum05.Text = "Delete Row";
            newGrid.Columns.Add(colum05);

            _host.Child = newGrid;
            _grid = newGrid;
        }

        private void NewGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 7 && e.RowIndex < _grid.Rows.Count - 2)
            {
                _grid.Rows.RemoveAt(e.RowIndex);                               
            }

            if (e.ColumnIndex == 0 && e.RowIndex == _grid.Rows.Count - 2)
            {
                _listSecurities = GetListSecuruties();
                SetNewRowsSpread();                
            }

            SaveTable();
        }

        private void NewGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 0 && e.RowIndex < _grid.Rows.Count - 2)
            {
                GetLastPriceSecurityInTable(e.ColumnIndex, e.RowIndex);
            }
            if (e.ColumnIndex == 1 && e.RowIndex < _grid.Rows.Count - 2)
            {
                GetLastPriceSecurityInTable(e.ColumnIndex, e.RowIndex);
            }
            
            SetSpreadInCell(e.RowIndex);

            SaveTable();
        }

        private void GetLastPriceSecurityInTable(int columnIndex, int rowIndex)
        {
            if (_grid[columnIndex, rowIndex].Value == null)
            {
                return;
            }

            string securuty = _grid[columnIndex, rowIndex].Value.ToString();

            for (int i = 0; i < _tab.Tabs.Count; i++)
            {
                if (_tab.Tabs[i].Securiti.NameFull == securuty)
                {
                    if (columnIndex == 0)
                    {
                        _grid[2, rowIndex].Value = _tab.Tabs[i].CandlesAll[_tab.Tabs[i].CandlesAll.Count - 1].Close;
                    }
                    if (columnIndex == 1)
                    {
                        _grid[3, rowIndex].Value = _tab.Tabs[i].CandlesAll[_tab.Tabs[i].CandlesAll.Count - 1].Close;
                    }
                }
            }
        }

        private void _tab_CandleUpdateEvent(List<Candle> candles, BotTabSimple tab)
        {
            SetSpreadInTable(candles, tab);
        }

        private void SetSpreadInTable(List<Candle> candles, BotTabSimple tab)
        {
            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke((Action<List<Candle>, BotTabSimple>)SetSpreadInTable, candles, tab);
                return;
            }

            if (_grid == null)
            {
                return;
            }

            if (_grid.Rows.Count <= 1)
            {
                return;
            }

            for (int i = 0; i < _grid.Rows.Count - 1; i++)
            {
                if (_grid.Rows[i].Cells[0].Value == null ||
                    _grid.Rows[i].Cells[1].Value == null)
                {
                    continue;
                }

                if (_grid.Rows[i].Cells[0].Value.ToString() == tab.Securiti.NameFull)
                {
                    _grid.Rows[i].Cells[2].Value = candles[candles.Count - 1].Close.ToString();
                }
                if (_grid.Rows[i].Cells[1].Value.ToString() == tab.Securiti.NameFull)
                {
                    _grid.Rows[i].Cells[3].Value = candles[candles.Count - 1].Close.ToString();
                }

                SetSpreadInCell(i);                
            }
        }

        private void SetSpreadInCell(int i)
        {
            try
            {
                if (_grid.Rows[i].Cells[4].Value == null)
                {
                    return;
                }
                if (_grid.Rows[i].Cells[5].Value == null ||
                    Convert.ToDecimal(_grid.Rows[i].Cells[5].Value) == 0)
                {
                    _grid.Rows[i].Cells[5].Value = 1;
                }

                string mathSign = _listMathSign[0];

                if (_grid.Rows[i].Cells[2].Value != null &&
                        _grid.Rows[i].Cells[3].Value != null)
                {
                    if (_grid.Rows[i].Cells[4].Value.ToString() == @"-")
                    {
                        mathSign = @"-";
                    }
                    _grid.Rows[i].Cells[6].Value = CalculateSpread(Convert.ToDecimal(_grid.Rows[i].Cells[2].Value), Convert.ToDecimal(_grid.Rows[i].Cells[3].Value), mathSign, Convert.ToDecimal(_grid.Rows[i].Cells[5].Value));
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.Message, Logging.LogMessageType.Error);
            }            
        }

        private decimal CalculateSpread(decimal price1, decimal price2, string mathSign, decimal mult)
        {           
            if (mathSign == @"-")
            {
                return CalculateRound(price1 - price2 * mult);
            }

            return CalculateRound(price1 / (price2 * mult));
        }

        private decimal CalculateRound(decimal number)
        {
            if (number > 1)
            {
                return Math.Round(number, 2, MidpointRounding.AwayFromZero);
            }

            return Math.Round(number, 8, MidpointRounding.AwayFromZero);
        }
               
        private void SetNewRowsSpread()
        {        
            DataGridViewRow newRow = new DataGridViewRow();

            newRow.Cells.Add(new DataGridViewComboBoxCell() { DataSource = _listSecurities });

            newRow.Cells.Add(new DataGridViewComboBoxCell() { DataSource = _listSecurities });

            newRow.Cells.Add(new DataGridViewTextBoxCell());
            newRow.Cells.Add(new DataGridViewTextBoxCell());
            newRow.Cells.Add(new DataGridViewComboBoxCell()
            { 
                DataSource = _listMathSign,
                Value = _listMathSign[0],                
            }
            );
            newRow.Cells.Add(new DataGridViewTextBoxCell());
            newRow.Cells.Add(new DataGridViewTextBoxCell());

            newRow.Cells[2].ReadOnly = true;
            newRow.Cells[3].ReadOnly = true;
            newRow.Cells[6].ReadOnly = true;

            _grid.Rows.Insert(_grid.Rows.Count - 2, newRow);
        }

        private void SaveTable()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SpreadScreener.txt", false)
                    )
                {
                    for (int i = 0; i < _grid.Rows.Count - 2; i++)
                    {
                        string saveString = "";

                        saveString += _grid.Rows[i].Cells[0].FormattedValue.ToString() + " ";
                        saveString += _grid.Rows[i].Cells[1].FormattedValue.ToString() + " ";
                        saveString += _grid.Rows[i].Cells[4].FormattedValue.ToString() + " ";
                        saveString += _grid.Rows[i].Cells[5].FormattedValue.ToString();                        

                        writer.WriteLine(saveString);
                    }
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void LoadTable()
        {
            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke(new Action(LoadTable));
                return;
            }

            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SpreadScreener.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SpreadScreener.txt"))
                {
                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrEmpty(line))
                        {
                            string[] split = line.Split(' ');
                           
                            if (_listSecurities.Contains(split[0]) &&
                                _listSecurities.Contains(split[1]))
                            {
                                DataGridViewRow newRow = new DataGridViewRow();

                                newRow.Cells.Add(new DataGridViewComboBoxCell() { DataSource = _listSecurities, Value = split[0] });

                                newRow.Cells.Add(new DataGridViewComboBoxCell() { DataSource = _listSecurities, Value = split[1] });

                                newRow.Cells.Add(new DataGridViewTextBoxCell());
                                newRow.Cells.Add(new DataGridViewTextBoxCell());
                                newRow.Cells.Add(new DataGridViewComboBoxCell()
                                {
                                    DataSource = _listMathSign,
                                    Value = split[2],
                                }
                                );
                                newRow.Cells.Add(new DataGridViewTextBoxCell() { Value = split[3] });
                                newRow.Cells.Add(new DataGridViewTextBoxCell());

                                newRow.Cells[2].ReadOnly = true;
                                newRow.Cells[3].ReadOnly = true;
                                newRow.Cells[6].ReadOnly = true;

                                _grid.Rows.Insert(_grid.Rows.Count - 2, newRow);
                            }
                            else
                            {
                                continue;
                            }
                            
                        }
                    }
                    reader.Close();
                }
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(), Logging.LogMessageType.Error);
            }
        }

        public List<string> GetListSecuruties()
        {
            if (_tab.Tabs.Count == 0)
            {
                return null;
            }

            List<string> list = new List<string>();

            for (int i = 0; i < _tab.Tabs.Count; i++)
            {
                if (_tab.Tabs[i].Securiti != null)
                {
                    list.Add(_tab.Tabs[i].Securiti.NameFull);
                }                
            }
            return list;
        }
        
    }    
}

