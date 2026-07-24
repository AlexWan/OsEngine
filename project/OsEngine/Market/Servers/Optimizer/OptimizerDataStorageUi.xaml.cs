/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsOptimizer;
using OsEngine.Wiki;

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

            LabelTabItemAccrualsAndCharge.Header = OsLocalization.Market.LabelAccrualsAndCharges;
            LabelTabItemDividends.Header = OsLocalization.Market.LabelTabItemDividends;
            LabelDividendsPaymentTableHeader.Content = OsLocalization.Market.Label332;
            CheckBoxDividendsIsOn.Content = OsLocalization.Market.LabelDividendsIsOn;
            ButtonOpenDataBaseDividends.Content = OsLocalization.Market.Label337;
            ButtonDivsUpdateBase.Content = OsLocalization.Market.Label338;

            CheckBoxDividendsIsOn.IsChecked = _server.DividendsIsOn;
            CheckBoxDividendsIsOn.Checked += CheckBoxDividendsIsOn_Checked;
            CheckBoxDividendsIsOn.Unchecked += CheckBoxDividendsIsOn_Unchecked;

            LabelTabItemMargin.Header = OsLocalization.Market.LabelTabItemMargin;
            LabelTabItemTaxes.Header = OsLocalization.Market.LabelTabItemTaxes;
            LabelMarginRegime.Content = OsLocalization.Market.LabelMarginRegime;
            CheckBoxTaxesIsOn.Content = OsLocalization.Market.LabelDividendsIsOn;

            CreateMarginGrids();

            ComboBoxMarginRegime.Items.Add("Off");
            ComboBoxMarginRegime.Items.Add("Summ");
            ComboBoxMarginRegime.Items.Add("Percent");
            ComboBoxMarginRegime.SelectedItem = _server.MarginRegime;
            ComboBoxMarginRegime.SelectionChanged += ComboBoxMarginRegime_SelectionChanged;

            UpdateMarginSettingsGrid();

            CreateTaxGrids();

            CheckBoxTaxesIsOn.IsChecked = _server.TaxesIsOn;
            CheckBoxTaxesIsOn.Click += CheckBoxTaxesIsOn_Click;

            ButtonHelpDividends.Click += ButtonHelpDividends_Click;
            ButtonHelpMargin.Click += ButtonHelpMargin_Click;
            ButtonHelpTaxes.Click += ButtonHelpTaxes_Click;

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
            try
            {
                ComboBoxSets.SelectionChanged -= ComboBoxSets_SelectionChanged;
                ComboBoxDataType.SelectionChanged -= ComboBoxDataType_SelectionChanged;
                ComboBoxOrderActivationType.SelectionChanged -= ComboBoxOrderActivationType_SelectionChanged;
                ComboBoxDataSourceType.SelectionChanged -= ComboBoxDataSourceType_SelectionChanged;

                TextBoxSlippageSimpleOrder.TextChanged -= TextBoxSlippageSimpleOrderTextChanged;
                TextBoxSlippageStop.TextChanged -= TextBoxSlippageStop_TextChanged;

                CheckBoxSlippageLimitOff.Checked -= CheckBoxSlippageLimitOff_Checked;
                CheckBoxSlippageLimitOn.Checked -= CheckBoxSlippageLimitOn_Checked;
                CheckBoxSlippageStopOff.Checked -= CheckBoxSlippageStopOff_Checked;
                CheckBoxSlippageStopOn.Checked -= CheckBoxSlippageStopOn_Checked;

                ButtonSetDataFromPath.Click -= ButtonSetDataFromPath_Click;

                CheckBoxDividendsIsOn.Checked -= CheckBoxDividendsIsOn_Checked;
                CheckBoxDividendsIsOn.Unchecked -= CheckBoxDividendsIsOn_Unchecked;
                CheckBoxTaxesIsOn.Click -= CheckBoxTaxesIsOn_Click;
                ComboBoxMarginRegime.SelectionChanged -= ComboBoxMarginRegime_SelectionChanged;
                ButtonHelpDividends.Click -= ButtonHelpDividends_Click;
                ButtonHelpMargin.Click -= ButtonHelpMargin_Click;
                ButtonHelpTaxes.Click -= ButtonHelpTaxes_Click;

                DeleteMarginAndTaxGrids();

                if (_server != null)
                {
                    _server.SecuritiesChangeEvent -= _server_SecuritiesChangeEvent;
                }

                DeleteSecuritiesGrid();
                DeleteClearingGrid();
                DeleteNonTradePeriodsGrid();

                if (_log != null)
                {
                    _log.StopPaint();
                }
                Host.Child = null;

                _server = null;
                _master = null;
                _log = null;

                Closed -= OptimizerDataStorageUi_Closed;
            }
            catch (Exception ex)
            {
                _log?.ProcessMessage(ex.ToString(), LogMessageType.Error);
            }
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

                        AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Market.LabelAcceptRemoveClearing);

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

                        AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Market.LabelAcceptRemoveNonTradePeriod);

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

        private void DeleteSecuritiesGrid()
        {
            if (_myGridView == null)
            {
                return;
            }

            HostSecurities.Child = null;
            DataGridFactory.ClearLinks(_myGridView);
            _myGridView.DoubleClick -= _myGridView_DoubleClick;
            _myGridView.CellValueChanged -= _myGridView_CellValueChanged;
            _myGridView.DataError -= _myGridView_DataError;
            _myGridView.Rows.Clear();
            _myGridView.Columns.Clear();
            _myGridView.DataSource = null;
            _myGridView.Dispose();
            _myGridView = null;
        }

        private void DeleteClearingGrid()
        {
            if (_gridClearing == null)
            {
                return;
            }

            HostClearing.Child = null;
            DataGridFactory.ClearLinks(_gridClearing);
            _gridClearing.CellClick -= _gridClearing_CellClick;
            _gridClearing.CellValueChanged -= _gridClearing_CellValueChanged;
            _gridClearing.DataError -= _myGridView_DataError;
            _gridClearing.Rows.Clear();
            _gridClearing.Columns.Clear();
            _gridClearing.DataSource = null;
            _gridClearing.Dispose();
            _gridClearing = null;
        }

        private void DeleteNonTradePeriodsGrid()
        {
            if (_gridNonTradePeriods == null)
            {
                return;
            }

            HostNonTradePeriods.Child = null;
            DataGridFactory.ClearLinks(_gridNonTradePeriods);
            _gridNonTradePeriods.CellValueChanged -= _gridNonTradePeriods_CellValueChanged;
            _gridNonTradePeriods.CellClick -= _gridNonTradePeriods_CellClick;
            _gridNonTradePeriods.DataError -= _myGridView_DataError;
            _gridNonTradePeriods.Rows.Clear();
            _gridNonTradePeriods.Columns.Clear();
            _gridNonTradePeriods.DataSource = null;
            _gridNonTradePeriods.Dispose();
            _gridNonTradePeriods = null;
        }

        #endregion

        #region Dividends

        private void CheckBoxDividendsIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_server != null)
                {
                    _server.DividendsIsOn = true;
                }
            }
            catch (Exception ex)
            {
                _master?.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void CheckBoxDividendsIsOn_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_server != null)
                {
                    _server.DividendsIsOn = false;
                }
            }
            catch (Exception ex)
            {
                _master?.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonOpenDataBaseDividends_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string path = Path.Combine(baseDir, "Wiki", "Dividends");

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _master?.SendLogMessage($"ButtonOpenDataBaseDividends_Click error: {ex}", LogMessageType.Error);
            }
        }

        private async void ButtonDivsUpdateBase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (WikiMaster.IsUpdating)
                {
                    return;
                }

                AcceptDialogUi dialog = new AcceptDialogUi(OsLocalization.Market.Label339);
                dialog.ShowDialog();

                if (!dialog.UserAcceptAction)
                {
                    return;
                }

                ButtonDivsUpdateBase.IsEnabled = false;

                await Task.Run(() => WikiMaster.UpdateDividendsBase());

                if (IsLoaded)
                {
                    ButtonDivsUpdateBase.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                if (IsLoaded)
                {
                    ButtonDivsUpdateBase.IsEnabled = true;
                }

                _master?.SendLogMessage($"ButtonDivsUpdateBase_Click error: {ex}", LogMessageType.Error);
            }
        }

        #endregion

        #region Help buttons

        private void ButtonHelpDividends_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CustomMessageBoxUi boxUi = new CustomMessageBoxUi(OsLocalization.Market.LabelHelpDividends);
                boxUi.ShowDialog();
            }
            catch (Exception ex)
            {
                _master?.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonHelpMargin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CustomMessageBoxUi boxUi = new CustomMessageBoxUi(OsLocalization.Market.LabelHelpMargin);
                boxUi.ShowDialog();
            }
            catch (Exception ex)
            {
                _master?.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonHelpTaxes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CustomMessageBoxUi boxUi = new CustomMessageBoxUi(OsLocalization.Market.LabelHelpTaxes);
                boxUi.ShowDialog();
            }
            catch (Exception ex)
            {
                _master?.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Margin

        private DataGridView _gridMarginSumm;
        private YearRateGridEditor _marginPercentEditor;
        private string _marginSettingsGridRegime = "Percent";

        private void ComboBoxMarginRegime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (ComboBoxMarginRegime.SelectedItem == null)
                {
                    return;
                }

                _server.MarginRegime = ComboBoxMarginRegime.SelectedItem.ToString();
                UpdateMarginSettingsGrid();
            }
            catch (Exception ex)
            {
                _master?.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateMarginSettingsGrid()
        {
            try
            {
                // таблица настроек видна всегда, в режиме Off — по последнему рабочему режиму
                if (_server.MarginRegime == "Summ"
                    || _server.MarginRegime == "Percent")
                {
                    _marginSettingsGridRegime = _server.MarginRegime;
                }

                if (_marginSettingsGridRegime == "Summ")
                {
                    HostMarginSettings.Child = _gridMarginSumm;
                }
                else
                {
                    HostMarginSettings.Child = _marginPercentEditor.Grid;
                }
            }
            catch (Exception ex)
            {
                _master?.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void CreateMarginGrids()
        {
            _gridMarginSumm = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);
            _gridMarginSumm.Dock = DockStyle.Fill;
            _gridMarginSumm.ScrollBars = ScrollBars.Vertical;
            _gridMarginSumm.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            _gridMarginSumm.ColumnCount = 3;
            _gridMarginSumm.RowCount = 31;
            _gridMarginSumm.Columns[0].HeaderText = "#";
            _gridMarginSumm.Columns[1].HeaderText = OsLocalization.ConvertToLocString("Eng:Year_" + "Ru:Год_");
            _gridMarginSumm.Columns[2].HeaderText = "";
            _gridMarginSumm.Columns[0].ReadOnly = true;
            _gridMarginSumm.Columns[1].ReadOnly = true;
            _gridMarginSumm.Columns[2].ReadOnly = true;
            _gridMarginSumm.Columns[0].FillWeight = 10;
            _gridMarginSumm.Columns[1].FillWeight = 45;
            _gridMarginSumm.Columns[2].FillWeight = 45;

            foreach (DataGridViewColumn column in _gridMarginSumm.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }

            for (int i = 0; i < _gridMarginSumm.Rows.Count; i++)
            {
                DataGridViewButtonCell buttonCell = new DataGridViewButtonCell();
                _gridMarginSumm.Rows[i].Cells[2] = buttonCell;

                _gridMarginSumm.Rows[i].Cells[0].Value = i + 1;
                _gridMarginSumm.Rows[i].Cells[1].Value = 2000 + i;
                _gridMarginSumm.Rows[i].Cells[2].Value = OsLocalization.ConvertToLocString("Eng:Settings_Ru:Настроить_");
            }

            _gridMarginSumm.CellClick += _gridMarginSumm_CellClick;
            _gridMarginSumm.DataError += _grid_DataError;

            _marginPercentEditor = new YearRateGridEditor(_server.GetMarginTablePercent, _server.SetMarginTablePercent);
        }

        private void _gridMarginSumm_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.ColumnIndex != 2
                    || e.RowIndex < 0)
                {
                    return;
                }

                int year = 0;
                int.TryParse(_gridMarginSumm.Rows[e.RowIndex].Cells[1].Value?.ToString(), out year);

                if (year == 0)
                {
                    return;
                }

                Dictionary<int, List<ListTableSumm>> table = _server.GetMarginTableSumm();

                if (table == null
                    || table.ContainsKey(year) == false)
                {
                    return;
                }

                OptimizerMarginRatesEditUi window = new OptimizerMarginRatesEditUi(_server, table[year], year);
            }
            catch (Exception ex)
            {
                _master?.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Taxes

        private YearRateGridEditor _taxRateEditor;

        private void CheckBoxTaxesIsOn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _server.TaxesIsOn = CheckBoxTaxesIsOn.IsChecked.Value;
            }
            catch (Exception ex)
            {
                _master?.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void CreateTaxGrids()
        {
            _taxRateEditor = new YearRateGridEditor(_server.GetTaxTable, _server.SetTaxTable);
            HostTaxSettings.Child = _taxRateEditor.Grid;
        }

        #endregion

        #region Margin and taxes grids service

        private void _grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            _master?.SendLogMessage(e.ToString(), LogMessageType.Error);
        }

        private void DeleteMarginAndTaxGrids()
        {
            try
            {
                if (_gridMarginSumm != null)
                {
                    _gridMarginSumm.CellClick -= _gridMarginSumm_CellClick;
                    _gridMarginSumm.DataError -= _grid_DataError;
                    DataGridFactory.ClearLinks(_gridMarginSumm);
                    _gridMarginSumm.Rows.Clear();
                    _gridMarginSumm.Columns.Clear();
                    _gridMarginSumm.DataSource = null;
                    _gridMarginSumm.Dispose();
                    _gridMarginSumm = null;
                }

                _marginPercentEditor?.Dispose();
                _marginPercentEditor = null;

                _taxRateEditor?.Dispose();
                _taxRateEditor = null;

                HostMarginSettings.Child = null;
                HostTaxSettings.Child = null;
            }
            catch (Exception ex)
            {
                _master?.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Year rate grid editor

        private class YearRateGridEditor
        {
            public DataGridView Grid { get; private set; }

            private List<ListTablePeriods> _listTable;
            private Func<List<ListTablePeriods>> _getTable;
            private Action<List<ListTablePeriods>> _saveTable;

            public YearRateGridEditor(Func<List<ListTablePeriods>> getTable, Action<List<ListTablePeriods>> saveTable)
            {
                _getTable = getTable;
                _saveTable = saveTable;

                CreateGrid();
                LoadTable();
            }

            private void CreateGrid()
            {
                Grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.AllCells);

                Grid.Dock = DockStyle.Fill;
                Grid.ScrollBars = ScrollBars.Vertical;
                Grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;

                Grid.ColumnCount = 4;
                Grid.RowCount = 1;

                Grid.Columns[0].HeaderText = "#";
                Grid.Columns[1].HeaderText = OsLocalization.ConvertToLocString("Eng:Year_" + "Ru:Год_");
                Grid.Columns[2].HeaderText = OsLocalization.ConvertToLocString("Eng:Rate_" + "Ru:Ставка_");
                Grid.Columns[3].HeaderText = "";

                Grid.Columns[0].ReadOnly = true;
                Grid.Columns[0].FillWeight = 10;
                Grid.Columns[1].FillWeight = 35;
                Grid.Columns[2].FillWeight = 35;
                Grid.Columns[3].FillWeight = 20;

                DataGridViewButtonCell cellButton = new DataGridViewButtonCell();

                Grid.Rows[Grid.RowCount - 1].Cells[0] = cellButton;
                Grid.Rows[Grid.RowCount - 1].Cells[0].Value = OsLocalization.ConvertToLocString("Eng:Add row_" + "Ru:Добавить строку_");
                Grid.Rows[Grid.RowCount - 1].Cells[1].ReadOnly = true;
                Grid.Rows[Grid.RowCount - 1].Cells[2].ReadOnly = true;
                Grid.Rows[Grid.RowCount - 1].Cells[3].ReadOnly = true;

                foreach (DataGridViewColumn column in Grid.Columns)
                {
                    column.SortMode = DataGridViewColumnSortMode.NotSortable;
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                }

                Grid.CellClick += Grid_CellClick;
                Grid.CellValueChanged += Grid_CellValueChanged;
                Grid.DataError += Grid_DataError;
            }

            private void Grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
            {
                ServerMaster.SendNewLogMessage(e.ToString(), LogMessageType.Error);
            }

            private void Grid_CellClick(object sender, DataGridViewCellEventArgs e)
            {
                try
                {
                    if (e.RowIndex == Grid.RowCount - 1 && e.ColumnIndex == 0)
                    {
                        AddRow();
                    }

                    if (e.ColumnIndex == 3)
                    {
                        if (e.RowIndex > -1 && e.RowIndex < Grid.RowCount - 1)
                        {
                            DeleteRow(e.RowIndex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }

            private void AddRow()
            {
                DataGridViewButtonCell cellButton = new DataGridViewButtonCell();
                Grid.Rows.Insert(Grid.RowCount - 1);

                Grid.Rows[Grid.RowCount - 2].Cells[3] = cellButton;
                Grid.Rows[Grid.RowCount - 2].Cells[3].Value = OsLocalization.ConvertToLocString("Eng:Delete row_" + "Ru:Удалить строку_");
                Grid.Rows[Grid.RowCount - 2].Cells[3].ReadOnly = true;

                RenumberRows();
            }

            private void DeleteRow(int rowIndex)
            {
                int year = 0;

                if (int.TryParse(Grid[1, rowIndex].Value?.ToString(), out year))
                {
                    int deleteIndex = _listTable.FindIndex(x => x.Year == year);

                    if (deleteIndex > -1)
                    {
                        _listTable.RemoveAt(deleteIndex);
                    }
                }

                Grid.Rows.RemoveAt(rowIndex);

                SaveTable();

                RenumberRows();
            }

            private void RenumberRows()
            {
                for (int i = 0; i < Grid.RowCount - 1; i++)
                {
                    Grid.Rows[i].Cells[0].Value = i + 1;
                }
            }

            private void Grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
            {
                try
                {
                    if (e.RowIndex < 0
                        || e.RowIndex == Grid.RowCount - 1
                        || e.ColumnIndex == 0
                        || e.ColumnIndex == 3)
                    {
                        return;
                    }

                    int year = 0;
                    int.TryParse(Grid.Rows[e.RowIndex].Cells[1].Value?.ToString(), out year);

                    if (year == 0)
                    {
                        return;
                    }

                    decimal rate = 0;
                    decimal.TryParse(Grid.Rows[e.RowIndex].Cells[2].Value?.ToString().Replace(".", ","), out rate);

                    ListTablePeriods list = new ListTablePeriods();
                    list.Year = year;
                    list.Rate = rate;

                    for (int i = 0; i < Grid.RowCount - 1; i++)
                    {
                        int valueYear = 0;
                        int.TryParse(Grid.Rows[i].Cells[1].Value?.ToString(), out valueYear);

                        if (valueYear == year)
                        {
                            if (i == e.RowIndex)
                            {
                                int index = _listTable.FindIndex(x => x.Year == year);

                                if (index > -1)
                                {
                                    _listTable[index] = list;
                                }
                                else
                                {
                                    _listTable.Add(list);
                                }

                                SaveTable();
                            }
                            else
                            {
                                Grid.Rows[e.RowIndex].Cells[1].Value = "";

                                string message = OsLocalization.ConvertToLocString("Eng:There is already such a year in the table._" + "Ru:В таблице уже есть такой год._");
                                ServerMaster.SendNewLogMessage(message, LogMessageType.Error);
                            }
                        }
                    }

                    for (int i = 0; i < _listTable.Count; i++)
                    {
                        int count = 0;

                        for (int j = 0; j < Grid.RowCount - 1; j++)
                        {
                            int valueYear = 0;
                            int.TryParse(Grid.Rows[j].Cells[1].Value?.ToString(), out valueYear);

                            if (_listTable[i].Year == valueYear)
                            {
                                count++;
                                break;
                            }
                        }

                        if (count == 0)
                        {
                            _listTable.RemoveAt(i);
                            i--;
                            SaveTable();
                        }
                    }
                }
                catch (Exception ex)
                {
                    ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }

            private void LoadTable()
            {
                try
                {
                    _listTable = _getTable();

                    if (_listTable == null
                        || _listTable.Count == 0)
                    {
                        _listTable = new List<ListTablePeriods>();

                        for (int i = 0; i < 31; i++)
                        {
                            _listTable.Add(new ListTablePeriods() { Year = 2000 + i, Rate = 13 });
                        }

                        SaveTable();
                    }

                    for (int i = 0; i < _listTable.Count; i++)
                    {
                        DataGridViewRow row = new DataGridViewRow();
                        row.Cells.Add(new DataGridViewTextBoxCell() { Value = i + 1 });
                        row.Cells.Add(new DataGridViewTextBoxCell() { Value = _listTable[i].Year });
                        row.Cells.Add(new DataGridViewTextBoxCell() { Value = _listTable[i].Rate });
                        row.Cells.Add(new DataGridViewButtonCell() { Value = OsLocalization.ConvertToLocString("Eng:Delete row_" + "Ru:Удалить строку_") });

                        Grid.Rows.Insert(Grid.RowCount - 1, row);
                    }
                }
                catch (Exception ex)
                {
                    ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }

            private void SaveTable()
            {
                try
                {
                    _saveTable(_listTable);
                }
                catch (Exception ex)
                {
                    ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }

            public void Dispose()
            {
                try
                {
                    if (Grid != null)
                    {
                        Grid.CellClick -= Grid_CellClick;
                        Grid.CellValueChanged -= Grid_CellValueChanged;
                        Grid.DataError -= Grid_DataError;

                        DataGridFactory.ClearLinks(Grid);

                        Grid.Rows.Clear();
                        Grid.Columns.Clear();
                        Grid.DataSource = null;
                        Grid.Dispose();
                        Grid = null;
                    }

                    _listTable = null;
                    _getTable = null;
                    _saveTable = null;
                }
                catch (Exception ex)
                {
                    ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        #endregion

    }
}