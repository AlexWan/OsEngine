using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Forms;

namespace OsEngine.OsTrader.ServerAvailability
{
    /// <summary>
    /// Логика взаимодействия для ServerAvailabilityUi.xaml
    /// </summary>
    public partial class ServerAvailabilityUi : Window
    {
        public ServerAvailabilityUi()
        {
            InitializeComponent();

            CheckBoxTrackPing.IsChecked = ServerAvailabilityMaster.IsTrackPing;
            CheckBoxTrackPing.Checked += CheckBoxTrackPing_Checked;
            CheckBoxTrackPing.Unchecked += CheckBoxTrackPing_Checked;

            CreateConnectorChart();
            CreatePingValueChart();

            ServerAvailabilityMaster.PingChangeEvent += PingConnectorsMaster_PingChangeEvent;

            Title = OsLocalization.Trader.Label605;
            CheckBoxTrackPing.Content = OsLocalization.Trader.Label606;
            LabelRefreshRate.Content = OsLocalization.Trader.Label607;

            TextBoxRefreshRate.Items.Add("1");
            TextBoxRefreshRate.Items.Add("10");
            TextBoxRefreshRate.Items.Add("30");
            TextBoxRefreshRate.Items.Add("60");

            TextBoxRefreshRate.SelectionChanged += TextBoxRefreshRate_SelectionChanged;
            TextBoxRefreshRate.SelectedItem = TextBoxRefreshRate.Items[1];
        }

        private void TextBoxRefreshRate_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ServerAvailabilityMaster.CheckPingPeriod = Convert.ToDouble(TextBoxRefreshRate.SelectedItem.ToString());
        }

        private void _dataGridConnector_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                for (int i = 0; i < _dataGridConnector.Rows.Count && i < ServerAvailabilityMaster.CurrentIpConnectors.Count; i++)
                {
                    SaveConnectorService(ServerAvailabilityMaster.CurrentIpConnectors[i], _dataGridConnector.Rows[i]);
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void SaveConnectorService(ServerAvailabilityMaster.IpAdressConnectorService connectorService, DataGridViewRow dataGridViewRow)
        {
            try
            {
                if (dataGridViewRow.Cells[1].Value != null)
                {
                    connectorService.CurrentIpAddres = dataGridViewRow.Cells[1].Value.ToString();
                }
                else
                {
                    connectorService.CurrentIpAddres = connectorService.IpAddresses[0];
                }

                if (dataGridViewRow.Cells[2].Value != null)
                {
                    connectorService.IsOn = Convert.ToBoolean(dataGridViewRow.Cells[2].Value.ToString());
                }
                else
                {
                    connectorService.IsOn = false;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void PingConnectorsMaster_PingChangeEvent()
        {
            UpdateGrid();
        }

        private DataGridView _dataGridConnector;
        private DataGridView _dataGridPingValue;

        private void CreatePingValueChart()
        {
            _dataGridPingValue = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.AllCells, false);

            _dataGridConnector.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            _dataGridConnector.RowTemplate.Height = 30;

            DataGridViewCellStyle headerStyle = new DataGridViewCellStyle();
            headerStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _dataGridConnector.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = "Пинг";
            column0.ReadOnly = true;
            column0.HeaderCell.Style = headerStyle;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _dataGridPingValue.Columns.Add(column0);

            HostPingValues.Child = _dataGridPingValue;
        }

        private void CreateConnectorChart()
        {
            try
            {
                _dataGridConnector = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.AllCells);
                _dataGridConnector.ScrollBars = ScrollBars.Vertical;

                DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
                cell0.Style = _dataGridConnector.DefaultCellStyle;

                DataGridViewColumn column0 = new DataGridViewColumn();
                column0.CellTemplate = cell0;
                column0.HeaderText = "Коннектор"; // serverType
                column0.ReadOnly = true;
                column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                _dataGridConnector.Columns.Add(column0);

                DataGridViewColumn column2 = new DataGridViewColumn();
                column2.CellTemplate = cell0;
                column2.HeaderText = "Сервера"; // IpAdresServers
                column2.ReadOnly = false;
                column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _dataGridConnector.Columns.Add(column2);

                DataGridViewColumn column3 = new DataGridViewColumn();
                column3.CellTemplate = cell0;
                column3.HeaderText = OsLocalization.Market.Label182; // IsOn
                column3.ReadOnly = false;
                column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _dataGridConnector.Columns.Add(column3);

                HostActiveConnections.Child = _dataGridConnector;
                _dataGridConnector.CellValueChanged += _dataGridConnector_CellValueChanged;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void UpdateGrid()
        {
            try
            {
                if (_dataGridConnector.InvokeRequired || _dataGridPingValue.InvokeRequired)
                {
                    _dataGridConnector.Invoke(new Action(PingConnectorsMaster_PingChangeEvent));
                    return;
                }

                DataGridViewCellStyle rowStyle = new DataGridViewCellStyle();
                rowStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

                if (_dataGridConnector.Rows.Count != ServerAvailabilityMaster.CurrentIpConnectors.Count)
                {
                    _dataGridConnector.SuspendLayout();
                    _dataGridConnector.Rows.Clear();

                    for (int i = 0; i < ServerAvailabilityMaster.CurrentIpConnectors.Count; i++)
                    {
                        ServerAvailabilityMaster.IpAdressConnectorService server = ServerAvailabilityMaster.CurrentIpConnectors[i];

                        _dataGridConnector.Rows.Add(GetConnectorRow(server));
                    }

                    _dataGridConnector.ResumeLayout();
                }

                _dataGridPingValue.SuspendLayout();
                _dataGridPingValue.Rows.Clear();

                for (int i = 0; i < ServerAvailabilityMaster.CurrentIpConnectors.Count; i++)
                {
                    ServerAvailabilityMaster.IpAdressConnectorService server = ServerAvailabilityMaster.CurrentIpConnectors[i];

                    int rowIndex = _dataGridPingValue.Rows.Add();
                    _dataGridPingValue.Rows[rowIndex].Cells[0].Style = rowStyle;
                    _dataGridPingValue.Rows[rowIndex].Cells[0].Value = server.PingValue;
                }

                _dataGridPingValue.ResumeLayout();

                _dataGridConnector.Refresh();
                _dataGridPingValue.Refresh();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private DataGridViewRow GetConnectorRow(ServerAvailabilityMaster.IpAdressConnectorService service)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = service.ServerType;

            DataGridViewComboBoxCell cellIpAdress = new DataGridViewComboBoxCell();
            for (int i = 0; i < service.IpAddresses.Count(); i++)
                cellIpAdress.Items.Add(service.IpAddresses[i]);
            cellIpAdress.Value = service.CurrentIpAddres;
            nRow.Cells.Add(cellIpAdress);

            DataGridViewComboBoxCell cellIsOn = new DataGridViewComboBoxCell();
            cellIsOn.Items.Add("True");
            cellIsOn.Items.Add("False");
            cellIsOn.Value = service.IsOn.ToString();
            nRow.Cells.Add(cellIsOn);

            return nRow;
        }

        private void CheckBoxTrackPing_Checked(object sender, RoutedEventArgs e)
        {
            ServerAvailabilityMaster.IsTrackPing = CheckBoxTrackPing.IsChecked.Value;
        }
    }
}
