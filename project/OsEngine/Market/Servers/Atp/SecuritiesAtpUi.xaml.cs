/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Language;
using OsEngine.Entity;

namespace OsEngine.Market.Servers.Atp
{
    public partial class SecuritiesAtpUi : Window
    {
        private IServer _server;

        public SecuritiesAtpUi(AServer server)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            CreateTable();

            List<Security> secOnServer = server.Securities;

            _allSecurities = new List<Security>();

            for (int i = 0; i < secOnServer.Count; i++)
            {
                _allSecurities.Add(secOnServer[i]);
            }

            PaintSecurities(server.Securities);

            _server = server;
            _server.SecuritiesChangeEvent += _server_SecuritiesChangeEvent;

            Title = OsLocalization.Entity.TitleSecuritiesUi;

            this.Activate();
            this.Focus();

            Closed += SecuritiesUi_Closed;
            SaveButton.Click += SaveButton_Click;
        }

        private List<Security> _allSecurities;

        private void SecuritiesUi_Closed(object sender, EventArgs e)
        {
            _server.SecuritiesChangeEvent -= _server_SecuritiesChangeEvent;
            _server = null;
            Closed -= SecuritiesUi_Closed;

            _grid.CellClick -= _grid_CellClick;
            _grid.DataError -= _grid_DataError;
            DataGridFactory.ClearLinks(_grid);
            _grid = null;
            HostSecurities.Child = null;

            SaveButton.Click -= SaveButton_Click;
        }

        private DataGridView _grid;

        private void CreateTable()
        {
            _grid = GetDataGridSecurities();

            HostSecurities.Child = _grid;
            HostSecurities.Child.Show();
            HostSecurities.Child.Refresh();
            _grid.CellClick += _grid_CellClick;
            _grid.DataError += _grid_DataError;
        }

        private void _grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), Logging.LogMessageType.Error);
        }

        public static DataGridView GetDataGridSecurities()
        {
            DataGridView grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = grid.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Entity.SecuritiesColumn1; // Name
            column0.ReadOnly = false;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            grid.Columns.Add(column0);

            DataGridViewColumn column01 = new DataGridViewColumn();
            column01.CellTemplate = cell0;
            column01.HeaderText = OsLocalization.Entity.SecuritiesColumn9; // Class
            column01.ReadOnly = false;
            column01.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            grid.Columns.Add(column01);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = OsLocalization.Entity.SecuritiesColumn2; // Type
            column.ReadOnly = false;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            grid.Columns.Add(column);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Entity.SecuritiesColumn3; // Lot
            column1.ReadOnly = false;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            grid.Columns.Add(column1);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = OsLocalization.Entity.SecuritiesColumn4; // Price step
            column3.ReadOnly = false;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            grid.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = OsLocalization.Entity.SecuritiesColumn5; // Price step cost
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(column4);

            DataGridViewColumn column5 = new DataGridViewColumn();
            column5.CellTemplate = cell0;
            column5.HeaderText = OsLocalization.Entity.SecuritiesColumn6; // Lot Price
            column5.ReadOnly = false;
            column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(column5);

            DataGridViewColumn column6 = new DataGridViewColumn();
            column6.CellTemplate = cell0;
            column6.HeaderText = OsLocalization.Entity.SecuritiesColumn7; // Volume decimals
            column6.ReadOnly = false;
            column6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(column6);

            DataGridViewColumn column7 = new DataGridViewColumn();
            column7.CellTemplate = cell0;
            column7.HeaderText = OsLocalization.Entity.SecuritiesColumn8; // Price decimals
            column7.ReadOnly = false;
            column7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(column7);

            DataGridViewColumn column8 = new DataGridViewColumn();
            column8.CellTemplate = cell0;
            column8.HeaderText = ""; // Add or Delete button
            column8.ReadOnly = false;
            column8.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(column8);

            return grid;
        }

        private void _grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int column = e.ColumnIndex;
            int row = e.RowIndex;

            if (row == 0 &&
                column == 9)
            {
                _allSecurities.Insert(0, new Security());
                PaintSecurities(_allSecurities);
            }

            if (column == 9)
            { // delete 

                if (row <= 0)
                {
                    return;
                }

                row = row - 1;

                if (row >= _allSecurities.Count)
                {
                    return;
                }

                _allSecurities.RemoveAt(row);
                PaintSecurities(_allSecurities);
            }
        }

        private void _server_SecuritiesChangeEvent(List<Security> securities)
        {
            for (int i = 0; i < securities.Count; i++)
            {
                Security curSecurity = securities[i];

                bool isInArray = false;

                for (int j = 0; j < _allSecurities.Count; j++)
                {
                    if (_allSecurities[j].Name == curSecurity.Name
                        && _allSecurities[j].NameClass == curSecurity.NameClass)
                    {
                        isInArray = true;
                        break;
                    }
                }

                if (isInArray)
                {
                    continue;
                }

                _allSecurities.Add(curSecurity);
            }

            PaintSecurities(_allSecurities);
        }

        private void PaintSecurities(List<Security> securities)
        {
            if (_grid.InvokeRequired)
            {
                _grid.Invoke(new Action<List<Security>>(PaintSecurities), securities);
                return;
            }

            _grid.Rows.Clear();

            _grid.Rows.Add(GetFirstRow());

            if (securities == null)
            {
                return;
            }

            for (int i = 0; i < securities.Count; i++)
            {
                DataGridViewRow nRow = GetSecurityRow(securities[i]);

                _grid.Rows.Add(nRow);
            }
        }

        private DataGridViewRow GetSecurityRow(Security security)
        {
            // 0 name
            // 1 class
            // 2 type
            // 3 lot
            // 4 price step
            // 5 price step cost
            // 6 lot price
            // 7 volume decimals
            // 8 price decimals
            // 9 add or delete button

            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].ReadOnly = false;
            nRow.Cells[0].Value = security.Name;  // 0 name

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].ReadOnly = false;
            nRow.Cells[1].Value = security.NameClass; // 1 class

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[2].ReadOnly = false;
            nRow.Cells[2].Value = security.SecurityType; // 2 type

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[3].ReadOnly = false;
            nRow.Cells[3].Value = security.Lot;         // 3 lot

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[4].ReadOnly = false;
            nRow.Cells[4].Value = security.PriceStep; // 4 price step

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[5].ReadOnly = false;
            nRow.Cells[5].Value = security.PriceStepCost; // 5 price step cost

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[6].ReadOnly = false;
            nRow.Cells[6].Value = security.MarginBuy;// 6 lot price

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[7].ReadOnly = false;
            nRow.Cells[7].Value = security.DecimalsVolume; // 7 volume decimals

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[8].ReadOnly = false;
            nRow.Cells[8].Value = security.Decimals; // 8 price decimals

            DataGridViewButtonCell button = new DataGridViewButtonCell();
            button.Value = OsLocalization.Market.Label47;// 9 add or delete button
            nRow.Cells.Add(button);

            return nRow;
        }

        private DataGridViewRow GetFirstRow()
        {
            // 0 name
            // 1 class
            // 2 type
            // 3 lot
            // 4 price step
            // 5 price step cost
            // 6 lot price
            // 7 volume decimals
            // 8 price decimals
            // 9 add or delete button

            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].ReadOnly = true;
            nRow.Cells[0].Value = "";

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].ReadOnly = true;
            nRow.Cells[1].Value = "";

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[2].ReadOnly = true;
            nRow.Cells[2].Value = "";

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[3].ReadOnly = true;
            nRow.Cells[3].Value = "";

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[4].ReadOnly = true;
            nRow.Cells[4].Value = "";

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[5].ReadOnly = true;
            nRow.Cells[5].Value = "";

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[6].ReadOnly = true;
            nRow.Cells[6].Value = "";

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[7].ReadOnly = true;
            nRow.Cells[7].Value = "";

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[8].ReadOnly = true;
            nRow.Cells[8].Value = "";

            DataGridViewButtonCell button = new DataGridViewButtonCell();
            button.Value = OsLocalization.Market.Label159;
            nRow.Cells.Add(button);

            return nRow;
        }

        private void SaveButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            List<Security> secOnServer = _server.Securities;

            secOnServer.Clear();

            for (int i = 1; i < _grid.Rows.Count; i++)
            {
                Security curSec = GetSecFromRow(_grid.Rows[i]);

                if (string.IsNullOrEmpty(curSec.Name))
                {
                    continue;
                }

                bool isInArray = false;

                for (int j = 0; j < secOnServer.Count; j++)
                {
                    if (secOnServer[j].Name == curSec.Name)
                    {
                        isInArray = true;
                        break;
                    }
                }

                if (isInArray)
                {
                    continue;
                }

                secOnServer.Add(curSec);
            }

            Close();
        }

        private Security GetSecFromRow(DataGridViewRow row)
        {
            Security newSec = new Security();

            // 0 name
            // 1 class
            // 2 type
            // 3 lot
            // 4 price step
            // 5 price step cost
            // 6 lot price
            // 7 volume decimals
            // 8 price decimals
            // 9 add or delete button

            try
            {
                if (row.Cells[0].Value != null)
                {
                    newSec.Name = row.Cells[0].Value.ToString();
                    newSec.NameFull = row.Cells[0].Value.ToString();
                    newSec.NameId = row.Cells[0].Value.ToString();
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                if (row.Cells[1].Value != null)
                {
                    newSec.NameClass = row.Cells[1].Value.ToString();
                }

            }
            catch
            {
                // ignore
            }

            try
            {
                Enum.TryParse(row.Cells[2].Value.ToString(), out newSec.SecurityType);
            }
            catch
            {
                // ignore
            }

            try
            {
                newSec.Lot = row.Cells[3].Value.ToString().ToDecimal();
            }
            catch
            {
                // ignore
            }

            try
            {
                newSec.PriceStep = row.Cells[4].Value.ToString().ToDecimal();
            }
            catch
            {
                // ignore
            }

            try
            {
                newSec.PriceStepCost = row.Cells[5].Value.ToString().ToDecimal();
            }
            catch
            {
                // ignore
            }

            try
            {
                newSec.MarginBuy = row.Cells[6].Value.ToString().ToDecimal();
            }
            catch
            {
                // ignore
            }

            try
            {
                newSec.DecimalsVolume = Convert.ToInt32(row.Cells[7].Value.ToString());
            }
            catch
            {
                // ignore
            }

            try
            {
                newSec.Decimals = Convert.ToInt32(row.Cells[8].Value.ToString());
            }
            catch
            {
                // ignore
            }

            return newSec;
        }
    }
}