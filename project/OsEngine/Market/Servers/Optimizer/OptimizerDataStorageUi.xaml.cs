/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

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
            _log = log;

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

            // clearing

            CreateClearingGrid();
            PaintClearingGrid();

            // non trade periods

            CreateNonTradePeriodsGrid();
            PaintNonTradePeriodsGrid();

            // testing data/данные для тестирования

            ComboBoxDataType.Items.Add(TesterDataType.Candle);
            ComboBoxDataType.Items.Add(TesterDataType.TickAllCandleState);
            ComboBoxDataType.Items.Add(TesterDataType.TickOnlyReadyCandle);
            //ComboBoxDataType.Items.Add(TesterDataType.MarketDepthOnlyReadyCandle);
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
            ComboBoxDataSourceType.Items.Add(TesterSourceDataType.Folder);
            ComboBoxDataSourceType.Items.Add(TesterSourceDataType.Set);
            ComboBoxDataSourceType.SelectedItem = _server.SourceDataType;
            ComboBoxDataSourceType.SelectionChanged += ComboBoxDataSourceType_SelectionChanged;

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

            if (_server != null)
            {
                _server.SecuritiesChangeEvent -= _server_SecuritiesChangeEvent;
                _server = null;
            }

            if (_myGridView != null)
            {
                DataGridFactory.ClearLinks(_myGridView);
                _myGridView.DoubleClick -= _myGridView_DoubleClick;
                _myGridView.CellValueChanged -= _myGridView_CellValueChanged;
                _myGridView.DataError -= _myGridView_DataError;
                HostSecurities.Child = null;
                _myGridView = null;
            }

            if (_gridNonTradePeriods != null)
            {
                HostNonTradePeriods.Child = null;
                DataGridFactory.ClearLinks(_gridNonTradePeriods);
                _gridNonTradePeriods.CellValueChanged -= _gridNonTradePeriods_CellValueChanged;
                _gridNonTradePeriods.CellClick -= _gridNonTradePeriods_CellClick;
                _gridNonTradePeriods.DataError -= _myGridView_DataError;
                _gridNonTradePeriods = null;
            }

            if (_gridClearing != null)
            {
                HostClearing.Child = null;
                DataGridFactory.ClearLinks(_gridClearing);
                _gridClearing.CellClick -= _gridClearing_CellClick;
                _gridClearing.CellValueChanged -= _gridClearing_CellValueChanged;
                _gridClearing.DataError -= _myGridView_DataError;
                _gridClearing = null;
            }

            _log.StopPaint();
            Host.Child = null;
            _log = null;
        }

        private CultureInfo _currentCulture;

        private Log _log;

        #region Data selection

        private void ComboBoxDataSourceType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                TesterSourceDataType sourceDataType;
                Enum.TryParse(ComboBoxDataSourceType.SelectedItem.ToString(), out sourceDataType);
                _server.SourceDataType = sourceDataType;
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ComboBoxDataType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                TesterDataType type;
                Enum.TryParse(ComboBoxDataType.SelectedItem.ToString(), out type);
                _server.TypeTesterData = type;
                _server.Save();

                PaintGrid();
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ComboBoxSets_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                _server.SetNewSet(ComboBoxSets.SelectedItem.ToString());
                PaintGrid();
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonSetDataFromPath_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _server.ShowPathSenderDialog();
                TextBoxDataPath.Text = _server.PathToFolder;
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Server

        private OptimizerDataStorage _server;

        private OptimizerMaster _master;

        private void _server_SecuritiesChangeEvent(List<Security> securities)
        {
            PaintGrid();
        }

        #endregion

        #region Securities table

        private DataGridView _myGridView;

        private void CreateGrid()
        {
            _myGridView = DataGridFactory.GetDataGridDataSource();

            _myGridView.DoubleClick += _myGridView_DoubleClick;
            _myGridView.CellValueChanged += _myGridView_CellValueChanged;
            _myGridView.DataError += _myGridView_DataError;
            HostSecurities.Child = _myGridView;
            HostSecurities.Child.Show();
            _myGridView.Rows.Add();
        }

        private void _myGridView_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            _master.SendLogMessage(e.ToString(), LogMessageType.Error);
        }

        private void PaintGrid()
        {
            try
            {
                if (_myGridView.InvokeRequired)
                {
                    _myGridView.Invoke(new Action(PaintGrid));
                    return;
                }

                int displayedRow = _myGridView.FirstDisplayedScrollingRowIndex;

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

                if (displayedRow > 0
                    && displayedRow < _myGridView.Rows.Count)
                {
                    _myGridView.FirstDisplayedScrollingRowIndex = displayedRow;
                }
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _myGridView_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
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

                _server.SaveSetSecuritiesTimeFrameSettings();
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _myGridView_DoubleClick(object sender, EventArgs e)
        {
            try
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
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Slippage and orders execution settings

        private void CheckBoxSlippageLimitOff_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TextBoxSlippageSimpleOrder.Text = "0";
                TextBoxSlippageSimpleOrder.IsEnabled = false;
                CheckBoxSlippageLimitOn.IsChecked = false;
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void CheckBoxSlippageLimitOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TextBoxSlippageSimpleOrder.IsEnabled = true;
                CheckBoxSlippageLimitOff.IsChecked = false;
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void CheckBoxSlippageStopOff_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TextBoxSlippageStop.Text = "0";
                TextBoxSlippageStop.IsEnabled = false;
                CheckBoxSlippageStopOn.IsChecked = false;
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void CheckBoxSlippageStopOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TextBoxSlippageStop.IsEnabled = true;
                CheckBoxSlippageStopOff.IsChecked = false;
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
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
            catch
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
            catch
            {
                TextBoxSlippageStop.Text = _master.SlippageToStopOrder.ToString(new CultureInfo("ru-RU"));
                // ignore
            }
        }

        #endregion

        #region Clearing

        private DataGridView _gridClearing;

        public void CreateClearingGrid()
        {
            _gridClearing = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridClearing.DefaultCellStyle;

            _gridClearing.ScrollBars = ScrollBars.Vertical;

            // Num
            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = OsLocalization.Market.Label157;
            column2.ReadOnly = true;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _gridClearing.Columns.Add(column2);

            // Time
            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = OsLocalization.Market.Label152;
            column3.ReadOnly = false;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridClearing.Columns.Add(column3);

            // OnOff
            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = OsLocalization.Market.Label153;
            column4.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridClearing.Columns.Add(column4);

            // Button Add or Delete
            DataGridViewColumn column5 = new DataGridViewColumn();
            column5.CellTemplate = cell0;
            //column5.HeaderText = "Button";
            column5.ReadOnly = true;
            column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _gridClearing.Columns.Add(column5);

            HostClearing.Child = _gridClearing;
            _gridClearing.CellClick += _gridClearing_CellClick;
            _gridClearing.CellValueChanged += _gridClearing_CellValueChanged;
            _gridClearing.DataError += _myGridView_DataError;
        }

        public void PaintClearingGrid()
        {
            try
            {
                if (_gridClearing.InvokeRequired)
                {
                    _gridClearing.Invoke(new Action(PaintClearingGrid));
                    return;
                }

                _gridClearing.CellValueChanged -= _gridClearing_CellValueChanged;

                _gridClearing.Rows.Clear();

                for (int i = 0; i < _master.ClearingTimes.Count; i++)
                {
                    _gridClearing.Rows.Add(GetClearingRow(_master.ClearingTimes[i], i + 1));
                }

                _gridClearing.Rows.Add(GetClearingLastRow());

                _gridClearing.CellValueChanged += _gridClearing_CellValueChanged;
            }
            catch (Exception error)
            {
                try
                {
                    _master.SendLogMessage(error.ToString(), LogMessageType.Error);
                }
                catch
                {
                    // ignore
                }
            }
        }

        private DataGridViewRow GetClearingLastRow()
        {
            DataGridViewRow nRow = new DataGridViewRow();
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());

            nRow.Cells.Add(new DataGridViewButtonCell());
            nRow.Cells[3].Value = OsLocalization.Market.Label156;

            return nRow;
        }

        private DataGridViewRow GetClearingRow(OrderClearing clearing, int num)
        {
            DataGridViewRow nRow = new DataGridViewRow();
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = num.ToString();

            string timeOfDay = clearing.Time.Hour.ToString();

            if (timeOfDay.Length == 1)
            {
                timeOfDay = "0" + timeOfDay;
            }

            timeOfDay += ":";
            string minute = clearing.Time.Minute.ToString();

            if (minute.Length == 1)
            {
                minute = "0" + minute;
            }
            timeOfDay += minute;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = timeOfDay;

            DataGridViewCheckBoxCell checkBox = new DataGridViewCheckBoxCell();
            checkBox.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            checkBox.Value = clearing.IsOn;

            nRow.Cells.Add(checkBox);

            nRow.Cells.Add(new DataGridViewButtonCell());
            nRow.Cells[3].Value = OsLocalization.Market.Label47;

            return nRow;
        }

        private void _gridClearing_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int row = e.RowIndex;
                int column = e.ColumnIndex;

                if (row > _master.ClearingTimes.Count)
                {
                    return;
                }

                if (column == 3)
                {
                    if (row == _master.ClearingTimes.Count)
                    {// Создание нового клиринга
                        _master.CreateNewClearing();
                        PaintClearingGrid();
                    }
                    else
                    {// Удаление клиринга

                        AcceptDialogUi ui = new AcceptDialogUi("Are you sure you want to remove the clearing?");

                        ui.ShowDialog();

                        if (ui.UserAcceptAction == false)
                        {
                            return;
                        }

                        _master.RemoveClearing(row);
                        PaintClearingGrid();
                    }
                }
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _gridClearing_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int row = e.RowIndex;
                int column = e.ColumnIndex;

                if (column == 1)
                { // Изменилось время клиринга

                    string value = _gridClearing.Rows[row].Cells[column].Value.ToString();

                    // "19:05"

                    if (value.Length != 5
                        || value.Contains(":") == false)
                    {
                        return;
                    }

                    string[] values = value.Split(':');

                    int hour = int.Parse(values[0]);
                    int minute = int.Parse(values[1]);

                    _master.ClearingTimes[row].Time = new DateTime(2022, 1, 1, hour, minute, 0);
                    _master.SaveClearingInfo();
                }
                else if (column == 2)
                { // Изменилось состояние вкл/выкл
                    string value = _gridClearing.Rows[row].Cells[column].Value.ToString();

                    if (value == "True")
                    {
                        _master.ClearingTimes[row].IsOn = true;
                        _master.SaveClearingInfo();
                    }
                    else if (value == "False")
                    {
                        _master.ClearingTimes[row].IsOn = false;
                        _master.SaveClearingInfo();
                    }
                }
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Non-trade periods

        private DataGridView _gridNonTradePeriods;

        public void CreateNonTradePeriodsGrid()
        {
            _gridNonTradePeriods = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridNonTradePeriods.DefaultCellStyle;

            _gridNonTradePeriods.ScrollBars = ScrollBars.Vertical;

            // Name
            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = OsLocalization.Market.Label157;
            column2.ReadOnly = false;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _gridNonTradePeriods.Columns.Add(column2);

            // Date start
            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = OsLocalization.Market.Label154;
            column3.ReadOnly = false;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridNonTradePeriods.Columns.Add(column3);

            // Date end
            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = OsLocalization.Market.Label155;
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridNonTradePeriods.Columns.Add(column4);

            // OnOff
            DataGridViewColumn column5 = new DataGridViewColumn();
            column5.CellTemplate = cell0;
            column5.HeaderText = OsLocalization.Market.Label153;
            column4.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            column5.ReadOnly = false;
            column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridNonTradePeriods.Columns.Add(column5);

            // Button Add or Delete
            DataGridViewColumn column6 = new DataGridViewColumn();
            column6.CellTemplate = cell0;
            //column6.HeaderText = "Button";
            column6.ReadOnly = true;
            column6.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _gridNonTradePeriods.Columns.Add(column6);

            HostNonTradePeriods.Child = _gridNonTradePeriods;
            _gridNonTradePeriods.CellValueChanged += _gridNonTradePeriods_CellValueChanged;
            _gridNonTradePeriods.CellClick += _gridNonTradePeriods_CellClick;
            _gridNonTradePeriods.DataError += _myGridView_DataError;
        }

        public void PaintNonTradePeriodsGrid()
        {
            try
            {
                if (_gridNonTradePeriods.InvokeRequired)
                {
                    _gridNonTradePeriods.Invoke(new Action(PaintNonTradePeriodsGrid));
                    return;
                }

                _gridNonTradePeriods.CellValueChanged -= _gridNonTradePeriods_CellValueChanged;

                _gridNonTradePeriods.Rows.Clear();

                for (int i = 0; i < _master.NonTradePeriods.Count; i++)
                {
                    _gridNonTradePeriods.Rows.Add(GetNonTradePeriodsRow(_master.NonTradePeriods[i], i + 1));
                }

                _gridNonTradePeriods.Rows.Add(GetNonTradePeriodsLastRow());

                _gridNonTradePeriods.CellValueChanged += _gridNonTradePeriods_CellValueChanged;
            }
            catch (Exception error)
            {
                try
                {
                    _master.SendLogMessage(error.ToString(), LogMessageType.Error);
                }
                catch
                {
                    // ignore
                }
            }
        }

        private DataGridViewRow GetNonTradePeriodsLastRow()
        {
            DataGridViewRow nRow = new DataGridViewRow();
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());

            nRow.Cells.Add(new DataGridViewButtonCell());
            nRow.Cells[4].Value = OsLocalization.Market.Label156;

            return nRow;
        }

        private DataGridViewRow GetNonTradePeriodsRow(NonTradePeriod period, int num)
        {
            DataGridViewRow nRow = new DataGridViewRow();
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = num.ToString();

            nRow.Cells.Add(new DataGridViewTextBoxCell());

            string dateStart = period.DateStart.Date.ToString(OsLocalization.CurCulture);
            dateStart = dateStart.Split(' ')[0];

            nRow.Cells[1].Value = dateStart;

            string dateEnd = period.DateEnd.Date.ToString(OsLocalization.CurCulture);
            dateEnd = dateEnd.Split(' ')[0];

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[2].Value = dateEnd;

            DataGridViewCheckBoxCell checkBox = new DataGridViewCheckBoxCell();
            checkBox.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            checkBox.Value = period.IsOn;

            nRow.Cells.Add(checkBox);

            nRow.Cells.Add(new DataGridViewButtonCell());
            nRow.Cells[4].Value = OsLocalization.Market.Label47;

            return nRow;
        }

        private void _gridNonTradePeriods_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int row = e.RowIndex;
                int column = e.ColumnIndex;

                if (row > _master.NonTradePeriods.Count)
                {
                    return;
                }

                if (column == 4)
                {
                    if (row == _master.NonTradePeriods.Count)
                    {// Создание нового периода
                        _master.CreateNewNonTradePeriod();
                        PaintNonTradePeriodsGrid();
                    }
                    else
                    {// Удаление периода

                        AcceptDialogUi ui = new AcceptDialogUi("Are you sure you want to remove the non trade period?");

                        ui.ShowDialog();

                        if (ui.UserAcceptAction == false)
                        {
                            return;
                        }

                        _master.RemoveNonTradePeriod(row);
                        PaintNonTradePeriodsGrid();
                    }
                }
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _gridNonTradePeriods_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int row = e.RowIndex;
                int column = e.ColumnIndex;

                if (column == 1)
                { // Изменилось время старта периода
                    string value = _gridNonTradePeriods.Rows[row].Cells[column].Value.ToString();

                    DateTime time = DateTime.MinValue;

                    try
                    {
                        time = Convert.ToDateTime(value, OsLocalization.CurCulture);
                    }
                    catch
                    {
                        return;
                    }

                    _master.NonTradePeriods[row].DateStart = time;
                    _master.SaveNonTradePeriods();
                }
                else if (column == 2)
                { // Изменилось время конца периода
                    string value = _gridNonTradePeriods.Rows[row].Cells[column].Value.ToString();

                    DateTime time = DateTime.MinValue;

                    try
                    {
                        time = Convert.ToDateTime(value, OsLocalization.CurCulture);
                    }
                    catch
                    {
                        return;
                    }

                    _master.NonTradePeriods[row].DateEnd = time;
                    _master.SaveNonTradePeriods();


                }
                else if (column == 3)
                { // Изменилось состояние вкл/выкл
                    string value = _gridNonTradePeriods.Rows[row].Cells[column].Value.ToString();

                    if (value == "True")
                    {
                        _master.NonTradePeriods[row].IsOn = true;
                        _master.SaveNonTradePeriods();
                    }
                    else if (value == "False")
                    {
                        _master.NonTradePeriods[row].IsOn = false;
                        _master.SaveNonTradePeriods();
                    }
                }
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

    }
}