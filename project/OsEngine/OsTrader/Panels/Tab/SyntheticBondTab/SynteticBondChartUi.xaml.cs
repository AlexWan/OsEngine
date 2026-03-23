using OsEngine.Charts;
using OsEngine.Charts.CandleChart;
using OsEngine.Entity;
using OsEngine.Entity.SynteticBondEntity;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels.Tab.SyntheticBondTab;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.Windows.Forms.DataVisualization.Charting;

namespace OsEngine.OsTrader.Panels.Tab.SynteticBondTab
{
    /// <summary>
    /// Логика взаимодействия для SynteticBondChartUi.xaml
    /// </summary>
    public partial class SynteticBondChartUi : Window
    {
        #region Constructor

        private SynteticBondSeries _synteticBond;

        private SyntheticBond _settingsFuturesSyntheticBond;

        private ChartCandleMaster _chartSec1;

        private ChartCandleMaster _chartSec2;

        public SynteticBondChartUi(SynteticBondSeries synteticBond, ref SyntheticBond modificationFuturesSyntheticBond)
        {
            InitializeComponent();

            _synteticBond = synteticBond;
            _settingsFuturesSyntheticBond = modificationFuturesSyntheticBond;

            Title = OsLocalization.Trader.Label699;

            LastSec1Label.Content = OsLocalization.Trader.Label717 + ":";
            LastSec2Label.Content = OsLocalization.Trader.Label717 + ":";
            LastContangoLabel.Content = OsLocalization.Trader.Label717 + ":";
            LastCointegrationLabel.Content = OsLocalization.Trader.Label717 + ":";

            if (_settingsFuturesSyntheticBond.BaseIsbergParameters != null
                && _settingsFuturesSyntheticBond.BaseIsbergParameters.BotTab != null
                && _settingsFuturesSyntheticBond.BaseIsbergParameters.BotTab.Connector != null
                && _settingsFuturesSyntheticBond.BaseIsbergParameters.BotTab.Connector.SecurityName != null)
            {
                Security1Label.Content = _settingsFuturesSyntheticBond.BaseIsbergParameters.BotTab.Connector.SecurityName;
            }
            else
            {
                Security1Label.Content = "None";
            }

            if (_settingsFuturesSyntheticBond != null && _settingsFuturesSyntheticBond.FuturesIsbergParameters != null && _settingsFuturesSyntheticBond.FuturesIsbergParameters.BotTab != null &&
                _settingsFuturesSyntheticBond.FuturesIsbergParameters.BotTab.Connector != null && _settingsFuturesSyntheticBond.FuturesIsbergParameters.BotTab.Connector.SecurityName != null)
            {
                Security2Label.Content = _settingsFuturesSyntheticBond.FuturesIsbergParameters.BotTab.Connector.SecurityName;
            }
            else
            {
                Security2Label.Content = "None";
            }

            CreateContangoChart();
            CreateCointegrationChart();

            PaintCandles();

            BotTabSimple baseTab = _settingsFuturesSyntheticBond.BaseIsbergParameters.BotTab;
            BotTabSimple futuresTab = _settingsFuturesSyntheticBond.FuturesIsbergParameters.BotTab;

            baseTab.CandleUpdateEvent += BaseTab_CandleUpdateEvent;
            futuresTab.CandleUpdateEvent += FuturesTab_CandleUpdateEvent;

            if (baseTab.StartProgram == StartProgram.IsTester)
            {
                baseTab.CandleFinishedEvent += BaseTab_CandleUpdateEvent;
                futuresTab.CandleFinishedEvent += FuturesTab_CandleUpdateEvent;
            }


            _synteticBond.ContangoChangeEvent += SynteticBond_ContangoChangeEvent;
            _synteticBond.CointegrationChangeEvent += SynteticBond_CointegrationChangeEvent;

            // Подписка на события позиций

            baseTab.PositionOpeningSuccesEvent += Tab_PositionChangeEvent;
            baseTab.PositionOpeningFailEvent += Tab_PositionChangeEvent;
            baseTab.PositionClosingSuccesEvent += Tab_PositionChangeEvent;
            baseTab.PositionClosingFailEvent += Tab_PositionChangeEvent;

            futuresTab.PositionOpeningSuccesEvent += Tab_PositionChangeEvent;
            futuresTab.PositionOpeningFailEvent += Tab_PositionChangeEvent;
            futuresTab.PositionClosingSuccesEvent += Tab_PositionChangeEvent;
            futuresTab.PositionClosingFailEvent += Tab_PositionChangeEvent;

            // Управление прорисовкой для тестера

            if (baseTab.StartProgram == StartProgram.IsTester)
            {
                TesterServer server = (TesterServer)ServerMaster.GetServers()[0];
                server.TestingEndEvent += Server_TestingEndEvent;
                server.TestingFastEvent += Server_TestingFastEvent;
                server.TestingStartEvent += Server_TestingStartEvent;
                server.TestRegimeChangeEvent += Server_TestRegimeChangeEvent;
            }

            UpdatePositionsOnChart();

            Closed += SyntheticBondOffsetUi_Closed;
        }

        private void PaintCandles()
        {
            BotTabSimple baseTabForChart = _settingsFuturesSyntheticBond.BaseIsbergParameters.BotTab;

            _chartSec1 = new ChartCandleMaster(baseTabForChart.TabName + "sec1", baseTabForChart.StartProgram);
            _chartSec1.StartPaint(null, HostSec1, null);
            _chartSec1.SetCandles(baseTabForChart.CandlesAll);

            for (int i = 0; i < baseTabForChart.Indicators.Count; i++)
            {
                if (_chartSec1.IndicatorIsCreate(baseTabForChart.Indicators[i].Name) == false)
                {
                    _chartSec1.CreateIndicator(baseTabForChart.Indicators[i], baseTabForChart.Indicators[i].NameArea);
                }
            }

            _chartSec2 = new ChartCandleMaster(_settingsFuturesSyntheticBond.FuturesIsbergParameters.BotTab.TabName + "sec2", _settingsFuturesSyntheticBond.FuturesIsbergParameters.BotTab.StartProgram);
            _chartSec2.StartPaint(null, HostSec2, null);
            _chartSec2.SetCandles(_settingsFuturesSyntheticBond.FuturesIsbergParameters.BotTab.CandlesAll);

            for (int i = 0; i < _settingsFuturesSyntheticBond.FuturesIsbergParameters.BotTab.Indicators.Count; i++)
            {
                if (_chartSec2.IndicatorIsCreate(_settingsFuturesSyntheticBond.FuturesIsbergParameters.BotTab.Indicators[i].Name) == false)
                {
                    _chartSec2.CreateIndicator(_settingsFuturesSyntheticBond.FuturesIsbergParameters.BotTab.Indicators[i], _settingsFuturesSyntheticBond.FuturesIsbergParameters.BotTab.Indicators[i].NameArea);
                }
            }
        }

        public string Key
        {
            get
            {
                return _settingsFuturesSyntheticBond.FuturesIsbergParameters.BotTab.TabName;
            }
        }

        #endregion

        #region Candle event handlers

        private void BaseTab_CandleUpdateEvent(List<Candle> candles)
        {
            if (_chartSec1 != null)
            {
                _chartSec1.SetCandles(candles);
            }

            if (_chartSec1 != null && _chartSec1.ChartCandle != null)
            {
                UpdateLastPriceLabel(LastSec1TextBox, candles);
            }
        }

        private void FuturesTab_CandleUpdateEvent(List<Candle> candles)
        {
            if (_chartSec2 != null)
            {
                _chartSec2.SetCandles(candles);
            }

            if (_chartSec2 != null && _chartSec2.ChartCandle != null)
            {
                UpdateLastPriceLabel(LastSec2TextBox, candles);
            }
        }

        private void UpdateLastPriceLabel(System.Windows.Controls.TextBox textBox, List<Candle> candles)
        {
            if (textBox.Dispatcher.CheckAccess() == false)
            {
                textBox.Dispatcher.Invoke(new Action<System.Windows.Controls.TextBox, List<Candle>>(UpdateLastPriceLabel), textBox, candles);
                return;
            }

            if (candles == null || candles.Count == 0)
            {
                return;
            }

            if ((_chartSec1 != null && _chartSec1.ChartCandle == null) ||
                _chartSec2 != null && _chartSec2.ChartCandle == null)
            {
                return;
            }

            textBox.Text = candles[candles.Count - 1].Close.ToString();
        }

        #endregion

        #region Contango and cointegration event handlers

        private void SynteticBond_ContangoChangeEvent(SyntheticBond settings)
        {
            if (settings != _settingsFuturesSyntheticBond)
            {
                return;
            }

            if ((_chartSec1 != null && _chartSec1.ChartCandle == null) ||
                _chartSec2 != null && _chartSec2.ChartCandle == null)
            {
                return;
            }

            UpdateContangoChart();
        }

        private void SynteticBond_CointegrationChangeEvent(SyntheticBond settings)
        {
            if (settings != _settingsFuturesSyntheticBond)
            {
                return;
            }

            if ((_chartSec1 != null && _chartSec1.ChartCandle == null) ||
                _chartSec2 != null && _chartSec2.ChartCandle == null)
            {
                return;
            }

            UpdateCointegrationChart();
        }

        #endregion

        #region Position drawing

        private void Tab_PositionChangeEvent(Position position)
        {
            UpdatePositionsOnChart();
        }

        private void UpdatePositionsOnChart()
        {
            if (_chartSec1 == null || _chartSec2 == null)
            {
                return;
            }

            BotTabSimple baseTabPos = _settingsFuturesSyntheticBond.BaseIsbergParameters.BotTab;
            BotTabSimple futuresTabPos = _settingsFuturesSyntheticBond.FuturesIsbergParameters.BotTab;

            if (baseTabPos != null && baseTabPos.PositionsAll != null)
            {
                _chartSec1.SetPosition(baseTabPos.PositionsAll);
            }

            if (futuresTabPos != null && futuresTabPos.PositionsAll != null)
            {
                _chartSec2.SetPosition(futuresTabPos.PositionsAll);
            }
        }

        #endregion

        #region Tester events

        private void Server_TestRegimeChangeEvent(TesterRegime currentTestRegime)
        {
            if (currentTestRegime == TesterRegime.Play)
            {
                _chartSec1.BindOff();
            }
            else if (currentTestRegime == TesterRegime.Pause)
            {
                _chartSec1.BindOn();
            }
            else if (currentTestRegime == TesterRegime.PlusOne)
            {
                _chartSec1.BindOn();
            }
        }

        private void Server_TestingStartEvent()
        {
            if (_chartContango == null)
            {
                return;
            }

            if (_chartContango.InvokeRequired)
            {
                _chartContango.Invoke(new Action(Server_TestingStartEvent));
                return;
            }

            _chartSec1.StartPaint(null, HostSec1, null);
            _chartSec2.StartPaint(null, HostSec2, null);
            HostContango.Child = _chartContango;
            HostCointegration.Child = _chartCointegration;
        }

        private void Server_TestingFastEvent()
        {
            if (_chartContango == null)
            {
                return;
            }

            if (_chartContango.InvokeRequired)
            {
                _chartContango.Invoke(new Action(Server_TestingFastEvent));
                return;
            }

            if (HostCointegration.Child == null)
            { // нужно показывать
                _chartSec1.StartPaint(null, HostSec1, null);
                _chartSec2.StartPaint(null, HostSec2, null);
                HostContango.Child = _chartContango;
                HostCointegration.Child = _chartCointegration;
            }
            else
            { // нужно прятать
                _chartSec1.StopPaint();
                _chartSec2.StopPaint();
                HostContango.Child = null;
                HostCointegration.Child = null;
            }
        }

        private void Server_TestingEndEvent()
        {
            if (_chartContango == null)
            {
                return;
            }

            if (_chartContango.InvokeRequired)
            {
                _chartContango.Invoke(new Action(Server_TestingEndEvent));
                return;
            }

            _chartSec1.StartPaint(null, HostSec1, null);
            _chartSec1.BindOn();
            _chartSec2.StartPaint(null, HostSec2, null);
            HostContango.Child = _chartContango;
            HostCointegration.Child = _chartCointegration;
        }

        #endregion

        #region Cointegration chart

        private void CreateCointegrationChart()
        {
            _chartCointegration = new Chart();

            ChartArea area = new ChartArea("Prime");

            _chartCointegration.ChartAreas.Clear();
            _chartCointegration.ChartAreas.Add(area);
            _chartCointegration.BackColor = Color.FromArgb(21, 26, 30);
            _chartCointegration.ChartAreas[0].AxisX.TitleForeColor = Color.FromArgb(149, 159, 176);

            for (int i = 0; _chartCointegration.ChartAreas != null && i < _chartCointegration.ChartAreas.Count; i++)
            {
                _chartCointegration.ChartAreas[i].BackColor = Color.FromArgb(21, 26, 30);
                _chartCointegration.ChartAreas[i].BorderColor = Color.FromArgb(17, 18, 23);
                _chartCointegration.ChartAreas[i].CursorY.LineColor = Color.FromArgb(149, 159, 176);
                _chartCointegration.ChartAreas[i].CursorX.LineColor = Color.FromArgb(149, 159, 176);

                foreach (Axis axe in _chartCointegration.ChartAreas[i].Axes)
                {
                    axe.LabelStyle.ForeColor = Color.FromArgb(149, 159, 176);
                }
            }

            // Столбцы коинтеграции
            Series series = new Series();
            series.ChartType = SeriesChartType.Column;
            _chartCointegration.Series.Clear();
            _chartCointegration.Series.Add(series);

            // Линия вернхняя

            Series seriesUpLine = new Series();
            seriesUpLine.ChartType = SeriesChartType.Line;
            seriesUpLine.Color = Color.AliceBlue;
            _chartCointegration.Series.Add(seriesUpLine);

            // Линия нижняя

            Series seriesDownLine = new Series();
            seriesDownLine.ChartType = SeriesChartType.Line;
            seriesDownLine.Color = Color.AliceBlue;
            _chartCointegration.Series.Add(seriesDownLine);

            HostCointegration.Child = _chartCointegration;
        }

        private void UpdateContangoChart()
        {
            if (_chartContango == null)
            {
                return;
            }

            if (_chartContango.InvokeRequired)
            {
                _chartContango.Invoke(new Action(UpdateContangoChart));
                return;
            }

            List<PairIndicatorValue> values = _settingsFuturesSyntheticBond.AbsoluteSeparationCandles;

            if (values == null || values.Count == 0)
            {
                return;
            }

            LastContangoTextBox.Text = values[values.Count - 1].Value.ToString();

            try
            {
                Series series = _chartContango.Series[0];
                series.Points.ClearFast();

                series.ChartType = SeriesChartType.Line;

                ChartArea chartArea = _chartContango.ChartAreas[0];
                chartArea.AxisX.LabelStyle.Format = "";
                chartArea.AxisX.Interval = 1;
                chartArea.AxisX.MajorGrid.Enabled = true;

                for (int i = 0; i < values.Count; i++)
                {
                    decimal val = values[i].Value;
                    DateTime time = values[i].Time;

                    int pointIndex = series.Points.AddXY(i + 1, val);
                    DataPoint point = series.Points[pointIndex];

                    if (val > 0)
                    {
                        point.Color = Color.DarkGreen;
                        point.BorderColor = Color.DarkGreen;
                        point.BackSecondaryColor = Color.DarkGreen;
                    }
                    else
                    {
                        point.Color = Color.DarkRed;
                        point.BorderColor = Color.DarkRed;
                        point.BackSecondaryColor = Color.DarkRed;
                    }

                    string toolTip = "Time: " + time + "\nValue: " + val.ToStringWithNoEndZero() + "\nIndex: " + (i + 1);
                    point.ToolTip = toolTip;

                    if (i + 1 == values.Count)
                    {
                        point.Label = val.ToStringWithNoEndZero();
                        point.LabelForeColor = Color.AntiqueWhite;

                        point.MarkerStyle = MarkerStyle.Circle;
                        point.MarkerSize = 6;

                        if (val > 0)
                        {
                            point.MarkerColor = Color.DarkGreen;
                        }
                        else
                        {
                            point.MarkerColor = Color.DarkRed;
                        }
                    }
                }

                if (values.Count > 0)
                {
                    decimal minValue = values[0].Value;
                    decimal maxValue = values[0].Value;

                    for (int i = 1; i < values.Count; i++)
                    {
                        if (values[i].Value < minValue)
                        {
                            minValue = values[i].Value;
                        }

                        if (values[i].Value > maxValue)
                        {
                            maxValue = values[i].Value;
                        }
                    }

                    decimal margin = Math.Max(Math.Abs(maxValue - minValue) * 0.1m, 0.01m);
                    chartArea.AxisY.Minimum = Math.Floor((double)(minValue - margin) * 100) / 100;
                    chartArea.AxisY.Maximum = Math.Ceiling((double)(maxValue + margin) * 100) / 100;
                }

                if (_chartContango.Series.Count > 1)
                {
                    Series seriesUpLine = _chartContango.Series[1];
                    seriesUpLine.Points.ClearFast();
                }

                chartArea.RecalculateAxesScale();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }
        }

        private void UpdateCointegrationChart()
        {
            if (_chartCointegration == null)
            {
                return;
            }

            if (_chartCointegration.InvokeRequired)
            {
                _chartCointegration.Invoke(new Action(UpdateCointegrationChart));
                return;
            }

            List<PairIndicatorValue> values = _settingsFuturesSyntheticBond.CointegrationBuilder.Cointegration;

            if (values == null
                || values.Count == 0)
            {
                return;
            }

            string lastValue = values[values.Count - 1].Value.ToString();

            LastCointegrationTextBox.Dispatcher.Invoke(new Action(delegate
            {
                LastCointegrationTextBox.Text = lastValue;
            }));

            try
            {
                Series series = _chartCointegration.Series[0];
                series.Points.ClearFast();

                for (int i = 0; i < values.Count; i++)
                {
                    decimal val = 0;
                    DateTime time = DateTime.MinValue;

                    try
                    {
                        val = values[i].Value;
                        time = values[i].Time;
                    }
                    catch
                    {
                        continue;
                    }

                    series.Points.AddXY(i + 1, val);

                    if (val > 0)
                    {
                        series.Points[series.Points.Count - 1].Color = Color.DarkGreen;
                        series.Points[series.Points.Count - 1].BorderColor = Color.DarkGreen;
                        series.Points[series.Points.Count - 1].BackSecondaryColor = Color.DarkGreen;
                    }
                    else
                    {
                        series.Points[series.Points.Count - 1].Color = Color.DarkRed;
                        series.Points[series.Points.Count - 1].BorderColor = Color.DarkRed;
                        series.Points[series.Points.Count - 1].BackSecondaryColor = Color.DarkRed;
                    }

                    string toolTip = "";

                    toolTip = "Time " + time + "\n" +
                         "Value: " + val.ToStringWithNoEndZero();

                    series.Points[series.Points.Count - 1].ToolTip = toolTip;

                    if (i + 1 == values.Count)
                    { // last point
                        series.Points[series.Points.Count - 1].Label = val.ToStringWithNoEndZero();
                        series.Points[series.Points.Count - 1].LabelForeColor = Color.AntiqueWhite;
                    }
                }

                Series seriesUpLine = _chartCointegration.Series[1];
                seriesUpLine.Points.ClearFast();

                if (_settingsFuturesSyntheticBond.CointegrationBuilder.LineUpCointegration != 0)
                {
                    seriesUpLine.Points.AddXY(0, _settingsFuturesSyntheticBond.CointegrationBuilder.LineUpCointegration);
                    seriesUpLine.Points.AddXY(series.Points.Count, _settingsFuturesSyntheticBond.CointegrationBuilder.LineUpCointegration);
                }

                Series seriesDownLine = _chartCointegration.Series[2];
                seriesDownLine.Points.ClearFast();

                if (_settingsFuturesSyntheticBond.CointegrationBuilder.LineDownCointegration != 0)
                {
                    seriesDownLine.Points.AddXY(0, _settingsFuturesSyntheticBond.CointegrationBuilder.LineDownCointegration);
                    seriesDownLine.Points.AddXY(series.Points.Count, _settingsFuturesSyntheticBond.CointegrationBuilder.LineDownCointegration);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }
        }

        #endregion

        #region Contango chart

        private void CreateContangoChart()
        {
            _chartContango = new Chart();

            ChartArea area = new ChartArea("Prime");

            _chartContango.ChartAreas.Clear();
            _chartContango.ChartAreas.Add(area);
            _chartContango.BackColor = Color.FromArgb(21, 26, 30);
            _chartContango.ChartAreas[0].AxisX.TitleForeColor = Color.FromArgb(149, 159, 176);

            for (int i = 0; _chartContango.ChartAreas != null && i < _chartContango.ChartAreas.Count; i++)
            {
                _chartContango.ChartAreas[i].BackColor = Color.FromArgb(21, 26, 30);
                _chartContango.ChartAreas[i].BorderColor = Color.FromArgb(17, 18, 23);
                _chartContango.ChartAreas[i].CursorY.LineColor = Color.FromArgb(149, 159, 176);
                _chartContango.ChartAreas[i].CursorX.LineColor = Color.FromArgb(149, 159, 176);

                foreach (Axis axe in _chartContango.ChartAreas[i].Axes)
                {
                    axe.LabelStyle.ForeColor = Color.FromArgb(149, 159, 176);
                }
            }

            // Столбцы коинтеграции
            Series series = new Series();
            series.ChartType = SeriesChartType.Column;
            _chartContango.Series.Clear();
            _chartContango.Series.Add(series);

            // Линия вернхняя

            Series seriesUpLine = new Series();
            seriesUpLine.ChartType = SeriesChartType.Line;
            seriesUpLine.Color = Color.AliceBlue;
            _chartContango.Series.Add(seriesUpLine);

            // Линия нижняя

            Series seriesDownLine = new Series();
            seriesDownLine.ChartType = SeriesChartType.Line;
            seriesDownLine.Color = Color.AliceBlue;
            _chartContango.Series.Add(seriesDownLine);

            HostContango.Child = _chartContango;
        }

        #endregion

        #region Private fields

        private Chart _chartCointegration;

        private Chart _chartContango;

        #endregion

        #region Window closed

        private void SyntheticBondOffsetUi_Closed(object sender, EventArgs e)
        {
            try
            {
                Closed -= SyntheticBondOffsetUi_Closed;

                BotTabSimple baseTab = _settingsFuturesSyntheticBond.BaseIsbergParameters.BotTab;
                BotTabSimple futuresTab = _settingsFuturesSyntheticBond.FuturesIsbergParameters.BotTab;

                if (baseTab != null)
                {
                    baseTab.CandleUpdateEvent -= BaseTab_CandleUpdateEvent;
                    baseTab.CandleFinishedEvent -= BaseTab_CandleUpdateEvent;

                    baseTab.PositionOpeningSuccesEvent -= Tab_PositionChangeEvent;
                    baseTab.PositionOpeningFailEvent -= Tab_PositionChangeEvent;
                    baseTab.PositionClosingSuccesEvent -= Tab_PositionChangeEvent;
                    baseTab.PositionClosingFailEvent -= Tab_PositionChangeEvent;
                }

                if (futuresTab != null)
                {
                    futuresTab.CandleUpdateEvent -= FuturesTab_CandleUpdateEvent;
                    futuresTab.CandleFinishedEvent -= FuturesTab_CandleUpdateEvent;

                    futuresTab.PositionOpeningSuccesEvent -= Tab_PositionChangeEvent;
                    futuresTab.PositionOpeningFailEvent -= Tab_PositionChangeEvent;
                    futuresTab.PositionClosingSuccesEvent -= Tab_PositionChangeEvent;
                    futuresTab.PositionClosingFailEvent -= Tab_PositionChangeEvent;
                }

                _synteticBond.ContangoChangeEvent -= SynteticBond_ContangoChangeEvent;
                _synteticBond.CointegrationChangeEvent -= SynteticBond_CointegrationChangeEvent;

                // Отписка от тестера

                if (baseTab != null && baseTab.StartProgram == StartProgram.IsTester)
                {
                    TesterServer server = (TesterServer)ServerMaster.GetServers()[0];
                    server.TestingEndEvent -= Server_TestingEndEvent;
                    server.TestingFastEvent -= Server_TestingFastEvent;
                    server.TestingStartEvent -= Server_TestingStartEvent;
                    server.TestRegimeChangeEvent -= Server_TestRegimeChangeEvent;
                }

                // Очистка графиков

                if (_chartSec1 != null)
                {
                    if (_chartSec1.Indicators != null)
                    {
                        _chartSec1.Indicators.Clear();
                    }

                    _chartSec1.Delete();
                    _chartSec1 = null;
                }

                if (_chartSec2 != null)
                {
                    if (_chartSec2.Indicators != null)
                    {
                        _chartSec2.Indicators.Clear();
                    }

                    _chartSec2.Delete();
                    _chartSec2 = null;
                }

                if (_chartContango != null)
                {
                    _chartContango.Series.Clear();
                    _chartContango = null;
                }

                if (_chartCointegration != null)
                {
                    _chartCointegration.Series.Clear();
                    _chartCointegration = null;
                }
            }
            catch
            {
                // ignore
            }
        }

        #endregion
    }
}
