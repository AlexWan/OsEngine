using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

/*Description
Sample “Custom Table In The Param Window” for osengine.

It shows:
• Dynamic table: The table is updated in real time as new data arrives.
• User Interaction: The user can change data in the table and get values ​​in specific cells.
• Customizable parameters: Ability to turn the robot on and off and also set a trailing stop for exit.

*/

namespace OsEngine.Robots.TechSamples
{
    [Bot("CustomTableInTheParamWindowSample")]
    public class CustomTableInTheParamWindowSample : BotPanel
    {

        #region Parameters and service

        public override string GetNameStrategyType()
        {
            return "CustomTableInTheParamWindowSample";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        public CustomTableInTheParamWindowSample(string name, StartProgram startProgram)
          : base(name, startProgram)
        {
            _regime = CreateParameter("Regime", "Off", new string[] { "Off", "On" }, " Base settings ");
            _trailingValue = CreateParameter("TrailingValue", 1, 1.0m, 10, 1, "Exit settings");

            TabCreate(BotTabType.Screener);
            _tab = TabsScreener[0];

            // customization param ui

            this.ParamGuiSettings.Title = " Bot settings ";
            this.ParamGuiSettings.Height = 800;
            this.ParamGuiSettings.Width = 780;

            // create table

            CustomTabToParametersUi customTabOrderGrid = ParamGuiSettings.CreateCustomTab(" Table settings ");
            CreateColumnsTable();
            customTabOrderGrid.AddChildren(_hostTable);

            // load data table

            LoadLines();

            // events

            _tab.NewTabCreateEvent += tab_NewTabCreateEvent;
            this.DeleteEvent += DeleteBotEvent;
            _tableDataGrid.CellValueChanged += CellValueChanged;

            Description = "Sample “Custom Table In The Param Window” for osengine. " +
                "It shows: " +
                "• Dynamic table: The table is updated in real time as new data arrives. " +
                "• User Interaction: The user can change data in the table and get values ​​in specific cells. " +
                "• Customizable parameters: Ability to turn the robot on and off and also set a trailing stop for exit.";
        }

        private BotTabScreener _tab;

        private StrategyParameterString _regime;

        private StrategyParameterDecimal _trailingValue;

        private List<TableBotLine> Lines = new List<TableBotLine>();

        private DataGridView _tableDataGrid;

        WindowsFormsHost _hostTable;

        /// <summary>
        /// Event of changes values the table / Событие изменений значений в таблице
        /// </summary>
        private void CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                for (int i = 0; i < Lines.Count; i++)
                {
                    Lines[i].Security = _tableDataGrid.Rows[i].Cells[0].Value.ToString();
                    Lines[i].CandelCount = Convert.ToInt32(_tableDataGrid.Rows[i].Cells[1].Value.ToString());
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

        /// <summary>
        /// Delete Bot Event / Событие удаления робота
        /// </summary>
        private void DeleteBotEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"Lines.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"Lines.txt");
            }
        }

        /// <summary>
        /// Save line event / событие сохранения линий
        /// </summary>
        public void SaveLines()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"Lines.txt", false)
                )
                {

                    for (int i = 0; i < _tableDataGrid.Rows.Count; i++)
                    {
                        writer.Write(_tableDataGrid.Rows[i].Cells[0].Value.ToString() +' ');
                        writer.Write(_tableDataGrid.Rows[i].Cells[1].Value.ToString() + ' ');
                        writer.Write(_tableDataGrid.Rows[i].Cells[2].Value.ToString() + ' ');
                        writer.Write(_tableDataGrid.Rows[i].Cells[3].Value.ToString() + ' ');
                        writer.WriteLine(_tableDataGrid.Rows[i].Cells[4].Value.ToString());
                    }
                    writer.Close();
                }
            }
            catch (Exception ex)
            {
                _tab.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        /// <summary>
        /// Loading saved table data / загрузка сохраненных данных таблицы
        /// </summary>
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

        /// <summary>
        /// New tab creation event / событие добавления нового инструмента
        /// </summary>
        private void tab_NewTabCreateEvent(BotTabSimple newTab)
        {
            newTab.CandleFinishedEvent += (List<Candle> candles) =>
            {
                AddNewLineInTable(newTab);
                SortLine();
                MovementPercentUpdate(candles ,newTab);
                NewCandleEvent(candles, newTab);
            };
        }

        /// <summary>
        /// Column creation event / событие создания колонок
        /// </summary>
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
                newColumn1.HeaderText = "Count candel";
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

        /// <summary>
        /// Event of adding new lines to the table / Событие добавления новых линий в таблицу
        /// </summary>
        private void AddNewLineInTable(BotTabSimple tab)
        {
            if(tab.Securiti == null)
            {
                return;
            }

            bool GotLine = false;

            if (_tableDataGrid.Rows.Count == 0)
            {
                TableBotLine newLine = new TableBotLine();

                newLine.Security = tab.Securiti.Name;

                newLine.CandelCount = 100;

                newLine.MovementToEnter = 0;

                newLine.CurrentMovement = 0;

                newLine.Side = Side.Sell;

                Lines.Add(newLine);

                CreateRowsTable(0);

                return;
            }

            for (int i = 0; i < _tableDataGrid.Rows.Count; i++)
            {
                if (_tableDataGrid.Rows[i].Cells[0].Value.ToString() == tab.Securiti.Name)
                {
                    GotLine = true;
                    break;
                }
            }

            if (GotLine == false)
            {
                TableBotLine newLine = new TableBotLine();

                newLine.Security = tab.Securiti.Name;

                newLine.CandelCount = 100;

                newLine.MovementToEnter = 0;

                newLine.CurrentMovement = 0;

                newLine.Side = Side.Sell;

                Lines.Add(newLine);

                CreateRowsTable(_tableDataGrid.Rows.Count);
            }
        }

        /// <summary>
        /// Create line in table / Событие создания новых линий в таблице
        /// </summary>
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
                candelCount.Value = Lines[index].CandelCount.ToString();
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

        /// <summary>
        /// Line sorting method / Метод сортировки линий
        /// </summary>
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
                        if (_tableDataGrid.Rows[i].Cells[0].Value.ToString() == _tab.Tabs[j].Securiti.Name)
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

        /// <summary>
        /// Movement percentage update event / Событие обновления текущего процента движения
        /// </summary>
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
                    if (Lines[i].Security == tab.Securiti.Name)
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
                        Lines[indexLine].CurrentMovement = Math.Round((candles[candles.Count - Lines[indexLine].CandelCount].Close - candles[candles.Count - 1].Close) / 
                            candles[candles.Count - 1].Close * 100, 2);
                        _tableDataGrid.Rows[indexLine].Cells[3].Value = Lines[indexLine].CurrentMovement.ToString() + '%';
                    }
                    else
                    {
                        Lines[indexLine].CurrentMovement = Math.Round((candles[candles.Count - 1].Close - candles[candles.Count - Lines[indexLine].CandelCount].Close) / 
                            candles[candles.Count - Lines[indexLine].CandelCount].Close * 100, 2);
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

        /// <summary>
        ///  Trade logic / Торговая логика
        /// </summary>
        private void NewCandleEvent(List<Candle> candles, BotTabSimple tab)
        {

            if (_regime.ValueString == "Off")
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
                if (Lines[i].Security == tab.Securiti.Name)
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
            { // логика открытия

                decimal movementToEnter = _tableDataGrid.Rows[indexLine].Cells[2].Value.ToString().ToDecimal();

                if (movementToEnter < Lines[indexLine].CurrentMovement)
                {
                    if (Lines[indexLine].Side == Side.Buy)
                    {
                        tab.BuyAtMarket(1);
                    }
                    else
                    {
                        tab.SellAtMarket(1);
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
                    {
                        decimal low = candles[candles.Count - 1].Low;
                        stopPriсe = low - low * _trailingValue.ValueDecimal / 100;
                    }
                }
                else // If the direction of the position is sale
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPriсe = high + high * _trailingValue.ValueDecimal / 100;
                }
                tab.CloseAtTrailingStop(positions[0], stopPriсe, stopPriсe);
            }
        }

        #endregion
    }

    public class TableBotLine
    {
        public string Security;

        public int CandelCount;

        public decimal MovementToEnter;

        public decimal CurrentMovement;

        public Side Side;

        public void SetFromStr(string str)
        {
            string[] saveArray = str.Split(' ');

            Security = saveArray[0].ToString();
            CandelCount = Convert.ToInt32(saveArray[1]);
            MovementToEnter = saveArray[2].ToDecimal();
            saveArray[3] = saveArray[3].Remove(saveArray[3].Length-1);
            CurrentMovement = saveArray[3].ToDecimal();
            Enum.TryParse(saveArray[4], out Side);
        }
    }
}
