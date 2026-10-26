/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Charts.ColorKeeper;
using OsEngine.IndicatorMarketDepth;
using OsEngine.Indicators;
using OsEngine.Logging;
using OsEngine.Market;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;

namespace OsEngine.Charts.MarketDepthChart
{
    public class MarketDepthIndicatorChart
    {
        private const string LegendName = "IndicatorLegend";

        private Chart _chart;

        private WindowsFormsHost _host;

        private ChartMasterColorKeeper _colorKeeper;

        private Color _gridColor;

        private Title _valueTitle;

        private AMarketDepth _indicator;

        public void StartPaint(WindowsFormsHost host, string name)
        {
            _host = host;

            _colorKeeper = new ChartMasterColorKeeper(name);
            _gridColor = Color.FromArgb(40, _colorKeeper.ColorText.R, _colorKeeper.ColorText.G, _colorKeeper.ColorText.B);

            _chart = new Chart();
            _chart.Dock = DockStyle.Fill;
            _chart.BackColor = _colorKeeper.ColorBackChart;

            ChartArea area = new ChartArea("Prime");
            area.BackColor = _colorKeeper.ColorBackChart;
            area.BorderColor = _colorKeeper.ColorBackSecond;
            area.BorderDashStyle = ChartDashStyle.Solid;
            area.BorderWidth = 1;
            area.CursorY.LineColor = _colorKeeper.ColorBackCursor;
            area.CursorX.LineColor = _colorKeeper.ColorBackCursor;
            area.CursorX.IsUserEnabled = true;
            area.CursorX.IsUserSelectionEnabled = false;
            area.CursorY.IsUserEnabled = true;
            area.CursorY.IsUserSelectionEnabled = false;

            foreach (Axis axe in area.Axes)
            {
                axe.LabelStyle.ForeColor = _colorKeeper.ColorText;
                axe.LineColor = _colorKeeper.ColorText;
                axe.MajorGrid.LineColor = _gridColor;
                axe.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            }

            area.AxisX.LabelStyle.Format = "HH:mm:ss";
            area.AxisX.IntervalType = DateTimeIntervalType.Auto;
            area.AxisY.Title = "";
            area.AxisY.TitleForeColor = _colorKeeper.ColorText;
            area.AxisY2.Title = "";
            area.AxisY2.TitleForeColor = _colorKeeper.ColorText;

            _chart.ChartAreas.Add(area);

            Title title = new Title(name);
            title.ForeColor = _colorKeeper.ColorText;
            title.Font = new Font("Arial", 10f, FontStyle.Bold);
            title.Docking = Docking.Top;
            _chart.Titles.Add(title);

            _valueTitle = new Title("");
            _valueTitle.ForeColor = _colorKeeper.ColorBackCursor;
            _valueTitle.Font = new Font("Arial", 9f);
            _valueTitle.Docking = Docking.Bottom;
            _chart.Titles.Add(_valueTitle);

            Legend legend = new Legend(LegendName);
            legend.ForeColor = _colorKeeper.ColorText;
            legend.BackColor = Color.Transparent;
            legend.Docking = Docking.Top;
            _chart.Legends.Add(legend);

            _chart.MouseMove += Chart_MouseMove;
            _chart.MouseLeave += Chart_MouseLeave;

            host.Child = _chart;
        }

        public void StopPaint()
        {
            if (_chart != null)
            {
                _chart.MouseMove -= Chart_MouseMove;
                _chart.MouseLeave -= Chart_MouseLeave;
            }

            if (_host != null)
            {
                _host.Child = null;
            }

            if (_chart != null)
            {
                try
                {
                    _chart.Dispose();
                }
                catch (Exception error)
                {
                    ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
            }

            _chart = null;
            _host = null;
            _colorKeeper = null;
            _valueTitle = null;
            _indicator = null;
        }

        public void PaintIndicator(AMarketDepth indicator)
        {
            if (_chart == null || indicator == null || indicator.IsOn == false)
            {
                return;
            }

            _indicator = indicator;

            List<IndicatorDataSeries> series = indicator.DataSeries;

            ChartArea area = FindAreaByNameSafe("Prime");

            if (area != null)
            {
                area.AxisY.Enabled = AxisEnabled.False;
                area.AxisY2.Enabled = AxisEnabled.Auto;
            }

            for (int i = 0; series != null && i < series.Count; i++)
            {
                if (series[i].IsPaint == false)
                {
                    continue;
                }

                if (series[i].ChartPaintType == IndicatorChartPaintType.Line)
                {
                    PaintLine(series[i], AxisType.Secondary);
                }
                else if (series[i].ChartPaintType == IndicatorChartPaintType.Column)
                {
                    PaintColumn(series[i], AxisType.Secondary);
                }
                else if (series[i].ChartPaintType == IndicatorChartPaintType.Point)
                {
                    PaintPoint(series[i], AxisType.Secondary);
                }
            }
        }

        public void ClearIndicator(AMarketDepth indicator)
        {
            if (_chart == null || indicator == null)
            {
                return;
            }

            List<IndicatorDataSeries> series = indicator.DataSeries;

            for (int i = 0; series != null && i < series.Count; i++)
            {
                Series mySeries = FindSeriesByNameSafe(series[i].Name);

                if (mySeries != null)
                {
                    mySeries.Points.Clear();
                }
            }
        }

        public void ClearAll()
        {
            if (_chart == null)
            {
                return;
            }

            for (int i = _chart.Series.Count - 1; i >= 0; i--)
            {
                _chart.Series.RemoveAt(i);
            }
        }

        public void RePaint()
        {
            if (_chart != null)
            {
                _chart.Refresh();
            }
        }

        private void Chart_MouseMove(object sender, MouseEventArgs e)
        {
            if (_chart == null || _valueTitle == null)
            {
                return;
            }

            try
            {
                HitTestResult hit = _chart.HitTest(e.X, e.Y, ChartElementType.DataPoint);

                if (hit == null || hit.ChartElementType != ChartElementType.DataPoint || hit.Series == null)
                {
                    return;
                }

                DataPoint point = hit.Series.Points[hit.PointIndex];
                double x = point.XValue;
                double y = point.YValues[0];

                string xText = hit.Series.XValueType == ChartValueType.DateTime
                    ? DateTime.FromOADate(x).ToString("HH:mm:ss")
                    : x.ToString("0");

                string title = xText;

                if (_indicator != null)
                {
                    int index = hit.PointIndex;

                    for (int i = 0; i < _indicator.DataSeries.Count; i++)
                    {
                        IndicatorDataSeries dataSeries = _indicator.DataSeries[i];

                        if (dataSeries.IsPaint == false || index >= dataSeries.Values.Count)
                        {
                            continue;
                        }

                        title += "   " + dataSeries.Name + ": " + dataSeries.Values[index].ToString("0.####");
                    }
                }
                else
                {
                    title += "   " + y.ToString("0.####");
                }

                _valueTitle.Text = title;

                ChartArea area = FindAreaByNameSafe("Prime");

                if (area != null)
                {
                    area.CursorX.Position = x;
                    area.CursorY.AxisType = AxisType.Secondary;
                    area.CursorY.Position = y;
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void Chart_MouseLeave(object sender, EventArgs e)
        {
            try
            {
                if (_valueTitle != null)
                {
                    _valueTitle.Text = "";
                }

                ChartArea area = FindAreaByNameSafe("Prime");

                if (area != null)
                {
                    area.CursorX.Position = double.NaN;
                    area.CursorY.Position = double.NaN;
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void PaintLine(IndicatorDataSeries dataSeries, AxisType axis)
        {
            if (dataSeries.Values == null || dataSeries.Values.Count == 0)
            {
                return;
            }

            Series series = FindSeriesByNameSafe(dataSeries.Name) ?? CreateSeries(dataSeries.Name, IndicatorChartPaintType.Line, axis);

            series.YAxisType = axis;
            series.ChartType = SeriesChartType.Line;
            series.Color = dataSeries.Color;
            series.BorderWidth = dataSeries.LineWidth;

            if (dataSeries.ZeroIsGap)
            {
                series.EmptyPointStyle.Color = Color.Transparent;
            }

            bool useTime = HasTimes(dataSeries);

            series.XValueType = useTime ? ChartValueType.DateTime : ChartValueType.Double;

            series.Points.Clear();

            for (int i = 0; i < dataSeries.Values.Count; i++)
            {
                DataPoint point = new DataPoint(GetX(dataSeries, i, useTime), (double)dataSeries.Values[i]);

                point.ToolTip = GetToolTip(dataSeries, i, useTime);

                if (dataSeries.Values[i] == 0 && dataSeries.ZeroIsGap)
                {
                    point.IsEmpty = true;
                }

                series.Points.Add(point);
            }
        }

        private void PaintColumn(IndicatorDataSeries dataSeries, AxisType axis)
        {
            if (dataSeries.Values == null || dataSeries.Values.Count == 0)
            {
                return;
            }

            Series series = FindSeriesByNameSafe(dataSeries.Name) ?? CreateSeries(dataSeries.Name, IndicatorChartPaintType.Column, axis);

            series.YAxisType = axis;
            series.ChartType = SeriesChartType.Column;
            series.Color = dataSeries.Color;

            double pointWidth = 0.7 + 0.1 * dataSeries.LineWidth;

            if (pointWidth > 1.0)
            {
                pointWidth = 1.0;
            }

            series["PointWidth"] = pointWidth.ToString("0.0", CultureInfo.InvariantCulture);

            bool useTime = HasTimes(dataSeries);

            series.XValueType = useTime ? ChartValueType.DateTime : ChartValueType.Double;

            series.Points.Clear();

            for (int i = 0; i < dataSeries.Values.Count; i++)
            {
                DataPoint point = new DataPoint(GetX(dataSeries, i, useTime), (double)dataSeries.Values[i]);
                point.ToolTip = GetToolTip(dataSeries, i, useTime);
                series.Points.Add(point);
            }
        }

        private void PaintPoint(IndicatorDataSeries dataSeries, AxisType axis)
        {
            if (dataSeries.Values == null || dataSeries.Values.Count == 0)
            {
                return;
            }

            Series series = FindSeriesByNameSafe(dataSeries.Name) ?? CreateSeries(dataSeries.Name, IndicatorChartPaintType.Point, axis);

            series.YAxisType = axis;
            series.ChartType = SeriesChartType.Point;
            series.Color = dataSeries.Color;
            series.MarkerStyle = MarkerStyle.Circle;
            series.MarkerSize = 4 * dataSeries.LineWidth;

            bool useTime = HasTimes(dataSeries);

            series.XValueType = useTime ? ChartValueType.DateTime : ChartValueType.Double;

            series.Points.Clear();

            for (int i = 0; i < dataSeries.Values.Count; i++)
            {
                DataPoint point = new DataPoint(GetX(dataSeries, i, useTime), (double)dataSeries.Values[i]);
                point.ToolTip = GetToolTip(dataSeries, i, useTime);
                series.Points.Add(point);
            }
        }

        private static bool HasTimes(IndicatorDataSeries dataSeries)
        {
            return dataSeries.Times != null
                && dataSeries.Times.Count == dataSeries.Values.Count
                && dataSeries.Times.Count > 0;
        }

        private static double GetX(IndicatorDataSeries dataSeries, int index, bool useTime)
        {
            if (useTime)
            {
                return dataSeries.Times[index].ToOADate();
            }

            return index;
        }

        private static string GetToolTip(IndicatorDataSeries dataSeries, int index, bool useTime)
        {
            string value = dataSeries.Values[index].ToString("0.####");

            if (useTime)
            {
                return dataSeries.Times[index].ToString("HH:mm:ss") + "  " + value;
            }

            return index.ToString() + "  " + value;
        }

        private Series CreateSeries(string name, IndicatorChartPaintType type, AxisType axis)
        {
            ChartArea area = FindAreaByNameSafe("Prime");

            Series newSeries = new Series(name);
            newSeries.ChartArea = area.Name;
            newSeries.YAxisType = axis;
            newSeries.Legend = LegendName;
            newSeries.ShadowOffset = Themes.ThemeManager.GetChartIndicatorShadow();

            if (type == IndicatorChartPaintType.Line)
            {
                newSeries.ChartType = SeriesChartType.Line;
            }
            else if (type == IndicatorChartPaintType.Column)
            {
                newSeries.ChartType = SeriesChartType.Column;
            }
            else if (type == IndicatorChartPaintType.Point)
            {
                newSeries.ChartType = SeriesChartType.Point;
            }

            _chart.Series.Add(newSeries);

            return newSeries;
        }

        private Series FindSeriesByNameSafe(string name)
        {
            if (_chart == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            return _chart.Series.FindByName(name);
        }

        private ChartArea FindAreaByNameSafe(string name)
        {
            if (_chart == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            return _chart.ChartAreas.FindByName(name);
        }
    }
}
