using CustomAnnotations;
using OsEngine.Charts.CandleChart.Entities;
using OsEngine.Entity;
using OsEngine.Indicators;
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

        public List<HighLowItem> candle_on_screen = new List<HighLowItem>();

        public List<ScatterSeries> scatter_series_list = new List<ScatterSeries>();
        public List<LineSeries> lines_series_list = new List<LineSeries>();
        public List<LinearBarSeries> linear_bar_series_list = new List<LinearBarSeries>();
        public CandleStickSeries candle_stick_seria = new CandleStickSeries();


        private int digits = 2;
        private double last_Y = 0;
        private double last_X = 0;
        private decimal last_price = 0;
        private LastClickButton last_click_button;
        public bool isFreeze = false;
        public double X_start = 0;
        public double X_end = 0;

        public bool mouse_on_main_chart = false;
        public bool has_candles = false;

        public OxyPlot.DataPoint mouse_point = new DataPoint();

        private object candle_locker = new object();


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

            candle_stick_seria = new CandleStickSeries()
            {
                IncreasingColor = OxyColor.FromArgb(255, 55, 219, 186),
                DecreasingColor = OxyColor.FromArgb(255, 235, 96, 47),
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };

            plot_view.MouseLeave += Plot_view_main_chart_MouseLeave;
            plot_view.MouseMove += Plot_view_main_chart_MouseMove;

            plot_model.Updated += Plot_model_Updated;
            plot_model.MouseDown += Plot_model_MouseDown;
            plot_model.MouseUp += Plot_model_MouseUp;

        }

        private void Plot_model_MouseUp(object sender, OxyMouseEventArgs e)
        {
            SetViewPoligonByMainChart();
            owner.mediator.RedrawScroll(false);
        }

        private void Plot_model_MouseDown(object sender, OxyMouseDownEventArgs e)
        {
            if (e.ChangedButton == OxyMouseButton.Right)
            {
                mouse_point = new DataPoint(e.Position.X, e.Position.Y);
            }
        }

        private void Plot_model_Updated(object sender, EventArgs e)
        {
            owner.mediator.ZoomIndicators(plot_model.Axes[0].ActualMinimum, plot_model.Axes[0].ActualMaximum);

            if (!owner.mediator.scroll_bar.scroll_chart_left_mouse_is_down)
            {
                SetViewPoligonByMainChart();
                owner.mediator.RedrawScroll(false);
            }
        }

        private void Plot_view_main_chart_MouseLeave(object sender, MouseEventArgs e)
        {
            mouse_on_main_chart = false;

            if (has_candles == false)
                return;

            Action action = () =>
            {
                if (last_X != 0 && last_Y != 0)
                {
                    cursor_Y.X = last_X;
                    cursor_Y.Y = double.MinValue;
                    cursor_X.X = double.MinValue;
                    cursor_X.Y = last_Y;

                    foreach (var area in all_areas.Where(x => x is IndicatorArea))
                    {
                        Action action_cursor = () =>
                        {
                            area.cursor_Y.X = double.MinValue;
                            area.cursor_Y.Y = double.MinValue;
                            area.cursor_X.X = double.MinValue;
                            area.cursor_X.Y = double.MinValue;
                        };

                        area.plot_view.Dispatcher.Invoke(action_cursor);
                        area.Redraw();
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
                        };

                        area.plot_view.Dispatcher.Invoke(action_cursor);
                        area.Redraw();
                    }
                }

                e.Handled = true;
            };

            actions_to_calculate.Enqueue(action);

            owner.mediator.RedrawPrime(false);
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
                cursor_Y.Y = double.MinValue;
                cursor_X.X = double.MinValue;
                cursor_X.Y = dataPoint_event.Y;


                foreach (var area in all_areas.Where(x => x is IndicatorArea))
                {
                    Action action_cursor = () =>
                    {
                        area.cursor_Y.X = cursor_Y.X;
                        area.cursor_Y.Y = cursor_Y.Y;
                    };

                    area.actions_to_calculate.Enqueue(action_cursor);
                    area.Calculate(time_frame_span, time_frame);
                    area.Redraw();
                }

                annotation_price.TextPosition = new ScreenPoint(plot_view.ActualWidth - plot_model.Padding.Right, e.GetPosition(plot_view).Y);
                annotation_price.Text = dataPoint_event.Y.ToString("F" + digits.ToString());

                annotation_date_time.TextPosition = new ScreenPoint(e.GetPosition(plot_view).X, plot_view.ActualHeight - plot_model.Padding.Bottom);
                annotation_date_time.Text = DateTimeAxis.ToDateTime(dataPoint_event.X).ToString();
            };

            actions_to_calculate.Enqueue(action);
            owner.mediator.RedrawPrime(false);

            e.Handled = true;
        }



        private void SetViewPoligonByMainChart()
        {
            ScrollBarArea scroll_area = owner.mediator.scroll_bar;

            Action action_move_polygon_viewer = () =>
            {
                double first_candle_X = plot_model.Axes[0].ActualMinimum;
                double last_candle_X = plot_model.Axes[0].ActualMaximum;

                List<OxyPlot.DataPoint> new_points = new List<OxyPlot.DataPoint>()
                {
                    new DataPoint(first_candle_X, scroll_area.top_value),
                    new DataPoint(last_candle_X, scroll_area.top_value),
                    new DataPoint(last_candle_X, scroll_area.bottom_value),
                    new DataPoint(first_candle_X, scroll_area.bottom_value)
                };

                scroll_area.screen_viewer_polygon.Points.Clear();
                scroll_area.screen_viewer_polygon.Points.AddRange(new_points);
            };

            scroll_area.plot_view.Dispatcher.Invoke(action_move_polygon_viewer);
        }

        public void ProcessPositions(List<Position> deals)
        {
            if (deals == null)
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

                    MarkerOutline = new[] { new ScreenPoint(0, 0), new ScreenPoint(-1.5, 1.5), new ScreenPoint(-1.5, -1.5) },
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


            actions_to_calculate.Enqueue(positions_action);
        }

        public void BuildCandleSeries()
        {
            if (my_candles == null || my_candles.Count == 0)
                return;

            if (plot_view == null || plot_model == null)
                return;

            lock (series_locker)
            {
                has_candles = true;


                try
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
                    }
                    else
                    {
                        items_oxy_candles = my_candles.Select(x => new HighLowItem()
                        {
                            X = DateTimeAxis.ToDouble(x.TimeStart),
                            Open = (double)x.Open,
                            High = (double)x.High,
                            Low = (double)x.Low,
                            Close = (double)x.Close,
                        }).ToList();
                    }
                }
                catch { return; }
            }
        }

        public override void Calculate(TimeSpan time_frame_span, TimeFrame time_frame)
        {
            if (my_candles == null || my_candles.Count < 1 || my_candles.Last().Close == last_price)
            {
                return;
            }

            if (plot_model == null || plot_view == null)
            {
                return;
            }


            for (int i = 0; i < actions_to_calculate.Count; i++)
            {
                Action action = new Action(() => { });

                bool result = actions_to_calculate.TryDequeue(out action);

                if (result)
                    plot_view.Dispatcher.Invoke(action);
            }

            Action redraw_action = () =>
            {
                if (items_oxy_candles.Count == 0)
                    return;

                lock (series_locker)
                {
                    var delta_seconds = time_frame_span.TotalSeconds;
                    int empty_gap = area_settings.empty_gap;
                    int candles_in_run = area_settings.candles_in_run;

                    if (delta_seconds == 0)
                    {
                        if (my_candles.Count < 2)
                        {
                            delta_seconds = 60;
                        }
                        else
                        {
                            if (my_candles.Count < 30)
                            {
                                var start_time = my_candles.First().TimeStart;
                                var end_time = my_candles.Last().TimeStart;

                                TimeSpan new_delta = end_time - start_time;

                                delta_seconds = new_delta.TotalSeconds / my_candles.Count;
                            }
                            else
                            {
                                var start_time = my_candles.First().TimeStart;
                                var end_time = my_candles[29].TimeStart;

                                TimeSpan new_delta = end_time - start_time;

                                delta_seconds = new_delta.TotalSeconds / 30;
                            }
                        }
                    }

                    last_X = items_oxy_candles.Last().X;
                    last_Y = items_oxy_candles.Last().Close;

                    try
                    {
                        candle_stick_seria.Items.Clear();
                        candle_stick_seria.Items.AddRange(items_oxy_candles);
                    }
                    catch { return; }

                    if (plot_model == null)
                    {
                        return;
                    }

                    foreach (var ax in plot_model.Axes)
                    {
                        ax.IsAxisVisible = true;
                    }

                    var area_scroll = all_areas.Find(x => x is ScrollBarArea);

                    area_scroll.date_time_axis_X.AbsoluteMinimum = items_oxy_candles.First().X;
                    area_scroll.date_time_axis_X.AbsoluteMaximum = DateTimeAxis.ToDouble(my_candles.Last().TimeStart.AddSeconds(delta_seconds * empty_gap));

                    area_scroll.date_time_axis_X.Minimum = DateTimeAxis.ToDouble(my_candles[0].TimeStart);
                    area_scroll.date_time_axis_X.Maximum = DateTimeAxis.ToDouble(my_candles[0].TimeStart.AddSeconds(delta_seconds * candles_in_run));

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
                        X_start = DateTimeAxis.ToDouble(my_candles.Last().TimeStart.AddSeconds(-1 * delta_seconds * (candles_in_run - empty_gap)));
                        X_end = DateTimeAxis.ToDouble(my_candles.Last().TimeStart.AddSeconds(delta_seconds * empty_gap));

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

                            area.actions_to_calculate.Enqueue(action_cursor);
                        }

                        annotation_price.TextPosition = new ScreenPoint(plot_view.ActualWidth - plot_model.Padding.Right, annotation_price.GetDataPointPosition(new OxyPlot.DataPoint(items_oxy_candles.Last().X, items_oxy_candles.Last().Close)).Y);
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

                                area.actions_to_calculate.Enqueue(action_cursor);
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
                }
            };

            plot_view.Dispatcher.Invoke(redraw_action);
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

            lock (series_locker)
            {
                var main_chart = (CandleStickArea)all_areas.Find(x => (string)x.Tag == "Prime");

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

                            points.Add(new DataPoint(items_oxy_candles[i].X, last_point));
                        };

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

                            points.Add(new DataPoint(items_oxy_candles[i].X, last_point));

                        };

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

                            points.Add(new ScatterPoint(items_oxy_candles[i].X, last_point));
                        };

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


                    if (scatter_series_list.Exists(x => (string)x.Tag == indi_seria.SeriaName))
                        scatter_series_list.Remove(scatter_series_list.Find(x => (string)x.Tag == indi_seria.SeriaName));

                    scatter_series_list.Add(scatter_seria);

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
                        values.AddRange(scatter.Points.AsParallel().Where(x => x.X >= start && x.X <= end).Select(x => x.Y));
                    }
                }

                if (lines_series_list.Count > 0)
                {
                    foreach (var lines_series in lines_series_list)
                    {
                        values.AddRange(lines_series.Points.AsParallel().Where(x => x.X >= start && x.X <= end).Select(x => x.Y));
                    }
                }

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

        public override void Redraw()
        {
            Action action = () =>
            {
                lock (series_locker)
                {

                    plot_model.Series.Clear();

                    plot_model.Series.Add(candle_stick_seria);

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
