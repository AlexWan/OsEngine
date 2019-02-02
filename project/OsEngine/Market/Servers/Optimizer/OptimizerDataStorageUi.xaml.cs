using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Tester;

namespace OsEngine.Market.Servers.Optimizer
{
    /// <summary>
    /// Логика взаимодействия для TesterServerUi.xaml
    /// </summary>
    public partial class OptimizerDataStorageUi
    {
        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="server">сервер</param>
        /// <param name="log">лог</param>
        public OptimizerDataStorageUi(OptimizerDataStorage server, Log log)
        {
            InitializeComponent();
            _server = server;

            log.StartPaint(Host);

            _server.SecuritiesChangeEvent += _server_SecuritiesChangeEvent;

            CreateGrid();
            PaintGrid();

            TextBoxFrom.TextChanged += TextBoxFrom_TextChanged;
            TextBoxTo.TextChanged += TextBoxTo_TextChanged;

            // прогресс бар

            List<string> sets = _server.Sets;

            // сеты

            for (int i = 0; sets != null && sets.Count != 0 && i < sets.Count; i++)
            {
                ComboBoxSets.Items.Add(sets[i]);
            }
            if (!string.IsNullOrEmpty(_server.ActiveSet) &&
                _server.ActiveSet.Split('_').Length == 2)
            {
                ComboBoxSets.SelectedItem = _server.ActiveSet.Split('_')[1];
            }

            ComboBoxSets.SelectionChanged += ComboBoxSets_SelectionChanged;

            // данные для тестирования

            ComboBoxDataType.Items.Add(TesterDataType.Candle);
            ComboBoxDataType.Items.Add(TesterDataType.TickOnlyReadyCandle);
            ComboBoxDataType.Items.Add(TesterDataType.MarketDepthOnlyReadyCandle);
            ComboBoxDataType.SelectedItem = _server.TypeTesterData;
            ComboBoxDataType.SelectionChanged += ComboBoxDataType_SelectionChanged;

            TextBoxDataPath.Text = _server.PathToFolder;
            ComboBoxDataSourseType.Items.Add(TesterSourceDataType.Folder);
            ComboBoxDataSourseType.Items.Add(TesterSourceDataType.Set);
            ComboBoxDataSourseType.SelectedItem = _server.SourceDataType;
            ComboBoxDataSourseType.SelectionChanged += ComboBoxDataSourseType_SelectionChanged;

            Title = OsLocalization.Market.TitleTester;
            Label22.Header = OsLocalization.Market.Label22;
            Label23.Header = OsLocalization.Market.Label23;
            Label24.Content = OsLocalization.Market.Label24;
            Label25.Content = OsLocalization.Market.Label25;
            LabelFrom.Content = OsLocalization.Market.Label26;
            LabelTo.Content = OsLocalization.Market.Label27;
            Label28.Content = OsLocalization.Market.Label28;
            ButtonSetDataFromPath.Content = OsLocalization.Market.ButtonSetFolder;
        }

        /// <summary>
        /// источник данных изменился. Папка или Сет 
        /// </summary>
        void ComboBoxDataSourseType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            TesterSourceDataType sourceDataType;
            Enum.TryParse(ComboBoxDataSourseType.SelectedItem.ToString(), out sourceDataType);
            _server.SourceDataType = sourceDataType;
        }

        /// <summary>
        /// изменился тип транслируемых данных
        /// </summary>
        void ComboBoxDataType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            TesterDataType type;
            Enum.TryParse(ComboBoxDataType.SelectedItem.ToString(), out type);
            _server.TypeTesterData = type;
            _server.Save();

            PaintGrid();
        }

        /// <summary>
        /// сет данных изменился
        /// </summary>
        void ComboBoxSets_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _server.SetNewSet(ComboBoxSets.SelectedItem.ToString());
            PaintGrid();
        }

        // сервер

        /// <summary>
        /// тестовый сервер
        /// </summary>
        private OptimizerDataStorage _server;

        /// <summary>
        /// изменились инструменты в сервере
        /// </summary>
        void _server_SecuritiesChangeEvent(List<Security> securities)
        {
            PaintGrid();
        }

        //  таблица с инструментами

        /// <summary>
        /// таблица с инструментами
        /// </summary>
        private DataGridView _myGridView;

        /// <summary>
        /// создать таблицу с инструментами
        /// </summary>
        private void CreateGrid()
        {
            _myGridView = DataGridFactory.GetDataGridDataSource();

            _myGridView.DoubleClick += _myGridView_DoubleClick;
            HostSecurities.Child = _myGridView;
            HostSecurities.Child.Show();
            _myGridView.Rows.Add();
        }

        /// <summary>
        /// прорисовать таблицу с инструментами
        /// </summary>
        private void PaintGrid()
        {
            if (_myGridView.InvokeRequired)
            {
                _myGridView.Invoke(new Action(PaintGrid));
                return;
            }

            SliderFrom.ValueChanged -= SliderFrom_ValueChanged;
            SliderTo.ValueChanged -= SliderTo_ValueChanged;

            _myGridView.Rows.Clear();

            List<SecurityTester> securities = _server.SecuritiesTester;

            if (securities != null && securities.Count != 0)
            {
                for (int i = 0; i < securities.Count; i++)
                {
                    DataGridViewRow nRow = new DataGridViewRow();
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[0].Value = securities[i].FileAdress;
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[1].Value = securities[i].Security.Name;
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[2].Value = securities[i].DataType;
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[3].Value = securities[i].Security.PriceStep;
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[4].Value = securities[i].TimeStart;
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[5].Value = securities[i].TimeEnd;

                    _myGridView.Rows.Add(nRow);
                }
            }

            TextBoxFrom.Text = _server.TimeStart.ToString(new CultureInfo("RU-ru"));
            TextBoxTo.Text = _server.TimeEnd.ToString(new CultureInfo("RU-ru"));

            SliderFrom.Minimum = (_server.TimeMin - DateTime.MinValue).TotalMinutes;
            SliderFrom.Maximum = (_server.TimeMax - DateTime.MinValue).TotalMinutes;
            SliderFrom.Value = (_server.TimeStart - DateTime.MinValue).TotalMinutes;

            SliderTo.Minimum = (_server.TimeMin - DateTime.MinValue).TotalMinutes;
            SliderTo.Maximum = (_server.TimeMax - DateTime.MinValue).TotalMinutes;
            SliderTo.Value = (_server.TimeMin - DateTime.MinValue).TotalMinutes;

            SliderFrom.ValueChanged += SliderFrom_ValueChanged;
            SliderTo.ValueChanged += SliderTo_ValueChanged;
        }

        /// <summary>
        /// двойной клик по таблице с инструментами
        /// </summary>
        void _myGridView_DoubleClick(object sender, EventArgs e)
        {
            DataGridViewRow row = null;
            try
            {
                row = _myGridView.SelectedRows[0];
            }
            catch (Exception)
            {
                // ignore
            }

            if (row == null)
            {
                return;
            }

            string str = row.Cells[1].Value.ToString();

            Security security = _server.Securities.Find(s=> s.Name == str);

            if (security == null)
            {
                return;
            }

            SecurityUi ui = new SecurityUi(security);
            ui.ShowDialog();

            if (ui.IsChanged)
            {
                _server.SaveSecurityDopSettings(security);
                _server.ReloadSecurities();
            }
        }


        // слайдеры. Установка начального и конечного времени тестирования

        private void SliderTo_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            TextBoxTo.TextChanged -= TextBoxTo_TextChanged;

            DateTime to = DateTime.MinValue.AddMinutes(SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value);
            _server.TimeEnd = to;
            TextBoxTo.Text = to.ToString(new CultureInfo("RU-ru"));

            if (SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value < SliderFrom.Value)
            {
                SliderFrom.Value = SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value;
            }
            TextBoxTo.TextChanged += TextBoxTo_TextChanged;
        }

        void SliderFrom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            TextBoxFrom.TextChanged -= TextBoxFrom_TextChanged;

            DateTime from = DateTime.MinValue.AddMinutes(SliderFrom.Value);
            _server.TimeStart = from;
            TextBoxFrom.Text = from.ToString(new CultureInfo("RU-ru"));

            if (SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value < SliderFrom.Value)
            {
                SliderTo.Value = SliderFrom.Minimum + SliderFrom.Maximum - SliderFrom.Value;
            }

            TextBoxFrom.TextChanged += TextBoxFrom_TextChanged;
        }

        void TextBoxTo_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            DateTime to;
            try
            {
                to = Convert.ToDateTime(TextBoxTo.Text);

                if (to < _server.TimeMin ||
                    to > _server.TimeMax)
                {
                    throw new Exception();
                }
            }
            catch (Exception)
            {
                TextBoxTo.Text = _server.TimeEnd.ToString(new CultureInfo("RU-ru"));
                return;
            }

            _server.TimeEnd = to;
            // SliderTo.Value = SliderFrom.Minimum + SliderFrom.Maximum - to.Minute;
            // SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value
            SliderTo.Value = SliderFrom.Minimum + SliderTo.Maximum - (to - DateTime.MinValue).TotalMinutes;
        }

        void TextBoxFrom_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            DateTime from;
            try
            {
                from = Convert.ToDateTime(TextBoxFrom.Text);

                if (from < _server.TimeMin ||
                    from > _server.TimeMax)
                {
                    throw new Exception();
                }
            }
            catch (Exception)
            {
                TextBoxFrom.Text = _server.TimeStart.ToString(new CultureInfo("RU-ru"));
                return;
            }

            _server.TimeStart = from;
            SliderFrom.Value = (_server.TimeStart - DateTime.MinValue).TotalMinutes;
        }

        private void ButtonSetDataFromPath_Click(object sender, RoutedEventArgs e)
        {
            _server.ShowPathSenderDialog();
            TextBoxDataPath.Text = _server.PathToFolder;
        }

    }
}
