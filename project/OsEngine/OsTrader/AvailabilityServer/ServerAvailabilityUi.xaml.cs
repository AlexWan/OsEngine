using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
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

            StickyBorders.Listen(this);
            StartupLocation.Start_MouseInCentre(this);

            CheckBoxTrackPing.IsChecked = ServerAvailabilityMaster.IsTrackPing;
            CheckBoxTrackPing.Checked += CheckBoxTrackPing_Checked;
            CheckBoxTrackPing.Unchecked += CheckBoxTrackPing_Checked;

            // Изменяем порядок: сначала создаем обе таблицы
            _dataGridConnector = CreateConnectorChart();
            _dataGridPingValue = CreatePingValueChart();

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

        #region Events

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

        private void PingConnectorsMaster_PingChangeEvent()
        {
            UpdateGrid();
        }

        private void CheckBoxTrackPing_Checked(object sender, RoutedEventArgs e)
        {
            ServerAvailabilityMaster.IsTrackPing = CheckBoxTrackPing.IsChecked.Value;
        }

        #endregion

        #region Fields

        private DataGridView _dataGridConnector;
        private DataGridView _dataGridPingValue;

        #endregion

        #region Create chart

        private DataGridView CreatePingValueChart()
        {
            var dataGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.None, false);
            dataGrid.RowTemplate.Height = 50;
            dataGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None; // ← ВАЖНО: отключаем авто-размер

            DataGridViewCellStyle headerStyle = new DataGridViewCellStyle();
            headerStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = dataGrid.DefaultCellStyle; // ← Используем стиль текущей таблицы

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = "Пинг";
            column0.ReadOnly = true;
            column0.HeaderCell.Style = headerStyle;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            dataGrid.Columns.Add(column0);

            HostPingValues.Child = dataGrid;
            return dataGrid;
        }

        private DataGridView CreateConnectorChart()
        {
            try
            {
                var dataGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.None);
                dataGrid.ScrollBars = ScrollBars.Vertical;
                dataGrid.RowTemplate.Height = 50;
                dataGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None; // ← ВАЖНО: отключаем авто-размер

                DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
                cell0.Style = dataGrid.DefaultCellStyle;

                DataGridViewColumn column0 = new DataGridViewColumn();
                column0.CellTemplate = cell0;
                column0.HeaderText = "Коннектор";
                column0.ReadOnly = true;
                column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                dataGrid.Columns.Add(column0);

                DataGridViewColumn column1 = new DataGridViewColumn();
                column1.CellTemplate = cell0;
                column1.HeaderText = "Сервера";
                column1.ReadOnly = false;
                column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                dataGrid.Columns.Add(column1);

                DataGridViewColumn column2 = new DataGridViewColumn();
                column2.CellTemplate = cell0;
                column2.HeaderText = OsLocalization.Market.Label182;
                column2.ReadOnly = false;
                column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                dataGrid.Columns.Add(column2);

                HostActiveConnections.Child = dataGrid;
                dataGrid.CellValueChanged += _dataGridConnector_CellValueChanged;

                return dataGrid;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                return null;
            }
        }

        #endregion

        #region Update chart

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

        private void UpdateGrid()
        {
            try
            {
                if (_dataGridConnector.InvokeRequired || _dataGridPingValue.InvokeRequired)
                {
                    _dataGridConnector.Invoke(new Action(UpdateGrid));
                    return;
                }

                if (_dataGridConnector.Rows.Count != ServerAvailabilityMaster.CurrentIpConnectors.Count)
                {
                    _dataGridConnector.SuspendLayout();
                    _dataGridConnector.Rows.Clear();

                    for (int i = 0; i < ServerAvailabilityMaster.CurrentIpConnectors.Count; i++)
                    {
                        ServerAvailabilityMaster.IpAdressConnectorService server = ServerAvailabilityMaster.CurrentIpConnectors[i];
                        DataGridViewRow row = GetConnectorRow(server);
                        row.Height = 50;
                        _dataGridConnector.Rows.Add(row);
                    }
                    _dataGridConnector.ResumeLayout();
                }

                _dataGridPingValue.SuspendLayout();
                _dataGridPingValue.Rows.Clear();

                for (int i = 0; i < ServerAvailabilityMaster.CurrentIpConnectors.Count; i++)
                {
                    ServerAvailabilityMaster.IpAdressConnectorService server = ServerAvailabilityMaster.CurrentIpConnectors[i];

                    int rowIndex = _dataGridPingValue.Rows.Add();
                    DataGridViewRow row = _dataGridPingValue.Rows[rowIndex];
                    row.Height = 50;
                    row.Cells[0].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    row.Cells[0].Value = server.PingValue;
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
            nRow.Height = 50;

            DataGridViewCellStyle cellStyle = new DataGridViewCellStyle();
            cellStyle.Padding = new Padding(0, 5, 0, 5);

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = service.ServerType;
            nRow.Cells[nRow.Cells.Count - 1].Style = cellStyle;

            DataGridViewComboBoxCell cellIpAdress = new DataGridViewComboBoxCell();
            cellIpAdress.FlatStyle = FlatStyle.Flat;
            for (int i = 0; i < service.IpAddresses.Count(); i++)
                cellIpAdress.Items.Add(service.IpAddresses[i]);
            cellIpAdress.Value = service.CurrentIpAddres;
            cellIpAdress.Style = cellStyle;
            nRow.Cells.Add(cellIpAdress);

            DataGridViewComboBoxCell cellIsOn = new DataGridViewComboBoxCell();
            cellIsOn.FlatStyle = FlatStyle.Flat;
            cellIsOn.Items.Add("True");
            cellIsOn.Items.Add("False");
            cellIsOn.Value = service.IsOn.ToString();
            cellIsOn.Style = cellStyle;
            nRow.Cells.Add(cellIsOn);

            return nRow;
        }

        #endregion
    }
}
