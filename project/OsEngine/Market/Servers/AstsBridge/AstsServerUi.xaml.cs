/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using OsEngine.Logging;

namespace OsEngine.Market.Servers.AstsBridge
{
    /// <summary>
    /// Логика взаимодействия для SmartComServerUi.xaml
    /// </summary>
    public partial class AstsServerUi
    {
        private AstsBridgeServer _server;

        public AstsServerUi(AstsBridgeServer server, Log log)
        {
            InitializeComponent();
            _server = server;

            TextBoxServerAdress.Text = _server.ServerAdress;
            TextBoxUserName.Text = _server.UserLogin;
            TextBoxUserPassword.Password = _server.UserPassword;
            TextBoxServerName.Text = _server.ServerName;
            TextBoxServiceName.Text = _server.ServiseName;


            LabelStatus.Content = _server.ServerStatus;
            _server.ConnectStatusChangeEvent += _server_ConnectStatusChangeEvent;
            log.StartPaint(Host);

            ComboBoxDislocation.Items.Add(AstsDislocation.Colo);
            ComboBoxDislocation.Items.Add(AstsDislocation.Internet);

            ComboBoxDislocation.SelectedItem = server.Dislocation;
            ComboBoxDislocation.ToolTip = "В не зоны колокации доступно не более 10 обновлений данных в секунду. Если в зоне интернет запросить данные чаще - последуют санкции";

            CheckBoxNeadToSaveTrade.IsChecked = server.NeadToSaveTicks;
            TextBoxCountDaysSave.Text = server.CountDaysTickNeadToSave.ToString();
        }

        void _server_ConnectStatusChangeEvent(string state)
        {
            if (!TextBoxServerAdress.Dispatcher.CheckAccess())
            {
                TextBoxServerAdress.Dispatcher.Invoke(new Action<string>(_server_ConnectStatusChangeEvent), state);
                return;
            }

            LabelStatus.Content = state;
        }

        private void ButtonConnect_Click(object sender, RoutedEventArgs e)
        {
            _server.ServerAdress = TextBoxServerAdress.Text;
            _server.UserLogin = TextBoxUserName.Text;
            _server.UserPassword = TextBoxUserPassword.Password;
            _server.ServerName = TextBoxServerName.Text;
            _server.ServiseName = TextBoxServiceName.Text;

            Enum.TryParse(ComboBoxDislocation.SelectedItem.ToString(), out _server.Dislocation);

            if (CheckBoxNeadToSaveTrade.IsChecked.HasValue)
            {
                _server.NeadToSaveTicks = CheckBoxNeadToSaveTrade.IsChecked.Value;
            }

            try
            {
                if (Convert.ToInt32(TextBoxCountDaysSave.Text) < 0 && Convert.ToInt32(TextBoxCountDaysSave.Text) > 50)
                {
                    return;
                }
            }
            catch
            {
                return;
            }


            _server.CountDaysTickNeadToSave = Convert.ToInt32(TextBoxCountDaysSave.Text);

            _server.Save();
            _server.StartServer();
        }

        private void ButtonAbort_Click(object sender, RoutedEventArgs e)
        {
            _server.StopServer();
        }
    }
}
