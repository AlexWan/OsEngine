/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using System;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Forms;

namespace OsEngine.MCP
{
    /// <summary>
    /// Window for monitoring MCP API log and managing master host settings.
    /// </summary>
    public partial class McpApiUi : Window
    {
        #region Fields

        private McpMaster _master;
        private Action _restartHost;
        private BindingList<McpAllowedIp> _allowedIps;
        private DataGridView _gridAllowedIps;

        #endregion

        #region Constructors

        public McpApiUi(McpMaster master, Action restartHost)
        {
            InitializeComponent();

            _master = master ?? throw new ArgumentNullException(nameof(master));
            _restartHost = restartHost ?? throw new ArgumentNullException(nameof(restartHost));

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            LoadSettings();

            ButtonSave.Click += ButtonSave_Click;
            ButtonRestart.Click += ButtonRestart_Click;
            ButtonAddIp.Click += ButtonAddIp_Click;
            CheckBoxEnabled.Checked += CheckBoxEnabled_CheckedChanged;
            CheckBoxEnabled.Unchecked += CheckBoxEnabled_CheckedChanged;
            CheckBoxFullLog.Checked += CheckBoxFullLog_CheckedChanged;
            CheckBoxFullLog.Unchecked += CheckBoxFullLog_CheckedChanged;

            _master.Log.StartPaint(HostLog);

            ChangeText();
            OsLocalization.LocalizationTypeChangeEvent += ChangeText;

            Closed += McpApiUi_Closed;

            this.Activate();
            this.Focus();
        }

        private void McpApiUi_Closed(object sender, EventArgs e)
        {
            try
            {
                Closed -= McpApiUi_Closed;
                OsLocalization.LocalizationTypeChangeEvent -= ChangeText;

                ButtonSave.Click -= ButtonSave_Click;
                ButtonRestart.Click -= ButtonRestart_Click;
                ButtonAddIp.Click -= ButtonAddIp_Click;
                CheckBoxEnabled.Checked -= CheckBoxEnabled_CheckedChanged;
                CheckBoxEnabled.Unchecked -= CheckBoxEnabled_CheckedChanged;
                CheckBoxFullLog.Checked -= CheckBoxFullLog_CheckedChanged;
                CheckBoxFullLog.Unchecked -= CheckBoxFullLog_CheckedChanged;

                _master?.Log?.StopPaint();

                if (HostLog != null)
                {
                    HostLog.Child = null;
                }

                if (_gridAllowedIps != null)
                {
                    _gridAllowedIps.CellContentClick -= _gridAllowedIps_CellContentClick;
                    _gridAllowedIps.CellValidating -= _gridAllowedIps_CellValidating;
                    _gridAllowedIps.CellValueChanged -= _gridAllowedIps_CellValueChanged;
                    _gridAllowedIps.DataError -= _gridAllowedIps_DataError;

                    HostAllowedIps.Child = null;

                    DataGridFactory.ClearLinks(_gridAllowedIps);
                    _gridAllowedIps.Rows.Clear();
                    _gridAllowedIps.Columns.Clear();
                    _gridAllowedIps.DataSource = null;
                    _gridAllowedIps.Dispose();
                    _gridAllowedIps = null;
                }

                HostAllowedIps = null;

                _master = null;
                _restartHost = null;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region Settings

        private void LoadSettings()
        {
            try
            {
                CheckBoxEnabled.IsChecked = McpSettings.IsEnabled;
                CheckBoxFullLog.IsChecked = McpSettings.IsFullLogEnabled;
                TextBoxPort.Text = McpSettings.Port.ToString();
                TextBoxApiKey.Text = McpSettings.ApiKey;

                _allowedIps = new BindingList<McpAllowedIp>(
                    McpSettings.AllowedIps.Select(i => i.Clone()).ToList());

                CreateAllowedIpsTable();
                _gridAllowedIps.DataSource = _allowedIps;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        private void CreateAllowedIpsTable()
        {
            _gridAllowedIps = DataGridFactory.GetDataGridView(
                DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCells);
            _gridAllowedIps.AutoGenerateColumns = false;
            _gridAllowedIps.AllowUserToAddRows = false;
            _gridAllowedIps.AllowUserToDeleteRows = false;

            DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
            cell.Style = _gridAllowedIps.DefaultCellStyle;

            DataGridViewColumn ipColumn = new DataGridViewColumn();
            ipColumn.CellTemplate = cell;
            ipColumn.DataPropertyName = "Ip";
            ipColumn.HeaderText = OsLocalization.McpApi.ColumnIp;
            ipColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridAllowedIps.Columns.Add(ipColumn);

            DataGridViewColumn portColumn = new DataGridViewColumn();
            portColumn.CellTemplate = cell;
            portColumn.DataPropertyName = "Port";
            portColumn.HeaderText = OsLocalization.McpApi.ColumnPort;
            portColumn.Width = 100;
            _gridAllowedIps.Columns.Add(portColumn);

            DataGridViewButtonColumn deleteColumn = new DataGridViewButtonColumn();
            deleteColumn.HeaderText = "";
            deleteColumn.Text = OsLocalization.McpApi.ButtonDeleteIp;
            deleteColumn.UseColumnTextForButtonValue = true;
            deleteColumn.Width = 90;
            _gridAllowedIps.Columns.Add(deleteColumn);

            HostAllowedIps.Child = _gridAllowedIps;

            _gridAllowedIps.CellContentClick += _gridAllowedIps_CellContentClick;
            _gridAllowedIps.CellValidating += _gridAllowedIps_CellValidating;
            _gridAllowedIps.CellValueChanged += _gridAllowedIps_CellValueChanged;
            _gridAllowedIps.DataError += _gridAllowedIps_DataError;
        }

        private void _gridAllowedIps_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.ColumnIndex >= _gridAllowedIps.Columns.Count - 1)
                {
                    return;
                }

                string value = e.FormattedValue?.ToString() ?? string.Empty;

                if (e.ColumnIndex == 0)
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        e.Cancel = true;
                        System.Windows.MessageBox.Show("IP address is required.");
                        return;
                    }

                    if (!IPAddress.TryParse(value, out _))
                    {
                        e.Cancel = true;
                        System.Windows.MessageBox.Show($"Invalid IP address '{value}'.");
                        return;
                    }
                }
                else if (e.ColumnIndex == 1)
                {
                    if (!string.IsNullOrWhiteSpace(value)
                        && !string.Equals(value, "any", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!int.TryParse(value, out int portValue)
                            || portValue < 0
                            || portValue > 65535)
                        {
                            e.Cancel = true;
                            System.Windows.MessageBox.Show($"Invalid port '{value}'. Use 'any' or a number from 0 to 65535.");
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        private void _gridAllowedIps_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.ColumnIndex >= _gridAllowedIps.Columns.Count - 1)
                {
                    return;
                }

                McpSettings.AllowedIps = _allowedIps.ToList();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        private void _gridAllowedIps_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0
                    || e.RowIndex >= _allowedIps.Count
                    || e.ColumnIndex != _gridAllowedIps.Columns.Count - 1)
                {
                    return;
                }

                _gridAllowedIps.EndEdit();
                _allowedIps.RemoveAt(e.RowIndex);
                McpSettings.AllowedIps = _allowedIps.ToList();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        private void _gridAllowedIps_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), OsEngine.Logging.LogMessageType.Error);
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(TextBoxPort.Text, out int port))
                {
                    System.Windows.MessageBox.Show("Port must be a number.");
                    return;
                }

                _gridAllowedIps?.EndEdit();

                if (!ValidateAllowedIps())
                {
                    return;
                }

                McpSettings.Port = port;
                McpSettings.ApiKey = TextBoxApiKey.Text;
                McpSettings.IsEnabled = CheckBoxEnabled.IsChecked == true;
                McpSettings.IsFullLogEnabled = CheckBoxFullLog.IsChecked == true;
                McpSettings.AllowedIps = _allowedIps.ToList();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        private bool ValidateAllowedIps()
        {
            for (int i = 0; i < _allowedIps.Count; i++)
            {
                McpAllowedIp item = _allowedIps[i];

                if (string.IsNullOrWhiteSpace(item.Ip))
                {
                    System.Windows.MessageBox.Show($"IP address is required in row {i + 1}.");
                    return false;
                }

                if (!IPAddress.TryParse(item.Ip, out _))
                {
                    System.Windows.MessageBox.Show($"Invalid IP address '{item.Ip}' in row {i + 1}.");
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(item.Port)
                    && !string.Equals(item.Port, "any", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(item.Port, out int portValue)
                        || portValue < 0
                        || portValue > 65535)
                    {
                        System.Windows.MessageBox.Show($"Invalid port '{item.Port}' in row {i + 1}. Use 'any' or a number from 0 to 65535.");
                        return false;
                    }
                }
            }

            return true;
        }

        private void ButtonAddIp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _gridAllowedIps?.EndEdit();
                _allowedIps.Add(new McpAllowedIp { Ip = "0.0.0.0", Port = "any" });
                McpSettings.AllowedIps = _allowedIps.ToList();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        private void ButtonRestart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _restartHost.Invoke();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        private void CheckBoxEnabled_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                McpSettings.IsEnabled = CheckBoxEnabled.IsChecked == true;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        private void CheckBoxFullLog_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                McpSettings.IsFullLogEnabled = CheckBoxFullLog.IsChecked == true;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region Localization

        private void ChangeText()
        {
            try
            {
                Title = OsLocalization.McpApi.Title;
                LabelEnabled.Content = OsLocalization.McpApi.LabelEnabled;
                LabelFullLog.Content = OsLocalization.McpApi.LabelFullLog;
                LabelPort.Content = OsLocalization.McpApi.LabelPort;
                LabelApiKey.Content = OsLocalization.McpApi.LabelApiKey;
                ButtonSave.Content = OsLocalization.McpApi.ButtonSave;
                ButtonRestart.Content = OsLocalization.McpApi.ButtonRestart;
                ButtonAddIp.Content = OsLocalization.McpApi.ButtonAddIp;
                TabItemLog.Header = OsLocalization.McpApi.TabLog;
                TabItemAllowedIps.Header = OsLocalization.McpApi.TabAllowedIps;

                if (_gridAllowedIps != null)
                {
                    _gridAllowedIps.Columns[0].HeaderText = OsLocalization.McpApi.ColumnIp;
                    _gridAllowedIps.Columns[1].HeaderText = OsLocalization.McpApi.ColumnPort;
                    _gridAllowedIps.Columns[2].HeaderText = "";

                    DataGridViewButtonColumn deleteColumn = _gridAllowedIps.Columns[2] as DataGridViewButtonColumn;
                    if (deleteColumn != null)
                    {
                        deleteColumn.Text = OsLocalization.McpApi.ButtonDeleteIp;
                    }
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        #endregion

    }
}
