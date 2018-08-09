using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using OsEngine.Logging;

namespace OsEngine.Market.Servers.Bitfinex
{
    /// <summary>
    /// Логика взаимодействия для BitfinexServerUi.xaml
    /// </summary>
    public partial class BitfinexServerUi : Window
    {
        private BitfinexServer _server;
        public BitfinexServerUi(BitfinexServer server, Log log)
        {
            InitializeComponent();
            _server = server;

            TextBoxUserKey.Password = _server.UserKey;
            TextBoxUserSecretKey.Password = _server.UserPrivateKey;

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
            _server.UserKey = TextBoxUserKey.Password;
            _server.UserPrivateKey = TextBoxUserSecretKey.Password;
            _server.Save();
            _server.StartServer();
        }

        private void ButtonAbort_Click(object sender, RoutedEventArgs e)
        {
            _server.StopServer();
        }
    }
}
