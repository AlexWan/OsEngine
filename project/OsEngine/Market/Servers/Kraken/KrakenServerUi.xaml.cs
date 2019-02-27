using System;
using System.Windows;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;

namespace OsEngine.Market.Servers.Kraken
{
    /// <summary>
    /// Interaction logic for KrakenServerUi.xaml
    /// Логика взаимодействия для KrakenServerUi.xaml
    /// </summary>
    public partial class KrakenServerUi
    {
      private KrakenServer _server;

        public KrakenServerUi(KrakenServer serv, Log log)
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
            TextBoxPublicKey.Text = _server.PublicKey;
            TextBoxPrivateKey.Password = _server.PrivateKey;

            ComboBoxLoadDataType.Items.Add(KrakenDateType.OnlyMarketDepth);
            ComboBoxLoadDataType.Items.Add(KrakenDateType.OnlyTrades);
            ComboBoxLoadDataType.Items.Add(KrakenDateType.AllData);
            
            ComboBoxLoadDataType.SelectedItem = _server.LoadDateType;

            ComboBoxLeverage.Items.Add("none");
            ComboBoxLeverage.Items.Add("2");
            ComboBoxLeverage.Items.Add("3");
            ComboBoxLeverage.Items.Add("4");
            ComboBoxLeverage.Items.Add("5");
            ComboBoxLeverage.SelectedItem = _server.LeverageType;
            ComboBoxLeverage.SelectionChanged += ComboBoxLeverage_SelectionChanged;

            LabelPublicKey.Content = OsLocalization.Market.ServerParamPublicKey;
            LabelSecretKey.Content = OsLocalization.Market.ServerParamSecretKey;
            LabelDaysToLoad.Content = OsLocalization.Market.ServerParam2;
            CheckBoxNeadToSaveTrade.Content = OsLocalization.Market.ServerParam1;
            LabelServerState.Content = OsLocalization.Market.Label21;
            ButtonConnect.Content = OsLocalization.Market.ButtonConnect;
            ButtonAbort.Content = OsLocalization.Market.ButtonDisconnect;
            ButtonProxy.Content = OsLocalization.Market.ServerParamProxy;
            LabelLeverage.Content = OsLocalization.Market.ServerParamLeverage;
            LabelLoadDataType.Content = OsLocalization.Market.ServerParam3;


        }
        
        void ComboBoxLeverage_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _server.LeverageType = ComboBoxLeverage.SelectedItem.ToString();
            _server.Save();
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

        void server_ConnectChangeEvent(string status) // changed serve status / изменился статус сервера
        {
            if (!LabelStatus.CheckAccess())
            {
                LabelStatus.Dispatcher.Invoke(new Action<string>(server_ConnectChangeEvent), status);
                return;
            }

            LabelStatus.Content = status;
        }

        private void ButtonConnect_Click(object sender, RoutedEventArgs e) // button connecto to server / кнопка подключить сервер
        {
            if (string.IsNullOrWhiteSpace(TextBoxPublicKey.Text) )
            {
                MessageBox.Show(OsLocalization.Market.Label55);
                return;
            }

            if (string.IsNullOrWhiteSpace(TextBoxPrivateKey.Password))
            {
                MessageBox.Show(OsLocalization.Market.Label55);
                return;
            }
            KrakenDateType loadDateType;
            Enum.TryParse(ComboBoxLoadDataType.SelectedItem.ToString(), out loadDateType);
            _server.LoadDateType = loadDateType;

            _server.PublicKey = TextBoxPublicKey.Text;
            _server.PrivateKey = TextBoxPrivateKey.Password;
            _server.Save();
            _server.StartServer();
        }

        private void ButtonAbort_Click(object sender, RoutedEventArgs e) // button stop server / кнопка остановить сервер
        {
            _server.StopServer();
        }

        private void ButtonProxy_Click(object sender, RoutedEventArgs e)
        {
            ProxiesUi ui = new ProxiesUi(_server.Proxies, _server);
            ui.ShowDialog();
        }

    }
}
