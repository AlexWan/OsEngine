using CustomAnnotations;
using OsEngine.Charts.CandleChart.Entities;
using OsEngine.Charts.CandleChart.OxyAreas;
using OsEngine.Entity;
using OsEngine.Indicators;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.SkiaSharp.Wpf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace OsEngine.Charts.CandleChart.OxyAreas
{
    public class OxyArea 
    {
        public bool isFreeze = false;
        public double last_date_time = 0;
        public object Tag = new object();
        public List<OxyArea> all_areas;

        public PlotView plot_view;
        public PlotModel plot_model;
        public OxyAreaSettings area_settings;

        public DateTimeAxis date_time_axis_X;
        public LinearAxis linear_axis_Y;

        public LineAnnotation cursor_Y;
        public LineAnnotation cursor_X;

        public CustomTextAnnotation annotation_price;
        public CustomTextAnnotation annotation_date_time;

        
        public ScreenPoint mouse_screen_point = new ScreenPoint();
        public System.Windows.Input.MouseEventArgs mouse_event_args;
        public List<Candle> my_candles = new List<Candle>();
        public List<HighLowItem> items_oxy_candles = new List<HighLowItem>();
        public List<HighLowItem> candle_on_screen = new List<HighLowItem>();
        public CandleStickSeries candle_stick_series = new CandleStickSeries();

        public int candles_in_run;
        public int empty_gap;

        public List<ScatterSeries>   scatter_series_list      = new List<ScatterSeries>();
        public List<LineSeries>      lines_series_list        = new List<LineSeries>();
        public List<AreaSeries>      area_seriies_list        = new List<AreaSeries>();
        public List<BarSeries>       bar_series_list          = new List<BarSeries>();
        public List<VolumeSeries>    volume_series_list       = new List<VolumeSeries>();
        public List<HistogramSeries> histogram_series_list    = new List<HistogramSeries>();
        public List<LinearBarSeries> linear_bar_series_list   = new List<LinearBarSeries>();

        public object redraw_locker = new object();
        object lines_locker = new object();
        object scatter_locker = new object();
        object area_locker = new object();
        object histogram_locker = new object();
        object linear_bar_locker = new object();
        object bar_locker = new object();
        object volume_locker = new object();

        public OxyChartPainter owner;

        public TimeSpan time_frame_span;
        public TimeFrame time_frame;

        public OxyArea(OxyAreaSettings settings, OxyChartPainter owner)
        {
            area_settings = settings;
            Tag = area_settings.Tag;
            this.owner = owner;

            candles_in_run = area_settings.candles_in_run;
            empty_gap = area_settings.empty_gap;

            plot_model = new PlotModel()
            {
                PlotAreaBorderThickness = new OxyThickness(0),
                TextColor = OxyColors.AliceBlue,
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };

            plot_view = new PlotView()
            {
                Background = area_settings.Brush_background,
            };

            candle_stick_series = new CandleStickSeries()
            {
                IncreasingColor = OxyColor.FromArgb(255, 55, 219, 186),
                DecreasingColor = OxyColor.FromArgb(255, 235, 96, 47),
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };

            annotation_price = new CustomTextAnnotation()
            {
                Layer = AnnotationLayer.AboveSeries,
                ClipByXAxis = false,
                ClipByYAxis = false,
                Background = OxyColor.Parse("#FF5500"),
                TextColor = OxyColors.AliceBlue,
                Stroke = OxyColor.Parse("#FF5500"),
                TextHorizontalAlignment = HorizontalAlignment.Right,
                TextVerticalAlignment = VerticalAlignment.Middle,
                Padding = new OxyThickness(2),
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed,
                Tag = "annotation_price"
            };


            annotation_date_time = new CustomTextAnnotation()
            {
                Layer = AnnotationLayer.AboveSeries,
                ClipByXAxis = false,
                ClipByYAxis = true,
                Background = OxyColor.Parse("#FF5500"),
                TextColor = OxyColors.AliceBlue,
                Stroke = OxyColor.Parse("#FF5500"),
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Bottom,
                Padding = new OxyThickness(2),
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed,
                Tag = "annotation_date_time"
            };


            date_time_axis_X = new DateTimeAxis()
            {
                TicklineColor = area_settings.TicklineColor,
                MajorGridlineColor = area_settings.MajorGridlineColor,
                MajorGridlineStyle = area_settings.MajorGridlineStyle,
                MajorGridlineThickness = area_settings.MajorGridlineThickness,
                MinorGridlineColor = area_settings.MinorGridlineColor,
                MinorGridlineStyle = area_settings.MinorGridlineStyle,
                MinorGridlineThickness = area_settings.MinorGridlineThickness,
                Key = "DateTime",
                IsAxisVisible = false,
                Position = AxisPosition.Bottom,
                EdgeRenderingMode = area_settings.EdgeRenderingMode,
            };


            linear_axis_Y = new LinearAxis()
            {
                TicklineColor = area_settings.TicklineColor,
                MajorGridlineColor = area_settings.MajorGridlineColor,
                MajorGridlineStyle = area_settings.MajorGridlineStyle,
                MajorGridlineThickness = area_settings.MajorGridlineThickness,
                MinorGridlineColor = area_settings.MinorGridlineColor,
                MinorGridlineStyle = area_settings.MinorGridlineStyle,
                MinorGridlineThickness = area_settings.MinorGridlineThickness,
                IsAxisVisible = false,
                Layer = AxisLayer.BelowSeries,
                Position = AxisPosition.Right,
                EdgeRenderingMode = area_settings.EdgeRenderingMode,
            };

            
            if (settings.cursor_Y_is_active)
            {
                cursor_Y = new LineAnnotation()
                {
                    Type = LineAnnotationType.Vertical,
                    Color = area_settings.CursorColor,
                    Selectable = false,
                    ClipByYAxis = false,
                    X = double.MinValue,
                    StrokeThickness = 1,
                    Layer = AnnotationLayer.BelowSeries,
                    EdgeRenderingMode = EdgeRenderingMode.Automatic,
                     
                };
            }

            if (settings.cursor_X_is_active)
            {
                cursor_X = new LineAnnotation()
                {
                    Type = LineAnnotationType.Horizontal,
                    Color = area_settings.CursorColor,
                    ClipByXAxis = false,
                    Selectable = false,
                    Y = double.MinValue,
                    StrokeThickness = 1,
                    Layer = AnnotationLayer.BelowSeries,
                    EdgeRenderingMode = EdgeRenderingMode.PreferSharpness
                };
            }

            plot_model.Annotations.Add(annotation_price);
            plot_model.Annotations.Add(annotation_date_time);


            plot_model.Axes.Add(date_time_axis_X);
            plot_model.Axes.Add(linear_axis_Y);

            if (area_settings.cursor_X_is_active)
            {
                plot_model.Annotations.Add(cursor_X);
            }

            if (area_settings.cursor_Y_is_active)
            {
                plot_model.Annotations.Add(cursor_Y);
            }

            plot_view.Model = plot_model;
        }



        public PlotView GetViewUI()
        {
            return plot_view;
        }

        public virtual void Calculate(TimeSpan time_frame_span, TimeFrame time_frame)
        {

        }


        public void BuildIndicatorSeries(IndicatorSeria indi_seria, List<decimal> data_points, TimeSpan time_frame_span)
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

            var main_chart = (CandleStickArea)all_areas.Find(x => (string)x.Tag == "Prime");

            if (main_chart.axis_Y_type == "linear" || (string)Tag != "Prime")
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

                        ConcurrentBag<DataPoint> points = new ConcurrentBag<DataPoint>();

                        Parallel.For(0, indi_seria.DataPoints.Count, i =>
                        {
                            double last_point = (double)indi_seria.DataPoints[i];

                            if (last_point == 0)
                                last_point = double.NaN;

                            points.Add(new DataPoint(items_oxy_candles[i].X, last_point));
                        });

                        indi_seria.IndicatorHistogramPoints = points.AsParallel().OrderBy(x => x.X).ToList();
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

                    lock (linear_bar_locker)
                    {
                        if (linear_bar_series_list.Exists(x => (string)x.Tag == indi_seria.SeriaName))
                            linear_bar_series_list.Remove(linear_bar_series_list.Find(x => (string)x.Tag == indi_seria.SeriaName));

                        linear_bar_series_list.Add(linear_bar_seria);
                    }
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

                        ConcurrentBag<DataPoint> points = new ConcurrentBag<DataPoint>();

                        Parallel.For(0, indi_seria.DataPoints.Count, i =>
                        {
                            double last_point = (double)indi_seria.DataPoints[i];

                            if (last_point == 0)
                                last_point = double.NaN;

                                points.Add(new DataPoint(items_oxy_candles[i].X, last_point));

                        });

                        indi_seria.IndicatorPoints = points.AsParallel().OrderBy(x => x.X).ToList();
                    }


                    LineSeries line_seria = new LineSeries()
                    {
                        StrokeThickness = 1,
                        LineStyle = LineStyle.Solid,
                        Color = indi_seria.OxyColor,
                        Tag = indi_seria.SeriaName
                    };

                    line_seria.Points.AddRange(indi_seria.IndicatorPoints);

                    lock (lines_locker)
                    {
                        if (lines_series_list.Exists(x => (string)x.Tag == indi_seria.SeriaName))
                            lines_series_list.Remove(lines_series_list.Find(x => (string)x.Tag == indi_seria.SeriaName));

                        lines_series_list.Add(line_seria);
                    }
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

                        ConcurrentBag<ScatterPoint> points = new ConcurrentBag<ScatterPoint>();

                        Parallel.For(0, indi_seria.DataPoints.Count, i =>
                        {
                            double last_point = (double)indi_seria.DataPoints[i];

                            if (last_point == 0)
                                last_point = double.NaN;

                            points.Add(new ScatterPoint(items_oxy_candles[i].X, last_point));
                        });

                        indi_seria.IndicatorScatterPoints = points.AsParallel().OrderBy(x => x.X).ToList();

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

                    lock (scatter_locker)
                    {
                        if (scatter_series_list.Exists(x => (string)x.Tag == indi_seria.SeriaName))
                            scatter_series_list.Remove(scatter_series_list.Find(x => (string)x.Tag == indi_seria.SeriaName));

                        scatter_series_list.Add(scatter_seria);
                    }
                }
            }

            if (main_chart.axis_Y_type == "percent" && (string)Tag == "Prime")
            {

            }
        }

        public List<double> GetHighLow(bool isPrime, double start, double end)
        {
            List<double> values = new List<double>();


            if (isPrime)
            {
                if (my_candles != null && my_candles.Count > 0)
                {
                    values.AddRange(items_oxy_candles.AsParallel().Where(x => x.X >= start && x.X <= end).Select(x => x.High));
                    values.AddRange(items_oxy_candles.AsParallel().Where(x => x.X >= start && x.X <= end).Select(x => x.Low));

                    if (values.Count == 0)
                        values.AddRange(items_oxy_candles.Select(x => x.X));
                }
            }

            lock (scatter_locker)
            {
                if (scatter_series_list.Count > 0)
                {
                    foreach (var scatter in scatter_series_list)
                    {
                        values.AddRange(scatter.Points.AsParallel().Where(x => x.X >= start && x.X <= end).Select(x => x.Y));
                    }
                }
            }

            lock (lines_locker)
            {
                if (lines_series_list.Count > 0)
                {
                    foreach (var lines_series in lines_series_list)
                    {
                        values.AddRange(lines_series.Points.AsParallel().Where(x => x.X >= start && x.X <= end).Select(x => x.Y));
                    }
                }
            }

            lock (area_locker)
            {
                if (area_seriies_list.Count > 0)
                {
                    foreach (var area_series in area_seriies_list)
                    {
                        values.AddRange(area_series.Points.AsParallel().Where(x => x.X >= start && x.X <= end).Select(x => x.Y));
                    }
                }
            }

            lock (histogram_locker)
            {
                if (histogram_series_list.Count > 0)
                {
                    foreach (var his_series in histogram_series_list)
                    {
                        values.AddRange(his_series.Items.AsParallel().Where(x => x.RangeCenter >= start && x.RangeCenter <= end).Select(x => x.Value));
                    }
                }
            }

            lock (linear_bar_locker)
            {
                if (linear_bar_series_list.Count > 0)
                {
                    foreach (var bar_series in linear_bar_series_list)
                    {
                        values.AddRange(bar_series.Points.AsParallel().Where(x => x.X >= start && x.X <= end).Select(x => x.Y));
                    }
                }
            }

            if (values.Count == 0)
                return new List<double>() { 0, 0 };


            return new List<double>() { values.Min(), values.Max() };
        }

        public void SetAllChartByScreenPosition()
        {
            List<OxyArea> areas_to_move = new List<OxyArea>();

            if (this is CandleStickArea)
                areas_to_move = all_areas.Where(x => x is IndicatorArea).ToList();

            if (this is IndicatorArea || this is ScrollBarArea)
                areas_to_move = all_areas.Where(x => x is IndicatorArea || x is CandleStickArea).ToList();

            if (this is ScrollBarArea)
            {
                foreach (var area in areas_to_move)
                {
                    if ((area.plot_model.Axes[0].ActualMinimum == ((ScrollBarArea)this).screen_viewer_polygon.Points[0].X &&
                        area.plot_model.Axes[0].ActualMaximum == ((ScrollBarArea)this).screen_viewer_polygon.Points[1].X) || area == this)
                    {
                        continue;
                    }

                    Action action_zoom_main_chart_by_screen_position = () =>
                    {
                        double X_min_value = ((ScrollBarArea)this).screen_viewer_polygon.Points[0].X;
                        double X_max_value = ((ScrollBarArea)this).screen_viewer_polygon.Points[1].X;

                        if (X_min_value == 0 && X_max_value == 0)
                            return;

                        area.date_time_axis_X.Zoom(X_min_value, X_max_value);

                        List<double> max_min = new List<double>();

                        if (area is CandleStickArea)
                            max_min = area.GetHighLow(true, X_min_value, X_max_value);
                        else
                            max_min = area.GetHighLow(false, X_min_value, X_max_value);

                        area.linear_axis_Y.Zoom(max_min[0], max_min[1]);

                        area.plot_model.InvalidatePlot(true);
                    };

                    area.plot_view.Dispatcher.Invoke(action_zoom_main_chart_by_screen_position);
                }

                return;
            }


            foreach (var area in areas_to_move)
            {
                if ((area.plot_model.Axes[0].ActualMinimum == plot_model.Axes[0].ActualMinimum &&
                    area.plot_model.Axes[0].ActualMaximum == plot_model.Axes[0].ActualMaximum) || area == this)
                {
                    continue;
                }

                Action action_zoom_main_chart_by_screen_position = () =>
                {
                    double X_min_value = plot_model.Axes[0].ActualMinimum;
                    double X_max_value = plot_model.Axes[0].ActualMaximum;

                    area.date_time_axis_X.Zoom(X_min_value, X_max_value);

                    List<double> max_min = new List<double>();

                    if (area is CandleStickArea)
                        max_min = area.GetHighLow(true, X_min_value, X_max_value);
                    else
                        max_min = area.GetHighLow(false, X_min_value, X_max_value);

                    area.linear_axis_Y.Zoom(max_min[0], max_min[1]);

                    area.plot_model.InvalidatePlot(true);
                };

                area.plot_view.Dispatcher.Invoke(action_zoom_main_chart_by_screen_position);
            }



            var scroll_area = all_areas.Find(x => x.Tag == (object)"ScrollChart");

            Action action_moove_box = () =>
            {
                    ((ScrollBarArea)scroll_area).Calculate(time_frame_span, time_frame);

                    scroll_area.plot_model.InvalidatePlot(true);
            };

            scroll_area.plot_view.Dispatcher.Invoke(action_moove_box);
        }


        public void Redraw()
        {
            lock (redraw_locker)
            {
                Action action = () =>
                {

                    plot_model.Series.Clear();

                    if (this is CandleStickArea)
                    {
                        plot_model.Series.Add(candle_stick_series);
                    }

                    foreach (var bar_seria in bar_series_list)
                    {
                        plot_model.Series.Add(bar_seria);
                    }

                    foreach (var volume_seria in volume_series_list)
                    {
                        plot_model.Series.Add(volume_seria);
                    }

                    foreach (var histogram_seria in histogram_series_list)
                    {
                        plot_model.Series.Add(histogram_seria);
                    }

                    foreach (var linear_bar_series in linear_bar_series_list)
                    {
                        plot_model.Series.Add(linear_bar_series);
                    }

                    foreach (var area_seria in area_seriies_list)
                    {
                        plot_model.Series.Add(area_seria);
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
                };

                plot_view.Dispatcher.Invoke(action);
            }
        }
    }
}
