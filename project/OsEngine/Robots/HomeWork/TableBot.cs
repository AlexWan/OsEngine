using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Attributes;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System;
using System.IO;

namespace OsEngine.Robots.HomeWork
{
    [Bot("TableBot")]
    public class TableBot : BotPanel
    {
        private BotTabScreener _tab;
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _volume;
        private StrategyParameterDecimal _stopLoss;
        private StrategyParameterDecimal _takeProfit;

        public TableBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _tab = TabsScreener[0];

            _regime = CreateParameter("Regime", "Off", new string[] { "Off", "On" });
            _volume = CreateParameter("Volume of trade", 1, 0.1m, 10, 0.1m);
            _stopLoss = CreateParameter("Stop Loss, points of price step", 1m, 1, 10, 1);
            _takeProfit = CreateParameter("Take Profit, points of price step", 1m, 1, 10, 1);

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;

            this.ParamGuiSettings.Title = "TableBot Parameters";
            this.ParamGuiSettings.Height = 300;
            this.ParamGuiSettings.Width = 600;

            CustomTabToParametersUi customTab = ParamGuiSettings.CreateCustomTab("Table Parameters");

            CreateTable();
            customTab.AddChildren(_host);

            LoadTable();

            CreateButtonAddRow();  
        }

        private void CreateButtonAddRow()
        {
            DataGridViewRow newRow = new DataGridViewRow();

            DataGridViewButtonCell buttonCell = new DataGridViewButtonCell();
            buttonCell.Value = "Add row";

            newRow.Cells.Add(buttonCell);
            newRow.Cells.Add(new DataGridViewTextBoxCell());
            newRow.Cells.Add(new DataGridViewTextBoxCell());
            newRow.Cells.Add(new DataGridViewTextBoxCell());
            newRow.Cells.Add(new DataGridViewTextBoxCell());

            _grid.Rows.Add(newRow);

            _grid.Rows[_grid.Rows.Count - 1].Cells[1].ReadOnly = true;
            _grid.Rows[_grid.Rows.Count - 1].Cells[2].ReadOnly = true;
            _grid.Rows[_grid.Rows.Count - 1].Cells[3].ReadOnly = true;
        }

        private DataGridView _grid;
        
        WindowsFormsHost _host;

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

            newGrid.CellValidating += NewGrid_CellValidating;
            newGrid.CellClick += NewGrid_CellClick;

            newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = newGrid.DefaultCellStyle;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = "Security";
            colum0.ReadOnly = false;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colum01 = new DataGridViewColumn();
            colum01.CellTemplate = cell0;
            colum01.HeaderText = "Number of candles";
            colum01.ReadOnly = false;
            colum01.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum01);

            DataGridViewColumn colum02 = new DataGridViewColumn();
            colum02.CellTemplate = cell0;
            colum02.HeaderText = "Movement for the number of candles (%)";
            colum02.ReadOnly = false;
            colum02.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum02);

            DataGridViewColumn colum03 = new DataGridViewColumn();
            colum03.CellTemplate = cell0;
            colum03.HeaderText = "Side (Buy/Sell)";
            colum03.ReadOnly = false;
            colum03.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum03);

            DataGridViewButtonColumn colum04 = new DataGridViewButtonColumn();           
            colum04.ReadOnly = true;
            colum04.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colum04.UseColumnTextForButtonValue = true;
            colum04.Text = "Delete row";
            newGrid.Columns.Add(colum04);

            _host.Child = newGrid;
            _grid = newGrid;
        }
    
        private void NewGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 4 && e.RowIndex != _grid.Rows.Count-1)
            {
                _grid.Rows.RemoveAt(e.RowIndex);
                SaveTable();
            }

            if (e.ColumnIndex == 0 && e.RowIndex == _grid.Rows.Count - 1)
            {
                DataGridViewRow newRow = new DataGridViewRow();
                _grid.Rows.Insert(_grid.Rows.Count-1, newRow);
            }
        }

        private void NewGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            SaveTable();
        }

        private void NewGrid_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.FormattedValue.ToString() == "")
            {
                return;
            }

            if (e.ColumnIndex == 1 || e.ColumnIndex == 2) 
            {
                if (!int.TryParse(e.FormattedValue.ToString(), out _))
                {
                    e.Cancel = true;
                    SendNewLogMessage("Enter numbers only.", Logging.LogMessageType.Error);
                }
            }

            if (e.ColumnIndex == 3)
            {
                if (e.FormattedValue.ToString().ToLower() == "buy" || e.FormattedValue.ToString().ToLower() == "sell")
                {
                    //                     
                }
                else
                {
                    e.Cancel = true;
                    SendNewLogMessage("Enter Buy or Sell only.", Logging.LogMessageType.Error);
                }
            }            
        }

        private void SaveTable()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"Table.txt", false)
                    )
                {
                    for (int i = 0; i < _grid.Rows.Count-1; i++)
                    {
                        string saveString = "";

                        for (int j = 0; j < _grid.Columns.Count-1; j++)
                        {
                            saveString += _grid.Rows[i].Cells[j].FormattedValue.ToString() + " ";
                        }
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
            _grid.Rows.Clear();
            
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"Table.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"Table.txt"))
                {
                    string line;
                    int counter = 0;
                   
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrEmpty(line))
                        {
                            string[] split = line.Split(' ');
                            _grid.Rows.Add(split);
                            counter++;
                        }
                    }
                    reader.Close();                   
                }                
                _grid.CellValueChanged += NewGrid_CellValueChanged;
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(), Logging.LogMessageType.Error);
            }
        }

        public override string GetNameStrategyType()
        {
            return "TableBot";
        }

        public override void ShowIndividualSettingsDialog()
        {
            
        }

        private void _tab_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }                       

            List<Position> positions = _tab.PositionsOpenAll;

            if (positions.Count > 0)
            {
                return;
            }

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                
                bool flagNullCell = false;

                for (int j = 0; j < _grid.Columns.Count; j++)
                {
                    if (_grid.Rows[i].Cells[j].FormattedValue.ToString().Equals("") || 
                        _grid.Rows[i].Cells[j].FormattedValue.ToString() == "0")
                    {
                        flagNullCell = true;
                        break;
                    }
                }

                if (flagNullCell)
                {
                    continue;
                }

                string securityName = _grid.Rows[i].Cells[0].Value.ToString();
               
                if (securityName == tab.Securiti.Name)
                {
                    int numberOfCandles = int.Parse(_grid.Rows[i].Cells[1].Value.ToString());

                    if (candles.Count < numberOfCandles + 1)
                    {
                        return;
                    }

                    decimal percentMoveCandles = decimal.Parse(_grid.Rows[i].Cells[2].Value.ToString());
                    Side sideOpenPosition = _grid.Rows[i].Cells[3].Value.ToString().ToLower() == "buy" ? Side.Buy : Side.Sell;
                    decimal lastCandleBody = candles[candles.Count - 1].Body;
                    decimal firstCandleOpen = candles[candles.Count - numberOfCandles - 1].Open;
                    decimal penultCandleClose = candles[candles.Count - 2].Close;
                    decimal movementCandles = (penultCandleClose - firstCandleOpen) * percentMoveCandles / 100;


                    if (sideOpenPosition == Side.Buy &&
                        candles[candles.Count - 1].IsUp &&
                        movementCandles > 0 &&
                        movementCandles < lastCandleBody)
                    {
                        tab.BuyAtMarket(_volume.ValueDecimal * tab.Securiti.Lot);
                    }

                    if (sideOpenPosition == Side.Sell &&
                        candles[candles.Count - 1].IsDown &&
                        movementCandles > 0 &&
                        movementCandles < lastCandleBody)
                    {
                        tab.SellAtMarket(_volume.ValueDecimal * tab.Securiti.Lot);
                    }
                }
            }            
        }

    private void _tab_PositionOpeningSuccesEvent(Position position, BotTabSimple tab)
    {
            if (position.Direction == Side.Buy)
            {
                tab.CloseAtStopMarket(position, position.EntryPrice - _stopLoss.ValueDecimal * position.PriceStep);
                tab.CloseAtProfitMarket(position, position.EntryPrice + _takeProfit.ValueDecimal * position.PriceStep);
            }
            else
            {
                tab.CloseAtStopMarket(position, position.EntryPrice + _stopLoss.ValueDecimal * position.PriceStep);
                tab.CloseAtProfitMarket(position, position.EntryPrice - _takeProfit.ValueDecimal * position.PriceStep);
            }
            
        }
    }
}
