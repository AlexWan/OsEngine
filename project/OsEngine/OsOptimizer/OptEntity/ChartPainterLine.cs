using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using OsEngine.Charts.ColorKeeper;
using Color = System.Drawing.Color;

namespace OsEngine.OsOptimizer.OptEntity
{
    public class ChartPainterLine
    {
        public static void Paint(WindowsFormsHost host, List<decimal> line)
        {
            // 1 создаём чарт с горизонтальными линиями

            Chart chart = CreateChart();
            host.Child = chart;

            // 2 в цикле, загоняем туда серии данных

            PaintLines(line, chart);

        }

        private static Chart CreateChart()
        {
            
            Chart _chart = null;

            try
            {
                _chart = new Chart();
                ChartMasterColorKeeper _colorKeeper = new ChartMasterColorKeeper("chartPainter");

                _chart.Series.Clear();
                _chart.ChartAreas.Clear();
                _chart.BackColor = _colorKeeper.ColorBackChart;
                _chart.SuppressExceptions = true;
                
                ChartArea prime = new ChartArea("Prime")
                {
                    CursorX = { AxisType = AxisType.Secondary, IsUserSelectionEnabled = false, IsUserEnabled = true, IntervalType = DateTimeIntervalType.Auto, Interval = 0.00001 },
                    CursorY = { AxisType = AxisType.Primary, IsUserEnabled = true, IntervalType = DateTimeIntervalType.Auto, Interval = 0.00001 },
                    BorderDashStyle = ChartDashStyle.Solid,
                    BorderWidth = 2,
                    BackColor = _colorKeeper.ColorBackChart,
                    BorderColor = _colorKeeper.ColorBackSecond,
                    
                };

                prime.AxisY.TitleAlignment = StringAlignment.Near;
                prime.AxisY.TitleForeColor = _colorKeeper.ColorBackCursor;
                prime.AxisY.ScrollBar.ButtonStyle = ScrollBarButtonStyles.SmallScroll;

                prime.AxisY.LabelAutoFitMinFontSize = 12;
                prime.AxisY.LabelAutoFitStyle = LabelAutoFitStyles.IncreaseFont;
                foreach (var axe in prime.Axes)
                {
                    axe.LabelStyle.ForeColor = _colorKeeper.ColorText;
                }
                prime.CursorY.LineColor = _colorKeeper.ColorBackCursor;
                prime.CursorX.LineColor = _colorKeeper.ColorBackCursor;

                _chart.ChartAreas.Add(prime);

                Series series = new Series("Series");
                series.ChartType = SeriesChartType.Line;
                series.YAxisType = AxisType.Primary;
                series.XAxisType = AxisType.Secondary;
                series.ChartArea = "Prime";
                series.ShadowOffset = 2;
                series.YValuesPerPoint = 2;
              
                _chart.Series.Add(series);

            }
            catch (Exception error)
            {
                System.Windows.MessageBox.Show(error.ToString());
            }

            return _chart;
        }

        private static void PaintLines(List<decimal> line, Chart _chart)
        {
            Series candleSeries = FindSeriesByNameSafe("Series", _chart);

            for (int i = 0; i < line.Count; i++)
            {
                decimal linePriceX = i;

                decimal clusterStartY = line[i];

                candleSeries.Points.AddXY(linePriceX, clusterStartY);

                DataPoint myPoint = candleSeries.Points[candleSeries.Points.Count - 1];

                myPoint.Color = Color.White;
                myPoint.BorderColor = Color.White;
                myPoint.BackSecondaryColor = Color.White;

            }

            PaintSeriesSafe(candleSeries, _chart);
        }

        private static void PaintSeriesSafe(Series series, Chart _chart)
        {
            try
            {

                for (int i = 0; i < _chart.Series.Count; i++)
                {
                    if (series.Name == _chart.Series[i].Name)
                    {
                        _chart.Series.Remove(FindSeriesByNameSafe(series.Name, _chart));
                        _chart.Series.Insert(i, series);
                        break;
                    }
                }

            }
            catch (Exception error)
            {
                System.Windows.MessageBox.Show(error.ToString());
            }
        }

        private static Series FindSeriesByNameSafe(string name, Chart _chart)
        {
            Series mySeries;
            try
            {
                mySeries = _chart.Series.FindByName(name);
            }
            catch (Exception)
            {
                try
                {
                    mySeries = _chart.Series.FindByName(name);
                }
                catch (Exception)
                {
                    return null;
                }
            }

            return mySeries;
        }
    }
}
