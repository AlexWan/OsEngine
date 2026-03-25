/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Charts;
using OsEngine.Charts.CandleChart;
using OsEngine.Entity;
using OsEngine.Entity.SyntheticBondEntity;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers.Tester;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.Windows.Forms.DataVisualization.Charting;

namespace OsEngine.OsTrader.Panels.Tab.SyntheticBondTab
{
    /// <summary>
    /// Логика взаимодействия для SynteticBondChartUi.xaml
    /// </summary>
    public partial class SyntheticBondChartUi : Window
    {
        #region Constructor

        private SyntheticBondSeries _syntheticBondSeries;

        private SyntheticBond _syntheticBond;

        private ChartCandleMaster _chartSec1;

        private ChartCandleMaster _chartSec2;

        public SyntheticBondChartUi(SyntheticBondSeries synteticBondSeries, ref SyntheticBond modificationFuturesSyntheticBond)
        {
            InitializeComponent();

            _syntheticBondSeries = synteticBondSeries;
            _syntheticBond = modificationFuturesSyntheticBond;

            Title = OsLocalization.Trader.Label699;

            LastSec1Label.Content = OsLocalization.Trader.Label717 + ":";
            LastSec2Label.Content = OsLocalization.Trader.Label717 + ":";
            LastContangoLabel.Content = OsLocalization.Trader.Label717 + ":";
            LastCointegrationLabel.Content = OsLocalization.Trader.Label717 + ":";

            if (_syntheticBond.BaseIcebergParameters != null
                && _syntheticBond.BaseIcebergParameters.BotTab != null
                && _syntheticBond.BaseIcebergParameters.BotTab.Connector != null
                && _syntheticBond.BaseIcebergParameters.BotTab.Connector.SecurityName != null)
            {
                Security1Label.Content = _syntheticBond.BaseIcebergParameters.BotTab.Connector.SecurityName;
            }
            else
            {
                Security1Label.Content = "None";
            }

            if (_syntheticBond != null && _syntheticBond.FuturesIcebergParameters != null && _syntheticBond.FuturesIcebergParameters.BotTab != null &&
                _syntheticBond.FuturesIcebergParameters.BotTab.Connector != null && _syntheticBond.FuturesIcebergParameters.BotTab.Connector.SecurityName != null)
            {
                Security2Label.Content = _syntheticBond.FuturesIcebergParameters.BotTab.Connector.SecurityName;
            }
            else
            {
                Security2Label.Content = "None";
            }

            CreateContangoChart();
            CreateCointegrationChart();

            PaintCandles();

            BotTabSimple baseTab = _syntheticBond.BaseIcebergParameters.BotTab;
            BotTabSimple futuresTab = _syntheticBond.FuturesIcebergParameters.BotTab;

            baseTab.CandleUpdateEvent += Tab_CandleUpdateEvent;
            futuresTab.CandleUpdateEvent += Tab_CandleUpdateEvent;

            if (baseTab.StartProgram == StartProgram.IsTester)
            {
                baseTab.CandleFinishedEvent += Tab_CandleUpdateEvent;
                futuresTab.CandleFinishedEvent += Tab_CandleUpdateEvent;
            }

            _syntheticBondSeries.ContangoChangeEvent += SynteticBond_ContangoChangeEvent;
            _syntheticBondSeries.CointegrationChangeEvent += SynteticBond_CointegrationChangeEvent;

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
            BotTabSimple baseTabForChart = _syntheticBond.BaseIcebergParameters.BotTab;
            BotTabSimple futuresTabForChart = _syntheticBond.FuturesIcebergParameters.BotTab;

            _chartSec1 = new ChartCandleMaster(baseTabForChart.TabName + "sec1", baseTabForChart.StartProgram);
            _chartSec1.StartPaint(null, HostSec1, null);

            _chartSec2 = new ChartCandleMaster(futuresTabForChart.TabName + "sec2", futuresTabForChart.StartProgram);
            _chartSec2.StartPaint(null, HostSec2, null);

            List<Candle> processedSec1;
            List<Candle> processedSec2;
            _syntheticBondSeries.GetProcessedCandles(_syntheticBond, out processedSec1, out processedSec2);

            if (processedSec1 != null && processedSec1.Count > 0)
            {
                _chartSec1.SetCandles(processedSec1);
            }
            else
            {
                _chartSec1.SetCandles(baseTabForChart.CandlesAll);
            }

            if (processedSec2 != null && processedSec2.Count > 0)
            {
                _chartSec2.SetCandles(processedSec2);
            }
            else
            {
                _chartSec2.SetCandles(futuresTabForChart.CandlesAll);
            }

            for (int i = 0; i < baseTabForChart.Indicators.Count; i++)
            {
                if (_chartSec1.IndicatorIsCreate(baseTabForChart.Indicators[i].Name) == false)
                {
                    _chartSec1.CreateIndicator(baseTabForChart.Indicators[i], baseTabForChart.Indicators[i].NameArea);
                }
            }

            for (int i = 0; i < futuresTabForChart.Indicators.Count; i++)
            {
                if (_chartSec2.IndicatorIsCreate(futuresTabForChart.Indicators[i].Name) == false)
                {
                    _chartSec2.CreateIndicator(futuresTabForChart.Indicators[i], futuresTabForChart.Indicators[i].NameArea);
                }
            }
        }

        public string Key
        {
            get
            {
                return _syntheticBond.FuturesIcebergParameters.BotTab.TabName;
            }
        }

        #endregion

        #region Candle event handlers

        private void Tab_CandleUpdateEvent(List<Candle> candles)
        {
            List<Candle> processedSec1;
            List<Candle> processedSec2;
            _syntheticBondSeries.GetProcessedCandles(_syntheticBond, out processedSec1, out processedSec2);

            if (processedSec1 != null && processedSec1.Count > 0 && _chartSec1 != null)
            {
                _chartSec1.SetCandles(processedSec1);
                UpdateLastPriceLabel(LastSec1TextBox, processedSec1);
            }

            if (processedSec2 != null && processedSec2.Count > 0 && _chartSec2 != null)
            {
                _chartSec2.SetCandles(processedSec2);
                UpdateLastPriceLabel(LastSec2TextBox, processedSec2);
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
            if (settings != _syntheticBond)
            {
                return;
            }

            if ((_chartSec1 != null && _chartSec1.ChartCandle == null) ||
                _chartSec2 != null && _chartSec2.ChartCandle == null)
            {
                return;
            }

            UpdateContangoChart();

            List<Candle> processedSec1;
            List<Candle> processedSec2;
            _syntheticBondSeries.GetProcessedCandles(_syntheticBond, out processedSec1, out processedSec2);

            if (processedSec1 != null && processedSec1.Count > 0)
            {
                _chartSec1.SetCandles(processedSec1);
                UpdateLastPriceLabel(LastSec1TextBox, processedSec1);
            }

            if (processedSec2 != null && processedSec2.Count > 0)
            {
                _chartSec2.SetCandles(processedSec2);
                UpdateLastPriceLabel(LastSec2TextBox, processedSec2);
            }
        }

        private void SynteticBond_CointegrationChangeEvent(SyntheticBond settings)
        {
            if (settings != _syntheticBond)
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

            BotTabSimple baseTabPos = _syntheticBond.BaseIcebergParameters.BotTab;
            BotTabSimple futuresTabPos = _syntheticBond.FuturesIcebergParameters.BotTab;

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

            List<PairIndicatorValue> values = _syntheticBond.AbsoluteSeparationCandles;

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

            List<PairIndicatorValue> values = _syntheticBond.CointegrationBuilder.Cointegration;

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

                    string toolTip = "Time: " + time + "\nValue: " + val.ToStringWithNoEndZero() + "\nIndex: " + (i + 1);

                    series.Points[series.Points.Count - 1].ToolTip = toolTip;

                    if (i + 1 == values.Count)
                    { // last point
                        series.Points[series.Points.Count - 1].Label = val.ToStringWithNoEndZero();
                        series.Points[series.Points.Count - 1].LabelForeColor = Color.AntiqueWhite;
                    }
                }

                Series seriesUpLine = _chartCointegration.Series[1];
                seriesUpLine.Points.ClearFast();

                if (_syntheticBond.CointegrationBuilder.LineUpCointegration != 0)
                {
                    seriesUpLine.Points.AddXY(0, _syntheticBond.CointegrationBuilder.LineUpCointegration);
                    seriesUpLine.Points.AddXY(series.Points.Count, _syntheticBond.CointegrationBuilder.LineUpCointegration);
                }

                Series seriesDownLine = _chartCointegration.Series[2];
                seriesDownLine.Points.ClearFast();

                if (_syntheticBond.CointegrationBuilder.LineDownCointegration != 0)
                {
                    seriesDownLine.Points.AddXY(0, _syntheticBond.CointegrationBuilder.LineDownCointegration);
                    seriesDownLine.Points.AddXY(series.Points.Count, _syntheticBond.CointegrationBuilder.LineDownCointegration);
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

                BotTabSimple baseTab = _syntheticBond.BaseIcebergParameters.BotTab;
                BotTabSimple futuresTab = _syntheticBond.FuturesIcebergParameters.BotTab;

                if (baseTab != null)
                {
                    baseTab.CandleUpdateEvent -= Tab_CandleUpdateEvent;
                    baseTab.CandleFinishedEvent -= Tab_CandleUpdateEvent;

                    baseTab.PositionOpeningSuccesEvent -= Tab_PositionChangeEvent;
                    baseTab.PositionOpeningFailEvent -= Tab_PositionChangeEvent;
                    baseTab.PositionClosingSuccesEvent -= Tab_PositionChangeEvent;
                    baseTab.PositionClosingFailEvent -= Tab_PositionChangeEvent;
                }

                if (futuresTab != null)
                {
                    futuresTab.CandleUpdateEvent -= Tab_CandleUpdateEvent;
                    futuresTab.CandleFinishedEvent -= Tab_CandleUpdateEvent;

                    futuresTab.PositionOpeningSuccesEvent -= Tab_PositionChangeEvent;
                    futuresTab.PositionOpeningFailEvent -= Tab_PositionChangeEvent;
                    futuresTab.PositionClosingSuccesEvent -= Tab_PositionChangeEvent;
                    futuresTab.PositionClosingFailEvent -= Tab_PositionChangeEvent;
                }

                _syntheticBondSeries.ContangoChangeEvent -= SynteticBond_ContangoChangeEvent;
                _syntheticBondSeries.CointegrationChangeEvent -= SynteticBond_CointegrationChangeEvent;

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
