using CustomAnnotations;
using OsEngine.Entity;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace OsEngine.Charts.CandleChart.OxyAreas
{
    public class ControlPanelArea : OxyArea
    {
        private bool is_first_start = true;

        public CustomTextAnnotation speed_lable;
        public CustomTextAnnotation minus_button;
        public CustomTextAnnotation plus_button;
        public CustomTextAnnotation speed_state_lable;

        public CustomTextAnnotation percent_button;
        public CustomTextAnnotation menu_button;

        public ScreenPoint pos_persent;

        public ControlPanelArea(OxyAreaSettings settings, List<OxyArea> all_areas, OxyChartPainter owner) : base(settings, owner)
        {
            if (owner.start_program == StartProgram.IsTester)
            {
                speed_lable = new CustomTextAnnotation()
                {
                    Background = OxyColors.Transparent,
                    Stroke = OxyColors.Transparent,
                    StrokeThickness = 0,
                    Text = "Speed: ",
                    TextPosition = new ScreenPoint(20, 12),
                    TextVerticalAlignment = VerticalAlignment.Bottom,
                    TextHorizontalAlignment = HorizontalAlignment.Left,
                    Layer = AnnotationLayer.AboveSeries,

                };

                minus_button = new CustomTextAnnotation()
                {
                    Background = OxyColors.Transparent,
                    Stroke = OxyColor.Parse("#FF5500"),
                    StrokeThickness = 1,
                    Text = "-",
                    TextPosition = new ScreenPoint(65, 12),
                    TextVerticalAlignment = VerticalAlignment.Bottom,
                    TextHorizontalAlignment = HorizontalAlignment.Left,
                    Layer = AnnotationLayer.AboveSeries,
                    Padding = new OxyThickness(4.5, -1, 4.5, 1),
                };

                plus_button = new CustomTextAnnotation()
                {
                    Background = OxyColors.Transparent,
                    Stroke = OxyColor.Parse("#FF5500"),
                    StrokeThickness = 1,
                    Text = "+",
                    TextPosition = new ScreenPoint(80, 12),
                    TextVerticalAlignment = VerticalAlignment.Bottom,
                    TextHorizontalAlignment = HorizontalAlignment.Left,
                    Layer = AnnotationLayer.AboveSeries,
                    Padding = new OxyThickness(3, -1, 2.5, 1),
                };

                speed_state_lable = new CustomTextAnnotation()
                {
                    Background = OxyColors.Transparent,
                    Stroke = OxyColors.Transparent,
                    StrokeThickness = 0,
                    Text = "x1",
                    TextPosition = new ScreenPoint(100, 12),
                    TextVerticalAlignment = VerticalAlignment.Bottom,
                    TextHorizontalAlignment = HorizontalAlignment.Left,
                    Layer = AnnotationLayer.AboveSeries,
                };
            }

            percent_button = new CustomTextAnnotation()
            {
                Background = OxyColors.Transparent,
                Stroke = OxyColor.Parse("#FF5500"),
                StrokeThickness = 1,
                Text = "%",
                TextVerticalAlignment = VerticalAlignment.Bottom,
                TextHorizontalAlignment = HorizontalAlignment.Left,
                Layer = AnnotationLayer.AboveSeries,
                Padding = new OxyThickness(3, 0, 3, 1),

            };

            menu_button = new CustomTextAnnotation()
            {
                Background = OxyColors.Transparent,
                Stroke = OxyColor.Parse("#FF5500"),
                StrokeThickness = 1,
                Text = "MENU",
                TextVerticalAlignment = VerticalAlignment.Bottom,
                TextHorizontalAlignment = HorizontalAlignment.Left,
                Layer = AnnotationLayer.AboveSeries,
                Padding = new OxyThickness(3, 0, 3, 1),

            };


            if (owner.start_program == StartProgram.IsTester)
            {
                plot_model.Annotations.Add(speed_lable);
                plot_model.Annotations.Add(minus_button);
                plot_model.Annotations.Add(plus_button);
                plot_model.Annotations.Add(speed_state_lable);
            }

            plot_model.Annotations.Add(percent_button);
            plot_model.Annotations.Add(menu_button);


            area_settings = settings;
            this.all_areas = all_areas;

            if (owner.start_program == StartProgram.IsTester)
            {
                minus_button.MouseDown += Minus_button_MouseDown;
                minus_button.MouseUp += Minus_button_MouseUp;

                plus_button.MouseDown += Plus_button_MouseDown;
                plus_button.MouseUp += Plus_button_MouseUp;
            }

            percent_button.MouseDown += Percent_button_MouseDown;
            menu_button.MouseDown += Menu_button_MouseDown;
            menu_button.MouseUp += Menu_button_MouseUp;

            plot_model.Updated += Plot_model_Updated;
        }

        private void Menu_button_MouseUp(object sender, OxyMouseEventArgs e)
        {
            if (sender != null)
            {
                menu_button.Background = OxyColors.Transparent;
                owner.MainChartMouseButtonClick(ChartClickType.RightButton);
            }
            plot_view.InvalidatePlot(true);

            e.Handled = true;
        }

        private void Menu_button_MouseDown(object sender, OxyMouseDownEventArgs e)
        {
            if (sender != null)
            {
                menu_button.Background = OxyColor.Parse("#FF5500");
            }

            plot_view.InvalidatePlot(true);

            e.Handled = true;
        }

        private void Plot_model_Updated(object sender, EventArgs e)
        {
            UpdateButtonsPos();
        }

        private void Plus_button_MouseUp(object sender, OxyMouseEventArgs e)
        {
            if (sender != null)
            {
                plus_button.Background = OxyColors.Transparent;

                plot_view.InvalidatePlot(true);
            }

            e.Handled = true;
        }

        private void Minus_button_MouseUp(object sender, OxyMouseEventArgs e)
        {
            if (sender != null)
            {
                minus_button.Background = OxyColors.Transparent;

                plot_view.InvalidatePlot(true);
            }

            e.Handled = true;
        }

        private void Percent_button_MouseDown(object sender, OxyMouseDownEventArgs e)
        {
            if (sender != null)
            {
                var main_chart = (CandleStickArea)all_areas.Find(x => (string)x.Tag == "Prime");

                if (main_chart.axis_Y_type == "linear")
                {
                    percent_button.Background = OxyColor.Parse("#FF5500"); 
                    main_chart.axis_Y_type = "percent";

                    if (!main_chart.isFreeze)
                        main_chart.Dispose();
                    else
                    {
                        main_chart.scatter_series_list = new List<ScatterSeries>();
                        main_chart.lines_series_list = new List<LineSeries>();
                        main_chart.area_seriies_list = new List<AreaSeries>();
                        main_chart.bar_series_list = new List<BarSeries>();
                        main_chart.volume_series_list = new List<VolumeSeries>();
                        main_chart.histogram_series_list = new List<HistogramSeries>();
                        main_chart.linear_bar_series_list = new List<LinearBarSeries>();
                    }
                }

                else if(main_chart.axis_Y_type == "percent")
                {
                    percent_button.Background = OxyColors.Transparent;
                    main_chart.axis_Y_type = "linear";

                    if (!main_chart.isFreeze)
                        main_chart.Dispose();
                    else
                    {
                        main_chart.Dispose();
                    }
                }

                plot_view.InvalidatePlot(true);
            }

            e.Handled = true;
        }

        private void Plus_button_MouseDown(object sender, OxyMouseDownEventArgs e)
        {
            if (sender != null)
            {
                plus_button.Background = OxyColor.Parse("#FF5500");

                var main_chart = (CandleStickArea)all_areas.Find(x => x.Tag == (object)"Prime");

                if (main_chart.factor_selector < 16)
                    main_chart.factor_selector += 1;

                main_chart.factor = main_chart.factor_array[main_chart.factor_selector];

                speed_state_lable.Text = "x" + main_chart.factor_array[main_chart.factor_selector].ToString();

                main_chart.plot_view.InvalidatePlot(true);

                plot_view.InvalidatePlot(true);
            }

            e.Handled = true;
        }

        private void Minus_button_MouseDown(object sender, OxyMouseDownEventArgs e)
        {
            if (sender != null)
            {
                minus_button.Background = OxyColor.Parse("#FF5500");

                var main_chart = (CandleStickArea)all_areas.Find(x => x.Tag == (object)"Prime");

                if (main_chart.factor_selector > 0)
                    main_chart.factor_selector -= 1;

                main_chart.factor = main_chart.factor_array[main_chart.factor_selector];

                speed_state_lable.Text = "x" + main_chart.factor_array[main_chart.factor_selector].ToString();

                main_chart.plot_view.InvalidatePlot(true);

                plot_view.InvalidatePlot(true);
            }

            e.Handled = true;
        }

        public void UpdateButtonsPos()
        {

            speed_lable.TextPosition = new ScreenPoint(20, plot_view.ActualHeight - 12);
            minus_button.TextPosition = new ScreenPoint(65, plot_view.ActualHeight - 12);
            plus_button.TextPosition = new ScreenPoint(80, plot_view.ActualHeight - 12);
            speed_state_lable.TextPosition = new ScreenPoint(100, plot_view.ActualHeight - 12);

            percent_button.TextPosition = new ScreenPoint(plot_view.ActualWidth - 25, plot_view.ActualHeight - 12);
            menu_button.TextPosition = new ScreenPoint(plot_view.ActualWidth - 80, plot_view.ActualHeight - 12);
        }

        public override void Calculate(TimeSpan time_frame_span, TimeFrame time_frame)
        {
            UpdateButtonsPos();
            plot_view.InvalidatePlot(true);
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
