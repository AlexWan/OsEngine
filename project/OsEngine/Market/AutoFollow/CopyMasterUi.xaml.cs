/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market.Proxy;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _master = master;
            _master.LogCopyMaster.StartPaint(HostLog);

            CreateGrid();
            UpdateGrid();

            this.Closed += CopyMasterUi_Closed;
        }

        private CopyMaster _master;

        private void CopyMasterUi_Closed(object sender, EventArgs e)
        {
            _master.LogCopyMaster.StopPaint();

            _master = null;

            _grid.Rows.Clear();
            DataGridFactory.ClearLinks(_grid);
            HostCopyTraders.Child = null;
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
            column2.HeaderText = "Name"; // Name
            column2.ReadOnly = false;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = "Type"; // Type
            column3.ReadOnly = true;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = "Is On"; // Is On
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column4);

            DataGridViewColumn column5 = new DataGridViewColumn();
            column5.CellTemplate = cell0;
            column5.HeaderText = "State"; // State
            column5.ReadOnly = true;
            column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column5);

            DataGridViewColumn column6 = new DataGridViewColumn();
            column6.CellTemplate = cell0;
            //column6.HeaderText = "Settings"; // Settings
            column6.ReadOnly = true;
            column6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column6);

            DataGridViewColumn column7 = new DataGridViewColumn();
            column7.CellTemplate = cell0;
            //column7.HeaderText = "Remove"; // Remove or Add
            column7.ReadOnly = true;
            column7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
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
                // 4 State
                // 5 Settings
                // 6 Remove

                _grid.Rows.Clear();

                 for (int i = 0; i < _master.CopyTraders.Count; i++)
                 {
                     _grid.Rows.Add(GetTraderRow(_master.CopyTraders[i]));
                 }

                 _grid.Rows.Add(GetLastRow());
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private DataGridViewRow GetTraderRow(CopyTrader proxy)
        {
            // 0 num
            // 1 Name
            // 2 Type
            // 3 Is On
            // 4 State
            // 5 Settings
            // 6 Remove / Add New

            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = proxy.Number;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = proxy.Name;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = proxy.Type.ToString();

            DataGridViewComboBoxCell cellIsOn = new DataGridViewComboBoxCell();
            cellIsOn.Items.Add("True");
            cellIsOn.Items.Add("False");
            cellIsOn.Value = proxy.IsOn.ToString();
            nRow.Cells.Add(cellIsOn);
            if (proxy.IsOn == true)
            {
                nRow.Cells[nRow.Cells.Count - 1].Style.ForeColor = System.Drawing.Color.Green;
            }

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = proxy.State;

            DataGridViewButtonCell cell1 = new DataGridViewButtonCell();
            cell1.Value = OsLocalization.Market.TabItem3;
            nRow.Cells.Add(cell1);

            DataGridViewButtonCell cell = new DataGridViewButtonCell();
            cell.Value = OsLocalization.Market.Label47;

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
            // 4 State
            // 5 Settings
            // 6 Remove / Add New

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
           
            DataGridViewButtonCell cell = new DataGridViewButtonCell();
            cell.Value = OsLocalization.Market.Label48;
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
                     && column == 6)
                 { // add new
                    _master.CreateNewCopyTrader();
                     UpdateGrid();
                 }
                 else if (column == 6)
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
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
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
                _master.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
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
                    trader.Name = nRow.Cells[1].Value.ToString();
                }

                if (nRow.Cells[3].Value != null)
                {
                    trader.IsOn = Convert.ToBoolean(nRow.Cells[3].Value.ToString());
                }
                else
                {
                    trader.IsOn = false;
                }
            }
            catch (Exception ex)
            {
                _master.SendLogMessage("Save proxy error. Proxy number: " + trader.Number + "\nError: " + ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            _master.SendLogMessage(e.Exception.ToString(), Logging.LogMessageType.Error);
        }

        #endregion

    }
}
