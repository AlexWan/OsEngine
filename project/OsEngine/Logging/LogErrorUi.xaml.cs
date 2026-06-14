/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
using OsEngine.Market;

namespace OsEngine.Logging
{
    /// <summary>
    /// Interaction logic for LogErrorUi.xaml
    /// Логика взаимодействия для LogErrorUi.xaml
    /// </summary>
    public partial class LogErrorUi
    {
        private DataGridView _gridErrorLog;

        public LogErrorUi(DataGridView gridErrorLog)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            GlobalGUILayout.Listen(this, "LogErrorUi");
            _gridErrorLog = gridErrorLog;
            HostLog.Child = _gridErrorLog;
            Title = OsLocalization.Logging.TitleExtraLog;
            Title = Title + " " + OsEngine.PrimeSettings.PrimeSettingsMaster.LabelInHeaderBotStation;
            
            this.Activate();
            this.Focus();

            ButtonClear.Content = OsLocalization.Logging.ButtonClearExtraLog;

            Closed += LogErrorUi_Closed;
        }

        private void LogErrorUi_Closed(object sender, EventArgs e)
        {
            try
            {
                Closed -= LogErrorUi_Closed;
                if (_gridErrorLog != null)
                {
                    HostLog.Child = null;
                    _gridErrorLog = null;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonClear_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Logging.Label30);
            ui.ShowDialog();

            if (ui.UserAcceptAction == false)
            {
                return;
            }

            Log.ClearErrorLog();
        }
    }
}