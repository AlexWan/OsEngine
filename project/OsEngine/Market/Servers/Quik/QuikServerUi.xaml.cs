/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using OsEngine.Logging;

namespace OsEngine.Market.Servers.Quik
{

    /// <summary>
    /// Логика взаимодействия для QuikServerUi.xaml
    /// </summary>
    public partial class QuikServerUi
    {
        private QuikServer _server;

        public QuikServerUi(QuikServer serv, Log log)
        {
            InitializeComponent();
            _server = serv;

            LabelStatus.Content = _server.ServerStatus;
            _server.ConnectStatusChangeEvent += server_ConnectChangeEvent;
            TextBoxPathToQuik.Text = ((QuikDdeServerRealization)_server.ServerRealization).PathToQuik;
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
            if (string.IsNullOrWhiteSpace(TextBoxPathToQuik.Text))
            {
                MessageBox.Show("Не хватает данных чтобы запустить сервер!");
                return;
            }

            ((QuikDdeServerRealization)_server.ServerRealization).PathToQuik = TextBoxPathToQuik.Text;
            _server.Save();

            _server.StartServer();
        }

        private void ButtonPathToQuikDialog_Click(object sender, RoutedEventArgs e) // кнопка указать путь к Квик
        {
            System.Windows.Forms.FolderBrowserDialog myDialog = new System.Windows.Forms.FolderBrowserDialog();
            myDialog.ShowDialog();

            if (myDialog.SelectedPath != "") // если хоть что-то выбрано и это свечи
            {
                ((QuikDdeServerRealization)_server.ServerRealization).PathToQuik = myDialog.SelectedPath;
                TextBoxPathToQuik.Text = ((QuikDdeServerRealization)_server.ServerRealization).PathToQuik;
                _server.Save();
            }
        }

        private void ButtonAbort_Click(object sender, RoutedEventArgs e) // кнопка остановить сервер
        {
            ((QuikDdeServerRealization)_server.ServerRealization).PathToQuik = TextBoxPathToQuik.Text;
            _server.StopServer();
        }

    }
}
