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

        DataPoint last_point = DataPoint.Undefined;

        public static List<Candle> candles_copy = new List<Candle>();



        public LineSeries line_seria = new LineSeries();

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


            plot_model.MouseDown += Plot_model_scroll_chart_MouseDown;
            plot_model.MouseUp += Plot_model_scroll_chart_MouseUp;
            plot_model.MouseLeave += Plot_model_scroll_chart_MouseLeave;
            plot_model.MouseMove += Plot_model_scroll_chart_MouseMove;

            plot_model.Updated += Plot_model_Updated;
        }

        private void Plot_model_Updated(object sender, EventArgs e)
        {
            
        }

        private void Plot_model_scroll_chart_MouseMove(object sender, OxyMouseEventArgs e)
        {
            if (!scroll_chart_left_mouse_is_down || !owner.mediator.prime_chart.isFreeze)
                return;

            Action screen_rect_move = () =>
            {
                var thisPoint = date_time_axis_X.InverseTransform(e.Position.X, e.Position.Y, linear_axis_Y);
                double dx = thisPoint.X - last_point.X;


                top_value = plot_model.Axes[1].ActualMaximum;
                bottom_value = plot_model.Axes[1].ActualMinimum;

                List<DataPoint> new_points = new List<DataPoint>()
                {
                        new DataPoint(screen_viewer_polygon.Points[0].X + dx, top_value),
                        new DataPoint(screen_viewer_polygon.Points[1].X + dx, top_value),
                        new DataPoint(screen_viewer_polygon.Points[2].X + dx, bottom_value),
                        new DataPoint(screen_viewer_polygon.Points[3].X + dx, bottom_value)
                };


                screen_viewer_polygon.Points.Clear();
                screen_viewer_polygon.Points.AddRange(new_points);

                last_point = thisPoint;

                owner.mediator.RedrawScroll(false);
            };

            plot_view.Dispatcher.Invoke(screen_rect_move);

            e.Handled = true;
        }

        public void SetPrimeChartByScreenPoligon()
        {
            double first_candle_value = screen_viewer_polygon.Points[0].X;
            double last_candle_value = screen_viewer_polygon.Points[1].X;

            owner.mediator.ZoomPrime(first_candle_value, last_candle_value);
        }

        private void Plot_model_scroll_chart_MouseLeave(object sender, OxyMouseEventArgs e)
        {
            scroll_chart_left_mouse_is_down = false;
            scroll_mouse_down_value_X = 0;
            e.Handled = true;
        }

        private void Plot_model_scroll_chart_MouseUp(object sender, OxyMouseEventArgs e)
        {
            scroll_mouse_down_value_X = 0;
            scroll_chart_left_mouse_is_down = false;

            owner.mediator.ZoomIndicators(screen_viewer_polygon.Points[0].X, screen_viewer_polygon.Points[1].X);

            owner.mediator.ZoomPrime(screen_viewer_polygon.Points[0].X, screen_viewer_polygon.Points[1].X);


            e.Handled = true;
        }

        private void Plot_model_scroll_chart_MouseDown(object sender, OxyMouseDownEventArgs e)
        {
            Action action = () =>
            {
            };

            if (e.ChangedButton == OxyMouseButton.Left)
            {
                last_point = date_time_axis_X.InverseTransform(e.Position.X, e.Position.Y, linear_axis_Y);
                scroll_chart_left_mouse_is_down = true;
            }

            if (e.ChangedButton == OxyMouseButton.Right)
            {
                action = () =>
                {
                    if (owner.mediator.prime_chart.isFreeze == false)
                    {
                        owner.mediator.prime_chart.isFreeze = true;
                        plot_view.Background = area_settings.Brush_scroll_freeze_bacground;
                        screen_viewer_polygon.Selectable = true;

                    }
                    else
                    {
                        owner.mediator.prime_chart.isFreeze = false;
                        plot_view.Background = area_settings.Brush_scroll_bacground;
                        screen_viewer_polygon.Selectable = false;
                    }

                    owner.mediator.RedrawScroll( true); 
                };        
            }

            plot_view.Dispatcher.Invoke(action);

            e.Handled = true;
        }


        public override void Calculate(TimeSpan time_frame_span, TimeFrame time_frame)
        {
            this.time_frame = time_frame;
            this.time_frame_span = time_frame_span;

            for (int i = 0; i < actions_to_calculate.Count; i++)
            {
                Action action = new Action(() => { });

                bool result = actions_to_calculate.TryDequeue(out action);

                if (result)
                    plot_view.Dispatcher.Invoke(action);
            }

            if (scroll_chart_left_mouse_is_down)
            {
                return;
            }

            Action action_scroll = () =>
            {
                if (my_candles == null || my_candles.Count == 0)
                    return;

                candles_copy.Clear();
                candles_copy.AddRange(my_candles);

                lock (series_locker)
                {
                    var main_area = ((CandleStickArea)all_areas.Find(x => x is CandleStickArea));

                    var items = candles_copy.Select(x => new DataPoint(DateTimeAxis.ToDouble(x.TimeStart), (double)x.Close));

                    line_seria = new LineSeries()
                    {
                        Color = OxyColor.Parse("#FF5500"),
                        MarkerStrokeThickness = 1,
                        StrokeThickness = 1,
                    };

                    try
                    {
                        line_seria.Points.Clear();
                        line_seria.Points.AddRange(items);
                    }
                    catch { return; }

                    var X_start = DateTimeAxis.ToDouble(candles_copy.First().TimeStart);
                    var X_end = DateTimeAxis.ToDouble(candles_copy.Last().TimeStart);

                    date_time_axis_X.Zoom(X_start, X_end);

                    if (plot_model == null)
                        return;

                    if (plot_model.Annotations.ToList().Exists(x => x.Tag == (object)"screen_viewer_polygon"))
                        plot_model.Annotations.Remove(plot_model.Annotations.First(x => (object)x.Tag == "screen_viewer_polygon"));


                    if (!owner.mediator.prime_chart.isFreeze)
                    {
                        double first_candle_X = main_area.plot_model.Axes[0].ActualMinimum;
                        double last_candle_X = main_area.plot_model.Axes[0].ActualMaximum;

                        top_value = plot_model.Axes[1].ActualMaximum;
                        bottom_value = plot_model.Axes[1].ActualMinimum;

                        List<DataPoint> new_points = new List<DataPoint>()
                    {
                        new DataPoint(first_candle_X, top_value),
                        new DataPoint(last_candle_X, top_value),
                        new DataPoint(last_candle_X, bottom_value),
                        new DataPoint(first_candle_X, bottom_value)
                    };


                        screen_viewer_polygon.Points.Clear();
                        screen_viewer_polygon.Points.AddRange(new_points);
                    }
                    else
                    {
                        double first_candle_X = main_area.plot_model.Axes[0].ActualMinimum;
                        double last_candle_X = main_area.plot_model.Axes[0].ActualMaximum;

                        top_value = plot_model.Axes[1].ActualMaximum;
                        bottom_value = plot_model.Axes[1].ActualMinimum;

                        List<DataPoint> new_points = new List<DataPoint>()
                    {
                        new DataPoint(screen_viewer_polygon.Points[0].X, top_value),
                        new DataPoint(screen_viewer_polygon.Points[1].X, top_value),
                        new DataPoint(screen_viewer_polygon.Points[2].X, bottom_value),
                        new DataPoint(screen_viewer_polygon.Points[3].X, bottom_value)
                    };


                        screen_viewer_polygon.Points.Clear();
                        screen_viewer_polygon.Points.AddRange(new_points);
                    }

                    plot_model.Annotations.Add(screen_viewer_polygon);
                }

            };

            plot_view.Dispatcher.Invoke(action_scroll);
        }

        public override void Redraw()
        {

            Action action = () =>
            {
                lock (series_locker)
                {
                    plot_model.Series.Clear();

                    plot_model.Series.Add(line_seria);

                    plot_view.InvalidatePlot(true);
                }
            };

            plot_view.Dispatcher.Invoke(action);
        }

        public override void Dispose()
        {
            line_seria = new LineSeries();
            candles_copy = new List<Candle>();
            my_candles = new List<Candle>();
            items_oxy_candles = new List<HighLowItem>();
        }  
    }
}
