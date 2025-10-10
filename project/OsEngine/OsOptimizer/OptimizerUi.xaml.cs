/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using OsEngine.Logging;
using OsEngine.Robots;
using MessageBox = System.Windows.MessageBox;
using ProgressBar = System.Windows.Controls.ProgressBar;
using OsEngine.OsOptimizer.OptEntity;
using System.Threading;
using OsEngine.Layout;
using System.Windows.Markup;
using System.Globalization;
using OsEngine.OsTrader.Panels.Tab;
using System.Linq;
using System.Windows.Forms.DataVisualization.Charting;


namespace OsEngine.OsOptimizer
{
    public partial class OptimizerUi
    {
        public OptimizerUi()
        {
            InitializeComponent();
            _currentCulture = OsLocalization.CurCulture;
            OsEngine.Layout.StickyBorders.Listen(this);
            Thread.Sleep(200);

            _master = new OptimizerMaster();
            _master.StartPaintLog(HostLog);
            _master.NewSecurityEvent += _master_NewSecurityEvent;
            _master.DateTimeStartEndChange += _master_DateTimeStartEndChange;
            _master.TestReadyEvent += _master_TestReadyEvent;
            _master.TimeToEndChangeEvent += _master_TimeToEndChangeEvent;

            CreateTableSources();
            CreateTableResults();
            CreateTableFazes();
            CreateTableParameters();
            CreateTableOptimizeFazes();
            CreateChartSeriesResults();

            for (int i = 1; i < 51; i++)
            {
                ComboBoxThreadsCount.Items.Add(i);
            }

            ComboBoxThreadsCount.SelectedItem = _master.ThreadsCount;
            CreateThreadsProgressBars();
            ComboBoxThreadsCount.SelectionChanged += ComboBoxThreadsCount_SelectionChanged;

            TextBoxStartPortfolio.Text = _master.StartDeposit.ToString();
            TextBoxStartPortfolio.TextChanged += TextBoxStartPortfolio_TextChanged;

            CommissionTypeLabel.Content = OsLocalization.Optimizer.Label40;
            CommissionTypeComboBox.Items.Add(CommissionType.None.ToString());
            CommissionTypeComboBox.Items.Add(CommissionType.OneLotFix.ToString());
            CommissionTypeComboBox.Items.Add(CommissionType.Percent.ToString());
            CommissionTypeComboBox.SelectedItem = _master.CommissionType.ToString();
            CommissionTypeComboBox.SelectionChanged += CommissionTypeComboBoxOnSelectionChanged;

            CommissionValueLabel.Content = OsLocalization.Optimizer.Label41;
            CommissionValueTextBox.Text = _master.CommissionValue.ToString();
            CommissionValueTextBox.TextChanged += CommissionValueTextBoxOnTextChanged;

            // filters/фильтры
            CheckBoxFilterProfitIsOn.IsChecked = _master.FilterProfitIsOn;
            CheckBoxFilterMaxDrowDownIsOn.IsChecked = _master.FilterMaxDrawDownIsOn;
            CheckBoxFilterMiddleProfitIsOn.IsChecked = _master.FilterMiddleProfitIsOn;
            CheckBoxFilterProfitFactorIsOn.IsChecked = _master.FilterProfitFactorIsOn;
            CheckBoxFilterDealsCount.IsChecked = _master.FilterDealsCountIsOn;

            CheckBoxFilterProfitIsOn.Click += CheckBoxFilterIsOn_Click;
            CheckBoxFilterMaxDrowDownIsOn.Click += CheckBoxFilterIsOn_Click;
            CheckBoxFilterMiddleProfitIsOn.Click += CheckBoxFilterIsOn_Click;
            CheckBoxFilterProfitFactorIsOn.Click += CheckBoxFilterIsOn_Click;
            CheckBoxFilterDealsCount.Click += CheckBoxFilterIsOn_Click;

            TextBoxFilterProfitValue.Text = _master.FilterProfitValue.ToString();
            TextBoxMaxDrowDownValue.Text = _master.FilterMaxDrawDownValue.ToString();
            TextBoxFilterMiddleProfitValue.Text = _master.FilterMiddleProfitValue.ToString();
            TextBoxFilterProfitFactorValue.Text = _master.FilterProfitFactorValue.ToString();
            TextBoxFilterDealsCount.Text = _master.FilterDealsCountValue.ToString();

            TextBoxFilterProfitValue.TextChanged += TextBoxFilterValue_TextChanged;
            TextBoxMaxDrowDownValue.TextChanged += TextBoxFilterValue_TextChanged;
            TextBoxFilterMiddleProfitValue.TextChanged += TextBoxFilterValue_TextChanged;
            TextBoxFilterProfitFactorValue.TextChanged += TextBoxFilterValue_TextChanged;
            TextBoxFilterDealsCount.TextChanged += TextBoxFilterValue_TextChanged;

            // Stages

            DatePickerStart.Language = XmlLanguage.GetLanguage(OsLocalization.CurLocalizationCode);
            DatePickerEnd.Language = XmlLanguage.GetLanguage(OsLocalization.CurLocalizationCode);

            DatePickerStart.SelectedDate = _master.TimeStart;
            DatePickerEnd.SelectedDate = _master.TimeEnd;

            TextBoxPercentFiltration.Text = _master.PercentOnFiltration.ToString();

            CheckBoxLastInSample.IsChecked = _master.LastInSample;
            CheckBoxLastInSample.Click += CheckBoxLastInSample_Click;

            DatePickerStart.SelectedDateChanged += DatePickerStart_SelectedDateChanged;
            DatePickerEnd.SelectedDateChanged += DatePickerEnd_SelectedDateChanged;
            TextBoxPercentFiltration.TextChanged += TextBoxPercentFiltration_TextChanged;

            TextBoxIterationCount.Text = _master.IterationCount.ToString();
            TextBoxIterationCount.TextChanged += delegate (object sender, TextChangedEventArgs args)
            {
                try
                {
                    if (Convert.ToInt32(TextBoxIterationCount.Text) == 0)
                    {
                        TextBoxIterationCount.Text = _master.IterationCount.ToString();
                        return;
                    }
                    _master.IterationCount = Convert.ToInt32(TextBoxIterationCount.Text);

                    Task.Run(PaintCountBotsInOptimization);
                }
                catch
                {
                    TextBoxIterationCount.Text = _master.IterationCount.ToString();
                }

            };

            _master.NeedToMoveUiToEvent += _master_NeedToMoveUiToEvent;
            TextBoxStrategyName.Text = _master.StrategyName;

            Thread worker = new Thread(PainterProgressArea);
            worker.Start();

            Label7.Content = OsLocalization.Optimizer.Label7;
            LabelTimeToEnd.Content = "";
            Label8.Content = OsLocalization.Optimizer.Label8;
            ButtonGo.Content = OsLocalization.Optimizer.Label9;
            TabItemControl.Header = OsLocalization.Optimizer.Label10;
            ButtonServerDialog.Content = OsLocalization.Optimizer.Label11;
            Label12.Content = OsLocalization.Optimizer.Label12;
            Label13.Content = OsLocalization.Optimizer.Label13;
            LabelTabsEndTimeFrames.Content = OsLocalization.Optimizer.Label14;
            TabItemParameters.Header = OsLocalization.Optimizer.Label16;
            Label17.Content = OsLocalization.Optimizer.Label17;
            TabItemFazes.Header = OsLocalization.Optimizer.Label18;
            ButtonCreateOptimizeFazes.Content = OsLocalization.Optimizer.Label19;
            Label20.Content = OsLocalization.Optimizer.Label20;
            Label21.Content = OsLocalization.Optimizer.Label21;
            Label22.Content = OsLocalization.Optimizer.Label22;
            TabItemFilters.Header = OsLocalization.Optimizer.Label23;
            CheckBoxFilterProfitIsOn.Content = OsLocalization.Optimizer.Label24;
            CheckBoxFilterMaxDrowDownIsOn.Content = OsLocalization.Optimizer.Label25;
            CheckBoxFilterMiddleProfitIsOn.Content = OsLocalization.Optimizer.Label26;
            CheckBoxFilterProfitFactorIsOn.Content = OsLocalization.Optimizer.Label28;
            TabItemResults.Header = OsLocalization.Optimizer.Label29;
            Label30.Content = OsLocalization.Optimizer.Label30;
            Label31.Content = OsLocalization.Optimizer.Label31;
            CheckBoxFilterDealsCount.Content = OsLocalization.Optimizer.Label34;
            ButtonStrategySelect.Content = OsLocalization.Optimizer.Label35;
            Label23.Content = OsLocalization.Optimizer.Label36;
            ButtonPositionSupport.Content = OsLocalization.Trader.Label47;
            ButtonStrategyReload.Content = OsLocalization.Optimizer.Label48;
            TabControlResultsSeries.Header = OsLocalization.Optimizer.Label37;
            TabControlResultsOutOfSampleResults.Header = OsLocalization.Optimizer.Label38;
            LabelSortBy.Content = OsLocalization.Optimizer.Label39;
            CheckBoxLastInSample.Content = OsLocalization.Optimizer.Label42;
            LabelIteartionCount.Content = OsLocalization.Optimizer.Label47;
            ButtonResults.Content = OsLocalization.Optimizer.Label49;
            LabelRobustnessMetric.Content = OsLocalization.Optimizer.Label53;
            ButtonSetStandardParameters.Content = OsLocalization.Optimizer.Label57;
            LabelSeriesResultChart.Content = OsLocalization.Optimizer.Label67;

            _resultsCharting = new OptimizerReportCharting(
                HostStepsOfOptimizationTable,
                HostRobustness,
                ComboBoxSortResultsType,
                LabelRobustnessMetricValue,
                ComboBoxSortResultsBotNumPercent);

            _resultsCharting.ActivateTotalProfitChart(WindowsFormsHostTotalProfit, ComboBoxTotalProfit);

            _resultsCharting.LogMessageEvent += _master.SendLogMessage;

            this.Closing += Ui_Closing;
            this.Activate();
            this.Focus();

            GlobalGUILayout.Listen(this, "optimizerUi");

            Task.Run(new Action(StrategyLoader));
        }

        private void Ui_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Data.Label27);
                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    e.Cancel = true;
                    return;
                }

                ComboBoxThreadsCount.SelectionChanged -= ComboBoxThreadsCount_SelectionChanged;

                TextBoxStartPortfolio.TextChanged -= TextBoxStartPortfolio_TextChanged;
                CommissionTypeComboBox.SelectionChanged -= CommissionTypeComboBoxOnSelectionChanged;

                CommissionValueTextBox.TextChanged -= CommissionValueTextBoxOnTextChanged;

                CheckBoxFilterProfitIsOn.Click -= CheckBoxFilterIsOn_Click;
                CheckBoxFilterMaxDrowDownIsOn.Click -= CheckBoxFilterIsOn_Click;
                CheckBoxFilterMiddleProfitIsOn.Click -= CheckBoxFilterIsOn_Click;
                CheckBoxFilterProfitFactorIsOn.Click -= CheckBoxFilterIsOn_Click;
                CheckBoxFilterDealsCount.Click -= CheckBoxFilterIsOn_Click;

                TextBoxFilterProfitValue.TextChanged -= TextBoxFilterValue_TextChanged;
                TextBoxMaxDrowDownValue.TextChanged -= TextBoxFilterValue_TextChanged;
                TextBoxFilterMiddleProfitValue.TextChanged -= TextBoxFilterValue_TextChanged;
                TextBoxFilterProfitFactorValue.TextChanged -= TextBoxFilterValue_TextChanged;
                TextBoxFilterDealsCount.TextChanged -= TextBoxFilterValue_TextChanged;

                CheckBoxLastInSample.Click -= CheckBoxLastInSample_Click;

                DatePickerStart.SelectedDateChanged -= DatePickerStart_SelectedDateChanged;
                DatePickerEnd.SelectedDateChanged -= DatePickerEnd_SelectedDateChanged;
                TextBoxPercentFiltration.TextChanged -= TextBoxPercentFiltration_TextChanged;

                _master.NewSecurityEvent -= _master_NewSecurityEvent;
                _master.DateTimeStartEndChange -= _master_DateTimeStartEndChange;
                _master.TestReadyEvent -= _master_TestReadyEvent;
                _master.TimeToEndChangeEvent -= _master_TimeToEndChangeEvent;

                _master = null;

                if (HostTabsSimple != null)
                {
                    HostTabsSimple.Child = null;
                }

                if (HostStepsOptimize != null)
                {
                    HostStepsOptimize.Child = null;
                }

                if (HostParam != null)
                {
                    HostParam.Child = null;
                }

                if (WindowsFormsHostFazeNumOnTubResult != null)
                {
                    WindowsFormsHostFazeNumOnTubResult.Child = null;
                }

                if (WindowsFormsHostResults != null)
                {
                    WindowsFormsHostResults.Child = null;
                }

                if (_gridSources != null)
                {
                    _gridSources.CellValueChanged -= _grid_CellValueChanged;
                    _gridSources.CellClick -= _gridSources_CellClick;
                    _gridSources.DataError -= _gridSources_DataError;
                    DataGridFactory.ClearLinks(_gridSources);
                    _gridSources = null;
                }

                if (_gridFazes != null)
                {
                    _gridFazes.CellValueChanged -= _gridFazes_CellValueChanged;
                    _gridFazes.DataError -= _gridSources_DataError;
                    DataGridFactory.ClearLinks(_gridFazes);
                    _gridFazes = null;
                }

                if (_gridParameters != null)
                {
                    _gridParameters.DataError -= _gridSources_DataError;
                    DataGridFactory.ClearLinks(_gridParameters);
                    _gridParameters = null;
                }

                if (_gridFazesEnd != null)
                {
                    _gridFazesEnd.CellClick -= _gridFazesEnd_CellClick;
                    _gridFazesEnd.DataError -= _gridSources_DataError;
                    DataGridFactory.ClearLinks(_gridFazesEnd);
                    _gridFazesEnd = null;
                }

                if (_gridResults != null)
                {
                    _gridResults.DataError -= _gridSources_DataError;
                    DataGridFactory.ClearLinks(_gridResults);
                    _gridResults = null;
                }
            }
            catch
            { 

            }
        }

        private CultureInfo _currentCulture;

        private OptimizerReportCharting _resultsCharting;

        private OptimizerMaster _master;

        private void StopUserActivity()
        {
            TabControlPrime.SelectedItem = TabControlPrime.Items[0];

            TabItemParameters.IsEnabled = false;
            TabItemFazes.IsEnabled = false;
            TabItemFilters.IsEnabled = false;
            TabItemResults.IsEnabled = false;
            ComboBoxThreadsCount.IsEnabled = false;
            ButtonResults.IsEnabled = false;
            ButtonStrategySelect.IsEnabled = false;
            ButtonPositionSupport.IsEnabled = false;
            TextBoxStartPortfolio.IsEnabled = false;
            CommissionTypeComboBox.IsEnabled = false;
            HostTabsSimple.IsEnabled = false;
            ButtonServerDialog.IsEnabled = false;
            CommissionValueTextBox.IsEnabled = false;
            TextBoxStrategyName.IsEnabled = false;
            ButtonStrategyReload.IsEnabled = false;
        }

        private void StartUserActivity()
        {
            if (!ButtonGo.Dispatcher.CheckAccess())
            {
                ButtonGo.Dispatcher.Invoke(StartUserActivity);
                return;
            }

            ButtonGo.Content = OsLocalization.Optimizer.Label9;
            TabControlPrime.SelectedItem = TabControlPrime.Items[4];
            TabControlResults.SelectedItem = TabControlResults.Items[1];

            TabItemParameters.IsEnabled = true;
            TabItemFazes.IsEnabled = true;
            TabItemFilters.IsEnabled = true;
            TabItemResults.IsEnabled = true;
            ComboBoxThreadsCount.IsEnabled = true;
            ButtonResults.IsEnabled = true;
            ButtonStrategySelect.IsEnabled = true;
            ButtonPositionSupport.IsEnabled = true;
            TextBoxStartPortfolio.IsEnabled = true;
            CommissionTypeComboBox.IsEnabled = true;
            HostTabsSimple.IsEnabled = true;
            ButtonServerDialog.IsEnabled = true;
            CommissionValueTextBox.IsEnabled = true;
            TextBoxStrategyName.IsEnabled = true;
            ButtonStrategyReload.IsEnabled = true;
        }

        private DateTime _lastTestEndEventTime = DateTime.MinValue;

        private string _testEndEventLocker = "testEndEventLocker";

        private void _master_TestReadyEvent(List<OptimizerFazeReport> reports)
        {
            lock (_testEndEventLocker)
            {
                if (_lastTestEndEventTime.AddSeconds(3) > DateTime.Now)
                {
                    return;
                }

                _lastTestEndEventTime = DateTime.Now;
                _reports = reports;
                RepaintResults();
                ShowResultDialog();
                _testIsEnd = true;

            }
        }

        private bool _testIsEnd;

        private List<OptimizerFazeReport> _reports;

        private void RepaintResults()
        {
            try
            {
                if (!ProgressBarPrime.Dispatcher.CheckAccess())
                {
                    ProgressBarPrime.Dispatcher.Invoke(RepaintResults);
                    return;
                }

                for (int i = 0; i < _reports.Count; i++)
                {
                    OptimizerFazeReport.SortResults(_reports[i].Reports, _sortBotsType);
                }

                PaintEndOnAllProgressBars();
                PaintTableFazes();

                if (_gridFazesEnd.Rows.Count > 0)
                {
                    _gridFazesEnd.Rows[0].Selected = true;
                    _gridFazesEnd.Rows[0].Cells[0].Selected = true;
                    _gridFazesEnd.CurrentCell = _gridFazes.Rows[0].Cells[0];
                }

                Label7.Content = OsLocalization.Optimizer.Label7;
                LabelTimeToEnd.Content = "";

                PaintTableResults();
                PaintSeriesResultsChart();

                StartUserActivity();

                _resultsCharting.ReLoad(_reports);
            }
            catch (Exception error)
            {
                _master.SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _master_TimeToEndChangeEvent(TimeSpan timeToEnd)
        {
            SetTimeToEnd(timeToEnd.ToString());
        }

        private void SetTimeToEnd(string timeToEnd)
        {
            try
            {
                if (LabelTimeToEnd.Dispatcher.CheckAccess() == false)
                {
                    LabelTimeToEnd.Dispatcher.Invoke(new Action<string>(SetTimeToEnd), timeToEnd);
                    return;
                }

                LabelTimeToEnd.Content =
                    OsLocalization.Optimizer.Label63 + ": "
                    + timeToEnd.ToString();

            }
            catch (Exception error)
            {
                _master.SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _master_NeedToMoveUiToEvent(NeedToMoveUiTo move)
        {
            // Moving the screen to the desired interface element, if the user has not managed to configure everything
            if (!TabControlPrime.Dispatcher.CheckAccess())
            {
                TabControlPrime.Dispatcher.Invoke(new Action<NeedToMoveUiTo>(_master_NeedToMoveUiToEvent), move);
                return;
            }

            if (move == NeedToMoveUiTo.Fazes)
            {
                TabControlPrime.SelectedItem = TabControlPrime.Items[2];
            }
            if (move == NeedToMoveUiTo.Filters)
            {
                TabControlPrime.SelectedItem = TabControlPrime.Items[3];
            }
            if (move == NeedToMoveUiTo.TabsAndTimeFrames)
            {
                TabControlPrime.SelectedItem = TabControlPrime.Items[0];
            }
            if (move == NeedToMoveUiTo.Storage
                || move == NeedToMoveUiTo.NameStrategy)
            {
                TabControlPrime.SelectedItem = TabControlPrime.Items[0];
            }
            if (move == NeedToMoveUiTo.Parameters)
            {
                TabControlPrime.SelectedItem = TabControlPrime.Items[1];
            }
            // Проверка параметра Regime (наличие/состояние)
            if (move == NeedToMoveUiTo.RegimeRow)
            {
                for (int i = 0; i < _gridParameters.Rows.Count; i++)
                {
                    for (int j = 0; j < ((DataGridViewCellCollection)_gridParameters.Rows[i].Cells).Count; j++)
                    {
                        if (Convert.ToString(_gridParameters.Rows[i].Cells[j].Value) == "Regime")
                        {
                            _gridParameters.CurrentCell = _gridParameters[_gridParameters.Rows[i].Cells[j].ColumnIndex + 2, i];
                            break;
                        }
                    }
                }
                TabControlPrime.SelectedItem = TabControlPrime.Items[1];
            }
            // Проверка параметра Regime (наличие/состояние) / конец
        }

        private DateTime _lastUpdateTimePicker;

        private void _master_DateTimeStartEndChange()
        {
            if (_lastUpdateTimePicker.AddSeconds(2) > DateTime.Now)
            {
                return;
            }

            if (!DatePickerStart.Dispatcher.CheckAccess())
            {
                DatePickerStart.Dispatcher.Invoke(_master_DateTimeStartEndChange);
                return;
            }

            _lastUpdateTimePicker = DateTime.Now;

            DatePickerStart.SelectedDate = _master.TimeStart;
            DatePickerEnd.SelectedDate = _master.TimeEnd;
        }

        #region Progress bars

        private void ComboBoxThreadsCount_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CreateThreadsProgressBars();
        }

        private List<ProgressBar> _progressBars;

        private void CreateThreadsProgressBars()
        {
            if (!ComboBoxThreadsCount.Dispatcher.CheckAccess())
            {
                ComboBoxThreadsCount.Dispatcher.Invoke(CreateThreadsProgressBars);
                return;
            }
            _progressBars = new List<ProgressBar>();

            int countThreads = ComboBoxThreadsCount.SelectedIndex + 1;

            GridThreadsProgress.Children.Clear();
            GridThreadsProgress.Height = 8 * countThreads + 10;
            GridThreadsProgress.MinHeight = 8 * countThreads + 10;

            for (int i = 0; i < countThreads; i++)
            {
                ProgressBar bar = new ProgressBar();
                bar.Margin = new Thickness(0, GridThreadsProgress.Height - 8 - i * 8, 0, i * 8);
                bar.VerticalAlignment = VerticalAlignment.Top;
                bar.Height = 8;
                GridThreadsProgress.Children.Add(bar);
                _progressBars.Add(bar);
            }
            _master.ThreadsCount = countThreads;
        }

        private void PainterProgressArea()
        {
            while (true)
            {
                Thread.Sleep(1500);

                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }
                PaintAllProgressBars();
            }
        }

        private void PaintAllProgressBars()
        {
            try
            {
                if (_progressBars == null ||
                    _progressBars.Count == 0)
                {
                    return;
                }

                if (!_progressBars[0].Dispatcher.CheckAccess())
                {
                    _progressBars[0].Dispatcher.Invoke(PaintAllProgressBars);
                    return;
                }

                if (_testIsEnd)
                {
                    ProgressBarPrime.Maximum = 100;
                    ProgressBarPrime.Value = 100;

                    for (int i2 = 0; i2 > -1 && i2 < _progressBars.Count; i2++)
                    {
                        _progressBars[i2].Maximum = 100;
                        _progressBars[i2].Value = 100;
                    }

                    return;
                }

                ProgressBarStatus primeStatus = _master.PrimeProgressBarStatus;

                if (primeStatus.MaxValue != 0 &&
                    primeStatus.CurrentValue != 0)
                {
                    ProgressBarPrime.Maximum = primeStatus.MaxValue;
                    ProgressBarPrime.Value = primeStatus.CurrentValue;
                }

                List<ProgressBarStatus> statuses = _master.ProgressBarStatuses;

                if (statuses == null ||
                    statuses.Count == 0)
                {
                    return;
                }

                for (int i = statuses.Count-1, i2 = _progressBars.Count-1; 
                    i >= 0 && i2 >= 0; 
                    i2--, i--)
                {
                    ProgressBarStatus status = statuses[i];

                    if (status == null)
                    {
                        return;
                    }

                    if(_progressBars[i2].Maximum != status.MaxValue)
                    {
                        _progressBars[i2].Maximum = status.MaxValue;
                    }
                    if(_progressBars[i2].Value != status.CurrentValue)
                    {
                        _progressBars[i2].Value = status.CurrentValue;
                    }
                }
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void PaintEndOnAllProgressBars()
        {
            if (!ProgressBarPrime.Dispatcher.CheckAccess())
            {
                ProgressBarPrime.Dispatcher.Invoke(PaintEndOnAllProgressBars);
                return;
            }

            ProgressBarPrime.Maximum = 100;
            ProgressBarPrime.Value = ProgressBarPrime.Maximum;

            for (int i = 0; _progressBars != null && i < _progressBars.Count; i++)
            {
                _progressBars[i].Value = _progressBars[i].Maximum;
            }
        }

        #endregion

        #region Processing controls by clicking on them by the user

        private void ButtonPositionSupport_Click(object sender, RoutedEventArgs e)
        {
            _master.ShowManualControlDialog();
        }

        private void ButtonGo_Click(object sender, RoutedEventArgs e)
        {
            SaveParametersFromTable();

            if (ButtonGo.Content.ToString() == OsLocalization.Optimizer.Label9 &&
                 _reports != null)
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Optimizer.Label33);

                ui.ShowDialog();

                if (!ui.UserAcceptAction)
                {
                    return;
                }
            }

            _testIsEnd = false;

            int botsCount = _master.GetMaxBotsCount();

            if (botsCount > 100000)
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Optimizer.Label60);

                ui.ShowDialog();

                if (!ui.UserAcceptAction)
                {
                    return;
                }
            }

            if (_master.Fazes != null &&
                _master.Fazes.Count > 1 &&
                (_master.FilterDealsCountIsOn
                || _master.FilterMaxDrawDownIsOn
                || _master.FilterMiddleProfitIsOn
                || _master.FilterProfitFactorIsOn
                || _master.FilterProfitIsOn))
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Optimizer.Label61);

                ui.ShowDialog();

                if (!ui.UserAcceptAction)
                {
                    return;
                }
            }

            if (ButtonGo.Content.ToString() == OsLocalization.Optimizer.Label9
                && _master.Start())
            {
                ButtonGo.Content = OsLocalization.Optimizer.Label32;
                StopUserActivity();

                ProgressBarPrime.Value = 0;

                if(_progressBars != null && _progressBars.Count > 0)
                {
                    for (int i2 = 0; i2 < _progressBars.Count; i2++)
                    {
                        _progressBars[i2].Value = 0;
                    }
                }
            }
            else if (ButtonGo.Content.ToString() == OsLocalization.Optimizer.Label32)
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Optimizer.Label51);

                ui.ShowDialog();

                if (!ui.UserAcceptAction)
                {
                    return;
                }

                _master.Stop();
                ButtonGo.Content = OsLocalization.Optimizer.Label9;
            }
        }

        private void TextBoxPercentFiltration_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _master.PercentOnFiltration = Convert.ToDecimal(TextBoxPercentFiltration.Text);
            }
            catch
            {
                TextBoxPercentFiltration.Text = _master.PercentOnFiltration.ToString();
            }
        }

        private void DatePickerEnd_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            _lastUpdateTimePicker = DateTime.Now;
            _master.TimeEnd = DatePickerEnd.SelectedDate.Value;
        }

        private void DatePickerStart_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            _lastUpdateTimePicker = DateTime.Now;
            _master.TimeStart = DatePickerStart.SelectedDate.Value;
        }

        private void TextBoxFilterValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _master.FilterProfitValue = Convert.ToDecimal(TextBoxFilterProfitValue.Text);
                _master.FilterMaxDrawDownValue = Convert.ToDecimal(TextBoxMaxDrowDownValue.Text);
                _master.FilterMiddleProfitValue = Convert.ToDecimal(TextBoxFilterMiddleProfitValue.Text);
                _master.FilterProfitFactorValue = Convert.ToDecimal(TextBoxFilterProfitFactorValue.Text);
                _master.FilterDealsCountValue = Convert.ToInt32(TextBoxFilterDealsCount.Text);
            }
            catch
            {
                TextBoxFilterProfitValue.Text = _master.FilterProfitValue.ToString();
                TextBoxMaxDrowDownValue.Text = _master.FilterMaxDrawDownValue.ToString();
                TextBoxFilterMiddleProfitValue.Text = _master.FilterMiddleProfitValue.ToString();
                TextBoxFilterProfitFactorValue.Text = _master.FilterProfitFactorValue.ToString();
                TextBoxFilterDealsCount.Text = _master.FilterDealsCountValue.ToString();
            }
        }

        private void CheckBoxFilterIsOn_Click(object sender, RoutedEventArgs e)
        {
            _master.FilterProfitIsOn = CheckBoxFilterProfitIsOn.IsChecked.Value;
            _master.FilterMaxDrawDownIsOn = CheckBoxFilterMaxDrowDownIsOn.IsChecked.Value;
            _master.FilterMiddleProfitIsOn = CheckBoxFilterMiddleProfitIsOn.IsChecked.Value;
            _master.FilterProfitFactorIsOn = CheckBoxFilterProfitFactorIsOn.IsChecked.Value;
            _master.FilterDealsCountIsOn = CheckBoxFilterDealsCount.IsChecked.Value;
        }

        private void ButtonStrategySelect_Click(object sender, RoutedEventArgs e)
        {
            List<string> namesForOptimization = BotFactory.GetNamesStrategyWithParametersSync();

            List<string> scriptsNames = BotFactory.GetScriptsNamesStrategy();

            List<string> includeNames = BotFactory.GetIncludeNamesStrategy();

            for (int i = 0; i < scriptsNames.Count; i++)
            {
                if (namesForOptimization.Find(s => s == scriptsNames[i]) == null)
                {
                    scriptsNames.RemoveAt(i);
                    i--;
                }
            }

            for (int i = 0; i < includeNames.Count; i++)
            {
                if (namesForOptimization.Find(s => s == includeNames[i]) == null)
                {
                    includeNames.RemoveAt(i);
                    i--;
                }
            }

            BotCreateUi2 ui = new BotCreateUi2(includeNames, scriptsNames,
                StartProgram.IsOsOptimizer, null);

            ui.ShowDialog();

            if (ui.IsAccepted == false)
            {
                return;
            }

            _master.StrategyName = ui.NameStrategy;
            _master.IsScript = ui.IsScript;
            _master.CreateBot();
            TextBoxStrategyName.Text = ui.NameStrategy;
            ReloadStrategy();
        }

        private void ReloadStrategy()
        {
            _parameters = _master.Parameters;
            _parametersActive = _master.ParametersOn;
            PaintTableSources();
            PaintTableParameters();
            PaintCountBotsInOptimization();
        }

        private void TextBoxStartPortfolio_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(TextBoxStartPortfolio.Text) <= 0)
                {
                    throw new Exception();
                }
            }
            catch
            {
                TextBoxStartPortfolio.Text = _master.StartDeposit.ToString();
                return;
            }

            _master.StartDeposit = Convert.ToInt32(TextBoxStartPortfolio.Text);
        }

        private void CommissionValueTextBoxOnTextChanged(object sender, TextChangedEventArgs e)
        {
            decimal commissionValue;
            try
            {
                var isParsed = decimal.TryParse(CommissionValueTextBox.Text, out commissionValue);
                if (!isParsed || commissionValue < 0)
                {
                    throw new Exception();
                }
            }
            catch
            {
                CommissionValueTextBox.Text = _master.CommissionValue.ToString();
                return;
            }

            _master.CommissionValue = commissionValue;
        }

        private void CommissionTypeComboBoxOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CommissionType commissionType = (CommissionType)Enum.Parse(typeof(CommissionType),
                (string)CommissionTypeComboBox.SelectedItem);
            _master.CommissionType = commissionType;
        }

        private void ButtonServerDialog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_master.ShowDataStorageDialog())
                {
                    if (_master.Fazes != null)
                    {
                        _master.Fazes.Clear();
                    }

                    _master.UpdateServerToSettings();
                    _master.CreateBot();

                    PaintTableSources();
                }
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void CheckBoxLastInSample_Click(object sender, RoutedEventArgs e)
        {
            _master.LastInSample = CheckBoxLastInSample.IsChecked.Value;
        }

        private void ButtonResults_Click(object sender, RoutedEventArgs e)
        {
            ShowResultDialog();
        }

        public void ShowResultDialog()
        {
            if (_gridFazes.InvokeRequired)
            {
                _gridFazes.Invoke(new Action(ShowResultDialog));
                return;
            }

            OptimizerReportUi _uiReport = new OptimizerReportUi(_master);
            _uiReport.Show();
            _uiReport.Paint(_reports);
            _uiReport.Activate();
        }

        #endregion

        #region Table of Sources

        private DataGridView _gridSources;

        private void _master_NewSecurityEvent(List<Security> securities)
        {
            PaintTableSources();
        }

        private void CreateTableSources()
        {
            _gridSources = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.ColumnHeaderSelect, DataGridViewAutoSizeRowsMode.AllCells);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridSources.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Optimizer.Message20;
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _gridSources.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Optimizer.Label64;
            column1.ReadOnly = false;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSources.Columns.Add(column1);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = OsLocalization.Optimizer.Label65;
            column2.ReadOnly = false;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSources.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = OsLocalization.Optimizer.Label66;
            column3.ReadOnly = false;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSources.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = "";
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSources.Columns.Add(column4);

            _gridSources.Rows.Add(null, null);

            HostTabsSimple.Child = _gridSources;

            _gridSources.CellValueChanged += _grid_CellValueChanged;
            _gridSources.CellClick += _gridSources_CellClick;
            _gridSources.DataError += _gridSources_DataError;
        }

        private void _gridSources_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            _master.SendLogMessage(e.ToString(), LogMessageType.Error);
        }

        private void PaintTableSources()
        {
            if (_gridSources == null)
            {
                return;
            }

            if (_gridSources.InvokeRequired)
            {
                _gridSources.Invoke(new Action(PaintTableSources));
                return;
            }

            // берём робота которого хотим оптимизировать

            string nameBot = _master.StrategyName;

            if (string.IsNullOrEmpty(nameBot))
            {
                return;
            }

            BotPanel bot = _master.BotToTest;

            if (bot == null)
            {
                PaintSources(null);
                return;
            }

            List<IIBotTab> botSources = bot.GetTabs();

            PaintSources(botSources);
        }

        private void PaintSources(List<IIBotTab> sources)
        {
            if (_gridSources.InvokeRequired)
            {
                _gridSources.Invoke(new Action<List<IIBotTab>>(PaintSources), sources);
                return;
            }

            _gridSources.Rows.Clear();

            if (sources == null)
            {
                return;
            }

            for (int i = 0; i < sources.Count; i++)
            {
                DataGridViewRow row = null;

                if (sources[i].TabType == BotTabType.Simple)
                {
                    row = GetBotTabSimpleRow((BotTabSimple)sources[i], i + 1);
                }
                else if (sources[i].TabType == BotTabType.Index)
                {
                    row = GetBotTabIndexRow((BotTabIndex)sources[i], i + 1);
                }
                else if (sources[i].TabType == BotTabType.Screener)
                {
                    row = GetBotTabScreenerRow((BotTabScreener)sources[i], i + 1);
                }

                if(row == null)
                {
                    return;
                }

                _gridSources.Rows.Add(row);
            }
        }

        private List<string> GetTimeFramesList(List<SecurityTester> securities)
        {
            List<string> timeFrame = new List<string>();

            if (securities[0].DataType == SecurityTesterDataType.Candle)
            {
                for (int i = 0; i < securities.Count; i++)
                {
                    if (timeFrame.Find(n => n == securities[i].TimeFrame.ToString()) == null)
                    {
                        timeFrame.Add(securities[i].TimeFrame.ToString());
                    }
                }
            }
            else
            {
                timeFrame.Add(TimeFrame.Sec2.ToString());
                timeFrame.Add(TimeFrame.Sec5.ToString());
                timeFrame.Add(TimeFrame.Sec10.ToString());
                timeFrame.Add(TimeFrame.Sec15.ToString());
                timeFrame.Add(TimeFrame.Sec20.ToString());
                timeFrame.Add(TimeFrame.Sec30.ToString());
                timeFrame.Add(TimeFrame.Min1.ToString());
                timeFrame.Add(TimeFrame.Min2.ToString());
                timeFrame.Add(TimeFrame.Min5.ToString());
                timeFrame.Add(TimeFrame.Min10.ToString());
                timeFrame.Add(TimeFrame.Min15.ToString());
                timeFrame.Add(TimeFrame.Min20.ToString());
                timeFrame.Add(TimeFrame.Min30.ToString());
                timeFrame.Add(TimeFrame.Hour1.ToString());
                timeFrame.Add(TimeFrame.Hour2.ToString());
                timeFrame.Add(TimeFrame.Hour4.ToString());
            }

            return timeFrame;
        }

        private DataGridViewRow GetBotTabSimpleRow(BotTabSimple source, int num)
        {
            // 1 берём бумаги доступные для тестов

            List<SecurityTester> securities = _master.SecurityTester;

            if (securities == null)
            {
                return null;
            }

            List<string> names = new List<string>();

            for (int i = 0; i < securities.Count; i++)
            {
                if (names.Find(n => n == securities[i].Security.Name) == null)
                {
                    names.Add(securities[i].Security.Name);
                }
            }

            // 2 берём таймфреймы доступные для тестов

            List<string> timeFrame = GetTimeFramesList(securities);

            // 3 генерируем строку

            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[0].Value = num;
            row.Cells[0].ReadOnly = true;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[1].Value = "BotTabSimple";
            row.Cells[1].ReadOnly = true;

            // 4 SecurityToTrade

            string securityInSource = null;

            if (source.Connector != null)
            {
                securityInSource = source.Connector.SecurityName;
            }

            bool isInArray = false;

            DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();
            for (int i2 = 0; i2 < names.Count; i2++)
            {
                cell.Items.Add(names[i2]);

                if (string.IsNullOrEmpty(securityInSource) == false
                     && securityInSource == names[i2])
                {
                    isInArray = true;
                }
            }

            if (isInArray)
            {
                cell.Value = securityInSource;
            }
            else
            {
                if (string.IsNullOrEmpty(securityInSource) == false)
                {
                    source.Connector.SecurityName = "";
                }
            }

            row.Cells.Add(cell);

            // 5 TimeFrame

            string timeFrameInSource = null;

            if (source.Connector != null)
            {
                timeFrameInSource = source.Connector.TimeFrame.ToString();
            }

            isInArray = false;

            DataGridViewComboBoxCell cell2 = new DataGridViewComboBoxCell();

            if (source.Connector.CandleCreateMethodType == CandleCreateMethodType.Simple.ToString())
            {
                for (int i2 = 0; i2 < timeFrame.Count; i2++)
                {
                    cell2.Items.Add(timeFrame[i2]);

                    if (timeFrameInSource == timeFrame[i2])
                    {
                        isInArray = true;
                    }
                }

                if (isInArray)
                {
                    cell2.Value = timeFrameInSource;
                }
                else
                {
                    source.Connector.TimeFrame = TimeFrame.Sec1;
                }
            }
            else
            {
                cell2.Items.Add(source.Connector.CandleCreateMethodType);
                cell2.Value = source.Connector.CandleCreateMethodType;
            }

            row.Cells.Add(cell2);

            DataGridViewButtonCell button = new DataGridViewButtonCell();
            button.Value = "Settings";
            row.Cells.Add(button);

            return row;
        }

        private DataGridViewRow GetBotTabIndexRow(BotTabIndex source, int num)
        {
            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[0].Value = num;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[1].Value = "BotTabIndex";

            DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();

            if (source.Tabs != null &&
                source.Tabs.Count > 0)
            {
                cell.Value = source.Tabs[0].SecurityName;
            }

            row.Cells.Add(cell);

            DataGridViewTextBoxCell cell2 = new DataGridViewTextBoxCell();

            if (source.Tabs != null &&
                source.Tabs.Count > 0)
            {
                cell2.Value = source.Tabs[0].TimeFrame.ToString();
            }

            row.Cells.Add(cell2);

            DataGridViewButtonCell button = new DataGridViewButtonCell();
            button.Value = "Settings";
            row.Cells.Add(button);

            return row;
        }

        private DataGridViewRow GetBotTabScreenerRow(BotTabScreener source, int num)
        {
            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[0].Value = num;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[1].Value = "BotTabScreener";

            DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();

            if (source.Tabs != null &&
                source.Tabs.Count > 0
                && source.Tabs[0].Connector != null)
            {
                cell.Value = source.Tabs[0].Connector.SecurityName;
            }

            row.Cells.Add(cell);
            cell.ReadOnly = true;

            DataGridViewTextBoxCell cell2 = new DataGridViewTextBoxCell();

            if (source.Tabs != null &&
                source.Tabs.Count > 0)
            {
                cell2.Value = source.Tabs[0].Connector.TimeFrame.ToString();
            }

            row.Cells.Add(cell2);
            cell2.ReadOnly = true;

            DataGridViewButtonCell button = new DataGridViewButtonCell();
            button.Value = "Settings";
            row.Cells.Add(button);

            return row;
        }

        private void _grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            // здесь только работа с BotTabIndex!!! Имя + Таймфрейм

            int rowIndex = e.RowIndex;
            int columnIndex = e.ColumnIndex;

            if (columnIndex != 2 &&
                columnIndex != 3)
            {
                return;
            }

            BotPanel bot = _master.BotToTest;

            List<IIBotTab> sources = bot.GetTabs();

            if (rowIndex >= sources.Count)
            {
                return;
            }

            IIBotTab curTab = sources[rowIndex];

            if (curTab.TabType != BotTabType.Simple)
            {
                return;
            }

            BotTabSimple simpleTab = (BotTabSimple)curTab;

            if (columnIndex == 2)
            {
                simpleTab.Connector.SecurityName = _gridSources.Rows[rowIndex].Cells[2].Value.ToString();
            }
            else if (columnIndex == 3)
            {
                TimeFrame newFrame = new TimeFrame();

                if (Enum.TryParse(_gridSources.Rows[rowIndex].Cells[3].Value.ToString(), out newFrame))
                {
                    simpleTab.Connector.TimeFrame = newFrame;
                }
            }
        }

        private void _gridSources_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int rowIndex = e.RowIndex;
                int columnIndex = e.ColumnIndex;

                if (columnIndex != 4)
                {
                    return;
                }

                BotPanel bot = _master.BotToTest;

                if(bot == null)
                {
                    return;
                }

                List<IIBotTab> sources = bot.GetTabs();

                if (sources == null 
                    || sources.Count == 0
                    || rowIndex >= sources.Count)
                {
                    return;
                }

                if(_master.Storage == null 
                    || _master.Storage.Securities == null 
                    || _master.Storage.Securities.Count == 0)
                {
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Optimizer.Label70);
                    ui.ShowDialog();
                    return;
                }

                IIBotTab curTab = sources[rowIndex];

                if (curTab.TabType == BotTabType.Simple)
                {
                    BotTabSimple simpleTab = (BotTabSimple)curTab;
                    simpleTab.ShowConnectorDialog();
                    simpleTab.DialogClosed += () =>
                    {
                        PaintTableSources();
                    };
                }
                else if (curTab.TabType == BotTabType.Index)
                {
                    BotTabIndex simpleTab = (BotTabIndex)curTab;
                    simpleTab.ShowDialog();
                    simpleTab.DialogClosed += () =>
                    {
                        PaintTableSources();
                    };
                }
                else if (curTab.TabType == BotTabType.Screener)
                {
                    BotTabScreener screenerTab = (BotTabScreener)curTab;
                    screenerTab.ShowDialog();

                    screenerTab.DialogClosed += () =>
                    {
                        screenerTab.TryLoadTabs();
                        screenerTab.TryReLoadTabs();
                        PaintTableSources();
                    };
                }
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Test phase table

        private void ButtonCreateOptimizeFazes_Click(object sender, RoutedEventArgs e)
        {
            _master.ReloadFazes();
            PaintTableOptimizeFazes();

            if (_master.Fazes == null ||
                _master.Fazes.Count == 0)
            {
                return;
            }

            WalkForwardPeriodsPainter.PaintForwards(HostWalkForwardPeriods, _master.Fazes);

            PaintCountBotsInOptimization();
        }

        private DataGridView _gridFazes;

        private void CreateTableOptimizeFazes()
        {
            _gridFazes = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.AllCells);
            _gridFazes.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridFazes.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Optimizer.Message23;
            column0.ReadOnly = true;
            column0.Width = 100;

            _gridFazes.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Optimizer.Message24;
            column1.ReadOnly = true;
            column1.Width = 150;

            _gridFazes.Columns.Add(column1);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = OsLocalization.Optimizer.Message25;
            column.ReadOnly = false;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridFazes.Columns.Add(column);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = OsLocalization.Optimizer.Message26;
            column2.ReadOnly = false;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridFazes.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = OsLocalization.Optimizer.Message27;
            column3.ReadOnly = true;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridFazes.Columns.Add(column3);

            _gridFazes.Rows.Add(null, null);

            HostStepsOptimize.Child = _gridFazes;

            _gridFazes.CellValueChanged += _gridFazes_CellValueChanged;
            _gridFazes.DataError += _gridSources_DataError;
        }

        private void _gridFazes_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            // 2 - Start 3 - End

            int indexColumn = e.ColumnIndex;

            if (indexColumn != 2 && indexColumn != 3)
            {
                return;
            }

            int indexRow = e.RowIndex;

            try
            {
                DateTime time = Convert.ToDateTime(_gridFazes.Rows[indexRow].Cells[indexColumn].EditedFormattedValue.ToString(), _currentCulture);

                if (indexColumn == 2)
                {
                    _master.Fazes[indexRow].TimeStart = time;
                }
                else
                {
                    _master.Fazes[indexRow].TimeEnd = time;
                }

            }
            catch
            {
                if (indexColumn == 2)
                {
                    _gridFazes.Rows[indexRow].Cells[indexColumn].Value = _master.Fazes[indexRow].TimeStart.ToString(OsLocalization.ShortDateFormatString);
                }
                else
                {
                    _gridFazes.Rows[indexRow].Cells[indexColumn].Value = _master.Fazes[indexRow].TimeEnd.ToString(OsLocalization.ShortDateFormatString);
                }
            }

            PaintTableOptimizeFazes();

            if (_master.Fazes.Count != 0)
            {
                WalkForwardPeriodsPainter.PaintForwards(HostWalkForwardPeriods, _master.Fazes);
            }
        }

        private void PaintTableOptimizeFazes()
        {
            if (_gridFazes.InvokeRequired)
            {
                _gridFazes.Invoke(new Action(PaintTableOptimizeFazes));
                return;
            }

            _gridFazes.Rows.Clear();

            List<OptimizerFaze> fazes = _master.Fazes;

            if (fazes == null ||
                fazes.Count == 0)
            {
                return;
            }

            for (int i = 0; i < fazes.Count; i++)
            {
                DataGridViewRow row = new DataGridViewRow();

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = i + 1;

                DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                cell.Value = fazes[i].TypeFaze;
                row.Cells.Add(cell);

                DataGridViewTextBoxCell cell2 = new DataGridViewTextBoxCell();
                cell2.Value = fazes[i].TimeStart.ToString(OsLocalization.ShortDateFormatString);
                row.Cells.Add(cell2);

                DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell();
                cell3.Value = fazes[i].TimeEnd.ToString(OsLocalization.ShortDateFormatString);
                row.Cells.Add(cell3);

                DataGridViewTextBoxCell cell4 = new DataGridViewTextBoxCell();
                cell4.Value = fazes[i].Days;
                row.Cells.Add(cell4);

                _gridFazes.Rows.Add(row);
            }
        }

        #endregion

        #region Parameters table

        private List<IIStrategyParameter> _parameters;

        private List<bool> _parametersActive;

        private DataGridView _gridParameters;

        private void CreateTableParameters()
        {
            _gridParameters = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.AllCells);
            _gridParameters.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridParameters.DefaultCellStyle;

            DataGridViewCheckBoxColumn column0 = new DataGridViewCheckBoxColumn();
            column0.CellTemplate = new DataGridViewCheckBoxCell();
            column0.HeaderText = OsLocalization.Optimizer.Message28;
            column0.ReadOnly = false;
            column0.Width = 100;

            _gridParameters.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Optimizer.Message29;
            column1.ReadOnly = true;
            column1.Width = 600;

            _gridParameters.Columns.Add(column1);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = OsLocalization.Optimizer.Message24;
            column.ReadOnly = true;
            column.Width = 100;
            _gridParameters.Columns.Add(column);

            DataGridViewComboBoxColumn column2 = new DataGridViewComboBoxColumn();
            column2.CellTemplate = new DataGridViewComboBoxCell();
            column2.HeaderText = OsLocalization.Optimizer.Message30;
            column2.ReadOnly = false;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridParameters.Columns.Add(column2);

            DataGridViewColumn column22 = new DataGridViewColumn();
            column22.CellTemplate = cell0;
            column22.HeaderText = OsLocalization.Optimizer.Message31;
            column22.ReadOnly = false;
            column22.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridParameters.Columns.Add(column22);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = OsLocalization.Optimizer.Message32;
            column3.ReadOnly = false;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridParameters.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = OsLocalization.Optimizer.Message33;
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridParameters.Columns.Add(column4);

            DataGridViewColumn column5 = new DataGridViewColumn();
            column5.CellTemplate = cell0;
            column5.ReadOnly = false;
            column5.Width = 20;
            _gridParameters.Columns.Add(column5);

            _gridParameters.Rows.Add(null, null);
            _gridParameters.DataError += _gridParameters_DataError;

            HostParam.Child = _gridParameters;
            _gridParameters.DataError += _gridSources_DataError;
        }

        private void _gridParameters_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            if (_master == null)
            {
                return;
            }
            _master.SendLogMessage(e.ToString(), LogMessageType.Error);
        }

        private void PaintTableParameters()
        {
            if (_gridParameters.InvokeRequired)
            {
                _gridParameters.Invoke(new Action(PaintTableParameters));
                return;
            }

            try
            {
                _gridParameters.CellValueChanged -= _gridParameters_CellValueChanged;
                _gridParameters.CellClick -= _gridParameters_CellClick;

                _gridParameters.Rows.Clear();

                if (_parameters == null ||
                     _parameters.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < _parameters.Count; i++)
                {
                    if (_parameters[i].Type == StrategyParameterType.Bool)
                    {
                        _gridParameters.Rows.Add(GetRowBool(_parameters[i]));
                    }
                    else if (_parameters[i].Type == StrategyParameterType.String)
                    {
                        _gridParameters.Rows.Add(GetRowString(_parameters[i]));
                    }
                    else if (_parameters[i].Type == StrategyParameterType.Int)
                    {
                        _gridParameters.Rows.Add(GetRowInt(_parameters[i], _parametersActive[i]));
                    }
                    else if (_parameters[i].Type == StrategyParameterType.Decimal)
                    {
                        _gridParameters.Rows.Add(GetRowDecimal(_parameters[i], _parametersActive[i]));
                    }
                    else if (_parameters[i].Type == StrategyParameterType.CheckBox)
                    {
                        _gridParameters.Rows.Add(GetRowCheckBox(_parameters[i]));
                    }
                    else if (_parameters[i].Type == StrategyParameterType.TimeOfDay)
                    {
                        _gridParameters.Rows.Add(GetRowTimeOfDay(_parameters[i]));
                    }
                    else if (_parameters[i].Type == StrategyParameterType.DecimalCheckBox)
                    {
                        _gridParameters.Rows.Add(GetRowDecimalCheckBox(_parameters[i], _parametersActive[i]));
                    }
                    else //if (_parameters[i].Type == StrategyParameterType.Label)
                    {// не известный или не реализованный параметр
                        continue;
                    }
                }

                _gridParameters.CellValueChanged += _gridParameters_CellValueChanged;
                _gridParameters.CellClick += _gridParameters_CellClick;
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _gridParameters_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int columnIndx = e.ColumnIndex;
            _lastRowClickParamGridNum = e.RowIndex;

            if (columnIndx == 0)
            {
                Task.Run(StopRedactTableTask);
            }
        }

        private int _lastRowClickParamGridNum;

        private async void StopRedactTableTask()
        {
            await Task.Delay(700);
            StopRedactTableAction();
        }

        private void StopRedactTableAction()
        {
            if (_gridParameters.InvokeRequired)
            {
                _gridParameters.Invoke(new Action(StopRedactTableAction));
                return;
            }

            try
            {
                if (_gridParameters.Rows.Count == 0 ||
                    _gridParameters.Rows[0].Cells == null ||
                    _gridParameters.Rows[0].Cells.Count < 2)
                {
                    return;
                }

                if (_lastRowClickParamGridNum >= _gridParameters.Rows.Count)
                {
                    return;
                }

                _gridParameters.Rows[_lastRowClickParamGridNum].Cells[1].Selected = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private DataGridViewRow GetRowBool(IIStrategyParameter parameter)
        {
            DataGridViewRow row = new DataGridViewRow();

            // 0 on / off
            row.Cells.Add(new DataGridViewCheckBoxCell());
            row.Cells[0].ReadOnly = true;
            row.Cells[0].Style.BackColor = System.Drawing.Color.DimGray;
            row.Cells[0].Style.SelectionBackColor = System.Drawing.Color.DimGray;

            // 1 Param Name by User
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[1].Value = parameter.Name;

            // 2 Param Type
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[2].Value = parameter.Type;

            // 3 Param Defoult Value

            DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();
            cell.Items.Add("False");
            cell.Items.Add("True");
            cell.Value = ((StrategyParameterBool)parameter).ValueBool.ToString();
            row.Cells.Add(cell);

            // 4 Start optimize value

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[4].ReadOnly = true;
            row.Cells[4].Style.BackColor = System.Drawing.Color.DimGray;
            row.Cells[4].Style.SelectionBackColor = System.Drawing.Color.DimGray;

            // 5 Step optimize

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[5].ReadOnly = true;
            row.Cells[5].Style.BackColor = System.Drawing.Color.DimGray;
            row.Cells[5].Style.SelectionBackColor = System.Drawing.Color.DimGray;

            // 6 Step optimize

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[6].ReadOnly = true;
            row.Cells[6].Style.BackColor = System.Drawing.Color.DimGray;
            row.Cells[6].Style.SelectionBackColor = System.Drawing.Color.DimGray;

            return row;
        }

        private DataGridViewRow GetRowCheckBox(IIStrategyParameter parameter)
        {
            DataGridViewRow row = new DataGridViewRow();

            // 0 on / off
            row.Cells.Add(new DataGridViewCheckBoxCell());
            row.Cells[0].ReadOnly = true;
            row.Cells[0].Style.BackColor = System.Drawing.Color.DimGray;
            row.Cells[0].Style.SelectionBackColor = System.Drawing.Color.DimGray;

            // 1 Param Name by User
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[1].Value = parameter.Name;

            // 2 Param Type
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[2].Value = parameter.Type;

            // 3 Param Defoult Value

            DataGridViewCheckBoxCell cell = new DataGridViewCheckBoxCell();

            cell.FlatStyle = FlatStyle.Standard;
            cell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            if (((StrategyParameterCheckBox)parameter).CheckState == CheckState.Checked)
            {
                cell.Value = true;
            }
            else
            {
                cell.Value = false;
            }

            row.Cells.Add(cell);

            // 4 Start optimize value

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[4].ReadOnly = true;
            row.Cells[4].Style.BackColor = System.Drawing.Color.DimGray;
            row.Cells[4].Style.SelectionBackColor = System.Drawing.Color.DimGray;

            // 5 Step optimize

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[5].ReadOnly = true;
            row.Cells[5].Style.BackColor = System.Drawing.Color.DimGray;
            row.Cells[5].Style.SelectionBackColor = System.Drawing.Color.DimGray;

            // 6 Step optimize

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[6].ReadOnly = true;
            row.Cells[6].Style.BackColor = System.Drawing.Color.DimGray;
            row.Cells[6].Style.SelectionBackColor = System.Drawing.Color.DimGray;

            return row;
        }

        private DataGridViewRow GetRowTimeOfDay(IIStrategyParameter parameter)
        {
            DataGridViewRow row = new DataGridViewRow();

            // 0 on / off
            row.Cells.Add(new DataGridViewCheckBoxCell());
            row.Cells[0].ReadOnly = true;
            row.Cells[0].Style.BackColor = System.Drawing.Color.DimGray;
            row.Cells[0].Style.SelectionBackColor = System.Drawing.Color.DimGray;

            // 1 Param Name by User
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[1].Value = parameter.Name;

            // 2 Param Type
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[2].Value = parameter.Type;

            // 3 Param Defoult Value

            DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
            StrategyParameterTimeOfDay param = (StrategyParameterTimeOfDay)parameter;
            cell.Value = param.Value.ToString();
            row.Cells.Add(cell);

            // 4 Start optimize value

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[4].ReadOnly = true;
            row.Cells[4].Style.BackColor = System.Drawing.Color.DimGray;
            row.Cells[4].Style.SelectionBackColor = System.Drawing.Color.DimGray;

            // 5 Step optimize

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[5].ReadOnly = true;
            row.Cells[5].Style.BackColor = System.Drawing.Color.DimGray;
            row.Cells[5].Style.SelectionBackColor = System.Drawing.Color.DimGray;

            // 6 Step optimize

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[6].ReadOnly = true;
            row.Cells[6].Style.BackColor = System.Drawing.Color.DimGray;
            row.Cells[6].Style.SelectionBackColor = System.Drawing.Color.DimGray;

            return row;
        }

        private DataGridViewRow GetRowInt(IIStrategyParameter parameter, bool isOptimize)
        {
            DataGridViewRow row = new DataGridViewRow();

            // 0 on / off
            row.Cells.Add(new DataGridViewCheckBoxCell());
            row.Cells[0].ReadOnly = false;
            row.Cells[0].Value = isOptimize;

            // 1 Param Name by User
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[1].Value = parameter.Name;

            // 2 Param Type
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[2].Value = parameter.Type;

            // 3 Param Defoult Value

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[3].Value = ((StrategyParameterInt)parameter).ValueIntDefolt.ToString();

            if (isOptimize == true)
            {
                row.Cells[3].ReadOnly = false;
                row.Cells[3].Style.BackColor = System.Drawing.Color.DimGray;
                row.Cells[3].Style.SelectionBackColor = System.Drawing.Color.DimGray;
            }

            // 4 Start optimize value

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[4].Value = ((StrategyParameterInt)parameter).ValueIntStart.ToString();

            if (isOptimize == false)
            {
                row.Cells[4].ReadOnly = true;
                row.Cells[4].Style.BackColor = System.Drawing.Color.DimGray;
                row.Cells[4].Style.SelectionBackColor = System.Drawing.Color.DimGray;
            }

            // 5 Step optimize

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[5].Value = ((StrategyParameterInt)parameter).ValueIntStep.ToString();


            if (isOptimize == false)
            {
                row.Cells[5].ReadOnly = true;
                row.Cells[5].Style.BackColor = System.Drawing.Color.DimGray;
                row.Cells[5].Style.SelectionBackColor = System.Drawing.Color.DimGray;
            }

            // 6 Step optimize

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[6].Value = ((StrategyParameterInt)parameter).ValueIntStop.ToString();

            if (isOptimize == false)
            {
                row.Cells[6].ReadOnly = true;
                row.Cells[6].Style.BackColor = System.Drawing.Color.DimGray;
                row.Cells[6].Style.SelectionBackColor = System.Drawing.Color.DimGray;
            }

            return row;
        }

        private DataGridViewRow GetRowDecimal(IIStrategyParameter parameter, bool isOptimize)
        {
            DataGridViewRow row = new DataGridViewRow();
            // 0 on / off
            row.Cells.Add(new DataGridViewCheckBoxCell());
            row.Cells[0].ReadOnly = false;
            row.Cells[0].Value = isOptimize;

            // 1 Param Name by User
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[1].Value = parameter.Name;

            // 2 Param Type
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[2].Value = parameter.Type;

            // 3 Param Defoult Value

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[3].Value = ((StrategyParameterDecimal)parameter).ValueDecimalDefolt.ToString();

            if (isOptimize == true)
            {
                row.Cells[3].ReadOnly = false;
                row.Cells[3].Style.BackColor = System.Drawing.Color.DimGray;
                row.Cells[3].Style.SelectionBackColor = System.Drawing.Color.DimGray;
            }

            // 4 Start optimize value

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[4].Value = ((StrategyParameterDecimal)parameter).ValueDecimalStart.ToString();

            if (isOptimize == false)
            {
                row.Cells[4].ReadOnly = true;
                row.Cells[4].Style.BackColor = System.Drawing.Color.DimGray;
                row.Cells[4].Style.SelectionBackColor = System.Drawing.Color.DimGray;
            }

            // 5 Step optimize

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[5].Value = ((StrategyParameterDecimal)parameter).ValueDecimalStep.ToString();


            if (isOptimize == false)
            {
                row.Cells[5].ReadOnly = true;
                row.Cells[5].Style.BackColor = System.Drawing.Color.DimGray;
                row.Cells[5].Style.SelectionBackColor = System.Drawing.Color.DimGray;
            }

            // 6 Step optimize

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[6].Value = ((StrategyParameterDecimal)parameter).ValueDecimalStop.ToString();

            if (isOptimize == false)
            {
                row.Cells[6].ReadOnly = true;
                row.Cells[6].Style.BackColor = System.Drawing.Color.DimGray;
                row.Cells[6].Style.SelectionBackColor = System.Drawing.Color.DimGray;
            }

            return row;
        }

        private DataGridViewRow GetRowDecimalCheckBox(IIStrategyParameter parameter, bool isOptimize)
        {
            DataGridViewRow row = new DataGridViewRow();
            // 0 on / off
            row.Cells.Add(new DataGridViewCheckBoxCell());
            row.Cells[0].ReadOnly = false;
            row.Cells[0].Value = isOptimize;

            // 1 Param Name by User
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[1].Value = parameter.Name;

            // 2 Param Type
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[2].Value = parameter.Type;

            // 3 Param Defoult Value

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[3].Value = ((StrategyParameterDecimalCheckBox)parameter).ValueDecimalDefolt.ToString();

            if (isOptimize == true)
            {
                row.Cells[3].ReadOnly = false;
                row.Cells[3].Style.BackColor = System.Drawing.Color.DimGray;
                row.Cells[3].Style.SelectionBackColor = System.Drawing.Color.DimGray;
            }

            // 4 Start optimize value

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[4].Value = ((StrategyParameterDecimalCheckBox)parameter).ValueDecimalStart.ToString();

            if (isOptimize == false)
            {
                row.Cells[4].ReadOnly = true;
                row.Cells[4].Style.BackColor = System.Drawing.Color.DimGray;
                row.Cells[4].Style.SelectionBackColor = System.Drawing.Color.DimGray;
            }

            // 5 Step optimize

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[5].Value = ((StrategyParameterDecimalCheckBox)parameter).ValueDecimalStep.ToString();

            if (isOptimize == false)
            {
                row.Cells[5].ReadOnly = true;
                row.Cells[5].Style.BackColor = System.Drawing.Color.DimGray;
                row.Cells[5].Style.SelectionBackColor = System.Drawing.Color.DimGray;
            }

            // 6 Step optimize

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[6].Value = ((StrategyParameterDecimalCheckBox)parameter).ValueDecimalStop.ToString();

            if (isOptimize == false)
            {
                row.Cells[6].ReadOnly = true;
                row.Cells[6].Style.BackColor = System.Drawing.Color.DimGray;
                row.Cells[6].Style.SelectionBackColor = System.Drawing.Color.DimGray;
            }

            DataGridViewCheckBoxCell cell = new DataGridViewCheckBoxCell();

            cell.FlatStyle = FlatStyle.Standard;
            cell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            if (((StrategyParameterDecimalCheckBox)parameter).CheckState == CheckState.Checked)
            {
                cell.Value = true;
            }
            else
            {
                cell.Value = false;
            }

            row.Cells.Add(cell);

            row.Cells[7].ReadOnly = false;
            row.Cells[7].Style.BackColor = System.Drawing.Color.DimGray;
            row.Cells[7].Style.SelectionBackColor = System.Drawing.Color.DimGray;

            return row;
        }

        private DataGridViewRow GetRowString(IIStrategyParameter parameter)
        {
            DataGridViewRow row = new DataGridViewRow();

            // 0 on / off
            row.Cells.Add(new DataGridViewCheckBoxCell());
            row.Cells[0].ReadOnly = true;
            row.Cells[0].Style.BackColor = System.Drawing.Color.DimGray;
            row.Cells[0].Style.SelectionBackColor = System.Drawing.Color.DimGray;

            // 1 Param Name by User
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[1].Value = parameter.Name;

            // 2 Param Type
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[2].Value = parameter.Type;

            // 3 Param Defoult Value

            StrategyParameterString param = (StrategyParameterString)parameter;

            if (param.ValuesString.Count > 1)
            {
                DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();

                string actualValue = param.ValueString;
                bool isInArray = false;

                for (int i2 = 0; i2 < param.ValuesString.Count; i2++)
                {
                    cell.Items.Add(param.ValuesString[i2]);
                    if (param.ValuesString[i2].Equals(actualValue))
                    {
                        isInArray = true;
                    }
                }
                if (isInArray)
                {
                    cell.Value = param.ValueString;
                }
                else
                {
                    cell.Value = param.ValuesString[0];
                }

                row.Cells.Add(cell);
            }
            else if (param.ValuesString.Count == 1)
            {
                DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                cell.Value = param.ValueString;
                row.Cells.Add(cell);
            }
            else
            {
                DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                cell.Value = "";
                row.Cells.Add(cell);
            }

            // 4 Start optimize value

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[4].ReadOnly = true;
            row.Cells[4].Style.BackColor = System.Drawing.Color.DimGray;
            row.Cells[4].Style.SelectionBackColor = System.Drawing.Color.DimGray;

            // 5 Step optimize

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[5].ReadOnly = true;
            row.Cells[5].Style.BackColor = System.Drawing.Color.DimGray;
            row.Cells[5].Style.SelectionBackColor = System.Drawing.Color.DimGray;

            // 6 Step optimize

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[6].ReadOnly = true;
            row.Cells[6].Style.BackColor = System.Drawing.Color.DimGray;
            row.Cells[6].Style.SelectionBackColor = System.Drawing.Color.DimGray;

            return row;
        }

        private void SaveParametersFromTable()
        {
            if (_parameters == null)
            {
                return;
            }

            try
            {

                for (int i_param = 0, i_grid = 0; i_param < _parameters.Count; i_param++, i_grid++)
                {
                    IIStrategyParameter parameter = _parameters[i_param];
                    DataGridViewRow row = _gridParameters.Rows[i_grid];

                    if (parameter.Type == StrategyParameterType.String)
                    {
                        ((StrategyParameterString)parameter).ValueString = row.Cells[3].Value.ToString();
                        _parametersActive[i_param] = false;
                    }
                    else if (parameter.Type == StrategyParameterType.Bool)
                    {
                        ((StrategyParameterBool)parameter).ValueBool = Convert.ToBoolean(row.Cells[3].Value.ToString());
                        _parametersActive[i_param] = false;
                    }
                    else if (parameter.Type == StrategyParameterType.CheckBox)
                    {
                        DataGridViewCheckBoxCell cell = (DataGridViewCheckBoxCell)row.Cells[3];

                        bool isChecked = Convert.ToBoolean(cell.Value.ToString());

                        if (isChecked)
                        {
                            ((StrategyParameterCheckBox)parameter).CheckState = CheckState.Checked;
                        }
                        else
                        {
                            ((StrategyParameterCheckBox)parameter).CheckState = CheckState.Unchecked;
                        }

                        _parametersActive[i_param] = false;
                    }
                    else if (parameter.Type == StrategyParameterType.TimeOfDay)
                    {
                        TimeOfDay tD = new TimeOfDay();
                        tD.LoadFromString(row.Cells[3].Value.ToString());

                        ((StrategyParameterTimeOfDay)parameter).Value = tD;
                        _parametersActive[i_param] = false;
                    }
                    else if (parameter.Type == StrategyParameterType.Int)
                    {
                        int valueDefoult = Convert.ToInt32(row.Cells[3].Value);
                        int valueStart = Convert.ToInt32(row.Cells[4].Value);
                        int valueStep = Convert.ToInt32(row.Cells[5].Value);
                        int valueStop = Convert.ToInt32(row.Cells[6].Value);

                        if (valueStart > valueStop)
                        {
                            MessageBox.Show(OsLocalization.Optimizer.Message34);
                            PaintTableParameters();
                            return;
                        }

                        StrategyParameterInt param = ((StrategyParameterInt)parameter);

                        if (valueStart != param.ValueIntStart ||
                            valueStep != param.ValueIntStep ||
                            valueStop != param.ValueIntStop ||
                            valueDefoult != param.ValueIntDefolt)
                        {
                            _parameters.Insert(i_param, new StrategyParameterInt(parameter.Name, valueDefoult,
                                valueStart, valueStop, valueStep));
                            _parameters.RemoveAt(i_param + 1);
                        }

                        DataGridViewCheckBoxCell box = (DataGridViewCheckBoxCell)row.Cells[0];
                        if (row.Cells[0].Value == null ||
                            (bool)row.Cells[0].Value == false)
                        {
                            _parametersActive[i_param] = false;
                            UnActivateRowOptimizing(row);
                        }
                        else
                        {
                            _parametersActive[i_param] = true;
                            ActivateRowOptimizing(row);
                        }
                    }
                    else if (parameter.Type == StrategyParameterType.Decimal)
                    {
                        decimal valueDefoult = row.Cells[3].Value.ToString().ToDecimal();
                        decimal valueStart = row.Cells[4].Value.ToString().ToDecimal();
                        decimal valueStep = row.Cells[5].Value.ToString().ToDecimal();
                        decimal valueStop = row.Cells[6].Value.ToString().ToDecimal();

                        if (valueStart > valueStop)
                        {
                            MessageBox.Show(OsLocalization.Optimizer.Message34);
                            PaintTableParameters();
                            return;
                        }

                        StrategyParameterDecimal param = ((StrategyParameterDecimal)parameter);

                        if (valueStart != param.ValueDecimalStart ||
                            valueStep != param.ValueDecimalStep ||
                            valueStop != param.ValueDecimalStop ||
                            valueDefoult != param.ValueDecimalDefolt)
                        {
                            _parameters.Insert(i_param, new StrategyParameterDecimal(parameter.Name, valueDefoult,
                               valueStart, valueStop, valueStep));
                            _parameters.RemoveAt(i_param + 1);
                        }
                        if (row.Cells[0].Value == null ||
                            (bool)row.Cells[0].Value == false)
                        {
                            _parametersActive[i_param] = false;
                            UnActivateRowOptimizing(row);
                        }
                        else
                        {
                            _parametersActive[i_param] = true;
                            ActivateRowOptimizing(row);
                        }
                    }
                    else if (parameter.Type == StrategyParameterType.DecimalCheckBox)
                    {
                        decimal valueDefoult = row.Cells[3].Value.ToString().ToDecimal();
                        decimal valueStart = row.Cells[4].Value.ToString().ToDecimal();
                        decimal valueStep = row.Cells[5].Value.ToString().ToDecimal();
                        decimal valueStop = row.Cells[6].Value.ToString().ToDecimal();
                        bool isChecked = Convert.ToBoolean(row.Cells[7].Value.ToString());

                        if (isChecked)
                        {
                            ((StrategyParameterDecimalCheckBox)parameter).CheckState = CheckState.Checked;
                        }
                        else
                        {
                            ((StrategyParameterDecimalCheckBox)parameter).CheckState = CheckState.Unchecked;
                        }

                        if (valueStart > valueStop)
                        {
                            MessageBox.Show(OsLocalization.Optimizer.Message34);
                            PaintTableParameters();
                            return;
                        }

                        StrategyParameterDecimalCheckBox param = ((StrategyParameterDecimalCheckBox)parameter);

                        if (valueStart != param.ValueDecimalStart ||
                            valueStep != param.ValueDecimalStep ||
                            valueStop != param.ValueDecimalStop ||
                            valueDefoult != param.ValueDecimalDefolt)
                        {
                            _parameters.Insert(i_param, new StrategyParameterDecimalCheckBox(parameter.Name, valueDefoult,
                               valueStart, valueStop, valueStep, true));
                            _parameters.RemoveAt(i_param + 1);
                        }
                        if (row.Cells[0].Value == null ||
                            (bool)row.Cells[0].Value == false)
                        {
                            _parametersActive[i_param] = false;
                            UnActivateRowOptimizing(row);
                        }
                        else
                        {
                            _parametersActive[i_param] = true;
                            ActivateRowOptimizing(row);
                        }
                    }
                    else //if (parameter.Type == StrategyParameterType.Label)
                    {//неизвестный или не реализованный параметр
                        i_grid--;
                        continue;
                    }
                }
                _master.SaveStandardParameters();
            }
            catch (Exception)
            {
                PaintTableParameters();
            }
        }

        private void ActivateRowOptimizing(DataGridViewRow row)
        {
            row.Cells[3].ReadOnly = true;
            row.Cells[3].Style.BackColor = System.Drawing.Color.DimGray;
            row.Cells[3].Style.SelectionBackColor = System.Drawing.Color.DimGray;

            row.Cells[4].ReadOnly = false;
            row.Cells[4].Style.BackColor = System.Drawing.Color.FromArgb(21, 26, 30);
            row.Cells[4].Style.SelectionBackColor = System.Drawing.Color.FromArgb(17, 18, 23);

            row.Cells[5].ReadOnly = false;
            row.Cells[5].Style.BackColor = System.Drawing.Color.FromArgb(21, 26, 30);
            row.Cells[5].Style.SelectionBackColor = System.Drawing.Color.FromArgb(17, 18, 23);

            row.Cells[6].ReadOnly = false;
            row.Cells[6].Style.BackColor = System.Drawing.Color.FromArgb(21, 26, 30);
            row.Cells[6].Style.SelectionBackColor = System.Drawing.Color.FromArgb(17, 18, 23);
        }

        private void UnActivateRowOptimizing(DataGridViewRow row)
        {
            row.Cells[3].ReadOnly = false;
            row.Cells[3].Style.BackColor = System.Drawing.Color.FromArgb(21, 26, 30);
            row.Cells[3].Style.SelectionBackColor = System.Drawing.Color.FromArgb(17, 18, 23);

            row.Cells[4].ReadOnly = true;
            row.Cells[4].Style.BackColor = System.Drawing.Color.DimGray;
            row.Cells[4].Style.SelectionBackColor = System.Drawing.Color.DimGray;

            row.Cells[5].ReadOnly = true;
            row.Cells[5].Style.BackColor = System.Drawing.Color.DimGray;
            row.Cells[5].Style.SelectionBackColor = System.Drawing.Color.DimGray;

            row.Cells[6].ReadOnly = true;
            row.Cells[6].Style.BackColor = System.Drawing.Color.DimGray;
            row.Cells[6].Style.SelectionBackColor = System.Drawing.Color.DimGray;
        }

        private void _gridParameters_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            SaveParametersFromTable();
            Task.Run(new Action(PaintCountBotsInOptimization));
        }

        private object _locker = new object();

        private void PaintCountBotsInOptimization()
        {
            lock (_locker)
            {
                int botCount = _master.GetMaxBotsCount();
                PaintBotsCount(botCount);
            }
        }

        private void PaintBotsCount(int value)
        {
            if (LabelIteartionCountNumber.Dispatcher.CheckAccess() == false)
            {
                LabelIteartionCountNumber.Dispatcher.Invoke(new Action<int>(PaintBotsCount), value);
                return;
            }

            LabelIteartionCountNumber.Content = value.ToString();

        }

        private void ButtonSetStandardParameters_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                List<IIStrategyParameter> par = _master.ParametersStandard;
                _master.SaveStandardParameters();
                _parameters = par;
                ReloadStrategy();
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.Message.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Phase table for switching after testing

        private DataGridView _gridFazesEnd;

        private void CreateTableFazes()
        {
            _gridFazesEnd = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);
            _gridFazesEnd.ScrollBars = ScrollBars.Vertical;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridFazesEnd.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Optimizer.Message23;
            column0.ReadOnly = true;
            column0.Width = 100;

            _gridFazesEnd.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Optimizer.Message24;
            column1.ReadOnly = true;
            column1.Width = 150;

            _gridFazesEnd.Columns.Add(column1);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = OsLocalization.Optimizer.Message25;
            column.ReadOnly = true;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridFazesEnd.Columns.Add(column);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = OsLocalization.Optimizer.Message26;
            column2.ReadOnly = true;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridFazesEnd.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = OsLocalization.Optimizer.Message27;
            column3.ReadOnly = true;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridFazesEnd.Columns.Add(column3);

            _gridFazesEnd.Rows.Add(null, null);

            WindowsFormsHostFazeNumOnTubResult.Child = _gridFazesEnd;

            _gridFazesEnd.CellClick += _gridFazesEnd_CellClick;
            _gridFazesEnd.DataError += _gridSources_DataError;
        }

        private void PaintTableFazes()
        {
            if (_gridFazesEnd.InvokeRequired)
            {
                _gridFazesEnd.Invoke(new Action(PaintTableFazes));
                return;
            }

            _gridFazesEnd.Rows.Clear();

            List<OptimizerFaze> fazes = _master.Fazes;

            if (fazes == null ||
                fazes.Count == 0)
            {
                return;
            }

            for (int i = 0; i < fazes.Count; i++)
            {
                DataGridViewRow row = new DataGridViewRow();

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = i + 1;

                DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                cell.Value = fazes[i].TypeFaze;
                row.Cells.Add(cell);

                DataGridViewTextBoxCell cell2 = new DataGridViewTextBoxCell();
                cell2.Value = fazes[i].TimeStart.ToString(OsLocalization.ShortDateFormatString);
                row.Cells.Add(cell2);

                DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell();
                cell3.Value = fazes[i].TimeEnd.ToString(OsLocalization.ShortDateFormatString);
                row.Cells.Add(cell3);

                DataGridViewTextBoxCell cell4 = new DataGridViewTextBoxCell();
                cell4.Value = fazes[i].Days;
                row.Cells.Add(cell4);

                _gridFazesEnd.Rows.Add(row);
            }
        }

        private void _gridFazesEnd_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            PaintTableResults();
            PaintSeriesResultsChart();
        }

        #endregion

        #region Optimization results table

        private DataGridView _gridResults;

        private void CreateTableResults()
        {
            _gridResults = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.ColumnHeaderSelect,
                DataGridViewAutoSizeRowsMode.None);

            _gridResults.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridResults.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = "Bot Name";
            column0.ReadOnly = true;
            column0.Width = 150;

            _gridResults.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = "Parameters";
            column1.ReadOnly = false;
            column1.Width = 150;
            _gridResults.Columns.Add(column1);

            DataGridViewColumn column21 = new DataGridViewColumn();
            column21.CellTemplate = cell0;
            column21.HeaderText = "Pos Count";
            column21.ReadOnly = false;
            column21.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column21);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = "Total Profit";
            column2.ReadOnly = false;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = "Max Drow Dawn";
            column3.ReadOnly = false;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = "Average Profit";
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column4);

            DataGridViewColumn column5 = new DataGridViewColumn();
            column5.CellTemplate = cell0;
            column5.HeaderText = "Average Profit %";
            column5.ReadOnly = false;
            column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column5);

            DataGridViewColumn column6 = new DataGridViewColumn();
            column6.CellTemplate = cell0;
            column6.HeaderText = "Profit Factor";
            column6.ReadOnly = false;
            column6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column6);

            DataGridViewColumn column7 = new DataGridViewColumn();
            column7.CellTemplate = cell0;
            column7.HeaderText = "Pay Off Ratio";
            column7.ReadOnly = false;
            column7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column7);

            DataGridViewColumn column8 = new DataGridViewColumn();
            column8.CellTemplate = cell0;
            column8.HeaderText = "Recovery";
            column8.ReadOnly = false;
            column8.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column8);

            DataGridViewColumn column9 = new DataGridViewColumn();
            column9.CellTemplate = cell0;
            column9.HeaderText = "Sharpe Ratio";
            column9.ReadOnly = false;
            column9.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column9);

            DataGridViewButtonColumn column11 = new DataGridViewButtonColumn();
            column11.CellTemplate = new DataGridViewButtonCell();
            //column11.HeaderText = OsLocalization.Optimizer.Message40;
            column11.ReadOnly = true;
            column11.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _gridResults.Columns.Add(column11);

            DataGridViewButtonColumn column12 = new DataGridViewButtonColumn();
            column12.CellTemplate = new DataGridViewButtonCell();
            //column12.HeaderText = OsLocalization.Optimizer.Message42;
            column12.ReadOnly = true;
            column12.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _gridResults.Columns.Add(column12);

            _gridResults.Rows.Add(null, null);

            WindowsFormsHostResults.Child = _gridResults;
            _gridResults.DataError += _gridSources_DataError;
        }

        private void UpdateHeaders()
        {
            _gridResults.Columns[0].HeaderText = "Bot Name \n \n \n";

            if (_sortBotsType == SortBotsType.BotName)
            {
                _gridResults.Columns[0].HeaderText += " vvv";
            }

            _gridResults.Columns[2].HeaderText = "Pos Count";

            System.Drawing.Color cellColor = System.Drawing.Color.Black;

            for (int i = 0; i < _gridResults.Columns.Count; i++)
            {
                _gridResults.Columns[i].HeaderCell.Style.BackColor = _gridResults.Columns[i].HeaderCell.Style.SelectionBackColor;
            }

            if (_sortBotsType == SortBotsType.PositionCount)
            {
                _gridResults.Columns[2].HeaderText += " vvv";
                _gridResults.Columns[2].HeaderCell.Style.BackColor = cellColor;
            }

            _gridResults.Columns[3].HeaderText = "Total Profit";

            if (_sortBotsType == SortBotsType.TotalProfit)
            {
                _gridResults.Columns[3].HeaderText += " vvv";
                _gridResults.Columns[3].HeaderCell.Style.BackColor = cellColor;
            }

            _gridResults.Columns[4].HeaderText = "Max Drow Dawn";
            if (_sortBotsType == SortBotsType.MaxDrawDawn)
            {
                _gridResults.Columns[4].HeaderText += " vvv";
                _gridResults.Columns[4].HeaderCell.Style.BackColor = cellColor;
            }

            _gridResults.Columns[5].HeaderText = "Average Profit";
            if (_sortBotsType == SortBotsType.AverageProfit)
            {
                _gridResults.Columns[5].HeaderText += " vvv";
                _gridResults.Columns[5].HeaderCell.Style.BackColor = cellColor;
            }

            _gridResults.Columns[6].HeaderText = "Average Profit %";
            if (_sortBotsType == SortBotsType.AverageProfitPercent)
            {
                _gridResults.Columns[6].HeaderText += " vvv";
                _gridResults.Columns[6].HeaderCell.Style.BackColor = cellColor;
            }

            _gridResults.Columns[7].HeaderText = "Profit Factor";
            if (_sortBotsType == SortBotsType.ProfitFactor)
            {
                _gridResults.Columns[7].HeaderText += " vvv";
                _gridResults.Columns[7].HeaderCell.Style.BackColor = cellColor;
            }

            _gridResults.Columns[8].HeaderText = "Pay Off Ratio";
            if (_sortBotsType == SortBotsType.PayOffRatio)
            {
                _gridResults.Columns[8].HeaderText += " vvv";
                _gridResults.Columns[8].HeaderCell.Style.BackColor = cellColor;
            }

            _gridResults.Columns[9].HeaderText = "Recovery";
            if (_sortBotsType == SortBotsType.Recovery)
            {
                _gridResults.Columns[9].HeaderText += " vvv";
                _gridResults.Columns[9].HeaderCell.Style.BackColor = cellColor;
            }

            _gridResults.Columns[10].HeaderText = "Sharpe Ratio";
            if (_sortBotsType == SortBotsType.SharpRatio)
            {
                _gridResults.Columns[10].HeaderText += " vvv";
                _gridResults.Columns[10].HeaderCell.Style.BackColor = cellColor;
            }

        }

        private void PaintTableResults()
        {
            try
            {
                if (_gridResults == null)
                {
                    return;
                }

                if (_gridResults.InvokeRequired)
                {
                    _gridResults.Invoke(new Action(PaintTableResults));
                    return;
                }
                _gridResults.SelectionChanged -= _gridResults_SelectionChanged;
                _gridResults.CellMouseClick -= _gridResults_CellMouseClick;

                UpdateHeaders();

                _gridResults.Rows.Clear();

                if (_reports == null)
                {
                    return;
                }

                if (_gridFazesEnd.CurrentCell == null)
                {
                    return;
                }

                int num = 0;
                num = _gridFazesEnd.CurrentCell.RowIndex;

                if (num >= _reports.Count)
                {
                    return;
                }

                OptimizerFazeReport fazeReport = _reports[num];

                if (fazeReport == null)
                {
                    return;
                }

                List<DataGridViewRow> rows = new List<DataGridViewRow>();

                for (int i = 0; i < fazeReport.Reports.Count; i++)
                {
                    OptimizerReport report = fazeReport.Reports[i];
                    if (report == null ||
                        report.TabsReports.Count == 0 ||
                        !_master.IsAcceptedByFilter(report))
                    {
                        continue;
                    }

                    DataGridViewRow row = new DataGridViewRow();
                    row.Height = 30;
                    row.Cells.Add(new DataGridViewTextBoxCell());

                    //  if (report.TabsReports.Count == 1)
                    //  {
                    row.Cells[0].Value = report.BotName;
                    // }
                    // else
                    // {
                    //    row.Cells[0].Value = "Сводные";
                    //}

                    DataGridViewTextBoxCell cell2 = new DataGridViewTextBoxCell();
                    cell2.Value = report.GetParametersToDataTable();
                    row.Cells.Add(cell2);

                    DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell();
                    cell3.Value = report.PositionsCount;
                    row.Cells.Add(cell3);

                    DataGridViewTextBoxCell cell4 = new DataGridViewTextBoxCell();
                    cell4.Value = Math.Round(report.TotalProfit, 5).ToStringWithNoEndZero() + " (" + report.TotalProfitPercent.ToStringWithNoEndZero() + "%)";
                    row.Cells.Add(cell4);

                    DataGridViewTextBoxCell cell5 = new DataGridViewTextBoxCell();
                    cell5.Value = Math.Round(report.MaxDrawDawn, 5).ToStringWithNoEndZero();
                    row.Cells.Add(cell5);

                    DataGridViewTextBoxCell cell6 = new DataGridViewTextBoxCell();
                    cell6.Value = Math.Round(report.AverageProfit, 5).ToStringWithNoEndZero();
                    row.Cells.Add(cell6);

                    DataGridViewTextBoxCell cell7 = new DataGridViewTextBoxCell();
                    cell7.Value = Math.Round(report.AverageProfitPercentOneContract, 5).ToStringWithNoEndZero();
                    row.Cells.Add(cell7);

                    DataGridViewTextBoxCell cell8 = new DataGridViewTextBoxCell();
                    cell8.Value = Math.Round(report.ProfitFactor, 5).ToStringWithNoEndZero();
                    row.Cells.Add(cell8);

                    DataGridViewTextBoxCell cell9 = new DataGridViewTextBoxCell();
                    cell9.Value = Math.Round(report.PayOffRatio, 5).ToStringWithNoEndZero();
                    row.Cells.Add(cell9);

                    DataGridViewTextBoxCell cell10 = new DataGridViewTextBoxCell();
                    cell10.Value = Math.Round(report.Recovery, 5).ToStringWithNoEndZero();
                    row.Cells.Add(cell10);

                    DataGridViewTextBoxCell cell11 = new DataGridViewTextBoxCell();
                    cell11.Value = Math.Round(report.SharpRatio, 5).ToStringWithNoEndZero();
                    row.Cells.Add(cell11);

                    DataGridViewButtonCell cell12 = new DataGridViewButtonCell();
                    cell12.Value = OsLocalization.Optimizer.Message40;
                    row.Cells.Add(cell12);

                    DataGridViewButtonCell cell13 = new DataGridViewButtonCell();
                    cell13.Value = OsLocalization.Optimizer.Message42;
                    row.Cells.Add(cell13);

                    rows.Add(row);
                }

                WindowsFormsHostResults.Child = null;

                if (rows.Count > 0)
                {
                    _gridResults.Rows.AddRange(rows.ToArray());
                }

                WindowsFormsHostResults.Child = _gridResults;

                _gridResults.SelectionChanged += _gridResults_SelectionChanged;
                _gridResults.CellMouseClick += _gridResults_CellMouseClick;
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(),LogMessageType.Error);
            }
        }

        private void _gridResults_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            if (e.ColumnIndex == 11)
            {
                ShowBotChartDialog(e);
            }

            if (e.ColumnIndex == 12)
            {
                ShowParametersDialog(e);
            }
        }

        private void ShowBotChartDialog(DataGridViewCellMouseEventArgs e)
        {
            OptimizerFazeReport fazeReport;

            if (_gridFazesEnd.CurrentCell == null ||
              _gridFazesEnd.CurrentCell.RowIndex == 0)
            {
                fazeReport = _reports[0];
            }
            else
            {
                if (_gridFazesEnd.CurrentCell.RowIndex > _reports.Count)
                {
                    return;
                }

                fazeReport = _reports[_gridFazesEnd.CurrentCell.RowIndex];
            }

            if (e.RowIndex >= fazeReport.Reports.Count)
            {
                return;
            }

            BotPanel bot = _master.TestBot(fazeReport, fazeReport.Reports[e.RowIndex]);

            BotPanelChartUi ui = bot.ShowChartDialog();

            ui.Closed += (sender, e) =>
            {
                try
                {
                    bot.Delete();
                }
                catch (Exception error)
                {
                    _master.SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            };
        }

        private void ShowParametersDialog(DataGridViewCellMouseEventArgs e)
        {
            OptimizerFazeReport fazeReport;

            if (_gridFazesEnd.CurrentCell == null ||
              _gridFazesEnd.CurrentCell.RowIndex == 0)
            {
                fazeReport = _reports[0];
            }
            else
            {
                if (_gridFazesEnd.CurrentCell.RowIndex > _reports.Count)
                {
                    return;
                }

                fazeReport = _reports[_gridFazesEnd.CurrentCell.RowIndex];
            }

            if (e.RowIndex >= fazeReport.Reports.Count)
            {
                return;
            }

            OptimizerBotParametersSimpleUi ui = new OptimizerBotParametersSimpleUi(fazeReport.Reports[e.RowIndex], fazeReport, _master.StrategyName);
            ui.Show();
        }

        private void _gridResults_SelectionChanged(object sender, EventArgs e)
        {
            if (_gridResults.SelectedCells.Count == 0)
            {
                return;
            }
            int columnSelect = _gridResults.SelectedCells[0].ColumnIndex;

            int rowSelect = _gridResults.SelectedCells[0].RowIndex;

            if (rowSelect != 0)
            {
                return;
            }

            SortBotsType currentSelection = SortBotsType.BotName;

            if (columnSelect == 0)
            {
                return;
            }
            else if (columnSelect == 2)
            {
                currentSelection = SortBotsType.PositionCount;
            }
            else if (columnSelect == 3)
            {
                currentSelection = SortBotsType.TotalProfit;
            }
            else if (columnSelect == 4)
            {
                currentSelection = SortBotsType.MaxDrawDawn;
            }
            else if (columnSelect == 5)
            {
                currentSelection = SortBotsType.AverageProfit;
            }
            else if (columnSelect == 6)
            {
                currentSelection = SortBotsType.AverageProfitPercent;
            }
            else if (columnSelect == 7)
            {
                currentSelection = SortBotsType.ProfitFactor;
            }
            else if (columnSelect == 8)
            {
                currentSelection = SortBotsType.PayOffRatio;
            }
            else if (columnSelect == 9)
            {
                currentSelection = SortBotsType.Recovery;
            }
            else if (columnSelect == 10)
            {
                currentSelection = SortBotsType.SharpRatio;
            }
            else
            {
                return;
            }

            if (currentSelection == _sortBotsType)
            {
                return;
            }

            _sortBotsType = currentSelection;

            try
            {
                for (int i = 0; i < _reports.Count; i++)
                {
                    OptimizerFazeReport.SortResults(_reports[i].Reports, _sortBotsType);
                }

                PaintTableResults();
                PaintSeriesResultsChart();
            }
            catch (Exception error)
            {
                _master.SendLogMessage(error.ToString(), LogMessageType.Error);
            }

        }

        private SortBotsType _sortBotsType;

        private void PaintBotInTable(string botName)
        {
            for (int i2 = 0; i2 < _gridResults.Rows.Count; i2++)
            {
                DataGridViewRow row = _gridResults.Rows[i2];

                if (row.Cells[0].Value.ToString() == botName)
                {
                    for (int i = 0; i < row.Cells.Count; i++)
                    {
                        row.Cells[i].Style.ForeColor = System.Drawing.Color.FromArgb(255, 83, 0);
                    }
                }
                else
                {
                    for (int i = 0; i < row.Cells.Count; i++)
                    {
                        row.Cells[i].Style = _gridResults.DefaultCellStyle;
                    }
                }
            }
        }

        #endregion

        #region Series results chart

        private Chart _chartSeriesResult;

        public void CreateChartSeriesResults()
        {
            _chartSeriesResult = new Chart();

            ChartArea area = new ChartArea("Prime");

            _chartSeriesResult.ChartAreas.Clear();
            _chartSeriesResult.ChartAreas.Add(area);
            _chartSeriesResult.BackColor = System.Drawing.Color.FromArgb(21, 26, 30);
            _chartSeriesResult.ChartAreas[0].AxisX.TitleForeColor = System.Drawing.Color.FromArgb(149, 159, 176);

            for (int i = 0; _chartSeriesResult.ChartAreas != null && i < _chartSeriesResult.ChartAreas.Count; i++)
            {
                _chartSeriesResult.ChartAreas[i].CursorX.IsUserSelectionEnabled = false;
                _chartSeriesResult.ChartAreas[i].CursorX.IsUserEnabled = true;
                _chartSeriesResult.ChartAreas[i].CursorX.LineColor = System.Drawing.Color.FromArgb(255, 83, 0);
                _chartSeriesResult.ChartAreas[i].CursorX.LineWidth = 2;
                _chartSeriesResult.ChartAreas[i].BackColor = System.Drawing.Color.FromArgb(21, 26, 30);
                _chartSeriesResult.ChartAreas[i].BorderColor = System.Drawing.Color.FromArgb(17, 18, 23);
                _chartSeriesResult.ChartAreas[i].CursorY.LineColor = System.Drawing.Color.FromArgb(149, 159, 176);

                foreach (var axe in _chartSeriesResult.ChartAreas[i].Axes)
                {
                    axe.LabelStyle.ForeColor = System.Drawing.Color.FromArgb(149, 159, 176);
                }
            }

            Series series = new Series();
            series.ChartType = SeriesChartType.Column;
            _chartSeriesResult.Series.Clear();
            _chartSeriesResult.Series.Add(series);

            WindowsFormsHostResultsChart.Child = _chartSeriesResult;

            _chartSeriesResult.SuppressExceptions = true;
            _chartSeriesResult.Click += _chartSeriesResult_Click;
        }

        private void _chartSeriesResult_Click(object sender, EventArgs e)
        {
            try
            {
                if (_chartSeriesResult.ChartAreas[0].CursorX.Position == Double.NaN)
                {
                    return;
                }

                if (_chartSeriesResult.Series == null
                    || _chartSeriesResult.Series.Count == 0
                    || _chartSeriesResult.Series[0].Points == null
                    || _chartSeriesResult.Series[0].Points.Count == 0)
                {
                    return;
                }

                int index = (int)_chartSeriesResult.ChartAreas[0].CursorX.Position;

                index--;

                for (int i = 0; i < _chartSeriesResult.Series[0].Points.Count; i++)
                {
                    if (index == i)
                    {
                        continue;
                    }
                    _chartSeriesResult.Series[0].Points[i].Label = null;
                    _chartSeriesResult.Series[0].Points[i].LabelForeColor = System.Drawing.Color.White;
                }

                if (index >= _chartSeriesResult.Series[0].Points.Count)
                {
                    return;
                }

                if (_chartSeriesResult.Series[0].Points[index].Label
                    != _chartSeriesResult.Series[0].Points[index].ToolTip)
                {
                    _chartSeriesResult.Series[0].Points[index].Label
                     = _chartSeriesResult.Series[0].Points[index].ToolTip;
                    string botName = _chartSeriesResult.Series[0].Points[index].ToolTip.Split('\n')[0];
                    PaintBotInTable(botName);
                }
                else
                {
                    _chartSeriesResult.ChartAreas[0].CursorX.Position = Double.NaN;
                    _chartSeriesResult.Series[0].Points[index].Label = null;
                    PaintBotInTable(" ");
                }
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void PaintSeriesResultsChart()
        {
            try
            {
                if (_chartSeriesResult.InvokeRequired)
                {
                    _chartSeriesResult.Invoke(new Action(PaintSeriesResultsChart));
                    return;
                }

                _chartSeriesResult.Series[0].Points.Clear();

                if (_reports == null)
                {
                    return;
                }

                if (_gridFazesEnd.CurrentCell == null)
                {
                    return;
                }

                int num = 0;
                num = _gridFazesEnd.CurrentCell.RowIndex;

                if (num >= _reports.Count)
                {
                    return;
                }

                OptimizerFazeReport fazeReport = _reports[num];

                if (fazeReport == null)
                {
                    return;
                }

                LabelSeriesResultChart.Content
                 = OsLocalization.Optimizer.Label67 + " "
                 + (num + 1) + " " + fazeReport.Faze.TypeFaze + ". "
                 + OsLocalization.Optimizer.Label39 + ": " + _sortBotsType;


                List<ChartOptimizationResultValue> values = new List<ChartOptimizationResultValue>();

                for (int i = 0; i < fazeReport.Reports.Count; i++)
                {
                    OptimizerReport report = fazeReport.Reports[i];

                    if (report == null ||
                        report.TabsReports.Count == 0 ||
                        !_master.IsAcceptedByFilter(report))
                    {
                        continue;
                    }

                    ChartOptimizationResultValue curReport = new ChartOptimizationResultValue();
                    values.Add(curReport);
                    curReport.BotName = report.BotName;
                    curReport.BotNum = report.BotNum;
                    curReport.Parameters = report.GetParametersToDataTable();

                    if (_sortBotsType == SortBotsType.PositionCount)
                    {
                        curReport.Value = report.PositionsCount;
                    }
                    else if (_sortBotsType == SortBotsType.TotalProfit)
                    {
                        curReport.Value = Math.Round(report.TotalProfitPercent, 5);
                    }
                    else if (_sortBotsType == SortBotsType.MaxDrawDawn)
                    {
                        curReport.Value = Math.Round(report.MaxDrawDawn, 5);
                    }
                    else if (_sortBotsType == SortBotsType.AverageProfit)
                    {
                        curReport.Value = Math.Round(report.AverageProfit, 5);
                    }
                    else if (_sortBotsType == SortBotsType.AverageProfitPercent)
                    {
                        curReport.Value = Math.Round(report.AverageProfitPercentOneContract, 5);
                    }
                    else if (_sortBotsType == SortBotsType.ProfitFactor)
                    {
                        curReport.Value = Math.Round(report.ProfitFactor, 5);
                    }
                    else if (_sortBotsType == SortBotsType.PayOffRatio)
                    {
                        curReport.Value = Math.Round(report.PayOffRatio, 5);
                    }
                    else if (_sortBotsType == SortBotsType.Recovery)
                    {
                        curReport.Value = Math.Round(report.Recovery, 5);
                    }
                    else if (_sortBotsType == SortBotsType.SharpRatio)
                    {
                        curReport.Value = Math.Round(report.SharpRatio, 5);
                    }
                }

                SetColorInOptimizationResultValues(values);

                values = values.OrderBy(x => x.BotNum).ToList();

                decimal max = decimal.MinValue;
                decimal min = decimal.MaxValue;

                for (int i = 0; i < values.Count; i++)
                {
                    ChartOptimizationResultValue curValue = values[i];

                    if (curValue.Value > max)
                    {
                        max = curValue.Value;
                    }
                    if (curValue.Value < min)
                    {
                        min = curValue.Value;
                    }

                    double valueToChart = Convert.ToDouble(Math.Round(curValue.Value, 4));

                    DataPoint point1 = new DataPoint(i + 1, valueToChart);
                    point1.ToolTip = curValue.ToolTip;
                    point1.Color = curValue.Color;

                    _chartSeriesResult.Series[0].Points.Add(point1);
                }

                max = Math.Round(max, 4);
                min = Math.Round(min, 4);

                if (max != decimal.MinValue
                    && max != 0
                    && min > 0)
                {
                    _chartSeriesResult.ChartAreas[0].AxisY.Maximum = Convert.ToDouble(max);
                    _chartSeriesResult.ChartAreas[0].AxisY.Minimum = 0;
                }
                else if (max != decimal.MinValue &&
                    min != decimal.MaxValue
                    && max != min)
                {
                    _chartSeriesResult.ChartAreas[0].AxisY.Maximum = Convert.ToDouble(max);
                    _chartSeriesResult.ChartAreas[0].AxisY.Minimum = Convert.ToDouble(min);
                }

                // label

                if (_chartSeriesResult.ChartAreas[0].CursorX.Position != Double.NaN)
                {
                    for (int i = 0; i < _chartSeriesResult.Series[0].Points.Count; i++)
                    {
                        _chartSeriesResult.Series[0].Points[i].Label = null;
                        _chartSeriesResult.Series[0].Points[i].LabelForeColor = System.Drawing.Color.White;
                    }

                    int index = (int)_chartSeriesResult.ChartAreas[0].CursorX.Position;

                    index--;

                    if (index >= 0
                        && index < _chartSeriesResult.Series[0].Points.Count)
                    {
                        _chartSeriesResult.Series[0].Points[index].Label
                      = _chartSeriesResult.Series[0].Points[index].ToolTip;

                        string botName =
                            _chartSeriesResult.Series[0].Points[index].ToolTip.Split('\n')[0];
                        PaintBotInTable(botName);
                    }
                }
            }
            catch(Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void SetColorInOptimizationResultValues(List<ChartOptimizationResultValue> resultValues)
        {
            // 15% green
            // 15% dark green

            for (int i = 0; i < resultValues.Count; i++)
            {
                resultValues[i].Color = System.Drawing.Color.Gray;
            }

            if (resultValues.Count <= 1)
            {
                return;
            }

            List<ChartOptimizationResultValue> sortedValue = new List<ChartOptimizationResultValue>();

            sortedValue = resultValues.OrderBy(x => x.Value).ToList();
            sortedValue.Reverse();

            int first15PercentMaxNum = Convert.ToInt32(sortedValue.Count * 0.15m);
            int first30PercentMaxNum = Convert.ToInt32(sortedValue.Count * 0.30m);

            for (int i = 0; i < sortedValue.Count; i++)
            {
                if (i < first15PercentMaxNum)
                {
                    sortedValue[i].Color = System.Drawing.Color.Green;
                }
                else if (i < first30PercentMaxNum)
                {
                    sortedValue[i].Color = System.Drawing.Color.DarkGreen;
                }
                else
                {
                    resultValues[i].Color = System.Drawing.Color.Gray;
                }
            }
        }

        #endregion

        #region Checking robots ready for optimization

        private void ButtonStrategyReload_Click(object sender, RoutedEventArgs e)
        {
            BotFactory.NeedToReloadOptimizerBots = true;

            Task.Run(new Action(StrategyLoader));
        }

        private void StrategyLoader()
        {
            try
            {
                DateTime start = DateTime.Now;

                _master.SendLogMessage(OsLocalization.Optimizer.Message11, LogMessageType.System);

                List<string> strategiesInclude = BotFactory.GetNamesStrategyWithParametersSync();

                _master.SendLogMessage(OsLocalization.Optimizer.Message19 + " " + strategiesInclude.Count, LogMessageType.System);

                if (string.IsNullOrEmpty(_master.StrategyName))
                {
                    return;
                }

                ReloadStrategy();

                //TimeSpan resultTime = DateTime.Now - start;
                //_master.SendLogMessage("Reload bots type time: " + resultTime.ToString(), LogMessageType.System);

            }
            catch (Exception ex)
            {
                _master?.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

    }

    public enum SortBotsType
    {
        TotalProfit,

        BotName,

        PositionCount,

        MaxDrawDawn,

        AverageProfit,

        AverageProfitPercent,

        ProfitFactor,

        PayOffRatio,

        Recovery,

        SharpRatio
    }
}