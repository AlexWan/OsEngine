/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Language;

/*Description
Sample “Custom Table In The Param Window” for osengine.

It shows:
• Dynamic table: The table is updated in real time as new data arrives.
• User Interaction: The user can change data in the table and get values ​​in specific cells.
• Customizable parameters: Ability to turn the robot on and off and also set a trailing stop for exit.
*/

namespace OsEngine.Robots.TechSamples
{
    [Bot("CustomTableInParamWindowSample")] // We create an attribute so that we don't write anything to the BotFactory
    public class CustomTableInParamWindowSample : BotPanel
    {

        #region Parameters and service

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CustomTableInParamWindowSample";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Screener tabs
        private BotTabScreener _tab;

        // Basic settings
        public StrategyParameterString Regime;
        public StrategyParameterInt MaxPositions;

        // GetVolume settings
        public StrategyParameterString VolumeType;
        public StrategyParameterDecimal Volume;
        public StrategyParameterString TradeAssetInPortfolio;
        
        // Exit setting
        public StrategyParameterDecimal TrailingValue;

        // Lines
        private List<TableBotLine> Lines = new List<TableBotLine>();

        // Table data
        private DataGridView _tableDataGrid;

        // Host table
        private WindowsFormsHost _hostTable;

        public CustomTableInParamWindowSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create tabs
            TabCreate(BotTabType.Screener);
            _tab = TabsScreener[0];

            // Basic settings
            Regime = CreateParameter("Regime", "Off", new string[] { "Off", "On" }, " Base settings ");
            MaxPositions = CreateParameter("Max positions", 5, 0, 20, 1, " Base settings ");
            
            // GetVolume settings
            VolumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" }, " Base settings ");
            Volume = CreateParameter("Volume", 1, 1.0m, 50, 4, " Base settings ");
            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", " Base settings ");
            
            // Exit setting
            TrailingValue = CreateParameter("TrailingValue", 1, 1.0m, 10, 1, " Base settings ");

            // Customization param ui
            this.ParamGuiSettings.Title = " Bot settings ";
            this.ParamGuiSettings.Height = 800;
            this.ParamGuiSettings.Width = 780;

            // Create table
            CustomTabToParametersUi customTabOrderGrid = ParamGuiSettings.CreateCustomTab(" Table settings ");
            CreateColumnsTable();
            customTabOrderGrid.AddChildren(_hostTable);

            // Load data table
            LoadLines();

            // Events
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
            this.DeleteEvent += DeleteBotEvent;
            _tableDataGrid.CellValueChanged += CellValueChanged;

            Description = OsLocalization.Description.DescriptionLabel104;
        }

        // Event of changes values the table
        private void CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                for (int i = 0; i < Lines.Count; i++)
                {
                    Lines[i].Security = _tableDataGrid.Rows[i].Cells[0].Value.ToString();
                    Lines[i].CandleCount = Convert.ToInt32(_tableDataGrid.Rows[i].Cells[1].Value.ToString());
                    Lines[i].MovementToEnter = _tableDataGrid.Rows[i].Cells[2].Value.ToString().ToDecimal();
                    Lines[i].Side = (Side)Enum.Parse(typeof(Side), _tableDataGrid.Rows[i].Cells[4].Value.ToString(), true);
                }

                SaveLines();
            }
            catch(Exception ex)
            {
                _tab.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        // Delete Bot Event
        private void DeleteBotEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"Lines.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"Lines.txt");
            }
        }

        // Save line event
        public void SaveLines()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"Lines.txt", false))
                {
                    for (int i = 0; i < Lines.Count; i++)
                    {
                        writer.WriteLine(Lines[i].GetSaveStr());
                    }

                    writer.Close();
                }
            }
            catch (Exception ex)
            {
                _tab.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        // Loading saved table data
        public void LoadLines()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"Lines.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"Lines.txt"))
                {
                    while (reader.EndOfStream == false)
                    {
                        TableBotLine newLine = new TableBotLine();
                        newLine.SetFromStr(reader.ReadLine());
                        Lines.Add(newLine);
                    }

                    reader.Close();
                }

                int cnt = 0;

                while (Lines.Count > cnt)
                {
                    CreateRowsTable(cnt);
                    cnt++;
                }
            }
            catch (Exception ex)
            {
                _tab.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region Table

        private void _tab_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            AddNewLineInTable(tab);
            SortLine();
            MovementPercentUpdate(candles, tab);
            TradeLogicMethod(candles, tab);
        }

        // Column creation event
        private void CreateColumnsTable()
        {
            try
            {
                if (MainWindow.GetDispatcher.CheckAccess() == false)
                {
                    MainWindow.GetDispatcher.Invoke(new Action(CreateColumnsTable));
                    return;
                }

                _hostTable = new WindowsFormsHost();

                _tableDataGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                       DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders);
                _tableDataGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                _tableDataGrid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                _tableDataGrid.RowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

                DataGridViewTextBoxCell cellParam0 = new DataGridViewTextBoxCell();
                cellParam0.Style = _tableDataGrid.DefaultCellStyle;
                cellParam0.Style.WrapMode = DataGridViewTriState.True;

                DataGridViewColumn newColumn0 = new DataGridViewColumn();
                newColumn0.CellTemplate = cellParam0;
                newColumn0.HeaderText = "Security";
                _tableDataGrid.Columns.Add(newColumn0);
                newColumn0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn1 = new DataGridViewColumn();
                newColumn1.CellTemplate = cellParam0;
                newColumn1.HeaderText = "Candle Count";
                _tableDataGrid.Columns.Add(newColumn1);
                newColumn1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn2 = new DataGridViewColumn();
                newColumn2.CellTemplate = cellParam0;
                newColumn2.HeaderText = "Movement to enter";
                _tableDataGrid.Columns.Add(newColumn2);
                newColumn2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn3 = new DataGridViewColumn();
                newColumn3.CellTemplate = cellParam0;
                newColumn3.HeaderText = "Сurrent movement";
                _tableDataGrid.Columns.Add(newColumn3);
                newColumn3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn4 = new DataGridViewColumn();
                newColumn4.CellTemplate = cellParam0;
                newColumn4.HeaderText = "Side";
                _tableDataGrid.Columns.Add(newColumn4);
                newColumn4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _hostTable.Child = _tableDataGrid;
            }
            catch (Exception ex)
            {
                _tab.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        // Event of adding new lines to the table
        private void AddNewLineInTable(BotTabSimple tab)
        {
            if(tab.Security == null)
            {
                return;
            }

            bool GotLine = false;

            for (int i = 0; _tableDataGrid.Rows != null && i < _tableDataGrid.Rows.Count; i++)
            {
                if (_tableDataGrid.Rows[i].Cells[0].Value.ToString() == tab.Security.Name)
                {
                    GotLine = true;
                    break;
                }
            }

            if (GotLine == false)
            {
                TableBotLine newLine = new TableBotLine();

                newLine.Security = tab.Security.Name;

                newLine.CandleCount = 100;

                newLine.MovementToEnter = 1;

                newLine.CurrentMovement = 0;

                newLine.Side = Side.Sell;

                Lines.Add(newLine);

                CreateRowsTable(_tableDataGrid.Rows.Count);
            }
        }

        // Create line in table
        private void CreateRowsTable(int index)
        {
            if (_tableDataGrid.InvokeRequired)
            {
                _tableDataGrid.Invoke(new Action(() => CreateRowsTable(index)));
                return;
            }

            try
            {
                _tableDataGrid.Rows.Add(CreateLine(index));

                SaveLines();
            }
            catch(Exception ex)
            {
                _tab.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        private DataGridViewRow CreateLine(int index)
        {
            DataGridViewRow row = new DataGridViewRow();

            DataGridViewTextBoxCell security = new DataGridViewTextBoxCell();
            row.Cells.Add(security);
            security.ReadOnly = true;

            DataGridViewTextBoxCell candelCount = new DataGridViewTextBoxCell();
            row.Cells.Add(candelCount);

            DataGridViewTextBoxCell movementToEnter = new DataGridViewTextBoxCell();
            row.Cells.Add(movementToEnter);

            DataGridViewTextBoxCell currentMovement = new DataGridViewTextBoxCell();
            row.Cells.Add(currentMovement);
            currentMovement.ReadOnly = true;

            DataGridViewComboBoxCell sideBox = new DataGridViewComboBoxCell();
            sideBox.Items.Add("Buy");
            sideBox.Items.Add("Sell");
            row.Cells.Add(sideBox);

            try
            {
                security.Value = Lines[index].Security.ToString();
                candelCount.Value = Lines[index].CandleCount.ToString();
                movementToEnter.Value = Lines[index].MovementToEnter.ToString();
                currentMovement.Value = Lines[index].CurrentMovement.ToString()+'%';
                sideBox.Value = Lines[index].Side == OsEngine.Entity.Side.Buy ? "Buy" : "Sell";
            }
            catch (Exception ex)
            {
                _tab.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }

            return row;
        }

        // Line sorting method
        private void SortLine()
        {
            if (_tableDataGrid.InvokeRequired)
            {
                _tableDataGrid.Invoke(new Action(SortLine));
                return;
            }

            try
            {
                if (_tableDataGrid.Rows[_tableDataGrid.Rows.Count - 1].Cells[0].Value.ToString() == null)
                {
                    return;
                }

                bool GotLine = false;

                int indexLine = 0;

                for (int i = 0; i < _tableDataGrid.Rows.Count; i++)
                {
                    GotLine = false;

                    for (int j = 0; j < _tab.Tabs.Count; j++)
                    {
                        if (_tableDataGrid.Rows[i].Cells[0].Value.ToString() == _tab.Tabs[j].Security.Name)
                        {
                            GotLine = true;
                        }
                    }

                    if (GotLine == false)
                    {
                        indexLine = i;
                        break;
                    }
                }

                if (GotLine == false)
                {
                    DataGridViewRow rowToDelete = _tableDataGrid.Rows[indexLine];
                    _tableDataGrid.Rows.Remove(rowToDelete);

                    SaveLines();

                    return;
                }
            }
            catch (Exception ex)
            {
                _tab.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        // Movement percentage update event
        private void MovementPercentUpdate(List<Candle> candles, BotTabSimple tab)
        {
            if (_tableDataGrid.InvokeRequired)
            {
                _tableDataGrid.Invoke(new Action(() => MovementPercentUpdate(candles, tab)));
                return;
            }

            try
            {
                int indexLine = 0;
                bool GotLine = false;

                for (int i = 0; i < Lines.Count; i++)
                {
                    if (Lines[i].Security == tab.Security.Name)
                    {
                        indexLine = i;
                        GotLine = true;
                        break;
                    }
                }

                int TableCandlesCount = Convert.ToInt32(_tableDataGrid.Rows[indexLine].Cells[1].Value.ToString());

                if (candles.Count < TableCandlesCount)
                {
                    return;
                }

                if (GotLine == false)
                {
                    return;
                }
                else
                {
                    if (Lines[indexLine].Side == Side.Buy)
                    {
                        Lines[indexLine].CurrentMovement = Math.Round((candles[candles.Count - Lines[indexLine].CandleCount].Close - candles[candles.Count - 1].Close) / 
                            candles[candles.Count - 1].Close * 100, 2);
                        _tableDataGrid.Rows[indexLine].Cells[3].Value = Lines[indexLine].CurrentMovement.ToString() + '%';
                    }
                    else
                    {
                        Lines[indexLine].CurrentMovement = Math.Round((candles[candles.Count - 1].Close - candles[candles.Count - Lines[indexLine].CandleCount].Close) / 
                            candles[candles.Count - Lines[indexLine].CandleCount].Close * 100, 2);
                        _tableDataGrid.Rows[indexLine].Cells[3].Value = Lines[indexLine].CurrentMovement.ToString() + '%';
                    }
                }

                SaveLines();
            }
            catch (Exception ex)
            {
                _tab.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region TradeLogic

        //  Trade logic
        private void TradeLogicMethod(List<Candle> candles, BotTabSimple tab)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count == 0)
            {
                return;
            }

            int indexLine = 0;
            bool GotLine = false;

            for(int i = 0;  i < Lines.Count; i++)
            {
                if (Lines[i].Security == tab.Security.Name)
                {
                    indexLine = i; 
                    GotLine = true;
                    break;
                }
            }

            if (GotLine == false) 
            {
                return;
            }

            List<Position> positions = tab.PositionsOpenAll;

            if (positions.Count == 0)
            { // opening logic

                if(_tab.PositionsOpenAll.Count >= MaxPositions.ValueInt)
                {
                    return;
                }

                decimal movementToEnter = _tableDataGrid.Rows[indexLine].Cells[2].Value.ToString().ToDecimal();

                if (movementToEnter < Lines[indexLine].CurrentMovement)
                {
                    if (Lines[indexLine].Side == Side.Buy)
                    {
                        tab.BuyAtMarket(GetVolume(tab));
                    }
                    else
                    {
                        tab.SellAtMarket(GetVolume(tab));
                    }
                }
            }
            else
            {
                if (positions[0].State != PositionStateType.Open)
                {
                    return;
                }

                decimal stopPriсe;

                if (positions[0].Direction == Side.Buy) // If the direction of the position is buy
                {
                    
                    decimal low = candles[candles.Count - 1].Low;
                    stopPriсe = low - low * TrailingValue.ValueDecimal / 100;
                    
                }
                else // If the direction of the position is sale
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPriсe = high + high * TrailingValue.ValueDecimal / 100;
                }

                tab.CloseAtTrailingStop(positions[0], stopPriсe, stopPriсe);
            }
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (VolumeType.ValueString == "Contracts")
            {
                volume = Volume.ValueDecimal;
            }
            else if (VolumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = Volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = Volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (VolumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (TradeAssetInPortfolio.ValueString == "Prime")
                {
                    portfolioPrimeAsset = myPortfolio.ValueCurrent;
                }
                else
                {
                    List<PositionOnBoard> positionOnBoard = myPortfolio.GetPositionOnBoard();

                    if (positionOnBoard == null)
                    {
                        return 0;
                    }

                    for (int i = 0; i < positionOnBoard.Count; i++)
                    {
                        if (positionOnBoard[i].SecurityNameCode == TradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + TradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (Volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    if (tab.Security.UsePriceStepCostToCalculateVolume == true
                     && tab.Security.PriceStep != tab.Security.PriceStepCost
                     && tab.PriceBestAsk != 0
                     && tab.Security.PriceStep != 0
                     && tab.Security.PriceStepCost != 0)
                    {// расчёт количества контрактов для фьючерсов и опционов на Мосбирже
                        qty = moneyOnPosition / (tab.PriceBestAsk / tab.Security.PriceStep * tab.Security.PriceStepCost);
                    }
                    qty = Math.Round(qty, tab.Security.DecimalsVolume);
                }
                else
                {
                    qty = Math.Round(qty, 7);
                }

                return qty;
            }

            return volume;
        }

        #endregion
    }

    public class TableBotLine
    {
        public string Security;

        public int CandleCount;

        public decimal MovementToEnter;

        public decimal CurrentMovement;

        public Side Side;

        public string GetSaveStr()
        {
            string saveStr = "";

            saveStr += Security + "%";
            saveStr += CandleCount + "%";
            saveStr += MovementToEnter + "%";
            saveStr += CurrentMovement + "%";
            saveStr += Side;

            return saveStr;
        }

        public void SetFromStr(string str)
        {
            string[] saveArray = str.Split('%');

            Security = saveArray[0].ToString();
            CandleCount = Convert.ToInt32(saveArray[1]);
            MovementToEnter = saveArray[2].ToDecimal();
            CurrentMovement = saveArray[3].ToDecimal();
            Enum.TryParse(saveArray[4], out Side);
        }
    }
}