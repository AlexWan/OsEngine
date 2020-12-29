using OsEngine.Entity;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.SkiaSharp.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace OsEngine.Charts.CandleChart.OxyAreas
{
    public class ScrollBarArea : OxyArea
    {
        public PolygonAnnotation screen_viewer_polygon;
        
        public bool scroll_chart_left_mouse_is_down = false;
        public double scroll_mouse_down_value_X = 0;

        public double top_value = 0;
        public double bottom_value = 0;

        public object locker_drawler = new object();

        public ScrollBarArea(OxyAreaSettings settings, List<OxyArea> all_areas, OxyChartPainter owner) : base(settings, owner)
        {
            area_settings = settings;
            this.all_areas = all_areas;

            plot_view.DefaultTrackerTemplate = new System.Windows.Controls.ControlTemplate();

            screen_viewer_polygon = new PolygonAnnotation()
            {
                Layer = AnnotationLayer.AboveSeries,
                Selectable = true,
                Fill = area_settings.ScrollBarColor,
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed,
                Tag = "screen_viewer_polygon",
            };

            screen_viewer_polygon.Points.AddRange(new List<OxyPlot.DataPoint>()
            {
                new OxyPlot.DataPoint(0, 0),
                new OxyPlot.DataPoint(0, 0),
                new OxyPlot.DataPoint(0, 0),
                new OxyPlot.DataPoint(0, 0),
            });

            plot_view.MouseRightButtonDown += Plot_view_MouseRightButtonDown;

            plot_model.MouseDown += Plot_model_scroll_chart_MouseDown;
            plot_model.MouseUp += Plot_model_scroll_chart_MouseUp;
            plot_model.MouseLeave += Plot_model_scroll_chart_MouseLeave;
            plot_model.MouseMove += Plot_model_scroll_chart_MouseMove;
        }

        
        private void Plot_model_scroll_chart_MouseMove(object sender, OxyMouseEventArgs e)
        {
            if (!scroll_chart_left_mouse_is_down)
                return;

            Action screen_rect_move = () =>
            {
                Axis yAxis = linear_axis_Y;
                DataPoint dataPoint_event = date_time_axis_X.InverseTransform(e.Position.X, e.Position.Y, yAxis);

                double delta_scroll = dataPoint_event.X - scroll_mouse_down_value_X;
                scroll_mouse_down_value_X = dataPoint_event.X;

                List<DataPoint> new_points = new List<DataPoint>();

                foreach (var point in screen_viewer_polygon.Points)
                {
                    new_points.Add(new OxyPlot.DataPoint(point.X + delta_scroll, point.Y));
                }

                screen_viewer_polygon.Points.Clear();
                screen_viewer_polygon.Points.AddRange(new_points);

                SetAllChartByScreenPoligon();

                plot_view.InvalidatePlot(true);
            };

            plot_view.Dispatcher.Invoke(screen_rect_move);

        }

        public void SetAllChartByScreenPoligon()
        {
           
            foreach (var area in all_areas.Where(x => x.Tag != (object)"ScrollChart"))
            {
                if (area.plot_model.Axes[0].ActualMinimum == plot_model.Axes[0].ActualMinimum &&
                   area.plot_model.Axes[0].ActualMaximum == plot_model.Axes[0].ActualMaximum)
                {
                    return;
                }

                Action action = () =>
                {
                    if (area is CandleStickArea)
                    {
                        var main_area = (CandleStickArea)area;
                        main_area.BuildCandleSeries(my_candles);
                        main_area.Calculate(owner.time_frame_span, owner.time_frame);
                    }

                    double first_candle_value = screen_viewer_polygon.Points[0].X;
                    double last_candle_value = screen_viewer_polygon.Points[1].X;

                    area.date_time_axis_X.Zoom(first_candle_value, last_candle_value);

                    List<double> max_min = new List<double>();

                    if (area.Tag == (object)"Prime")
                        max_min = area.GetHighLow(true, first_candle_value, last_candle_value);
                    else
                        max_min = area.GetHighLow(false, first_candle_value, last_candle_value);



                    area.linear_axis_Y.Zoom(max_min[0], max_min[1]);
                };

                plot_view.Dispatcher.Invoke(action);
            }
        }

        private void Plot_model_scroll_chart_MouseLeave(object sender, OxyMouseEventArgs e)
        {
            scroll_chart_left_mouse_is_down = false;
            scroll_mouse_down_value_X = 0;
        }

        private void Plot_model_scroll_chart_MouseUp(object sender, OxyMouseEventArgs e)
        {
            scroll_chart_left_mouse_is_down = false;
            scroll_mouse_down_value_X = 0;

            foreach (var area in all_areas.Where(x => (string)x.Tag != "ScrollChart"))
            {
                Action action = () =>
                {
                    area.plot_model.InvalidatePlot(true);
                };

                area.plot_view.Dispatcher.Invoke(action);
            }

        }

        private void Plot_model_scroll_chart_MouseDown(object sender, OxyMouseDownEventArgs e)
        {
            scroll_chart_left_mouse_is_down = true;

            Axis yAxis = linear_axis_Y;
            DataPoint dataPoint_event = date_time_axis_X.InverseTransform(e.Position.X, e.Position.Y, yAxis);

            scroll_mouse_down_value_X = dataPoint_event.X;
        }

        private void Plot_view_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            Action action_rb_down = () =>
            {
                if (all_areas.Find(x => (string)x.Tag == "Prime").isFreeze == false)
                {
                    all_areas.Find(x => (string)x.Tag == "Prime").isFreeze = true;
                    plot_view.Background = area_settings.Brush_scroll_freeze_bacground;
                    screen_viewer_polygon.Selectable = true;

                }
                else
                {
                    all_areas.Find(x => (string)x.Tag == "Prime").isFreeze = false;
                    plot_view.Background = area_settings.Brush_scroll_bacground; 
                    screen_viewer_polygon.Selectable = false;
                }

                plot_view.InvalidatePlot(true);
            };


            plot_view.Dispatcher.Invoke(action_rb_down);



        }


        public override void Calculate(TimeSpan time_frame_span, TimeFrame time_frame)
        {
            Action action_scroll = () =>
            {
                if (my_candles == null || my_candles.Count == 0)
                    return;

                var main_area = ((CandleStickArea)all_areas.Find(x => (string)x.Tag == "Prime"));


                var area_series_parallel = new LineSeries()
                {
                    Color = OxyColor.FromArgb(150, 255, 85, 0),
                    EdgeRenderingMode = EdgeRenderingMode.PreferSharpness
                };

                var items = my_candles.AsParallel().AsOrdered().Select(x => new DataPoint(DateTimeAxis.ToDouble(x.TimeStart), (double)x.Close));

                if (plot_model == null)
                    return;

                if (plot_model.Series.ToList().Exists(x => x.SeriesGroupName == "ScrollPoints"))
                    plot_model.Series.Remove(plot_model.Series.First(x => x.SeriesGroupName == "ScrollPoints"));

                area_series_parallel.Points.AddRange(items);
                area_series_parallel.SeriesGroupName = "ScrollPoints";

                plot_model.Series.Add(area_series_parallel);


                if (plot_model.Annotations.ToList().Exists(x => x.Tag == (object)"screen_viewer_polygon"))
                    plot_model.Annotations.Remove(plot_model.Annotations.First(x => (object)x.Tag == "screen_viewer_polygon"));

                top_value = plot_model.Axes[1].ActualMaximum;
                bottom_value = plot_model.Axes[1].ActualMinimum;

                double first_candle_X = main_area.plot_model.Axes[0].ActualMinimum;
                double last_candle_X = main_area.plot_model.Axes[0].ActualMaximum;

                List<DataPoint> new_points = new List<DataPoint>()
                {
                        new DataPoint(first_candle_X, top_value),
                        new DataPoint(last_candle_X, top_value),
                        new DataPoint(last_candle_X, bottom_value),
                        new DataPoint(first_candle_X, bottom_value)
                };

                screen_viewer_polygon.Points.Clear();
                screen_viewer_polygon.Points.AddRange(new_points);

                plot_model.Annotations.Add(screen_viewer_polygon);

                plot_view.InvalidatePlot(true);
            };

                plot_view.Dispatcher.Invoke(action_scroll);
        }

        public void Dispose()
        {

            scatter_series_list = new List<ScatterSeries>();
            lines_series_list = new List<LineSeries>();
            area_seriies_list = new List<AreaSeries>();
            bar_series_list = new List<BarSeries>();
            volume_series_list = new List<VolumeSeries>();
            histogram_series_list = new List<HistogramSeries>();
            linear_bar_series_list = new List<LinearBarSeries>();
            my_candles = new List<Candle>();
            items_oxy_candles = new List<HighLowItem>();
        }

        
    }
}
