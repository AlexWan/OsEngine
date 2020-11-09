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
        private bool already_cursor_subscribed = false;
        public IndicatorArea(OxyAreaSettings settings, List<OxyArea> all_areas, object tag, OxyChartPainter owner) : base(settings, owner)
        {
            area_settings = settings;
            this.all_areas = all_areas;
            Tag = tag;

            date_time_axis_X.IsAxisVisible = area_settings.X_Axies_is_visible;
            linear_axis_Y.IsAxisVisible = area_settings.Y_Axies_is_visible;
        }

        public void Main_area_cursor_Y_Changed(double X, double Y)
        {
            Action redraw_action = () =>
            {
                cursor_Y.X = X;
                cursor_Y.Y = Y;

                plot_model.InvalidatePlot(true);
            };

            plot_view.Dispatcher.Invoke(redraw_action);
        }


        public override void Calculate(TimeSpan time_frame_span, TimeFrame time_frame)
        {
            Action redraw_action = () =>
            {
                var main_chart = ((CandleStickArea)all_areas.Find(x => (string)x.Tag == "Prime"));

                var delta_seconds = time_frame_span.TotalSeconds;
                int empty_gap = area_settings.empty_gap;
                int candles_in_run = area_settings.candles_in_run;

                if (main_chart.my_candles == null || main_chart.my_candles.Count == 0)
                    return;

                if (main_chart.plot_model == null || main_chart.plot_view == null)
                    return;

                if (all_areas.Exists(x => x.Tag == (object)"Prime"))
                {
                    double plot_margin = main_chart.plot_model.ActualPlotMargins.Right;

                    plot_model.PlotMargins = new OxyThickness(plot_model.PlotMargins.Left, plot_model.PlotMargins.Top, plot_margin, plot_model.PlotMargins.Bottom);
                }
            };

            plot_view.Dispatcher.Invoke(redraw_action);
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
