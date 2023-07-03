/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Charts.CandleChart;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.DataVisualization.Charting;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers.Tester;

namespace OsEngine.OsTrader.Panels.Tab
{
    /// <summary>
    /// Interaction logic for BotTabPairUi.xaml
    /// </summary>
    public partial class BotTabPairUi : Window
    {
        public BotTabPairUi(PairToTrade pair)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);

            _pair = pair;

            Name = pair.Name;

            TextBoxCorrelationLookBack.Text = _pair.CorrelationLookBack.ToString();
            TextBoxCointegrationLookBack.Text = _pair.CointegrationLookBack.ToString();
            TextBoxCointegrationDeviation.Text = _pair.CointegrationDeviation.ToString();

            Title = OsLocalization.Trader.Label249;

            LabelCointegration.Content = OsLocalization.Trader.Label238;
            LabelCointegrationDeviation.Content = OsLocalization.Trader.Label239;
            LabelCointegrationLookBack.Content = OsLocalization.Trader.Label240;
            LabelCorrelation.Content = OsLocalization.Trader.Label242;
            LabelCorrelationLookBack.Content = OsLocalization.Trader.Label240;

            ButtonCointegrationReload.Content = OsLocalization.Trader.Label243;
            ButtonCorrelationReload.Content = OsLocalization.Trader.Label243;

            TextBoxCorrelationLookBack.TextChanged += TextBoxCorrelationLookBack_TextChanged;
            TextBoxCointegrationLookBack.TextChanged += TextBoxCointegrationLookBack_TextChanged;
            TextBoxCointegrationDeviation.TextChanged += TextBoxCointegrationDeviation_TextChanged;
            ButtonCorrelationReload.Click += ButtonCorrelationReload_Click;
            ButtonCointegrationReload.Click += ButtonCointegrationReload_Click;
            Closed += BotTabPairUi_Closed;

            GlobalGUILayout.Listen(this, "botTabPairUi_" + pair.Name);

            PaintCandles();

            _pair.Tab1.CandleUpdateEvent += Tab1_CandleUpdateEvent;
            _pair.Tab2.CandleUpdateEvent += Tab2_CandleUpdateEvent;

            if(pair.Tab1.StartProgram == StartProgram.IsTester)
            {
                _pair.Tab1.CandleFinishedEvent += Tab1_CandleUpdateEvent;
                _pair.Tab2.CandleFinishedEvent += Tab2_CandleUpdateEvent;
            }

            CreateCorrelationChart();
            UpdateCorrelationChart();

            CreateCointegrationChart();
            UpdateCointegrationChart();

            _pair.CorrelationChangeEvent += _pair_CorrelationChangeEvent;
            _pair.CointegrationChangeEvent += _pair_CointegrationChangeEvent;

            if(_pair.Tab1.StartProgram == StartProgram.IsTester)
            { // управление прорисовкой для тестера

                TesterServer server = (TesterServer)ServerMaster.GetServers()[0];

                server.TestingEndEvent += Server_TestingEndEvent;
                server.TestingFastEvent += Server_TestingFastEvent;
                server.TestingStartEvent += Server_TestingStartEvent;
                server.TestRegimeChangeEvent += Server_TestRegimeChangeEvent;

            }
        }


        private void BotTabPairUi_Closed(object sender, EventArgs e)
        {
            TextBoxCorrelationLookBack.TextChanged -= TextBoxCorrelationLookBack_TextChanged;
            TextBoxCointegrationLookBack.TextChanged -= TextBoxCointegrationLookBack_TextChanged;
            TextBoxCointegrationDeviation.TextChanged -= TextBoxCointegrationDeviation_TextChanged;
            ButtonCorrelationReload.Click -= ButtonCorrelationReload_Click;
            ButtonCointegrationReload.Click -= ButtonCointegrationReload_Click;
            _pair.Tab1.CandleUpdateEvent -= Tab1_CandleUpdateEvent;
            _pair.Tab2.CandleUpdateEvent -= Tab2_CandleUpdateEvent;
            _pair.Tab1.CandleFinishedEvent -= Tab1_CandleUpdateEvent;
            _pair.Tab2.CandleFinishedEvent -= Tab2_CandleUpdateEvent;

            _pair.CorrelationChangeEvent -= _pair_CorrelationChangeEvent;
            _pair.CointegrationChangeEvent -= _pair_CointegrationChangeEvent;

            Closed -= BotTabPairUi_Closed;

            if (_pair.Tab1.StartProgram == StartProgram.IsTester)
            { // управление прорисовкой для тестера

                TesterServer server = (TesterServer)ServerMaster.GetServers()[0];
                server.TestingEndEvent -= Server_TestingEndEvent;
                server.TestingFastEvent -= Server_TestingFastEvent;
                server.TestingStartEvent -= Server_TestingStartEvent;
                server.TestRegimeChangeEvent -= Server_TestRegimeChangeEvent;
            }

            _pair = null;

            if (_chartSec1 != null)
            {
                _chartSec1.Delete();
                _chartSec1 = null;
            }

            if (_chartSec2 != null)
            {
                _chartSec2.Delete();
                _chartSec2 = null;
            }

            if (_chartCorrelation != null)
            {
                _chartCorrelation.Series.Clear();
                _chartCorrelation = null;
            }

            if (_chartCointegration != null)
            {
                _chartCointegration.Series.Clear();
                _chartCointegration = null;
            }

        }

        // изменение значений пользователем

        private void TextBoxCointegrationDeviation_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _pair.CointegrationDeviation = TextBoxCointegrationDeviation.Text.ToDecimal();
                _pair.Save();
            }
            catch
            {
                return;
            }
        }

        private void TextBoxCointegrationLookBack_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _pair.CointegrationLookBack = Convert.ToInt32(TextBoxCointegrationLookBack.Text);
                _pair.Save();
            }
            catch
            {
                return;
            }
        }

        private void TextBoxCorrelationLookBack_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _pair.CorrelationLookBack = Convert.ToInt32(TextBoxCorrelationLookBack.Text);
                _pair.Save();
            }
            catch
            {
                return;
            }
        }

        // управление прорисовкой во время тестирования

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
            if (_chartCorrelation == null)
            {
                return;
            }

            if (_chartCorrelation.InvokeRequired)
            {
                _chartCorrelation.Invoke(new Action(Server_TestingStartEvent));
                return;
            }

            _chartSec1.StartPaint(null, HostSec1, null);
            _chartSec2.StartPaint(null, HostSec2, null);
            HostCorrelation.Child = _chartCorrelation;
            HostCointegration.Child = _chartCointegration;
        }

        private void Server_TestingFastEvent()
        {
            if (_chartCorrelation == null)
            {
                return;
            }

            if (_chartCorrelation.InvokeRequired)
            {
                _chartCorrelation.Invoke(new Action(Server_TestingFastEvent));
                return;
            }

            if (HostCointegration.Child == null)
            { // нужно показывать
                _chartSec1.StartPaint(null, HostSec1, null);
                _chartSec2.StartPaint(null, HostSec2, null);
                HostCorrelation.Child = _chartCorrelation;
                HostCointegration.Child = _chartCointegration;
            }
            else
            { // нужно прятать
                _chartSec1.StopPaint();
                _chartSec2.StopPaint();
                HostCorrelation.Child = null;
                HostCointegration.Child = null;
            }
        }

        private void Server_TestingEndEvent()
        {
            if (_chartCorrelation == null)
            {
                return;
            }

            if (_chartCorrelation.InvokeRequired)
            {
                _chartCorrelation.Invoke(new Action(Server_TestingEndEvent));
                return;
            }
            _chartSec1.StartPaint(null, HostSec1, null);
            _chartSec1.BindOn();
            _chartSec2.StartPaint(null, HostSec2, null);
            HostCorrelation.Child = _chartCorrelation;
            HostCointegration.Child = _chartCointegration;
        }

        // обработка нажатий на кнопки

        PairToTrade _pair;

        private void ButtonCointegrationReload_Click(object sender, RoutedEventArgs e)
        {
            _pair.ReloadCointegrationHard();
        }

        private void ButtonCorrelationReload_Click(object sender, RoutedEventArgs e)
        {
            _pair.ReloadCorrelationHard();
        }

        // прорисовка инструментов

        ChartCandleMaster _chartSec1;

        ChartCandleMaster _chartSec2;

        private void PaintCandles()
        {
            _chartSec1 = new ChartCandleMaster(_pair.Name + "sec1", _pair.Tab1.StartProgram);
            _chartSec1.StartPaint(null, HostSec1, null);
            _chartSec1.SetCandles(_pair.Tab1.CandlesAll);

            _chartSec2 = new ChartCandleMaster(_pair.Name + "sec2", _pair.Tab2.StartProgram);
            _chartSec2.StartPaint(null, HostSec2, null);
            _chartSec2.SetCandles(_pair.Tab2.CandlesAll);

            _chartSec1.Bind(_chartSec2);
        }

        private void Tab1_CandleUpdateEvent(System.Collections.Generic.List<Candle> candles)
        {
            _chartSec1.SetCandles(candles);
        }

        private void Tab2_CandleUpdateEvent(System.Collections.Generic.List<Candle> candles)
        {
            _chartSec2.SetCandles(candles);
        }

        // прорисовка корреляции

        private Chart _chartCorrelation;

        private void _pair_CorrelationChangeEvent(List<PairIndicatorValue> obj, PairToTrade pair)
        {
            UpdateCorrelationChart();
        }

        private void CreateCorrelationChart()
        {
            _chartCorrelation = new Chart();

            ChartArea area = new ChartArea("Prime");

            _chartCorrelation.ChartAreas.Clear();
            _chartCorrelation.ChartAreas.Add(area);
            _chartCorrelation.BackColor = Color.FromArgb(21, 26, 30);
            _chartCorrelation.ChartAreas[0].AxisX.TitleForeColor = Color.FromArgb(149, 159, 176);

            for (int i = 0; _chartCorrelation.ChartAreas != null && i < _chartCorrelation.ChartAreas.Count; i++)
            {
                _chartCorrelation.ChartAreas[i].BackColor = Color.FromArgb(21, 26, 30);
                _chartCorrelation.ChartAreas[i].BorderColor = Color.FromArgb(17, 18, 23);
                _chartCorrelation.ChartAreas[i].CursorY.LineColor = Color.FromArgb(149, 159, 176);
                _chartCorrelation.ChartAreas[i].CursorX.LineColor = Color.FromArgb(149, 159, 176);

                foreach (var axe in _chartCorrelation.ChartAreas[i].Axes)
                {
                    axe.LabelStyle.ForeColor = Color.FromArgb(149, 159, 176);
                }
            }

            Series series = new Series();
            series.ChartType = SeriesChartType.Column;
            _chartCorrelation.Series.Clear();
            _chartCorrelation.Series.Add(series);

            HostCorrelation.Child = _chartCorrelation;
        }

        private void UpdateCorrelationChart()
        {
            if (_chartCorrelation == null)
            {
                return;
            }

            if (_chartCorrelation.InvokeRequired)
            {
                _chartCorrelation.Invoke(new Action(UpdateCorrelationChart));
                return;
            }

            List<PairIndicatorValue> values = _pair.CorrelationList;

            if (values == null
                || values.Count == 0)
            {
                return;
            }

            try
            {
                Series series = _chartCorrelation.Series[0];
                series.Points.Clear();

                for (int i = 0; i < values.Count; i++)
                {
                    decimal val = values[i].Value;


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

                    toolTip = "Time " + values[i].Time + "\n" +
                         "Value: " + val.ToStringWithNoEndZero();

                    series.Points[series.Points.Count - 1].ToolTip = toolTip;

                    if (i + 1 == values.Count)
                    { // last point
                        series.Points[series.Points.Count - 1].Label = val.ToStringWithNoEndZero();
                        series.Points[series.Points.Count - 1].LabelForeColor = Color.AntiqueWhite;
                    }

                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }
        }

        // прорисовка коинтеграции

        private Chart _chartCointegration;

        private void _pair_CointegrationChangeEvent(List<PairIndicatorValue> obj, PairToTrade pair)
        {
            UpdateCointegrationChart();
        }

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

                foreach (var axe in _chartCointegration.ChartAreas[i].Axes)
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

            List<PairIndicatorValue> values = _pair.Cointegration;

            if (values == null
                || values.Count == 0)
            {
                return;
            }

            try
            {
                Series series = _chartCointegration.Series[0];
                series.Points.Clear();

                for (int i = 0; i < values.Count; i++)
                {
                    decimal val = values[i].Value;


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

                    toolTip = "Time " + values[i].Time + "\n" +
                         "Value: " + val.ToStringWithNoEndZero();

                    series.Points[series.Points.Count - 1].ToolTip = toolTip;

                    if (i + 1 == values.Count)
                    { // last point
                        series.Points[series.Points.Count - 1].Label = val.ToStringWithNoEndZero();
                        series.Points[series.Points.Count - 1].LabelForeColor = Color.AntiqueWhite;
                    }

                }

                Series seriesUpLine = _chartCointegration.Series[1];
                seriesUpLine.Points.Clear();

                if (_pair.LineUpCointegration != 0)
                {
                    seriesUpLine.Points.AddXY(0, _pair.LineUpCointegration);
                    seriesUpLine.Points.AddXY(series.Points.Count, _pair.LineUpCointegration);
                }

                Series seriesDownLine = _chartCointegration.Series[2];
                seriesDownLine.Points.Clear();

                if (_pair.LineDownCointegration != 0)
                {
                    seriesDownLine.Points.AddXY(0, _pair.LineDownCointegration);
                    seriesDownLine.Points.AddXY(series.Points.Count, _pair.LineDownCointegration);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }
        }

    }
}
