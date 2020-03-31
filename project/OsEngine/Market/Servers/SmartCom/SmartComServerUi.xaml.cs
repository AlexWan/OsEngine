/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using OsEngine.Language;
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

            Title = OsLocalization.Market.TitleAServerParametrUi + _server.ServerType;

            LabelAdress.Content = OsLocalization.Market.Message59;
            LabelPort.Content = OsLocalization.Market.Message90;
            LabelNameUser.Content = OsLocalization.Market.Message63;
            LabelPassword.Content = OsLocalization.Market.Message64;
            LabelServerState.Content = OsLocalization.Market.Label21;

            ButtonConnect.Content = OsLocalization.Market.ButtonConnect;
            ButtonAbort.Content = OsLocalization.Market.ButtonDisconnect;

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
