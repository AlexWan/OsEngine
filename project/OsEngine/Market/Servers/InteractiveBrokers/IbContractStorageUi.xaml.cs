/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Language;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace OsEngine.Market.Servers.InteractiveBrokers
{
    /// <summary>
    /// Interaction logic for IbContractStorageUi.xaml
    /// Логика взаимодействия для IbContractStorageUi.xaml
    /// </summary>
    public partial class IbContractStorageUi
    {
        private DataGridView _grid;

        private InteractiveBrokersServerRealization _server;

        public IbContractStorageUi(List<SecurityIb> secToSubscribe, InteractiveBrokersServerRealization server)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            SecToSubscribe = secToSubscribe;
            _server = server;

            _grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);
            _grid.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _grid.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Market.Label42;
            column0.ReadOnly = false;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            // column0.Width = 150;

            _grid.Columns.Add(column0);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = OsLocalization.Market.Label43;
            column.ReadOnly = false;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            // column.Width = 150;
            _grid.Columns.Add(column);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column1.ReadOnly = false;
            // column1.Width = 150;
            column1.HeaderText = OsLocalization.Market.Label44;
            _grid.Columns.Add(column1);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column3.ReadOnly = false;
            // column1.Width = 150;
            column3.HeaderText = OsLocalization.Market.Label45;
            _grid.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column4.ReadOnly = false;
            column4.HeaderText = OsLocalization.Market.Label46;
            _grid.Columns.Add(column4);

            DataGridViewColumn column6 = new DataGridViewColumn();
            column6.CellTemplate = cell0;
            column6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column6.ReadOnly = false;
            column6.HeaderText = OsLocalization.Market.Label61;
            _grid.Columns.Add(column6);

            DataGridViewColumn column5 = new DataGridViewColumn();
            column5.CellTemplate = cell0;
            column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column5.ReadOnly = false;
            column5.HeaderText = OsLocalization.Market.Label60;
            _grid.Columns.Add(column5);

            _grid.Rows.Add(null, null);

            Host.Child = _grid;
            LoadSecOnTable();

            Closing += IbContractStorageUi_Closing;
            _grid.Click += _grid_Click;
            _grid.CellValueChanged += _grid_CellValueChanged;

            this.Activate();
            this.Focus();
        }

        private void IbContractStorageUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveInServer();
        }

        void _grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            SaveSecFromTable();
        }

        public List<SecurityIb> SecToSubscribe;

        private void LoadSecOnTable()
        {
            if (!Host.CheckAccess())
            {
                Host.Dispatcher.Invoke(new Action(LoadSecOnTable));
                return;
            }

            _grid.Rows.Clear();

            if (SecToSubscribe == null ||
                SecToSubscribe.Count == 0)
            {
                return;
            }

            for (int i = 0; SecToSubscribe != null && SecToSubscribe.Count != 0 && i < SecToSubscribe.Count; i++)
            {

                DataGridViewRow nRow = new DataGridViewRow();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[0].Value = SecToSubscribe[i].Symbol;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[1].Value = SecToSubscribe[i].Exchange;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[2].Value = SecToSubscribe[i].SecType;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[3].Value = SecToSubscribe[i].LocalSymbol;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[4].Value = SecToSubscribe[i].PrimaryExch;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[5].Value = SecToSubscribe[i].Currency;

                DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();
                cell.Items.Add(true.ToString());
                cell.Items.Add(false.ToString());
                cell.Value = SecToSubscribe[i].CreateMarketDepthFromTrades.ToString();

                nRow.Cells.Add(cell);

                _grid.Rows.Add(nRow);
            }
        }

        private void SaveSecFromTable()
        {
            if (SecToSubscribe == null ||
                SecToSubscribe.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                SecurityIb security = SecToSubscribe[i];

                security.Symbol = Convert.ToString(_grid.Rows[i].Cells[0].Value);
                security.Exchange = Convert.ToString(_grid.Rows[i].Cells[1].Value);
                security.SecType = Convert.ToString(_grid.Rows[i].Cells[2].Value);
                security.LocalSymbol = Convert.ToString(_grid.Rows[i].Cells[3].Value);
                security.PrimaryExch = Convert.ToString(_grid.Rows[i].Cells[4].Value);
                security.Currency = Convert.ToString(_grid.Rows[i].Cells[5].Value);
                security.CreateMarketDepthFromTrades = Convert.ToBoolean(_grid.Rows[i].Cells[6].Value);
            }
        }

        void _grid_Click(object sender, EventArgs e)
        {
            MouseEventArgs mouse = (MouseEventArgs)e;
            if (mouse.Button != MouseButtons.Right)
            {
                if(_grid.ContextMenuStrip != null)
                {
                    _grid.ContextMenuStrip = null;
                }

                return;
            }

            // creating a context menu / cоздание контекстного меню

            ToolStripMenuItem[] items = new ToolStripMenuItem[2];

            items[0] = new ToolStripMenuItem();
            items[0].Text = OsLocalization.Market.Label47;
            items[0].Click += AlertDelete_Click;

            items[1] = new ToolStripMenuItem() { Text = OsLocalization.Market.Label48 };
            items[1].Click += AlertCreate_Click;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.AddRange(items);

            _grid.ContextMenuStrip = menu;
            _grid.ContextMenuStrip.Show(_grid, new System.Drawing.Point(mouse.X, mouse.Y));
        }

        void AlertDelete_Click(object sender, EventArgs e)
        {
            if (_grid.CurrentCell == null || _grid.CurrentCell.RowIndex <= -1)
            {
                return;
            }

            SecToSubscribe.RemoveAt(_grid.CurrentCell.RowIndex);
            LoadSecOnTable();
        }

        void AlertCreate_Click(object sender, EventArgs e)
        {
            if (SecToSubscribe == null)
            {
                SecToSubscribe = new List<SecurityIb>();
            }
            SecToSubscribe.Insert(0, new SecurityIb());
            LoadSecOnTable();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SaveInServer();
        }

        private void SaveInServer()
        {
            SaveSecFromTable();
            _server.GetSecurities();
            _server.SaveIbSecurities();
        }
    }
}
