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
    public abstract class OxyArea 
    {
        public string chart_name;
        public ConcurrentQueue<Action> actions_to_calculate = new ConcurrentQueue<Action>();

        public object Tag = new object();
        public List<OxyArea> all_areas;
        public List<string> indicator_series_list;
        public string bot_name;
        public int bot_tab;

        public PlotView plot_view;
        public PlotModel plot_model;
        public OxyAreaSettings area_settings;

        public DateTimeAxis date_time_axis_X;
        public LinearAxis linear_axis_Y;

        public LineAnnotation cursor_Y;
        public LineAnnotation cursor_X;

        public CustomTextAnnotation annotation_price;
        public CustomTextAnnotation annotation_date_time;
        public CustomTextAnnotation drawed_name;


        public ScreenPoint mouse_screen_point = new ScreenPoint();
        public System.Windows.Input.MouseEventArgs mouse_event_args;
        public static List<Candle> my_candles = new List<Candle>();
        public static List<HighLowItem> items_oxy_candles = new List<HighLowItem>();

        public int candles_in_run;
        public int empty_gap;

        public OxyChartPainter owner;

        public TimeSpan time_frame_span;
        public TimeFrame time_frame;

        public event Action<OxyArea> Redrawed;
        public event Action<OxyArea> Updated;

        public object series_locker = new object();

        public void SendRedrawed(OxyArea oxy_area)
        {
            Redrawed?.Invoke(oxy_area);
        }

        public void SendUpdated(OxyArea oxy_area)
        {
            Updated?.Invoke(oxy_area);
        }

        public OxyArea(OxyAreaSettings settings, OxyChartPainter owner)
        {
            this.chart_name = owner.chart_name;

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

            drawed_name = new CustomTextAnnotation()
            {
                Text = (string)Tag,
                TextColor = OxyColor.FromArgb(255, 98, 103, 113),
                Background = OxyColors.Transparent,
                Stroke = OxyColors.Transparent,
                Tag = "drawed_name",
                Layer = OxyPlot.Annotations.AnnotationLayer.AboveSeries,
                TextHorizontalAlignment = HorizontalAlignment.Left,
                TextVerticalAlignment = VerticalAlignment.Middle,
                FontSize = 24,
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
                AxislineStyle = area_settings.AxislineStyle,
                AxislineThickness = 1,
                AxislineColor = area_settings.AxislineColor,
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
                AxislineStyle = area_settings.AxislineStyle,
                AxislineThickness = 1,
                AxislineColor = area_settings.AxislineColor,
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
                    StrokeThickness = 0.5,
                    Layer = AnnotationLayer.BelowSeries,
                    EdgeRenderingMode = EdgeRenderingMode.Automatic,
                    LineStyle = LineStyle.LongDash
                     
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
                    StrokeThickness = 0.5,
                    Layer = AnnotationLayer.BelowSeries,
                    EdgeRenderingMode = EdgeRenderingMode.Automatic,
                    LineStyle = LineStyle.LongDash
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

        public virtual void BuildIndicatorSeries(IndicatorSeria indi_seria, List<decimal> data_points, TimeSpan time_frame_span)
        {
            
        }

        public virtual void CreateIndicatorLegend()
        {

        }

        public virtual List<double> GetHighLow(bool isPrime, double start, double end)
        {
            return new List<double>() { 0, 0 };
        }

        public virtual void Update()
        {

        }

        public virtual void Redraw()
        {

        }

        public virtual void Dispose()
        {
            List<Annotation> point_annotations = new List<Annotation>();

            foreach (var annotation in plot_model.Annotations)
            {
                if (annotation.Tag == (object)"point")
                    point_annotations.Add(annotation);
            }

            foreach (var ann in point_annotations)
                plot_model.Annotations.Remove(ann);

        }
    }
}
