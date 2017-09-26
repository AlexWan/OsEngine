using System;
using System.Windows;
using OsEngine.Logging;

namespace OsEngine.Market.Servers.Oanda
{
    /// <summary>
    /// Логика взаимодействия для OandaServerUi.xaml
    /// </summary>
    public partial class OandaServerUi
    {
        private OandaServer _server;

        public OandaServerUi(OandaServer serv, Log log)
        {
            InitializeComponent();
            _server = serv;

            LabelStatus.Content = _server.ServerStatus;
            _server.ConnectStatusChangeEvent += server_ConnectChangeEvent;
            log.StartPaint(Host);
            CheckBoxNeadToSaveTrade.IsChecked = _server.NeadToSaveTicks;
            CheckBoxNeadToSaveTrade.Click += CheckBoxNeadToSaveTrade_Click;
            TextBoxToken.Password = _server.Token;
            TextBoxId.Text = _server.ClientIdInSystem;
            CheckBoxTestServer.IsChecked = _server.IsTestConnection;
        }

        void CheckBoxNeadToSaveTrade_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxNeadToSaveTrade.IsChecked.HasValue)
            {
                _server.NeadToSaveTicks = CheckBoxNeadToSaveTrade.IsChecked.Value;
                _server.Save();
            }
        }

        void server_ConnectChangeEvent(string status) // изменился статус сервера
        {
            if (!LabelStatus.CheckAccess())
            {
                LabelStatus.Dispatcher.Invoke(new Action<string>(server_ConnectChangeEvent), status);
                return;
            }

            LabelStatus.Content = status;
        }

        private void ButtonConnect_Click(object sender, RoutedEventArgs e) // кнопка подключить сервер
        {
            if (string.IsNullOrWhiteSpace(TextBoxToken.Password) ||
                string.IsNullOrWhiteSpace(TextBoxId.Text))
            {
                MessageBox.Show("Не хватает данных чтобы запустить сервер!");
                return;
            }

            if (CheckBoxTestServer.IsChecked != null)
                _server.IsTestConnection = CheckBoxTestServer.IsChecked.Value;

            _server.Token = TextBoxToken.Password;
            _server.ClientIdInSystem = TextBoxId.Text;

            _server.Save();
            _server.StartServer();
        }

        private void ButtonAbort_Click(object sender, RoutedEventArgs e) // кнопка остановить сервер
        {
            _server.StopServer();
        }
    }
}
