/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using OsEngine.Charts.CandleChart;
using OsEngine.Charts.ColorKeeper;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.OsTrader.Panels.Tab;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace OsEngine.Charts.ClusterChart
{
    public class ChartClusterPainter
    {
        // service
        // сервис

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="name">name of the robot that owns the chart/имя робота, которому принадлежит чарт</param>
        /// <param name="startProgram">program that creates a class object/программа создающая объект класса</param>
        /// <param name="volume">volumes/объёмы</param>
        public ChartClusterPainter(string name, StartProgram startProgram, HorizontalVolume volume)
        {
            try
            {
                _volume = volume;
                _startProgram = startProgram;
                _colorKeeper = new ChartMasterColorKeeper(name);
                _colorKeeper.LogMessageEvent += SendLogMessage;

                CreateChart();

                _chart.Text = name;
                _chart.AxisViewChanged += _chart_AxisViewChanged; // zoom event/событие изменения масштаба

                _chart.MouseLeave += _chart_MouseLeave;

                Task task = new Task(PainterThreadArea);
                task.Start();

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// program that creates a class object
        /// программа создающая объект класса
        /// </summary>
        private StartProgram _startProgram;

        /// <summary>
        /// area where chart will be placed
        /// область на которой будет размещён чарт
        /// </summary>
        private WindowsFormsHost _host;

        /// <summary>
        /// element on which _host will be placed
        /// элемент на котором будет размещён _host
        /// </summary>
        private Rectangle _rectangle;

        /// <summary>
        /// candles chart
        /// свечной график
        /// </summary>
        private Chart _chart;

        private HorizontalVolume _volume;

        /// <summary>
        /// clusters
        /// кластеры
        /// </summary>
        private List<HorizontalVolumeLine> _myClusters;

        public ClusterType ChartType
        {
            get { return _chartType; }
            set
            {
                if (_chartType == value)
                {
                    return;
                }
                _chartType = value;
            }
        }

        private ClusterType _chartType;

        /// <summary>
        /// take a chart
        /// взять чарт
        /// </summary>
        /// <returns>returning the chart./возвращаем чарт</returns>
        public Chart GetChart()
        {
            return _chart;
        }

        /// <summary>
        /// начать прорисовку графика
        /// </summary>
        public void StartPaintPrimeChart(WindowsFormsHost host, Rectangle rectangle)
        {
            _host = host;
            _rectangle = rectangle;

            try
            {
                if (_host.Child != null && _host.Child.Text == _chart.Text)
                {
                    return;
                }

                if (_host != null && !_host.Dispatcher.CheckAccess())
                {
                    _host.Dispatcher.Invoke(new Action<WindowsFormsHost, Rectangle>(StartPaintPrimeChart), host, rectangle);
                    return;
                }
                _host.Child = _chart;
                _host.Child.Show();
                _rectangle.Fill =
                    new SolidColorBrush(System.Windows.Media.Color.FromArgb(_colorKeeper.ColorBackChart.A,
                        _colorKeeper.ColorBackChart.R, _colorKeeper.ColorBackChart.G, _colorKeeper.ColorBackChart.B));

                //ResizeYAxisOnArea("Prime");
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// остановить прорисовку графика
        /// </summary>
        public void StopPaint()
        {
            try
            {
                if (_host != null)
                {
                    _host.Child = null;
                }

                _host = null;
                _rectangle = null;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private DateTime _lastTimeClear;

        /// <summary>
        /// очистить график
        /// </summary>
        public void ClearDataPointsAndSizeValue()
        {
            try
            {
                if (_chart != null && _chart.InvokeRequired)
                {
                    _chart.Invoke(new Action(ClearDataPointsAndSizeValue));
                    return;
                }

                _lastMaxVolume = 0;

                bool neadToBackChild = false;

                if (_host != null && _host.Child != null)
                {
                    _host.Child = null;
                    neadToBackChild = true;
                }

                Series oldcandleSeries = FindSeriesByNameSafe("SeriesCluster");

                if (oldcandleSeries.Points.Count != 0)
                {
                    oldcandleSeries.Points.Clear();
                }

                //_chartElements = new List<IChartElement>();

                if (FindSeriesByNameSafe("Deal_" + "BuySell") != null)
                {
                    _chart.Series.Remove(FindSeriesByNameSafe("Deal_" + "BuySell"));
                }

                for (int i = 0; _chart.Series != null && i < _chart.Series.Count; i++)
                {
                    // удаляем стопы
                    if (_chart.Series[i].Name.Split('_')[0] == "Stop" ||
                        _chart.Series[i].Name.Split('_')[0] == "Profit")
                    {
                        _chart.Series.Remove(_chart.Series[i]);
                        i--;
                    }
                }

                for (int i = 0; _chart.Series != null && i < _chart.Series.Count; i++)
                {
                    if (_chart.Series[i].Points.Count != 0)
                    {
                        _chart.Series[i].Points.Clear();
                    }

                }

                ClearZoom();
                _myClusters = null;

                if (neadToBackChild)
                {
                    _host.Child = _chart;
                }
                _lastTimeClear = DateTime.Now;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void ClearSeries()
        {
            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action(ClearSeries));
                return;
            }
            for (int i = 0; i < _chart.Series.Count; i++)
            {
                if (_chart.Series[i].Name == "SeriesCluster")
                {
                    continue;
                }

                _chart.Series.RemoveAt(i);
                i--;
            }
        }

        /// <summary>
        /// удалить все файлы связанные с классом
        /// </summary>
        public void Delete()
        {
            _colorKeeper.Delete();
            _neadToDelete = true;
        }

        /// <summary>
        /// добавить серию данных на чарт безопасно
        /// </summary>
        /// <param name="series">сирия данных</param>
        private void PaintSeriesSafe(Series series)
        {
            try
            {
                if (_chart.InvokeRequired)
                {
                    _chart.Invoke(new Action<Series>(PaintSeriesSafe), series);
                    return;
                }
                if (FindSeriesByNameSafe(series.Name) == null)
                {
                    _chart.Series.Add(series);
                }
                else
                {
                    for (int i = 0; i < _chart.Series.Count; i++)
                    {
                        if (series.Name == _chart.Series[i].Name)
                        {
                            _chart.Series.Remove(FindSeriesByNameSafe(series.Name));
                            _chart.Series.Insert(i, series);
                            break;
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// удалить серию данных с чарта безопасно
        /// </summary>
        /// <param name="series">серия данных</param>
        private void ReMoveSeriesSafe(Series series)
        {
            try
            {
                if (_chart.InvokeRequired)
                {
                    _chart.Invoke(new Action<Series>(ReMoveSeriesSafe), series);
                    return;
                }
                if (FindSeriesByNameSafe(series.Name) == null)
                {
                    return;
                }

                _chart.Series.Remove(series);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// найти серию данных по имени безопасно
        /// </summary>
        /// <param name="name">имя серии</param>
        /// <returns>серия данных. Если такой нет, то null</returns>
        private Series FindSeriesByNameSafe(string name)
        {
            Series mySeries;
            try
            {
                mySeries = _chart.Series.FindByName(name);
            }
            catch (Exception)
            {
                try
                {
                    mySeries = _chart.Series.FindByName(name);
                }
                catch (Exception)
                {
                    return null;
                }
            }

            return mySeries;
        }

        /// <summary>
        /// найти область данных по имени безопасно
        /// </summary>
        /// <param name="name">имя области</param>
        /// <returns>область данных. Если такой нет, то null</returns>
        private ChartArea FindAreaByNameSafe(string name)
        {
            ChartArea myArea;
            try
            {
                myArea = _chart.ChartAreas.FindByName(name);
            }
            catch (Exception)
            {
                try
                {
                    myArea = _chart.ChartAreas.FindByName(name);
                }
                catch (Exception)
                {
                    return null;
                }
            }

            return myArea;
        }

        /// <summary>
        /// из хранителя цвета пришло сообщение в лог
        /// </summary>
        /// <param name="message">сообщение</param>
        /// <param name="type">тип сообщения</param>
        void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            { // если никто на нас не подписан и происходит ошибка
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        // работа с цветом чарта и областей

        /// <summary>
        /// хранилище цветов для чарта
        /// </summary>
        private ChartMasterColorKeeper _colorKeeper;

        // создание удаление областей и серий

        /// <summary>
        /// создать чарт. Создаёт первоначальную область и серию для прорисовки свечек
        /// </summary>
        private void CreateChart()
        {
            try
            {
                if (_chart != null && _chart.InvokeRequired)
                {
                    _chart.Invoke(new Action(CreateChart));
                    return;
                }

                _chart = new Chart();

                _chart.Series.Clear();
                _chart.ChartAreas.Clear();
                _chart.BackColor = _colorKeeper.ColorBackChart;
                _chart.SuppressExceptions = true;

                if (_rectangle != null)
                {
                    _rectangle.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(_colorKeeper.ColorBackSecond.A,
                        _colorKeeper.ColorBackSecond.R, _colorKeeper.ColorBackSecond.G, _colorKeeper.ColorBackSecond.B));
                }

                ChartArea prime = new ChartArea("Prime")
                {
                    CursorX = { AxisType = AxisType.Secondary, IsUserSelectionEnabled = false, IsUserEnabled = true, IntervalType = DateTimeIntervalType.Auto, Interval = 0.00001 },
                    CursorY = { AxisType = AxisType.Primary, IsUserEnabled = true, IntervalType = DateTimeIntervalType.Auto, Interval = 0.00001 },
                    //AxisX2 = { IsMarginVisible = false, Enabled = AxisEnabled.False },
                    BorderDashStyle = ChartDashStyle.Solid,
                    BorderWidth = 2,
                    BackColor = _colorKeeper.ColorBackChart,
                    BorderColor = _colorKeeper.ColorBackSecond,
                };

                prime.AxisY.TitleAlignment = StringAlignment.Near;
                prime.AxisY.TitleForeColor = _colorKeeper.ColorBackCursor;
                prime.AxisY.ScrollBar.ButtonStyle = ScrollBarButtonStyles.SmallScroll;

                prime.AxisY.LabelAutoFitMinFontSize = 12;
                prime.AxisY.LabelAutoFitStyle = LabelAutoFitStyles.IncreaseFont;
                foreach (var axe in prime.Axes)
                {
                    axe.LabelStyle.ForeColor = _colorKeeper.ColorText;
                }
                prime.CursorY.LineColor = _colorKeeper.ColorBackCursor;
                prime.CursorX.LineColor = _colorKeeper.ColorBackCursor;

                _chart.ChartAreas.Add(prime);

                Series clusterSeries = new Series("SeriesCluster");
                clusterSeries.ChartType = SeriesChartType.RangeBar;
                clusterSeries.YAxisType = AxisType.Primary;
                clusterSeries.XAxisType = AxisType.Secondary;
                clusterSeries.ChartArea = "Prime";
                clusterSeries.ShadowOffset = 2;
                clusterSeries.YValuesPerPoint = 2;

                _chart.Series.Add(clusterSeries);
                _chart.MouseMove += _chart_MouseMove2;
                _chart.ClientSizeChanged += _chart_ClientSizeChanged;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }



        // работа потока прорисовывающего чарт

        /// <summary>
        /// метод в котором работает поток прорисовывающий чарт
        /// </summary>
        private async void PainterThreadArea()
        {
            while (true)
            {
                await Task.Delay(1000);

                if (_neadToDelete)
                {
                    return;
                }

                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }

                if (_host == null)
                {
                    continue;
                }

                if (_lastTimeClear.AddSeconds(5) > DateTime.Now
                    && _startProgram != StartProgram.IsOsOptimizer)
                {
                    await Task.Delay(5000);
                    _clustersToPaint = new ConcurrentQueue<List<HorizontalVolumeLine>>();
                }

                // проверяем, пришли ли свечи

                if (!_clustersToPaint.IsEmpty)
                {
                    List<HorizontalVolumeLine> candles = new List<HorizontalVolumeLine>();

                    while (!_clustersToPaint.IsEmpty)
                    {
                        _clustersToPaint.TryDequeue(out candles);
                    }

                    if (candles != null)
                    {
                        PaintCluster(candles);
                    }
                }

                if (_startProgram == StartProgram.IsTester ||
                    _startProgram == StartProgram.IsOsOptimizer)
                {
                    await Task.Delay(2000);
                }
            }
        }

        /// <summary>
        /// пора прекращать прорисовывать чарт на совсем
        /// </summary>
        private bool _neadToDelete;

        /// <summary>
        /// очередь со свечками, которые нужно прорисовать
        /// </summary>
        private ConcurrentQueue<List<HorizontalVolumeLine>> _clustersToPaint = new ConcurrentQueue<List<HorizontalVolumeLine>>();

        // объёмы

        private decimal _lastMaxVolume;

        /// <summary>
        /// добавить свечки на прорисовку
        /// </summary>
        /// <param name="history">свечи</param>
        public void ProcessCluster(List<HorizontalVolumeLine> history)
        {
            if ((_startProgram == StartProgram.IsTester ||
                 _startProgram == StartProgram.IsOsMiner) &&
                _host != null)
            {
                PaintCluster(history);
            }
            else
            {
                _clustersToPaint.Enqueue(history);
            }
        }

        /// <summary>
        /// прорисовать свечки
        /// </summary>
        /// <param name="history">свечи</param>
        private void PaintCluster(List<HorizontalVolumeLine> history)
        {

            try
            {
                if (history == null && _myClusters == null)
                {
                    return;
                }

                if (history == null ||
                    history.Count == 0)
                {
                    return;
                }

                if (_chart.InvokeRequired)
                {
                    _chart.Invoke(new Action<List<HorizontalVolumeLine>>(PaintCluster), history);
                    return;
                }

                if (history == null)
                {
                    history = _myClusters;
                }
                else
                {
                    _myClusters = history;
                }

                Series oldclusterSeries = FindSeriesByNameSafe("SeriesCluster");

                if (oldclusterSeries == null)
                {
                    CreateChart();
                    oldclusterSeries = FindSeriesByNameSafe("SeriesCluster");
                }

                if (oldclusterSeries == null)
                {
                    return;
                }

                if (_startProgram != StartProgram.IsTester)
                { // в реальном подключении, каждую новую свечу рассчитываем 
                    // актуальные мультипликаторы для областей
                    //ReloadAreaSizes();
                }
                else if (history.Count == 20 ||
                    history.Count == 50)
                {
                    //ReloadAreaSizes();
                }

                int pointsCountStart = 0;

                for (int i = 0; i < _volume.VolumeClusters.Count-1; i++)
                {
                    if (_volume.VolumeClusters[i].Lines == null)
                    {
                        continue;
                    }
                    pointsCountStart += _volume.VolumeClusters[i].Lines.Count;
                }

                decimal maxVolume = GetMaxVolume(_chartType);

                if (pointsCountStart > oldclusterSeries.Points.Count)
                {
                    if (history.Count < oldclusterSeries.Points.Count)
                    {
                        ClearZoom();
                    }

                    //ReloadAreaSizes();
                    PaintAllClusters(history,maxVolume);
                    //ResizeSeriesLabels();
                    //RePaintRightLebels();
                    //ResizeYAxisOnArea("Prime");
                }
                else
                {
                    if (maxVolume != _lastMaxVolume)
                    {
                        pointsCountStart = 0;
                    }
                    RePaintClusterFromIndex(history, pointsCountStart, maxVolume);
                }

                _lastMaxVolume = maxVolume;
                ResizeXAxis();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// перерисовать свечку по индексу
        /// </summary>
        /// <param name="history">свечи</param>
        /// <param name="index">индекс</param>
        private void RePaintClusterFromIndex(List<HorizontalVolumeLine> history, int index, decimal maxVolume)
        {
            if (history == null ||
                           history.Count == 0)
            {
                return;
            }

            Series candleSeries = FindSeriesByNameSafe("SeriesCluster");

            for (int i = index; i < history.Count; i++)
            {
                decimal clusterStartY = history[i].NumCluster; // Это точка начала кластера

                decimal linePriceX = history[i].Price;

                decimal lineVolume = history[i].GetVolume(_chartType);

                decimal move = Math.Round(lineVolume / maxVolume,4);

                if (_chartType == ClusterType.DeltaVolume)
                {
                    move = move * 0.7m;
                }

                DataPoint myPoint;

                if (i < candleSeries.Points.Count)
                {
                    myPoint = candleSeries.Points[i];
                    myPoint.YValues[1] = Convert.ToDouble(clusterStartY + move);
                }
                else
                {
                    candleSeries.Points.AddXY(linePriceX, clusterStartY, clusterStartY + move);
                    myPoint = candleSeries.Points[candleSeries.Points.Count-1];
                }

                if (_chartType == ClusterType.DeltaVolume)
                {
                    if (move > 0.35m)
                    {
                        myPoint.Color = _colorKeeper.ColorUpBorderCandle;
                        myPoint.BorderColor = _colorKeeper.ColorUpBorderCandle;
                        myPoint.BackSecondaryColor =
                            _colorKeeper.ColorUpBodyCandle;
                    }
                    else if (move < -0.35m)
                    {
                        myPoint.Color = _colorKeeper.ColorUpBorderCandle;
                        myPoint.BorderColor = _colorKeeper.ColorUpBorderCandle;
                        myPoint.BackSecondaryColor =
                            _colorKeeper.ColorUpBodyCandle;
                    }
                    else
                    {
                        myPoint.Color = _colorKeeper.ColorDownBorderCandle;
                        myPoint.BorderColor = _colorKeeper.ColorDownBorderCandle;
                        myPoint.BackSecondaryColor =
                            _colorKeeper.ColorDownBodyCandle;
                    }
                }
                else
                {
                    if (move > 0.5m)
                    {
                        myPoint.Color = _colorKeeper.ColorUpBorderCandle;
                        myPoint.BorderColor = _colorKeeper.ColorUpBorderCandle;
                        myPoint.BackSecondaryColor =
                            _colorKeeper.ColorUpBodyCandle;
                    }
                    else
                    {
                        myPoint.Color = _colorKeeper.ColorDownBorderCandle;
                        myPoint.BorderColor = _colorKeeper.ColorDownBorderCandle;
                        myPoint.BackSecondaryColor =
                            _colorKeeper.ColorDownBodyCandle;
                    }
                }


                myPoint.ToolTip = history[i].ToolTip;

            }


            PaintSeriesSafe(candleSeries);
            if (FindSeriesByNameSafe("Cursor") != null)
            {
                ReMoveSeriesSafe(FindSeriesByNameSafe("Cursor"));
            }


            //ReloadAreaSizes();
        }

        /// <summary>
        /// прорисовать все свечки
        /// </summary>
        /// <param name="history">свечи</param>
        private void PaintAllClusters(List<HorizontalVolumeLine> history, decimal maxVolume)
        {
            if (history == null ||
                history.Count == 0)
            {
                return;
            }

            Series candleSeries = new Series("SeriesCluster");
            candleSeries.ChartType = SeriesChartType.RangeBar;
            candleSeries.YAxisType = AxisType.Primary;
            candleSeries.XAxisType = AxisType.Secondary;
            candleSeries.ChartArea = "Prime";
            candleSeries.ShadowOffset = 2;
            candleSeries.YValuesPerPoint = 2;

            for (int i = 0; i < history.Count; i++)
            {
                decimal clusterStartY = history[i].NumCluster; // Это точка начала кластера

                decimal linePriceX = history[i].Price;

                decimal lineVolume = history[i].GetVolume(_chartType);
                // 344360
                decimal move = lineVolume / maxVolume;
                if (_chartType == ClusterType.DeltaVolume)
                {
                    move = move * 0.7m;
                }

                candleSeries.Points.AddXY(linePriceX, clusterStartY, clusterStartY + move);

                if (_chartType == ClusterType.DeltaVolume)
                {
                    if (move > 0.35m)
                    {
                        candleSeries.Points[candleSeries.Points.Count - 1].Color = _colorKeeper.ColorUpBorderCandle;
                        candleSeries.Points[candleSeries.Points.Count - 1].BorderColor = _colorKeeper.ColorUpBorderCandle;
                        candleSeries.Points[candleSeries.Points.Count - 1].BackSecondaryColor =
                            _colorKeeper.ColorUpBodyCandle;
                    }
                    if (move < -0.35m)
                    {
                        candleSeries.Points[candleSeries.Points.Count - 1].Color = _colorKeeper.ColorUpBorderCandle;
                        candleSeries.Points[candleSeries.Points.Count - 1].BorderColor = _colorKeeper.ColorUpBorderCandle;
                        candleSeries.Points[candleSeries.Points.Count - 1].BackSecondaryColor =
                            _colorKeeper.ColorUpBodyCandle;
                    }
                    else
                    {
                        candleSeries.Points[candleSeries.Points.Count - 1].Color = _colorKeeper.ColorDownBorderCandle;
                        candleSeries.Points[candleSeries.Points.Count - 1].BorderColor = _colorKeeper.ColorDownBorderCandle;
                        candleSeries.Points[candleSeries.Points.Count - 1].BackSecondaryColor =
                            _colorKeeper.ColorDownBodyCandle;
                    }
                }
                else
                {
                    if (move > 0.5m)
                    {
                        candleSeries.Points[candleSeries.Points.Count - 1].Color = _colorKeeper.ColorUpBorderCandle;
                        candleSeries.Points[candleSeries.Points.Count - 1].BorderColor =
                            _colorKeeper.ColorUpBorderCandle;
                        candleSeries.Points[candleSeries.Points.Count - 1].BackSecondaryColor =
                            _colorKeeper.ColorUpBodyCandle;
                    }
                    else
                    {
                        candleSeries.Points[candleSeries.Points.Count - 1].Color = _colorKeeper.ColorDownBorderCandle;
                        candleSeries.Points[candleSeries.Points.Count - 1].BorderColor =
                            _colorKeeper.ColorDownBorderCandle;
                        candleSeries.Points[candleSeries.Points.Count - 1].BackSecondaryColor =
                            _colorKeeper.ColorDownBodyCandle;
                    }
                }

                candleSeries.Points[candleSeries.Points.Count - 1].ToolTip = history[i].ToolTip;

            }

            ChartArea candleArea = FindAreaByNameSafe("Prime");
           // if (candleArea != null && candleArea.AxisY.ScrollBar.IsVisible)
                //если уже выбран какой-то диапазон
           // {
                // сдвигаем представление вправо
                candleArea.AxisY.Maximum = _volume.VolumeClusters.Count + 2;
                candleArea.AxisY.Minimum = -1;
            //}

            if (FindSeriesByNameSafe("Cursor") != null)
            {
                ReMoveSeriesSafe(FindSeriesByNameSafe("Cursor"));
            }

            PaintSeriesSafe(candleSeries);

            //ReloadAreaSizes();
        }

        private decimal GetMaxVolume(ClusterType clusterType)
        {
            decimal result = 0;

            if (clusterType == ClusterType.SummVolume)
            {
                if (double.IsNaN(_chart.ChartAreas[0].AxisY.ScaleView.Size) ||
                    double.IsNaN(_chart.ChartAreas[0].AxisY.ScaleView.Position))
                {
                    result = _volume.MaxSummVolumeCluster.MaxSummVolumeLine.VolumeSumm;
                }
                else
                {
                    int values = (int)_chart.ChartAreas[0].AxisY.ScaleView.Size;
                    int firstPos = (int)_chart.ChartAreas[0].AxisY.ScaleView.Position;
                    result = _volume.FindMaxVolumeCluster(firstPos, firstPos + values, clusterType).MaxSummVolumeLine.VolumeSumm;
                }
            }
            if (clusterType == ClusterType.BuyVolume)
            {
                if (double.IsNaN(_chart.ChartAreas[0].AxisY.ScaleView.Size))
                {
                    result = _volume.MaxBuyVolumeCluster.MaxBuyVolumeLine.VolumeBuy;
                }
                else
                {
                    int values = (int)_chart.ChartAreas[0].AxisY.ScaleView.Size;
                    int firstPos = (int)_chart.ChartAreas[0].AxisY.ScaleView.Position;
                    result = _volume.FindMaxVolumeCluster(firstPos, firstPos + values, clusterType).MaxBuyVolumeLine.VolumeBuy;
                }
            }
            if (clusterType == ClusterType.SellVolume)
            {
                if (double.IsNaN(_chart.ChartAreas[0].AxisY.ScaleView.Size))
                {
                    result = _volume.MaxSellVolumeCluster.MaxSellVolumeLine.VolumeSell;
                }
                else
                {
                    int values = (int)_chart.ChartAreas[0].AxisY.ScaleView.Size;
                    int firstPos = (int)_chart.ChartAreas[0].AxisY.ScaleView.Position;
                    result = _volume.FindMaxVolumeCluster(firstPos, firstPos + values, clusterType).MaxSellVolumeLine.VolumeSell;
                }
            }
            if (clusterType == ClusterType.DeltaVolume)
            {
                if (double.IsNaN(_chart.ChartAreas[0].AxisY.ScaleView.Size))
                {
                    result = _volume.MaxDeltaVolumeCluster.MaxDeltaVolumeLine.VolumeDelta;

                    if (-_volume.MaxDeltaVolumeCluster.MinDeltaVolumeLine.VolumeDelta > result)
                    {
                        result = -_volume.MaxDeltaVolumeCluster.MinDeltaVolumeLine.VolumeDelta;
                    }
                }
                else
                {
                    int values = (int)_chart.ChartAreas[0].AxisY.ScaleView.Size;
                    int firstPos = (int)_chart.ChartAreas[0].AxisY.ScaleView.Position;
                    decimal max = _volume.FindMaxVolumeCluster(firstPos, firstPos + values, clusterType).MaxDeltaVolumeLine.VolumeDelta;
                    decimal min = -_volume.FindMaxVolumeCluster(firstPos, firstPos + values, clusterType).MaxDeltaVolumeLine.VolumeDelta;

                    if (max > min)
                    {
                        result = max;
                    }
                    else
                    {
                        result = min;
                    }
                }
            }

            return result;
        }

        // управление исчезанием перекрестия на графике


        private void _chart_MouseLeave(object sender, EventArgs e)
        {
            for (int i = 0; i < _chart.ChartAreas.Count; i++)
            {
                _chart.ChartAreas[i].CursorX.Position = double.NaN;
                _chart.ChartAreas[i].CursorY.Position = 0;
            }
        }

        // управление высотой областей

        private List<ChartAreaPosition> _areaPositions;

        private void _chart_MouseMove2(object sender, MouseEventArgs e)
        {
            if (_chart.Cursor == Cursors.SizeAll)
            {
                return;
            }
            if (_chart.ChartAreas.Count < 1)
            {
                return;
            }

            if (e.Button == MouseButtons.Left &&
                _chart.Cursor == Cursors.Arrow)
            {
                return;
            }

            if (_volume.VolumeClusters == null ||
                _volume.VolumeClusters.Count < 3)
            {
                return;
            }

            if (_areaPositions.Count != _chart.ChartAreas.Count ||
                _areaPositions.Find(posi => posi.RightPoint == 0) != null)
            {
                _areaPositions = new List<ChartAreaPosition>();
                ReloadChartAreasSize();
            }

            if (_chart.Cursor == Cursors.Hand ||
                _chart.Cursor == Cursors.SizeNS ||
                _areaPositions == null)
            {
                return;
            }

            ChartAreaPosition myPosition = null;

            MouseEventArgs mouse = (MouseEventArgs)e;

            ChartAreaPosition pos = _areaPositions[0];

            if ((pos.LeftPoint < e.X &&
                 pos.RightPoint > e.X &&
                 pos.DownPoint - 30 < e.Y &&
                 pos.DownPoint - 10 > e.Y)
                ||
                (mouse.Button == MouseButtons.Left && _chart.Cursor == Cursors.SizeWE && pos.LeftPoint < e.X &&
                 pos.RightPoint > e.X &&
                 pos.DownPoint - 200 < e.Y &&
                 pos.DownPoint + 100 > e.Y))
            {
                myPosition = pos;
                _chart.Cursor = Cursors.SizeWE;
            }
            else
            {
                pos.ValueYMouseOnClickStart = 0;
                pos.HeightAreaOnClick = 0;
                pos.ValueYChartOnClick = 0;
            }


            if (myPosition == null)
            {
                _chart.Cursor = Cursors.Arrow;
                return;
            }

            if (mouse.Button != MouseButtons.Left)
            {
                myPosition.ValueXMouseOnClickStart = 0;
                myPosition.CountXValuesChartOnClickStart = 0;
                return;
            }

            if (myPosition.ValueXMouseOnClickStart == 0)
            {
                myPosition.ValueXMouseOnClickStart = e.X;

                if (double.IsNaN(_chart.ChartAreas[0].AxisY.ScaleView.Size))
                {
                    int max = _volume.VolumeClusters.Count;

                    myPosition.CountXValuesChartOnClickStart = max;
                }
                else
                {
                    myPosition.CountXValuesChartOnClickStart = (int)_chart.ChartAreas[0].AxisY.ScaleView.Size;
                }
                return;
            }


            double persentMove = Math.Abs(myPosition.ValueXMouseOnClickStart - e.X) / _host.Child.Width;

            if (double.IsInfinity(persentMove) ||
                persentMove == 0)
            {
                return;
            }

            //double concateValue = 100*persentMove*5;


            int maxSize = _volume.VolumeClusters.Count;


            if (myPosition.ValueXMouseOnClickStart < e.X)
            {
                if (myPosition.Area.Position.Height < 10)
                {
                    return;
                }

                double newVal = myPosition.CountXValuesChartOnClickStart +
                             myPosition.CountXValuesChartOnClickStart * persentMove * 3;


                if (newVal > maxSize)
                {
                    _chart.ChartAreas[0].AxisY.ScaleView.Size = Double.NaN;
                }
                else
                {

                    if (newVal + _chart.ChartAreas[0].AxisY.ScaleView.Position > maxSize)
                    {
                        _chart.ChartAreas[0].AxisY.ScaleView.Position = maxSize - newVal;
                    }

                    _chart.ChartAreas[0].AxisY.ScaleView.Size = newVal;
                    //RePaintRightLebels();

                    if (_chart.ChartAreas[0].AxisY.ScaleView.Position + newVal > maxSize)
                    {
                        _chart.ChartAreas[0].AxisY.ScaleView.Position = maxSize - newVal;
                    }
                }
            }
            else if (myPosition.ValueXMouseOnClickStart > e.X)
            {
                double newVal = myPosition.CountXValuesChartOnClickStart -
               myPosition.CountXValuesChartOnClickStart * persentMove * 3;


                if (newVal < 5)
                {
                    _chart.ChartAreas[0].AxisY.ScaleView.Size = 5;
                }
                else
                {
                    if (!double.IsNaN(_chart.ChartAreas[0].AxisY.ScaleView.Size))
                    {


                        _chart.ChartAreas[0].AxisY.ScaleView.Position = _chart.ChartAreas[0].AxisY.ScaleView.Position + _chart.ChartAreas[0].AxisY.ScaleView.Size - newVal;
                    }

                    _chart.ChartAreas[0].AxisY.ScaleView.Size = newVal;
                    if (_chart.ChartAreas[0].AxisY.ScaleView.Position + newVal > maxSize)
                    {
                        double newStartPos = maxSize - newVal;
                        if (newStartPos < 0)
                        {
                            newStartPos = 0;
                        }
                        _chart.ChartAreas[0].AxisY.ScaleView.Position = newStartPos;
                    }

                }
            }

            ResizeXAxis();
            ResizeYAxis();
        }

        void _chart_ClientSizeChanged(object sender, EventArgs e)
        {
            ReloadChartAreasSize();
        }

        /// <summary>
        /// пересчитать расположение областей на окне
        /// </summary>
        private void ReloadChartAreasSize()
        {
            if (_areaPositions == null)
            {
                _areaPositions = new List<ChartAreaPosition>();
            }

            for (int i = 0; i < _chart.ChartAreas.Count; i++)
            {
                GetAreaPosition(_chart.ChartAreas[i]);
            }
        }

        private void GetAreaPosition(ChartArea area)
        {
            if (_host == null ||
                _host.Child == null)
            {
                return;
            }

            ChartAreaPosition position = _areaPositions.Find(pos => pos.Area.Name == area.Name);

            if (position == null)
            {
                position = new ChartAreaPosition();
                _areaPositions.Add(position);
                position.Area = area;
            }

            position.LeftPoint = _host.Child.Left + _host.Child.Width * (area.InnerPlotPosition.X / 100);

            position.RightPoint = position.LeftPoint + _host.Child.Width * (area.InnerPlotPosition.Width / 100);

            position.UpPoint = _chart.Height - _chart.Height * ((100 - area.Position.Y) / 100);

            position.DownPoint = position.UpPoint + _host.Child.Height * (area.Position.Height / 100);

        }

        // изменение масштабов осей

        /// <summary>
        /// изменились масштабы Оси Х
        /// </summary>
        void _chart_AxisViewChanged(object sender, ViewEventArgs e)
        {
            try
            {
                ResizeXAxis();
                ResizeYAxis();
                ProcessCluster(_volume.VolumeClusterLines);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// перерисовать линии на оси Икс
        /// </summary>
        private void ResizeXAxis()
        {
            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action(ResizeXAxis));

                return;
            }
            if (_volume.VolumeClusters == null ||
                _volume.VolumeClusters.Count == 0)
            {
                return;
            }
            ChartArea area = _chart.ChartAreas[0];

            double values = 0;

            int firstPos = 0;

            if (double.IsNaN(_chart.ChartAreas[0].AxisY.ScaleView.Size))
            {
                values = _volume.VolumeClusterLines[_volume.VolumeClusterLines.Count-1].NumCluster+1;
            }
            else
            {
                values = (int)_chart.ChartAreas[0].AxisY.ScaleView.Size;
                firstPos = (int)_chart.ChartAreas[0].AxisY.ScaleView.Position;
            }

            if (firstPos < 0 ||
                firstPos > _volume.VolumeClusters.Count)
            {
                return;
            }

            int labelCount = 4;


            if (_volume.VolumeClusters.Count <= 2)
            {
                labelCount = 2;
            }
            else if (_volume.VolumeClusters.Count == 3)
            {
                labelCount = 3;
            }

            area.AxisY.Interval = values / labelCount;

            while (area.AxisY.CustomLabels.Count < labelCount)
            {
                area.AxisY.CustomLabels.Add(new CustomLabel());
            }
            while (area.AxisY.CustomLabels.Count > labelCount)
            {
                area.AxisY.CustomLabels.RemoveAt(0);
            }

            double value = firstPos + area.AxisY.Interval;

            if (labelCount < 4)
            {
                value = 0;
            }


            for (int i = 0; i < labelCount; i++)
            {
                area.AxisY.CustomLabels[i].FromPosition = value - area.AxisY.Interval * 0.7;
                area.AxisY.CustomLabels[i].ToPosition = value + area.AxisY.Interval * 0.7;
                int clusterIndex = (int)value;

                if (clusterIndex >= _volume.VolumeClusters.Count)
                {
                    clusterIndex = _volume.VolumeClusters.Count - 1;
                }

                area.AxisY.CustomLabels[i].Text = _volume.VolumeClusters[clusterIndex].Time.ToString();

                value += area.AxisY.Interval;

                if (value >= _myClusters.Count)
                {
                    value = _myClusters.Count - 1;
                }
            }

        }

        private void ResizeYAxis()
        {
            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action(ResizeYAxis));

                return;
            }
            if (_volume.VolumeClusters == null ||
                _volume.VolumeClusters.Count == 0)
            {
                return;
            }

            int lastPos = 0;

            int firstPos = 0;

            if (double.IsNaN(_chart.ChartAreas[0].AxisY.ScaleView.Size))
            {
                lastPos = _volume.VolumeClusterLines[_volume.VolumeClusterLines.Count - 1].NumCluster + 1;
            }
            else
            {
                firstPos = (int)_chart.ChartAreas[0].AxisY.ScaleView.Position;
                lastPos = (int)_chart.ChartAreas[0].AxisY.ScaleView.Size
                + firstPos;
                
            }

            if (firstPos < 0 ||
                firstPos > _volume.VolumeClusters.Count)
            {
                return;
            }

            if (firstPos < 0)
            {
                firstPos = 0;
            }

            if (lastPos > _volume.VolumeClusters.Count)
            {
                lastPos = _volume.VolumeClusters.Count - 1;
            }

            if (firstPos > lastPos)
            {
                return;
            }

            decimal max = 0;
            decimal min = Decimal.MaxValue;

            for (int i = firstPos; i < lastPos; i++)
            {
                if (max < _volume.VolumeClusters[i].MaxPriceLine.Price)
                {
                    max = _volume.VolumeClusters[i].MaxPriceLine.Price;
                }
                if (min > _volume.VolumeClusters[i].MinPriceLine.Price)
                {
                    min = _volume.VolumeClusters[i].MinPriceLine.Price;
                }
            }

            _chart.ChartAreas[0].AxisX2.Maximum = Convert.ToDouble(max);
            _chart.ChartAreas[0].AxisX2.Minimum = Convert.ToDouble(min);

        }

        /// <summary>
        /// сбросить увеличение
        /// </summary>
        private void ClearZoom()
        {
            try
            {
                if (_chart.InvokeRequired)
                {
                    _chart.Invoke(new Action(ClearZoom));
                    return;
                }

                while (_chart.ChartAreas[0].AxisX.ScaleView.IsZoomed)
                {
                    _chart.ChartAreas[0].AxisX.ScaleView.ZoomReset();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }
    }
}
