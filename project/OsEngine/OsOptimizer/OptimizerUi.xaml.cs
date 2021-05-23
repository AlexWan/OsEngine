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

namespace OsEngine.OsOptimizer
{
    /// <summary>
    /// Interaction Logic for OptimizerUi.xaml
    /// Логика взаимодействия для OptimizerUi.xaml
    /// </summary>
    public partial class OptimizerUi
    {
        public OptimizerUi()
        {
            InitializeComponent();
            Thread.Sleep(200);

            _master = new OptimizerMaster();
            _master.StartPaintLog(HostLog);
            _master.NewSecurityEvent += _master_NewSecurityEvent;
            _master.DateTimeStartEndChange += _master_DateTimeStartEndChange;
            _master.TestReadyEvent += _master_TestReadyEvent;

            Task.Run(new Action(StrategyLoader));

            CreateTableTabsSimple();
            CreateTableTabsIndex();
            CreateTableResults();
            CreateTableFazes();
            CreateTableParametrs();
            CreateTableOptimizeFazes();

            for (int i = 1; i < 21; i++)
            {
                ComboBoxThreadsCount.Items.Add(i);
            }

            ComboBoxThreadsCount.SelectedItem = _master.ThreadsCount;
            CreateThradsProgressBars();
            ComboBoxThreadsCount.SelectionChanged += ComboBoxThreadsCount_SelectionChanged;

            TextBoxStartPortfolio.Text = _master.StartDepozit.ToString();
            TextBoxStartPortfolio.TextChanged += TextBoxStartPortfolio_TextChanged;

            CommissionTypeLabel.Content = OsLocalization.Optimizer.Label40;
            CommissionTypeComboBox.Items.Add(ComissionType.None.ToString());
            CommissionTypeComboBox.Items.Add(ComissionType.OneLotFix.ToString());
            CommissionTypeComboBox.Items.Add(ComissionType.Percent.ToString());
            CommissionTypeComboBox.SelectedItem = _master.CommissionType.ToString();
            CommissionTypeComboBox.SelectionChanged += CommissionTypeComboBoxOnSelectionChanged;

            CommissionValueLabel.Content = OsLocalization.Optimizer.Label41;
            CommissionValueTextBox.Text = _master.CommissionValue.ToString();
            CommissionValueTextBox.TextChanged += CommissionValueTextBoxOnTextChanged;

            // filters/фильтры
            CheckBoxFilterProfitIsOn.IsChecked = _master.FilterProfitIsOn;
            CheckBoxFilterMaxDrowDownIsOn.IsChecked = _master.FilterMaxDrowDownIsOn;
            CheckBoxFilterMiddleProfitIsOn.IsChecked = _master.FilterMiddleProfitIsOn;
            CheckBoxFilterProfitFactorIsOn.IsChecked = _master.FilterProfitFactorIsOn;
            CheckBoxFilterDealsCount.IsChecked = _master.FilterDealsCountIsOn;

            CheckBoxFilterProfitIsOn.Click += CheckBoxFilterIsOn_Click;
            CheckBoxFilterMaxDrowDownIsOn.Click += CheckBoxFilterIsOn_Click;
            CheckBoxFilterMiddleProfitIsOn.Click += CheckBoxFilterIsOn_Click;
            CheckBoxFilterProfitFactorIsOn.Click += CheckBoxFilterIsOn_Click;
            CheckBoxFilterDealsCount.Click += CheckBoxFilterIsOn_Click;

            TextBoxFilterProfitValue.Text = _master.FilterProfitValue.ToString();
            TextBoxMaxDrowDownValue.Text = _master.FilterMaxDrowDownValue.ToString();
            TextBoxFilterMiddleProfitValue.Text = _master.FilterMiddleProfitValue.ToString();
            TextBoxFilterProfitFactorValue.Text = _master.FilterProfitFactorValue.ToString();
            TextBoxFilterDealsCount.Text = _master.FilterDealsCountValue.ToString();

            TextBoxFilterProfitValue.TextChanged += TextBoxFiltertValue_TextChanged;
            TextBoxMaxDrowDownValue.TextChanged += TextBoxFiltertValue_TextChanged;
            TextBoxFilterMiddleProfitValue.TextChanged += TextBoxFiltertValue_TextChanged;
            TextBoxFilterProfitFactorValue.TextChanged += TextBoxFiltertValue_TextChanged;
            TextBoxFilterDealsCount.TextChanged += TextBoxFiltertValue_TextChanged;

            // Stages/Этапы

            DatePickerStart.DisplayDate = _master.TimeStart;
            DatePickerEnd.DisplayDate = _master.TimeEnd;
            TextBoxPercentFiltration.Text = _master.PercentOnFilration.ToString();

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
                }
                catch
                {
                    TextBoxIterationCount.Text = _master.IterationCount.ToString();
                }

            };

            _master.NeadToMoveUiToEvent += _master_NeadToMoveUiToEvent;
            TextBoxStrategyName.Text = _master.StrategyName;

            Task task = new Task(PainterProgressArea);
            task.Start();

            Label7.Content = OsLocalization.Optimizer.Label7;
            Label8.Content = OsLocalization.Optimizer.Label8;
            ButtonGo.Content = OsLocalization.Optimizer.Label9;
            TabItemControl.Header = OsLocalization.Optimizer.Label10;
            ButtonServerDialog.Content = OsLocalization.Optimizer.Label11;
            Label12.Content = OsLocalization.Optimizer.Label12;
            Label13.Content = OsLocalization.Optimizer.Label13;
            LabelTabsEndTimeFrames.Content = OsLocalization.Optimizer.Label14;
            Label15.Content = OsLocalization.Optimizer.Label15;
            TabItemParams.Header = OsLocalization.Optimizer.Label16;
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

            TabControlResultsSeries.Header = OsLocalization.Optimizer.Label37;
            TabControlResultsOutOfSampleResults.Header = OsLocalization.Optimizer.Label38;
            LabelSortBy.Content = OsLocalization.Optimizer.Label39;
            CheckBoxLastInSample.Content = OsLocalization.Optimizer.Label42;


            _resultsCharting = new OptimizerReportCharting(
                WindowsFormsHostDependences, WindowsFormsHostColumnsResults,
                WindowsFormsHostPieResults, ComboBoxSortDependencesResults,null,null);
            _resultsCharting.LogMessageEvent += _master.SendLogMessage;

            this.Closing += Ui_Closing;
        }

        void Ui_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Data.Label27);
            ui.ShowDialog();

            if (ui.UserAcceptActioin == false)
            {
                e.Cancel = true;
            }
        }

        private OptimizerReportCharting _resultsCharting;

        /// <summary>
        /// an object containing data for optimization
        /// and starting the optimization process
        /// объект хранящий в себе данные для оптимизации
        /// и запускающий процесс оптимизации
        /// </summary>
        private OptimizerMaster _master;

        /// <summary>
        /// prevent the user from touching the interface
        /// запретить пользователю трогать интерфейс
        /// </summary>
        private void StopUserActivity()
        {
            TabControlPrime.SelectedItem = TabControlPrime.Items[0];
            TabControlPrime.IsEnabled = false;
            ComboBoxThreadsCount.IsEnabled = false;
        }

        /// <summary>
        /// allow the user to touch the interface
        /// разрешить пользователю трогать интерфейс
        /// </summary>
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
            TabControlPrime.IsEnabled = true;
            ComboBoxThreadsCount.IsEnabled = true;
        }

        /// <summary>
        /// inbound event: optimization process completed
        /// входящее событие: завершился процесс оптимизации
        /// </summary>
        void _master_TestReadyEvent(List<OptimazerFazeReport> reports)
        {
            _reports = reports;
            RepaintResults();
            ShowResultDialog();
        }

        private List<OptimazerFazeReport> _reports;

        private void RepaintResults()
        {
            try
            {
                for (int i = 0; i < _reports.Count; i++)
                {
                    SortResults(_reports[i].Reports);
                }

                PaintEndOnAllProgressBars();
                PaintTableFazes();
                PaintTableResults();
                StartUserActivity();

                _resultsCharting.ReLoad(_reports);
            }
            catch (Exception error)
            {
                _master.SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        // results window / окно результатов

        private void ButtonResults_Click(object sender, RoutedEventArgs e)
        {
            ShowResultDialog();
        }

        public void ShowResultDialog()
        {
            if(_gridFazes.InvokeRequired)
            {
                _gridFazes.Invoke(new Action(ShowResultDialog));
                return;
            }

            OptimizerReportUi ui = new OptimizerReportUi(_master);
            ui.Show();
            ui.Paint(_reports);
            ui.Activate();
        }

        // work on drawing progress bars / работа по рисованию прогресс Баров

        /// <summary>
        /// the user has changed the number of threads that will be optimized
        /// пользователь изменил кол-во потоков которым будет проходить оптимизация
        /// </summary>
        void ComboBoxThreadsCount_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CreateThradsProgressBars();
        }

        /// <summary>
        /// progress bars showing progress of individual threads during optimization
        /// прогрессбары показывающие прогресс отдельных потоков во время оптимизации
        /// </summary>
        private List<ProgressBar> _progressBars;

        /// <summary>
        /// create progress bars for stream optimization
        /// создать прогресс бары для потоков оптимизации
        /// </summary>
        private void CreateThradsProgressBars()
        {
            if (!ComboBoxThreadsCount.Dispatcher.CheckAccess())
            {
                ComboBoxThreadsCount.Dispatcher.Invoke(CreateThradsProgressBars);
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

        /// <summary>
        /// place of work update stream progress on progress bars
        /// место работы потока обновляющего прогресс на прогрессБарах
        /// </summary>
        private async void PainterProgressArea()
        {
            while (true)
            {
                await Task.Delay(500);

                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }
                PaintAllProgressBars();
            }
        }

        /// <summary>
        /// update progress
        /// обновить прогресс
        /// </summary>
        private void PaintAllProgressBars()
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


            for (int i = statuses.Count - 1, i2 = 0; i > -1 && i2 < _progressBars.Count; i2++, i--)
            {
                if (statuses[i] == null)
                {
                    return;
                }
                _progressBars[i2].Maximum = statuses[i].MaxValue;
                _progressBars[i2].Value = statuses[i].CurrentValue;
            }
        }

        /// <summary>
        /// upgrade all progress bars to the final stage
        /// обновить все прогрессбары до завершающей стадии
        /// </summary>
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

        // Moving the screen to the desired interface element, if the user has not managed to configure everything
        // передвижение экрана к нужному элементу интерфейса, если пользователь не успел всё настроить

        /// <summary>
        /// optimization can not start and you need to move the display to a place that is not configured
        /// оптимизация не может стартовать и нужно переместить отображение к месту которое не настроено
        /// </summary>
        /// <param name="move">place to move GUI/место куда нужно переместить ГУИ</param>
        void _master_NeadToMoveUiToEvent(NeadToMoveUiTo move)
        {
            if (!TabControlPrime.Dispatcher.CheckAccess())
            {
                TabControlPrime.Dispatcher.Invoke(new Action<NeadToMoveUiTo>(_master_NeadToMoveUiToEvent), move);
                return;
            }

            if (move == NeadToMoveUiTo.Fazes)
            {
                TabControlPrime.SelectedItem = TabControlPrime.Items[2];
            }
            if (move == NeadToMoveUiTo.Filters)
            {
                TabControlPrime.SelectedItem = TabControlPrime.Items[3];
            }
            if (move == NeadToMoveUiTo.TabsAndTimeFrames)
            {
                TabControlPrime.SelectedItem = TabControlPrime.Items[0];
            }
            if (move == NeadToMoveUiTo.Storage)
            {
                TabControlPrime.SelectedItem = TabControlPrime.Items[0];
            }
            if (move == NeadToMoveUiTo.Parametrs)
            {
                TabControlPrime.SelectedItem = TabControlPrime.Items[1];
            }
            // Проверка параметра Regime (наличие/состояние)
            if (move == NeadToMoveUiTo.RegimeRow)
            {
                for (int i = 0; i < _gridParametrs.Rows.Count; i++)
                {
                    for (int j = 0; j < ((DataGridViewCellCollection)_gridParametrs.Rows[i].Cells).Count; j++)
                    {
                        if (Convert.ToString(_gridParametrs.Rows[i].Cells[j].Value) == "Regime")
                        {
                            _gridParametrs.CurrentCell = _gridParametrs[_gridParametrs.Rows[i].Cells[j].ColumnIndex + 2, i];
                            break;
                        }
                    }
                }
                TabControlPrime.SelectedItem = TabControlPrime.Items[1];
            }
            // Проверка параметра Regime (наличие/состояние) / конец
        }


        // processing controls by clicking on them by the user/обработка контролов по нажатию их пользователем

        /// <summary>
        /// the user has clicked on the start and stop optimization button
        /// пользователь нажал на кнопку запускающую и останавливающую оптимизацию
        /// </summary>
        private void ButtonGo_Click(object sender, RoutedEventArgs e)
        {
            SaveParamsFromTable();

            if (ButtonGo.Content.ToString() == OsLocalization.Optimizer.Label9 &&
                 _reports != null)
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Optimizer.Label33);

                ui.ShowDialog();

                if (!ui.UserAcceptActioin)
                {
                    return;
                }
            }

            if (ButtonGo.Content.ToString() == OsLocalization.Optimizer.Label9 && _master.Start())
            {
                ButtonGo.Content = OsLocalization.Optimizer.Label32;
                StopUserActivity();
            }
            else if (ButtonGo.Content.ToString() == OsLocalization.Optimizer.Label32)
            {
                _master.Stop();
                ButtonGo.Content = OsLocalization.Optimizer.Label9;
            }
        }

        void TextBoxPercentFiltration_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _master.PercentOnFilration = Convert.ToDecimal(TextBoxPercentFiltration.Text);
            }
            catch
            {
                TextBoxPercentFiltration.Text = _master.PercentOnFilration.ToString();
            }
        }

        void DatePickerEnd_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            _master.TimeEnd = DatePickerEnd.DisplayDate;
        }

        void DatePickerStart_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            _master.TimeStart = DatePickerStart.DisplayDate;
        }

        void TextBoxFiltertValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _master.FilterProfitValue = Convert.ToDecimal(TextBoxFilterProfitValue.Text);
                _master.FilterMaxDrowDownValue = Convert.ToDecimal(TextBoxMaxDrowDownValue.Text);
                _master.FilterMiddleProfitValue = Convert.ToDecimal(TextBoxFilterMiddleProfitValue.Text);
                _master.FilterProfitFactorValue = Convert.ToDecimal(TextBoxFilterProfitFactorValue.Text);
                _master.FilterDealsCountValue = Convert.ToInt32(TextBoxFilterDealsCount.Text);
            }
            catch
            {
                TextBoxFilterProfitValue.Text = _master.FilterProfitValue.ToString();
                TextBoxMaxDrowDownValue.Text = _master.FilterMaxDrowDownValue.ToString();
                TextBoxFilterMiddleProfitValue.Text = _master.FilterMiddleProfitValue.ToString();
                TextBoxFilterProfitFactorValue.Text = _master.FilterProfitFactorValue.ToString();
                TextBoxFilterDealsCount.Text = _master.FilterDealsCountValue.ToString();
            }
        }

        void CheckBoxFilterIsOn_Click(object sender, RoutedEventArgs e)
        {
            _master.FilterProfitIsOn = CheckBoxFilterProfitIsOn.IsChecked.Value;
            _master.FilterMaxDrowDownIsOn = CheckBoxFilterMaxDrowDownIsOn.IsChecked.Value;
            _master.FilterMiddleProfitIsOn = CheckBoxFilterMiddleProfitIsOn.IsChecked.Value;
            _master.FilterProfitFactorIsOn = CheckBoxFilterProfitFactorIsOn.IsChecked.Value;
            _master.FilterDealsCountIsOn = CheckBoxFilterDealsCount.IsChecked.Value;
        }

        void _master_NewSecurityEvent(List<Security> securities)
        {
            PaintTableTabsSimple();
            PaintTableTabsIndex();
        }

        private void ButtonStrategySelect_Click(object sender, RoutedEventArgs e)
        {
            BotCreateUi ui = new BotCreateUi(
                BotFactory.GetNamesStrategyWithParametersSync(), BotFactory.GetScriptsNamesStrategy(),
                StartProgram.IsOsOptimizer);

            ui.ShowDialog();

            if (ui.IsAccepted == false)
            {
                return;
            }

            _master.StrategyName = ui.NameStrategy;
            _master.IsScript = ui.IsScript;
            TextBoxStrategyName.Text = ui.NameStrategy;
            ReloadStrategy();
        }

        private void ReloadStrategy()
        {
            _parameters = _master.Parameters;
            _parametrsActiv = _master.ParametersOn;
            PaintTableTabsSimple();
            PaintTableParametrs();
            PaintTableTabsIndex();
        }

        void TextBoxStartPortfolio_TextChanged(object sender, TextChangedEventArgs e)
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
                TextBoxStartPortfolio.Text = _master.StartDepozit.ToString();
                return;
            }

            _master.StartDepozit = Convert.ToInt32(TextBoxStartPortfolio.Text);
        }
        
        private void CommissionValueTextBoxOnTextChanged(object sender, TextChangedEventArgs e)
        {
            decimal commissionValue;
            try
            {
                var isParsed = decimal.TryParse(CommissionValueTextBox.Text,  out commissionValue);
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
            ComissionType commissionType = (ComissionType) Enum.Parse(typeof(ComissionType),
                (string) CommissionTypeComboBox.SelectedItem);
            _master.CommissionType = commissionType;
        }

        private void ButtonServerDialog_Click(object sender, RoutedEventArgs e)
        {
            _master.ShowDataStorageDialog();
        }

        private void CheckBoxLastInSample_Click(object sender, RoutedEventArgs e)
        {
            _master.LastInSample = CheckBoxLastInSample.IsChecked.Value;
        }

        // events from the server / события из сервера

        /// <summary>
        /// inbound event: the start or end time of the data in the server has changed
        /// входящее событие: изменилась начальная или конечное время данных в сервере
        /// </summary>
        void _master_DateTimeStartEndChange()
        {
            if (!DatePickerStart.Dispatcher.CheckAccess())
            {
                DatePickerStart.Dispatcher.Invoke(_master_DateTimeStartEndChange);
                return;
            }
            DatePickerStart.SelectedDate = _master.TimeStart;
            DatePickerEnd.SelectedDate = _master.TimeEnd;
        }

        // Table of Papers and Time Frames for ordinary tabs / таблица Бумаг и таймФреймов для обычных вкладок

        /// <summary>
        /// table with settings entries for the usual tabs of the robot
        /// таблица с записями настроек для обычных вкладок у робота
        /// </summary>
        private DataGridView _gridTableTabsSimple;

        /// <summary>
        /// create a table for regular tabs
        /// создать таблицу для обычных вкладок
        /// </summary>
        private void CreateTableTabsSimple()
        {
            _gridTableTabsSimple = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.ColumnHeaderSelect, DataGridViewAutoSizeRowsMode.None);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridTableTabsSimple.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Optimizer.Message20;
            column0.ReadOnly = true;
            column0.Width = 100;

            _gridTableTabsSimple.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Optimizer.Message21;
            column1.ReadOnly = false;
            column1.Width = 150;

            _gridTableTabsSimple.Columns.Add(column1);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = OsLocalization.Optimizer.Label2;
            column.ReadOnly = false;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridTableTabsSimple.Columns.Add(column);

            _gridTableTabsSimple.Rows.Add(null, null);

            HostTabsSimple.Child = _gridTableTabsSimple;

            _gridTableTabsSimple.CellValueChanged += _grid_CellValueChanged;
        }

        /// <summary>
        /// draw a table for regular tabs
        /// прорисовать таблицу для обычных вкладок
        /// </summary>
        private void PaintTableTabsSimple()
        {
            if (_gridTableTabsSimple.InvokeRequired)
            {
                _gridTableTabsIndex.Invoke(new Action(PaintTableTabsSimple));
                return;
            }

            List<SecurityTester> securities = _master.SecurityTester;

            if (securities == null)
            {
                return;
            }
            if (_gridTableTabsSimple == null)
            {
                return;
            }

            if (_gridTableTabsSimple.InvokeRequired)
            {
                _gridTableTabsSimple.Invoke(new Action(PaintTableTabsSimple));
                return;
            }

            _gridTableTabsSimple.Rows.Clear();
            List<string> names = new List<string>();

            for (int i = 0; i < securities.Count; i++)
            {
                if (names.Find(n => n == securities[i].Security.Name) == null)
                {
                    names.Add(securities[i].Security.Name);
                }
            }

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


            int countTab = 0;
            string nameBot = _master.StrategyName;

            BotPanel bot = BotFactory.GetStrategyForName(nameBot, "", StartProgram.IsOsOptimizer, _master.IsScript);

            if (bot == null)
            {
                return;
            }
            if (bot.TabsSimple != null)
            {
                countTab += bot.TabsSimple.Count;
            }
            if (countTab == 0)
            {
                return;
            }

            for (int i = 0; i < countTab; i++)
            {
                DataGridViewRow row = new DataGridViewRow();

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = i;

                DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();
                for (int i2 = 0; i2 < names.Count; i2++)
                {
                    cell.Items.Add(names[i2]);
                }
                row.Cells.Add(cell);

                DataGridViewComboBoxCell cell2 = new DataGridViewComboBoxCell();
                for (int i2 = 0; i2 < timeFrame.Count; i2++)
                {
                    cell2.Items.Add(timeFrame[i2]);
                }
                row.Cells.Add(cell2);

                _gridTableTabsSimple.Rows.Insert(0, row);
            }
        }

        /// <summary>
        /// the user has changed something in the table of the usual tabs of the robot
        /// пользователь поменял что-то в таблице обычных вкладок робота
        /// </summary>
        void _grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)

        {
            for (int i = 0; i < _gridTableTabsSimple.Rows.Count; i++)
            {
                if (_gridTableTabsSimple.Rows[i].Cells[1].Value == null ||
                    _gridTableTabsSimple.Rows[i].Cells[2].Value == null)
                {
                    return;
                }
            }

            List<TabSimpleEndTimeFrame> _tabs = new List<TabSimpleEndTimeFrame>();
            for (int i = 0; i < _gridTableTabsSimple.Rows.Count; i++)
            {
                TabSimpleEndTimeFrame tab = new TabSimpleEndTimeFrame();

                tab.NumberOfTab = i;

                tab.NameSecurity = _gridTableTabsSimple.Rows[i].Cells[1].Value.ToString();
                Enum.TryParse(_gridTableTabsSimple.Rows[i].Cells[2].Value.ToString(), out tab.TimeFrame);
                _tabs.Add(tab);
                _gridTableTabsSimple.Rows[i].Cells[0].Style = new DataGridViewCellStyle();
            }

            _master.TabsSimpleNamesAndTimeFrames = _tabs;
        }

        // table of papers and time frames for indexes таблица Бумаг и таймФреймов для индексов

        /// <summary>
        /// table with tab settings with indexes
        /// таблица с настройками вкладок с индексами
        /// </summary>
        private DataGridView _gridTableTabsIndex;

        /// <summary>
        /// create a table with tab settings with indexes
        /// создать таблицу с настройками вкладок с индексами
        /// </summary>
        private void CreateTableTabsIndex()
        {
            _gridTableTabsIndex = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.ColumnHeaderSelect, DataGridViewAutoSizeRowsMode.None);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridTableTabsIndex.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Optimizer.Message20;
            column0.ReadOnly = true;
            column0.Width = 100;

            _gridTableTabsIndex.Columns.Add(column0);

            DataGridViewButtonColumn column1 = new DataGridViewButtonColumn();
            column1.CellTemplate = new DataGridViewButtonCell();
            column1.HeaderText = @"";
            column1.ReadOnly = false;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _gridTableTabsIndex.Columns.Add(column1);
            HostTabsIndex.Child = _gridTableTabsIndex;

            _gridTableTabsIndex.CellClick += _gridTableTabsIndex_CellClick;
        }

        /// <summary>
        /// draw a table with tab settings with indexes
        /// прорисовать таблицу с настройками вкладок с индексами
        /// </summary>
        private void PaintTableTabsIndex()
        {
            if (_gridTableTabsSimple.InvokeRequired)
            {
                _gridTableTabsIndex.Invoke(new Action(PaintTableTabsIndex));
                return;
            }
            List<SecurityTester> securities = _master.SecurityTester;

            if (_gridTableTabsIndex == null)
            {
                return;
            }

            _gridTableTabsIndex.Rows.Clear();

            int countTab = 0;
            string nameBot = _master.StrategyName;

            BotPanel bot = BotFactory.GetStrategyForName(nameBot, "", StartProgram.IsOsOptimizer, _master.IsScript);

            _master.TabsIndexNamesAndTimeFrames = new List<TabIndexEndTimeFrame>();

            if (bot == null)
            {
                return;
            }
            if (bot.TabsIndex != null)
            {
                countTab += bot.TabsIndex.Count;
            }
            if (countTab == 0)
            {
                return;
            }

            for (int i = 0; i < countTab; i++)
            {
                _master.TabsIndexNamesAndTimeFrames.Add(new TabIndexEndTimeFrame());

                DataGridViewRow row = new DataGridViewRow();

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = i;
                row.Cells[0].Style = new DataGridViewCellStyle();

                DataGridViewButtonCell cell2 = new DataGridViewButtonCell();
                cell2.Style = new DataGridViewCellStyle();
                cell2.Value = OsLocalization.Optimizer.Message22;
                row.Cells.Add(cell2);

                _gridTableTabsIndex.Rows.Insert(0, row);
            }
        }

        /// <summary>
        /// the user has changed the value in the table with the settings tabs with indexes
        /// пользователь изменил значение в таблице с настройками вкладок с индексами
        /// </summary>
        void _gridTableTabsIndex_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex > _master.TabsIndexNamesAndTimeFrames.Count)
            {
                return;
            }

            if (e.ColumnIndex == 1)
            {
                TabIndexOptimizerUi ui = new TabIndexOptimizerUi(_master.SecurityTester,
                    _master.TabsIndexNamesAndTimeFrames[e.RowIndex]);
                ui.ShowDialog();

                if (ui.NeadToSave)
                {
                    _master.TabsIndexNamesAndTimeFrames[e.RowIndex] = ui.Index;
                }
            }
        }

        // test phase table/таблица этапов тестирования

        /// <summary>
        /// handler for clicking on the button for creating optimization steps
        /// обработчик для нажатия на кнопку создания этапов оптимизации
        /// </summary>
        private void ButtonCreateOptimizeFazes_Click(object sender, RoutedEventArgs e)
        {
            _master.ReloadFazes();
            PaintTableOptimizeFazes();
            WolkForwardPeriodsPainter.PaintForwards(HostWalkForwardPeriods, _master.Fazes);
        }

        /// <summary>
        /// table with optimization steps
        /// таблица с этапами оптимизации
        /// </summary>
        private DataGridView _gridFazes;

        /// <summary>
        /// create a table with optimization phases
        /// создать таблицу с фазами оптимизации
        /// </summary>
        private void CreateTableOptimizeFazes()
        {
            _gridFazes = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.None);
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
        }

        private void _gridFazes_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            // 2 - Start 3 - End

            int indexColumn = e.ColumnIndex;

            if(indexColumn != 2 && indexColumn != 3)
            {
                return;
            }

            int indexRow = e.RowIndex;

            try
            {
               DateTime time = Convert.ToDateTime(_gridFazes.Rows[indexRow].Cells[indexColumn].EditedFormattedValue.ToString());

                if (indexColumn == 2)
                {
                    _master.Fazes[indexRow].TimeStart = time;
                }
                else
                {
                    _master.Fazes[indexRow].TimeEnd = time;
                }

            }
            catch (Exception exception)
            {
                if(indexColumn == 2)
                {
                    _gridFazes.Rows[indexRow].Cells[indexColumn].Value = _master.Fazes[indexRow].TimeStart.ToShortDateString(); ;
                }
                else
                {
                    _gridFazes.Rows[indexRow].Cells[indexColumn].Value = _master.Fazes[indexRow].TimeEnd.ToShortDateString(); ;
                }
               
            }
            PaintTableOptimizeFazes();
            WolkForwardPeriodsPainter.PaintForwards(HostWalkForwardPeriods, _master.Fazes);
        }


        /// <summary>
        /// draw a table with optimization phases
        /// прорисовать таблицу с фазами оптимизации
        /// </summary>
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
                cell2.Value = fazes[i].TimeStart.ToShortDateString();
                row.Cells.Add(cell2);

                DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell();
                cell3.Value = fazes[i].TimeEnd.ToShortDateString();
                row.Cells.Add(cell3);

                DataGridViewTextBoxCell cell4 = new DataGridViewTextBoxCell();
                cell4.Value = fazes[i].Days;
                row.Cells.Add(cell4);

                _gridFazes.Rows.Add(row);
            }
        }

        // parameter table/таблица параметров

        /// <summary>
        /// parameters for optimizing the current robot
        /// параметры для оптимизации текущего робота
        /// </summary>
        private List<IIStrategyParameter> _parameters;

        /// <summary>
        /// list of included parameters
        /// список включенных параметров
        /// </summary>
        private List<bool> _parametrsActiv;

        /// <summary>
        /// table with optimization parameters
        /// таблица с параметрами оптимизации
        /// </summary>
        private DataGridView _gridParametrs;

        /// <summary>
        /// create parameter table
        /// создать таблицу параметров
        /// </summary>
        private void CreateTableParametrs()
        {
            _gridParametrs = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.None);
            _gridParametrs.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridParametrs.DefaultCellStyle;

            DataGridViewCheckBoxColumn column0 = new DataGridViewCheckBoxColumn();
            column0.CellTemplate = new DataGridViewCheckBoxCell();
            column0.HeaderText = OsLocalization.Optimizer.Message28;
            column0.ReadOnly = false;
            column0.Width = 100;

            _gridParametrs.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Optimizer.Message29;
            column1.ReadOnly = true;
            column1.Width = 150;

            _gridParametrs.Columns.Add(column1);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = OsLocalization.Optimizer.Message24;
            column.ReadOnly = true;
            column1.Width = 100;
            _gridParametrs.Columns.Add(column);

            DataGridViewComboBoxColumn column2 = new DataGridViewComboBoxColumn();
            column2.CellTemplate = new DataGridViewComboBoxCell();
            column2.HeaderText = OsLocalization.Optimizer.Message30;
            column2.ReadOnly = false;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridParametrs.Columns.Add(column2);

            DataGridViewColumn column22 = new DataGridViewColumn();
            column22.CellTemplate = cell0;
            column22.HeaderText = OsLocalization.Optimizer.Message31;
            column22.ReadOnly = false;
            column22.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridParametrs.Columns.Add(column22);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = OsLocalization.Optimizer.Message32;
            column3.ReadOnly = false;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridParametrs.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = OsLocalization.Optimizer.Message33;
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridParametrs.Columns.Add(column4);

            _gridParametrs.Rows.Add(null, null);

            HostParam.Child = _gridParametrs;
        }

        /// <summary>
        /// draw parameter table
        /// прорисовать таблицу параметров
        /// </summary>
        private void PaintTableParametrs()
        {
            if (_gridParametrs.InvokeRequired)
            {
                _gridParametrs.Invoke(new Action(PaintTableOptimizeFazes));
                return;
            }
            _gridParametrs.Rows.Clear();

            _gridParametrs.CellValueChanged -= _gridParametrs_CellValueChanged;

            List<IIStrategyParameter> fazes = _parameters;

            if (fazes == null ||
                fazes.Count == 0)
            {
                return;
            }

            for (int i = 0; i < fazes.Count; i++)
            {
                DataGridViewRow row = new DataGridViewRow();

                row.Cells.Add(new DataGridViewCheckBoxCell());

                if (i < _parametrsActiv.Count)
                {
                    row.Cells[0].Value = _parametrsActiv[i];
                }

                if (_parameters[i].Type == StrategyParameterType.Bool ||
                    _parameters[i].Type == StrategyParameterType.String)
                {
                    row.Cells[0].ReadOnly = true;
                }

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[1].Value = _parameters[i].Name;

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[2].Value = _parameters[i].Type;

                // default value. For bool and string
                // значение по умолчанию. Для Булл и Стринг

                if (_parameters[i].Type == StrategyParameterType.Bool)
                {
                    DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();
                    cell.Items.Add("False");
                    cell.Items.Add("True");
                    cell.Value = ((StrategyParameterBool)_parameters[i]).ValueBool.ToString();
                    row.Cells.Add(cell);
                }
                else if (_parameters[i].Type == StrategyParameterType.String)
                {
                    StrategyParameterString param = (StrategyParameterString)_parameters[i];

                    if (param.ValuesString.Count > 1)
                    {
                        DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();

                        for (int i2 = 0; i2 < param.ValuesString.Count; i2++)
                        {
                            cell.Items.Add(param.ValuesString[i2]);
                        }
                        cell.Value = param.ValueString;
                        row.Cells.Add(cell);
                    }
                    else if (param.ValuesString.Count == 1)
                    {
                        DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                        cell.Value = param.ValueString;
                        row.Cells.Add(cell);
                    }
                }
                else if (_parameters[i].Type == StrategyParameterType.Int)
                {
                    DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();
                    row.Cells.Add(cell);
                }
                else if (_parameters[i].Type == StrategyParameterType.Decimal)
                {
                    DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();
                    row.Cells.Add(cell);
                }

                // starting value. For bool and String, the only one is manual! field
                // стартовое значение. Для Булл и Стринг единственное настрамое вручную! поле
                if (_parameters[i].Type == StrategyParameterType.Bool)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                    row.Cells.Add(cell);
                    cell.ReadOnly = true;
                }
                else if (_parameters[i].Type == StrategyParameterType.String)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                    row.Cells.Add(cell);
                    cell.ReadOnly = true;
                }
                else if (_parameters[i].Type == StrategyParameterType.Int)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                    StrategyParameterInt param = (StrategyParameterInt)_parameters[i];
                    param.ValueInt = param.ValueIntStart;
                    cell.Value = param.ValueIntStart.ToString();
                    row.Cells.Add(cell);
                }
                else if (_parameters[i].Type == StrategyParameterType.Decimal)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                    StrategyParameterDecimal param = (StrategyParameterDecimal)_parameters[i];
                    cell.Value = param.ValueDecimalStart.ToString();
                    param.ValueDecimal = param.ValueDecimalStart;
                    row.Cells.Add(cell);
                }

                // value for increment. For bool and String is not available
                // значение для приращения. Для Булл и Стринг не доступно

                if (_parameters[i].Type == StrategyParameterType.Bool)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                    row.Cells.Add(cell);
                    cell.ReadOnly = true;
                }
                else if (_parameters[i].Type == StrategyParameterType.String)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                    row.Cells.Add(cell);
                    cell.ReadOnly = true;
                }
                else if (_parameters[i].Type == StrategyParameterType.Int)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                    cell.Value = ((StrategyParameterInt)_parameters[i]).ValueIntStep.ToString();
                    row.Cells.Add(cell);
                }
                else if (_parameters[i].Type == StrategyParameterType.Decimal)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                    cell.Value = ((StrategyParameterDecimal)_parameters[i]).ValueDecimalStep.ToString();
                    row.Cells.Add(cell);
                }

                // value for the final element of the collection. For bool and String is not available
                // значение для завершающего элемента коллекции. Для Булл и Стринг не доступно

                if (_parameters[i].Type == StrategyParameterType.Bool)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                    row.Cells.Add(cell);
                    cell.ReadOnly = true;
                }
                else if (_parameters[i].Type == StrategyParameterType.String)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                    row.Cells.Add(cell);
                    cell.ReadOnly = true;
                }
                else if (_parameters[i].Type == StrategyParameterType.Int)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                    cell.Value = ((StrategyParameterInt)_parameters[i]).ValueIntStop.ToString();
                    row.Cells.Add(cell);
                }
                else if (_parameters[i].Type == StrategyParameterType.Decimal)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                    cell.Value = ((StrategyParameterDecimal)_parameters[i]).ValueDecimalStop.ToString();
                    row.Cells.Add(cell);
                }

                _gridParametrs.Rows.Add(row);
            }

            _gridParametrs.CellValueChanged += _gridParametrs_CellValueChanged;
        }

        /// <summary>
        /// save parameters taking for this value from the parameter table
        /// сохранить параметры взяв для этого значения из таблицы параметров
        /// </summary>
        private void SaveParamsFromTable()
        {
            if (_parameters == null)
            {
                return;
            }

            try
            {
                for (int i = 0; i < _parameters.Count; i++)
                {
                    if (_parameters[i].Type == StrategyParameterType.String)
                    {
                        ((StrategyParameterString)_parameters[i]).ValueString = _gridParametrs.Rows[i].Cells[3].Value.ToString();
                        _parametrsActiv[i] = false;
                    }
                    else if (_parameters[i].Type == StrategyParameterType.Bool)
                    {
                        ((StrategyParameterBool)_parameters[i]).ValueBool = Convert.ToBoolean(_gridParametrs.Rows[i].Cells[3].Value.ToString());
                        _parametrsActiv[i] = false;
                    }
                    else if (_parameters[i].Type == StrategyParameterType.Int)
                    {
                        int valueStart = Convert.ToInt32(_gridParametrs.Rows[i].Cells[4].Value);
                        int valueStep = Convert.ToInt32(_gridParametrs.Rows[i].Cells[5].Value);
                        int valueStop = Convert.ToInt32(_gridParametrs.Rows[i].Cells[6].Value);

                        if (valueStart > valueStop)
                        {
                            MessageBox.Show(OsLocalization.Optimizer.Message34);
                            PaintTableParametrs();
                            return;
                        }

                        StrategyParameterInt param = ((StrategyParameterInt)_parameters[i]);

                        if (valueStart != param.ValueIntStart ||
                            valueStep != param.ValueIntStep ||
                            valueStop != param.ValueIntStop)
                        {
                            _parameters.Insert(i, new StrategyParameterInt(_parameters[i].Name, param.ValueInt,
                                valueStart, valueStop, valueStep));
                            _parameters.RemoveAt(i + 1);
                        }

                        DataGridViewCheckBoxCell box = (DataGridViewCheckBoxCell)_gridParametrs.Rows[i].Cells[0];
                        if (_gridParametrs.Rows[i].Cells[0].Value == null ||
                            (bool)_gridParametrs.Rows[i].Cells[0].Value == false)
                        {
                            _parametrsActiv[i] = false;
                        }
                        else
                        {
                            _parametrsActiv[i] = true;
                        }
                    }
                    else if (_parameters[i].Type == StrategyParameterType.Decimal)
                    {
                        decimal valueStart = Convert.ToDecimal(_gridParametrs.Rows[i].Cells[4].Value);
                        decimal valueStep = Convert.ToDecimal(_gridParametrs.Rows[i].Cells[5].Value);
                        decimal valueStop = Convert.ToDecimal(_gridParametrs.Rows[i].Cells[6].Value);

                        if (valueStart > valueStop)
                        {
                            MessageBox.Show(OsLocalization.Optimizer.Message34);
                            PaintTableParametrs();
                            return;
                        }

                        StrategyParameterDecimal param = ((StrategyParameterDecimal)_parameters[i]);

                        if (valueStart != param.ValueDecimalStart ||
                            valueStep != param.ValueDecimalStep ||
                            valueStop != param.ValueDecimalStop)
                        {
                            _parameters.Insert(i, new StrategyParameterDecimal(_parameters[i].Name, param.ValueDecimal,
                               valueStart, valueStop, valueStep));
                            _parameters.RemoveAt(i + 1);
                        }
                        if (_gridParametrs.Rows[i].Cells[0].Value == null ||
                            (bool)_gridParametrs.Rows[i].Cells[0].Value == false)
                        {
                            _parametrsActiv[i] = false;
                        }
                        else
                        {
                            _parametrsActiv[i] = true;
                        }
                    }
                }
            }
            catch (Exception)
            {
                PaintTableParametrs();
            }
        }

        /// <summary>
        /// the user has changed something in the parameter table
        /// пользователь изменил что-то в таблице параметров
        /// </summary>
        void _gridParametrs_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            SaveParamsFromTable();
        }

        // phase table for switching after testing/таблица фаз для переключения после тестирования

        /// <summary>
        /// table with optimization steps on the totals tab
        /// таблица с этапами оптимизации на вкладке итогов
        /// </summary>
        private DataGridView _gridFazesEnd;

        /// <summary>
        /// create phase table on totals tabs
        /// создать таблицу фаз на вкладки итогов
        /// </summary>
        private void CreateTableFazes()
        {
            _gridFazesEnd = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.None);
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
        }

        /// <summary>
        /// draw phase table on totals tab
        /// прорисовать таблицу фаз на вкладке итогов
        /// </summary>
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
                cell2.Value = fazes[i].TimeStart.ToShortDateString();
                row.Cells.Add(cell2);

                DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell();
                cell3.Value = fazes[i].TimeEnd.ToShortDateString();
                row.Cells.Add(cell3);

                DataGridViewTextBoxCell cell4 = new DataGridViewTextBoxCell();
                cell4.Value = fazes[i].Days;
                row.Cells.Add(cell4);

                _gridFazesEnd.Rows.Add(row);
            }
        }

        /// <summary>
        /// the user clicked on the phase table in the totals tab
        /// пользователь кликнул по таблице фаз на вкладке итогов
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void _gridFazesEnd_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            PaintTableResults();
        }

        // optimization results table / таблица результатов оптимизации

        /// <summary>
        /// table with optimization steps
        /// таблица с этапами оптимизации
        /// </summary>
        private DataGridView _gridResults;

        /// <summary>
        /// create a table of results
        /// создать таблицу результатов
        /// </summary>
        private void CreateTableResults()
        {
            _gridResults = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.ColumnHeaderSelect, DataGridViewAutoSizeRowsMode.None);
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

            DataGridViewButtonColumn column11 = new DataGridViewButtonColumn();
            column11.CellTemplate = new DataGridViewButtonCell();
            column11.HeaderText = OsLocalization.Optimizer.Message40;
            column11.ReadOnly = true;
            column11.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column11);

            _gridResults.Rows.Add(null, null);

            WindowsFormsHostResults.Child = _gridResults;
        }

        private void UpdateHeaders()
        {

            _gridResults.Columns[0].HeaderText = "Bot Name";

            if (_sortBotsType == SortBotsType.BotName)
            {
                _gridResults.Columns[0].HeaderText += " vvv";
            }

            _gridResults.Columns[2].HeaderText = "Pos Count";

            if (_sortBotsType == SortBotsType.PositionCount)
            {
                _gridResults.Columns[2].HeaderText += " vvv";
            }

            _gridResults.Columns[3].HeaderText = "Total Profit";

            if (_sortBotsType == SortBotsType.TotalProfit)
            {
                _gridResults.Columns[3].HeaderText += " vvv";
            }

            _gridResults.Columns[4].HeaderText = "Max Drow Dawn";
            if (_sortBotsType == SortBotsType.MaxDrowDawn)
            {
                _gridResults.Columns[4].HeaderText += " vvv";
            }

            _gridResults.Columns[5].HeaderText = "Average Profit";
            if (_sortBotsType == SortBotsType.AverageProfit)
            {
                _gridResults.Columns[5].HeaderText += " vvv";
            }

            _gridResults.Columns[6].HeaderText = "Average Profit %";
            if (_sortBotsType == SortBotsType.AverageProfitPercent)
            {
                _gridResults.Columns[6].HeaderText += " vvv";
            }

            _gridResults.Columns[7].HeaderText = "Profit Factor";
            if (_sortBotsType == SortBotsType.ProfitFactor)
            {
                _gridResults.Columns[7].HeaderText += " vvv";
            }

            _gridResults.Columns[8].HeaderText = "Pay Off Ratio";
            if (_sortBotsType == SortBotsType.PayOffRatio)
            {
                _gridResults.Columns[8].HeaderText += " vvv";
            }

            _gridResults.Columns[9].HeaderText = "Recovery";
            if (_sortBotsType == SortBotsType.Recovery)
            {
                _gridResults.Columns[9].HeaderText += " vvv";
            }
        }

        /// <summary>
        /// draw a table of results
        /// прорисовать таблицу результатов
        /// </summary>
        private void PaintTableResults()
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

            OptimazerFazeReport fazeReport = _reports[num];

            if (fazeReport == null)
            {
                return;
            }

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
                row.Cells.Add(new DataGridViewTextBoxCell());

                if (report.TabsReports.Count == 1)
                {
                    row.Cells[0].Value = report.BotName;
                }
                else
                {
                    row.Cells[0].Value = "Сводные";
                }

                DataGridViewTextBoxCell cell2 = new DataGridViewTextBoxCell();
                cell2.Value = report.GetParamsToDataTable();
                row.Cells.Add(cell2);

                DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell();
                cell3.Value = report.PositionsCount;
                row.Cells.Add(cell3);

                DataGridViewTextBoxCell cell4 = new DataGridViewTextBoxCell();
                cell4.Value = report.TotalProfit;
                row.Cells.Add(cell4);

                DataGridViewTextBoxCell cell5 = new DataGridViewTextBoxCell();
                cell5.Value = report.MaxDrowDawn;
                row.Cells.Add(cell5);

                DataGridViewTextBoxCell cell6 = new DataGridViewTextBoxCell();
                cell6.Value = report.AverageProfit;
                row.Cells.Add(cell6);

                DataGridViewTextBoxCell cell7 = new DataGridViewTextBoxCell();
                cell7.Value = report.AverageProfitPercent;
                row.Cells.Add(cell7);

                DataGridViewTextBoxCell cell8 = new DataGridViewTextBoxCell();
                cell8.Value = report.ProfitFactor;
                row.Cells.Add(cell8);

                DataGridViewTextBoxCell cell9 = new DataGridViewTextBoxCell();
                cell9.Value = report.PayOffRatio;
                row.Cells.Add(cell9);

                DataGridViewTextBoxCell cell10 = new DataGridViewTextBoxCell();
                cell10.Value = report.Recovery;
                row.Cells.Add(cell10);


                DataGridViewButtonCell cell11 = new DataGridViewButtonCell();
                cell11.Value = OsLocalization.Optimizer.Message40;
                row.Cells.Add(cell11);

                _gridResults.Rows.Add(row);

                if (report.TabsReports.Count > 1)
                {
                    for (int i2 = 0; i2 < report.TabsReports.Count; i2++)
                    {
                        _gridResults.Rows.Add(GetRowResult(report.TabsReports[i2]));
                    }
                }
            }

            _gridResults.SelectionChanged += _gridResults_SelectionChanged;
            _gridResults.CellMouseClick += _gridResults_CellMouseClick;
        }

        private void SortResults(List<OptimizerReport> reports)
        {
            for (int i = 0; i < reports.Count; i++)
            {
                for (int i2 = 0; i2 < reports.Count - 1; i2++)
                {
                    if (FirstLessSecond(reports[i2], reports[i2 + 1], _sortBotsType))
                    {
                        // сортировка пузыриком // фаталити https://youtu.be/LOh_J0Dah7c?t=30
                        OptimizerReport glass = reports[i2];
                        reports[i2] = reports[i2 + 1];
                        reports[i2 + 1] = glass;
                    }
                }
            }
        }

        private bool FirstLessSecond(OptimizerReport rep1, OptimizerReport rep2, SortBotsType sortType)
        {
            if (sortType == SortBotsType.BotName &&
            Convert.ToInt32(rep1.BotName.Split(' ')[0]) > Convert.ToInt32(rep2.BotName.Split(' ')[0]))
            {
                return true;
            }
            else if (sortType == SortBotsType.TotalProfit &&
                rep1.TotalProfit < rep2.TotalProfit)
            {
                return true;
            }
            else if (sortType == SortBotsType.PositionCount &&
                     rep1.PositionsCount < rep2.PositionsCount)
            {
                return true;
            }
            else if (sortType == SortBotsType.MaxDrowDawn &&
                     rep1.MaxDrowDawn < rep2.MaxDrowDawn)
            {
                return true;
            }
            else if (sortType == SortBotsType.AverageProfit &&
                     rep1.AverageProfit < rep2.AverageProfit)
            {
                return true;
            }
            else if (sortType == SortBotsType.AverageProfitPercent &&
                     rep1.AverageProfitPercent < rep2.AverageProfitPercent)
            {
                return true;
            }
            else if (sortType == SortBotsType.ProfitFactor &&
                     rep1.ProfitFactor < rep2.ProfitFactor)
            {
                return true;
            }
            else if (sortType == SortBotsType.PayOffRatio &&
                     rep1.PayOffRatio < rep2.PayOffRatio)
            {
                return true;
            }
            else if (sortType == SortBotsType.Recovery &&
                     rep1.Recovery < rep2.Recovery)
            {
                return true;
            }

            return false;
        }

        private DataGridViewRow GetRowResult(OptimizerReportTab report)
        {
            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[0].Value = report.SecurityName;


            DataGridViewTextBoxCell cell2 = new DataGridViewTextBoxCell();
            row.Cells.Add(cell2);

            DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell();
            cell3.Value = report.PositionsCount;
            row.Cells.Add(cell3);

            DataGridViewTextBoxCell cell4 = new DataGridViewTextBoxCell();
            cell4.Value = report.TotalProfit;
            row.Cells.Add(cell4);

            DataGridViewTextBoxCell cell5 = new DataGridViewTextBoxCell();
            cell5.Value = report.MaxDrowDawn;
            row.Cells.Add(cell5);

            DataGridViewTextBoxCell cell6 = new DataGridViewTextBoxCell();
            cell6.Value = report.AverageProfit;
            row.Cells.Add(cell6);

            DataGridViewTextBoxCell cell7 = new DataGridViewTextBoxCell();
            cell7.Value = report.AverageProfitPercent;
            row.Cells.Add(cell7);

            DataGridViewTextBoxCell cell8 = new DataGridViewTextBoxCell();
            cell8.Value = report.ProfitFactor;
            row.Cells.Add(cell8);

            DataGridViewTextBoxCell cell9 = new DataGridViewTextBoxCell();
            cell9.Value = report.PayOffRatio;
            row.Cells.Add(cell9);

            DataGridViewTextBoxCell cell10 = new DataGridViewTextBoxCell();
            cell10.Value = report.Recovery;
            row.Cells.Add(cell10);

            row.Cells.Add(null);

            return row;

        }

        /// <summary>
        /// user clicked a button in the result table
        /// пользователь нажал на кнопку в таблице результатов
        /// </summary>
        void _gridResults_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            if (e.ColumnIndex != 10)
            {
                return;
            }

            OptimazerFazeReport fazeReport;

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

            bot.ShowChartDialog();
        }

        /// <summary>
        /// user clicked results table
        /// пользователь кликнул по таблице результатов
        /// </summary>
        void _gridResults_SelectionChanged(object sender, EventArgs e)
        {
            if (_gridResults.SelectedCells.Count == 0)
            {
                return;
            }
            int columnSelect = _gridResults.SelectedCells[0].ColumnIndex;


            if (columnSelect == 0)
            {
                _sortBotsType = SortBotsType.BotName;
            }
            else if (columnSelect == 2)
            {
                _sortBotsType = SortBotsType.PositionCount;
            }
            else if (columnSelect == 3)
            {
                _sortBotsType = SortBotsType.TotalProfit;
            }
            else if (columnSelect == 4)
            {
                _sortBotsType = SortBotsType.MaxDrowDawn;
            }
            else if (columnSelect == 5)
            {
                _sortBotsType = SortBotsType.AverageProfit;
            }
            else if (columnSelect == 6)
            {
                _sortBotsType = SortBotsType.AverageProfitPercent;
            }
            else if (columnSelect == 7)
            {
                _sortBotsType = SortBotsType.ProfitFactor;
            }
            else if (columnSelect == 8)
            {
                _sortBotsType = SortBotsType.PayOffRatio;
            }
            else if (columnSelect == 9)
            {
                _sortBotsType = SortBotsType.Recovery;
            }
            else
            {
                return;
            }

            try
            {
                for (int i = 0; i < _reports.Count; i++)
                {
                    SortResults(_reports[i].Reports);
                }

                PaintTableResults();
            }
            catch (Exception error)
            {
                _master.SendLogMessage(error.ToString(), LogMessageType.Error);
            }

        }

        /// <summary>
        /// robot sorting type in the results table
        /// тип сортировки роботов в таблице результатов
        /// </summary>
        private SortBotsType _sortBotsType;

        // принудительная перепроверка роботов для оптимизации

        private void ButtonStrategyReload_Click(object sender, RoutedEventArgs e)
        {
            BotFactory.NeadToReload = true;

            Task.Run(new Action(StrategyLoader));
        }

        private void StrategyLoader()
        {
            _master.SendLogMessage(OsLocalization.Optimizer.Message11, LogMessageType.System);

            List<string> strategies = BotFactory.GetNamesStrategyWithParametersSync();

            _master.SendLogMessage(OsLocalization.Optimizer.Message19 + " " + strategies.Count, LogMessageType.System);

            if (string.IsNullOrEmpty(_master.StrategyName))
            {
                return;
            }

            ReloadStrategy();
        }
    }

    /// <summary>
    /// sorting type in the result table
    /// тип сортировки в таблице результатов
    /// </summary>
    public enum SortBotsType
    {
        TotalProfit,

        BotName,

        PositionCount,

        MaxDrowDawn,

        AverageProfit,

        AverageProfitPercent,

        ProfitFactor,

        PayOffRatio,

        Recovery,
    }
}