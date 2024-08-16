using OsEngine.Entity;
using OsEngine.OsMiner.Patterns;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;

/*Description
Sample “Chart in the parameters window” for osengine.

It shows:
• Dynamic graph: The graph updates in real time as new data becomes available.
• User interaction: The user can change the scale of the graph and get values ​​at specific points.
• Customizable parameters: Ability to select the spread calculation method and the maximum number of points on the chart.

*/

namespace OsEngine.Robots.TechSamples
{
    [Bot("CustomChartInParamWindowSample")]
    public class CustomChartInParamWindowSample : BotPanel
    {
        private BotTabSimple _tab0;

        private BotTabSimple _tab1;

        private StrategyParameterString _regimeSpread;

        private StrategyParameterInt _maxCountDotInLine;

        private WindowsFormsHost _host;

        private Chart _chart;

        private TextAnnotation _annotation;

        public CustomChartInParamWindowSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            TabCreate(BotTabType.Simple);

            _tab0 = TabsSimple[0];
            _tab1 = TabsSimple[1];

            _regimeSpread = CreateParameter("Regime calculation spread", "Subtraction", new string[] { "Subtraction", "Division" });

            _maxCountDotInLine = CreateParameter("Max count dot in line",10,10,100,10);

            this.ParamGuiSettings.Title = "CustomChartInParamWindowSample Parameters";
            this.ParamGuiSettings.Height = 300;
            this.ParamGuiSettings.Width = 600;
            CustomTabToParametersUi customTab = ParamGuiSettings.CreateCustomTab("Chart Spread");

            CreateChart();
            customTab.AddChildren(_host);

            Thread worker = new Thread(StartPaintChart);
            worker.Start();

            Description = "Sample “Chart in the parameters window” for osengine. " +
                "It shows: " +
                "• Dynamic graph: The graph updates in real time as new data becomes available. " +
                "• User interaction: The user can change the scale of the graph and get values ​​at specific points. " +
                "• Customizable parameters: Ability to select the spread calculation method and the maximum number of points on the chart.";
        }

        public override string GetNameStrategyType()
        {
            return "CustomChartInParamWindowSample";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        /// <summary>
        /// Prime thread work area / место работы потока прорисовывающего чарт
        /// </summary>
        private void StartPaintChart()
        {
            long countCandlesTab0 = 0;
            long countCandlesTab1 = 0;

            while (true)
            {
                try
                {
                    Thread.Sleep(1000);

                    if (_tab0.Securiti != null && _tab1.Securiti != null)
                    {
                        if (_tab0.CandlesFinishedOnly != null && _tab1.CandlesFinishedOnly != null)
                        {
                            if (countCandlesTab0 < _tab0.CandlesFinishedOnly.Count && countCandlesTab1 < _tab1.CandlesFinishedOnly.Count)
                            {
                                countCandlesTab0 = _tab0.CandlesFinishedOnly.Count;
                                countCandlesTab1 = _tab1.CandlesFinishedOnly.Count;

                                LoadValueOnChart();

                            }
                        }
                    }
                }
                catch(Exception ex)
                {
                    Thread.Sleep(5000);
                    _tab0.SetNewLogMessage(ex.ToString(),Logging.LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// create chart / создаем чарт 
        /// </summary>
        private void CreateChart()
        {
            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke(new Action(CreateChart));
                return;
            }

            try
            {
                _host = new WindowsFormsHost();
                _chart = new Chart();
                _host.Child = _chart;
                _host.Child.Show();

                _chart.Series.Clear();
                _chart.ChartAreas.Clear();

                ChartArea spreadArea = new ChartArea("ChartAreaSpread"); // создаём область на графике / create area on the chart
                spreadArea.CursorX.IsUserSelectionEnabled = true;
                spreadArea.CursorX.IsUserEnabled = true;
                spreadArea.CursorY.AxisType = AxisType.Secondary;
                spreadArea.Position.Height = 100;
                spreadArea.Position.X = 0;
                spreadArea.Position.Width = 100;
                spreadArea.Position.Y = 0;
                spreadArea.AxisX.Minimum = 0;
                spreadArea.BackColor = Color.Transparent;

                _chart.ChartAreas.Add(spreadArea); // добавляем область на чарт / add area on the chart

                _chart.ChartAreas[0].CursorX.LineColor = Color.Red;
                _chart.ChartAreas[0].CursorX.LineWidth = 2;
                _chart.BackColor = Color.Transparent;
                _chart.ChartAreas[0].AxisX.LabelStyle.ForeColor = Color.Gray;   // Цвет меток оси X / color X
                _chart.ChartAreas[0].AxisY2.LabelStyle.ForeColor = Color.Gray;    // Цвет меток оси Y / color Y

                // подписываемся на события изменения масштабов / subscribe to zoom events
                _chart.AxisScrollBarClicked += chart_AxisScrollBarClicked;
                _chart.AxisViewChanged += chart_AxisViewChanged;
                _chart.CursorPositionChanged += chart_CursorPositionChanged;

                // подписываемся событие обработки клика по графику / subscribe to the chart click processing event
                _chart.MouseClick += Chart_MouseClick;
            }
            catch (Exception ex)
            {
                _tab0.SetNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        // data spread / хранилище спреда
        private List<decimal> spreadData = new List<decimal>();

        // last calculation mode / последнее значение метода вычисления
        private string _lastCalculationMode = "";

        /// <summary>
        /// create data series / формирует серии данных
        /// </summary>
        private void LoadValueOnChart()
        {
            if(_regimeSpread.ValueString != _lastCalculationMode)
            {
                spreadData.Clear();
                _lastCalculationMode = _regimeSpread.ValueString;
            }

            if(spreadData.Count > _maxCountDotInLine.ValueInt)
            {
                while(spreadData.Count > _maxCountDotInLine.ValueInt)
                {
                    spreadData.RemoveAt(0);
                }
            }

            if (_tab0.CandlesFinishedOnly == null || _tab1.CandlesFinishedOnly == null)
            {
                return;
            }

            if (_regimeSpread.ValueString == "Subtraction")
            {
                spreadData.Add(Math.Abs((_tab0.PriceBestAsk - _tab0.PriceBestBid) - (_tab1.PriceBestAsk - _tab1.PriceBestBid)));
            }
            else
            {
                if ((_tab1.PriceBestAsk - _tab1.PriceBestBid) != 0)
                {
                    spreadData.Add((_tab0.PriceBestAsk - _tab0.PriceBestBid) / (_tab1.PriceBestAsk - _tab1.PriceBestBid));
                }
                else
                {
                    spreadData.Add(0);
                }
            }

            Series lineSeries = new Series("SeriesLine");
            lineSeries.ChartType = SeriesChartType.Line;
            lineSeries.YAxisType = AxisType.Secondary;
            lineSeries.ChartArea = "ChartAreaSpread";
            lineSeries.ShadowOffset = 2;
            lineSeries.YValuesPerPoint = 4;

            for (int i = spreadData.Count; i > 0; i--)
            {
                // забиваем новую точку / add new point
                lineSeries.Points.AddXY(i - 1, spreadData[i - 1]);
            }

            SetSeries(lineSeries);
        }

        /// <summary>
        /// loads data series onto the chart / подгружает серии данных на график 
        /// </summary>
        private void SetSeries(Series lineSeries)
        {
            try
            {
                if (MainWindow.GetDispatcher.CheckAccess() == false)
                {
                    MainWindow.GetDispatcher.Invoke(new Action<Series>(SetSeries),
                        lineSeries);
                    return;
                }

                _chart.Series.Clear(); // убираем с нашего графика все до этого созданные серии с данными / we remove from our chart all previously created series with data
                _chart.Series.Add(lineSeries);

                ChartArea SpreadArea = _chart.ChartAreas.FindByName("ChartAreaSpread");
                if (SpreadArea != null && SpreadArea.AxisX.ScrollBar.IsVisible)
                {
                    // сдвигаем представление вправо / move the view to the right
                    SpreadArea.AxisX.ScaleView.Scroll(_chart.ChartAreas[0].AxisX.Maximum);
                }
                ChartResize();
                _chart.Refresh();
            }
            catch (Exception ex)
            {
                _tab0.SetNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        // events / события

        private void chart_CursorPositionChanged(object sender, CursorEventArgs e)
        {
            ChartResize();
        }

        private void chart_AxisViewChanged(object sender, ViewEventArgs e)
        {
            ChartResize();
        }

        private void chart_AxisScrollBarClicked(object sender, ScrollBarEventArgs e)
        {
            ChartResize();
        }

        private void Chart_MouseClick(object sender, MouseEventArgs e)
        {
            try
            {
                // Получаем координаты клика / Getting click coordinates
                double x = e.X;

                // Преобразуем координаты клика в координаты на графике / Convert click coordinates to coordinates on the chart
                Axis xAxis = _chart.ChartAreas[0].AxisX;
                double xValueOnChart = xAxis.PixelPositionToValue(x);

                // находим ближайшее значение х / find the closest value of x
                int roundedX = (int)Math.Ceiling(xValueOnChart - 0.5);

                // Удаляем предыдущую аннотацию, если она существует / Remove the previous annotation if it exists
                if (_annotation != null)
                {
                    _chart.Annotations.Remove(_annotation);
                }

                // проверяем чтобы x не выходил за размер колекции / check that x does not exceed the size of the collection
                if (roundedX >= spreadData.Count)
                {
                    return;
                }

                // Создаем новую аннотацию с координатами ближайшей точки / Create a new annotation with the coordinates of the nearest point
                _annotation = new TextAnnotation
                {
                    Text = $"{roundedX}: {spreadData[roundedX]}",
                    X = 0,
                    Y = -1,
                    AnchorX = roundedX,
                    AnchorY = (double)spreadData[roundedX],
                    Font = new Font("Arial", 12, FontStyle.Bold),
                    ForeColor = Color.Gray,
                    BackColor = Color.Gray,
                    LineColor = Color.Gray,
                    AnchorAlignment = ContentAlignment.MiddleCenter
                };

                _chart.Annotations.Add(_annotation);
            }
            catch (Exception ex)
            {
                _tab0.SetNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        /// <summary>
        /// set the borders of the view along the Y axis / устанавливает границы представления по оси У
        /// </summary>
        private void ChartResize() 
        {
            try
            {
                if (spreadData.Count == 0)
                {
                    return;
                }

                Series lineSeries = _chart.Series.FindByName("SeriesLine");
                ChartArea spreadArea = _chart.ChartAreas.FindByName("ChartAreaSpread");

                if (spreadArea == null ||
                    lineSeries == null)
                {
                    return;
                }

                int start = 0;
                int end = lineSeries.Points.Count;

                if (_chart.ChartAreas[0].AxisX.ScrollBar.IsVisible)
                {
                    start = Convert.ToInt32(spreadArea.AxisX.ScaleView.Position);
                    end = Convert.ToInt32(spreadArea.AxisX.ScaleView.Position) +
                                  Convert.ToInt32(spreadArea.AxisX.ScaleView.Size);
                }

                spreadArea.AxisY2.Maximum = GetMaxValueOnChart(spreadData.ToArray(), start, end);
                spreadArea.AxisY2.Minimum = GetMinValueOnChart(spreadData.ToArray(), start, end);

                if (GetMinValueOnChart(spreadData.ToArray(), start, end) == GetMaxValueOnChart(spreadData.ToArray(), start, end))
                {
                    if (GetMinValueOnChart(spreadData.ToArray(), start, end) == 0)
                    {
                        spreadArea.AxisY2.Maximum = 1;
                    }
                    else 
                    {
                        spreadArea.AxisY2.Minimum = 0;
                    }
                }
            }
            catch (Exception error)
            {
                MessageBox.Show("Error when changing the width of the view. Error: " + error);
            }
        }

        /// <summary>
        /// takes the minimum value from the array / берёт минимальное значение из массива
        /// </summary>
        private double GetMinValueOnChart(decimal[] book, int start, int end)
        {
            double result = double.MaxValue;

            for (int i = start; i < end && i < book.Length; i++)
            {
                if ((double)book[i] < result)
                {
                    result = (double)book[i];
                }
            }

            return result;
        }

        /// <summary>
        /// takes the maximum value from the array / берёт максимальное значение из массива
        /// </summary>
        private double GetMaxValueOnChart(decimal[] book, int start, int end)
        {
            double result = 0;

            for (int i = start; i < end && i < book.Length; i++)
            {
                if ((double)book[i] > result)
                {
                    result = (double)book[i];
                }
            }

            return result;
        }
    }
}