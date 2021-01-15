using CustomAnnotations;
using OsEngine.Charts.CandleChart.Entities;
using OsEngine.Entity;
using OsEngine.Indicators;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static OsEngine.Charts.CandleChart.OxyChartPainter;

namespace OsEngine.Charts.CandleChart.OxyAreas
{
    public class IndicatorArea : OxyArea
    {
        public bool mouse_on_main_chart = false;
        public bool has_candles = false;
        public string indicator_name;

        public List<ScatterSeries> scatter_series_list = new List<ScatterSeries>();
        public List<LineSeries> lines_series_list = new List<LineSeries>();
        public List<LinearBarSeries> linear_bar_series_list = new List<LinearBarSeries>();


        public IndicatorArea(OxyAreaSettings settings, List<OxyArea> all_areas, object tag, OxyChartPainter owner) : base(settings, owner)
        {
            area_settings = settings;
            this.all_areas = all_areas;
            Tag = tag;

            date_time_axis_X.IsAxisVisible = area_settings.X_Axies_is_visible;
            date_time_axis_X.IsPanEnabled = false;

            linear_axis_Y.IsAxisVisible = area_settings.Y_Axies_is_visible;
            linear_axis_Y.IsPanEnabled = false;

           
            plot_model.Updated += Plot_model_Updated;
        }

        private void Plot_model_Updated(object sender, EventArgs e)
        {
            //owner.mediator.ZoomAll(plot_model.Axes[0].ActualMinimum, plot_model.Axes[0].ActualMaximum, this);
        }

        public override void Calculate(TimeSpan time_frame_span, TimeFrame time_frame)
        {
            for (int i = 0; i < actions_to_calculate.Count; i++)
            {
                Action action = new Action(() => { });

                bool result = actions_to_calculate.TryDequeue(out action);

                if (result)
                    plot_view.Dispatcher.Invoke(action);
            }

            Action redraw_action = () =>
            {
                

                if (!plot_model.Annotations.Contains(drawed_name))
                    plot_model.Annotations.Add(drawed_name);

                drawed_name.TextPosition = new ScreenPoint(15, 20);

                var main_chart = ((CandleStickArea)all_areas.Find(x => x is CandleStickArea));

                double plot_margin = main_chart.plot_model.ActualPlotMargins.Right;

                plot_model.PlotMargins = new OxyThickness(plot_model.PlotMargins.Left, plot_model.PlotMargins.Top, plot_margin, plot_model.PlotMargins.Bottom);
            };

            plot_view.Dispatcher.Invoke(redraw_action);
        }

        public void Zoom(double X_start, double X_end)
        {
            List<double> min_max = GetHighLow(false, X_start, X_end);

            linear_axis_Y.Zoom(min_max[0], min_max[1]);

            date_time_axis_X.Zoom(X_start, X_end);
        }

        public override void BuildIndicatorSeries(IndicatorSeria indi_seria, List<decimal> data_points, TimeSpan time_frame_span)
        {
            var time_step_double = 1 / (1000 * 60 * 60 * 24 / time_frame_span.TotalMilliseconds);

            indi_seria.DataPoints = data_points;

            if (indi_seria.DataPoints == null || indi_seria.DataPoints.Count == 0)
            {
                return;
            }

            if (plot_view == null || plot_model == null)
            {
                return;
            }

            var main_chart = (CandleStickArea)all_areas.Find(x => x is CandleStickArea);

            lock (series_locker)
            {
                if (main_chart != null && (string)Tag != "Prime")
                {
                    if (indi_seria.IndicatorType == IndicatorChartPaintType.Column)
                    {
                        if (indi_seria.DataPoints.Count == indi_seria.IndicatorPoints.Count)
                        {
                            double last_point = (double)indi_seria.DataPoints.Last();

                            if (last_point == 0)
                                last_point = double.NaN;

                            indi_seria.IndicatorHistogramPoints[indi_seria.IndicatorHistogramPoints.Count - 1] = new DataPoint(items_oxy_candles.Last().X, last_point);
                        }
                        else if (indi_seria.DataPoints.Count == indi_seria.IndicatorPoints.Count + 1)
                        {
                            double last_point = (double)indi_seria.DataPoints.Last();

                            if (last_point == 0)
                                last_point = double.NaN;

                            indi_seria.IndicatorHistogramPoints.Add(new DataPoint(items_oxy_candles.Last().X, last_point));

                        }
                        else
                        {
                            indi_seria.IndicatorHistogramPoints.Clear();

                            List<DataPoint> points = new List<DataPoint>();

                            for (int i = 0; i < indi_seria.DataPoints.Count; i++)
                            {
                                double last_point = (double)indi_seria.DataPoints[i];

                                if (last_point == 0)
                                    last_point = double.NaN;

                                try
                                {
                                    points.Add(new DataPoint(items_oxy_candles[i].X, last_point));
                                }
                                catch { return; }
                            }

                            indi_seria.IndicatorHistogramPoints = points.ToList();
                        }



                        LinearBarSeries linear_bar_seria = new LinearBarSeries()
                        {
                            StrokeThickness = 1,
                            StrokeColor = OxyColor.FromArgb(255, 55, 219, 186),
                            FillColor = OxyColor.FromArgb(69, 55, 219, 186),
                            NegativeFillColor = OxyColor.FromArgb(69, 235, 96, 47),
                            NegativeStrokeColor = OxyColor.FromArgb(255, 235, 96, 47),
                            Tag = indi_seria.SeriaName
                        };


                        linear_bar_seria.Points.AddRange(indi_seria.IndicatorHistogramPoints);


                        if (linear_bar_series_list.Exists(x => (string)x.Tag == indi_seria.SeriaName))
                            linear_bar_series_list.Remove(linear_bar_series_list.Find(x => (string)x.Tag == indi_seria.SeriaName));

                        linear_bar_series_list.Add(linear_bar_seria);

                    }



                    if (indi_seria.IndicatorType == IndicatorChartPaintType.Line)
                    {
                        if (indi_seria.DataPoints.Count == indi_seria.IndicatorPoints.Count)
                        {
                            double last_point = (double)indi_seria.DataPoints.Last();

                            if (last_point == 0)
                                last_point = double.NaN;


                            indi_seria.IndicatorPoints[indi_seria.IndicatorPoints.Count - 1] = new DataPoint(items_oxy_candles.Last().X, last_point);

                            indi_seria.IndicatorPoints[indi_seria.IndicatorPoints.Count - 1] = new DataPoint(items_oxy_candles.Last().X, last_point);
                        }
                        else if (indi_seria.DataPoints.Count == indi_seria.IndicatorPoints.Count + 1)
                        {
                            double last_point = (double)indi_seria.DataPoints.Last();

                            if (last_point == 0)
                                last_point = double.NaN;


                            indi_seria.IndicatorPoints.Add(new DataPoint(items_oxy_candles.Last().X, last_point));
                        }
                        else
                        {
                            indi_seria.IndicatorPoints.Clear();

                            List<DataPoint> points = new List<DataPoint>();

                            for (int i = 0; i < indi_seria.DataPoints.Count; i++)
                            {
                                double last_point = (double)indi_seria.DataPoints[i];

                                if (last_point == 0)
                                    last_point = double.NaN;

                                try
                                {
                                    points.Add(new DataPoint(items_oxy_candles[i].X, last_point));
                                }
                                catch { return; }

                            };

                            indi_seria.IndicatorPoints = points;
                        }


                        LineSeries line_seria = new LineSeries()
                        {
                            StrokeThickness = 1,
                            LineStyle = LineStyle.Solid,
                            Color = indi_seria.OxyColor,
                            Tag = indi_seria.SeriaName
                        };

                        line_seria.Points.AddRange(indi_seria.IndicatorPoints);


                        if (lines_series_list.Exists(x => (string)x.Tag == indi_seria.SeriaName))
                            lines_series_list.Remove(lines_series_list.Find(x => (string)x.Tag == indi_seria.SeriaName));

                        lines_series_list.Add(line_seria);

                    }


                    if (indi_seria.IndicatorType == IndicatorChartPaintType.Point)
                    {
                        if (indi_seria.DataPoints.Count == indi_seria.IndicatorScatterPoints.Count)
                        {
                            double last_point = (double)indi_seria.DataPoints.Last();

                            if (last_point == 0)
                                last_point = double.NaN;

                            indi_seria.IndicatorScatterPoints[indi_seria.IndicatorScatterPoints.Count - 1] = new ScatterPoint(items_oxy_candles.Last().X, last_point);
                        }
                        else if (indi_seria.DataPoints.Count == indi_seria.IndicatorScatterPoints.Count + 1)
                        {
                            double last_point = (double)indi_seria.DataPoints.Last();

                            if (last_point == 0)
                                last_point = double.NaN;

                            indi_seria.IndicatorScatterPoints.Add(new ScatterPoint(items_oxy_candles.Last().X, last_point));
                        }
                        else
                        {
                            indi_seria.IndicatorScatterPoints.Clear();

                            List<ScatterPoint> points = new List<ScatterPoint>();

                            for (int i = 0; i < indi_seria.DataPoints.Count; i++)
                            {
                                double last_point = (double)indi_seria.DataPoints[i];

                                if (last_point == 0)
                                    last_point = double.NaN;

                                try
                                {
                                    points.Add(new ScatterPoint(items_oxy_candles[i].X, last_point));
                                }
                                catch { return; }
                            };

                            indi_seria.IndicatorScatterPoints = points;

                        }

                        ScatterSeries scatter_seria = new ScatterSeries()
                        {
                            MarkerType = MarkerType.Circle,
                            MarkerFill = indi_seria.OxyColor,
                            MarkerSize = 2,
                            MarkerStrokeThickness = 0,
                            Tag = indi_seria.SeriaName
                        };

                        scatter_seria.Points.AddRange(indi_seria.IndicatorScatterPoints);


                        if (scatter_series_list.Exists(x => (string)x.Tag == indi_seria.SeriaName))
                            scatter_series_list.Remove(scatter_series_list.Find(x => (string)x.Tag == indi_seria.SeriaName));

                        scatter_series_list.Add(scatter_seria);

                    }
                }
            }

        }

        public override List<double> GetHighLow(bool isPrime, double start, double end)
        {
            List<double> values = new List<double>();

            lock (series_locker)
            {



                if (isPrime)
                {
                    if (my_candles != null && my_candles.Count > 0)
                    {
                        values.AddRange(items_oxy_candles.Where(x => x.X >= start && x.X <= end).Select(x => x.High));
                        values.AddRange(items_oxy_candles.Where(x => x.X >= start && x.X <= end).Select(x => x.Low));

                        if (values.Count == 0)
                            values.AddRange(items_oxy_candles.Select(x => x.X));
                    }
                }


                if (scatter_series_list.Count > 0)
                {
                    foreach (var scatter in scatter_series_list)
                    {
                        values.AddRange(scatter.Points.Where(x => x.X >= start && x.X <= end).Select(x => x.Y));
                    }
                }

                if (lines_series_list.Count > 0)
                {
                    foreach (var lines_series in lines_series_list)
                    {
                        values.AddRange(lines_series.Points.Where(x => x.X >= start && x.X <= end).Select(x => x.Y));
                    }
                }

                if (linear_bar_series_list.Count > 0)
                {
                    foreach (var bar_series in linear_bar_series_list)
                    {
                        values.AddRange(bar_series.Points.Where(x => x.X >= start && x.X <= end).Select(x => x.Y));
                    }
                }

            }

                if (values.Count == 0)
                    return new List<double>() { 0, 0 };


                return new List<double>() { values.Min(), values.Max() };      
        }

        public override void Redraw()
        {
            
                Action action = () =>
                {
                    lock (series_locker)
                    {
                        plot_model.Series.Clear();

                        foreach (var linear_bar_series in linear_bar_series_list)
                        {
                            plot_model.Series.Add(linear_bar_series);
                        }

                        foreach (var line_series in lines_series_list)
                        {
                            plot_model.Series.Add(line_series);
                        }

                        foreach (var scatter_series in scatter_series_list)
                        {
                            plot_model.Series.Add(scatter_series);
                        }

                        plot_view.InvalidatePlot(true);
                    }
                };

                plot_view.Dispatcher.Invoke(action);
            
        }

        public override void Dispose()
        {
            base.Dispose();


            List<ScatterSeries> new_scatter_series_list = new List<ScatterSeries>();

            for (int i = 0; i < scatter_series_list.Count; i++)
            {
                if (((string)scatter_series_list[i].Tag).Contains("element"))
                {
                    new_scatter_series_list.Add(scatter_series_list[i]);
                }
            }

            List<LineSeries> new_lines_series_list = new List<LineSeries>();

            for (int i = 0; i < lines_series_list.Count; i++)
            {
                if (((string)lines_series_list[i].Tag).Contains("HorLine"))
                {
                    new_lines_series_list.Add(lines_series_list[i]);
                }
            }

            scatter_series_list = new_scatter_series_list;
            lines_series_list = new_lines_series_list;
            linear_bar_series_list = new List<LinearBarSeries>();
            my_candles = new List<Candle>();
            items_oxy_candles = new List<HighLowItem>();
        }
    }
}
