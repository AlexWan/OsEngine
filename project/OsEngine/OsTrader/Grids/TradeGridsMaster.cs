/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace OsEngine.OsTrader.Grids
{
    public class TradeGridsMaster
    {
        #region Service

        public TradeGridsMaster(StartProgram startProgram, string botName, BotTabSimple tab)
        {
            _startProgram = startProgram;
            _nameBot = botName;
            _tab = tab;
            LoadGrids();
        }

        private StartProgram _startProgram;

        private string _nameBot;

        private BotTabSimple _tab;

        public void Clear()
        {
            if(TradeGrids != null 
                && TradeGrids.Count > 0)
            {
                TradeGrid[] grids = TradeGrids.ToArray();

                for (int i = 0;i < grids.Length; i++)
                {
                    DeleteAtNum(grids[i].Number, true);
                }

                TradeGrids.Clear();
                PaintGridView(); 
            }
          
            SaveGrids();
        }

        public void Delete()
        {
            _tab = null;

            if (_startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            try
            {
                if (File.Exists(@"Engine\" + _nameBot + @"GridsSettings.txt"))
                {
                    File.Delete(@"Engine\" + _nameBot + @"GridsSettings.txt");
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        #endregion

        #region TradeGrid management

        public List<TradeGrid> TradeGrids = new List<TradeGrid>();

        public TradeGrid CreateNewTradeGrid()
        {
            TradeGrid newGrid = new TradeGrid(_startProgram, _tab);
            newGrid.NeedToSaveEvent += NewGrid_NeedToSaveEvent;
            newGrid.LogMessageEvent += SendNewLogMessage;
            newGrid.RePaintSettingsEvent += NewGrid_UpdateTableEvent;

            int gridNum = 1;

            for(int i = 0;i < TradeGrids.Count;i++)
            {
                if(TradeGrids[i].Number >= gridNum)
                {
                    gridNum = TradeGrids[i].Number + 1;
                }
            }

            newGrid.Number = gridNum;

            TradeGrids.Add(newGrid);

            SaveGrids();
            PaintGridView();

            return newGrid;
        }

        private void NewGrid_NeedToSaveEvent()
        {
            SaveGrids();
        }

        public void DeleteAtNum(int num, bool isAuto = true)
        {
            if(isAuto == false)
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label443);

                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }
            }

            for(int i = 0;i < TradeGrids.Count;i++)
            {
                if (TradeGrids[i].Number == num)
                {
                    for (int j = 0; j < _tradeGridUis.Count; j++)
                    {
                        TradeGridUi uiGrid = _tradeGridUis[j];

                        if (uiGrid.Number == num)
                        {
                            if (uiGrid.Dispatcher.CheckAccess() == false)
                            {
                                uiGrid.Dispatcher.Invoke(new Action<int,bool>(DeleteAtNum), num,isAuto);
                                return;
                            }
                            uiGrid.Close();
                        }
                    }

                    TradeGrids[i].NeedToSaveEvent -= NewGrid_NeedToSaveEvent;
                    TradeGrids[i].LogMessageEvent -= SendNewLogMessage;
                    TradeGrids[i].RePaintSettingsEvent -= NewGrid_UpdateTableEvent;
                    TradeGrids[i].DeleteGrid();
                    TradeGrids[i].Delete();
                    TradeGrids[i].Regime = TradeGridRegime.Off;
                    TradeGrids.RemoveAt(i);
                    
                    break;
                }
            }

            SaveGrids();
            PaintGridView();
        }

        private List<TradeGridUi> _tradeGridUis = new List<TradeGridUi>();

        public void ShowDialog(int num)
        {
            TradeGrid myGrid = null;

            for (int i = 0; i < TradeGrids.Count; i++)
            {
                if (TradeGrids[i].Number == num)
                {
                    myGrid = TradeGrids[i];
                    break;
                }
            }

            if(myGrid == null)
            {
                return;
            }

            for(int i = 0; i < _tradeGridUis.Count; i++)
            {
                TradeGridUi ui = _tradeGridUis[i];

                if(ui.Number == myGrid.Number)
                {
                    if (ui.WindowState == System.Windows.WindowState.Minimized)
                    {
                        ui.WindowState = System.Windows.WindowState.Normal;
                    }

                    ui.Activate();
                    return;
                }
            }

            TradeGridUi newUi = new TradeGridUi(myGrid);
            _tradeGridUis.Add(newUi);
            newUi.Closed += Ui_Closed;
            newUi.Show();

        }

        private void Ui_Closed(object sender, EventArgs e)
        {
            try
            {
                TradeGridUi senderUi = (TradeGridUi)sender;
                senderUi.Closed -= Ui_Closed;

                for (int i = 0; i < _tradeGridUis.Count; i++)
                {
                    TradeGridUi ui = _tradeGridUis[i];

                    if(ui.TradeGrid == null)
                    {
                        _tradeGridUis.RemoveAt(i);
                        i++;
                        continue;
                    }

                    if (ui.Number == senderUi.Number)
                    {
                        _tradeGridUis.RemoveAt(i);
                        return;
                    }
                }
            }
            catch(Exception ex)
            {
                SendNewLogMessage(ex.ToString(),LogMessageType.Error);
            }
        }

        private void SaveGrids()
        {
            if(_startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + _nameBot + @"GridsSettings.txt", false))
                {
                    for(int i =0;i < TradeGrids.Count;i++)
                    {
                        writer.WriteLine(TradeGrids[i].GetSaveString());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void LoadGrids()
        {
            if (_startProgram == StartProgram.IsOsOptimizer
                || _startProgram == StartProgram.IsTester)
            {
                return;
            }

            if (!File.Exists(@"Engine\" + _nameBot + @"GridsSettings.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + _nameBot + @"GridsSettings.txt"))
                {
                    while(reader.EndOfStream == false)
                    {
                        string settings = reader.ReadLine();

                        if(string.IsNullOrEmpty(settings) == true)
                        {
                            continue;
                        }

                        TradeGrid newGrid = new TradeGrid(_startProgram, _tab);

                        newGrid.NeedToSaveEvent += NewGrid_NeedToSaveEvent;
                        newGrid.LogMessageEvent += SendNewLogMessage;
                        newGrid.RePaintSettingsEvent += NewGrid_UpdateTableEvent;

                        newGrid.LoadFromString(settings);
                        TradeGrids.Add(newGrid);
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        #endregion

        #region DataGridView paint in menu

        private WindowsFormsHost _hostGrid;

        private DataGridView _gridViewInstances;

        public void StartPaint(WindowsFormsHost hostGrid)
        {
            if(_gridViewInstances == null)
            {
                CreateGridView();
            }

            _hostGrid = hostGrid;

            _hostGrid.Child = _gridViewInstances;

            PaintGridView();

        }

        public void StopPaint()
        {
            if(_hostGrid != null)
            {
                _hostGrid.Child = null;
                _hostGrid = null;
            }

        }

        private void CreateGridView()
        {
            _gridViewInstances = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);
            _gridViewInstances.ScrollBars = ScrollBars.Vertical;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridViewInstances.DefaultCellStyle;

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = "#";
            column1.ReadOnly = true;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            column1.MinimumWidth = 30;

            _gridViewInstances.Columns.Add(column1);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = OsLocalization.Trader.Label467;
            column2.ReadOnly = true;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _gridViewInstances.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = OsLocalization.Trader.Label468;
            column3.ReadOnly = true;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridViewInstances.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            //column4.HeaderText = "Settings";
            column4.ReadOnly = true;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _gridViewInstances.Columns.Add(column4);

            DataGridViewColumn column5 = new DataGridViewColumn();
            column5.CellTemplate = cell0;
            //column4.HeaderText = "Delete";
            column5.ReadOnly = true;
            column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _gridViewInstances.Columns.Add(column5);
            _gridViewInstances.CellClick += _gridViewInstances_CellClick;
        }

        private void PaintGridView()
        {
            try
            {
                if(_gridViewInstances == null)
                {
                    return;
                }

                if (_gridViewInstances.InvokeRequired)
                {
                    _gridViewInstances.Invoke(new Action(PaintGridView));
                    return;
                }

                _gridViewInstances.Rows.Clear();

                for(int i = 0;i < TradeGrids.Count;i++)
                {
                    DataGridViewRow row = GetGridRow(TradeGrids[i]);
                    _gridViewInstances.Rows.Add(row);
                }

                _gridViewInstances.Rows.Add(GetLastRow());

            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private DataGridViewRow GetGridRow(TradeGrid tradeGrid)
        {
            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[row.Cells.Count - 1].Value = tradeGrid.Number;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[row.Cells.Count - 1].Value = tradeGrid.GridType.ToString();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[row.Cells.Count - 1].Value = tradeGrid.Regime.ToString();

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[row.Cells.Count - 1].Value = OsLocalization.Trader.Label469;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[row.Cells.Count - 1].Value = OsLocalization.Trader.Label470;

            return row;
        }

        private DataGridViewRow GetLastRow()
        {
            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[row.Cells.Count - 1].Value = OsLocalization.Trader.Label471;

            return row;
        }

        private void _gridViewInstances_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int row = e.RowIndex;
                int column = e.ColumnIndex;

                if (row == _gridViewInstances.Rows.Count - 1
                    && column == 4)
                { // Add new
                    CreateNewTradeGrid();
                    PaintGridView();
                    return;
                }

                if (row < 0)
                {
                    return;
                }

                if(row < _gridViewInstances.Rows.Count-1
                    && column == 4)
                { // Delete

                    int number = Convert.ToInt32(_gridViewInstances.Rows[row].Cells[0].Value.ToString());

                    DeleteAtNum(number, false);
                }

                if (row < _gridViewInstances.Rows.Count - 1
                    && column == 3)
                { // Settings

                    int number = Convert.ToInt32(_gridViewInstances.Rows[row].Cells[0].Value.ToString());

                    ShowDialog(number);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void NewGrid_UpdateTableEvent()
        {
            PaintGridView();
        }

        #endregion

        #region Log

        public void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                ServerMaster.SendNewLogMessage(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}
