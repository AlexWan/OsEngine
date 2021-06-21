/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using Color = System.Drawing.Color;

namespace OsEngine.Market.Servers.Tester
{
    /// <summary>
    /// Interaction logic for TesterServerUi.xaml
    /// Логика взаимодействия для TesterServerUi.xaml
    /// </summary>
    public partial class TesterServerUi 
    {

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="server">server/сервер</param>
        /// <param name="log">log/лог</param>
        public TesterServerUi(TesterServer server, Log log)
        {
            InitializeComponent();
            _server = server;
            _server.LoadSecurityTestSettings();
            LabelStatus.Content = _server.ServerStatus;

            _server.ConnectStatusChangeEvent += _server_ConnectStatusChangeEvent;
            log.StartPaint(Host);

            TextBoxStartDepozit.Text = _server.StartPortfolio.ToString(new CultureInfo("ru-RU"));
            TextBoxStartDepozit.TextChanged += TextBoxStartDepozit_TextChanged;

            if (_server.ProfitMarketIsOn == true)
            {
                CheckBoxOnOffMarketPortfolio.IsChecked = true;
            }
            else
            {
                CheckBoxOnOffMarketPortfolio.IsChecked = false;
            }

            ResizeMode = System.Windows.ResizeMode.NoResize;
            HostSecurities.Visibility = Visibility.Hidden;
            Host.Visibility = Visibility.Hidden;
            SliderTo.Visibility = Visibility.Hidden;
            SliderFrom.Visibility = Visibility.Hidden;
            TextBoxFrom.Visibility = Visibility.Hidden;
            TextBoxTo.Visibility = Visibility.Hidden;
            LabelFrom.Visibility = Visibility.Hidden;
            LabelTo.Visibility = Visibility.Hidden;
            TextBoxStartDepozit.Visibility = Visibility.Hidden;
            ComboBoxDataType.Visibility = Visibility.Hidden;
            ComboBoxSets.Visibility = Visibility.Hidden;
            Height = 130;
            Width = 570;

            _server.TestingStartEvent += _server_TestingStartEvent;
            _server.SecuritiesChangeEvent += _server_SecuritiesChangeEvent;

            CreateGrid();
            PaintGrid();


            TextBoxFrom.TextChanged += TextBoxFrom_TextChanged;
            TextBoxTo.TextChanged += TextBoxTo_TextChanged;

            TextBoxSlipageSimpleOrder.Text = _server.SlipageToSimpleOrder.ToString(new CultureInfo("ru-RU"));
            TextBoxSlipageSimpleOrder.TextChanged += TextBoxSlipageSimpleOrderTextChanged;

            TextBoxSlipageStop.Text = _server.SlipageToStopOrder.ToString(new CultureInfo("ru-RU"));
            TextBoxSlipageStop.TextChanged += TextBoxSlipageStop_TextChanged;

            if (_server.SlipageToStopOrder == 0)
            {
                CheckBoxSlipageStopOff.IsChecked = true;
            }
            else
            {
                CheckBoxSlipageStopOn.IsChecked = false;
            }

            if (_server.SlipageToSimpleOrder == 0)
            {
                CheckBoxSlipageLimitOff.IsChecked = true;
            }
            else
            {
                CheckBoxSlipageLimitOn.IsChecked = false;
            }

            if (_server.OrderExecutionType == OrderExecutionType.Touch)
            {
                CheckBoxExecutionOrderTuch.IsChecked = true;
            }
            else if (_server.OrderExecutionType == OrderExecutionType.Intersection)
            {
                CheckBoxExecutionOrderIntersection.IsChecked = true;
            }
            else if (_server.OrderExecutionType == OrderExecutionType.FiftyFifty)
            {
                CheckBoxExecutionOrderFiftyFifty.IsChecked = true;
            }

            // progress bar/прогресс бар

            server.TestingNewSecurityEvent += server_TestingNewSecurityEvent;

            ProgressBar.Maximum = (_server.TimeMax - DateTime.MinValue).TotalMinutes;
            ProgressBar.Minimum = (_server.TimeMin - DateTime.MinValue).TotalMinutes;
            ProgressBar.Value = (_server.TimeNow - DateTime.MinValue).TotalMinutes;

            barUpdater = new Thread(UpdaterProgressBarThreadArea);
            barUpdater.CurrentCulture = new CultureInfo("ru-RU");
            barUpdater.IsBackground = true;
            barUpdater.Start();
            Closing += TesterServerUi_Closing;

            // chart/чарт

            CreateChart();

            PaintProfitOnChart();

            Resize();

            _chartActive = true;

            _server.NewCurrentValue += _server_NewCurrentValue;

            List<string> sets = _server.Sets;

            // sets/сеты

            for (int i = 0;sets != null && sets.Count != 0 && i < sets.Count; i++)
            {
                ComboBoxSets.Items.Add(sets[i]);
            }
            if (!string.IsNullOrEmpty(_server.ActiveSet) &&
                _server.ActiveSet.Split('_').Length == 2)
            {
                ComboBoxSets.SelectedItem = _server.ActiveSet.Split('_')[1];
            }
            
            ComboBoxSets.SelectionChanged += ComboBoxSets_SelectionChanged;

            // data for test/данные для тестирования

            ComboBoxDataType.Items.Add(TesterDataType.Candle);
            ComboBoxDataType.Items.Add(TesterDataType.TickAllCandleState);
            ComboBoxDataType.Items.Add(TesterDataType.TickOnlyReadyCandle);
            ComboBoxDataType.Items.Add(TesterDataType.MarketDepthAllCandleState);
            ComboBoxDataType.Items.Add(TesterDataType.MarketDepthOnlyReadyCandle);
            ComboBoxDataType.SelectedItem = _server.TypeTesterData;
            ComboBoxDataType.SelectionChanged +=ComboBoxDataType_SelectionChanged;

            TextBoxDataPath.Text = _server.PathToFolder;
            ComboBoxDataSourseType.Items.Add(TesterSourceDataType.Folder);
            ComboBoxDataSourseType.Items.Add(TesterSourceDataType.Set);
            ComboBoxDataSourseType.SelectedItem = _server.SourceDataType;
            ComboBoxDataSourseType.SelectionChanged += ComboBoxDataSourseType_SelectionChanged;

            ButtonSinhronazer.Content = OsLocalization.Market.Button1;
            Title = OsLocalization.Market.TitleTester;
            Label21.Content = OsLocalization.Market.Label21;
            ButtonStartTest.Content = OsLocalization.Market.Button2;
            Label29.Header = OsLocalization.Market.Label29;
            Label30.Header = OsLocalization.Market.Label30;
            Label31.Header = OsLocalization.Market.Label31;
            Label23.Header = OsLocalization.Market.Label23;
            Label24.Content = OsLocalization.Market.Label24;
            Label25.Content = OsLocalization.Market.Label25;
            LabelFrom.Content = OsLocalization.Market.Label26;
            LabelTo.Content = OsLocalization.Market.Label27;
            Label28.Content = OsLocalization.Market.Label28;
            ButtonSetDataFromPath.Content = OsLocalization.Market.ButtonSetFolder;
            Label32.Content = OsLocalization.Market.Label32;
            Label33.Content = OsLocalization.Market.Label33;
            Label34.Content = OsLocalization.Market.Label34;
            CheckBoxSlipageLimitOff.Content = OsLocalization.Market.Label35;
            CheckBoxSlipageStopOff.Content = OsLocalization.Market.Label35;
            CheckBoxSlipageLimitOn.Content = OsLocalization.Market.Label36;
            CheckBoxSlipageStopOn.Content = OsLocalization.Market.Label36;
            CheckBoxExecutionOrderIntersection.Content = OsLocalization.Market.Label38;
            CheckBoxExecutionOrderTuch.Content = OsLocalization.Market.Label37;
            CheckBoxOnOffMarketPortfolio.Content = OsLocalization.Market.Label39;
            Label40.Content = OsLocalization.Market.Label40;

        }

        /// <summary>
        /// window is closing
        /// окно закрывается
        /// </summary>
        void TesterServerUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            barUpdater.Abort();
        }

        /// <summary>
        /// data source has changed. Folder or Set
        /// источник данных изменился. Папка или Сет 
        /// </summary>
        void ComboBoxDataSourseType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            TesterSourceDataType sourceDataType;
            Enum.TryParse(ComboBoxDataSourseType.SelectedItem.ToString(), out sourceDataType);
            _server.SourceDataType = sourceDataType;
        }

        /// <summary>
        /// translation type has changed
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

        /// <summary>
        /// thread updates a progress bar
        /// поток обновляющий полосу прогресса
        /// </summary>
        private readonly Thread barUpdater;

        /// <summary>
        /// work method of thread updates a progress bar
        /// метод работы потока обновляющего прогресс бар
        /// </summary>
        private void UpdaterProgressBarThreadArea()
        {
            while (true)
            {
                Thread.Sleep(100);

                ChangeProgressBar();
            }
        }

        /// <summary>
        /// updat progress bar
        /// обновить прогресс бар
        /// </summary>
        void ChangeProgressBar()
        {
            if (!ProgressBar.Dispatcher.CheckAccess())
            {
                ProgressBar.Dispatcher.Invoke(ChangeProgressBar);
                return;
            }

            ProgressBar.Value = (_server.TimeNow - DateTime.MinValue).TotalMinutes;
        }

        /// <summary>
        /// changed slippage window
        /// изменилось окно проскальзывания
        /// </summary>
        void TextBoxSlipageSimpleOrderTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                _server.SlipageToSimpleOrder = Convert.ToInt32(TextBoxSlipageSimpleOrder.Text);
                _server.Save();
            }
            catch (Exception)
            {
                TextBoxSlipageSimpleOrder.Text = _server.SlipageToSimpleOrder.ToString(new CultureInfo("ru-RU"));
                // ignore
            }
            
        }

        /// <summary>
        /// changed window with value of initial deposit
        /// изменилось окно со значением начального депозита
        /// </summary>
        void TextBoxStartDepozit_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                _server.StartPortfolio = TextBoxStartDepozit.Text.ToDecimal();
                _server.Save();
            }
            catch (Exception)
            {
                TextBoxStartDepozit.Text = _server.StartPortfolio.ToString(new CultureInfo("ru-RU"));
                // ignore
            }
        }

        void TextBoxSlipageStop_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                _server.SlipageToStopOrder = Convert.ToInt32(TextBoxSlipageStop.Text);
                _server.Save();
            }
            catch (Exception)
            {
                TextBoxSlipageStop.Text = _server.SlipageToStopOrder.ToString(new CultureInfo("ru-RU"));
                // ignore
            }
        }

// server/сервер

        /// <summary>
        /// test server
        /// тестовый сервер
        /// </summary>
        private TesterServer _server;

        /// <summary>
        /// changed server status 
        /// изменился статус сервера
        /// </summary>
        void _server_ConnectStatusChangeEvent(string status)
        {
            if (!LabelStatus.Dispatcher.CheckAccess())
            {
                LabelStatus.Dispatcher.Invoke(new Action<string>(_server_ConnectStatusChangeEvent), status);
                return;
            }
            LabelStatus.Content = status;
        }

        /// <summary>
        /// changed server instrument
        /// изменились инструменты в сервере
        /// </summary>
        void _server_SecuritiesChangeEvent(List<Security> securities)
        {
            PaintGrid();
        }

        /// <summary>
        /// start testing event
        /// событие начала тестирования
        /// </summary>
        void _server_TestingStartEvent()
        {
            _chartActive = true;
            CreateChart();
            PaintGrid();
        }

        /// <summary>
        /// add a new security to the tester
        /// добавлена новая бумага в тестер
        /// </summary>
        void server_TestingNewSecurityEvent()
        {
            PaintGrid();
        }

        // button handlers / обработчики кнопок

        private void buttonFast_Click(object sender, RoutedEventArgs e)
        {
            _server.TestingFast();
        }

        private void buttonPausePlay_Click(object sender, RoutedEventArgs e)
        {
            _server.TestingPausePlay();

            if (ButtonPausePlay.Content.ToString() == "| |")
            {
                ButtonPausePlay.Content = ">";
            }
            else
            {
                ButtonPausePlay.Content = "| |";
            }
        }

        private void buttonNextCandle_Click(object sender, RoutedEventArgs e)
        {
            _server.TestingPlusOne();
        }

        private void buttonStartTest_Click(object sender, RoutedEventArgs e)
        {
            Thread worker = new Thread(_server.TestingStart);
            worker.CurrentCulture = new CultureInfo("ru-RU");
            worker.IsBackground = true;
            worker.Start();
            ButtonPausePlay.Content = "| |";
        }

        private void ButtonSinhronazer_Click(object sender, RoutedEventArgs e)
        {
            if (HostSecurities.Visibility == Visibility.Hidden)
            {
                // if need to expand the form/ если нужно раскрывать форму
                Height = 600;
                Width = 835;
                MinWidth = 835;
                HostSecurities.Visibility = Visibility.Visible;
                Host.Visibility = Visibility.Visible;
                SliderFrom.Visibility = Visibility.Visible;
                SliderTo.Visibility = Visibility.Visible;
                TextBoxFrom.Visibility = Visibility.Visible;
                TextBoxTo.Visibility = Visibility.Visible;
                LabelFrom.Visibility = Visibility.Visible;
                LabelTo.Visibility = Visibility.Visible;
                TextBoxStartDepozit.Visibility = Visibility.Visible;
                ResizeMode = System.Windows.ResizeMode.CanResize;
                ComboBoxDataType.Visibility = Visibility.Visible;
                ComboBoxSets.Visibility = Visibility.Visible;
            }
            else
            {
                ResizeMode = System.Windows.ResizeMode.NoResize;
                // if need to hide / если нужно прятать
                HostSecurities.Visibility = Visibility.Hidden;
                Host.Visibility = Visibility.Hidden;
                SliderTo.Visibility = Visibility.Hidden;
                SliderFrom.Visibility = Visibility.Hidden;
                TextBoxFrom.Visibility = Visibility.Hidden;
                TextBoxTo.Visibility = Visibility.Hidden;
                LabelFrom.Visibility = Visibility.Hidden;
                LabelTo.Visibility = Visibility.Hidden;
                TextBoxStartDepozit.Visibility = Visibility.Hidden;
                ComboBoxDataType.Visibility = Visibility.Hidden;
                ComboBoxSets.Visibility = Visibility.Hidden;
                Height = 130;
                Width = 570;
                MinWidth = 570;
            }
        }

// chart/чарт

        /// <summary>
        /// report chart
        /// чарт для отчёта
        /// </summary>
        Chart _chartReport;

        /// <summary>
        /// create chart
        /// создать чарт
        /// </summary>
        private void CreateChart()
        {
            if (!HostPortfolio.Dispatcher.CheckAccess())
            {
                HostPortfolio.Dispatcher.Invoke(CreateChart);
                return;
            }

            _chartReport = new Chart();
            HostPortfolio.Child = _chartReport;
            HostPortfolio.Child.Show();

            _chartReport.Series.Clear();
            _chartReport.ChartAreas.Clear();

            ChartArea areaLineProfit = new ChartArea("ChartAreaProfit");
            areaLineProfit.Position.Height = 70;
            areaLineProfit.Position.Width = 100;
            areaLineProfit.Position.Y = 0;
            areaLineProfit.CursorX.IsUserSelectionEnabled = false; 
            areaLineProfit.CursorX.IsUserEnabled = false; 
            areaLineProfit.AxisX.Enabled = AxisEnabled.False;

            _chartReport.ChartAreas.Add(areaLineProfit);

            Series profit = new Series("SeriesProfit");

            profit.ChartType = SeriesChartType.Line;
            profit.Color = Color.DeepSkyBlue;
            profit.YAxisType = AxisType.Secondary;
            profit.ChartArea = "ChartAreaProfit";
            profit.ShadowOffset = 2;
            _chartReport.Series.Add(profit);

            ChartArea areaLineProfitBar = new ChartArea("ChartAreaProfitBar");
            areaLineProfitBar.AlignWithChartArea = "ChartAreaProfit";
            areaLineProfitBar.Position.Height = 30;
            areaLineProfitBar.Position.Width = 100;
            areaLineProfitBar.Position.Y = 70;
            areaLineProfitBar.AxisX.Enabled = AxisEnabled.False;


            _chartReport.ChartAreas.Add(areaLineProfitBar);

            Series profitBar = new Series("SeriesProfitBar");
            profitBar.ChartType = SeriesChartType.Column;
            profitBar.YAxisType = AxisType.Secondary;
            profitBar.ChartArea = "ChartAreaProfitBar";
            profitBar.ShadowOffset = 2;
            _chartReport.Series.Add(profitBar);

            _chartReport.BackColor = Color.FromArgb(-15395563);

            for (int i = 0; _chartReport.ChartAreas != null && i < _chartReport.ChartAreas.Count; i++)
            {
                _chartReport.ChartAreas[i].BackColor = Color.FromArgb(-15395563);
                _chartReport.ChartAreas[i].BorderColor = Color.FromArgb(-16701360);
                _chartReport.ChartAreas[i].CursorY.LineColor = Color.DimGray;
                _chartReport.ChartAreas[i].CursorX.LineColor = Color.DimGray;
                _chartReport.ChartAreas[i].AxisX.TitleForeColor = Color.DimGray;

                foreach (var axe in _chartReport.ChartAreas[i].Axes)
                {
                    axe.LabelStyle.ForeColor = Color.DimGray;
                }
            }

        }

        /// <summary>
        /// paint ENTIRE chart
        /// прорисовать ВЕСЬ чарт
        /// </summary>
        private void PaintProfitOnChart()
        {
            List<decimal> portfolio = _server.ProfitArray;

            if (portfolio == null || portfolio.Count == 0)
            {
                return;
            }

            for (int i = 0; i < portfolio.Count; i++)
            {
                if (portfolio[i] != 0)
                {
                    _chartReport.Series[0].Points.AddXY(i, portfolio[i]);

                    if (i == 0)
                    {
                        _chartReport.Series[1].Points.AddXY(i,  portfolio[i] - 1000000);
                        continue;
                    }

                    _chartReport.Series[1].Points.AddXY(i, portfolio[i] - portfolio[i-1]);

                    if (portfolio[i] - portfolio[i - 1] > 0)
                    {
                        _chartReport.Series[1].Points[_chartReport.Series[1].Points.Count - 1].Color = Color.DeepSkyBlue;
                    }
                    else
                    {
                        _chartReport.Series[1].Points[_chartReport.Series[1].Points.Count - 1].Color = Color.DarkRed;
                    }
                }
            }
        }

        /// <summary>
        /// paint the last data
        /// прорисовать последнии данные
        /// </summary>
        private void PaintLastPointOnChart()
        {
            if (_chartReport.InvokeRequired)
            {
                _chartReport.Invoke(new Action(PaintLastPointOnChart));
                return;
            }
            List<decimal> portfolio = _server.ProfitArray;

            if (portfolio.Count == 0)
            {
                return;
            }

            if (portfolio.Count != 0)
            {
                _chartReport.Series[0].Points.AddXY(_chartReport.Series[0].Points.Count, portfolio[portfolio.Count-1]);

                if (portfolio.Count == 1)
                {
                    _chartReport.Series[1].Points.AddXY(_chartReport.Series[1].Points.Count, portfolio[0] - 1000000);
                   return;
                }

                _chartReport.Series[1].Points.AddXY(_chartReport.Series[1].Points.Count, portfolio[portfolio.Count - 1] - portfolio[portfolio.Count - 1 - 1]);

                if (portfolio[portfolio.Count - 1] - portfolio[portfolio.Count - 1 - 1] > 0)
                {
                    _chartReport.Series[1].Points[_chartReport.Series[1].Points.Count - 1].Color = Color.DeepSkyBlue;
                }
                else
                {
                    _chartReport.Series[1].Points[_chartReport.Series[1].Points.Count - 1].Color = Color.DarkRed;
                }
            }

            if (_chartReport.ChartAreas[0] != null && _chartReport.ChartAreas[0].AxisX.ScrollBar.IsVisible)
            //if you have already selected a range/если уже выбран какой-то диапазон
            {
                // shift view to the right/сдвигаем представление вправо
                _chartReport.ChartAreas[0].AxisX.ScaleView.Scroll(_chartReport.ChartAreas[0].AxisX.Maximum + 1);
            }

            Resize();
        }

        /// <summary>
        /// chart is ready for step-by-step drawing
        /// чарт готов к пошаговой прорисовке
        /// </summary>
        private bool _chartActive;

        /// <summary>
        /// new value of portfolio from the server has come
        /// пришло новое значение портфеля из сервера
        /// </summary>
        void _server_NewCurrentValue(decimal val)
        {
            if (_chartActive == false)
            {
                return;
            }

            PaintLastPointOnChart();
        }

        private void Resize()
        {
            if (_chartReport.InvokeRequired)
            {
                _chartReport.Invoke(new Action(Resize));
                return;
            }

            Series profitSeries = _chartReport.Series.FindByName("SeriesProfit");

            ChartArea area = _chartReport.ChartAreas[0];

            if (profitSeries == null ||
                profitSeries.Points == null ||
                profitSeries.Points.Count < 1)
            {
                return;
            }

            int firstX = 0; // first candle displayed/первая отображаемая свеча
            int lastX = profitSeries.Points.Count; // последняя отображаемая свеча

            if (_chartReport.ChartAreas[0].AxisX.ScrollBar.IsVisible)
            {// if you have already selected a range, assign the first and last based on this range/если уже выбран какой-то диапазон, назначаем первую и последнюю исходя из этого диапазона
                firstX = Convert.ToInt32(area.AxisX.ScaleView.Position);
                lastX = Convert.ToInt32(area.AxisX.ScaleView.Position) +
                              Convert.ToInt32(area.AxisX.ScaleView.Size) + 1;
            }

            if (firstX < 0)
            {
                firstX = 0;
                lastX = firstX +
                              Convert.ToInt32(area.AxisX.ScaleView.Size) + 1;
            }

            if (firstX == lastX ||
                firstX > lastX ||
                firstX < 0 ||
                lastX <= 0)
            {
                return;
            }

            double max = 0;
            double min = double.MaxValue;

            for (int i = firstX; profitSeries.Points != null && i < profitSeries.Points.Count && i < lastX; i++)
            {
                if (profitSeries.Points[i].YValues.Max() > max)
                {
                    max = profitSeries.Points[i].YValues.Max();
                }
                if (profitSeries.Points[i].YValues.Min() < min && profitSeries.Points[i].YValues.Min() != 0)
                {
                    min = profitSeries.Points[i].YValues.Min();
                }
            }


            if (min == double.MaxValue ||
                max == 0 ||
                max == min ||
                max < min)
            {
                return;
            }

            area.AxisY2.Maximum = max;
            area.AxisY2.Minimum = min;
        }

// instrument table
//  таблица с инструментами

        /// <summary>
        /// instrument table
        /// таблица с инструментами
        /// </summary>
        private DataGridView _myGridView;

        /// <summary>
        /// create instrument table
        /// создать таблицу с инструментами
        /// </summary>
        private void CreateGrid()
        {
            _myGridView = DataGridFactory.GetDataGridDataSource();

            _myGridView.DoubleClick += _myGridView_DoubleClick;
            HostSecurities.Child = _myGridView;
            HostSecurities.Child.Show();
            _myGridView.Rows.Add();
            _myGridView.CellValueChanged += _myGridView_CellValueChanged;
        }

        /// <summary>
        /// paint instrument table
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

            ProgressBar.Maximum = (_server.TimeMax - DateTime.MinValue).TotalMinutes;
            ProgressBar.Minimum = (_server.TimeMin - DateTime.MinValue).TotalMinutes;
            ProgressBar.Value = (_server.TimeNow - DateTime.MinValue).TotalMinutes;

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
                        comboBox.Items.Add(TimeFrame.Min20.ToString());
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

            if (_server.TimeEnd != DateTime.MinValue &&
                SliderFrom.Minimum + SliderTo.Maximum - (_server.TimeEnd - DateTime.MinValue).TotalMinutes > 0)
            {
                SliderTo.Value =
                SliderFrom.Minimum + SliderTo.Maximum - (_server.TimeEnd - DateTime.MinValue).TotalMinutes;
            }

            SliderFrom.ValueChanged += SliderFrom_ValueChanged;
            SliderTo.ValueChanged += SliderTo_ValueChanged;
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
        /// double click on instrument table
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

            Security security = _server.GetSecurityForName(str);

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

// sliders. Setting the start and end time of testing
// слайдеры. Установка начального и конечного времени тестирования

        private void SliderTo_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            TextBoxTo.TextChanged -= TextBoxTo_TextChanged;

            DateTime to = DateTime.MinValue.AddMinutes(SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value);
            _server.TimeEnd= to;
            _server.SaveSecurityTestSettings();
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
            _server.SaveSecurityTestSettings();
            TextBoxFrom.Text = from.ToString(new CultureInfo("RU-ru"));

            if (SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value < SliderFrom.Value)
            {
                SliderTo.Value = SliderFrom.Minimum + SliderFrom.Maximum -SliderFrom.Value;
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

        private void CheckBoxSlipageLimitOff_Checked(object sender, RoutedEventArgs e)
        {
            TextBoxSlipageSimpleOrder.Text = "0";
            TextBoxSlipageSimpleOrder.IsEnabled = false;
            CheckBoxSlipageLimitOn.IsChecked = false;
        }

        private void CheckBoxSlipageLimitOn_Checked(object sender, RoutedEventArgs e)
        {
            TextBoxSlipageSimpleOrder.IsEnabled = true;
            CheckBoxSlipageLimitOff.IsChecked = false;
        }

        private void CheckBoxSlipageStopOff_Checked(object sender, RoutedEventArgs e)
        {
            TextBoxSlipageStop.Text = "0";
            TextBoxSlipageStop.IsEnabled = false;
            CheckBoxSlipageStopOn.IsChecked = false;
        }

        private void CheckBoxSlipageStopOn_Checked(object sender, RoutedEventArgs e)
        {
            TextBoxSlipageStop.IsEnabled = true;
            CheckBoxSlipageStopOff.IsChecked = false;
        }

        private void CheckBoxExecutionOrderIntersection_Checked(object sender, RoutedEventArgs e)
        {
            _server.OrderExecutionType = OrderExecutionType.Intersection;
            CheckBoxExecutionOrderTuch.IsChecked = false;
            CheckBoxExecutionOrderFiftyFifty.IsChecked = false;
        }

        private void CheckBoxExecutionOrderTuch_Checked(object sender, RoutedEventArgs e)
        {
            _server.OrderExecutionType = OrderExecutionType.Touch;
            CheckBoxExecutionOrderIntersection.IsChecked = false;
            CheckBoxExecutionOrderFiftyFifty.IsChecked = false;
        }

        private void CheckBoxExecutionOrderFiftyFifty_Checked(object sender, RoutedEventArgs e)
        {
            _server.OrderExecutionType = OrderExecutionType.FiftyFifty;
            CheckBoxExecutionOrderTuch.IsChecked = false;
            CheckBoxExecutionOrderIntersection.IsChecked = false;
        }

        private void CheckBoxOnOffMarketPortfolio_Checked(object sender, RoutedEventArgs e)
        {
            if (CheckBoxOnOffMarketPortfolio.IsChecked == true)
            {
                _server.ProfitMarketIsOn = true;
            }
            else
            {
                _server.ProfitMarketIsOn = false;
            }
        }
    }
}
