/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;


namespace OsEngine.Market.Proxy
{
    public partial class ProxyMasterUi : Window
    {
        public ProxyMasterUi(ProxyMaster master)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _master = master;

            CreateGrid();
            UpdateGrid();

            ComboBoxAutoPingInterval.Items.Add("1");
            ComboBoxAutoPingInterval.Items.Add("5");
            ComboBoxAutoPingInterval.Items.Add("10");
            ComboBoxAutoPingInterval.Items.Add("30");
            ComboBoxAutoPingInterval.SelectedItem = _master.AutoPingMinutes.ToString();
            ComboBoxAutoPingInterval.SelectionChanged += ComboBoxAutoPingInterval_SelectionChanged;

            CheckBoxAutoPingIsOn.IsChecked = _master.AutoPingIsOn;
            CheckBoxAutoPingIsOn.Click += CheckBoxAutoPingIsOn_Click;

            TextBoxAutoPingLastTime.Text = _master.AutoPingLastTime.ToString();

            this.Closed += ProxyMasterUi_Closed;

            _master.ProxyPingEndEvent += _master_ProxyPingEndEvent;
            _master.ProxyCheckLocationEndEvent += _master_ProxyCheckLocationEndEvent;
        }

        private void _master_ProxyCheckLocationEndEvent()
        {
            UpdateGrid();
        }

        private void _master_ProxyPingEndEvent()
        {
            UpdateGrid();
        }

        private void ProxyMasterUi_Closed(object sender, EventArgs e)
        {
            _master.ProxyPingEndEvent -= _master_ProxyPingEndEvent;
            _master.ProxyCheckLocationEndEvent -= _master_ProxyCheckLocationEndEvent;
            _master = null;

            _grid.Rows.Clear();
            DataGridFactory.ClearLinks(_grid);
            HostProxy.Child = null;
        }

        private ProxyMaster _master;

        private void ButtonCheckPing_Click(object sender, RoutedEventArgs e)
        {
            _master.CheckPing();
        }

        private void ButtonCheckLocation_Click(object sender, RoutedEventArgs e)
        {
            _master.CheckLocation();
        }

        #region Settings

        private void ComboBoxAutoPingInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                _master.AutoPingMinutes = Convert.ToInt32(ComboBoxAutoPingInterval.SelectedItem.ToString());
                _master.SaveSettings();
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(),Logging.LogMessageType.Error);
            }
        }

        private void CheckBoxAutoPingIsOn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _master.AutoPingIsOn = CheckBoxAutoPingIsOn.IsChecked.Value;
                _master.SaveSettings();
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

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

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = "Prefix"; // Prefix
            column1.ReadOnly = false;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column1);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = "Is on"; // IsOn
            column2.ReadOnly = false;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = "Ip"; // Ip
            column3.ReadOnly = false;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = "Port"; // Port
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _grid.Columns.Add(column4);

            DataGridViewColumn column5 = new DataGridViewColumn();
            column5.CellTemplate = cell0;
            column5.HeaderText = "Login"; // UserName
            column5.ReadOnly = false;
            column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column5);

            DataGridViewColumn column6 = new DataGridViewColumn();
            column6.CellTemplate = cell0;
            column6.HeaderText = "User password"; // UserPassword
            column6.ReadOnly = false;
            column6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column6);

            DataGridViewColumn column7 = new DataGridViewColumn();
            column7.CellTemplate = cell0;
            column7.HeaderText = "Location"; // Location
            column7.ReadOnly = false;
            column7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column7);

            DataGridViewColumn column8 = new DataGridViewColumn();
            column8.CellTemplate = cell0;
            column8.HeaderText = "Max connectors"; // Max connectors
            column8.ReadOnly = false;
            column8.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column8);

            DataGridViewColumn column9 = new DataGridViewColumn();
            column9.CellTemplate = cell0;
            column9.HeaderText = "Ping status"; // Status
            column9.ReadOnly = false;
            column9.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _grid.Columns.Add(column9);

            DataGridViewColumn column10 = new DataGridViewColumn();
            column10.CellTemplate = cell0;
            column10.HeaderText = "Ping address"; // Ping address
            column10.ReadOnly = false;
            column10.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column10);

            DataGridViewColumn column11_0 = new DataGridViewColumn();
            column11_0.CellTemplate = cell0;
            column11_0.HeaderText = "Allow connection"; // AllowConnection
            column11_0.ReadOnly = true;
            column11_0.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _grid.Columns.Add(column11_0);

            DataGridViewColumn column11 = new DataGridViewColumn();
            column11.CellTemplate = cell0;
            column11.HeaderText = "Used Connection"; // UseConnection
            column11.ReadOnly = true;
            column11.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _grid.Columns.Add(column11);

            DataGridViewColumn column12 = new DataGridViewColumn();
            column12.CellTemplate = cell0;// Add new
            column12.ReadOnly = true;
            column12.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _grid.Columns.Add(column12);

            HostProxy.Child = _grid;
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

                TextBoxAutoPingLastTime.Text = _master.AutoPingLastTime.ToString();

                // 0 num
                // 1 Prefix
                // 2 IsOn
                // 3 Ip
                // 4 Port
                // 5 UserName
                // 6 UserPassword
                // 7 Location
                // 8 Max connectors
                // 9 Status
                // 10 Ping address
                // 11 AllowConnection
                // 12 UseConnection

                _grid.Rows.Clear();

                for (int i = 0; i < _master.Proxies.Count; i++)
                {
                    _grid.Rows.Add(GetProxyRow(_master.Proxies[i]));
                }

                _grid.Rows.Add(GetLastRow());
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private DataGridViewRow GetProxyRow(ProxyOsa proxy)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = proxy.Number;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = proxy.Prefix;

            DataGridViewComboBoxCell cellIsOn = new DataGridViewComboBoxCell();
            cellIsOn.Items.Add("True");
            cellIsOn.Items.Add("False");
            cellIsOn.Value = proxy.IsOn.ToString();
            nRow.Cells.Add(cellIsOn);

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = proxy.Ip;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = proxy.Port;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = proxy.Login;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = proxy.UserPassword;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = proxy.Location;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = proxy.MaxConnectorsOnThisProxy;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = proxy.AutoPingLastStatus;
            if(proxy.AutoPingLastStatus == "Connect")
            {
                nRow.Cells[nRow.Cells.Count - 1].Style.ForeColor = System.Drawing.Color.Green;
            }

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = proxy.PingWebAddress;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = proxy.AllowConnectionCount;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = proxy.UseConnectionCount;

            DataGridViewButtonCell cell = new DataGridViewButtonCell();
            cell.Value = "Delete";
            nRow.Cells.Add(cell);

            return nRow;
        }

        private DataGridViewRow GetLastRow()
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());

            DataGridViewButtonCell cell = new DataGridViewButtonCell();
            cell.Value = "Add new";
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
                    && column == 13)
                { // add new
                    _master.CreateNewProxy();
                    UpdateGrid();
                }
                else if (column == 13)
                { // delete

                    AcceptDialogUi ui = new AcceptDialogUi("The proxy will be deleted and data will be lost. Are you sure?");

                    ui.ShowDialog();

                    if (ui.UserAcceptAction == false)
                    {
                        return;
                    }

                    int number = Convert.ToInt32(_grid.Rows[row].Cells[0].Value.ToString());

                    _master.RemoveProxy(number);
                    UpdateGrid();
                }
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(),Logging.LogMessageType.Error);
            }
        }

        private void _grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                for(int i = 0;i < _grid.Rows.Count-1 && i < _master.Proxies.Count; i++)
                {
                    SaveProxy(_master.Proxies[i], _grid.Rows[i]);
                }
                _master.SaveProxy();
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void SaveProxy(ProxyOsa proxy, DataGridViewRow nRow)
        {
            try
            {
                if(nRow.Cells[1].Value != null)
                {
                    proxy.Prefix = nRow.Cells[1].Value.ToString();
                }
                else
                {
                    proxy.Prefix = "";
                }

                if (nRow.Cells[2].Value != null)
                {
                    proxy.IsOn = Convert.ToBoolean(nRow.Cells[2].Value.ToString());
                }
                else
                {
                    proxy.IsOn = false;
                }

                if (nRow.Cells[3].Value != null)
                {
                    proxy.Ip = nRow.Cells[3].Value.ToString();
                }
                else
                {
                    proxy.Ip = "";
                }

                if (nRow.Cells[4].Value != null)
                {
                    proxy.Port = Convert.ToInt32(nRow.Cells[4].Value.ToString());
                }
                else
                {
                    proxy.Port = 0;
                }

                if (nRow.Cells[5].Value != null)
                {
                    proxy.Login = nRow.Cells[5].Value.ToString();
                }
                else
                {
                    proxy.Login = "";
                }

                if (nRow.Cells[6].Value != null)
                {
                    proxy.UserPassword = nRow.Cells[6].Value.ToString();
                }
                else
                {
                    proxy.UserPassword = "";
                }

                if (nRow.Cells[7].Value != null)
                {
                    proxy.Location = nRow.Cells[7].Value.ToString();
                }
                else
                {
                    proxy.Location = "";
                }

                if(nRow.Cells[8].Value != null)
                {
                    proxy.MaxConnectorsOnThisProxy = Convert.ToInt32(nRow.Cells[8].Value);
                }
                else
                {
                    proxy.MaxConnectorsOnThisProxy = 5;
                }
            }
            catch (Exception ex)
            {
                _master.SendLogMessage("Save proxy error. Proxy: " + proxy.UniqueName + "\nError: " + ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            _master.SendLogMessage(e.Exception.ToString(), Logging.LogMessageType.Error);
        }

        #endregion

        #region Save / Load settings in file




        #endregion

    }
}