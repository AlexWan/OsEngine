using System;
using System.Windows;
using OsEngine.Logging;

namespace OsEngine.Market.Servers.Binance
{
    /// <summary>
    /// Логика взаимодействия для BinanceServerUi.xaml
    /// </summary>
    public partial class BinanceServerUi
    {
        private BinanceServer _server;

        private BinanceServerRealization _serverRealization;

        public BinanceServerUi(BinanceServer server, Log log)
        {
            InitializeComponent();
            _server = server;

            _serverRealization = (BinanceServerRealization)_server.ServerRealization;

            TextBoxUserKey.Password = _serverRealization.UserKey;
            TextBoxUserSecretKey.Password = _serverRealization.UserPrivateKey;

            LabelStatus.Content = _server.ServerStatus;
            _server.ConnectStatusChangeEvent += _server_ConnectStatusChangeEvent;
            log.StartPaint(Host);

            CheckBoxNeadToSaveTrade.IsChecked = _server.NeadToSaveTicks;
            CheckBoxNeadToSaveTrade.Click += CheckBoxNeadToSaveTrade_Click;
            TextBoxCountDaysSave.Text = _server.CountDaysTickNeadToSave.ToString();
            TextBoxCountDaysSave.TextChanged += TextBoxCountDaysSave_TextChanged;
        }

        void TextBoxCountDaysSave_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(TextBoxCountDaysSave.Text) < 0 ||
                    Convert.ToInt32(TextBoxCountDaysSave.Text) > 30)
                {
                    throw new Exception();
                }
            }
            catch (Exception)
            {
                TextBoxCountDaysSave.Text = _server.CountDaysTickNeadToSave.ToString();
            }

            _server.CountDaysTickNeadToSave = Convert.ToInt32(TextBoxCountDaysSave.Text);
            _server.Save();
        }

        void _server_ConnectStatusChangeEvent(string state)
        {
            if (!CheckBoxNeadToSaveTrade.Dispatcher.CheckAccess())
            {
                CheckBoxNeadToSaveTrade.Dispatcher.Invoke(new Action<string>(_server_ConnectStatusChangeEvent), state);
                return;
            }

            LabelStatus.Content = state;
        }

        void CheckBoxNeadToSaveTrade_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxNeadToSaveTrade.IsChecked.HasValue)
            {
                _server.NeadToSaveTicks = CheckBoxNeadToSaveTrade.IsChecked.Value;
                _server.Save();
            }
        }

        private void ButtonConnect_Click(object sender, RoutedEventArgs e)
        {
            _serverRealization.UserKey = TextBoxUserKey.Password;
            _serverRealization.UserPrivateKey = TextBoxUserSecretKey.Password;
            _server.Save();
            _server.StartServer();
        }

        private void ButtonAbort_Click(object sender, RoutedEventArgs e)
        {
            _server.StopServer();
        }
    }
}
