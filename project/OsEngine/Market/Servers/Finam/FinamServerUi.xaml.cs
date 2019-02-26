/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using OsEngine.Language;
using OsEngine.Logging;

namespace OsEngine.Market.Servers.Finam
{
    /// <summary>
    /// Interaction logic for FinamServerUi.xaml
    /// Логика взаимодействия для FinamServerUi.xaml
    /// </summary>
    public partial class FinamServerUi
    {
        private FinamServer _server;
        public FinamServerUi(FinamServer server, Log log)
        {
            InitializeComponent();
            _server = server;

            TextBoxServerAdress.Text = _server.ServerAdress;

            LabelStatus.Content = _server.ServerStatus;
            _server.ConnectStatusChangeEvent += _server_ConnectStatusChangeEvent;
            log.StartPaint(Host);
            Label41.Content = OsLocalization.Market.Label41;
            Label21.Content = OsLocalization.Market.Label21;
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
            _server.Save();
            _server.StartServer();
        }

        private void ButtonAbort_Click(object sender, RoutedEventArgs e)
        {
            _server.StopServer();
        }
    }
}
