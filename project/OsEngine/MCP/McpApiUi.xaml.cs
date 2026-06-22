/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple.pdf
*/

using OsEngine.Language;
using OsEngine.Market;
using System;
using System.Windows;

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
                CheckBoxEnabled.Checked -= CheckBoxEnabled_CheckedChanged;
                CheckBoxEnabled.Unchecked -= CheckBoxEnabled_CheckedChanged;
                CheckBoxFullLog.Checked -= CheckBoxFullLog_CheckedChanged;
                CheckBoxFullLog.Unchecked -= CheckBoxFullLog_CheckedChanged;

                _master?.Log?.StopPaint();

                if (HostLog != null)
                {
                    HostLog.Child = null;
                }

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
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
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

                McpSettings.Port = port;
                McpSettings.ApiKey = TextBoxApiKey.Text;
                McpSettings.IsEnabled = CheckBoxEnabled.IsChecked == true;
                McpSettings.IsFullLogEnabled = CheckBoxFullLog.IsChecked == true;
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
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        #endregion

    }
}
