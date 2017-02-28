/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using OsEngine.Logging;

namespace OsEngine.Market.Servers.SmartCom
{
    /// <summary>
    /// Логика взаимодействия для SmartComServerUi.xaml
    /// </summary>
    public partial class SmartComServerUi
    {
        private SmartComServer _server;
        public SmartComServerUi(SmartComServer server, Log log)
        {
            InitializeComponent();
            _server = server;

            TextBoxServerAdress.Text = _server.ServerAdress;
            TextBoxServerPort.Text = _server.ServerPort;
            TextBoxUserName.Text = _server.UserLogin;
            TextBoxUserPassword.Password = _server.UserPassword;

            LabelStatus.Content = _server.ServerStatus;
            _server.ConnectStatusChangeEvent += _server_ConnectStatusChangeEvent;
            log.StartPaint(Host);
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
            _server.ServerPort = TextBoxServerPort.Text;
            _server.UserLogin = TextBoxUserName.Text;
            _server.UserPassword = TextBoxUserPassword.Password;
            _server.Save();
            _server.StartServer();
        }

        private void ButtonAbort_Click(object sender, RoutedEventArgs e)
        {
            _server.StopServer();
        }
    }
}
