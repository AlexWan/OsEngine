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
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using OsEngine.Charts;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
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
        public TesterServerUi(TesterServer server, Log log)
        {
            InitializeComponent();
            _currentCulture = OsLocalization.CurCulture;
            OsEngine.Layout.StickyBorders.Listen(this);
            _server = server;
            _server.LoadSecurityEvent += _server_LoadSecurityEvent;
            _log = log;

            _server.LoadSecurityTestSettings();
            LabelStatus.Content = _server.ServerStatus;

            _server.ConnectStatusChangeEvent += _server_ConnectStatusChangeEvent;
            log.StartPaint(Host);

            TextBoxStartDepozit.Text = _server.StartPortfolio.ToString(new CultureInfo("ru-RU"));
            TextBoxStartDepozit.TextChanged += TextBoxStartDeposit_TextChanged;

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
            Width = 670;

            if (_server.ServerStatus == ServerConnectStatus.Disconnect)
            {
                ButtonStartTest.Content = OsLocalization.Market.Label134;
                ButtonStartTest.IsEnabled = false;
            }
            else
            {
                ButtonStartTest.Content = OsLocalization.Market.Button2;
                ButtonStartTest.IsEnabled = true;
            }

            _server.TestingStartEvent += _server_TestingStartEvent;
            _server.SecuritiesChangeEvent += _server_SecuritiesChangeEvent;
            _server.TestRegimeChangeEvent += _server_TestRegimeChangeEvent;
            _server.TestingFastEvent += _server_TestingFastEvent;

            CreateGrid();
            PaintGrid();


            TextBoxFrom.TextChanged += TextBoxFrom_TextChanged;
            TextBoxTo.TextChanged += TextBoxTo_TextChanged;

            TextBoxSlippageSimpleOrder.Text = _server.SlippageToSimpleOrder.ToString(new CultureInfo("ru-RU"));
            TextBoxSlippageSimpleOrder.TextChanged += TextBoxSlippageSimpleOrderTextChanged;

            TextBoxSlippageStop.Text = _server.SlippageToStopOrder.ToString(new CultureInfo("ru-RU"));
            TextBoxSlippageStop.TextChanged += TextBoxSlippageStop_TextChanged;

            if (_server.SlippageToStopOrder == 0)
            {
                CheckBoxSlippageStopOff.IsChecked = true;
            }
            else
            {
                CheckBoxSlippageStopOn.IsChecked = true;
            }

            if (_server.SlippageToSimpleOrder == 0)
            {
                CheckBoxSlippageLimitOff.IsChecked = true;
            }
            else
            {
                CheckBoxSlippageLimitOn.IsChecked = true;
            }

            ComboBoxOrderActivationType.Items.Add(OrderExecutionType.Touch.ToString());
            ComboBoxOrderActivationType.Items.Add(OrderExecutionType.Intersection.ToString());
            ComboBoxOrderActivationType.Items.Add(OrderExecutionType.FiftyFifty.ToString());
            ComboBoxOrderActivationType.SelectedItem = _server.OrderExecutionType.ToString();
            ComboBoxOrderActivationType.SelectionChanged += ComboBoxOrderActivationType_SelectionChanged;

            // progress bar/прогресс бар

            server.TestingNewSecurityEvent += server_TestingNewSecurityEvent;

            ProgressBar.Maximum = (_server.TimeMax - DateTime.MinValue).TotalMinutes;
            ProgressBar.Minimum = (_server.TimeMin - DateTime.MinValue).TotalMinutes;
            ProgressBar.Value = (_server.TimeNow - DateTime.MinValue).TotalMinutes;

            Closing += TesterServerUi_Closing;

            // chart/чарт

            CreateChart();

            PaintProfitOnChart();

            Resize();

            _chartActive = true;

            _server.NewCurrentValue += _server_NewCurrentValue;

            List<string> sets = _server.Sets;

            // clearing

            CreateClearingGrid();
            PaintClearingGrid();

            // non trade periods

            CreateNonTradePeriodsGrid();
            PaintNonTradePeriodsGrid();

            // sets

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

            CheckBoxRemoveTrades.IsChecked = _server.RemoveTradesFromMemory;
            CheckBoxRemoveTrades.Click += CheckBoxRemoveTrades_Click;

            // data for test

            ComboBoxDataType.Items.Add(TesterDataType.Candle);
            ComboBoxDataType.Items.Add(TesterDataType.TickAllCandleState);
            ComboBoxDataType.Items.Add(TesterDataType.TickOnlyReadyCandle);
            ComboBoxDataType.Items.Add(TesterDataType.MarketDepthAllCandleState);
            ComboBoxDataType.Items.Add(TesterDataType.MarketDepthOnlyReadyCandle);
            ComboBoxDataType.SelectedItem = _server.TypeTesterData;
            ComboBoxDataType.SelectionChanged += ComboBoxDataType_SelectionChanged;

            TextBoxDataPath.Text = _server.PathToFolder;
            ComboBoxDataSourceType.Items.Add(TesterSourceDataType.Folder);
            ComboBoxDataSourceType.Items.Add(TesterSourceDataType.Set);
            ComboBoxDataSourceType.SelectedItem = _server.SourceDataType;
            ComboBoxDataSourceType.SelectionChanged += ComboBoxDataSourceType_SelectionChanged;

            ButtonSynchronizer.Content = OsLocalization.Market.Button1;
            Title = OsLocalization.Market.TitleTester;
            Label21.Content = OsLocalization.Market.Label21;
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
            CheckBoxSlippageLimitOff.Content = OsLocalization.Market.Label35;
            CheckBoxSlippageStopOff.Content = OsLocalization.Market.Label35;
            CheckBoxSlippageLimitOn.Content = OsLocalization.Market.Label36;
            CheckBoxSlippageStopOn.Content = OsLocalization.Market.Label36;
            CheckBoxOnOffMarketPortfolio.Content = OsLocalization.Market.Label39;
            Label40.Content = OsLocalization.Market.Label40;
            LabelClearing.Content = OsLocalization.Market.Label150;
            LabelNonTradePeriod.Content = OsLocalization.Market.Label151;
            LabelOrderActivationType.Content = OsLocalization.Market.Label148;
            ButtonNextPos.Content = OsLocalization.Market.Label62;
            ButtonGoTo.Content = OsLocalization.Market.Label63;
            CheckBoxRemoveTrades.Content = OsLocalization.Market.Label130;

            Thread worker = new Thread(SecuritiesGridPainterWorkerPlace);
            worker.Start();

            Thread barUpdater = new Thread(UpdaterProgressBarThreadArea);
            barUpdater.CurrentCulture = new CultureInfo("ru-RU");
            barUpdater.IsBackground = true;
            barUpdater.Start();

            Thread profitChartUpdater = new Thread(ResizeWorker);
            profitChartUpdater.Start();

            this.Activate();
            this.Focus();

            if (server.GuiIsOpenFullSettings)
            {
                ButtonSynchronizer_Click(null, null);
            }

            GlobalGUILayout.Listen(this, "testerServerGui");
        }

        private CultureInfo _currentCulture;

        private void TesterServerUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _uiIsClosed = true;

            Closing -= TesterServerUi_Closing;

            TextBoxStartDepozit.TextChanged -= TextBoxStartDeposit_TextChanged;
            TextBoxFrom.TextChanged -= TextBoxFrom_TextChanged;
            TextBoxTo.TextChanged -= TextBoxTo_TextChanged;
            TextBoxSlippageSimpleOrder.TextChanged -= TextBoxSlippageSimpleOrderTextChanged;
            TextBoxSlippageStop.TextChanged -= TextBoxSlippageStop_TextChanged;
            ComboBoxSets.SelectionChanged -= ComboBoxSets_SelectionChanged;
            ComboBoxDataType.SelectionChanged -= ComboBoxDataType_SelectionChanged;
            ComboBoxDataSourceType.SelectionChanged -= ComboBoxDataSourceType_SelectionChanged;
            SliderFrom.ValueChanged -= SliderFrom_ValueChanged;
            SliderTo.ValueChanged -= SliderTo_ValueChanged;

            _server.ConnectStatusChangeEvent -= _server_ConnectStatusChangeEvent;
            _server.NewCurrentValue -= _server_NewCurrentValue;
            _server.TestingStartEvent -= _server_TestingStartEvent;
            _server.SecuritiesChangeEvent -= _server_SecuritiesChangeEvent;
            _server.TestRegimeChangeEvent -= _server_TestRegimeChangeEvent;
            _server.TestingFastEvent -= _server_TestingFastEvent;
            _server.LoadSecurityEvent -= _server_LoadSecurityEvent;

            _server = null;

            if (_securitiesGrid != null)
            {
                DataGridFactory.ClearLinks(_securitiesGrid);
                _securitiesGrid.DoubleClick -= _myGridView_DoubleClick;
                _securitiesGrid.CellValueChanged -= _myGridView_CellValueChanged;
                _securitiesGrid.DataError -= _gridClearing_DataError;
                HostSecurities.Child = null;
                _securitiesGrid = null;
            }

            if (_gridClearing != null)
            {
                DataGridFactory.ClearLinks(_gridClearing);
                _gridClearing.CellClick -= _gridClearing_CellClick;
                _gridClearing.CellValueChanged -= _gridClearing_CellValueChanged;
                _gridClearing.DataError -= _gridClearing_DataError;
                HostClearing.Child = null;
                _gridClearing = null;
            }

            if (_gridNonTradePeriods != null)
            {
                DataGridFactory.ClearLinks(_gridNonTradePeriods);
                _gridNonTradePeriods.CellValueChanged -= _gridNonTradePeriods_CellValueChanged;
                _gridNonTradePeriods.CellClick -= _gridNonTradePeriods_CellClick;
                _gridNonTradePeriods.DataError -= _gridClearing_DataError;
                HostNonTradePeriods.Child = null;
                _gridNonTradePeriods = null;
            }

            _log.StopPaint();
            _log = null;
        }

        private void CheckBoxRemoveTrades_Click(object sender, RoutedEventArgs e)
        {
            _server.RemoveTradesFromMemory = CheckBoxRemoveTrades.IsChecked.Value;
        }

        private void ComboBoxDataSourceType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            TesterSourceDataType sourceDataType;
            Enum.TryParse(ComboBoxDataSourceType.SelectedItem.ToString(), out sourceDataType);
            _server.SourceDataType = sourceDataType;
        }

        private void ComboBoxDataType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            TesterDataType type;
            Enum.TryParse(ComboBoxDataType.SelectedItem.ToString(), out type);
            _server.TypeTesterData = type;
            _server.Save();

            PaintGrid();
        }

        private void ComboBoxSets_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _server.SetNewSet(ComboBoxSets.SelectedItem.ToString());
            PaintGrid();
        }

        private void UpdaterProgressBarThreadArea()
        {
            while (true)
            {
                Thread.Sleep(100);

                if (_uiIsClosed)
                {
                    return;
                }

                ChangeProgressBar();
            }
        }

        private void ChangeProgressBar()
        {
            try
            {
                if (!ProgressBar.Dispatcher.CheckAccess())
                {
                    ProgressBar.Dispatcher.Invoke(ChangeProgressBar);
                    return;
                }


                if (_server == null)
                {
                    return;
                }
                ProgressBar.Value = (_server.TimeNow - DateTime.MinValue).TotalMinutes;
            }
            catch (Exception error)
            {
                try
                {
                    _server.SendLogMessage(error.ToString(), LogMessageType.Error);
                }
                catch
                {
                    // ignore
                }
            }
        }

        private void TextBoxSlippageSimpleOrderTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                _server.SlippageToSimpleOrder = Convert.ToInt32(TextBoxSlippageSimpleOrder.Text);
                _server.Save();
            }
            catch (Exception)
            {
                TextBoxSlippageSimpleOrder.Text = _server.SlippageToSimpleOrder.ToString(new CultureInfo("ru-RU"));
                // ignore
            }

        }

        private void TextBoxStartDeposit_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
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

        private void TextBoxSlippageStop_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                _server.SlippageToStopOrder = Convert.ToInt32(TextBoxSlippageStop.Text);
                _server.Save();
            }
            catch (Exception)
            {
                TextBoxSlippageStop.Text = _server.SlippageToStopOrder.ToString(new CultureInfo("ru-RU"));
                // ignore
            }
        }

        #region Server

        private TesterServer _server;

        private Log _log;

        private void _server_ConnectStatusChangeEvent(string status)
        {
            if (!LabelStatus.Dispatcher.CheckAccess())
            {
                LabelStatus.Dispatcher.Invoke(new Action<string>(_server_ConnectStatusChangeEvent), status);
                return;
            }
            LabelStatus.Content = status;

            if (_server.ServerStatus == ServerConnectStatus.Disconnect)
            {
                ButtonStartTest.Content = OsLocalization.Market.Label134;
                ButtonStartTest.IsEnabled = false;
            }
            else
            {
                ButtonStartTest.Content = OsLocalization.Market.Button2;
                ButtonStartTest.IsEnabled = true;
            }
        }

        private void _server_SecuritiesChangeEvent(List<Security> securities)
        {
            _needToRePaintGrid = true;
        }

        private void _server_TestingStartEvent()
        {
            _chartActive = true;
            CreateChart();
            PaintPausePlayButtonByActualServerState();
        }

        private void server_TestingNewSecurityEvent()
        {
            _needToRePaintGrid = true;
        }

        private void _server_TestRegimeChangeEvent(TesterRegime regime)
        {
            PaintPausePlayButtonByActualServerState();
        }

        private void _server_TestingFastEvent()
        {
            PaintPausePlayButtonByActualServerState();
        }

        #endregion

        #region Block button start on connect securities

        private void _server_LoadSecurityEvent()
        {
            _lastTimeConnectSecurity = DateTime.Now;

            if (_buttonStartLockerThread == null)
            {
                _buttonStartLockerThread = new Thread(ButtonStartThreadWorkArea);
                _buttonStartLockerThread.Start();
            }
        }

        private Thread _buttonStartLockerThread;

        private DateTime _lastTimeConnectSecurity;

        private void ButtonStartThreadWorkArea()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(1000);

                    if (_uiIsClosed)
                    {
                        return;
                    }

                    if (_lastTimeConnectSecurity.AddSeconds(5) > DateTime.Now)
                    {
                        BlockButtonStartTests();
                    }
                    else
                    {
                        UnblockButtonStartTests();
                        _buttonStartLockerThread = null;
                        return;
                    }
                }
                catch (Exception e)
                {
                    _server.SendLogMessage(e.ToString(), LogMessageType.Error);
                }
            }
        }

        private void BlockButtonStartTests()
        {
            try
            {
                if (ButtonStartTest.Dispatcher.CheckAccess() == false)
                {
                    ButtonStartTest.Dispatcher.Invoke(BlockButtonStartTests);
                    return;
                }

                if (ButtonStartTest.Content.ToString() == OsLocalization.Market.Button2)
                {
                    ButtonStartTest.Content = OsLocalization.Market.Label132 + ".";
                    ButtonStartTest.IsEnabled = false;
                    return;
                }

                int pointsCount = ButtonStartTest.Content.ToString().Split('.').Length;

                if (pointsCount > 5)
                {
                    ButtonStartTest.Content = OsLocalization.Market.Label132 + ".";
                }
                else
                {
                    ButtonStartTest.Content = ButtonStartTest.Content + ".";
                }
            }
            catch (Exception e)
            {
                _server.SendLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        private void UnblockButtonStartTests()
        {
            try
            {
                if (ButtonStartTest.Dispatcher.CheckAccess() == false)
                {
                    ButtonStartTest.Dispatcher.Invoke(UnblockButtonStartTests);
                    return;
                }

                ButtonStartTest.Content = OsLocalization.Market.Button2;
                ButtonStartTest.IsEnabled = true;
            }
            catch (Exception e)
            {
                _server.SendLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Button handlers

        private void buttonFast_Click(object sender, RoutedEventArgs e)
        {
            _server.TestingFastOnOff();
        }

        private void buttonPausePlay_Click(object sender, RoutedEventArgs e)
        {
            _server.TestingPausePlay();
        }

        private void PaintPausePlayButtonByActualServerState()
        {
            if (ButtonPausePlay.Dispatcher.CheckAccess() == false)
            {
                ButtonPausePlay.Dispatcher.Invoke(PaintPausePlayButtonByActualServerState);
                return;
            }

            TesterRegime regime = _server.TesterRegime;

            if (regime == TesterRegime.Play ||
                regime == TesterRegime.PlusOne)
            {
                ButtonPausePlay.Content = "| |";
            }
            else if (regime == TesterRegime.Pause)
            {
                ButtonPausePlay.Content = ">";
            }
        }

        private void buttonNextCandle_Click(object sender, RoutedEventArgs e)
        {
            _server.TestingPlusOne();
        }

        private void ButtonNextPos_Click(object sender, RoutedEventArgs e)
        {
            _server.ToNextPositionActionTestingFast();
        }

        private GoToUi _goToUi;

        private void ButtonGoTo_Click(object sender, RoutedEventArgs e)
        {

            if (_goToUi == null)
            {
                _goToUi = new GoToUi(_server.TimeStart, _server.TimeEnd, _server.TimeNow);
                _goToUi.Show();
                _goToUi.SetLocation(this.Left + this.Width, this.Top);

                _goToUi.Closing += (a, b) =>
                {
                    if (_goToUi.IsChange)
                    {
                        _server.ToDateTimeTestingFast(_goToUi.TimeGoTo);
                    }

                    _goToUi = null;
                };
            }
            else
            {
                _goToUi.Activate();
            }
        }

        private void buttonStartTest_Click(object sender, RoutedEventArgs e)
        {
            if (_server.TimeStart == DateTime.MinValue)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Market.Label133);
                ui.ShowDialog();

                return;
            }

            Thread worker = new Thread(_server.TestingStart);
            worker.CurrentCulture = new CultureInfo("ru-RU");
            worker.IsBackground = true;
            worker.Start();
            ButtonPausePlay.Content = "| |";
        }

        private void ButtonSynchronizer_Click(object sender, RoutedEventArgs e)
        {
            if (HostSecurities.Visibility == Visibility.Hidden)
            {
                // if need to expand the form/ если нужно раскрывать форму
                Height = 600;
                Width = 815;
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
                _server.GuiIsOpenFullSettings = true;
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
                Width = 670;
                _server.GuiIsOpenFullSettings = false;
            }
        }

        #endregion

        #region Chart

        private void ResizeWorker()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(3000);

                    if (_uiIsClosed)
                    {
                        return;
                    }

                    if (_needToUpdateChartValue == false)
                    {
                        continue;
                    }

                    _needToUpdateChartValue = false;

                    PaintProfitOnChart();
                    Resize();

                }
                catch (Exception ex)
                {
                    _server?.SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        private Chart _chartReport;

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

        private void PaintProfitOnChart()
        {
            try
            {
                if (_chartReport == null)
                {
                    return;
                }

                List<decimal> portfolio = _server.ProfitArray;

                if (portfolio == null || portfolio.Count == 0)
                {
                    return;
                }

                if (_chartReport.InvokeRequired)
                {
                    _chartReport.Invoke(new Action(PaintProfitOnChart));
                    return;
                }

                _chartReport.Series[0].Points.ClearFast();
                _chartReport.Series[1].Points.ClearFast();

                for (int i = 0; i < portfolio.Count; i++)
                {
                    if (portfolio[i] != 0)
                    {
                        _chartReport.Series[0].Points.AddXY(i, portfolio[i]);

                        if (i == 0)
                        {
                            _chartReport.Series[1].Points.AddXY(i, portfolio[i] - _server.StartPortfolio);
                            continue;
                        }

                        _chartReport.Series[1].Points.AddXY(i, portfolio[i] - portfolio[i - 1]);

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
            catch (Exception ex)
            {
                _server.SendLogMessage(ex.ToString(), LogMessageType.Error);
                return;
            }
        }

        private void PaintLastPointOnChart()
        {
            if (_chartReport.InvokeRequired)
            {
                _chartReport.Invoke(new Action(PaintLastPointOnChart));
                return;
            }

            if (_server == null)
            {
                return;
            }

            List<decimal> portfolio = _server.ProfitArray;

            if (portfolio.Count == 0)
            {
                return;
            }

            if (portfolio.Count != 0)
            {
                _chartReport.Series[0].Points.AddXY(_chartReport.Series[0].Points.Count, portfolio[portfolio.Count - 1]);

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

        private bool _chartActive;

        private bool _needToUpdateChartValue;

        private void _server_NewCurrentValue(decimal val)
        {
            if (_chartActive == false)
            {
                return;
            }

            _needToUpdateChartValue = true;
        }

        private void Resize()
        {
            try
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
            catch (Exception ex)
            {
                _server?.SendLogMessage(ex.ToString(), LogMessageType.Error);
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
            _gridClearing.DataError += _gridClearing_DataError;
        }

        private void _gridClearing_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            _server.SendLogMessage(e.ToString(), LogMessageType.Error);
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

                for (int i = 0; i < _server.ClearingTimes.Count; i++)
                {
                    _gridClearing.Rows.Add(GetClearingRow(_server.ClearingTimes[i], i + 1));
                }

                _gridClearing.Rows.Add(GetClearingLastRow());

                _gridClearing.CellValueChanged += _gridClearing_CellValueChanged;
            }
            catch (Exception error)
            {
                try
                {
                    _server.SendLogMessage(error.ToString(), LogMessageType.Error);
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

                if (row > _server.ClearingTimes.Count)
                {
                    return;
                }

                if (column == 3)
                {
                    if (row == _server.ClearingTimes.Count)
                    {// Создание нового клиринга
                        _server.CreateNewClearing();
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

                        _server.RemoveClearing(row);
                        PaintClearingGrid();
                    }
                }
            }
            catch (Exception ex)
            {
                _server.SendLogMessage(ex.ToString(), LogMessageType.Error);
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

                    _server.ClearingTimes[row].Time = new DateTime(2022, 1, 1, hour, minute, 0);
                    _server.SaveClearingInfo();
                }
                else if (column == 2)
                { // Изменилось состояние вкл/выкл
                    string value = _gridClearing.Rows[row].Cells[column].Value.ToString();

                    if (value == "True")
                    {
                        _server.ClearingTimes[row].IsOn = true;
                        _server.SaveClearingInfo();
                    }
                    else if (value == "False")
                    {
                        _server.ClearingTimes[row].IsOn = false;
                        _server.SaveClearingInfo();
                    }
                }
            }
            catch (Exception ex)
            {
                _server.SendLogMessage(ex.ToString(), LogMessageType.Error);
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
            _gridNonTradePeriods.DataError += _gridClearing_DataError;
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

                for (int i = 0; i < _server.NonTradePeriods.Count; i++)
                {
                    _gridNonTradePeriods.Rows.Add(GetNonTradePeriodsRow(_server.NonTradePeriods[i], i + 1));
                }

                _gridNonTradePeriods.Rows.Add(GetNonTradePeriodsLastRow());

                _gridNonTradePeriods.CellValueChanged += _gridNonTradePeriods_CellValueChanged;
            }
            catch (Exception error)
            {
                try
                {
                    _server.SendLogMessage(error.ToString(), LogMessageType.Error);
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

                if (row > _server.NonTradePeriods.Count)
                {
                    return;
                }

                if (column == 4)
                {
                    if (row == _server.NonTradePeriods.Count)
                    {// Создание нового периода
                        _server.CreateNewNonTradePeriod();
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

                        _server.RemoveNonTradePeriod(row);
                        PaintNonTradePeriodsGrid();
                    }
                }
            }
            catch (Exception ex)
            {
                _server.SendLogMessage(ex.ToString(), LogMessageType.Error);
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

                    _server.NonTradePeriods[row].DateStart = time;
                    _server.SaveNonTradePeriods();
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

                    _server.NonTradePeriods[row].DateEnd = time;
                    _server.SaveNonTradePeriods();


                }
                else if (column == 3)
                { // Изменилось состояние вкл/выкл
                    string value = _gridNonTradePeriods.Rows[row].Cells[column].Value.ToString();

                    if (value == "True")
                    {
                        _server.NonTradePeriods[row].IsOn = true;
                        _server.SaveNonTradePeriods();
                    }
                    else if (value == "False")
                    {
                        _server.NonTradePeriods[row].IsOn = false;
                        _server.SaveNonTradePeriods();
                    }
                }
            }
            catch (Exception ex)
            {
                _server.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Securities table

        private bool _needToRePaintGrid;

        private bool _uiIsClosed;

        private void SecuritiesGridPainterWorkerPlace()
        {
            while (true)
            {
                Thread.Sleep(5000);

                if (_uiIsClosed)
                {
                    return;
                }

                if (_needToRePaintGrid)
                {
                    _needToRePaintGrid = false;

                    try
                    {
                        PaintGrid();
                    }
                    catch (Exception error)
                    {
                        _server.SendLogMessage(error.ToString(), LogMessageType.Error);
                    }
                }
            }
        }

        private DataGridView _securitiesGrid;

        private void CreateGrid()
        {
            _securitiesGrid = DataGridFactory.GetDataGridDataSource();
            _securitiesGrid.DoubleClick += _myGridView_DoubleClick;
            HostSecurities.Child = _securitiesGrid;
            HostSecurities.Child.Show();
            _securitiesGrid.Rows.Add();
            _securitiesGrid.CellValueChanged += _myGridView_CellValueChanged;
            _securitiesGrid.DataError += _gridClearing_DataError;
        }

        private void PaintGrid()
        {
            try
            {
                if (_securitiesGrid.InvokeRequired)
                {
                    _securitiesGrid.Invoke(new Action(PaintGrid));
                    return;
                }

                SliderFrom.ValueChanged -= SliderFrom_ValueChanged;
                SliderTo.ValueChanged -= SliderTo_ValueChanged;

                ProgressBar.Maximum = (_server.TimeMax - DateTime.MinValue).TotalMinutes;
                ProgressBar.Minimum = (_server.TimeMin - DateTime.MinValue).TotalMinutes;
                ProgressBar.Value = (_server.TimeNow - DateTime.MinValue).TotalMinutes;

                int displayedRow = _securitiesGrid.FirstDisplayedScrollingRowIndex;

                _securitiesGrid.Rows.Clear();

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
                        nRow.Cells[3].Value = securities[i].Security.PriceStep.ToStringWithNoEndZero();
                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells[4].Value = securities[i].TimeStart.ToString(_currentCulture);
                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells[5].Value = securities[i].TimeEnd.ToString(_currentCulture);

                        _securitiesGrid.Rows.Add(nRow);
                    }
                }

                TextBoxFrom.Text = _server.TimeStart.ToString(_currentCulture);
                TextBoxTo.Text = _server.TimeEnd.ToString(_currentCulture);

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

                if (displayedRow > 0
                    && displayedRow < _securitiesGrid.Rows.Count)
                {
                    _securitiesGrid.FirstDisplayedScrollingRowIndex = displayedRow;
                }
            }
            catch (Exception error)
            {
                try
                {
                    _server.SendLogMessage(error.ToString(), LogMessageType.Error);
                }
                catch
                {
                    // ignore
                }
            }
        }

        private void _myGridView_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            List<SecurityTester> securities = _server.SecuritiesTester;

            for (int i = 0; i < securities.Count && i < _securitiesGrid.Rows.Count; i++)
            {
                TimeFrame frame;

                if (Enum.TryParse(_securitiesGrid.Rows[i].Cells[2].Value.ToString(), out frame))
                {
                    securities[i].TimeFrame = frame;
                }
            }
            _server.SaveSetSecuritiesTimeFrameSettings();
        }

        private void _myGridView_DoubleClick(object sender, EventArgs e)
        {
            DataGridViewRow row = null;
            try
            {
                row = _securitiesGrid.SelectedRows[0];
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

            Security security = _server.GetSecurityForName(str, "");

            if (security == null)
            {
                return;
            }

            SecurityUi ui = new SecurityUi(security);
            ui.ShowDialog();

            if (ui.IsChanged)
            {
                _server.SaveSecurityDopSettings(security);
                PaintGrid();
            }
        }

        #endregion

        #region Sliders. Setting the start and end time of testing. Slippage

        private void SliderTo_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            TextBoxTo.TextChanged -= TextBoxTo_TextChanged;

            DateTime to = DateTime.MinValue.AddMinutes(SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value);
            _server.TimeEnd = to;
            _server.SaveSecurityTestSettings();
            TextBoxTo.Text = to.ToString(_currentCulture);

            if (SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value < SliderFrom.Value)
            {
                SliderFrom.Value = SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value;
            }
            TextBoxTo.TextChanged += TextBoxTo_TextChanged;
        }

        private void SliderFrom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            TextBoxFrom.TextChanged -= TextBoxFrom_TextChanged;

            DateTime from = DateTime.MinValue.AddMinutes(SliderFrom.Value);
            _server.TimeStart = from;
            _server.SaveSecurityTestSettings();
            TextBoxFrom.Text = from.ToString(_currentCulture);

            if (SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value < SliderFrom.Value)
            {
                SliderTo.Value = SliderFrom.Minimum + SliderFrom.Maximum - SliderFrom.Value;
            }

            TextBoxFrom.TextChanged += TextBoxFrom_TextChanged;
        }

        private void TextBoxTo_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            DateTime to;
            try
            {
                to = Convert.ToDateTime(TextBoxTo.Text, _currentCulture);

                if (to < _server.TimeMin ||
                    to > _server.TimeMax)
                {
                    throw new Exception();
                }
            }
            catch (Exception)
            {
                TextBoxTo.Text = _server.TimeEnd.ToString(_currentCulture);
                return;
            }

            _server.TimeEnd = to;
            // SliderTo.Value = SliderFrom.Minimum + SliderFrom.Maximum - to.Minute;
            // SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value
            SliderTo.Value = SliderFrom.Minimum + SliderTo.Maximum - (to - DateTime.MinValue).TotalMinutes;
        }

        private void TextBoxFrom_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            DateTime from;
            try
            {
                from = Convert.ToDateTime(TextBoxFrom.Text, _currentCulture);

                if (from < _server.TimeMin ||
                    from > _server.TimeMax)
                {
                    throw new Exception();
                }
            }
            catch (Exception)
            {
                TextBoxFrom.Text = _server.TimeStart.ToString(_currentCulture);
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
                    _server.OrderExecutionType = type;
                }
            }
            catch (Exception ex)
            {
                _server.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
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

        #endregion
    }
}