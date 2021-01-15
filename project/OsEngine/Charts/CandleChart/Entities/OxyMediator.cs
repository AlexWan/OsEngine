using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Charts.CandleChart.OxyAreas;
using OsEngine.Entity;
using OsEngine.Indicators;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OsEngine.Charts.CandleChart.Entities
{
    public class OxyMediator
    {
        public CandleStickArea prime_chart;

        public List<IndicatorArea> indicators_list = new List<IndicatorArea>();

        public ScrollBarArea scroll_bar;

        public ControlPanelArea control_panel;

        public OxyChartPainter owner;

        public Task delay = new Task(() => { return; });

        bool can_redraw_prime = true;
        bool can_redraw_indicators = true;
        bool can_redraw_scroll = true;
        bool can_redraw_all = true;

        bool can_zoom_prime = true;
        bool can_zoom_indi = true;

        public double[] factor_array = new double[] { 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024 };
        public int factor_selector = 6;
        public double factor = 1;
        public int count_skiper;
        public bool is_first_start = true;

        public object in_progress = new object();


        public OxyMediator(OxyChartPainter owner)
        {
            this.owner = owner;

            owner.UpdateCandlesEvent += Owner_UpdateCandlesEvent; 
            owner.UpdateIndicatorEvent += Owner_UpdateIndicatorEvent; 
        }

        private void Owner_UpdateIndicatorEvent()
        {
            if (OxyArea.my_candles == null || OxyArea.my_candles.Count == 0)
                return;

            MainRedraw();
        }

        private void Owner_UpdateCandlesEvent()
        {
            if (OxyArea.my_candles == null || OxyArea.my_candles.Count == 0)
                return;

            MainRedraw();
        }

        public void MainRedraw()
        {        
            bool need_to_redraw = true;

            foreach (var seria in owner.series)
            {
                if (seria.isHide)
                    continue;

                if (seria.DataPoints.Count != 0 && seria.DataPoints.Count != OxyArea.my_candles.Count)
                    need_to_redraw = false;
            }

            if (need_to_redraw || (indicators_list.Count == 0 && owner.series.Where(x => x.AreaName == "Prime").Count() == 0))
            {
                RedrawAll(null);

                if (factor > 1)
                    count_skiper = (int)factor;

                if (is_first_start == true && indicators_list.Select(x => x.plot_model.ActualPlotMargins.Right == 0) != null)
                {
                    is_first_start = false;

                    delay = new Task(() =>
                    {           
                        Delay(500).Wait(1000);

                        RedrawAll(null);

                        Delay(500).Wait(1000);
                    });

                    delay.Start();
                }
            }
        }

        public void RemoveOxyArea(OxyArea oxy_area)
        {
            indicators_list.Remove((IndicatorArea)oxy_area);
        }

        public void PrimeChart_BuildCandleSeries()
        {
            if (prime_chart != null)
                prime_chart.BuildCandleSeries();
        }

        public void BuildIndicatorSeries(OxyArea oxy_area, IndicatorSeria seria, List<decimal> values, TimeSpan time_frame_span)
        {
            oxy_area.BuildIndicatorSeries(seria, values, time_frame_span);
        }

        public void ProcessPositions(List<Position> positions)
        {
            if (prime_chart != null)
            prime_chart.ProcessPositions(positions);
        }

        public void AddOxyArea(OxyArea oxy_area)
        {
            if (oxy_area is CandleStickArea)
            {
                if (prime_chart != null)
                {
                    prime_chart.Updated -= UpdateDoneEvent;
                    prime_chart.Redrawed -= RedrawDoneEvent;
                }

                prime_chart = (CandleStickArea)oxy_area;
                oxy_area.Redrawed += RedrawDoneEvent;
                oxy_area.Updated += UpdateDoneEvent;
            }

            if (oxy_area is IndicatorArea)
            {
                for (int i = 0; i < indicators_list.Count; i++)
                {
                    if (indicators_list[i].Tag == oxy_area.Tag)
                    {
                        indicators_list[i].Redrawed -= RedrawDoneEvent;
                        indicators_list[i].Updated -= UpdateDoneEvent;

                        indicators_list[i] = (IndicatorArea)oxy_area;

                        indicators_list[i].Redrawed += RedrawDoneEvent;
                        indicators_list[i].Updated += UpdateDoneEvent;

                        return;
                    }
                }

                indicators_list.Add((IndicatorArea)oxy_area);
                oxy_area.Redrawed += RedrawDoneEvent;
                oxy_area.Updated += UpdateDoneEvent;
            }

            if (oxy_area is ScrollBarArea)
            {
                scroll_bar = (ScrollBarArea)oxy_area;
                oxy_area.Redrawed += RedrawDoneEvent;
                oxy_area.Updated += UpdateDoneEvent;
            }

            if (oxy_area is ControlPanelArea)
            {
                control_panel = (ControlPanelArea)oxy_area;
                oxy_area.Redrawed += RedrawDoneEvent;
                oxy_area.Updated += UpdateDoneEvent;
            }
        }

        public void RedrawDoneEvent(OxyArea oxyArea)
        {
            if (oxyArea is CandleStickArea)
            {
                //prime_wait_handle.Set();
            }
        }

        public void UpdateDoneEvent(OxyArea oxyArea)
        {

        }

        public void ZoomIndicators(double x_start, double x_end)
        {
            if (!can_zoom_indi)
                return;

            can_zoom_indi = false;

            foreach (var area in indicators_list)
            {
                if ((area.plot_model.Axes[0].ActualMinimum == x_start &&
                    area.plot_model.Axes[0].ActualMaximum == x_end))
                {
                    continue;
                }

                Action action_zoom_chart_by_screen_position = () =>
                {
                    double X_min_value = x_start;
                    double X_max_value = x_end;

                    area.date_time_axis_X.Zoom(X_min_value, X_max_value);

                    List<double> max_min = new List<double>();

                    max_min = area.GetHighLow(false, X_min_value, X_max_value);

                    area.linear_axis_Y.Zoom(max_min[0], max_min[1]);
                };

                area.actions_to_calculate.Enqueue(action_zoom_chart_by_screen_position);

                area.Calculate(owner.time_frame_span, owner.time_frame);
                area.Redraw();
            }

            can_zoom_indi = true;
        }

        public void ZoomPrime(double x_start, double x_end)
        {
            if (!can_zoom_prime)
                return;

            can_zoom_prime = false;

            if (prime_chart.plot_model.Axes[0].ActualMinimum == x_start &&
                prime_chart.plot_model.Axes[0].ActualMaximum == x_end)
            {
                can_zoom_prime = true;
                return;
            }

            Action action_zoom_main_chart_by_screen_position = () =>
            {
                double X_min_value = x_start;
                double X_max_value = x_end;

                prime_chart.date_time_axis_X.Zoom(X_min_value, X_max_value);

                List<double> max_min = new List<double>();

                max_min = prime_chart.GetHighLow(true, X_min_value, X_max_value);

                prime_chart.linear_axis_Y.Zoom(max_min[0], max_min[1]);
            };

            prime_chart.actions_to_calculate.Enqueue(action_zoom_main_chart_by_screen_position);

            RedrawPrime(false);

            can_zoom_prime = true;
        }

        public void RedrawAll(OxyArea excluded_area)
        {
            if (!can_redraw_all)
                return;

            can_redraw_all = false;

            lock (in_progress)
            {
                if (owner.start_program == StartProgram.IsTester || owner.start_program == StartProgram.IsOsOptimizer)
                {
                    RedrawPrime(false);
                    RedrawIndiAreas(excluded_area, false);
                    RedrawScroll(false);
                    RedrawControlPanel(false);


                    delay = new Task(() =>
                    {
                        Delay(25).Wait(50);
                    });

                    delay.Start();
                    delay.Wait(100);

                    can_redraw_all = true;
                }
                else
                {
                    Task.Run(() =>
                    {
                        RedrawPrime(false);
                        RedrawIndiAreas(excluded_area, false);
                        RedrawScroll(false);
                        RedrawControlPanel(false);

                        delay = new Task(() =>
                        {
                            Delay(25).Wait(50);
                        });

                        delay.Start();
                        delay.Wait(100);

                        can_redraw_all = true;
                    });
                }
            }
        }

        private async Task Delay(int millisec)
        {
            await Task.Delay(millisec);
        }

        public void RedrawPrime( bool nead_to_delay)
        {
            if (!can_redraw_prime)
                return;

            can_redraw_prime = false;

            if (prime_chart == null)
            {
                can_redraw_prime = true;
                return;
            }

                prime_chart.Calculate(owner.time_frame_span, owner.time_frame);
                prime_chart.Redraw();

            if (nead_to_delay)
            {
                delay = new Task(() =>
                {
                    Delay(25).Wait(50);
                });

                delay.Start();
                delay.Wait(100);
            }

            can_redraw_prime = true;
        }

        public void RedrawIndiAreas(OxyArea excluded_area, bool nead_to_delay)
        {
            if (!can_redraw_indicators)
                return;

            can_redraw_indicators = false;

            foreach (var indicator in indicators_list)
            {
                if (indicator == excluded_area)
                    continue;

                indicator.Calculate(owner.time_frame_span, owner.time_frame);
            }

            foreach (var indicator in indicators_list)
            {
                if (indicator == excluded_area)
                    continue;

                indicator.Redraw();
            }

            if (nead_to_delay)
            {
                delay = new Task(() =>
                {
                    Delay(25).Wait(50);
                });

                delay.Start();
                delay.Wait(100);
            }

            can_redraw_indicators = true;
        }

        public void RedrawScroll(bool nead_to_delay)
        {
            if (!can_redraw_scroll)
                return;

            can_redraw_scroll = false;
            try
            {
                if (scroll_bar == null)
                {
                    can_redraw_scroll = true;
                    return;
                }

                scroll_bar.Calculate(owner.time_frame_span, owner.time_frame);
                scroll_bar.Redraw();
            }
            catch { can_redraw_scroll = true; return; }

            if (nead_to_delay)
            {
                delay = new Task(() =>
                {
                    Delay(25).Wait(50);
                });

                delay.Start();
                delay.Wait(100);
            }

            can_redraw_scroll = true;
        }

        public void RedrawControlPanel( bool nead_to_delay)
        {
            try
            {
                if (control_panel == null)
                    return;

                control_panel.Calculate(owner.time_frame_span, owner.time_frame);
                control_panel.Redraw();
            }
            catch { return; }

            if (nead_to_delay)
            {
                delay = new Task(() =>
                {
                    Delay(25).Wait(50);
                });

                delay.Start();
                delay.Wait(100);
            }
        }

        

        public void ProcessElem(IChartElement element)
        {
            if (element is LineHorisontal)
            {
                var indi_area = indicators_list.Find(x => (string)x.Tag == element.Area);

                if (indi_area.lines_series_list.Exists(x => (string)x.Tag == element.UniqName + "HorLine"))
                    indi_area.lines_series_list.Remove(indi_area.lines_series_list.Find(x => (string)x.Tag == element.UniqName + "HorLine"));

                var line = new LineSeries()
                {
                    Color = OxyColor.FromArgb(((LineHorisontal)element).Color.A, ((LineHorisontal)element).Color.R, ((LineHorisontal)element).Color.G, ((LineHorisontal)element).Color.B),
                    MarkerStrokeThickness = 1,
                    StrokeThickness = 1,
                    MarkerStroke = OxyColor.FromArgb(((LineHorisontal)element).Color.A, ((LineHorisontal)element).Color.R, ((LineHorisontal)element).Color.G, ((LineHorisontal)element).Color.B),
                    Tag = (object)(element.UniqName + "HorLine"),
                };

                line.Points.AddRange(new List<DataPoint>() {
                    new DataPoint(DateTimeAxis.ToDouble(((LineHorisontal)element).TimeStart), (double)((LineHorisontal)element).Value),
                    new DataPoint(DateTimeAxis.ToDouble(((LineHorisontal)element).TimeEnd), (double)((LineHorisontal)element).Value)
                });

                indi_area.lines_series_list.Add(line);
            }

            if (element is PointElement)
            {
                MarkerType shape = MarkerType.None;

                int size = (int)(((PointElement)element).Size / 2);

                double stroke_thickness = 1;

                if (((PointElement)element).Style == System.Windows.Forms.DataVisualization.Charting.MarkerStyle.Circle)
                    shape = MarkerType.Circle;

                else if (((PointElement)element).Style == System.Windows.Forms.DataVisualization.Charting.MarkerStyle.Diamond)
                    shape = MarkerType.Diamond;

                else if (((PointElement)element).Style == System.Windows.Forms.DataVisualization.Charting.MarkerStyle.Square)
                    shape = MarkerType.Square;

                else if (((PointElement)element).Style == System.Windows.Forms.DataVisualization.Charting.MarkerStyle.Cross)
                {
                    shape = MarkerType.Cross;
                    stroke_thickness = size / 2;
                }

                else if (((PointElement)element).Style == System.Windows.Forms.DataVisualization.Charting.MarkerStyle.Triangle)
                    shape = MarkerType.Triangle;

                else if (((PointElement)element).Style == System.Windows.Forms.DataVisualization.Charting.MarkerStyle.None)
                    shape = MarkerType.None;

                else
                {
                    shape = MarkerType.Star;
                    stroke_thickness = size / 4;
                }

                PointAnnotation point = new PointAnnotation()
                {
                    X = DateTimeAxis.ToDouble(((PointElement)element).TimePoint),
                    Y = (double)((PointElement)element).Y,
                    Layer = AnnotationLayer.AboveSeries,
                    Fill = OxyColor.FromArgb(((PointElement)element).Color.A, ((PointElement)element).Color.R, ((PointElement)element).Color.G, ((PointElement)element).Color.B),
                    Shape = shape,
                    Size = size,
                    StrokeThickness = stroke_thickness,
                    Stroke = OxyColor.FromArgb(((PointElement)element).Color.A, ((PointElement)element).Color.R, ((PointElement)element).Color.G, ((PointElement)element).Color.B),
                    Tag = "point"
                };


                OxyArea area;

                if (indicators_list.Exists(x => (string)x.Tag == element.Area))
                {
                    area = indicators_list.Find(x => (string)x.Tag == element.Area);

                    area.plot_model.Annotations.Add(point);
                }

                if (element.Area == "Prime")
                {
                    prime_chart.plot_model.Annotations.Add(point);
                }
            }
        }

        public void Dispose()
        {
            if (owner != null)
            {
                owner.UpdateCandlesEvent -= Owner_UpdateCandlesEvent;
                owner.UpdateIndicatorEvent -= Owner_UpdateIndicatorEvent;
            }

            if (prime_chart != null)
            {
                prime_chart.Redrawed -= RedrawDoneEvent;
                prime_chart.Updated -= UpdateDoneEvent;
            }

            for (int i = 0; i < indicators_list.Count; i++)
            {
                if (indicators_list[i] != null)
                {
                    indicators_list[i].Redrawed -= RedrawDoneEvent;
                    indicators_list[i].Updated -= UpdateDoneEvent;
                }
            }

            if (scroll_bar != null)
            {
                scroll_bar.Redrawed -= RedrawDoneEvent;
                scroll_bar.Updated -= UpdateDoneEvent;
            }

            prime_chart = null;

            indicators_list = new List<IndicatorArea>();

            scroll_bar = null;
        }
    }
}
