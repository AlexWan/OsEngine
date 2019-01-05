using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using OsEngine.Entity;
using OsEngine.Journal;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels;
using MessageBox = System.Windows.MessageBox;
using ProgressBar = System.Windows.Controls.ProgressBar;

namespace OsEngine.OsOptimizer
{
    /// <summary>
    /// Логика взаимодействия для OptimizerUi.xaml
    /// </summary>
    public partial class OptimizerUi
    {
        public OptimizerUi()
        {
            InitializeComponent();

            TabControlPrime.Items.RemoveAt(4);

            _master = new OptimizerMaster();
            _master.StrategyNamesReadyEvent += _master_StrategyNamesReadyEvent;
            _master.StartPaintLog(HostLog);
            _master.NewSecurityEvent += _master_NewSecurityEvent;
            _master.DateTimeStartEndChange += _master_DateTimeStartEndChange;
            _master.TestReadyEvent += _master_TestReadyEvent;

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

            // фильтры
            CheckBoxFilterProfitIsOn.IsChecked = _master.FilterProfitIsOn;
            CheckBoxFilterMaxDrowDownIsOn.IsChecked = _master.FilterMaxDrowDownIsOn;
            CheckBoxFilterMiddleProfitIsOn.IsChecked = _master.FilterMiddleProfitIsOn;
            CheckBoxFilterWinPositonIsOn.IsChecked = _master.FilterWinPositionIsOn;
            CheckBoxFilterProfitFactorIsOn.IsChecked = _master.FilterProfitFactorIsOn;

            CheckBoxFilterProfitIsOn.Click += CheckBoxFilterIsOn_Click;
            CheckBoxFilterMaxDrowDownIsOn.Click += CheckBoxFilterIsOn_Click;
            CheckBoxFilterMiddleProfitIsOn.Click += CheckBoxFilterIsOn_Click;
            CheckBoxFilterWinPositonIsOn.Click += CheckBoxFilterIsOn_Click;
            CheckBoxFilterProfitFactorIsOn.Click += CheckBoxFilterIsOn_Click;

            TextBoxFilterProfitValue.Text = _master.FilterProfitFactorValue.ToString();
            TextBoxMaxDrowDownValue.Text = _master.FilterMaxDrowDownValue.ToString();
            TextBoxFilterMiddleProfitValue.Text = _master.FilterMiddleProfitValue.ToString();
            TextBoxFilterWinPositionValue.Text = _master.FilterWinPositionValue.ToString();
            TextBoxFilterProfitFactorValue.Text = _master.FilterProfitFactorValue.ToString();

            TextBoxFilterProfitValue.TextChanged += TextBoxFiltertValue_TextChanged;
            TextBoxMaxDrowDownValue.TextChanged += TextBoxFiltertValue_TextChanged;
            TextBoxFilterMiddleProfitValue.TextChanged += TextBoxFiltertValue_TextChanged;
            TextBoxFilterWinPositionValue.TextChanged += TextBoxFiltertValue_TextChanged;
            TextBoxFilterProfitFactorValue.TextChanged += TextBoxFiltertValue_TextChanged;

            // Оптимизация

            if (_master.TypeOptimization == OptimizationType.SimulatedAnnealing)
            {
                CheckBoxOptimizationTypeSimulatedAnnealing.IsChecked = true;
            }
            else if (_master.TypeOptimization == OptimizationType.GeneticАlgorithm)
            {
                CheckBoxOptimizationTypeGeneticAlgoritm.IsChecked = true;
            }

            CheckBoxOptimizationTypeSimulatedAnnealing.Click += CheckBoxOptimizationTypeSimulatedAnnealing_Click;
            CheckBoxOptimizationTypeGeneticAlgoritm.Click += CheckBoxOptimizationTypeGeneticAlgoritm_Click;

            if (_master.TypeOprimizationFunction == OptimizationFunctionType.EndProfit)
            {
                CheckBoxOptimizationFunctionTypeEndProfit.IsChecked = true;
            }
            if (_master.TypeOprimizationFunction == OptimizationFunctionType.MiddleProfitFromPosition)
            {
                CheckBoxOptimizationFunctionMiddleProfitFromPosition.IsChecked = true;
            }
            if (_master.TypeOprimizationFunction == OptimizationFunctionType.MaxDrowDown)
            {
                CheckBoxOptimizationFunctionMaxDrowDawn.IsChecked = true;
            }
            if (_master.TypeOprimizationFunction == OptimizationFunctionType.ProfitFactor)
            {
                CheckBoxOptimizationFunctionProfitFactor.IsChecked = true;
            }

            CheckBoxOptimizationFunctionTypeEndProfit.Click += CheckBoxOptimizationFunctionTypeEndProfit_Click;
            CheckBoxOptimizationFunctionMiddleProfitFromPosition.Click +=
            CheckBoxOptimizationFunctionMiddleProfitFromPosition_Click;
            CheckBoxOptimizationFunctionMaxDrowDawn.Click += CheckBoxOptimizationFunctionMaxDrowDawn_Click;
            CheckBoxOptimizationFunctionProfitFactor.Click += CheckBoxOptimizationFunctionProfitFactor_Click;

            // Этапы

            DatePickerStart.DisplayDate = _master.TimeStart;
            DatePickerEnd.DisplayDate = _master.TimeEnd;
            TextBoxPercentFiltration.Text = _master.PercentOnFilration.ToString();

            DatePickerStart.SelectedDateChanged += DatePickerStart_SelectedDateChanged;
            DatePickerEnd.SelectedDateChanged += DatePickerEnd_SelectedDateChanged;
            TextBoxPercentFiltration.TextChanged += TextBoxPercentFiltration_TextChanged;


            // алгоритмы оптимизации. Пока блокируем

            CheckBoxOptimizationTypeSimulatedAnnealing.IsEnabled = false;
            CheckBoxOptimizationTypeGeneticAlgoritm.IsEnabled = false;
            CheckBoxOptimizationTypeAllBots.IsEnabled = false;
            CheckBoxOptimizationTypeAllBots.IsChecked = true;

            CheckBoxOptimizationFunctionTypeEndProfit.IsEnabled = false;
            CheckBoxOptimizationFunctionMiddleProfitFromPosition.IsEnabled = false;
            CheckBoxOptimizationFunctionMaxDrowDawn.IsEnabled = false;
            CheckBoxOptimizationFunctionProfitFactor.IsEnabled = false;

            _master.NeadToMoveUiToEvent += _master_NeadToMoveUiToEvent;

            Thread proggressPainter = new Thread(PainterProgressArea);
            proggressPainter.Name = "ProggressPainter";
            proggressPainter.IsBackground = true;
            proggressPainter.Start();
        }

        /// <summary>
        /// объект хранящий в себе данные для оптимизации
        /// и запускающий процесс оптимизации
        /// </summary>
        private OptimizerMaster _master;

        /// <summary>
        /// запретить пользователю трогать интерфейс
        /// </summary>
        private void StopUserActivity()
        {
            TabControlPrime.SelectedItem = TabControlPrime.Items[0];
            TabControlPrime.IsEnabled = false;
            ComboBoxThreadsCount.IsEnabled = false;
        }

        /// <summary>
        /// разрешить пользователю трогать интерфейс
        /// </summary>
        private void StartUserActivity()
        {
            if (!ButtonGo.Dispatcher.CheckAccess())
            {
                ButtonGo.Dispatcher.Invoke(StartUserActivity);
                return;
            }

            ButtonGo.Content = "Погнали!";
            TabControlPrime.SelectedItem = TabControlPrime.Items[4];
            TabControlPrime.IsEnabled = true;
            ComboBoxThreadsCount.IsEnabled = true;
        }

        /// <summary>
        /// входящее событие: завершился процесс оптимизации
        /// </summary>
        void _master_TestReadyEvent(List<BotPanel> botsEndFirstFaze, List<BotPanel> botsOutOfSample)
        {
            PaintEndOnAllProgressBars();
            _botsInSample = botsEndFirstFaze;
            _botsOutOfSample = botsOutOfSample;
            PaintTableFazes();
            PaintTableResults();
            StartUserActivity();
        }

        /// <summary>
        /// роботы с результатами InSample
        /// </summary>
        private List<BotPanel> _botsInSample;

        /// <summary>
        /// роботы с результатами OutOfSample
        /// </summary>
        private List<BotPanel> _botsOutOfSample;


// работа по рисованию прогрессБаров

        /// <summary>
        /// пользователь изменил кол-во потоков которым будет проходить оптимизация
        /// </summary>
        void ComboBoxThreadsCount_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CreateThradsProgressBars();
        }

        /// <summary>
        /// прогрессбары показывающие прогресс отдельных потоков во время оптимизации
        /// </summary>
        private List<ProgressBar> _progressBars;

        /// <summary>
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
        /// место работы потока обновляющего прогресс на прогрессБарах
        /// </summary>
        private void PainterProgressArea()
        {
            while (true)
            {
                Thread.Sleep(500);

                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }
                PaintAllProgressBars();
            }
        }

        /// <summary>
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

// передвижение экрана к нужному элементу интерфейса, если пользователь не успел всё настроить

        /// <summary>
        /// оптимизация не может стартовать и нужно переместить отображение к месту которое не настроено
        /// </summary>
        /// <param name="move">место куда нужно переместить ГУИ</param>
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
                GridFazes.Background = Brushes.DarkOrange;
            }
            if (move == NeadToMoveUiTo.Filters)
            {
                TabControlPrime.SelectedItem = TabControlPrime.Items[3];
                GridFilters.Background = Brushes.DarkOrange;
            }
            if (move == NeadToMoveUiTo.TabsAndTimeFrames)
            {
                TabControlPrime.SelectedItem = TabControlPrime.Items[0];
                RectangleTimeFramesAndSecurities.Fill = Brushes.DarkOrange;
            }
            if (move == NeadToMoveUiTo.Storage)
            {
                TabControlPrime.SelectedItem = TabControlPrime.Items[0];
                RectangleServerData.Fill = Brushes.DarkOrange;
            }
            if (move == NeadToMoveUiTo.Parametrs)
            {
                TabControlPrime.SelectedItem = TabControlPrime.Items[1];
                GridParametrs.Background = Brushes.DarkOrange;
            }

            Thread worker = new Thread(RePaintAreas);
            worker.IsBackground = true;
            worker.Start();
        }

        /// <summary>
        /// место работы потока который возвращает стандартные цвета для объектов на форме, 
        /// после того как мы подсветили какие-то из них для пользователя
        /// </summary>
        private void RePaintAreas()
        {
            if (!TabControlPrime.Dispatcher.CheckAccess())
            {
                Thread.Sleep(1000);
                TabControlPrime.Dispatcher.Invoke(RePaintAreas);
                return;
            }
            GridFazes.Background = Brushes.Black;
            RectangleTimeFramesAndSecurities.Fill = Brushes.Black;
            RectangleServerData.Fill = Brushes.Black;
            RectangleStrategyName.Fill = Brushes.Black;
            GridParametrs.Background = Brushes.Black;
            GridFilters.Background = Brushes.Black;
        }

// обработка контролов по нажатию их пользователем

        /// <summary>
        /// пользователь нажал на кнопку запускающую и останавливающую оптимизацию
        /// </summary>
        private void ButtonGo_Click(object sender, RoutedEventArgs e)
        {
            SaveParamsFromTable();

            if (ButtonGo.Content.ToString() == "Погнали!" &&
                _botsInSample != null)
            {
                AcceptDialogUi ui = new AcceptDialogUi("Предыдущие данные будут уничтожены!");

                ui.ShowDialog();

                if (!ui.UserAcceptActioin)
                {
                    return;
                }
            }

            if (ButtonGo.Content.ToString() == "Погнали!" && _master.Start())
            {
                ButtonGo.Content = "Остановить";
                StopUserActivity();
            }
            else if (ButtonGo.Content.ToString() == "Остановить")
            {
                _master.Stop();
                ButtonGo.Content = "Погнали!";
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

        private void CheckBoxOptimizationFunctionProfitFactor_Click(object sender, RoutedEventArgs e)
        {
            _master.TypeOprimizationFunction = OptimizationFunctionType.ProfitFactor;
            CheckBoxOptimizationFunctionMaxDrowDawn.IsChecked = false;
            CheckBoxOptimizationFunctionMiddleProfitFromPosition.IsChecked = false;
            CheckBoxOptimizationFunctionTypeEndProfit.IsChecked = false;
        }

        void CheckBoxOptimizationFunctionMaxDrowDawn_Click(object sender, RoutedEventArgs e)
        {
            _master.TypeOprimizationFunction = OptimizationFunctionType.MaxDrowDown;
            CheckBoxOptimizationFunctionProfitFactor.IsChecked = false;
            CheckBoxOptimizationFunctionMiddleProfitFromPosition.IsChecked = false;
            CheckBoxOptimizationFunctionTypeEndProfit.IsChecked = false;
        }

        void CheckBoxOptimizationFunctionMiddleProfitFromPosition_Click(object sender, RoutedEventArgs e)
        {
            _master.TypeOprimizationFunction = OptimizationFunctionType.MiddleProfitFromPosition;
            CheckBoxOptimizationFunctionProfitFactor.IsChecked = false;
            CheckBoxOptimizationFunctionMaxDrowDawn.IsChecked = false;
            CheckBoxOptimizationFunctionTypeEndProfit.IsChecked = false;
        }

        void CheckBoxOptimizationFunctionTypeEndProfit_Click(object sender, RoutedEventArgs e)
        {
            _master.TypeOprimizationFunction = OptimizationFunctionType.EndProfit;
            CheckBoxOptimizationFunctionProfitFactor.IsChecked = false;
            CheckBoxOptimizationFunctionMaxDrowDawn.IsChecked = false;
            CheckBoxOptimizationFunctionMiddleProfitFromPosition.IsChecked = false;
        }

        void CheckBoxOptimizationTypeGeneticAlgoritm_Click(object sender, RoutedEventArgs e)
        {
            CheckBoxOptimizationTypeSimulatedAnnealing.IsChecked = false;
            _master.TypeOptimization = OptimizationType.GeneticАlgorithm;
        }

        void CheckBoxOptimizationTypeSimulatedAnnealing_Click(object sender, RoutedEventArgs e)
        {
            CheckBoxOptimizationTypeGeneticAlgoritm.IsChecked = false;
            _master.TypeOptimization = OptimizationType.SimulatedAnnealing;
        }

        void TextBoxFiltertValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _master.FilterProfitFactorValue = Convert.ToDecimal(TextBoxFilterProfitValue.Text);
                _master.FilterMaxDrowDownValue = Convert.ToDecimal(TextBoxMaxDrowDownValue.Text);
                _master.FilterMiddleProfitValue = Convert.ToDecimal(TextBoxFilterMiddleProfitValue.Text);
                _master.FilterWinPositionValue = Convert.ToDecimal(TextBoxFilterWinPositionValue.Text);
                _master.FilterProfitFactorValue = Convert.ToDecimal(TextBoxFilterProfitFactorValue.Text);
            }
            catch
            {
                TextBoxFilterProfitValue.Text = _master.FilterProfitFactorValue.ToString();
                TextBoxMaxDrowDownValue.Text = _master.FilterMaxDrowDownValue.ToString();
                TextBoxFilterMiddleProfitValue.Text = _master.FilterMiddleProfitValue.ToString();
                TextBoxFilterWinPositionValue.Text = _master.FilterWinPositionValue.ToString();
                TextBoxFilterProfitFactorValue.Text = _master.FilterProfitFactorValue.ToString();
            }
        }

        void CheckBoxFilterIsOn_Click(object sender, RoutedEventArgs e)
        {
            _master.FilterProfitIsOn = CheckBoxFilterProfitIsOn.IsChecked.Value;
            _master.FilterMaxDrowDownIsOn = CheckBoxFilterMaxDrowDownIsOn.IsChecked.Value;
            _master.FilterMiddleProfitIsOn = CheckBoxFilterMiddleProfitIsOn.IsChecked.Value;
            _master.FilterWinPositionIsOn = CheckBoxFilterWinPositonIsOn.IsChecked.Value;
            _master.FilterProfitFactorIsOn = CheckBoxFilterProfitFactorIsOn.IsChecked.Value;
        }

        void _master_NewSecurityEvent(List<Security> securities)
        {
            PaintTableTabsSimple();
        }

        void ComboBoxNameStrategyToOptimization_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(ComboBoxNameStrategyToOptimization.SelectedItem == null)
            {
                return;
            }
            string name = ComboBoxNameStrategyToOptimization.SelectedItem.ToString();
            _master.StrategyName = name;
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

        private void ButtonServerDialog_Click(object sender, RoutedEventArgs e)
        {
            _master.ShowDataStorageDialog();
        }

// события из сервера

        /// <summary>
        /// входящее событие: изменилось кол-во стратегий доступных для оптимизации
        /// </summary>
        private void _master_StrategyNamesReadyEvent(List<string> strategy)
        {

            if (!ComboBoxNameStrategyToOptimization.Dispatcher.CheckAccess())
            {
                ComboBoxNameStrategyToOptimization.Dispatcher.Invoke(
                    new Action<List<string>>(_master_StrategyNamesReadyEvent), strategy);
                return;
            }

            if (strategy.Count == ComboBoxNameStrategyToOptimization.Items.Count)
            {
                return;
            }

            _master.SendLogMessage("Найдено стратегий с параметрами: " + strategy.Count, LogMessageType.System);

            ComboBoxNameStrategyToOptimization.Items.Clear();

            for (int i = 0; i < strategy.Count; i++)
            {
                ComboBoxNameStrategyToOptimization.Items.Add(strategy[i]);
            }
            ComboBoxNameStrategyToOptimization.SelectionChanged += ComboBoxNameStrategyToOptimization_SelectionChanged;
            ComboBoxNameStrategyToOptimization.SelectedItem = _master.StrategyName;

            if (ComboBoxNameStrategyToOptimization.SelectedItem != null)
            {
                PaintTableParametrs();
            }

        }

        /// <summary>
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

// таблица Бумаг и таймФреймов для обычных вкладок

        /// <summary>
        /// таблица с записями настроек для обычных вкладок у робота
        /// </summary>
        private DataGridView _gridTableTabsSimple;

        /// <summary>
        /// создать таблицу для обычных вкладок
        /// </summary>
        private void CreateTableTabsSimple()
        {
            _gridTableTabsSimple = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.ColumnHeaderSelect, DataGridViewAutoSizeRowsMode.None);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridTableTabsSimple.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = @"№ вкладки";
            column0.ReadOnly = true;
            column0.Width = 100;

            _gridTableTabsSimple.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = @"Бумага";
            column1.ReadOnly = false;
            column1.Width = 150;

            _gridTableTabsSimple.Columns.Add(column1);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = @"ТаймФрейм";
            column.ReadOnly = false;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridTableTabsSimple.Columns.Add(column);

            _gridTableTabsSimple.Rows.Add(null, null);

            HostTabsSimple.Child = _gridTableTabsSimple;

            _gridTableTabsSimple.CellValueChanged += _grid_CellValueChanged;
        }

        /// <summary>
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

            if(securities == null)
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
            }


            int countTab = 0;
            string nameBot = _master.StrategyName;

            BotPanel bot = PanelCreator.GetStrategyForName(nameBot, "",StartProgram.IsOsOptimizer);
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
        /// пользователь поменял что-то в таблице обычных вкладок робота
        /// </summary>
        void _grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        
        {
            for (int i = 0; i < _gridTableTabsSimple.Rows.Count; i++)
            {
                if(_gridTableTabsSimple.Rows[i].Cells[1].Value == null ||
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

// таблица Бумаг и таймФреймов для индексов

        /// <summary>
        /// таблица с настройками вкладок с индексами
        /// </summary>
        private DataGridView _gridTableTabsIndex;

        /// <summary>
        /// создать таблицу с настройками вкладок с индексами
        /// </summary>
        private void CreateTableTabsIndex()
        {
            _gridTableTabsIndex  = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.ColumnHeaderSelect, DataGridViewAutoSizeRowsMode.None);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridTableTabsIndex.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = @"№ вкладки";
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

            if (securities == null)
            {
                return;
            }
            if (_gridTableTabsIndex == null)
            {
                return;
            }

            if (_gridTableTabsIndex.InvokeRequired)
            {
                _gridTableTabsIndex.Invoke(new Action(PaintTableTabsIndex));
                return;
            }

            _gridTableTabsIndex.Rows.Clear();

            int countTab = 0;
            string nameBot = _master.StrategyName;

            BotPanel bot = PanelCreator.GetStrategyForName(nameBot, "", StartProgram.IsOsOptimizer);
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
                cell2.Value = "Настроить";
                row.Cells.Add(cell2);

                _gridTableTabsIndex.Rows.Insert(0, row);
            }
        }

        /// <summary>
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


// таблица этапов тестирования

        /// <summary>
        /// обработчик для нажатия на кнопку создания этапов оптимизации
        /// </summary>
        private void ButtonCreateOptimizeFazes_Click(object sender, RoutedEventArgs e)
        {
            _master.ReloadFazes();
            PaintTableOptimizeFazes();
        }

        /// <summary>
        /// таблица с этапами оптимизации
        /// </summary>
        private DataGridView _gridFazes;

        /// <summary>
        /// создать таблицу с фазами оптимизации
        /// </summary>
        private void CreateTableOptimizeFazes()
        {
            _gridFazes = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.None);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridFazes.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = @"№ шага";
            column0.ReadOnly = true;
            column0.Width = 100;

            _gridFazes.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = @"Тип";
            column1.ReadOnly = true;
            column1.Width = 150;

            _gridFazes.Columns.Add(column1);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = @"Начало";
            column.ReadOnly = true;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridFazes.Columns.Add(column);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = @"Конец";
            column2.ReadOnly = true;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridFazes.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = @"Дней";
            column3.ReadOnly = true;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridFazes.Columns.Add(column3);

            _gridFazes.Rows.Add(null, null);

            HostStepsOptimize.Child = _gridFazes;
        }

        /// <summary>
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
                row.Cells[0].Value = i+1;

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

// таблица параметров

        /// <summary>
        /// параметры для оптимизации текущего робота
        /// </summary>
        private List<IIStrategyParameter> _parameters;

        /// <summary>
        /// список включенных параметров
        /// </summary>
        private List<bool> _parametrsActiv; 

        /// <summary>
        /// таблица с параметрами оптимизации
        /// </summary>
        private DataGridView _gridParametrs;

        /// <summary>
        /// создать таблицу параметров
        /// </summary>
        private void CreateTableParametrs()
        {
            _gridParametrs = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.None);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridParametrs.DefaultCellStyle;

            DataGridViewCheckBoxColumn column0 = new DataGridViewCheckBoxColumn();
            column0.CellTemplate = new DataGridViewCheckBoxCell();
            column0.HeaderText = @"Вкл/Выкл";
            column0.ReadOnly = false;
            column0.Width = 100;

            _gridParametrs.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = @"Название";
            column1.ReadOnly = true;
            column1.Width = 150;

            _gridParametrs.Columns.Add(column1);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = @"Тип";
            column.ReadOnly = true;
            column1.Width = 100;
            _gridParametrs.Columns.Add(column);

            DataGridViewComboBoxColumn column2 = new DataGridViewComboBoxColumn();
            column2.CellTemplate = new DataGridViewComboBoxCell();
            column2.HeaderText = @"По умолчанию";
            column2.ReadOnly = false;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridParametrs.Columns.Add(column2);

            DataGridViewColumn column22 = new DataGridViewColumn();
            column22.CellTemplate = cell0;
            column22.HeaderText = @"Стартовое значение";
            column22.ReadOnly = false;
            column22.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridParametrs.Columns.Add(column22);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = @"Шаг приращения";
            column3.ReadOnly = false;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridParametrs.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = @"Последнее значение";
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridParametrs.Columns.Add(column4);

            _gridParametrs.Rows.Add(null, null);

            HostParam.Child = _gridParametrs;
        }

        /// <summary>
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
                    DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();
                    StrategyParameterString param = (StrategyParameterString) _parameters[i];

                    for (int i2 = 0; i2 < param.ValuesString.Count; i2++)
                    {
                        cell.Items.Add(param.ValuesString[i2]);
                    }
                    cell.Value = param.ValueString;
                    row.Cells.Add(cell);
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
                            MessageBox.Show("Стартовое значение не может быть больше конечного");
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
                            MessageBox.Show("Стартовое значение не может быть больше конечного");
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
        /// пользователь изменил что-то в таблице параметров
        /// </summary>
        void _gridParametrs_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            SaveParamsFromTable();
        }

// таблица фаз для переключения после тестирования

        /// <summary>
        /// таблица с этапами оптимизации на вкладке итогов
        /// </summary>
        private DataGridView _gridFazesEnd;

        /// <summary>
        /// создать таблицу фаз на вкладки итогов
        /// </summary>
        private void CreateTableFazes()
        {
            _gridFazesEnd = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.None);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridFazesEnd.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = @"№ шага";
            column0.ReadOnly = true;
            column0.Width = 100;

            _gridFazesEnd.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = @"Тип";
            column1.ReadOnly = true;
            column1.Width = 150;

            _gridFazesEnd.Columns.Add(column1);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = @"Начало";
            column.ReadOnly = true;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridFazesEnd.Columns.Add(column);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = @"Конец";
            column2.ReadOnly = true;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridFazesEnd.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = @"Дней";
            column3.ReadOnly = true;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridFazesEnd.Columns.Add(column3);

            _gridFazesEnd.Rows.Add(null, null);

            WindowsFormsHostFazeNumOnTubResult.Child = _gridFazesEnd;

            _gridFazesEnd.CellClick += _gridFazesEnd_CellClick;
        }

        /// <summary>
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
        /// пользователь кликнул по таблице фаз на вкладке итогов
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void _gridFazesEnd_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            PaintTableResults();
        }

        
// таблица результатов оптимизации

        /// <summary>
        /// таблица с этапами оптимизации
        /// </summary>
        private DataGridView _gridResults;

        /// <summary>
        /// создать таблицу результатов
        /// </summary>
        private void CreateTableResults()
        {
            _gridResults = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.ColumnHeaderSelect, DataGridViewAutoSizeRowsMode.None);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridResults.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = @"Имя";
            column0.ReadOnly = true;
            column0.Width = 150;

            _gridResults.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = @"Профит в % к депо";
            column1.ReadOnly = false;
            column1.Width = 150;

            _gridResults.Columns.Add(column1);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = @"Средняя прибыль в %";
            column.ReadOnly = false;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column);

            DataGridViewButtonColumn column2 = new DataGridViewButtonColumn();
            column2.CellTemplate = new DataGridViewButtonCell();
            column2.HeaderText = @"Параметры";
            column2.ReadOnly = true;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column2);

            DataGridViewButtonColumn column3 = new DataGridViewButtonColumn();
            column3.CellTemplate = new DataGridViewButtonCell();
            column3.HeaderText = @"Журнал";
            column3.ReadOnly = true;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column3);

            DataGridViewButtonColumn column4 = new DataGridViewButtonColumn();
            column4.CellTemplate = new DataGridViewButtonCell();
            column4.HeaderText = @"График";
            column4.ReadOnly = true;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column4);

            _gridResults.Rows.Add(null, null);

            WindowsFormsHostResults.Child = _gridResults;
        }

        /// <summary>
        /// прорисовать таблицу результатов
        /// </summary>
        private void PaintTableResults()
        {
            if (_gridResults.InvokeRequired)
            {
                _gridResults.Invoke(new Action(PaintTableResults));
                return;
            }
            _gridResults.SelectionChanged -= _gridResults_SelectionChanged;
            _gridResults.CellMouseClick -= _gridResults_CellMouseClick;

            _gridResults.Rows.Clear();

            List<BotPanel> bots = new List<BotPanel>();

            if (_gridFazesEnd.CurrentCell == null ||
                _gridFazesEnd.CurrentCell.RowIndex == 0)
            {
                bots = _botsInSample;
            }
            else
            {
                bots = _botsOutOfSample;
            }

            if (bots == null)
            {
                return;
            }

            if (_sortBotsType == SortBotsType.TotalProfit)
            {
                for (int i = 0; i < bots.Count; i++)
                {
                    for (int i2 = 1; i2 < bots.Count; i2++)
                    {
                        if (bots[i2].TotalProfitInPersent > bots[i2 - 1].TotalProfitInPersent)
                        {
                            BotPanel bot = bots[i2];
                            bots[i2] = bots[i2 - 1];
                            bots[i2 - 1] = bot;
                        }
                    }
                }
            }
            else if (_sortBotsType == SortBotsType.MiddleProfit)
            {
                for (int i = 0; i < bots.Count; i++)
                {
                    for (int i2 = 1; i2 < bots.Count; i2++)
                    {
                        if (bots[i2].MiddleProfitInPersent > bots[i2 - 1].MiddleProfitInPersent)
                        {
                            BotPanel bot = bots[i2];
                            bots[i2] = bots[i2 - 1];
                            bots[i2 - 1] = bot;
                        }
                    }
                }
            }

            for (int i = 0; i < bots.Count; i++)
            {
                DataGridViewRow row = new DataGridViewRow();

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = bots[i].NameStrategyUniq;

                DataGridViewTextBoxCell cell2 = new DataGridViewTextBoxCell();
                cell2.Value = bots[i].TotalProfitInPersent.ToString();
                row.Cells.Add(cell2);

                DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell();
                cell3.Value = bots[i].MiddleProfitInPersent.ToString();
                row.Cells.Add(cell3);

                DataGridViewButtonCell cell4 = new DataGridViewButtonCell();
                cell4.Value = "Параметры";
                row.Cells.Add(cell4);

                DataGridViewButtonCell cell5 = new DataGridViewButtonCell();
                cell5.Value = "Журнал сделок";
                row.Cells.Add(cell5);

                DataGridViewButtonCell cell6 = new DataGridViewButtonCell();
                cell6.Value = "График";
                row.Cells.Add(cell6);

                _gridResults.Rows.Add(row);
            }
            _gridResults.SelectionChanged += _gridResults_SelectionChanged;
            _gridResults.CellMouseClick += _gridResults_CellMouseClick;
        }

        /// <summary>
        /// пользователь кликнул по таблице результатов
        /// </summary>
        void _gridResults_SelectionChanged(object sender, EventArgs e)
        {
            if (_gridResults.SelectedCells.Count == 0)
            {
                return;
            }
            int columnSelect = _gridResults.SelectedCells[0].ColumnIndex;

            if (columnSelect == 1)
            {
                _sortBotsType = SortBotsType.TotalProfit;
                PaintTableResults();
            }
            else if (columnSelect == 2)
            {
                _sortBotsType = SortBotsType.MiddleProfit;
                PaintTableResults();
            }
            
        }

        /// <summary>
        /// тип сортировки роботов в таблице результатов
        /// </summary>
        private SortBotsType _sortBotsType;

        /// <summary>
        /// пользователь нажал на кнопку в таблице результатов
        /// </summary>
        void _gridResults_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex<0)
            {
                return;
            }

            if (e.ColumnIndex != 3 &&
                e.ColumnIndex != 4 && 
                e.ColumnIndex != 5)
            {
                return;
            }

            List<BotPanel> bots = new List<BotPanel>();
            if (_gridFazesEnd.CurrentCell == null ||
              _gridFazesEnd.CurrentCell.RowIndex == 0)
            {
                bots = _botsInSample;
            }
            else
            {
                bots = _botsOutOfSample;
            }

            if (_sortBotsType == SortBotsType.TotalProfit)
            {
                for (int i = 0; i < bots.Count; i++)
                {
                    for (int i2 = 1; i2 < bots.Count; i2++)
                    {
                        if (bots[i2].TotalProfitInPersent > bots[i2-1].TotalProfitInPersent)
                        {
                            BotPanel bot = bots[i2];
                            bots[i2] = bots[i2 - 1];
                            bots[i2 - 1] = bot;
                        }
                    }
                }
            }
            else if (_sortBotsType == SortBotsType.MiddleProfit)
            {
                for (int i = 0; i < bots.Count; i++)
                {
                    for (int i2 = 1; i2 < bots.Count; i2++)
                    {
                        if (bots[i2].MiddleProfitInPersent > bots[i2 - 1].MiddleProfitInPersent)
                        {
                            BotPanel bot = bots[i2];
                            bots[i2] = bots[i2 - 1];
                            bots[i2 - 1] = bot;
                        }
                    }
                }
            }

            if (e.ColumnIndex == 3)
            {
                bots[e.RowIndex].ShowParametrDialog();
            }
            else if (e.ColumnIndex == 4)
            {
                if (_journalUi != null)
                {
                    _journalUi.Activate();
                    return;
                }

                List<BotPanelJournal> panelsJournal = new List<BotPanelJournal>();

                List<Journal.Journal> journals = bots[e.RowIndex].GetJournals();

                    if (journals == null)
                    {
                       return;
                    }

                    BotPanelJournal botPanel = new BotPanelJournal();
                    botPanel.BotName = bots[e.RowIndex].NameStrategyUniq;
                    botPanel._Tabs = new List<BotTabJournal>();

                    for (int i2 = 0; journals != null && i2 < journals.Count; i2++)
                    {
                        BotTabJournal botTabJournal = new BotTabJournal();
                        botTabJournal.TabNum = i2;
                        botTabJournal.Journal = journals[i2];
                        botPanel._Tabs.Add(botTabJournal);
                    }

                    panelsJournal.Add(botPanel);
                

                _journalUi = new JournalUi(panelsJournal,StartProgram.IsOsOptimizer);
                _journalUi.Closed += _journalUi_Closed;
                _journalUi.Show();
            }
            if (e.ColumnIndex == 5)
            {
                bots[e.RowIndex].ShowChartDialog();
            }
        }

        /// <summary>
        /// ГУИ журнала
        /// </summary>
        private JournalUi _journalUi;

        /// <summary>
        /// входящее событие: журнал закрыт пользователем
        /// </summary>
        void _journalUi_Closed(object sender, EventArgs e)
        {
            _journalUi.IsErase = true;
            _journalUi = null;
        }

    }

    /// <summary>
    /// тип сортировки в таблице результатов
    /// </summary>
    public enum SortBotsType
    {
        /// <summary>
        /// по общему профиту
        /// </summary>
        TotalProfit,

        /// <summary>
        /// по среднему профиту с одной сделки
        /// </summary>
        MiddleProfit
    }
}
