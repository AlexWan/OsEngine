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
using OxyPlot.Wpf;
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
        public event Action<string, LogMessageType> LogMessageEvent;
        public event Action<int> ClickToIndexEvent;
        public event Action<int> SizeAxisXChangeEvent;
        public event Action<ChartClickType> ChartClickEvent;
        public Task delay = new Task(() => { return; });

        public bool IsPatternChart { get; set; }
        private WindowsFormsHost host;
        public StartProgram start_program;
        public string chart_name;
        public string bot_name;
        public int bot_tab;
        private ChartMasterColorKeeper color_keeper;
        private System.Windows.Forms.Panel panel_winforms;
        private bool isPaint = false;
        private System.Windows.Controls.Grid main_grid_chart;
        public TimeSpan time_frame_span = new TimeSpan();
        public TimeFrame time_frame = new TimeFrame();

        private List<GridSplitter> splitters = new List<GridSplitter>();
        public OxyMediator mediator;

        public List<IndicatorSeria> series = new List<IndicatorSeria>();
        private List<OxyArea> all_areas = new List<OxyArea>();

        public event Action UpdateCandlesEvent;
        public event Action UpdateIndicatorEvent;

        public bool can_draw = false;

        public OxyChartPainter(string name, StartProgram startProgram)
        {
            this.chart_name = name;
            start_program = startProgram;
            color_keeper = new ChartMasterColorKeeper(name);
            mediator = new OxyMediator(this);
        }

        public void SendCandlesUpdated()
        {
            UpdateCandlesEvent?.Invoke();
        }

        public void SendIndicatorsUpdated()
        {
            UpdateIndicatorEvent?.Invoke();
        }

        public void MainChartMouseButtonClick(ChartClickType click_type)
        {
            ChartClickEvent?.Invoke(click_type);
        }

        public bool AreaIsCreate(string area_name)
        {
            if (area_name == "Prime")
                return true;

            foreach (var area in all_areas)
            {
                if (area.Tag == (object)area_name)
                    return true;
            }

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
                if (all_areas[i].chart_name == this.chart_name)
                {
                    all_areas[i].Dispose();
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

            indicator_chart.indicator_name = nameArea.Replace("Area", ";").Split(';')[0];
            indicator_chart.bot_tab = this.bot_tab;

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
            mediator.AddOxyArea(indicator_chart);

            return nameArea;
        }

        public string CreateSeries(string areaName, IndicatorChartPaintType indicatorType, string seria_name)
        {
            if (series.Exists(x => (string)x.SeriaName == seria_name))
            {
                return seria_name;
            }

            var new_seria = new IndicatorSeria()
            {
                AreaName = areaName,
                IndicatorType = indicatorType,
                SeriaName = seria_name,
                BotTab = this.bot_tab
            };

            if (!series.Contains(new_seria))
            {
                series.Add(new IndicatorSeria()
                {
                    AreaName = areaName,
                    IndicatorType = indicatorType,
                    SeriaName = seria_name,
                    BotTab = this.bot_tab
                });
            }

            return seria_name;
        }

        public void CreateTickArea()
        {
            throw new NotImplementedException();
        }

        public void Delete()
        {
            Task delay = new Task(() =>
            {
                Thread.Sleep(1000);
            });

            delay.Start();
            delay.Wait();


            color_keeper.Delete();

            series.Clear();
            time_frame_span = new TimeSpan();
            time_frame = new TimeFrame();

            for (int i = 0; i < all_areas.Count; i++)
            {
                if (all_areas[i].chart_name == this.chart_name)
                {
                    all_areas[i].Dispose();
                }

                all_areas[i] = null;
            }

            mediator.Dispose();
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
            series.Remove(series.Find(x => x.SeriaName == indicator.NameSeries));

            if (indicator.NameArea == "Prime")              
                return;
            
            var area = all_areas.Find(x => ((string)x.Tag).Contains(indicator.NameArea) &&  x.chart_name == this.chart_name);
            var splitter = splitters.Find(x => (string)x.Tag == indicator.NameArea);

            ((IndicatorArea)area).Dispose();

            mediator.RemoveOxyArea(area);
            all_areas.Remove(area);
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
            GoChartToTime(OxyArea.my_candles[index].TimeStart);
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
                        mediator.PrimeChart_BuildCandleSeries();
                        main_area.Calculate(area.owner.time_frame_span, area.owner.time_frame);
                    }

                    double first_value = area.plot_model.Axes[0].ActualMinimum;
                    double last_value = area.plot_model.Axes[0].ActualMinimum;

                    double main_volume = DateTimeAxis.ToDouble(time);

                    area.date_time_axis_X.Zoom(main_volume - (last_value - first_value) / 2, main_volume + (last_value - first_value) / 2);

                    List<double> max_min = new List<double>();

                    if (area is CandleStickArea)
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
                return false;            
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
            if (isPaint == false)
            {
                return;
            }

            if (mediator.count_skiper > 0)
                mediator.count_skiper--;

            if (mediator.count_skiper > 0)
            {
                can_draw = false;
                return;
            }

            if (mediator.factor < 1)
            {
                Task delay = new Task(() =>
                {
                    Thread.Sleep((int)(50 / mediator.factor - 50));
                });

                delay.Start();
                delay.Wait();

                delay = new Task(() =>
                {
                    Delay((int)(50 / mediator.factor - 50)).Wait((int)(50 / mediator.factor - 50) + 50);
                });

                delay.Start();
                delay.Wait((int)(50 / mediator.factor - 50) + 100);
            }


            can_draw = true;

            OxyArea.my_candles = history;
     
            if (mediator.prime_chart != null)
            mediator.PrimeChart_BuildCandleSeries();

            if (mediator.prime_chart != null)
            UpdateCandlesEvent?.Invoke();
        }

        private async Task Delay(int millisec)
        {
            await Task.Delay(millisec);
        }

        public void ProcessClearElem(IChartElement element)
        {
            throw new NotImplementedException();
        }

        public void ProcessElem(IChartElement element)
        {
            if (isPaint == false || can_draw == false)
                return;

            mediator.ProcessElem(element);
        }

        public void ProcessIndicator(IIndicator indicator)
        {
            if (isPaint == false || can_draw == false)
                return;

            if (!series.Exists(x => x.AreaName == indicator.NameArea))
                return;

            OxyArea area = all_areas.Find(x => (string)x.Tag == indicator.NameArea);

            if (indicator.ValuesToChart != null)
            {
                if (indicator.PaintOn == false)
                {
                    UpdateIndicatorEvent?.Invoke(); // тут может быть косяк так как некоторые индюки нужно прорисовывать даже на false
                    return;
                }

                List<List<decimal>> val_list = indicator.ValuesToChart;
                List<Color> colors = indicator.Colors;
                string name = indicator.Name;


                Parallel.For(0, val_list.Count, i =>
                {
                    if (val_list[i] == null || val_list[i].Count == 0)
                    {
                        return;
                    }

                    var seria = series.Find(x => x.SeriaName == name + i.ToString());

                    if (seria.OxyColor != null)
                        seria.OxyColor = OxyColor.FromArgb(colors[i].A, colors[i].R, colors[i].G, colors[i].B);

                    seria.Count = val_list.Count;

                    try
                    {
                        if (area != null)
                            area.BuildIndicatorSeries(seria, val_list[i], time_frame_span);
                    }
                    catch { return; }
                });



                UpdateIndicatorEvent?.Invoke();

                return;
            }

            Aindicator ind = (Aindicator)indicator;
            List<IndicatorDataSeries> indi_series = ind.DataSeries;


            Parallel.For(0, indi_series.Count, i =>
            {
                if (indi_series[i].IsPaint == false)
                    return;

                var seria = series.Find(x => x.SeriaName == indi_series[i].NameSeries);

                seria.OxyColor = OxyColor.FromArgb(indi_series[i].Color.A, indi_series[i].Color.R, indi_series[i].Color.G, indi_series[i].Color.B);

                seria.Count = indi_series.Count;
                try
                {
                    if (area != null)
                        mediator.BuildIndicatorSeries(area, seria, indi_series[i].Values, time_frame_span);
                }
                catch { return; }
            });


            UpdateIndicatorEvent?.Invoke();
        }

        public void ProcessPositions(List<Position> deals)
        {
            if (isPaint == false || can_draw == false)
                return;

            if (deals == null || deals.Count == 0)
                return;          

            mediator.ProcessPositions(deals);
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
            if (isPaint == false || can_draw == false)
                return;

            if (indicatorCandle == null)
                return;

            if (indicatorCandle.NameSeries != null)
            {
                if (!indicatorCandle.PaintOn)
                    return;

                if (!all_areas.Exists(x => (string)x.Tag == indicatorCandle.NameArea) && indicatorCandle.NameArea != "Prime")
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

                    indicator_chart.indicator_name = indicatorCandle.NameArea.Replace("Area", ";").Split(';')[0];
                    indicator_chart.bot_tab = this.bot_tab;
                    indicator_chart.bot_name = this.bot_name;

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
                    mediator.AddOxyArea(indicator_chart);
                }

                if (!series.Exists(x => x.SeriaName == indicatorCandle.NameSeries && x.AreaName == indicatorCandle.NameArea))
                {
                    var indi_area = all_areas.FindLast(x => (string)x.Tag == indicatorCandle.NameArea);

                    if (indi_area == null)
                        return;

                    if (!indicatorCandle.NameSeries.StartsWith(this.bot_name))
                        return;

                    var new_seria = new IndicatorSeria()
                    {
                        AreaName = indicatorCandle.NameArea,
                        IndicatorType = indicatorCandle.TypeIndicator,
                        SeriaName = indicatorCandle.NameSeries,
                        BotTab = this.bot_tab
                    };

                    if (!series.Contains(new_seria))
                    {
                        series.Add(new IndicatorSeria()
                        {
                            AreaName = indicatorCandle.NameArea,
                            IndicatorType = indicatorCandle.TypeIndicator,
                            SeriaName = indicatorCandle.NameSeries,
                            BotTab = this.bot_tab
                        });
                    }
                }
            }
            else
            {
                foreach (var ser_name in ((Aindicator)indicatorCandle).DataSeries)
                {
                    if (!ser_name.IsPaint)
                        continue;

                    string seria_name = ser_name.NameSeries;

                    if (!all_areas.Exists(x => (string)x.Tag == indicatorCandle.NameArea) && indicatorCandle.NameArea != "Prime")
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

                        indicator_chart.indicator_name = indicatorCandle.NameArea.Replace("Area", ";").Split(';')[0];
                        indicator_chart.bot_tab = this.bot_tab;
                        indicator_chart.bot_name = this.bot_name;

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
                        mediator.AddOxyArea(indicator_chart);
                    }

                    if (!series.Exists(x => x.SeriaName == seria_name && x.AreaName == indicatorCandle.NameArea))
                    {
                        var indi_area = all_areas.FindLast(x => (string)x.Tag == indicatorCandle.NameArea);

                        if (indi_area == null)
                            return;

                        if (!seria_name.StartsWith(this.bot_name))
                            return;

                        var new_seria = new IndicatorSeria()
                        {
                            AreaName = indicatorCandle.NameArea,
                            IndicatorType = indicatorCandle.TypeIndicator,
                            SeriaName = seria_name,
                            BotTab = this.bot_tab
                        };

                        if (!series.Contains(new_seria))
                        {
                            series.Add(new IndicatorSeria()
                            {
                                AreaName = indicatorCandle.NameArea,
                                IndicatorType = indicatorCandle.TypeIndicator,
                                SeriaName = seria_name,
                                BotTab = this.bot_tab
                            });
                        }
                    }
                }
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
            this.time_frame_span = timeFrameSpan;
            this.time_frame = timeFrame;

            foreach (var area in all_areas)
            {
                area.time_frame_span = timeFrameSpan;
                area.time_frame = timeFrame;
            }
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
                brush_background = "#111721",
                AxislineStyle = LineStyle.Solid,
            }, all_areas, this);

            main_chart.chart_name = this.chart_name;
            main_chart.date_time_axis_X.MaximumMargin = 0;
            main_chart.date_time_axis_X.MinimumMargin = 0;
            main_chart.plot_view.Margin = new Thickness(0, main_chart.plot_view.Margin.Top, main_chart.plot_view.Margin.Right, main_chart.plot_view.Margin.Bottom);
            main_chart.plot_model.PlotMargins = new OxyThickness(0, main_chart.plot_model.PlotMargins.Top, main_chart.plot_model.PlotMargins.Right, main_chart.plot_model.PlotMargins.Bottom);
            main_chart.plot_model.Padding = new OxyThickness(0, main_chart.plot_model.Padding.Top, main_chart.plot_model.Padding.Right, main_chart.plot_model.Padding.Bottom);
            main_chart.time_frame = this.time_frame;
            main_chart.time_frame_span = this.time_frame_span;


            if (all_areas.Exists(x => (string)x.Tag == "Prime" && x.chart_name == this.chart_name))
            {
                OxyArea area_prime = all_areas.Find(x => (string)x.Tag == "Prime" && x.chart_name == this.chart_name);

                area_prime.Dispose();

                all_areas.Remove(all_areas.Find(x => (string)x.Tag == "Prime" && x.chart_name == this.chart_name));
            }

            System.Windows.Controls.Grid.SetRow(main_chart.GetViewUI(), 0);
            System.Windows.Controls.Grid.SetColumn(main_chart.GetViewUI(), 0);

            main_grid_chart.Children.Add(main_chart.GetViewUI());

            all_areas.Add(main_chart);
            mediator.AddOxyArea(main_chart);

            var indi_areas = all_areas.Where(x => x is IndicatorArea);

            foreach (var area in indi_areas)
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

            scroll_chart.chart_name = this.chart_name;
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
            mediator.AddOxyArea(scroll_chart);

            if (start_program != StartProgram.IsOsData)
            {
                var control_panel = new ControlPanelArea(new OxyAreaSettings()
                {                  
                    brush_background = "#111721",
                    Tag = "ControlPanel",
                }, all_areas, this);

                control_panel.chart_name = this.chart_name;
                control_panel.plot_view.Height = 50;

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
                mediator.AddOxyArea(control_panel);

                control_panel.plot_model.InvalidatePlot(true);
                control_panel.Calculate(time_frame_span, time_frame);
                control_panel.Redraw();                   
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

        public void StopPaint()
        {
            isPaint = false;
            mediator.is_first_start = true;



            foreach (var area in all_areas)
            {
                area.Dispose();
            }

            if (main_grid_chart != null)
            {
                main_grid_chart.RowDefinitions.Clear();
                main_grid_chart.Children.Clear();
                main_grid_chart = null;
            }
        }
    }
}
