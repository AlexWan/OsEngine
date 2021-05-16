using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using OsEngine.Charts.ColorKeeper;
using Color = System.Drawing.Color;

namespace OsEngine.OsOptimizer.OptEntity
{
    public static class WolkForwardPeriodsPainter
    {
        public static void PaintForwards(WindowsFormsHost host, List<OptimizerFaze> fazes)
        {
            // 1 создаём чарт с горизонтальными линиями

            Chart chart = CreateChart();
            host.Child = chart;

            // 2 в цикле, загоняем туда серии данных

            PaintLines(fazes, chart);

        }

        private static Chart CreateChart()
        {
            Chart _chart = null;

            try
            {
                /* if (_chart != null && _chart.InvokeRequired)
                 {
                     _chart.Invoke(new Action(CreateChart));
                     return;
                 }*/

                _chart = new Chart();
                ChartMasterColorKeeper _colorKeeper = new ChartMasterColorKeeper("walkForward");

                _chart.Series.Clear();
                _chart.ChartAreas.Clear();
                _chart.BackColor = _colorKeeper.ColorBackChart;
                _chart.SuppressExceptions = true;

                ChartArea prime = new ChartArea("Prime")
                {
                    CursorX = { AxisType = AxisType.Secondary, IsUserSelectionEnabled = false, IsUserEnabled = true, IntervalType = DateTimeIntervalType.Auto, Interval = 0.00001 },
                    CursorY = { AxisType = AxisType.Primary, IsUserEnabled = true, IntervalType = DateTimeIntervalType.Auto, Interval = 0.00001 },
                    //AxisX2 = { IsMarginVisible = false, Enabled = AxisEnabled.False },
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

                Series clusterSeries = new Series("SeriesCluster");
                clusterSeries.ChartType = SeriesChartType.RangeBar;
                clusterSeries.YAxisType = AxisType.Primary;
                clusterSeries.XAxisType = AxisType.Secondary;
                clusterSeries.ChartArea = "Prime";
                clusterSeries.ShadowOffset = 2;
                clusterSeries.YValuesPerPoint = 2;

                _chart.Series.Add(clusterSeries);

            }
            catch (Exception error)
            {
                System.Windows.MessageBox.Show(error.ToString());
            }

            return _chart;
        }

        private static void PaintLines(List<OptimizerFaze> fazes, Chart _chart)
        {
            ChartMasterColorKeeper _colorKeeper = new ChartMasterColorKeeper("walkForward");

            Series candleSeries = FindSeriesByNameSafe("SeriesCluster",_chart);

            DateTime firstTime = fazes[0].TimeStart;

            for (int i = 0,j = fazes.Count;i < fazes.Count;i++,j--)
            {
                decimal linePriceX = j;

                decimal clusterStartY = Convert.ToDecimal((fazes[i].TimeStart - firstTime).TotalDays);

                candleSeries.Points.AddXY(linePriceX, clusterStartY, clusterStartY + fazes[i].Days);

                DataPoint myPoint = candleSeries.Points[candleSeries.Points.Count - 1];

                if (fazes[i].TypeFaze == OptimizerFazeType.InSample)
                {
                    myPoint.Color = Color.White;
                    myPoint.BorderColor = Color.White;
                    myPoint.BackSecondaryColor = Color.White;
                }
                else if (fazes[i].TypeFaze == OptimizerFazeType.OutOfSample)
                {
                    myPoint.Color = Color.Green;
                    myPoint.BorderColor = Color.Green;
                    myPoint.BackSecondaryColor = Color.Green;
                }

                PaintSeriesSafe(candleSeries,_chart);

            }
        }

        private static void PaintSeriesSafe(Series series,Chart _chart)
        {
            try
            {

                    for (int i = 0; i < _chart.Series.Count; i++)
                    {
                        if (series.Name == _chart.Series[i].Name)
                        {
                            _chart.Series.Remove(FindSeriesByNameSafe(series.Name,_chart));
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
