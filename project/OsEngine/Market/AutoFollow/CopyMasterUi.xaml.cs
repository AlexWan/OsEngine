/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
using System;
using System.Windows;
using System.Windows.Forms;
using System.Collections.Generic;

namespace OsEngine.Market.AutoFollow
{
    /// <summary>
    /// Interaction logic for CopyMasterUi.xaml
    /// </summary>
    public partial class CopyMasterUi : Window
    {
        public CopyMasterUi(CopyMaster master)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);

            _master = master;
            _master.LogCopyMaster.StartPaint(HostLog);

            CreateGrid();
            UpdateGrid();

            Title = OsLocalization.Market.Label197;
            LabelCopyTraders.Content = OsLocalization.Market.Label198;
            LabelLog.Content = OsLocalization.Market.Label199;

            this.Closed += CopyMasterUi_Closed;

            GlobalGUILayout.Listen(this, "copyMasterUi");
        }

        private CopyMaster _master;

        private void CopyMasterUi_Closed(object sender, EventArgs e)
        {
            try
            {
                for (int i = 0; i < _uis.Count; i++)
                {
                    _uis[i].LogMessageEvent -= _master.SendLogMessage;
                    _uis[i].NeedToUpdateCopyTradersGridEvent -= Ui_NeedToUpdateCopyTradersGridEvent;
                }
                _uis.Clear();

                _master.LogCopyMaster.StopPaint();
                _master = null;

                _grid.CellClick -= _grid_CellClick;
                _grid.CellValueChanged -= _grid_CellValueChanged;
                _grid.DataError -= _grid_DataError;
                _grid.Rows.Clear();
                DataGridFactory.ClearLinks(_grid);
                HostCopyTraders.Child = null;
            }
            catch(Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(),Logging.LogMessageType.Error);
            }
        }

        #region Grid

        private DataGridView _grid;

        private void CreateGrid()
        {
            _grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect,
   DataGridViewAutoSizeRowsMode.AllCells);
            _grid.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _grid.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = "#"; // num
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _grid.Columns.Add(column0);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = OsLocalization.Market.Label164; // Name
            column2.ReadOnly = false;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column2);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = OsLocalization.Market.Label182; // Is On
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column4.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.Columns.Add(column4);

            DataGridViewColumn column6 = new DataGridViewColumn();
            column6.CellTemplate = cell0;
            //column6.HeaderText = "Settings"; // Settings
            column6.ReadOnly = true;
            column6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column6.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.Columns.Add(column6);

            DataGridViewColumn column7 = new DataGridViewColumn();
            column7.CellTemplate = cell0;
            //column7.HeaderText = "Remove"; // Remove or Add
            column7.ReadOnly = true;
            column7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column7.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.Columns.Add(column7);

            HostCopyTraders.Child = _grid;
            _grid.CellClick += _grid_CellClick;
            _grid.CellValueChanged += _grid_CellValueChanged;
            _grid.DataError += _grid_DataError;
        }

        private void UpdateGrid()
        {
            try
            {
                if (_grid.InvokeRequired)
                {
                    _grid.Invoke(new Action(UpdateGrid));
                    return;
                }

                // 0 num
                // 1 Name
                // 2 Type
                // 3 Is On
                // 4 Settings
                // 5 Remove

                _grid.Rows.Clear();

                 for (int i = 0; i < _master.CopyTraders.Count; i++)
                 {
                     _grid.Rows.Add(GetTraderRow(_master.CopyTraders[i]));
                 }

                 _grid.Rows.Add(GetLastRow());
            }
            catch (Exception ex)
            {
                _master?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private DataGridViewRow GetTraderRow(CopyTrader trader)
        {
            // 0 num
            // 1 Name
            // 2 Type
            // 3 Is On
            // 4 Settings
            // 5 Remove / Add New

            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = trader.Number;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = trader.Name;

            DataGridViewComboBoxCell cellIsOn = new DataGridViewComboBoxCell();
            cellIsOn.Items.Add("True");
            cellIsOn.Items.Add("False");
            cellIsOn.Value = trader.IsOn.ToString();
            cellIsOn.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(cellIsOn);
            if (trader.IsOn == true)
            {
                nRow.Cells[nRow.Cells.Count - 1].Style.ForeColor = System.Drawing.Color.Green;
            }
            else
            {
                nRow.Cells[nRow.Cells.Count - 1].Style.ForeColor = System.Drawing.Color.Red;
            }

            DataGridViewButtonCell cell1 = new DataGridViewButtonCell();
            cell1.Value = OsLocalization.Market.TabItem3;
            cell1.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(cell1);

            DataGridViewButtonCell cell = new DataGridViewButtonCell();
            cell.Value = OsLocalization.Market.Label47;
            cell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            nRow.Cells.Add(cell);

            return nRow;
        }

        private DataGridViewRow GetLastRow()
        {
            DataGridViewRow nRow = new DataGridViewRow();

            // 0 num
            // 1 Name
            // 2 Type
            // 3 Is On
            // 4 Settings
            // 5 Remove / Add New

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
           
            DataGridViewButtonCell cell = new DataGridViewButtonCell();
            cell.Value = OsLocalization.Market.Label48;
            cell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(cell);

            return nRow;
        }

        private void _grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                 int row = e.RowIndex;
                 int column = e.ColumnIndex;

                 if (row > _grid.Rows.Count
                     || row < 0)
                 {
                     return;
                 }

                 if (row + 1 == _grid.Rows.Count
                     && column == 4)
                 { // add new
                    _master.CreateNewCopyTrader();
                     UpdateGrid();
                 }
                 else if (column == 4)
                 { // delete

                     AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Market.Label196);

                     ui.ShowDialog();

                     if (ui.UserAcceptAction == false)
                     {
                         return;
                     }

                     int number = Convert.ToInt32(_grid.Rows[row].Cells[0].Value.ToString());
                     _master.RemoveCopyTraderAt(number);
                     UpdateGrid();
                 }
                else if (row + 1 < _grid.Rows.Count
                    && column == 3)
                { // show dialog

                    int number = Convert.ToInt32(_grid.Rows[row].Cells[0].Value.ToString());
                    ShowCopyTraderDialog(number);
                    UpdateGrid();
                }
            }
            catch (Exception ex)
            {
                _master?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                for (int i = 0; i < _grid.Rows.Count - 1 && i < _master.CopyTraders.Count; i++)
                {
                    SaveCopyTrader(_master.CopyTraders[i], _grid.Rows[i]);
                }
                _master.SaveCopyTraders();
            }
            catch (Exception ex)
            {
                _master?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void SaveCopyTrader(CopyTrader trader, DataGridViewRow nRow)
        {
            // 0 num
            // 1 Name
            // 2 Type
            // 3 Is On
            // 4 State
            // 5 Settings
            // 6 Remove / Add New

            try
            {
                if (nRow.Cells[1].Value != null)
                {
                    trader.Name = nRow.Cells[1].Value.ToString().RemoveExcessFromSecurityName();
                }

                if (nRow.Cells[2].Value != null)
                {
                    trader.IsOn = Convert.ToBoolean(nRow.Cells[2].Value.ToString());
                }
                else
                {
                    trader.IsOn = false;
                }

                if(trader.IsOn == true)
                {
                    nRow.Cells[2].Style.ForeColor = System.Drawing.Color.Green;
                }
                else
                {
                    nRow.Cells[2].Style.ForeColor = nRow.Cells[0].Style.ForeColor;
                }
            }
            catch (Exception ex)
            {
                _master?.SendLogMessage("Save copy trader error. Proxy number: " + trader.Number + "\nError: " + ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            _master?.SendLogMessage(e.Exception.ToString(), Logging.LogMessageType.Error);
        }

        #endregion

        #region Copy traders UI

        private void ShowCopyTraderDialog(int number)
        {
            CopyTrader trader = null;

            for(int i = 0;i < _master.CopyTraders.Count;i++)
            {
                if(_master.CopyTraders [i].Number == number)
                {
                    trader = _master.CopyTraders [i];
                    break;
                }
            }

            if(trader == null)
            {
                return;
            }

            // 1 ищем UI в массиве

            CopyTraderUi ui = null;

            for(int i = 0;i < _uis.Count;i++)
            {
                if (_uis[i].CopyTraderInstance.Number == trader.Number)
                {
                    ui = _uis[i];
                    break;
                }
            }

            // 2 создаём или активируем

            if(ui == null)
            {
                ui = new CopyTraderUi(trader);
                ui.LogMessageEvent += _master.SendLogMessage;
                ui.NeedToUpdateCopyTradersGridEvent += Ui_NeedToUpdateCopyTradersGridEvent;
                ui.Show();
                ui.Closed += Ui_Closed;
                _uis.Add(ui);
            }
            else
            {
                if(ui.WindowState == WindowState.Minimized)
                {
                    ui.WindowState = WindowState.Normal;
                }
                ui.Activate();
            }
        }

        private void Ui_NeedToUpdateCopyTradersGridEvent()
        {
            UpdateGrid();
        }

        private void Ui_Closed(object sender, EventArgs e)
        {
            try
            {
                CopyTraderUi ui = (CopyTraderUi)sender;

                for (int i = 0; i < _uis.Count; i++)
                {
                    if (_uis[i].TraderNumber == ui.TraderNumber)
                    {
                        _uis[i].LogMessageEvent -= _master.SendLogMessage;
                        _uis[i].NeedToUpdateCopyTradersGridEvent -= Ui_NeedToUpdateCopyTradersGridEvent;
                        _uis.RemoveAt(i);
                        break;
                    }
                }
            }
            catch(Exception ex)
            {
                _master?.SendLogMessage(ex.ToString(),Logging.LogMessageType.Error);
            }
        }

        private List<CopyTraderUi> _uis = new List<CopyTraderUi>(); 

        #endregion

    }
}
