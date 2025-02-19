using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsOptimizer;

namespace OsEngine.Market.Servers.Optimizer
{
    public partial class OptimizerDataStorageUi
    {
        public OptimizerDataStorageUi(OptimizerDataStorage server, Log log, OptimizerMaster master)
        {
            InitializeComponent();
            _currentCulture = OsLocalization.CurCulture;
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _server = server;
            _master = master;

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

            ComboBoxOrderActivationType.Items.Add(OrderExecutionType.Touch.ToString());
            ComboBoxOrderActivationType.Items.Add(OrderExecutionType.Intersection.ToString());
            ComboBoxOrderActivationType.Items.Add(OrderExecutionType.FiftyFifty.ToString());
            ComboBoxOrderActivationType.SelectedItem = _master.OrderExecutionType.ToString();
            ComboBoxOrderActivationType.SelectionChanged += ComboBoxOrderActivationType_SelectionChanged;

            if (_master.SlippageToStopOrder == 0)
            {
                CheckBoxSlippageStopOff.IsChecked = true;
            }
            else
            {
                CheckBoxSlippageStopOn.IsChecked = true;
            }

            if (_master.SlippageToSimpleOrder == 0)
            {
                CheckBoxSlippageLimitOff.IsChecked = true;
            }
            else
            {
                CheckBoxSlippageLimitOn.IsChecked = true;
            }

            TextBoxDataPath.Text = _server.PathToFolder;
            ComboBoxDataSourseType.Items.Add(TesterSourceDataType.Folder);
            ComboBoxDataSourseType.Items.Add(TesterSourceDataType.Set);
            ComboBoxDataSourseType.SelectedItem = _server.SourceDataType;
            ComboBoxDataSourseType.SelectionChanged += ComboBoxDataSourceType_SelectionChanged;

            TextBoxSlippageSimpleOrder.Text = master.SlippageToSimpleOrder.ToString(new CultureInfo("ru-RU"));
            TextBoxSlippageSimpleOrder.TextChanged += TextBoxSlippageSimpleOrderTextChanged;

            TextBoxSlippageStop.Text = master.SlippageToStopOrder.ToString(new CultureInfo("ru-RU"));
            TextBoxSlippageStop.TextChanged += TextBoxSlippageStop_TextChanged;

            Title = OsLocalization.Optimizer.Label62;

            Label22.Header = OsLocalization.Market.Label22;
            Label23.Header = OsLocalization.Market.Label23;
            Label24.Content = OsLocalization.Market.Label24;
            Label25.Content = OsLocalization.Market.Label25;
            Label28.Content = OsLocalization.Market.Label28;
            ButtonSetDataFromPath.Content = OsLocalization.Market.ButtonSetFolder;

            Label30.Header = OsLocalization.Market.Label30;
            Label32.Content = OsLocalization.Market.Label32;
            Label33.Content = OsLocalization.Market.Label33;
            Label34.Content = OsLocalization.Market.Label34;
            CheckBoxSlippageLimitOff.Content = OsLocalization.Market.Label35;
            CheckBoxSlippageStopOff.Content = OsLocalization.Market.Label35;
            CheckBoxSlippageLimitOn.Content = OsLocalization.Market.Label36;
            CheckBoxSlippageStopOn.Content = OsLocalization.Market.Label36;
            LabelOrderActivationType.Content = OsLocalization.Market.Label148;
            LabelClearing.Content = OsLocalization.Market.Label150;
            LabelNonTradePeriod.Content = OsLocalization.Market.Label151;

            this.Activate();
            this.Focus();

            Closed += OptimizerDataStorageUi_Closed;
        }

        private void OptimizerDataStorageUi_Closed(object sender, EventArgs e)
        {
            _master = null;
            _server = null;

            DataGridFactory.ClearLinks(_myGridView);
            _myGridView.DoubleClick -= _myGridView_DoubleClick;
            _myGridView.CellValueChanged -= _myGridView_CellValueChanged;
            HostSecurities.Child = null;
            _myGridView = null;
        }

        private CultureInfo _currentCulture;

        void ComboBoxDataSourceType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            TesterSourceDataType sourceDataType;
            Enum.TryParse(ComboBoxDataSourseType.SelectedItem.ToString(), out sourceDataType);
            _server.SourceDataType = sourceDataType;
        }

        void ComboBoxDataType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            TesterDataType type;
            Enum.TryParse(ComboBoxDataType.SelectedItem.ToString(), out type);
            _server.TypeTesterData = type;
            _server.Save();

            PaintGrid();
        }

        void ComboBoxSets_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _server.SetNewSet(ComboBoxSets.SelectedItem.ToString());
            PaintGrid();
        }

		// server

        private OptimizerDataStorage _server;

        private OptimizerMaster _master;

        void _server_SecuritiesChangeEvent(List<Security> securities)
        {
            PaintGrid();
        }

		// table with instruments

        private DataGridView _myGridView;

        private void CreateGrid()
        {
            _myGridView = DataGridFactory.GetDataGridDataSource();

            _myGridView.DoubleClick += _myGridView_DoubleClick;
            _myGridView.CellValueChanged += _myGridView_CellValueChanged;
            HostSecurities.Child = _myGridView;
            HostSecurities.Child.Show();
            _myGridView.Rows.Add();
        }

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
                    nRow.Cells[0].Value = securities[i].FileAddress;
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

        private void CheckBoxSlippageLimitOff_Checked(object sender, RoutedEventArgs e)
        {
            TextBoxSlippageSimpleOrder.Text = "0";
            TextBoxSlippageSimpleOrder.IsEnabled = false;
            CheckBoxSlippageLimitOn.IsChecked = false;
        }

        private void CheckBoxSlippageLimitOn_Checked(object sender, RoutedEventArgs e)
        {
            TextBoxSlippageSimpleOrder.IsEnabled = true;
            CheckBoxSlippageLimitOff.IsChecked = false;
        }

        private void CheckBoxSlippageStopOff_Checked(object sender, RoutedEventArgs e)
        {
            TextBoxSlippageStop.Text = "0";
            TextBoxSlippageStop.IsEnabled = false;
            CheckBoxSlippageStopOn.IsChecked = false;
        }

        private void CheckBoxSlippageStopOn_Checked(object sender, RoutedEventArgs e)
        {
            TextBoxSlippageStop.IsEnabled = true;
            CheckBoxSlippageStopOff.IsChecked = false;
        }

        private void ComboBoxOrderActivationType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                OrderExecutionType type = OrderExecutionType.Intersection;

                if (Enum.TryParse(ComboBoxOrderActivationType.SelectedItem.ToString(), out type))
                {
                    _master.OrderExecutionType = type;
                }
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxSlippageSimpleOrderTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                _master.SlippageToSimpleOrder = Convert.ToInt32(TextBoxSlippageSimpleOrder.Text);
            }
            catch (Exception)
            {
                TextBoxSlippageSimpleOrder.Text = _master.SlippageToSimpleOrder.ToString(new CultureInfo("ru-RU"));
                // ignore
            }

        }

        private void TextBoxSlippageStop_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                _master.SlippageToStopOrder = Convert.ToInt32(TextBoxSlippageStop.Text);
            }
            catch (Exception)
            {
                TextBoxSlippageStop.Text = _master.SlippageToStopOrder.ToString(new CultureInfo("ru-RU"));
                // ignore
            }
        }

    }
}