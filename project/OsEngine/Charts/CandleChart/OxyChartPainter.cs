using CustomAnnotations;
using OsEngine.Alerts;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Charts.CandleChart.Entities;
using OsEngine.Charts.CandleChart.OxyAreas;
using OsEngine.Charts.ColorKeeper;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Logging;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.SkiaSharp.Wpf;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using System.Windows.Input;



namespace OsEngine.Charts.CandleChart
{
    public class OxyChartPainter : IChartPainter
    {
        public List<IndicatorSeria> series = new List<IndicatorSeria>();
        public bool IsPatternChart { get; set; }

        public event Action<string, LogMessageType> LogMessageEvent;
        public event Action<int> ClickToIndexEvent;
        public event Action<int> SizeAxisXChangeEvent;
        public event Action<ChartClickType> ChartClickEvent;

        public StartProgram start_program;
        private ChartMasterColorKeeper color_keeper;
        WindowsFormsHost host;
        private System.Windows.Forms.Panel panel_winforms;

        private List<OxyArea> all_areas = new List<OxyArea>();
        private List<GridSplitter> splitters = new List<GridSplitter>();

        private bool isPaint = false;
        private bool nead_no_delete = false;
        private System.Windows.Controls.Grid main_grid_chart;
      
        public TimeSpan time_frame_span = new TimeSpan();
        public TimeFrame time_frame = new TimeFrame();

        private int skipper = 0;
        private int last_deals_count = 0;

        private int series_counter = 0;

        private int indicators_count = 0;
        private int indicator_number = 0;
        private bool have_indicators;
        private bool is_last_indicator;

        public object seria_locker = new object();

        private void Awaite()
        {
            Task delay = new Task(() =>
            {
                Thread.Sleep(500);
            });

            delay.Start();
            delay.Wait();
        }

        public OxyChartPainter(string name, StartProgram startProgram)
        {
            start_program = startProgram;
            color_keeper = new ChartMasterColorKeeper(name);
        }

        public void MainChartMouseButtonClick(ChartClickType click_type)
        {
            ChartClickEvent?.Invoke(click_type);
        }

        public bool AreaIsCreate(string name)
        {
            foreach (var area in all_areas)
                if (area.Tag == (object)name)
                    return true;

            return false;
        }

        public void ClearAlerts(List<IIAlert> alertArray)
        {
            //throw new NotImplementedException();
        }

        public void ClearDataPointsAndSizeValue()
        {
            for (int i = 0; i < all_areas.Count; i++)
            {
                if (all_areas[i] is CandleStickArea)
                {
                    ((CandleStickArea)all_areas[i]).Dispose();
                }

                if (all_areas[i] is ScrollBarArea)
                {
                    ((ScrollBarArea)all_areas[i]).Dispose();
                }

                if (all_areas[i] is IndicatorArea)
                {
                    ((IndicatorArea)all_areas[i]).Dispose();
                }

                if (all_areas[i] is ControlPanelArea)
                {
                    ((ControlPanelArea)all_areas[i]).Dispose();
                }
            }
        }

        public void ClearSeries()
        {
            series.Clear();
        }

        public string CreateArea(string nameArea, int height)
        {
            if (all_areas.Exists(x => (string)x.Tag == nameArea))
            {
                return nameArea;
            }

            if (nameArea == "Prime" || all_areas.Exists(x => (string)x.Tag == nameArea))
            {
                return nameArea;
            }

            var indicator_chart = new IndicatorArea(new OxyAreaSettings()
            {
                cursor_X_is_active = true,
                cursor_Y_is_active = true,
                Tag = nameArea,
                AbsoluteMinimum = double.MinValue,
                Y_Axies_is_visible = true,
                X_Axies_is_visible = true,
                brush_background = "#111721"
            }, all_areas, nameArea, this);

            indicator_chart.plot_model.Axes[0].TextColor = OxyColors.Transparent;
            indicator_chart.plot_model.Axes[0].TicklineColor = OxyColors.Transparent;
            indicator_chart.plot_model.Axes[0].AxisDistance = -50;
            indicator_chart.plot_model.Axes[1].IntervalLength = 10;
            indicator_chart.plot_model.Axes[1].MinorGridlineStyle = LineStyle.None;
            indicator_chart.plot_model.PlotMargins = new OxyThickness(0, indicator_chart.plot_model.PlotMargins.Top, indicator_chart.plot_model.PlotMargins.Right, indicator_chart.plot_model.PlotMargins.Bottom);
            indicator_chart.plot_model.Padding = new OxyThickness(0, 0, indicator_chart.plot_model.Padding.Right, 0);
            indicator_chart.plot_model.PlotMargins = new OxyThickness(0, 0, indicator_chart.plot_model.PlotMargins.Right, 0);
            indicator_chart.plot_view.Padding = new System.Windows.Thickness(0, 0, indicator_chart.plot_view.Padding.Right, 0);
            indicator_chart.plot_view.Margin = new System.Windows.Thickness(0, 0, indicator_chart.plot_view.Margin.Right, 0);

            all_areas.Add(indicator_chart);

            return nameArea;
        }

        public string CreateSeries(string areaName, IndicatorChartPaintType indicatorType, string name)
        {
            var new_seria = new IndicatorSeria()
            {
                AreaName = areaName,
                IndicatorType = indicatorType,
                SeriaName = name
            };

            if (!series.Contains(new_seria))
            {
                series.Add(new IndicatorSeria()
                {
                    AreaName = areaName,
                    IndicatorType = indicatorType,
                    SeriaName = name
                });

                indicators_count++;
                indicator_number = indicators_count;
            }

            return name;
        }

        public void CreateTickArea()
        {
            throw new NotImplementedException();
        }

        public void Delete()
        {
            color_keeper.Delete();

            series.Clear();
            time_frame_span = new TimeSpan();
            time_frame = new TimeFrame();
            skipper = 0;
            series_counter = 0;
            have_indicators = false;

            for (int i = 0; i < all_areas.Count; i++)
            {
                if (all_areas[i] is CandleStickArea)
                {
                    ((CandleStickArea)all_areas[i]).Dispose();
                }

                if (all_areas[i] is ScrollBarArea)
                {
                    ((ScrollBarArea)all_areas[i]).Dispose();
                }

                if (all_areas[i] is IndicatorArea)
                {
                    ((IndicatorArea)all_areas[i]).Dispose();
                }

                if (all_areas[i] is ControlPanelArea)
                {
                    ((ControlPanelArea)all_areas[i]).Dispose();
                }

                all_areas[i] = null;
            }

            all_areas.Clear();

            if (main_grid_chart != null)
            {
                main_grid_chart.RowDefinitions.Clear();
                main_grid_chart.Children.Clear();
                main_grid_chart = null;
            }
        }

        public void DeleteIndicator(IIndicator indicator)
        {
            if (indicator.NameArea == "Prime")
            {
                series.Remove(series.Find(x => x.SeriaName == indicator.NameSeries));
                return;
            }

            series.Remove(series.Find(x => x.SeriaName == indicator.NameSeries));

            var area = all_areas.Find(x => ((string)x.Tag).Contains(indicator.NameArea));
            var splitter = splitters.Find(x => (string)x.Tag == indicator.NameArea);

            ((IndicatorArea)area).Dispose();

            lock (seria_locker)
            {
                all_areas.Remove(area);
            }
            splitters.Remove(splitter);

            main_grid_chart.Children.Clear();
            main_grid_chart.RowDefinitions.Clear();

            MakeChart(main_grid_chart);
        }

        public void DeleteTickArea()
        {
            throw new NotImplementedException();
        }

        public List<string> GetAreasNames()
        {
            return all_areas.Where(x => (string)x.Tag != "ControlPanel" && (string)x.Tag != "ScrollChart").Select(x => (string)x.Tag).ToList();
        }

        public int GetCursorSelectCandleNumber()
        {
            throw new NotImplementedException();
        }

        public decimal GetCursorSelectPrice()
        {
            throw new NotImplementedException();
        }

        public void GoChartToIndex(int index)
        {
            var main_area = all_areas.Find(x => (string)x.Tag == "Prime");

            GoChartToTime(main_area.my_candles[index].TimeStart);
        }

        public void GoChartToTime(DateTime time)
        {
            foreach (var area in all_areas.Where(x => x.Tag != (object)"ScrollChart"))
            {
                Action action = () =>
                {
                    if (area is CandleStickArea)
                    {
                        var main_area = (CandleStickArea)area;
                        main_area.BuildCandleSeries(area.my_candles);
                        main_area.Calculate(area.owner.time_frame_span, area.owner.time_frame);
                    }

                    double first_value = area.plot_model.Axes[0].ActualMinimum;
                    double last_value = area.plot_model.Axes[0].ActualMinimum;

                    double main_volume = DateTimeAxis.ToDouble(time);

                    area.date_time_axis_X.Zoom(main_volume - (last_value - first_value) / 2, main_volume + (last_value - first_value) / 2);

                    List<double> max_min = new List<double>();

                    if (area.Tag == (object)"Prime")
                        max_min = area.GetHighLow(true, main_volume - (last_value - first_value) / 2, main_volume + (last_value - first_value) / 2);
                    else
                        max_min = area.GetHighLow(false, main_volume - (last_value - first_value) / 2, main_volume + (last_value - first_value) / 2);

                    area.linear_axis_Y.Zoom(max_min[0], max_min[1]);
                };

                area.plot_view.Dispatcher.Invoke(action);
            }
        }

        public void HideAreaOnChart()
        {
            foreach (var row in main_grid_chart.RowDefinitions.Where(x => (string)x.Tag != "Prime" && (string)x.Tag != "ScrollChart" && (string)x.Tag != "ControlPanel"))
                row.Height = new GridLength(0, GridUnitType.Pixel);
        }

        public bool IndicatorIsCreate(string name)
        {
            if (series.Exists(x => x.SeriaName == name))
                return true;
            else
            {
                return false;
            }    
        }

        public void PaintAlert(AlertToChart alert)
        {
            throw new NotImplementedException();
        }

        public void PaintHorisiontalLineOnArea(LineHorisontal lineElement)
        {
            throw new NotImplementedException();
        }

        public void PaintInDifColor(int indexStart, int indexEnd, string seriesName)
        {
            throw new NotImplementedException();
        }

        public void PaintOneLine(System.Windows.Forms.DataVisualization.Charting.Series mySeries, List<Candle> candles, ChartAlertLine line, Color colorLine, int borderWidth, Color colorLabel, string label)
        {
            throw new NotImplementedException();
        }

        public void PaintPoint(PointElement point)
        {
            throw new NotImplementedException();
        }

        public void PaintSingleCandlePattern(List<Candle> candles)
        {
            throw new NotImplementedException();
        }

        public void PaintSingleVolumePattern(List<Candle> candles)
        {
            throw new NotImplementedException();
        }

        public void ProcessCandles(List<Candle> history)
        {
            lock (seria_locker)
            {
                if (skipper > 0)
                {
                    skipper -= 1;
                    return;
                }

                if (isPaint == false)
                {
                    return;
                }

                lock (seria_locker)
                {
                    foreach (var seria in series)
                    {
                        seria.series_counter = 1;
                    }
                    if (all_areas.Exists(x => x.Tag == (object)"Prime"))
                    {
                        var main_area = (CandleStickArea)all_areas.Find(x => x.Tag == (object)"Prime");

                        if (main_area.factor > 1)
                        {
                            skipper = (int)main_area.factor;
                        }
                    }

                    var area = all_areas.Find(x => x is ScrollBarArea);
                        
                    area.my_candles = history;
                    

                    if (all_areas.Exists(x => x.Tag == (object)"Prime"))
                    {
                        var main_chart = (CandleStickArea)all_areas.Find(x => x.Tag == (object)"Prime");
                        main_chart.BuildCandleSeries(history);

                        if (!have_indicators)
                        {
                            DrawChart();
                        }
                    }

                    indicator_number = indicators_count;
                }
            }
        }

        public void ProcessClearElem(IChartElement element)
        {
            throw new NotImplementedException();
        }

        public void ProcessElem(IChartElement element)
        {
            throw new NotImplementedException();
        }

        public void ProcessIndicator(IIndicator indicator)
        {
            lock (seria_locker)
            {
                var seria_indi = series.Find(x => x.AreaName == indicator.NameArea);

                seria_indi.series_counter -= 1;

                if (seria_indi.series_counter != 0)
                {
                    return;
                }

                if (isPaint == false || (string.IsNullOrWhiteSpace(indicator.NameSeries) && indicator.ValuesToChart != null))
                {
                    return;
                }

                OxyArea area = all_areas.Find(x => (string)x.Tag == indicator.NameArea);

                if (indicator.ValuesToChart != null)
                {
                    if (indicator.PaintOn == false)
                    {
                        List<List<decimal>> values = indicator.ValuesToChart;

                        for (int i = 0; i < values.Count; i++)
                        {
                            //var seria = series.Find(x => x.SeriaName == name + i.ToString());
                        }

                        return;
                    }

                    List<List<decimal>> val_list = indicator.ValuesToChart;
                    List<Color> colors = indicator.Colors;
                    string name = indicator.Name;

                    Parallel.For(0, val_list.Count, i =>
                    {
                        indicator_number--;


                        if (val_list[i] == null || val_list[i].Count == 0)
                        {
                            return;
                        }


                        var seria = series.Find(x => x.SeriaName == name + i.ToString());

                        if (seria.OxyColor != null)
                            seria.OxyColor = OxyColor.FromArgb(colors[i].A, colors[i].R, colors[i].G, colors[i].B);

                        seria.Count = val_list.Count;

                        area.BuildIndicatorSeries(seria, val_list[i], time_frame_span);
                    });

                    have_indicators = true;


                    if (indicator_number == 0)
                        DrawChart();

                    return;
                }




                Aindicator ind = (Aindicator)indicator;

                List<IndicatorDataSeries> indi_series = ind.DataSeries;

                Parallel.For(0, indi_series.Count, i =>
                {
                    indicator_number--;

                    if (indi_series[i].IsPaint == false)
                        return;

                    var seria = series.Find(x => x.SeriaName == indi_series[i].NameSeries);

                    seria.OxyColor = OxyColor.FromArgb(indi_series[i].Color.A, indi_series[i].Color.R, indi_series[i].Color.G, indi_series[i].Color.B);

                    seria.Count = indi_series.Count;

                    area.BuildIndicatorSeries(seria, indi_series[i].Values, time_frame_span);
                });

                have_indicators = true;

                if (indicator_number == 0)
                    DrawChart();
            }
        }

        public void ProcessPositions(List<Position> deals)
        {
            if (isPaint == false || deals == null || deals.Count == 0 )
            {
                return;
            }

            last_deals_count = deals.Count;

            if (all_areas.Exists(x => x.Tag == (object)"Prime"))
                ((CandleStickArea)all_areas.Find(x => x.Tag == (object)"Prime")).ProcessPositions(deals);
        }

        public void ProcessTrades(List<Trade> trades)
        {
            //обработка нужна для отображение аска бида. устарело
        }

        public void RefreshChartColor()
        {
            // кто нить другой сделает
        }

        public void RemoveCursor()
        {
            // устарело
        }

        public void RePaintIndicator(IIndicator indicatorCandle)
        {
            if (!indicatorCandle.PaintOn || indicatorCandle == null || indicatorCandle.NameArea == "Prime")
                return;

            if (!all_areas.Exists(x => (string)x.Tag == indicatorCandle.NameArea))
            {
                var indicator_chart = new IndicatorArea(new OxyAreaSettings()
                {
                    cursor_X_is_active = true,
                    cursor_Y_is_active = true,
                    Tag = indicatorCandle.NameArea,
                    AbsoluteMinimum = double.MinValue,
                    Y_Axies_is_visible = true,
                    X_Axies_is_visible = true,
                    brush_background = "#111721"
                }, all_areas, indicatorCandle.NameArea, this);

                indicator_chart.plot_model.Axes[0].TextColor = OxyColors.Transparent;
                indicator_chart.plot_model.Axes[0].TicklineColor = OxyColors.Transparent;
                indicator_chart.plot_model.Axes[0].AxisDistance = -50;
                indicator_chart.plot_model.Axes[1].IntervalLength = 10;
                indicator_chart.plot_model.Axes[1].MinorGridlineStyle = LineStyle.None;
                indicator_chart.plot_model.PlotMargins = new OxyThickness(0, indicator_chart.plot_model.PlotMargins.Top, indicator_chart.plot_model.PlotMargins.Right, indicator_chart.plot_model.PlotMargins.Bottom);
                indicator_chart.plot_model.Padding = new OxyThickness(0, 0, indicator_chart.plot_model.Padding.Right, 0);
                indicator_chart.plot_model.PlotMargins = new OxyThickness(0, 0, indicator_chart.plot_model.PlotMargins.Right, 0);
                indicator_chart.plot_view.Padding = new System.Windows.Thickness(0, 0, indicator_chart.plot_view.Padding.Right, 0);
                indicator_chart.plot_view.Margin = new System.Windows.Thickness(0, 0, indicator_chart.plot_view.Margin.Right, 0);

                all_areas.Add(indicator_chart);
            }

            if (!series.Exists(x => x.SeriaName == indicatorCandle.NameSeries && x.AreaName == indicatorCandle.NameArea))
            {
                var new_seria = new IndicatorSeria()
                {
                    AreaName = indicatorCandle.NameArea,
                    IndicatorType = indicatorCandle.TypeIndicator,
                    SeriaName = indicatorCandle.NameSeries
                };

                series.Add(new IndicatorSeria()
                {
                    AreaName = indicatorCandle.NameArea,
                    IndicatorType = indicatorCandle.TypeIndicator,
                    SeriaName = indicatorCandle.NameSeries
                });
            }

            if (main_grid_chart != null)
            {
                main_grid_chart.Children.Clear();
                main_grid_chart.RowDefinitions.Clear();

                MakeChart(main_grid_chart);
            }
        }

        public void SetBlackScheme()
        {
            // кто нить другой сделает не интересно
        }

        public void SetNewTimeFrame(TimeSpan timeFrameSpan, TimeFrame timeFrame)
        {
            time_frame_span = timeFrameSpan;
            time_frame = timeFrame;
        }

        public void SetPointSize(ChartPositionTradeSize pointSize)
        {
            // надо бы так то но лень
        }

        public void SetPointType(PointType type)
        {
            // надо бы так то но лень
        }

        public void SetWhiteScheme()
        {
            // кто нить другой сделает не интересно
        }

        public void ShowAreaOnChart()
        {
            foreach (var row in main_grid_chart.RowDefinitions.Where(x => (string)x.Tag != "Prime" && (string)x.Tag != "ScrollChart" && (string)x.Tag != "ControlPanel"))
            {
                string tag = (string)row.Tag;

                if (tag.Contains("GridSplitter_")) 
                    row.Height = new GridLength(2, GridUnitType.Pixel);
                else
                    row.Height = new GridLength(1, GridUnitType.Star);
            }
        }

        public void ShowContextMenu(System.Windows.Forms.ContextMenu menu)
        {
            if (panel_winforms == null)
                return;

            menu.Show(panel_winforms, new System.Drawing.Point(0, 0));
        }

        public void StartPaintPrimeChart(System.Windows.Controls.Grid grid_chart, WindowsFormsHost host, System.Windows.Shapes.Rectangle rectangle)
        {
            if (isPaint == true)
                return;

            isPaint = true;

            this.host = host;

            host.Width = 0;

            panel_winforms = new System.Windows.Forms.Panel()
            {
                Width = 0,
            };

            this.host.Child = panel_winforms;


            MakeChart(grid_chart);
        }

        public void MakeChart(System.Windows.Controls.Grid grid_chart)
        {
            System.Windows.Media.BrushConverter converter = new System.Windows.Media.BrushConverter();

            main_grid_chart = new System.Windows.Controls.Grid();
            main_grid_chart = grid_chart;
            main_grid_chart.Margin = new System.Windows.Thickness(25, 0, 0, 0);

            main_grid_chart.RowDefinitions.Add(new RowDefinition()
            {
                Tag = "Prime",
                Height = new System.Windows.GridLength(4, System.Windows.GridUnitType.Star)
            });

            var main_chart = new CandleStickArea(new OxyAreaSettings()
            {
                cursor_X_is_active = true,
                cursor_Y_is_active = true,
                Tag = "Prime",
                brush_background = "#111721"
            }, all_areas, this);

            main_chart.date_time_axis_X.MaximumMargin = 0;
            main_chart.date_time_axis_X.MinimumMargin = 0;
            main_chart.plot_view.Margin = new Thickness(0, main_chart.plot_view.Margin.Top, main_chart.plot_view.Margin.Right, main_chart.plot_view.Margin.Bottom);
            main_chart.plot_model.PlotMargins = new OxyThickness(0, main_chart.plot_model.PlotMargins.Top, main_chart.plot_model.PlotMargins.Right, main_chart.plot_model.PlotMargins.Bottom);
            main_chart.plot_model.Padding = new OxyThickness(0, main_chart.plot_model.Padding.Top, main_chart.plot_model.Padding.Right, main_chart.plot_model.Padding.Bottom);


            if (all_areas.Exists(x => (string)x.Tag == "Prime"))
            {
                var area_prime = all_areas.Find(x => (string)x.Tag == "Prime");

                ((CandleStickArea)area_prime).Dispose();

                all_areas.Remove(all_areas.Find(x => (string)x.Tag == "Prime"));
            }

            System.Windows.Controls.Grid.SetRow(main_chart.GetViewUI(), 0);
            System.Windows.Controls.Grid.SetColumn(main_chart.GetViewUI(), 0);

            main_grid_chart.Children.Add(main_chart.GetViewUI());

            all_areas.Add(main_chart);

            foreach (var area in all_areas.Where(x => (string)x.Tag != "Prime" && (string)x.Tag != "ScrollChart" && (string)x.Tag != "ControlPanel"))
            {
                main_grid_chart.RowDefinitions.Add(new RowDefinition()
                {
                    Tag = "GridSplitter_" + area.Tag,
                    Height = new System.Windows.GridLength(3),
                });

                GridSplitter grid_splitter = new GridSplitter()
                {
                    ShowsPreview = false,
                    Tag = area.Tag,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                    VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                    Background = (System.Windows.Media.Brush)converter.ConvertFromString("#50BEFFD5"),
                };

                if (!splitters.Contains(grid_splitter))
                    splitters.Add(grid_splitter);

                System.Windows.Controls.Grid.SetColumn(grid_splitter, 0);
                System.Windows.Controls.Grid.SetRow(grid_splitter, main_grid_chart.RowDefinitions.Count - 1);

                main_grid_chart.Children.Add(grid_splitter);

                main_grid_chart.RowDefinitions.Add(new RowDefinition()
                {
                    Tag = area.Tag,
                    Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star),
                });

                System.Windows.Controls.Grid.SetRow(area.GetViewUI(), main_grid_chart.RowDefinitions.Count - 1);
                System.Windows.Controls.Grid.SetColumn(area.GetViewUI(), 0);

                main_grid_chart.Children.Add(area.GetViewUI());
            }

            if (all_areas.Exists(x => (string)x.Tag == "ScrollChart"))
            {
                var area_scroll = all_areas.Find(x => (string)x.Tag == "ScrollChart");

                ((ScrollBarArea)area_scroll).Dispose();

                all_areas.Remove(all_areas.Find(x => (string)x.Tag == "ScrollChart"));
            }

            var scroll_chart = new ScrollBarArea(new OxyAreaSettings()
            {
                brush_background = "#282E38",
                brush_scroll_bacground = "#282E38",
                cursor_X_is_active = true,
                Tag = "ScrollChart",
            }, all_areas, this);

            scroll_chart.date_time_axis_X.MaximumPadding = 0;
            scroll_chart.date_time_axis_X.MinimumPadding = 0;
            scroll_chart.plot_model.Padding = new OxyThickness(0, 0, 0, 0);
            scroll_chart.plot_model.PlotMargins = new OxyThickness(0, 0, 0, 0);
            scroll_chart.plot_model.PlotAreaBorderThickness = new OxyThickness(1, 1, 2, 2);
            scroll_chart.plot_model.PlotAreaBorderColor = OxyColor.Parse("#50BEFFD5");

            main_grid_chart.RowDefinitions.Add(new RowDefinition()
            {
                Tag = "ScrollChart",
                Height = new System.Windows.GridLength(40),
            });

            System.Windows.Controls.Grid.SetRow(scroll_chart.GetViewUI(), main_grid_chart.RowDefinitions.Count - 1);
            System.Windows.Controls.Grid.SetColumn(scroll_chart.GetViewUI(), 0);

            main_grid_chart.Children.Add(scroll_chart.GetViewUI());

            all_areas.Add(scroll_chart);



            if (start_program != StartProgram.IsOsData)
            {
                var control_panel = new ControlPanelArea(new OxyAreaSettings()
                {
                    brush_background = "#111721",
                    Tag = "ControlPanel",
                }, all_areas, this);


                if (all_areas.Exists(x => (string)x.Tag == "ControlPanel"))
                {
                    var area_control = all_areas.Find(x => (string)x.Tag == "ControlPanel");

                    ((ControlPanelArea)area_control).Dispose();

                    all_areas.Remove(all_areas.Find(x => (string)x.Tag == "ControlPanel"));
                }


                main_grid_chart.RowDefinitions.Add(new RowDefinition()
                {
                    Tag = "ControlPanel",
                    Height = new System.Windows.GridLength(40),
                });

                System.Windows.Controls.Grid.SetRow(control_panel.GetViewUI(), main_grid_chart.RowDefinitions.Count - 1);
                System.Windows.Controls.Grid.SetColumn(control_panel.GetViewUI(), 0);

                main_grid_chart.Children.Add(control_panel.GetViewUI());

                all_areas.Add(control_panel);

                control_panel.Calculate(time_frame_span, time_frame);
                control_panel.Redraw();
                control_panel.plot_model.InvalidatePlot(true);

                
            }

            if (all_areas.Count > 3)
            {
                for (int i = 0; i < all_areas.Count; i++)
                {
                    if ((string)all_areas[i].Tag != "ScrollChart" && (string)all_areas[i].Tag != "Prime" && (string)all_areas[i].Tag != "ControlPanel")
                    {
                        var axes = all_areas[i].plot_model.Axes.ToList().Find(x => x.Key == "DateTime");

                        axes.TextColor = OxyColors.Transparent;
                        axes.TicklineColor = OxyColors.Transparent;
                        axes.AxisDistance = -50;
                        axes.MaximumPadding = 0;
                        axes.MinimumPadding = 0;

                        all_areas[i].plot_model.Padding = new OxyThickness(0, 0, all_areas[i].plot_model.Padding.Right, 0);
                        all_areas[i].plot_model.PlotMargins = new OxyThickness(0, 0, all_areas[i].plot_model.PlotMargins.Right, 0);
                        all_areas[i].plot_view.Padding = new System.Windows.Thickness(0, 0, all_areas[i].plot_view.Padding.Right, 0);
                        all_areas[i].plot_view.Margin = new System.Windows.Thickness(0, 0, all_areas[i].plot_view.Margin.Right, 0);
                    }
                    else
                    {
                        //цвет прозрачный + смещение вниз 
                    }
                }
            }
        }


        private void DrawChart()
        {

            var main_area = ((CandleStickArea)all_areas.Find(x => x.Tag == (object)"Prime"));

            foreach (var area in all_areas)
            {
                area.Calculate(time_frame_span, time_frame);
            }

            //Parallel.For(0, areas_to_draw.Count(), i =>
            //{
            //    areas_to_draw[i].Redraw();
            //});

            //var result = all_areas.AsParallel().Select(x =>
            //{
            //    x.Calculate(time_frame_span, time_frame);
            //    return 1;
            //});


            foreach (var seria in series)
            {
                if (seria.DataPoints.Count != main_area.items_oxy_candles.Count && seria.DataPoints.Count != 0 ||
                    seria.IndicatorHistogramPoints.Count != main_area.items_oxy_candles.Count && seria.IndicatorHistogramPoints.Count != 0 ||
                    seria.IndicatorPoints.Count != main_area.items_oxy_candles.Count && seria.IndicatorPoints.Count != 0 ||
                    seria.IndicatorScatterPoints.Count != main_area.items_oxy_candles.Count && seria.IndicatorScatterPoints.Count != 0)
                {
                    return;
                }
            }

            List<OxyArea> areas_to_draw = all_areas.Where(x => x is IndicatorArea || x is CandleStickArea).ToList();

            foreach (var area in all_areas.Where(x => x is IndicatorArea || x is CandleStickArea))
            {
                area.Redraw();
            }




            Task delay = new Task(() =>
            {
                Thread.Sleep(25);
            });

            delay.Start();
            delay.Wait();


            if (main_area.factor < 1)
            {
                Task task = new Task(() =>
                {
                    Thread.Sleep((int)(100 / main_area.factor));
                });

                task.Start();
                task.Wait();
            }
        }

        public void StopPaint()
        {
            isPaint = false;

            seria_locker = new object();

            if (main_grid_chart != null)
            {
                main_grid_chart.RowDefinitions.Clear();
                main_grid_chart.Children.Clear();
                main_grid_chart = null;
            }
        }
    }
}
