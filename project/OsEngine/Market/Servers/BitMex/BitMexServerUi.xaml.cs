/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using OsEngine.Logging;

namespace OsEngine.Market.Servers.BitMex
{
    /// <summary>
    /// Логика взаимодействия для BitMexServerUi.xaml
    /// </summary>
    public partial class BitMexServerUi
    {
        private BitMexServer _server;
        public BitMexServerUi(BitMexServer server, Log log)
        {
            InitializeComponent();
            _server = server;


            TextBoxUserId.Text = _server.UserId;
            TextBoxUserKey.Password = _server.UserKey;

            LabelStatus.Content = _server.ServerStatus;
            _server.ConnectStatusChangeEvent += _server_ConnectStatusChangeEvent;
            log.StartPaint(Host);

            CheckBoxTestServer.IsChecked = _server.IsDemo;
            CheckBoxTestServer.Click += CheckBoxTestServer_Click;
            CheckBoxNeadToSaveTrade.IsChecked = _server.NeadToSaveTicks;
            CheckBoxNeadToSaveTrade.Click += CheckBoxNeadToSaveTrade_Click;
            TextBoxCountDaysSave.Text = _server.CountDaysTickNeadToSave.ToString();
            TextBoxCountDaysSave.TextChanged += TextBoxCountDaysSave_TextChanged;
        }

        void CheckBoxTestServer_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxTestServer.IsChecked.HasValue)
            {
                _server.IsDemo = CheckBoxTestServer.IsChecked.Value;
                _server.Save();
            }
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
            if (!CheckBoxTestServer.Dispatcher.CheckAccess())
            {
                CheckBoxTestServer.Dispatcher.Invoke(new Action<string>(_server_ConnectStatusChangeEvent), state);
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
            _server.UserId = TextBoxUserId.Text;
            _server.UserKey = TextBoxUserKey.Password;
            _server.Save();
            _server.StartServer();
        }

        private void ButtonAbort_Click(object sender, RoutedEventArgs e)
        {
            _server.StopServer();
        }
    }
}
