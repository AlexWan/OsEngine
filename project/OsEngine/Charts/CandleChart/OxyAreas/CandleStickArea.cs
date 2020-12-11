using CustomAnnotations;
using OsEngine.Entity;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace OsEngine.Charts.CandleChart.OxyAreas
{
    public class CandleStickArea : OxyArea
    {
        enum LastClickButton
        {
            Right,
            Left,
            Middle,
            None
        }

        OxyImage image;

        private int digits = 2;
        private double last_Y = 0;
        private double last_X = 0;
        private decimal last_price = 0;
        private LastClickButton last_click_button;

        public bool mouse_on_main_chart = false;
        public bool has_candles = false;

        public double[] factor_array = new double[] { 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024};
        public int factor_selector = 6;
        public double factor = 1;

        public string axis_Y_type = "linear";

        private bool mouse_move = false;

        public CandleStickArea(OxyAreaSettings settings, List<OxyArea> all_areas, OxyChartPainter owner) : base(settings, owner)
        {
            area_settings = settings;
            this.all_areas = all_areas;

            date_time_axis_X.MinimumMargin = 25;
            date_time_axis_X.MaximumMargin = 25;

            var assembly = this.GetType().GetTypeInfo().Assembly;
            using (var stream = assembly.GetManifestResourceStream("OsEngine.Resources.OsEngine_logo_transpared.png"))
            {
                image = new OxyImage(stream);
            }

            plot_model.Annotations.Add(new ImageAnnotation
            {
                ImageSource = image,
                Opacity = 1,
                Interpolate = false,
                X = new PlotLength(0.5, PlotLengthUnit.RelativeToPlotArea),
                Y = new PlotLength(0.5, PlotLengthUnit.RelativeToPlotArea),
                Width = new PlotLength(0.5, PlotLengthUnit.RelativeToPlotArea),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Middle,
                Layer = AnnotationLayer.BelowSeries
            });

            plot_view.LayoutUpdated += Plot_view_LayoutUpdated;
            plot_view.MouseLeave += Plot_view_main_chart_MouseLeave;
            plot_view.MouseMove += Plot_view_main_chart_MouseMove;
            plot_view.MouseWheel += Plot_view_main_chart_MouseWheel;


            plot_model.Updated += Plot_model_Updated;
        }

        private void Plot_model_Updated(object sender, EventArgs e)
        {
            SetAllChartByScreenPosition();
        }

        private void Plot_view_main_chart_MouseLeave(object sender, MouseEventArgs e)
        {
            mouse_on_main_chart = false;

            if (has_candles == false)
                return;

            Action action = () =>
            {

                ScreenPoint point = new ScreenPoint(e.GetPosition(plot_view).X, e.GetPosition(plot_view).Y);

                var args = new HitTestArguments(point, 0);

                Axis yAxis = plot_view.ActualModel.Axes[1];
                DataPoint dataPoint = plot_view.ActualModel.DefaultXAxis.InverseTransform(args.Point.X, args.Point.Y, yAxis);

                if (last_X != 0 && last_Y != 0)
                {
                    cursor_Y.X = last_X;
                    cursor_Y.Y = 0;
                    cursor_X.X = 0;
                    cursor_X.Y = last_Y;

                    foreach (var area in all_areas.Where(x => x is IndicatorArea))
                    {
                        Action action_cursor = () =>
                        {
                            area.cursor_Y.X = double.NaN;
                            area.cursor_Y.Y = cursor_Y.Y;

                            area.plot_model.InvalidatePlot(true);
                        };

                        area.plot_view.Dispatcher.Invoke(action_cursor);
                    }

                    annotation_price.TextPosition = new ScreenPoint(plot_view.ActualWidth - plot_model.Padding.Right, annotation_price.GetDataPointPosition(new OxyPlot.DataPoint(last_X, last_Y)).Y);
                    annotation_price.Text = last_Y.ToString("F" + digits.ToString());

                    annotation_date_time.TextPosition = new ScreenPoint(annotation_date_time.GetDataPointPosition(new DataPoint(last_X, last_Y)).X, plot_view.ActualHeight - plot_model.Padding.Bottom);
                    annotation_date_time.Text = DateTimeAxis.ToDateTime(last_X).ToString();
                }
                else
                {
                    cursor_Y.X = double.MinValue;
                    cursor_Y.Y = double.MinValue;
                    cursor_X.X = double.MinValue;
                    cursor_X.Y = double.MinValue;

                    foreach (var area in all_areas.Where(x => x is IndicatorArea))
                    {
                        Action action_cursor = () =>
                        {
                            area.cursor_Y.X = cursor_Y.X;
                            area.cursor_Y.Y = cursor_Y.Y;

                            area.plot_model.InvalidatePlot(true);
                        };

                        area.plot_view.Dispatcher.Invoke(action_cursor);
                    }
                }
                plot_view.InvalidatePlot(true);


                e.Handled = true;
            };

            plot_view.Dispatcher.Invoke(action);
        }

        private void Plot_view_LayoutUpdated(object sender, EventArgs e)
        {

        }

        private void Plot_view_main_chart_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            mouse_on_main_chart = true;

            if (has_candles == false)
                return;

            Action action = () =>
            {

                mouse_screen_point = new ScreenPoint(e.GetPosition(plot_view).X, e.GetPosition(plot_view).Y);
                mouse_event_args = e;

                Axis yAxis = linear_axis_Y;
                DataPoint dataPoint_event = date_time_axis_X.InverseTransform(e.GetPosition(plot_view).X, e.GetPosition(plot_view).Y, yAxis);

                cursor_Y.X = dataPoint_event.X;
                cursor_Y.Y = 0;
                cursor_X.X = 0;
                cursor_X.Y = dataPoint_event.Y;


                foreach (var area in all_areas.Where(x => x is IndicatorArea))
                {
                    Action action_cursor = () =>
                    {
                        area.cursor_Y.X = cursor_Y.X;
                        area.cursor_Y.Y = cursor_Y.Y;

                        area.plot_model.InvalidatePlot(true);
                    };

                    area.plot_view.Dispatcher.Invoke(action_cursor);
                }

                annotation_price.TextPosition = new ScreenPoint(plot_view.ActualWidth - plot_model.Padding.Right, e.GetPosition(plot_view).Y);
                annotation_price.Text = dataPoint_event.Y.ToString("F" + digits.ToString());

                annotation_date_time.TextPosition = new ScreenPoint(e.GetPosition(plot_view).X, plot_view.ActualHeight - plot_model.Padding.Bottom);
                annotation_date_time.Text = DateTimeAxis.ToDateTime(dataPoint_event.X).ToString();

                plot_view.InvalidatePlot(true);
            };

            plot_view.Dispatcher.Invoke(action);

            e.Handled = true;
        }

        private void Plot_view_main_chart_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            SetViewPoligonByMainChart();
        }

        private void SetViewPoligonByMainChart()
        {
            ScrollBarArea indicator_area = ((ScrollBarArea)all_areas.Find(x => (string)x.Tag == "ScrollChart"));

            Action action_move_polygon_viewer = () =>
            {
                double first_candle_X = plot_model.Axes[0].ActualMinimum;
                double last_candle_X = plot_model.Axes[0].ActualMaximum; 

                List<OxyPlot.DataPoint> new_points = new List<OxyPlot.DataPoint>()
                {
                    new DataPoint(first_candle_X, indicator_area.top_value),
                    new DataPoint(last_candle_X, indicator_area.top_value),
                    new DataPoint(last_candle_X, indicator_area.bottom_value),
                    new DataPoint(first_candle_X, indicator_area.bottom_value)
                };

                indicator_area.screen_viewer_polygon.Points.Clear();
                indicator_area.screen_viewer_polygon.Points.AddRange(new_points);
            };

            indicator_area.plot_view.Dispatcher.Invoke(action_move_polygon_viewer);
        }

        public void ProcessPositions(List<Position> deals)
        {
            if (deals == null || axis_Y_type == "percent")
                return;

            Action positions_action = () =>
            {

                if (scatter_series_list.Exists(x => (string)x.Tag == "open_long_deals_series"))
                    scatter_series_list.Remove(scatter_series_list.Find(x => (string)x.Tag == "open_long_deals_series"));

                var open_long_deals_series = new ScatterSeries()
                {
                    Tag = "open_long_deals_series",
                    MarkerType = MarkerType.Custom,
                    MarkerSize = 4,
                    MarkerStrokeThickness = 0.5,
                    MarkerStroke = OxyColors.AliceBlue,
                    EdgeRenderingMode = EdgeRenderingMode.Adaptive,
                    MarkerFill = OxyColor.FromArgb(255, 13, 255, 0),
                    
                    MarkerOutline = new[]
                                    {
                                    new ScreenPoint(0, 0), new ScreenPoint(-1.5, 1.5), new ScreenPoint(-1.5, -1.5),
                                },
                };

                if (scatter_series_list.Exists(x => (string)x.Tag == "open_short_deals_series"))
                    scatter_series_list.Remove(scatter_series_list.Find(x => (string)x.Tag == "open_short_deals_series"));

                var open_short_deals_series = new ScatterSeries()
                {
                    Tag = "open_short_deals_series",
                    MarkerType = MarkerType.Custom,
                    MarkerSize = 4,
                    MarkerStrokeThickness = 0.5,
                    MarkerStroke = OxyColors.AliceBlue,
                    EdgeRenderingMode = EdgeRenderingMode.Adaptive,
                    MarkerFill = OxyColor.FromArgb(255, 255, 17, 0),
                    MarkerOutline = new[]
                                    {
                                    new ScreenPoint(0, 0), new ScreenPoint(-1.5, 1.5), new ScreenPoint(-1.5, -1.5),
                                },
                };

                if (scatter_series_list.Exists(x => (string)x.Tag == "close_long_deals_series"))
                    scatter_series_list.Remove(scatter_series_list.Find(x => (string)x.Tag == "close_long_deals_series"));


                var close_long_deals_series = new ScatterSeries()
                {
                    Tag = "close_long_deals_series",
                    MarkerType = MarkerType.Custom,
                    MarkerSize = 4,
                    MarkerStrokeThickness = 0.5,
                    MarkerStroke = OxyColors.AliceBlue,
                    EdgeRenderingMode = EdgeRenderingMode.Adaptive,
                    MarkerFill = OxyColor.FromArgb(255, 167, 168, 170),
                    MarkerOutline = new[]
                                    {
                                    new ScreenPoint(0, 0), new ScreenPoint(1.5, -1.5), new ScreenPoint(1.5, 1.5),
                                },
                };

                if (scatter_series_list.Exists(x => (string)x.Tag == "close_short_deals_series"))
                    scatter_series_list.Remove(scatter_series_list.Find(x => (string)x.Tag == "close_short_deals_series"));


                var close_short_deals_series = new ScatterSeries()
                {
                    Tag = "close_short_deals_series",
                    MarkerType = MarkerType.Custom,
                    MarkerSize = 4,
                    MarkerStrokeThickness = 0.5,
                    MarkerStroke = OxyColors.AliceBlue,
                    EdgeRenderingMode = EdgeRenderingMode.Adaptive,
                    MarkerFill = OxyColor.FromArgb(255, 167, 168, 170),
                    MarkerOutline = new[]
                                    {
                                    new ScreenPoint(0, 0), new ScreenPoint(1.5, -1.5), new ScreenPoint(1.5, 1.5),
                                },
                };


                var long_deals = deals.Where(x => x.Direction == Side.Buy && x.State != PositionStateType.OpeningFail && x.EntryPrice != 0).ToArray();
                var short_deals = deals.Where(x => x.Direction == Side.Sell && x.State != PositionStateType.OpeningFail && x.EntryPrice != 0).ToArray();

                var items_open_long = long_deals
                                 .Select(x => new ScatterPoint(DateTimeAxis.ToDouble(x.TimeOpen), (double)x.EntryPrice)
                                 {
                                 }).ToList();

                var items_open_short = short_deals
                                 .Select(x => new ScatterPoint(DateTimeAxis.ToDouble(x.TimeOpen), (double)x.EntryPrice)
                                 {
                                 }).ToList();

                var items_close_long = long_deals
                                 .Where(x => x.State == PositionStateType.Done)
                                 .Select(x => new ScatterPoint(DateTimeAxis.ToDouble(x.TimeClose), (double)x.ClosePrice)
                                 {
                                 }).ToList();

                var items_close_short = short_deals
                                 .Where(x => x.State == PositionStateType.Done)
                                 .Select(x => new ScatterPoint(DateTimeAxis.ToDouble(x.TimeClose), (double)x.ClosePrice)
                                 {
                                 }).ToList();



                open_long_deals_series.Points.AddRange(items_open_long);
                open_short_deals_series.Points.AddRange(items_open_short);
                close_long_deals_series.Points.AddRange(items_close_long);
                close_short_deals_series.Points.AddRange(items_close_short);

                scatter_series_list.Add(open_long_deals_series);
                scatter_series_list.Add(open_short_deals_series);
                scatter_series_list.Add(close_long_deals_series);
                scatter_series_list.Add(close_short_deals_series);

                
                

                var profit_line_long = new LineSeries()
                {
                    LineStyle = LineStyle.LongDash,
                    Color = OxyColor.FromArgb(255, 13, 255, 0),
                    Selectable = false,
                    EdgeRenderingMode = EdgeRenderingMode.Adaptive,
                    StrokeThickness = 1,
                    Tag = "profit_line_long"
                };

                
                var loss_line_short = new LineSeries()
                {
                    LineStyle = LineStyle.LongDash,
                    Color = OxyColor.FromArgb(255, 255, 17, 0),
                    Selectable = false,
                    EdgeRenderingMode = EdgeRenderingMode.Adaptive,
                    StrokeThickness = 1,
                    Tag = "loss_line_short"
                };



                var profit_deals = deals.Where(x => x.ProfitOperationPunkt > 0 && x.State == PositionStateType.Done && x.EntryPrice != 0).ToArray();
                var loss_deals = deals.Where(x => x.ProfitOperationPunkt <= 0 && x.State == PositionStateType.Done && x.EntryPrice != 0).ToArray();


                for (int i = 0; i < profit_deals.Length; i++)
                {
                    profit_line_long.Points.Add(new DataPoint(DateTimeAxis.ToDouble(profit_deals[i].TimeOpen), (double)profit_deals[i].EntryPrice));
                    profit_line_long.Points.Add(new DataPoint(DateTimeAxis.ToDouble(profit_deals[i].TimeClose), (double)profit_deals[i].ClosePrice));

                    if (i != profit_deals.Length - 1)
                        profit_line_long.Points.Add(new DataPoint(double.NaN, (double)profit_deals[i].ClosePrice));
                }

                for (int i = 0; i < loss_deals.Length; i++)
                {
                    loss_line_short.Points.Add(new DataPoint(DateTimeAxis.ToDouble(loss_deals[i].TimeOpen), (double)loss_deals[i].EntryPrice));
                    loss_line_short.Points.Add(new DataPoint(DateTimeAxis.ToDouble(loss_deals[i].TimeClose), (double)loss_deals[i].ClosePrice));

                    if (i != loss_deals.Length - 1)
                        loss_line_short.Points.Add(new DataPoint(double.NaN, (double)loss_deals[i].ClosePrice));
                }

                if (lines_series_list.Exists(x => (string)x.Tag == "profit_line_long"))
                    lines_series_list.Remove(lines_series_list.Find(x => (string)x.Tag == "profit_line_long"));

                if (lines_series_list.Exists(x => (string)x.Tag == "loss_line_short"))
                    lines_series_list.Remove(lines_series_list.Find(x => (string)x.Tag == "loss_line_short"));

                if (profit_line_long.Points.Count > 0)
                    lines_series_list.Add(profit_line_long);

                if (loss_line_short.Points.Count > 0)
                    lines_series_list.Add(loss_line_short);
            };


            plot_view.Dispatcher.Invoke(positions_action);
        }

        public void BuildCandleSeries(List<Candle> history_candles)
        {
            lock (redraw_locker)
            {
                my_candles = history_candles;

                if (my_candles == null || my_candles.Count == 0)
                    return;

                if (plot_view == null || plot_model == null)
                    return;

                has_candles = true;

                if (axis_Y_type == "linear")
                {
                    if (my_candles.Count == items_oxy_candles.Count)
                    {
                        Candle last_candle = my_candles.Last();

                        items_oxy_candles[items_oxy_candles.Count - 1] = new HighLowItem()
                        {
                            X = DateTimeAxis.ToDouble(last_candle.TimeStart),
                            Open = (double)last_candle.Open,
                            High = (double)last_candle.High,
                            Low = (double)last_candle.Low,
                            Close = (double)last_candle.Close,
                        };
                    }
                    else if (my_candles.Count == items_oxy_candles.Count + 1)
                    {
                        Candle last_candle = my_candles.Last();

                        items_oxy_candles.Add(new HighLowItem()
                        {
                            X = DateTimeAxis.ToDouble(last_candle.TimeStart),
                            Open = (double)last_candle.Open,
                            High = (double)last_candle.High,
                            Low = (double)last_candle.Low,
                            Close = (double)last_candle.Close,
                        });

                        //if (my_candles.Count >= 2)
                        //{
                        //    double gap_main = DateTimeAxis.ToDouble(my_candles[my_candles.Count - 2].TimeStart.Add(owner.time_frame_span)) - DateTimeAxis.ToDouble(my_candles[my_candles.Count - 2].TimeStart);

                        //    double gap_now = items_oxy_candles[items_oxy_candles.Count - 1].X - items_oxy_candles[items_oxy_candles.Count - 2].X;

                        //    if (gap_main * 2 < gap_now)
                        //    {
                        //        foreach (var area in all_areas)
                        //        {
                        //            area.date_time_axis_X.gaps.Add(new double[] { items_oxy_candles[items_oxy_candles.Count - 2].X, items_oxy_candles[items_oxy_candles.Count - 1].X });
                        //        }
                        //    }
                        //}
                    }
                    else
                    {

                        items_oxy_candles = my_candles.AsParallel()
                                                      .AsOrdered()
                                                      .Select(x => new HighLowItem()
                                                      {
                                                          X = DateTimeAxis.ToDouble(x.TimeStart),
                                                          Open = (double)x.Open,
                                                          High = (double)x.High,
                                                          Low = (double)x.Low,
                                                          Close = (double)x.Close,
                                                      }).ToList();
                    }
                }

                if (axis_Y_type == "percent")
                {
                    items_oxy_candles = my_candles.Select(x => new HighLowItem()
                    {
                        X = DateTimeAxis.ToDouble(x.TimeStart),
                        Open = (double)x.Open,
                        High = (double)x.High,
                        Low = (double)x.Low,
                        Close = (double)x.Close,
                    }).ToList();



                    var start_time = DateTimeAxis.InverseTransform(new ScreenPoint(plot_model.PlotArea.Left, plot_model.PlotArea.Top), date_time_axis_X, linear_axis_Y);
                    var end_time = DateTimeAxis.InverseTransform(new ScreenPoint(plot_model.PlotArea.Right, plot_model.PlotArea.Top), date_time_axis_X, linear_axis_Y);



                    candle_on_screen = items_oxy_candles.Where(x => x.X >= start_time.X && x.X <= end_time.X).ToList();

                    if (candle_on_screen.Count == 0)
                        return;

                    items_oxy_candles = items_oxy_candles.Select(x => new HighLowItem()
                    {
                        X = x.X,
                        Open = ((candle_on_screen[0].Open * 100 / x.Open) - 100) * -1,
                        High = ((candle_on_screen[0].Open * 100 / x.High) - 100) * -1,
                        Low = ((candle_on_screen[0].Open * 100 / x.Low) - 100) * -1,
                        Close = ((candle_on_screen[0].Open * 100 / x.Close) - 100) * -1,
                    }).ToList();
                }

                foreach (var area in all_areas)
                {
                    area.items_oxy_candles = items_oxy_candles;
                }

                candle_stick_series.Items.Clear();
                candle_stick_series.Items.AddRange(items_oxy_candles);
            }
        }



        public override void Calculate(TimeSpan time_frame_span, TimeFrame time_frame)
        {
            lock (redraw_locker)
            {
                if (my_candles == null || my_candles.Count < 1 || my_candles.Last().Close == last_price)
                {
                    return;
                }

                if (plot_model == null || plot_view == null)
                {
                    return;
                }


                Action redraw_action = () =>
                {
                    List<HighLowItem> candles_items = new List<HighLowItem>();

                    candles_items = items_oxy_candles;

                    if (candles_items.Count == 0)
                        return;

                    var delta_seconds = time_frame_span.TotalSeconds;
                    int empty_gap = area_settings.empty_gap;
                    int candles_in_run = area_settings.candles_in_run;

                    last_X = candles_items.Last().X;
                    last_Y = candles_items.Last().Close;

                    var candle_stick_series = new CandleStickSeries()
                    {
                        IncreasingColor = OxyColors.MediumSeaGreen,
                        DecreasingColor = OxyColors.Tomato,
                        EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
                    };

                    candle_stick_series.Items.AddRange(candles_items);
                    candle_stick_series.SeriesGroupName = "MainCandles";
                    candle_stick_series.TextColor = OxyColors.AliceBlue;

                    if (plot_model == null)
                    {
                        return;
                    }

                    foreach (var ax in plot_model.Axes)
                    {
                        ax.IsAxisVisible = true;
                    }

                    foreach (var area in all_areas.Where(x => (string)x.Tag != "ScrollChart"))
                    {
                        area.date_time_axis_X.AbsoluteMinimum = candles_items.First().X;
                        area.date_time_axis_X.AbsoluteMaximum = DateTimeAxis.ToDouble(my_candles.Last().TimeStart.AddSeconds(delta_seconds * empty_gap));

                        area.date_time_axis_X.Minimum = DateTimeAxis.ToDouble(my_candles[0].TimeStart);
                        area.date_time_axis_X.Maximum = DateTimeAxis.ToDouble(my_candles[0].TimeStart.AddSeconds(delta_seconds * candles_in_run));
                    }

                    date_time_axis_X.IsAxisVisible = true;

                    if (my_candles.Count <= 20)
                    {
                        for (int i = 0; i < my_candles.Count; i++)
                        {
                            if (GetDecimalsCount(my_candles[i].Close) > digits)
                                digits = GetDecimalsCount(my_candles[i].Close);
                        }
                    }
                    else
                    {
                        for (int i = my_candles.Count - 20; i < my_candles.Count; i++)
                        {
                            if (GetDecimalsCount(my_candles[i].Close) > digits)
                                digits = GetDecimalsCount(my_candles[i].Close);
                        }
                    }

                    linear_axis_Y.StringFormat = "f" + digits.ToString();
                    linear_axis_Y.IsAxisVisible = true;

                    if (!isFreeze)
                    {
                        double X_start = DateTimeAxis.ToDouble(my_candles.Last().TimeStart.AddSeconds(-1 * delta_seconds * (candles_in_run - empty_gap)));
                        double X_end = DateTimeAxis.ToDouble(my_candles.Last().TimeStart.AddSeconds(delta_seconds * empty_gap));

                        List<double> min_max = GetHighLow(true, X_start, X_end);

                        linear_axis_Y.Zoom(min_max[0], min_max[1]);

                        date_time_axis_X.Zoom(X_start, X_end);
                    }

                    if (mouse_on_main_chart == false)
                    {
                        cursor_Y.X = 0;
                        cursor_Y.Y = 0;
                        cursor_X.X = 0;
                        cursor_X.Y = last_Y;

                        foreach (var area in all_areas.Where(x => x is IndicatorArea))
                        {
                            Action action_cursor = () =>
                            {
                                area.cursor_Y.X = cursor_Y.X;
                                area.cursor_Y.Y = cursor_Y.Y;
                            };
                        }

                        annotation_price.TextPosition = new ScreenPoint(plot_view.ActualWidth - plot_model.Padding.Right, annotation_price.GetDataPointPosition(new OxyPlot.DataPoint(candles_items.Last().X, candles_items.Last().Close)).Y);
                        annotation_price.Text = last_Y.ToString("F" + digits.ToString());

                        annotation_date_time.TextPosition = new ScreenPoint(annotation_price.GetDataPointPosition(new DataPoint(last_X, last_Y)).X, plot_view.ActualHeight - plot_model.Padding.Bottom);
                        annotation_date_time.Text = DateTimeAxis.ToDateTime(last_X).ToString();
                    }
                    else
                    {
                        if (mouse_event_args != null)
                        {
                            Axis yAxis = linear_axis_Y;
                            DataPoint dataPoint_event = date_time_axis_X.InverseTransform(mouse_event_args.GetPosition(plot_view).X, mouse_event_args.GetPosition(plot_view).Y, yAxis);

                            cursor_Y.X = dataPoint_event.X;
                            cursor_Y.Y = 0;
                            cursor_X.X = 0;
                            cursor_X.Y = dataPoint_event.Y;

                            foreach (var area in all_areas.Where(x => x is IndicatorArea))
                            {
                                Action action_cursor = () =>
                                {
                                    area.cursor_Y.X = cursor_Y.X;
                                    area.cursor_Y.Y = cursor_Y.Y;
                                };
                            }

                            if (mouse_event_args != null)
                            {
                                annotation_price.TextPosition = new ScreenPoint(plot_view.ActualWidth - plot_model.Padding.Right, mouse_event_args.GetPosition(plot_view).Y);
                                annotation_price.Text = dataPoint_event.Y.ToString("F" + digits.ToString());

                                annotation_date_time.TextPosition = new ScreenPoint(mouse_event_args.GetPosition(plot_view).X, plot_view.ActualHeight - plot_model.Padding.Bottom);
                                annotation_date_time.Text = DateTimeAxis.ToDateTime(dataPoint_event.X).ToString();
                            }
                        }
                    }

                    plot_model.InvalidatePlot(true);
                };


                plot_view.Dispatcher.Invoke(redraw_action);
            }
        }

        private int GetDecimalsCount(decimal d)
        {
            int i = 0;
            while (d * GetPow(10, 1 + i) % 10 != 0) { i++; }

            return i;
        }

        private decimal GetPow(decimal num, int pow)
        {
            decimal num_n = 1;

            for (int i = 0; i < pow; i++)
            {
                num_n *= num;
            }

            return num_n;
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
