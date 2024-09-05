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
	/// Interaction logic for TesterServerUi.xaml
    /// Логика взаимодействия для TesterServerUi.xaml
    /// </summary>
    public partial class OptimizerDataStorageUi
    {
        /// <summary>
		/// constructor
        /// конструктор
        /// </summary>
        /// <param name="server">server/сервер</param>
        /// <param name="log">log/лог</param>
        public OptimizerDataStorageUi(OptimizerDataStorage server, Log log)
        {
            InitializeComponent();
            _currentCulture = OsLocalization.CurCulture;
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _server = server;

            log.StartPaint(Host);

            _server.SecuritiesChangeEvent += _server_SecuritiesChangeEvent;

            CreateGrid();
            PaintGrid();

            // progress-bar/прогресс бар

            List<string> sets = _server.Sets;

            // sets/сеты

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

            // testing data/данные для тестирования

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

            Title = OsLocalization.Optimizer.Label62;

            Label22.Header = OsLocalization.Market.Label22;
            Label23.Header = OsLocalization.Market.Label23;
            Label24.Content = OsLocalization.Market.Label24;
            Label25.Content = OsLocalization.Market.Label25;
            Label28.Content = OsLocalization.Market.Label28;
            ButtonSetDataFromPath.Content = OsLocalization.Market.ButtonSetFolder;

            this.Activate();
            this.Focus();
        }

        private CultureInfo _currentCulture;

        /// <summary>
		/// data source has changed. Folder or set
        /// источник данных изменился. Папка или Сет 
        /// </summary>
        void ComboBoxDataSourseType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            TesterSourceDataType sourceDataType;
            Enum.TryParse(ComboBoxDataSourseType.SelectedItem.ToString(), out sourceDataType);
            _server.SourceDataType = sourceDataType;
        }

        /// <summary>
		/// data type has changed
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
		/// data set has changed
        /// сет данных изменился
        /// </summary>
        void ComboBoxSets_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _server.SetNewSet(ComboBoxSets.SelectedItem.ToString());
            PaintGrid();
        }

		// server
        // сервер

        /// <summary>
		/// test server
        /// тестовый сервер
        /// </summary>
        private OptimizerDataStorage _server;

        /// <summary>
		/// server instruments have changed
        /// изменились инструменты в сервере
        /// </summary>
        void _server_SecuritiesChangeEvent(List<Security> securities)
        {
            PaintGrid();
        }

		// table with instruments
        //  таблица с инструментами

        /// <summary>
		/// table with instruments
        /// таблица с инструментами
        /// </summary>
        private DataGridView _myGridView;

        /// <summary>
		/// create table with instruments
        /// создать таблицу с инструментами
        /// </summary>
        private void CreateGrid()
        {
            _myGridView = DataGridFactory.GetDataGridDataSource();

            _myGridView.DoubleClick += _myGridView_DoubleClick;
            _myGridView.CellValueChanged += _myGridView_CellValueChanged;
            HostSecurities.Child = _myGridView;
            HostSecurities.Child.Show();
            _myGridView.Rows.Add();
        }

        /// <summary>
		/// paint table with instruments
        /// прорисовать таблицу с инструментами
        /// </summary>
        private void PaintGrid()
        {
            if (_myGridView.InvokeRequired)
            {
                _myGridView.Invoke(new Action(PaintGrid));
                return;
            }

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
                   

                    if (securities[i].DataType == SecurityTesterDataType.Candle)
                    {
                        DataGridViewComboBoxCell comboBox = new DataGridViewComboBoxCell();

                        comboBox.Items.Add(TimeFrame.Day.ToString());
                        comboBox.Items.Add(TimeFrame.Hour1.ToString());
                        comboBox.Items.Add(TimeFrame.Hour2.ToString());
                        comboBox.Items.Add(TimeFrame.Hour4.ToString());
                        comboBox.Items.Add(TimeFrame.Min1.ToString());
                        comboBox.Items.Add(TimeFrame.Min2.ToString());
                        comboBox.Items.Add(TimeFrame.Min5.ToString());
                        comboBox.Items.Add(TimeFrame.Min3.ToString());
                        comboBox.Items.Add(TimeFrame.Min10.ToString());
                        comboBox.Items.Add(TimeFrame.Min15.ToString());
                        comboBox.Items.Add(TimeFrame.Min30.ToString());
                        comboBox.Items.Add(TimeFrame.Min45.ToString());
                        comboBox.Items.Add(TimeFrame.Sec1.ToString());
                        comboBox.Items.Add(TimeFrame.Sec2.ToString());
                        comboBox.Items.Add(TimeFrame.Sec5.ToString());
                        comboBox.Items.Add(TimeFrame.Sec10.ToString());
                        comboBox.Items.Add(TimeFrame.Sec15.ToString());
                        comboBox.Items.Add(TimeFrame.Sec20.ToString());
                        comboBox.Items.Add(TimeFrame.Sec30.ToString());


                        nRow.Cells.Add(comboBox);
                        nRow.Cells[2].Value = securities[i].TimeFrame.ToString();
                    }
                    else
                    {
                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells[2].Value = securities[i].DataType;
                    }

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[3].Value = securities[i].Security.PriceStep.ToStringWithNoEndZero();
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[4].Value = securities[i].TimeStart.ToString(_currentCulture);
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[5].Value = securities[i].TimeEnd.ToString(_currentCulture);

                    _myGridView.Rows.Add(nRow);
                }
            }
        }

        private void _myGridView_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            List<SecurityTester> securities = _server.SecuritiesTester;

            for (int i = 0; i < securities.Count && i < _myGridView.Rows.Count; i++)
            {
                TimeFrame frame;

                if (Enum.TryParse(_myGridView.Rows[i].Cells[2].Value.ToString(), out frame))
                {
                    securities[i].TimeFrame = frame;
                }
            }
        }

        /// <summary>
        /// double click on table with instruments
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

            Security security = _server.Securities.Find(s => s.Name == str);

            if (security == null)
            {
                return;
            }

            int rowNum = row.Index;

            SecurityUi ui = new SecurityUi(security);
            ui.ShowDialog();

            if (ui.IsChanged)
            {
                security = _server.SecuritiesTester[rowNum].Security;

                for (int i = 0; i < _server.SecuritiesTester.Count; i++)
                {
                    if (_server.SecuritiesTester[i].Security.Name == security.Name)
                    {
                        _server.SecuritiesTester[i].Security = security;
                    }
                }

                _server.SaveSecurityDopSettings(security);
            }

            PaintGrid();
        }

        private void ButtonSetDataFromPath_Click(object sender, RoutedEventArgs e)
        {
            _server.ShowPathSenderDialog();
            TextBoxDataPath.Text = _server.PathToFolder;
        }

    }
}
