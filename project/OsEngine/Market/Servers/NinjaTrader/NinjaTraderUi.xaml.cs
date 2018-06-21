using System;
/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/
using System.Windows;
using OsEngine.Logging;

namespace OsEngine.Market.Servers.NinjaTrader
{
    /// <summary>
    /// Логика взаимодействия для NinjaTraderUi.xaml
    /// </summary>
    public partial class NinjaTraderUi
    {
        private readonly NinjaTraderServer _server;

        public NinjaTraderUi(NinjaTraderServer serv, Log log)
        {
            InitializeComponent();
            _server = serv;

            LabelStatus.Content = _server.ServerStatus;
            _server.ConnectStatusChangeEvent += server_ConnectChangeEvent;
            log.StartPaint(Host);
            CheckBoxNeadToSaveTrade.IsChecked = _server.NeadToSaveTicks;
            CheckBoxNeadToSaveTrade.Click += CheckBoxNeadToSaveTrade_Click;
            TextBoxCountDaysSave.Text = _server.CountDaysTickNeadToSave.ToString();
            TextBoxCountDaysSave.TextChanged += TextBoxCountDaysSave_TextChanged;

            TextBoxServerAdress.Text = _server.ServerAdress;
            TextBoxPort.Text = _server.Port;
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
            if (string.IsNullOrWhiteSpace(TextBoxServerAdress.Text) ||
                string.IsNullOrWhiteSpace(TextBoxPort.Text))
            {
                MessageBox.Show("Не хватает данных чтобы запустить сервер!");
                return;
            }

            TextBoxServerAdress.Text = _server.ServerAdress;
            TextBoxPort.Text = _server.Port;

            _server.Save();
            _server.StartServer();
        }

        private void ButtonAbort_Click(object sender, RoutedEventArgs e) // кнопка остановить сервер
        {
            TextBoxServerAdress.Text = _server.ServerAdress;
            TextBoxPort.Text = _server.Port;

            _server.StopServer();
        }

    }
}
