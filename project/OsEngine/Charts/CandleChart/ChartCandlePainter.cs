/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using OsEngine.Alerts;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Charts.ColorKeeper;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using Color = System.Drawing.Color;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace OsEngine.Charts.CandleChart
{
    /// <summary>
    /// Мастер прорисовки свечного графика
    /// </summary>
    public class ChartCandlePainter
    {

// сервис

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="name">имя робота, которому принадлежит чарт</param>
        /// <param name="startProgram">программа создающая объект класса</param>
        public ChartCandlePainter(string name,StartProgram startProgram)
        {
            try
            {
                _startProgram = startProgram;
                _colorKeeper = new ChartMasterColorKeeper(name);
                _colorKeeper.LogMessageEvent += SendLogMessage;
                _colorKeeper.NeedToRePaintFormEvent += _colorKeeper_NeedToRePaintFormEvent;

                CreateChart();

                _chart.Text = name;
                _chart.AxisScrollBarClicked += _chart_AxisScrollBarClicked; // событие передвижения курсора
                _chart.AxisViewChanged += _chart_AxisViewChanged; // событие изменения масштаба
                _chart.Click += _chart_Click;

                _chart.MouseDown += _chartForCandle_MouseDown;
                _chart.MouseUp += _chartForCandle_MouseUp;
                _chart.MouseMove += _chartForCandle_MouseMove;

                _chart.MouseMove += _chartForCandle_MouseMove2ChartElement;
                _chart.MouseDown += _chartForCandle_MouseDown2ChartElement;
                _chart.MouseUp += _chartForCandle_MouseUp2ChartElement;

                _chart.MouseLeave += _chart_MouseLeave;

                Thread painterThred = new Thread(PainterThreadArea);
                painterThred.IsBackground = true;
                painterThred.Name = name + "ChartPainterThread";
                painterThred.Start();
                
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(),LogMessageType.Error);
            }
        }

        /// <summary>
        /// программа создающая объект класса
        /// </summary>
        private StartProgram _startProgram;

        /// <summary>
        /// область на которой будет размещён чарт
        /// </summary>
        private WindowsFormsHost _host;

        /// <summary>
        /// элемент на котором будет размещён _host
        /// </summary>
        private Rectangle _rectangle;

        /// <summary>
        /// свечной график
        /// </summary>
        private Chart _chart;

        /// <summary>
        /// свечки
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// таймфрейм свечек которые прорисовываются в чарте в виде времени
        /// </summary>
        private TimeSpan _timeFrameSpan;

        /// <summary>
        /// таймфрейм свечек которые прорисовываются в чарте
        /// </summary>
        private TimeFrame _timeFrame;

        /// <summary>
        /// таймфреймы для данного чата устанавливались
        /// </summary>
        private bool _timeFrameIsActivate;

        /// <summary>
        /// изменить таймФрейм свечек для прорисовки
        /// </summary>
        /// <param name="timeFrameSpan"></param>
        /// <param name="timeFrame"></param>
        public void SetNewTimeFrame(TimeSpan timeFrameSpan, TimeFrame timeFrame)
        {
            _timeFrameSpan = timeFrameSpan;
            _timeFrame = timeFrame;
            _timeFrameIsActivate = true;
        }

        /// <summary>
        /// начать прорисовку графика
        /// </summary>
        public void StartPaintPrimeChart(WindowsFormsHost host, Rectangle rectangle) 
        {
            _host = host;
            _rectangle = rectangle;
            _isPaint = true;

            try
            {
                if (_host.Child != null && _host.Child.Text == _chart.Text)
                {
                    _isPaint = true;
                    return;
                }

                if (_host != null && !_host.Dispatcher.CheckAccess())
                {
                    _host.Dispatcher.Invoke(new Action<WindowsFormsHost,Rectangle>(StartPaintPrimeChart),host,rectangle);
                    return;
                }
                _host.Child = _chart;
                _host.Child.Show();
                _isPaint = true;
                _rectangle.Fill =
                    new SolidColorBrush(System.Windows.Media.Color.FromArgb(_colorKeeper.ColorBackChart.A,
                        _colorKeeper.ColorBackChart.R, _colorKeeper.ColorBackChart.G, _colorKeeper.ColorBackChart.B));

                ResizeYAxisOnArea("Prime");
                ResizeSeriesLabels();
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

                _isPaint = false;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// включена ли прорисовка графика
        /// </summary>
        private bool _isPaint;

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
                if (_areaSizes != null)
                {
                    _areaSizes.Clear();
                }


                if (_seriesYLengths != null)
                {
                    _seriesYLengths.Clear();
                }
               
                _timePoints.Clear();
                bool neadToBackChild = false;

                if (_host != null && _host.Child != null)
                {
                    _host.Child = null;
                    neadToBackChild = true;
                }

                Series oldcandleSeries = FindSeriesByNameSafe("SeriesCandle");
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
                _myCandles = null;

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
                if (_chart.Series[i].Name == "SeriesCandle")
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
        public event Action<string,LogMessageType> LogMessageEvent;

// работа с цветом чарта и областей

        /// <summary>
        /// хранилище цветов для чарта
        /// </summary>
        private ChartMasterColorKeeper _colorKeeper; 

        /// <summary>
        /// установить тёмную схему для чарта
        /// </summary>
        public void SetBlackScheme()
        {
            _colorKeeper.SetBlackScheme();
        }

        public void SetPointSize(ChartPositionTradeSize pointSize)
        {
            _colorKeeper.PointsSize = pointSize;
        }

        public void SetPointType(PointType type)
        {
            _colorKeeper.PointType = type;
        }

        /// <summary>
        /// установить белую схему для чарта
        /// </summary>
        public void SetWhiteScheme()
        {
            _colorKeeper.SetWhiteScheme();
        }

        /// <summary>
        /// входящее событие: изменились цвета графика
        /// </summary>
        void _colorKeeper_NeedToRePaintFormEvent()
        {
            RefreshChartColor();

            // перерисовка правой оси

            for (int i = 0; _chart.ChartAreas.Count != 0 && i < _chart.ChartAreas.Count; i++)
            {
                RePaintPrimeLines(_chart.ChartAreas[i].Name);
            }
        }

        /// <summary>
        /// перекрасить цвета у чарта
        /// </summary>
        public void RefreshChartColor() 
        {
            try
            {
                if (_chart.InvokeRequired)
                {
                    _chart.Invoke(new Action(RefreshChartColor));
                    return;
                }

                _chart.BackColor = _colorKeeper.ColorBackChart;

                _rectangle.Fill =
                    new SolidColorBrush(System.Windows.Media.Color.FromArgb(_colorKeeper.ColorBackChart.A,
                        _colorKeeper.ColorBackChart.R, _colorKeeper.ColorBackChart.G, _colorKeeper.ColorBackChart.B));

                _chart.ChartAreas[0].AxisX.TitleForeColor = _colorKeeper.ColorBackCursor;

                for (int i = 0; _chart.ChartAreas != null && i < _chart.ChartAreas.Count; i++)
                {
                    _chart.ChartAreas[i].BackColor = _colorKeeper.ColorBackChart;
                    _chart.ChartAreas[i].BorderColor = _colorKeeper.ColorBackSecond;
                    _chart.ChartAreas[i].CursorY.LineColor = _colorKeeper.ColorBackCursor;
                    _chart.ChartAreas[i].CursorX.LineColor = _colorKeeper.ColorBackCursor;

                    foreach (var axe in _chart.ChartAreas[i].Axes)
                    {
                        axe.LabelStyle.ForeColor = _colorKeeper.ColorText;
                    }
                }

                Series candleSeries = FindSeriesByNameSafe("SeriesCandle");

                if (candleSeries == null)
                {
                    return;
                }

                for (int i = 0; i < candleSeries.Points.Count; i++)
                {
                    if (candleSeries.Points[i].YValues[3] > candleSeries.Points[i].YValues[2])
                    {
                        candleSeries.Points[i].Color = _colorKeeper.ColorUpBorderCandle;
                        candleSeries.Points[i].BorderColor = _colorKeeper.ColorUpBorderCandle;
                        candleSeries.Points[i].BackSecondaryColor = _colorKeeper.ColorUpBodyCandle;
                    }
                    else
                    {
                        candleSeries.Points[i].Color = _colorKeeper.ColorDownBorderCandle;
                        candleSeries.Points[i].BorderColor = _colorKeeper.ColorDownBorderCandle;
                        candleSeries.Points[i].BackSecondaryColor = _colorKeeper.ColorDownBodyCandle;
                    }
                }


            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

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
                    CursorX = { IsUserSelectionEnabled = false, IsUserEnabled = true },
                    CursorY = { AxisType = AxisType.Secondary, IsUserEnabled = true, IntervalType = DateTimeIntervalType.Auto, Interval = 0.00000001 },
                    AxisX2 = { IsMarginVisible = false, Enabled = AxisEnabled.False},
                    BorderDashStyle = ChartDashStyle.Solid,
                    BorderWidth = 2,
                    BackColor = _colorKeeper.ColorBackChart,
                    BorderColor = _colorKeeper.ColorBackSecond,
                };

                prime.AxisX.TitleAlignment = StringAlignment.Near;
                prime.AxisX.TitleForeColor = _colorKeeper.ColorBackCursor;
                prime.AxisX.ScrollBar.ButtonStyle = ScrollBarButtonStyles.SmallScroll;

                prime.AxisY2.LabelAutoFitMinFontSize = 12;
                prime.AxisY2.LabelAutoFitStyle = LabelAutoFitStyles.IncreaseFont;
                foreach (var axe in prime.Axes)
                {
                    axe.LabelStyle.ForeColor = _colorKeeper.ColorText;
                }
                prime.CursorY.LineColor = _colorKeeper.ColorBackCursor;
                prime.CursorX.LineColor = _colorKeeper.ColorBackCursor;

                _chart.ChartAreas.Add(prime);

                Series candleSeries = new Series("SeriesCandle");
                candleSeries.ChartType = SeriesChartType.Candlestick;
                candleSeries.YAxisType = AxisType.Secondary;
                candleSeries.ChartArea = "Prime";
                candleSeries.ShadowOffset = 2;
                candleSeries.YValuesPerPoint = 4;

                _chart.Series.Add(candleSeries);
                _chart.MouseMove += _chart_MouseMove;
                _chart.MouseMove += _chart_MouseMove2;
                _chart.ClientSizeChanged += _chart_ClientSizeChanged;
                _chart.AxisViewChanging += _chart_AxisViewChanging;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /*
         * Первичными объектами на чарте являются области данных - ChartArea
         * На областях, затем, можно создавать серии данных - Series
         * У Series в свою очередь есть точки данных(Point), через которые мы прорисовываем объекты
         */
         
        /// <summary>
        /// создать область данных на чарте
        /// </summary>
        /// <param name="nameArea">имя области</param>
        /// <param name="height">высота области</param>
        /// <returns></returns>
        public ChartArea CreateArea(string nameArea, int height)
        {
            try
            {
                ChartArea area = FindAreaByNameSafe(nameArea);

                if (area != null)
                {
                    return area;
                }


                if (height > 100 || string.IsNullOrWhiteSpace(nameArea))
                {
                    return null;
                }

                // 1 уже созданные области надо подвинуть.

                ChartArea primeArea = FindAreaByNameSafe("Prime");
                if (primeArea == null)
                {
                    return null;
                }

                if (_chart.ChartAreas.Count > 1)
                {
                    int allHeght = 0;

                    for (int i = 1; i < _chart.ChartAreas.Count; i++)
                    {
                        allHeght += Convert.ToInt32(_chart.ChartAreas[i].Position.Height);
                    }

                    int totalLenght = 100 - allHeght - height;

                    primeArea.Position.Height = totalLenght;
                    primeArea.Position.Width = 100;
                    primeArea.Position.Y = 0;

                    for (int i = 1; i < _chart.ChartAreas.Count; i++)
                    {
                        _chart.ChartAreas[i].Position.Y = _chart.ChartAreas[i - 1].Position.Y +
                                                          _chart.ChartAreas[i - 1].Position.Height;
                    }
                }
                else
                {
                    primeArea.Position.Height = 100 - height;
                    primeArea.Position.Width = 100;
                    primeArea.Position.Y = 0;
                }

                ChartArea newArea = new ChartArea(nameArea);
                newArea.AlignWithChartArea = "Prime";
                newArea.Position.Height = height;
                newArea.Position.Width = 100;
                newArea.Position.Y = 100 - height;
                newArea.AxisX.Enabled = AxisEnabled.False;
                newArea.AxisX2.Enabled = AxisEnabled.False;
                newArea.AxisY2.LabelAutoFitMinFontSize = 10;
                newArea.AxisY2.LabelAutoFitMinFontSize = 12;
                newArea.AxisY2.LabelAutoFitStyle = LabelAutoFitStyles.IncreaseFont;
                newArea.BorderDashStyle = ChartDashStyle.Solid;
                newArea.BorderWidth = 2;

                newArea.BackColor = _colorKeeper.ColorBackChart;
                newArea.BorderColor = _colorKeeper.ColorBackSecond;
                newArea.CursorY.LineColor = _colorKeeper.ColorBackCursor;
                newArea.CursorY.IsUserEnabled = true;
                newArea.CursorX.LineColor = _colorKeeper.ColorBackCursor;

                foreach (var axe in newArea.Axes)
                {
                    axe.LabelStyle.ForeColor = _colorKeeper.ColorText;
                }

                _chart.ChartAreas.Add(newArea);
                _areaPositions = new List<ChartAreaPosition>();
                ReloadChartAreasSize();
                return newArea;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// создать серию данных на области
        /// </summary>
        /// <returns>возвращается имя серии данных. null в случае ошибки</returns>
        public string CreateSeries(ChartArea area, IndicatorOneCandleChartType indicatorType, string name)
        {
            try
            {

                Series newSeries = new Series(name);

                newSeries.ChartArea = area.Name;
                newSeries.YAxisType = AxisType.Secondary;

                if (indicatorType == IndicatorOneCandleChartType.Line)
                {
                    newSeries.ChartType = SeriesChartType.Line;
                }
                if (indicatorType == IndicatorOneCandleChartType.Column)
                {
                    newSeries.ChartType = SeriesChartType.Column;
                }
                if (indicatorType == IndicatorOneCandleChartType.Point)
                {
                    newSeries.ChartType = SeriesChartType.Point;
                }

                newSeries.ShadowOffset = 2;
                _chart.Series.Add(newSeries);
                return newSeries.Name;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// взять область данных по имени
        /// </summary>
        /// <param name="name">имя области</param>
        /// <returns>возвращается область данных или null</returns>
        public ChartArea GetChartArea(string name)
        {
            return FindAreaByNameSafe(name);
        }

        /// <summary>
        /// взять чарт
        /// </summary>
        /// <returns>возвращаем чарт</returns>
        public Chart GetChart()
        {
            return _chart;
        }

        /// <summary>
        /// удаляет с графика индикатор и, если он был последний на области, то и область
        /// </summary>
        /// <param name="indicator">индикатор который следует удалить</param>
        public void DeleteIndicator(IIndicatorCandle indicator)
        {
            try
            {

                for (int i = 0; i < 20; i++)
                {
                    Series mySeries = FindSeriesByNameSafe(indicator.Name + i);

                    if (mySeries != null)
                    {
                        if (mySeries.Points.Count != 0)
                        {
                            ClearLabelOnY2(mySeries.Name + "Label", mySeries.ChartArea, mySeries.Points[0].Color);
                        }
                        mySeries.Points.Clear();
                        _chart.Series.Remove(FindSeriesByNameSafe(indicator.Name + i));

                        if (_labelSeries != null)
                        {
                            Series labelSeries = _labelSeries.Find(series => series.Name == indicator.Name + i + "label");
                            if (labelSeries != null)
                            {
                                _labelSeries.Remove(labelSeries);
                                _chart.Series.Remove(labelSeries);
                            }
                        }
                    }

                }



                if (indicator.NameArea == "Prime")
                {
                    return;
                }
                ChartArea area = FindAreaByNameSafe(indicator.NameArea);

                if (area == null)
                {
                    return;
                }
                bool haveAnoserSeries = false;
                for (int i = 0; i < _chart.Series.Count; i++)
                {
                    if (_chart.Series[i].ChartArea == area.Name && _chart.Series[i].Points.Count > 2)
                    {
                        haveAnoserSeries = true;
                    }
                    else if (_chart.Series[i].ChartArea == area.Name)
                    {
                        //_chart.Series.Remove(_chart.Series[i]);
                    }
                }

                if (haveAnoserSeries)
                {
                    return;
                }
                _chart.ChartAreas.Remove(FindAreaByNameSafe(indicator.NameArea));
                ShowAreaOnChart();

                _areaPositions = new List<ChartAreaPosition>();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// создан ли индикатор
        /// </summary>
        /// <param name="name">имя индикатора</param>
        /// <returns>true / false</returns>
        public bool IndicatorIsCreate(string name)
        {
            try
            {
                for (int i = 0; i < _chart.Series.Count; i++)
                {
                    if (_chart.Series[i].Name == name)
                    {
                        return true;
                    }

                    for (int i2 = 0; i2 < 20; i2++)
                    {
                        if (_chart.Series[i].Name == name + i2)
                        {
                            return true;
                        }
                    }

                }
                return false;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
            return false;
        }

        /// <summary>
        /// создана ли область
        /// </summary>
        /// <param name="name">имя области</param>
        /// <returns>try / false</returns>
        public bool AreaIsCreate(string name)
        {
            try
            {
                for (int i = 0; i < _chart.ChartAreas.Count; i++)
                {
                    if (FindAreaByNameSafe(name) != null)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
            return false;
        }

// работа потока прорисовывающего чарт

        /// <summary>
        /// метод в котором работает поток прорисовывающий чарт
        /// </summary>
        private void PainterThreadArea()
        {
            while (true)
            {
                Thread.Sleep(1000);

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
                    Thread.Sleep(5000);
                    _candlesToPaint = new ConcurrentQueue<List<Candle>>();
                    _indicatorsToPaint = new ConcurrentQueue<IIndicatorCandle>();
                }

                // проверяем, пришли ли свечи

                if (!_candlesToPaint.IsEmpty)
                {
                    List<Candle> candles = new List<Candle>();

                    while (!_candlesToPaint.IsEmpty)
                    {
                        _candlesToPaint.TryDequeue(out candles);
                    }

                    if (candles != null)
                    {
                        PaintCandles(candles);
                    }
                }

                // проверяем, пришли ли позиции

                if (!_positions.IsEmpty)
                {
                    List<Position> positions = new List<Position>();

                    while (!_positions.IsEmpty)
                    {
                        _positions.TryDequeue(out positions);
                    }

                    if (positions != null && positions.Count != 0)
                    {
                        PaintPositions(positions);
                    }
                }

                // проверяем, пришли ли тики

                if (!_tradesToPaint.IsEmpty)
                {
                    List<Trade> trades = new List<Trade>();

                    while (!_tradesToPaint.IsEmpty)
                    {
                        _tradesToPaint.TryDequeue(out trades);
                    }

                    if (trades != null)
                    {
                        PaintTrades(trades);
                    }
                }

                // проверяем, есть ли новые элементы для прорисовки на чарте

                if (!_chartElementsToPaint.IsEmpty)
                {
                    List<IChartElement> elements = new List<IChartElement>();

                    while (!_chartElementsToPaint.IsEmpty)
                    {
                        IChartElement newElement;
                        _chartElementsToPaint.TryDequeue(out newElement);

                        if (newElement != null)
                        {
                            elements.Add(newElement);
                        }
                    }

                    List<IChartElement> elementsWithoutRepiat = new List<IChartElement>();

                    for (int i = elements.Count-1; i > -1; i--)
                    {
                        if (elementsWithoutRepiat.Find(element => element.UniqName == elements[i].UniqName) == null)
                        {
                            elementsWithoutRepiat.Add(elements[i]);
                        }
                    }

                    for (int i = 0; i < elementsWithoutRepiat.Count; i++)
                    {
                        PaintElem(elementsWithoutRepiat[i]);
                    }
                }

 // проверяем, есть ли новые элементы для удаления с чарта

                if (!_chartElementsToClear.IsEmpty)
                {
                    List<IChartElement> elements = new List<IChartElement>();

                    while (!_chartElementsToClear.IsEmpty)
                    {
                        IChartElement newElement;
                        _chartElementsToClear.TryDequeue(out newElement);

                        if (newElement != null)
                        {
                            elements.Add(newElement);
                        }
                    }

                    List<IChartElement> elementsWithoutRepiat = new List<IChartElement>();

                    for (int i = elements.Count-1; i >-1 ; i--)
                    {
                        if (elementsWithoutRepiat.Find(element => element.UniqName == elements[i].UniqName) == null)
                        {
                            elementsWithoutRepiat.Add(elements[i]);
                        }
                    }

                    for (int i = 0; i < elementsWithoutRepiat.Count; i++)
                    {
                        ClearElem(elementsWithoutRepiat[i]);
                    }

                }
// проверяем, есть ли индикаторы для прорисовки на чарте

                if (!_indicatorsToPaint.IsEmpty)
                {
                    List<IIndicatorCandle> elements = new List<IIndicatorCandle>();

                    while (!_indicatorsToPaint.IsEmpty)
                    {
                        IIndicatorCandle newElement;
                        _indicatorsToPaint.TryDequeue(out newElement);

                        if (newElement != null)
                        {
                            elements.Add(newElement);
                        }
                    }

                    List<IIndicatorCandle> elementsWithoutRepiat = new List<IIndicatorCandle>();

                    for (int i = elements.Count-1; i >-1 ; i--)
                    {
                        if (elementsWithoutRepiat.Find(element => element.Name == elements[i].Name) == null)
                        {
                            elementsWithoutRepiat.Add(elements[i]);
                        }
                    }

                    for (int i = 0; i < elementsWithoutRepiat.Count; i++)
                    {
                        PaintIndicator(elementsWithoutRepiat[i]);
                    }
                }

                if (_startProgram == StartProgram.IsTester ||
                    _startProgram == StartProgram.IsOsOptimizer)
                {
                    Thread.Sleep(2000);
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
        private ConcurrentQueue<List<Candle>> _candlesToPaint = new ConcurrentQueue<List<Candle>>();

        /// <summary>
        /// очеедь с трейдами, котоыре нужно прорисовать
        /// </summary>
        private ConcurrentQueue<List<Trade>> _tradesToPaint = new ConcurrentQueue<List<Trade>>();

        /// <summary>
        /// очередь с позициями, которые нужно прорисовать
        /// </summary>
        private ConcurrentQueue<List<Position>> _positions = new ConcurrentQueue<List<Position>>(); 

        /// <summary>
        /// очередь с элементами чарта, которые нужно прорисовать
        /// </summary>
        private ConcurrentQueue<IChartElement> _chartElementsToPaint = new ConcurrentQueue<IChartElement>();

        /// <summary>
        /// очередь с элементами чарта, которые нужно удалить
        /// </summary>
        private ConcurrentQueue<IChartElement> _chartElementsToClear = new ConcurrentQueue<IChartElement>();

        /// <summary>
        /// очередь с индикаторами, которые нужно прорисовать
        /// </summary>
        private ConcurrentQueue<IIndicatorCandle> _indicatorsToPaint = new ConcurrentQueue<IIndicatorCandle>();

// свечи

        /// <summary>
        /// культура отрисовки данных
        /// </summary>
        private CultureInfo _culture = new CultureInfo("ru-RU");

        /// <summary>
        /// прорисовать свечки
        /// </summary>
        /// <param name="history">свечи</param>
        private void PaintCandles(List<Candle> history)
        {
            if (_mouseDown == true)
            {
                return;
            }
            try
            {
                if (history == null && _myCandles == null)
                {
                    return;
                }

                if (_chart.InvokeRequired)
                {
                    _chart.Invoke(new Action<List<Candle>>(PaintCandles), history);
                    return;
                }

                if (history == null)
                {
                    history = _myCandles;
                }
                else
                {
                    _myCandles = history;
                }

                Series oldcandleSeries = FindSeriesByNameSafe("SeriesCandle");

                if (oldcandleSeries == null)
                {
                    CreateChart();
                    oldcandleSeries = FindSeriesByNameSafe("SeriesCandle");
                }

                if (oldcandleSeries == null)
                {
                    return;
                }

                if (_startProgram != StartProgram.IsTester)
                { // в реальном подключении, каждую новую свечу рассчитываем 
                    // актуальные мультипликаторы для областей
                    ReloadAreaSizes();
                }
                else if (history.Count == 20 ||
                    history.Count == 50)
                {
                    ReloadAreaSizes();
                }

                if (history.Count - 1 == oldcandleSeries.Points.Count ||
                    oldcandleSeries.Points.Count - 1 == history.Count)
                {
                    AddCandleInArray(history, history.Count - 1);
                    RePaintToIndex(history, history.Count - 2);
                }
                else if (history.Count == oldcandleSeries.Points.Count)
                {
                    RePaintToIndex(history, history.Count - 1);
                }
                else
                {
                    if (history.Count < oldcandleSeries.Points.Count)
                    {
                        ClearZoom();
                    }

                    ReloadAreaSizes();
                    PaintAllCandles(history);
                    ResizeSeriesLabels();
                    RePaintRightLebels();
                    ResizeYAxisOnArea("Prime");
                }
                ResizeXAxis();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// добавить свечки на прорисовку
        /// </summary>
        /// <param name="history">свечи</param>
        public void ProcessCandles(List<Candle> history)
        {
            if ((_startProgram == StartProgram.IsTester ||
                 _startProgram == StartProgram.IsOsMiner) &&
                _host != null)
            {
                PaintCandles(history);
            }
            else
            {
                _candlesToPaint.Enqueue(history); 
            }
        }

        /// <summary>
        /// перерисовать свечку по индексу
        /// </summary>
        /// <param name="history">свечи</param>
        /// <param name="index">индекс</param>
        private void RePaintToIndex(List<Candle> history, int index)
        {
            if (index < 0 || history == null)
            {
                return;
            }

            if (index >= history.Count)
            {
                index = history.Count - 1;
                //return;
            }

            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action<List<Candle>, int>(RePaintToIndex), history, index);
                return;
            }
            Series candleSeries = FindSeriesByNameSafe("SeriesCandle");

            if (candleSeries == null)
            {
                return;
            }

            try
            {

                if (index >= candleSeries.Points.Count ||
                    index >= history.Count ||
                    Convert.ToDecimal(candleSeries.Points[index].YValues[3]) == history[index].Close)
                {
                    return;
                }

                double[] doubles = new double[] { Convert.ToDouble(history[index].Low), Convert.ToDouble(history[index].High),
                    Convert.ToDouble(history[index].Open), Convert.ToDouble(history[index].Close) };

                candleSeries.Points[index].YValues = doubles;
                if (history[index].Close > history[index].Open)
                {
                    candleSeries.Points[index].Color = _colorKeeper.ColorUpBorderCandle;
                    candleSeries.Points[index].BorderColor = _colorKeeper.ColorUpBorderCandle;
                    candleSeries.Points[index].BackSecondaryColor = _colorKeeper.ColorUpBodyCandle;
                }
                else
                {
                    candleSeries.Points[index].Color = _colorKeeper.ColorDownBorderCandle;
                    candleSeries.Points[index].BorderColor = _colorKeeper.ColorDownBorderCandle;
                    candleSeries.Points[index].BackSecondaryColor = _colorKeeper.ColorDownBodyCandle;
                }

                candleSeries.Points[index].ToolTip = history[index].ToolTip;

                ChartArea candleArea = FindAreaByNameSafe("Prime");
                if (candleArea != null && candleArea.AxisX.ScrollBar.IsVisible &&
                    candleArea.AxisX.ScaleView.Position + candleArea.AxisX.ScaleView.Size + 3 >= candleSeries.Points.Count)
                //если уже выбран какой-то диапазон
                {
                    // сдвигаем представление вправо
                    candleArea.AxisX.ScaleView.Scroll(candleSeries.Points.Count + 1);
                }

                RePaintRightLebels();

                ResizeYAxisOnArea("Prime");
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// прорисовать последнюю свечку
        /// </summary>
        /// <param name="history">свечи</param>
        private void AddCandleInArray(List<Candle> history, int index)
        {
            Series candleSeries = FindSeriesByNameSafe("SeriesCandle");

            if (candleSeries == null)
            {
                return;
            }

            candleSeries.Points.AddXY(index, history[index].Low, history[index].High, history[index].Open, history[index].Close);
            if (history[index].Close > history[index].Open)
            {
                candleSeries.Points[candleSeries.Points.Count - 1].Color = _colorKeeper.ColorUpBorderCandle;
                candleSeries.Points[candleSeries.Points.Count - 1].BorderColor = _colorKeeper.ColorUpBorderCandle;
                candleSeries.Points[candleSeries.Points.Count - 1].BackSecondaryColor = _colorKeeper.ColorUpBodyCandle;
            }
            else
            {
                candleSeries.Points[candleSeries.Points.Count - 1].Color = _colorKeeper.ColorDownBorderCandle;
                candleSeries.Points[candleSeries.Points.Count - 1].BorderColor = _colorKeeper.ColorDownBorderCandle;
                candleSeries.Points[candleSeries.Points.Count - 1].BackSecondaryColor = _colorKeeper.ColorDownBodyCandle;
            }

           
            candleSeries.Points[candleSeries.Points.Count - 1].ToolTip = history[candleSeries.Points.Count - 1].ToolTip;

            // candleSeries.Points[candleSeries.Points.Count - 1].AxisLabel = history[lastIndex].TimeStart.ToString(_culture);

            ChartArea candleArea = FindAreaByNameSafe("Prime");
            if (candleArea != null && candleArea.AxisX.ScrollBar.IsVisible &&
                candleArea.AxisX.ScaleView.Position + candleArea.AxisX.ScaleView.Size + 3 >= candleSeries.Points.Count)
                //если уже выбран какой-то диапазон
            {
                // сдвигаем представление вправо
                candleArea.AxisX.ScaleView.Scroll(candleSeries.Points.Count + 1);
            }

            if (candleSeries.Points.Count > 1)
            {
                candleSeries.Points[candleSeries.Points.Count - 2].Label = "";
            }

            RePaintRightLebels();
         
            ResizeYAxisOnArea("Prime");
 
        }

        /// <summary>
        /// прорисовать все свечки
        /// </summary>
        /// <param name="history">свечи</param>
        private void PaintAllCandles(List<Candle> history)
        {
            Series candleSeries = new Series("SeriesCandle");
            candleSeries.ChartType = SeriesChartType.Candlestick;
            candleSeries.YAxisType = AxisType.Secondary;
            candleSeries.ChartArea = "Prime";
            candleSeries.ShadowOffset = 2;
            candleSeries.YValuesPerPoint = 4;

            for (int i = 0; i < history.Count; i++)
            {
                candleSeries.Points.AddXY(i, history[i].Low, history[i].High, history[i].Open, history[i].Close);

                if (history[i].Close > history[i].Open)
                {
                    candleSeries.Points[candleSeries.Points.Count - 1].Color = _colorKeeper.ColorUpBorderCandle;
                    candleSeries.Points[candleSeries.Points.Count - 1].BorderColor = _colorKeeper.ColorUpBorderCandle;
                    candleSeries.Points[candleSeries.Points.Count - 1].BackSecondaryColor = _colorKeeper.ColorUpBodyCandle;
                }
                else
                {
                    candleSeries.Points[candleSeries.Points.Count - 1].Color = _colorKeeper.ColorDownBorderCandle;
                    candleSeries.Points[candleSeries.Points.Count - 1].BorderColor = _colorKeeper.ColorDownBorderCandle;
                    candleSeries.Points[candleSeries.Points.Count - 1].BackSecondaryColor = _colorKeeper.ColorDownBodyCandle;
                }

                candleSeries.Points[candleSeries.Points.Count - 1].ToolTip = history[candleSeries.Points.Count - 1].ToolTip;

                // candleSeries.Points[candleSeries.Points.Count - 1].AxisLabel = history[i].TimeStart.ToString(new CultureInfo("ru-RU"));
            }

            ChartArea candleArea = FindAreaByNameSafe("Prime");
            if (candleArea != null && candleArea.AxisX.ScrollBar.IsVisible)
            //если уже выбран какой-то диапазон
            {
                // сдвигаем представление вправо
                candleArea.AxisX.ScaleView.Scroll(candleSeries.Points.Count + 1 - candleArea.AxisX.ScaleView.Size);
            }

            if (FindSeriesByNameSafe("Cursor") != null)
            {
                ReMoveSeriesSafe(FindSeriesByNameSafe("Cursor"));
            }

            PaintSeriesSafe(candleSeries);
           
            ReloadAreaSizes();
        }

// тики

        /// <summary>
        /// прорисовать тики
        /// </summary>
        /// <param name="trades">тики</param>
        private void PaintTrades(List<Trade> trades)
        {
            if (_mouseDown == true)
            {
                return;
            }
            try
            {
                ChartArea tickArea = FindAreaByNameSafe("TradeArea");

                if (tickArea == null)
                {
                    return;
                }

                if (_lastTickTime != DateTime.MinValue &&
                    _lastTickTime == trades[trades.Count - 1].Time)
                {
                    return;
                }

                _lastTickTime = trades[trades.Count - 1].Time;

                PaintLast300(trades);
                ResizeTickArea();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// добавить тики в прорисовку
        /// </summary>
        /// <param name="trades">тики</param>
        public void ProcessTrades(List<Trade> trades)
        {
            if (_startProgram == StartProgram.IsTester &&
                _host != null)
            {
                PaintTrades(trades);
            }
            else
            {
                _tradesToPaint.Enqueue(trades);
            }
        }

        /// <summary>
        /// создать область для тиковых данных
        /// </summary>
        public void CreateTickArea()
        {
            try
            {
                ChartArea tickArea = FindAreaByNameSafe("TradeArea");

                if (tickArea != null)
                {
                    return;
                }

                tickArea = CreateArea("TradeArea", 25);
                tickArea.AlignWithChartArea = "";
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// удалить область для тиковых данных
        /// </summary>
        public void DeleteTickArea()
        {
            try
            {
                ChartArea tickArea = FindAreaByNameSafe("TradeArea");

                if (tickArea != null)
                {
                    _chart.ChartAreas.Remove(tickArea);
                    ShowAreaOnChart();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// время прихода последних тиков
        /// </summary>
        private DateTime _lastTickTime = DateTime.MinValue;

        /// <summary>
        /// прорисовать последние 300 тиков
        /// </summary>
        /// <param name="history">тики</param>
        private void PaintLast300(List<Trade> history)
        {
            Series tradeSeries = new Series("trade");
            tradeSeries.ChartType = SeriesChartType.Point;
            tradeSeries.MarkerStyle = MarkerStyle.Circle;
            tradeSeries.YAxisType = AxisType.Secondary;
            tradeSeries.ChartArea = "TradeArea";

            decimal max = 0;

            for (int i = history.Count - 200; i < history.Count; i++)
            {
                if (i < 0)
                {
                    i = 0;
                }
                if (history[i].Volume > max)
                {
                    max = history[i].Volume;
                }
            }


            for (int index = 0, i = history.Count - 200; i < history.Count; i++,index++)
            {
                if (i < 0)
                {
                    i = 0;
                }

                tradeSeries.Points.AddXY(index, history[i].Price);

                if (history[i].Side == Side.Buy)
                {
                    tradeSeries.Points[tradeSeries.Points.Count - 1].Color = _colorKeeper.ColorUpBodyCandle;

                    tradeSeries.Points[tradeSeries.Points.Count - 1].MarkerSize = 1;
                }
                else
                {
                    tradeSeries.Points[tradeSeries.Points.Count - 1].Color = _colorKeeper.ColorDownBorderCandle;
                    tradeSeries.Points[tradeSeries.Points.Count - 1].MarkerSize = 1;
                }
                if(max == 0)
                {
                    return;
                }
                decimal categori = history[i].Volume/max;
                
                if(categori< 0.02m)
                {
                    tradeSeries.Points[tradeSeries.Points.Count - 1].MarkerSize = 1;
                }
                else if (categori < 0.05m)
                {
                    tradeSeries.Points[tradeSeries.Points.Count - 1].MarkerSize = 2;
                }
                else if (categori < 0.1m)
                {
                    tradeSeries.Points[tradeSeries.Points.Count - 1].MarkerSize = 3;
                }
                else if (categori < 0.3m)
                {
                    tradeSeries.Points[tradeSeries.Points.Count - 1].MarkerSize = 4;
                }
                else if (categori < 0.5m)
                {
                    tradeSeries.Points[tradeSeries.Points.Count - 1].MarkerSize = 5;
                }
                else if (categori < 0.7m)
                {
                    tradeSeries.Points[tradeSeries.Points.Count - 1].MarkerSize = 6;
                }
                else 
                {
                    tradeSeries.Points[tradeSeries.Points.Count - 1].MarkerSize = 8;
                }
            }

            PaintSeriesSafe(tradeSeries);
        }

// сделки

        /// <summary>
        /// прорисовать сделки на графике
        /// </summary>
        /// <param name="deals">сделки</param>
        private void PaintPositions(List<Position> deals)
        {
            try
            {
                if (_mouseDown == true)
                {
                    //return;
                }

                if (_myCandles == null)
                {
                    return;
                }
                if (deals == null || _myCandles == null)
                {
                    return;
                }

                if (_isPaint == false)
                {
                    return;
                }

                Series buySellSeries;
                Series[] stopProfitOrderSeries = null;

                // формируем точки покупок и продаж

                buySellSeries = new Series("Deal_" + "BuySell");

                buySellSeries.YAxisType = AxisType.Secondary;
                buySellSeries.ChartArea = "Prime";
                buySellSeries.YValuesPerPoint = 1;
                
                buySellSeries.ChartType = SeriesChartType.Point;
                buySellSeries.MarkerStyle = MarkerStyle.Cross;

                if (_colorKeeper.PointsSize == ChartPositionTradeSize.Size4)
                {
                    buySellSeries.MarkerSize = 12;
                }
                if (_colorKeeper.PointsSize == ChartPositionTradeSize.Size3)
                {
                    buySellSeries.MarkerSize = 11;
                }
                if (_colorKeeper.PointsSize == ChartPositionTradeSize.Size2)
                {
                    buySellSeries.MarkerSize = 10;
                }
                if (_colorKeeper.PointsSize == ChartPositionTradeSize.Size1)
                {
                    buySellSeries.MarkerSize = 9;
                }

                if (_colorKeeper.PointType == PointType.Cross)
                {
                    buySellSeries.MarkerStyle = MarkerStyle.Cross;
                }
                if (_colorKeeper.PointType == PointType.Circle)
                {
                    buySellSeries.MarkerStyle = MarkerStyle.Circle;
                }
                else if (_colorKeeper.PointType == PointType.Romb)
                {
                    buySellSeries.MarkerStyle = MarkerStyle.Diamond;
                }
                else if (_colorKeeper.PointType == PointType.TriAngle)
                {
                    buySellSeries.MarkerStyle = MarkerStyle.Triangle;
                }

                for (int i = 0; i < deals.Count; i++)
                {
                    List<MyTrade> trades = deals[i].MyTrades;

                    for (int indTrades = 0; indTrades < trades.Count; indTrades++)
                    {
                        DateTime timePoint = trades[indTrades].Time;

                        if (timePoint == DateTime.MinValue)
                        {
                            continue;
                        }
                        int xIndexPoint = GetTimeIndex(timePoint);
                        if (xIndexPoint == 0)
                        {
                            continue;
                        }

                        buySellSeries.Points.AddXY(xIndexPoint, trades[indTrades].Price);

                        buySellSeries.Points[buySellSeries.Points.Count - 1].ToolTip = trades[indTrades].ToolTip;

                        if (_colorKeeper.PointType == PointType.TriAngle)
                        {
                            // прорисовываем картинки
                            double openViewSize = _myCandles.Count;

                            if (_chart.ChartAreas[0].AxisX.ScaleView.IsZoomed)
                            {
                                openViewSize = _chart.ChartAreas[0].AxisX.ScaleView.Size;
                            }
                            if (trades[indTrades].Side == Side.Buy)
                            {
                                if (_colorKeeper.PointsSize == ChartPositionTradeSize.Size4)
                                {
                                    buySellSeries.Points[buySellSeries.Points.Count - 1].MarkerImage =
                                        @"Images\pickUpSize4.png";
                                }
                                else if (_colorKeeper.PointsSize == ChartPositionTradeSize.Size3)
                                {
                                    buySellSeries.Points[buySellSeries.Points.Count - 1].MarkerImage =
                                        @"Images\pickUpSize3.png";
                                }
                                else if (_colorKeeper.PointsSize == ChartPositionTradeSize.Size2)
                                {
                                    buySellSeries.Points[buySellSeries.Points.Count - 1].MarkerImage =
                                        @"Images\pickUpSize2.png";
                                }
                                else if (_colorKeeper.PointsSize == ChartPositionTradeSize.Size1)
                                {
                                    buySellSeries.Points[buySellSeries.Points.Count - 1].MarkerImage =
                                        @"Images\pickUpSize2.png";
                                }
                            }
                            else
                            {
                                if (_colorKeeper.PointsSize == ChartPositionTradeSize.Size4)
                                {
                                    buySellSeries.Points[buySellSeries.Points.Count - 1].MarkerImage =
                                        @"Images\pickDownSize4.png";
                                }
                                if (_colorKeeper.PointsSize == ChartPositionTradeSize.Size3)
                                {
                                    buySellSeries.Points[buySellSeries.Points.Count - 1].MarkerImage =
                                        @"Images\pickDownSize3.png";
                                }
                                else if (_colorKeeper.PointsSize == ChartPositionTradeSize.Size2)
                                {
                                    buySellSeries.Points[buySellSeries.Points.Count - 1].MarkerImage =
                                        @"Images\pickDownSize2.png";
                                }
                                else if (_colorKeeper.PointsSize == ChartPositionTradeSize.Size1)
                                {
                                    buySellSeries.Points[buySellSeries.Points.Count - 1].MarkerImage =
                                        @"Images\pickDownSize2.png";
                                }
                            }
                        }

                        if (trades[indTrades].Side == Side.Buy)
                        {
                            buySellSeries.Points[buySellSeries.Points.Count - 1].Color = Color.Aqua;
                        }
                        else
                        {
                            buySellSeries.Points[buySellSeries.Points.Count - 1].Color = Color.DarkRed;

                        }
                    }
                }

                // формируем линии Профитов и Стопов
                for (int i = 0; i < deals.Count; i++)
                { // проходим Лимит ОРДЕРА НА ОТКРЫТИИ

                    if (deals[i].State == PositionStateType.Opening && deals[i].OpenOrders != null &&
                        deals[i].OpenOrders[deals[i].OpenOrders.Count - 1].State == OrderStateType.Activ)
                    {
                        Series lineSeries = new Series("Open_" + deals[i].Number);
                        lineSeries.ChartType = SeriesChartType.Line;
                        lineSeries.YAxisType = AxisType.Secondary;
                        lineSeries.XAxisType = AxisType.Secondary;
                        lineSeries.ChartArea = "Prime";
                        lineSeries.ShadowOffset = 1;
                        lineSeries.YValuesPerPoint = 1;


                        lineSeries.Points.AddXY(0, deals[i].OpenOrders[deals[i].OpenOrders.Count - 1].Price);
                        lineSeries.Points.AddXY(_myCandles.Count + 500, deals[i].OpenOrders[deals[i].OpenOrders.Count - 1].Price);

                        if (deals[i].OpenOrders[deals[i].OpenOrders.Count - 1].Side == Side.Buy)
                        {
                            lineSeries.Color = Color.Green;
                        }
                        else
                        {
                            lineSeries.Color = Color.Red;
                        }

                        if (stopProfitOrderSeries == null)
                        {
                            stopProfitOrderSeries = new[] { lineSeries };
                        }
                        else
                        {
                            Series[] newLineSeries = new Series[stopProfitOrderSeries.Length + 1];
                            for (int i2 = 0; i2 < stopProfitOrderSeries.Length; i2++)
                            {
                                newLineSeries[i2] = stopProfitOrderSeries[i2];
                            }
                            newLineSeries[newLineSeries.Length - 1] = lineSeries;
                            stopProfitOrderSeries = newLineSeries;
                        }
                    }
                }
                for (int i = 0; i < deals.Count; i++)
                { // проходим Лимит ОРДЕРА НА ЗАКРЫТИИ

                    if (i == 16)
                    {

                    }
                    if (deals[i].State == PositionStateType.Closing &&
                        deals[i].CloseOrders != null &&
                        deals[i].CloseOrders[deals[i].CloseOrders.Count - 1].State == OrderStateType.Activ)
                    {
                        Series lineSeries = new Series("Close_" + deals[i].Number);
                        lineSeries.ChartType = SeriesChartType.Line;
                        lineSeries.YAxisType = AxisType.Secondary;
                        lineSeries.XAxisType = AxisType.Secondary;
                        lineSeries.ChartArea = "Prime";
                        lineSeries.ShadowOffset = 1;
                        lineSeries.YValuesPerPoint = 1;


                        lineSeries.Points.AddXY(0, deals[i].CloseOrders[deals[i].CloseOrders.Count - 1].Price);
                        lineSeries.Points.AddXY(_myCandles.Count + 500, deals[i].CloseOrders[deals[i].CloseOrders.Count - 1].Price);

                        if (deals[i].CloseOrders[deals[i].CloseOrders.Count - 1].Side == Side.Buy)
                        {
                            lineSeries.Color = Color.Green;
                        }
                        else
                        {
                            lineSeries.Color = Color.Red;
                        }

                        if (stopProfitOrderSeries == null)
                        {
                            stopProfitOrderSeries = new[] { lineSeries };
                        }
                        else
                        {
                            Series[] newLineSeries = new Series[stopProfitOrderSeries.Length + 1];
                            for (int i2 = 0; i2 < stopProfitOrderSeries.Length; i2++)
                            {
                                newLineSeries[i2] = stopProfitOrderSeries[i2];
                            }
                            newLineSeries[newLineSeries.Length - 1] = lineSeries;
                            stopProfitOrderSeries = newLineSeries;
                        }
                    }
                }
                for (int i = 0; i < deals.Count; i++)
                { // проходим СТОП ОРДЕРА
                    if (
                        (deals[i].State == PositionStateType.Open || deals[i].State == PositionStateType.Closing) &&
                        deals[i].StopOrderIsActiv)
                    {
                        Series lineSeries = new Series("Stop_" + deals[i].Number);
                        lineSeries.ChartType = SeriesChartType.Line;
                        lineSeries.YAxisType = AxisType.Secondary;
                        lineSeries.XAxisType = AxisType.Secondary;
                        lineSeries.ChartArea = "Prime";
                        lineSeries.ShadowOffset = 1;
                        lineSeries.YValuesPerPoint = 1;


                        lineSeries.Points.AddXY(0, deals[i].StopOrderRedLine);
                        lineSeries.Points.AddXY(_myCandles.Count + 500, deals[i].StopOrderRedLine);

                        if (deals[i].Direction == Side.Sell)
                        {
                            lineSeries.Color = Color.Green;
                        }
                        else
                        {
                            lineSeries.Color = Color.Red;
                        }

                        if (stopProfitOrderSeries == null)
                        {
                            stopProfitOrderSeries = new[] { lineSeries };
                        }
                        else
                        {
                            Series[] newLineSeries = new Series[stopProfitOrderSeries.Length + 1];
                            for (int i2 = 0; i2 < stopProfitOrderSeries.Length; i2++)
                            {
                                newLineSeries[i2] = stopProfitOrderSeries[i2];
                            }
                            newLineSeries[newLineSeries.Length - 1] = lineSeries;
                            stopProfitOrderSeries = newLineSeries;
                        }
                    }
                }

                for (int i = 0; i < deals.Count; i++)
                { // проходим ПРОФИТ ОРДЕРА
                    if (
                       (deals[i].State == PositionStateType.Open || deals[i].State == PositionStateType.Closing) &&
                        deals[i].ProfitOrderIsActiv)
                    {
                        Series lineSeries = new Series("Profit_" + deals[i].Number);
                        lineSeries.ChartType = SeriesChartType.Line;
                        lineSeries.YAxisType = AxisType.Secondary;
                        lineSeries.XAxisType = AxisType.Secondary;
                        lineSeries.ChartArea = "Prime";
                        lineSeries.ShadowOffset = 1;
                        lineSeries.YValuesPerPoint = 1;

                        lineSeries.Points.AddXY(0, deals[i].ProfitOrderRedLine);
                        lineSeries.Points.AddXY(_myCandles.Count + 500, deals[i].ProfitOrderRedLine);

                        if (deals[i].Direction == Side.Sell)
                        {
                            lineSeries.Color = Color.Green;
                        }
                        else
                        {
                            lineSeries.Color = Color.Red;
                        }

                        if (stopProfitOrderSeries == null)
                        {
                            stopProfitOrderSeries = new[] { lineSeries };
                        }
                        else
                        {
                            Series[] newLineSeries = new Series[stopProfitOrderSeries.Length + 1];
                            for (int i2 = 0; i2 < stopProfitOrderSeries.Length; i2++)
                            {
                                newLineSeries[i2] = stopProfitOrderSeries[i2];
                            }
                            newLineSeries[newLineSeries.Length - 1] = lineSeries;
                            stopProfitOrderSeries = newLineSeries;
                        }
                    }
                }

                PaintDealSafe(buySellSeries, stopProfitOrderSeries);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// добавить позиции в прорисовку
        /// </summary>
        /// <param name="deals">сделки</param>
        public void ProcessPositions(List<Position> deals)
        {
            if (_startProgram == StartProgram.IsTester &&
                _host != null)
            {
                PaintPositions(deals);
            }
            else
            {
                _positions.Enqueue(deals);
            }
        }

        /// <summary>
        /// прорисовать серию со сделками безопасно
        /// </summary>
        /// <param name="buySellSeries">серия точек</param>
        /// <param name="profitStopOrderSeries">серии стоп ордеров</param>
        private void PaintDealSafe(Series buySellSeries, Series [] profitStopOrderSeries)
        {
            try
            {
                if (_chart.InvokeRequired)
                {
                    _chart.Invoke(new Action<Series, Series[]>
                        (PaintDealSafe), buySellSeries, profitStopOrderSeries);
                    return;
                }

                // 1 прорисовываем сделки

                if (FindSeriesByNameSafe(buySellSeries.Name) == null)
                {
                    _chart.Series.Add(buySellSeries);
                }
                else
                {
                    _chart.Series.Remove(FindSeriesByNameSafe(buySellSeries.Name));
                    _chart.Series.Add(buySellSeries);
                }

                // 2 прорисовываем стопы

                for (int i = 0; _chart.Series != null && i < _chart.Series.Count; i++)
                { // удаляем старые
                    string name = _chart.Series[i].Name.Split('_')[0];

                    if (name == "Stop" ||
                        name == "Profit" ||
                        name == "Open" ||
                        name == "Close")
                    {
                        _chart.Series.Remove(_chart.Series[i]);
                        i--;
                    }
                }

                for (int i = 0; profitStopOrderSeries != null && i < profitStopOrderSeries.Length; i++)
                {
                    _chart.Series.Add(profitStopOrderSeries[i]);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

// поиск индекса по времени

        /// <summary>
        /// временные точки на графике привязанные к оси X
        /// </summary>
        private List<TimeAxisXPoint> _timePoints = new List<TimeAxisXPoint>();

        /// <summary>
        /// взять индекс свечи по времени
        /// </summary>
        /// <param name="time">время</param>
        /// <returns>индекс. Если время не найдено, возвращает 0</returns>
        private int GetTimeIndex(DateTime time)
        {
            if (_timePoints == null)
            {
                _timePoints = new List<TimeAxisXPoint>();
            }
            TimeAxisXPoint point = _timePoints.Find(po => po.PositionTime == time);

            if (point != null)
            {
                return point.PositionXPoint;
            }

            int result;
            if (_myCandles.Count < 20)
            {
                result = SimpleSearch(time);
            }
            else
            {
                result = MagicSearch(time, _myCandles, 0, _myCandles.Count);
            }
           
            point = new TimeAxisXPoint();
            point.PositionTime = time;
            point.PositionXPoint = result;
            _timePoints.Add(point);

            return result;
        }

        /// <summary>
        /// поиск точки на оси Х в лоб
        /// </summary>
        /// <param name="time">время по которому нужно узнать индекс на оси Х</param>
        /// <returns></returns>
        private int SimpleSearch(DateTime time)
        {
            try
            {
                List<Candle> myCandles = _myCandles;

                if (myCandles == null)
                {
                    return 0;
                }

                int result = myCandles.FindIndex
                    (
                        candle =>
                            (
                                candle.Trades != null &&
                                candle.Trades.Count != 0 &&
                                candle.Trades[candle.Trades.Count - 1].Time > time
                                )
                            ||
                            candle.TimeStart >= time 
                            ||
                            (  _timeFrameIsActivate &&
                               candle.TimeStart.Add(_timeFrameSpan) > time
                            )
                    );

                if (result < 0)
                {
                    result = 0;
                }

                return result;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
            return 0;
        }

        /// <summary>
        /// рекурсивный, быстрый метод поиска точки на оси Х
        /// </summary>
        private int MagicSearch(DateTime time, List<Candle> candles, int start, int end)
        {
            if(end - start < 5 ||
                start > end)
            {

            }
            if (end - start < 50)
            {
                for (int i = start; i < end; i++)
                {
                    if (candles[i].TimeStart >= time)
                    {
                        return i;
                    }
                    if (candles[i].Trades != null &&
                        candles[i].Trades.Count != 0 &&
                        candles[i].Trades[candles[i].Trades.Count - 1].Time > time)
                    {
                        return i;
                    }
                    if (
                        (_timeFrameIsActivate &&
                         candles[i].TimeStart.Add(_timeFrameSpan) > time
                            ))
                    {
                        return i;
                    }
                }
                return end;
            }

            if (candles[start + (end - start)/2].TimeStart > time)
            {
                return MagicSearch(time, candles, start, start + (end - start) / 2);
            }
            else if (candles[start + (end - start) / 2].TimeStart < time)
            {
                return MagicSearch(time, candles, start + (end - start) / 2, end);
            }
            else
            {
                return start + (end - start) / 2;
            }
        }

// ПОЛЬЗОВАТЕЛЬСКИЕ ЭЛЕМЕНТЫ

        /// <summary>
        /// прорисовать элемент на чарте
        /// </summary>
        /// <param name="element">элемент</param>
        private void PaintElem(IChartElement element)
        {
            try
            {

                if (_chartElements == null)
                {
                    _chartElements = new List<IChartElement>();
                }
                if (_chartElements.Find(chartElement => chartElement.UniqName == element.UniqName) == null)
                {
                    _chartElements.Add(element);
                }

                if (_isPaint == false)
                {
                    return;
                }
                if (element.TypeName() == "LineHorisontal")
                {
                    PaintHorisiontalLineOnArea((LineHorisontal)element);
                }
                else if (element.TypeName() == "PointElement")
                {
                    PointElement elem = (PointElement)element;
                    PaintPoint(elem);
                }

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// добавить элемент в прорисовку
        /// </summary>
        /// <param name="element">элемент</param>
        public void ProcessElem(IChartElement element)
        {
            if (_startProgram == StartProgram.IsTester &&
                _host != null)
            {
                PaintElem(element);
            }
            else
            {
                _chartElementsToPaint.Enqueue(element);
            }
        }

        /// <summary>
        /// элементы на чарте
        /// </summary>
        private List<IChartElement> _chartElements;

        private void ClearElem(IChartElement element)
        {
            try
            {
                if (_chart.InvokeRequired)
                {
                    _chart.Invoke(new Action<IChartElement>(ClearElem), element);
                    return;
                }

                // если есть такая серия на графике - удаляем
                if (_chart.Series != null && _chart.Series.Count != 0)
                {
                    Series mySeries = FindSeriesByNameSafe(element.UniqName);

                    if (mySeries != null)
                    {
                        _chart.Series.Remove(mySeries);
                    }

                    Series mySeriesPoint = FindSeriesByNameSafe(element.UniqName + "Point");

                    if (mySeriesPoint != null)
                    {
                        _chart.Series.Remove(mySeriesPoint);
                    }
                }

                if (_chartElements != null && _chartElements.Count != 0)
                {
                    _chartElements.Remove(element);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// убрать элемент с чарта
        /// </summary>
        public void ProcessClearElem(IChartElement element)
        {
            if (_startProgram == StartProgram.IsTester &&
                _host != null)
            {
                ClearElem(element);
            }
            else
            {
                _chartElementsToClear.Enqueue(element);
            }
        }

        /// <summary>
        /// прорисовать горизонтальную линию через весь чарт
        /// </summary>
        /// <param name="lineElement">линия</param>
        public void PaintHorisiontalLineOnArea(LineHorisontal lineElement)
        {
            if (lineElement.Value == 0)
            {
                return;
            }
            if (_myCandles == null)
            {
                return;
            }

            Series newSeries = new Series(lineElement.UniqName);
            newSeries.ChartType = SeriesChartType.Line;
            newSeries.Color = lineElement.Color;
            newSeries.ChartArea = lineElement.Area;
            newSeries.YAxisType = AxisType.Secondary;
            newSeries.XAxisType = AxisType.Primary;

            if (!string.IsNullOrWhiteSpace(lineElement.Label))
            {
                newSeries.Label = lineElement.Label;
                newSeries.LabelForeColor = _colorKeeper.ColorText;
            }

            int firstIndex = 0;
            int secondIndex = _myCandles.Count-1;

            if (lineElement.TimeStart != DateTime.MinValue)
            {
                int index = _myCandles.FindIndex(candle => candle.TimeStart >= lineElement.TimeStart);
                if (index >= 0)
                {
                    firstIndex = index;
                }
            }

            if (lineElement.TimeEnd != DateTime.MaxValue)
            {
                int index = _myCandles.FindIndex(candle => candle.TimeStart >= lineElement.TimeEnd);

                if (index > 0)
                {
                    secondIndex = index;
                }
            }

            if (firstIndex > secondIndex)
            {
                return;
            }

            if (firstIndex < 0 || firstIndex >= _myCandles.Count ||
                secondIndex < 0 || secondIndex >= _myCandles.Count)
            {
                return;
            }

            newSeries.Label = lineElement.Label;
            newSeries.Points.AddXY(firstIndex, lineElement.Value);
            newSeries.Points.AddXY(secondIndex, lineElement.Value);
            PaintSeriesSafe(newSeries);

            if (!lineElement.CanResize)
            { // если двигать нельзя, выходим
                return;
            }

            Series newSeriesPoint = new Series(lineElement.UniqName+ "Point");
            newSeriesPoint.ChartType = SeriesChartType.Point;
            newSeriesPoint.Color = Color.WhiteSmoke;
            newSeriesPoint.ChartArea = lineElement.Area;
            newSeriesPoint.YAxisType = AxisType.Secondary;
            newSeriesPoint.XAxisType = AxisType.Primary;
            newSeriesPoint.MarkerStyle= MarkerStyle.Square;
            //newSeriesPoint.MarkerImage = "OsLogo16.png";
            newSeriesPoint.MarkerSize = 9;
            newSeriesPoint.Points.AddXY(firstIndex, lineElement.Value);

            PaintSeriesSafe(newSeriesPoint);
        }

        /// <summary>
        /// нарисовать на чарте точку
        /// </summary>
        /// <param name="point"></param>
        public void PaintPoint(PointElement point)
        {
            if (point.Y <= 0)
            {
                return;
            }
            if (_myCandles == null)
            {
                return;
            }

            int index = _myCandles.FindIndex(candle => candle.TimeStart >= point.TimePoint);

            if (index < 0 || index >= _myCandles.Count)
            {
                return;
            }

            Series newSeries = new Series(point.UniqName + "Point");
            newSeries.ChartType = SeriesChartType.Point;
            newSeries.Color = point.Color;
            newSeries.ChartArea = point.Area;
            newSeries.YAxisType = AxisType.Secondary;
            newSeries.XAxisType = AxisType.Primary;
            newSeries.MarkerStyle = point.Style;
            newSeries.MarkerSize = point.Size;

            newSeries.Points.AddXY(index, point.Y);

            PaintSeriesSafe(newSeries);
        }

// Перетаскивание Пользовательских элементов

        /// <summary>
        /// элемент по которому пользователь произвёл клик
        /// </summary>
        private IChartElement _clickElement;

        /// <summary>
        /// началось ли перетаскивание элемента
        /// </summary>
        private bool _isMoving;

        /// <summary>
        /// значение линии по оси игрик при нажатии на кнопку
        /// </summary>
        private decimal _yPoint; 

        /// <summary>
        /// значение линии по оси икс при нажатии на кнопку
        /// </summary>
        private decimal _xPoint; 

        /// <summary>
        /// значение курсора по оси игрик при нажатии на кнопку
        /// </summary>
        private decimal _yMouse;

        /// <summary>
        /// значение курсора по оси икс при нажатии на точку
        /// </summary>
        private decimal _xMouse; 

        /// <summary>
        /// двинулась мышь по графику
        /// </summary>
        void _chartForCandle_MouseMove2ChartElement(object sender, MouseEventArgs e)
        {
            if (_chart.Cursor != Cursors.SizeAll)
            { // если пользователь не зажал элемент
                return;
            }
// игрик
            try
            {
                decimal lastY = e.Y;

                _isMoving = true;

                decimal highInPicsel = _host.Child.Height * Convert.ToDecimal(_chart.ChartAreas[0].InnerPlotPosition.Height / 100);
                decimal highInPrice = Convert.ToDecimal(_chart.ChartAreas[0].AxisY2.Maximum - _chart.ChartAreas[0].AxisY2.Minimum);

                decimal priceToOnePixel = highInPrice / highInPicsel;

                decimal moveMouse = lastY - _yMouse;

                decimal movePrice = moveMouse * priceToOnePixel;

                // округлить надо 

                string[] str = _chart.ChartAreas[0].AxisY2.Maximum.ToString(new CultureInfo("ru-RU")).Split(',');
                int precise = str.Length;

                if (precise == 1)
                {
                    movePrice = Convert.ToInt32(movePrice);
                    _yPoint = Convert.ToInt32(_yPoint);
                }
                else
                {
                    precise = _chart.ChartAreas[0].AxisY2.Maximum.ToString(new CultureInfo("ru-RU")).Split(',')[1].Length;
                    movePrice = Math.Round(movePrice, precise);
                    _yPoint = Math.Round(_yPoint, precise);
                }

                // икс
                decimal widthInPicsel = _host.Child.Width * Convert.ToDecimal(_chart.ChartAreas[0].InnerPlotPosition.Width / 100);
                decimal widthInInts = Convert.ToDecimal(_chart.ChartAreas[0].AxisX.ScaleView.ViewMaximum - _chart.ChartAreas[0].AxisX.ScaleView.ViewMinimum);

                decimal intsToPicsel = widthInInts / widthInPicsel;

                decimal moveMouseX = e.X - _xMouse;

                // это у нас нужная точка по оси икс
                int moveInInts = Convert.ToInt32(_xPoint + moveMouseX * intsToPicsel);

                // ищем какое это время

                Series candleSeries = FindSeriesByNameSafe("SeriesCandle");

                if (candleSeries == null)
                {
                    return;
                }

                if (_clickElement != null && _clickElement.TypeName() == "LineHorisontal")
                {
                    LineHorisontal elem = (LineHorisontal)_clickElement;
                    if (moveInInts > 0 && moveInInts < candleSeries.Points.Count)
                    {
                        //DateTime time = Convert.ToDateTime(candleSeries.Points[moveInInts].AxisLabel);

                        //elem.TimeStart = time;
                    }

                    elem.SetNewValueFromChart(_yPoint - movePrice);

                    elem.Refresh();
                }

                if (_clickElement != null && _clickElement.TypeName() == "PointElement")
                {
                    PointElement elem = (PointElement)_clickElement;
                    if (moveInInts > 0 && moveInInts < candleSeries.Points.Count)
                    {
                        DateTime time = _myCandles[moveInInts].TimeStart;

                        elem.TimePoint = time;
                    }

                    elem.SetNewValueFromChart(_yPoint - movePrice);

                    elem.Refresh();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// опустилась ЛКМ на чарт
        /// </summary>
        private void _chartForCandle_MouseDown2ChartElement(object sender, MouseEventArgs e) 
        {
            if (_chartElements == null)
            {
                return;
            }
            // надо найти икс и игрик по которому кликнули

            try
            {
                MouseEventArgs mouse = (MouseEventArgs)e;
                if (mouse.Button != MouseButtons.Left)
                {
                    return;
                }

                _isMoving = false;

                if (_host.ActualWidth * 0.9 < e.X)
                {
                    return;
                }

                if (double.IsNaN(_chart.ChartAreas[0].CursorX.Position) ||
                    double.IsNaN(_chart.ChartAreas[0].CursorY.Position))
                {
                    return;
                }



                int x = Convert.ToInt32(_chart.ChartAreas[0].CursorX.Position);
                decimal y = Convert.ToDecimal(_chart.ChartAreas[0].CursorY.Position);

                // теперь проверяем, не нажата ли какая-то из линий

                for (int i = 0; i < _chartElements.Count; i++)
                {
                    Series point = FindSeriesByNameSafe(_chartElements[i].UniqName + "Point");

                    if (point == null)
                    {
                        continue;
                    }

                    int centerX = Convert.ToInt32(point.Points[0].XValue);
                    decimal centerY = Convert.ToDecimal(point.Points[0].YValues[0]);


                    decimal widthInPicselY = _host.Child.Width * Convert.ToDecimal(_chart.ChartAreas[0].InnerPlotPosition.Height / 100);
                    decimal widthInIntsY = Convert.ToDecimal(_chart.ChartAreas[0].AxisY2.ScaleView.ViewMaximum - _chart.ChartAreas[0].AxisY2.ScaleView.ViewMinimum);

                    decimal intsToPicselY = widthInIntsY / widthInPicselY;

                    decimal yUp = centerY +  intsToPicselY * 8;
                    decimal yDown = centerY - intsToPicselY * 8;


                    decimal widthInPicselX = _host.Child.Width * Convert.ToDecimal(_chart.ChartAreas[0].InnerPlotPosition.Width / 100);
                    decimal widthInIntsX = Convert.ToDecimal(_chart.ChartAreas[0].AxisX.ScaleView.ViewMaximum - _chart.ChartAreas[0].AxisX.ScaleView.ViewMinimum);

                    decimal intsToPicselX = widthInIntsX / widthInPicselX;

                    decimal xUp = centerX + intsToPicselX * 6;
                    decimal xDown = centerX - intsToPicselX * 6;

                    if (x > xDown &&
                        x < xUp &&
                        y > yDown &&
                        y < yUp)
                    {
                        _yPoint = y;
                        _xPoint = x;
                        _yMouse = e.Y;
                        _xMouse = e.X;
                        _chart.Cursor = Cursors.SizeAll;
                        _clickElement = _chartElements[i];

                        break;
                    }
                }         
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// поднялась ЛКМ с чарта
        /// </summary>
        void _chartForCandle_MouseUp2ChartElement(object sender, MouseEventArgs e)
        {
            try
            {
                if (_clickElement == null)
                {
                    return;
                }

                if (_clickElement.TypeName() == "LineElement")
                {
                    LineHorisontal line = (LineHorisontal) _clickElement;
                    if (_isMoving == false)
                    {
                        line.ShowDialog(e.X + WindowCoordinate.X + 280, e.Y + WindowCoordinate.Y + 70);
                    }
                }

                if (_clickElement.TypeName() == "PointElement")
                {
                    PointElement line = (PointElement)_clickElement;
                    if (_isMoving == false)
                    {
                            line.ShowDialog();
                        
                    }
                }

                _chart.Cursor = Cursors.Default;
                _clickElement = null;

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

// АЛЕРТЫ

        public void ClearAlerts(List<IIAlert> alertArray)
        {
            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action<List<IIAlert>>(ClearAlerts), alertArray);
                return;
            }

            try
            {
                for (int i = 0; i < _chart.Series.Count; i++)
                {
                    if (_chart.Series[i].Name.Split('_').Length == 2 && _chart.Series[i].Name.Split('_')[0] == "Alert")
                    {
                        _chart.Series.Remove(_chart.Series[i]);
                        i--;
                    }
                }

            }
            catch (Exception error)
            {
                if (LogMessageEvent != null)
                {
                    LogMessageEvent(error.ToString(), LogMessageType.Error);
                }
            }
        }

        public void PaintAlert(AlertToChart alert)
        {
            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action<AlertToChart>(PaintAlert), alert);
                return;
            }

            PaintAlert(alert.Lines, alert.ColorLine, alert.BorderWidth, alert.ColorLabel, alert.Type, alert.Label,alert.Name);
        }

        /// <summary>
        /// прорисовать линии алерта на чарте
        /// </summary>
        private void PaintAlert(ChartAlertLine[] lines, Color colorLine, int borderWidth, Color colorLabel, ChartAlertType type, string label,string alertName)
        {
            Series seriesCanlde = _chart.Series.FindByName("SeriesCandle");

            if (seriesCanlde == null || seriesCanlde.Points == null || seriesCanlde.Points.Count < 20)
            {
                return;
            }

            if (_myCandles == null || _myCandles.Count < 10)
            {
                return;
            }

            for (int i = 0; lines != null && i < lines.Length; i++)
            {
                if (lines[i] == null)
                {
                    continue;
                }
                Series alertSeries = new Series("Alert_" + alertName + i);
                alertSeries.ChartArea = "Prime";

                if (type == ChartAlertType.FibonacciChannel && lines.Length != 1)
                {
                    if (i == 0)
                    {
                        alertSeries.Label = "0";
                    }
                    if (i == 1)
                    {
                        alertSeries.Label = "23 %";
                    }
                    if (i == 2)
                    {
                        alertSeries.Label = "38 %";
                    }
                    if (i == 3)
                    {
                        alertSeries.Label = "50 %";
                    }
                    if (i == 4)
                    {
                        alertSeries.Label = "61 %";
                    }
                    if (i == 5)
                    {
                        alertSeries.Label = "76 %";
                    }
                    if (i == 6)
                    {
                        alertSeries.Label = "100 %";
                    }
                    if (i == 7)
                    {
                        alertSeries.Label = "161 %";
                    }
                    if (i == 8)
                    {
                        alertSeries.Label = "261 %";
                    }
                    if (i == 9)
                    {
                        alertSeries.Label = "423 %";
                    }

                }
                if (_chart.Series.FindByName(alertSeries.Name) == null)
                {
                    _chart.Series.Add(alertSeries);
                }

                PaintOneLine(alertSeries, _myCandles, lines[i],colorLine, borderWidth, colorLabel, label);
            }
        }

        /// <summary>
        /// прорисовать линию на серии
        /// </summary>
        public void PaintOneLine(Series mySeries, List<Candle> candles, ChartAlertLine line, Color colorLine, int borderWidth, Color colorLabel, string label)
        {
            // 1 устанавливаем на серию настройки

            mySeries.Color = colorLine;
            mySeries.ChartType = SeriesChartType.Line;
            mySeries.YAxisType = AxisType.Secondary;
            mySeries.BorderWidth = borderWidth;

            mySeries.LabelForeColor = colorLabel;
            mySeries.Label += " " + label;

            // 2 ищем индекс наших точек

            int firstPointIndex = -1;
            int secondPointIndex = -1;
            for (int i = 0; i < candles.Count; i++)
            {
                if (candles[i].TimeStart == line.TimeFirstPoint)
                {
                    firstPointIndex = i;
                }
                if (candles[i].TimeStart == line.TimeSecondPoint)
                {
                    secondPointIndex = i;
                }
            }
            if (firstPointIndex == -1 ||
                secondPointIndex == -1)
            {
                return;
            }
            // 3 находим, какое движение проходит наша линия за свечку

            decimal stepCorner; // сколько наша линия проходит за свечку
            //stepCorner = (_secondPoint - _firstPoint) / (_numberCandleSecond - _numberCandleFirst);

            stepCorner = (line.ValueSecondPoint - line.ValueFirstPoint) / (secondPointIndex - firstPointIndex);

            int x1 = 0;
            int x2 = candles.Count - 1;

            decimal valueFirst = (firstPointIndex * -stepCorner) + line.ValueFirstPoint;

            decimal valieSecond = ((candles.Count - firstPointIndex) * stepCorner) + line.ValueFirstPoint;

            mySeries.Points.AddXY(x1, valueFirst);
            mySeries.Points.AddXY(x2, valieSecond);
        }

// ИНДИКАТОРЫ

        /// <summary>
        /// прорисовать индикатор на графике
        /// </summary>
        /// <param name="indicator">индикатор</param>
        private void PaintIndicator(IIndicatorCandle indicator)
        {
            if (_mouseDown == true)
            {
                return;
            }
            try
            {
                if (string.IsNullOrWhiteSpace(indicator.NameSeries))
                {
                    return;
                }

                if (_chart.InvokeRequired)
                {
                    _chart.Invoke(new Action<IIndicatorCandle>(PaintIndicator), indicator);
                    return;
                }

                if (indicator.PaintOn == false)
                {
                    List<List<decimal>> values = indicator.ValuesToChart;

                    for (int i = 0; i < values.Count; i++)
                    {
                        Series mySeries = FindSeriesByNameSafe(indicator.Name + i);

                        if (mySeries != null && mySeries.Points.Count != 0)
                        {
                            ClearIndicatorSeries(mySeries);
                        }
                    }

                    return;
                }

                if (indicator.TypeIndicator == IndicatorOneCandleChartType.Line)
                {
                    List<List<decimal>> valList = indicator.ValuesToChart;
                    List<Color> colors = indicator.Colors;
                    string name = indicator.Name;

                    for (int i = 0; i < valList.Count; i++)
                    {
                        PaintLikeLine(valList[i], colors[i], name + i);
                    }
                }
                if (indicator.TypeIndicator == IndicatorOneCandleChartType.Column)
                {
                    List<List<decimal>> valList = indicator.ValuesToChart;
                    List<Color> colors = indicator.Colors;
                    string name = indicator.Name;

                    PaintLikeColumn(valList[0], colors[0], colors[1], name + 0);
                }
                if (indicator.TypeIndicator == IndicatorOneCandleChartType.Point)
                {
                    List<List<decimal>> valList = indicator.ValuesToChart;
                    List<Color> colors = indicator.Colors;
                    string name = indicator.Name;

                    for (int i = 0; i < valList.Count; i++)
                    {
                        PaintLikePoint(valList[i], colors[i], name + i);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// добавить индикатор в прорисовку
        /// </summary>
        /// <param name="indicator">индикатор</param>
        public void ProcessIndicator(IIndicatorCandle indicator)
        {
            if ((_startProgram == StartProgram.IsTester
                || _startProgram == StartProgram.IsOsMiner ||
                IsPatternChart) 
            &&
                _host != null)
            {
                PaintIndicator(indicator);
                ReloadAreaSizes();
                ResizeYAxisOnArea("Prime");
            }
            else
            {
                _indicatorsToPaint.Enqueue(indicator);
            }
        }

        /// <summary>
        /// принудительно перерисоват индикатор на графике от начала до конца
        /// </summary>
        /// <param name="indicatorCandle">индикатор</param>
        public void RePaintIndicator(IIndicatorCandle indicatorCandle)
        {
            if (_mouseDown == true)
            {
                return;
            }
            try
            {
                if (_myCandles == null)
                {
                    return;
                }

                for (int i = 0; i < 10; i++)
                {
                    Series mySeries = FindSeriesByNameSafe(indicatorCandle.Name + i);
                    if (mySeries != null)
                    {
                        mySeries.Points.Clear();
                    }
                }

                ProcessIndicator(indicatorCandle);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// очистить серию данных
        /// </summary>
        /// <param name="series">имя серии</param>
        private void ClearIndicatorSeries(Series series)
        {
            try
            {
                if (_chart.InvokeRequired)
                {
                    _chart.Invoke(new Action<Series>(ClearIndicatorSeries), series);
                    return;
                }
                series.Points.Clear();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

// Индикатор точки

        /// <summary>
        /// прорисовать индикатор как точки
        /// </summary>
        private void PaintLikePoint(List<decimal> values, Color color, string nameSeries)
        {
            if (values == null ||
                values.Count == 0)
            {
                return;
            }
            Series mySeries = FindSeriesByNameSafe(nameSeries);

            if (mySeries == null)
            {
                mySeries = FindSeriesByNameSafe(nameSeries);
            }

            if (mySeries == null)
            {
                return;
            }

            ChartArea myArea = FindAreaByNameSafe(mySeries.ChartArea);

            if (myArea == null)
            {
                return;
            }


            if (mySeries.Points.Count != 0 &&
               values.Count - 1 == mySeries.Points.Count)
            {// если прорисовываем только последнюю точку
                PaintLikePointLast(values, nameSeries);
            }
            else if (mySeries.Points.Count != 0 &&
                values.Count == mySeries.Points.Count)
            {// перерисовываем последнюю точку
                RePaintLikePointLast(values, nameSeries);
            }
            else
            {// если надо полностью перерисовываем весь индикатор

                List<decimal> array = values;

                Series series = new Series(nameSeries);
                series.ChartType = SeriesChartType.Point;
                series.YAxisType = AxisType.Secondary;
                series.ChartArea = myArea.Name;
                series.ShadowOffset = 1;
                series.YValuesPerPoint = 1;
                series.Color = color;

                for (int i = 0; i < array.Count; i++)
                {
                    if (array[i] != 0)
                    {
                        series.Points.AddXY(i, array[i]);
                        series.Points[series.Points.Count - 1].ToolTip = array[i].ToString();
                    }
                }
                ReloadAreaSizes();
                PaintSeriesSafe(series);
            }
            ResizeYAxisOnArea(myArea.Name);
        }

        /// <summary>
        ///  прорисовать последние точки
        /// </summary>
        private void PaintLikePointLast(List<decimal> values, string nameSeries)
        {
            Series mySeries = FindSeriesByNameSafe(nameSeries);

            decimal lastPoint = values[values.Count - 1];
            mySeries.Points.AddXY(values.Count - 1, lastPoint);
            mySeries.Points[mySeries.Points.Count - 1].ToolTip = lastPoint.ToString();
        }

        /// <summary>
        ///  перерисовать последнюю точку
        /// </summary>
        private void RePaintLikePointLast(List<decimal> values, string nameSeries)
        {
            Series mySeries = FindSeriesByNameSafe(nameSeries);
            decimal point2 = Convert.ToDecimal(values[values.Count - 2]);
            mySeries.Points[mySeries.Points.Count - 1].YValues = new[] { Convert.ToDouble(point2) };
            mySeries.Points[mySeries.Points.Count - 1].ToolTip = point2.ToString();
        }

// Индикатор линия

        /// <summary>
        /// прорисовать индикатор как линию
        /// </summary>
        private void PaintLikeLine(List<decimal> values, Color color, string nameSeries)
        {
            if(values == null ||
                values.Count == 0)
            {
                return;
            }

            Series mySeries = FindSeriesByNameSafe(nameSeries);

            if (mySeries == null)
            {
                return;
            }

            ChartArea myArea = FindAreaByNameSafe(mySeries.ChartArea);

            if (myArea == null)
            {
                return;
            }

            if(mySeries.Points.Count != 0 &&
                values.Count - 1 == mySeries.Points.Count)
            {// если прорисовываем только последнюю точку
                PaintLikeLineLast(values, nameSeries);
            }
            else if (mySeries.Points.Count != 0 &&
                values.Count == mySeries.Points.Count)
            {// перерисовываем последнюю точку
                RePaintLikeLineLast(values, nameSeries);
            }
            else
            { // если надо полностью перерисовываем весь индикатор

                List<decimal> array = values;

                if (array == null)
                {
                    return;
                }

                Series series = new Series(nameSeries);
                series.ChartType = SeriesChartType.Line;
                series.YAxisType = AxisType.Secondary;
                series.ChartArea = myArea.Name;
                series.ShadowOffset = 1;
                series.YValuesPerPoint = 1;
                series.Color = color;

                for (int i = 0; i < array.Count; i++)
                {
                    if (array[i] != 0)
                    {
                        series.Points.AddXY(i, array[i]);
                        series.Points[series.Points.Count - 1].ToolTip = array[i].ToString();
                    }
                    else
                    {
                        series.Points.AddXY(i, 0);
                    }
                }

                PaintSeriesSafe(series);
                ReloadAreaSizes();
            }
            ResizeYAxisOnArea(myArea.Name);
            
        }

        /// <summary>
        /// прорисовать индикатор как линию, последний элемент
        /// </summary>
        private void PaintLikeLineLast(List<decimal> values, string nameSeries)
        {
            Series mySeries = FindSeriesByNameSafe(nameSeries);

            decimal lastPoint = values[values.Count - 1];
            mySeries.Points.AddXY(mySeries.Points.Count, lastPoint);
            mySeries.Points[mySeries.Points.Count - 1].ToolTip = lastPoint.ToString();

            RePaintRightLebels();
        }

        /// <summary>
        /// перерисовать индикатор как линию, последний элемент
        /// </summary>
        private void RePaintLikeLineLast(List<decimal> values, string nameSeries)
        {
            Series mySeries = FindSeriesByNameSafe(nameSeries);

            decimal lastPoint = Convert.ToDecimal(values[values.Count - 1]);
            mySeries.Points[mySeries.Points.Count - 1].YValues = new [] { Convert.ToDouble(lastPoint) };
            mySeries.Points[mySeries.Points.Count - 1].ToolTip = lastPoint.ToString();

            RePaintRightLebels();
        }

// Индикатор столбец

        /// <summary>
        /// прорисовать как столбцы
        /// </summary>
        private void PaintLikeColumn(List<decimal> values, Color colorUp, Color colorDown, string nameSeries)
        {
            if (values == null ||
                values.Count == 0)
            {
                return;
            }
            Series mySeries = FindSeriesByNameSafe(nameSeries);

            if (mySeries == null ||
                values == null)
            {
                return;
            }

            ChartArea myArea = FindAreaByNameSafe(mySeries.ChartArea);

            if (myArea == null)
            {
                return;
            }

            if (mySeries.Points.Count != 0 &&
                values.Count - 1 == mySeries.Points.Count)
            {// если прорисовываем только последнюю точку
                PaintLikeColumnLast(values,nameSeries,colorUp,colorDown);
            }
            else if (mySeries.Points.Count != 0 &&
                values.Count == mySeries.Points.Count)
            {// перерисовываем последнюю точку
                RePaintLikeColumnLast(values, nameSeries, colorUp, colorDown);
            }
            else
            { // если надо полностью перерисовываем весь индикатор

                List<decimal> array = values;

                Series series = new Series(nameSeries);
                series.ChartType = SeriesChartType.Column;
                series.YAxisType = AxisType.Secondary;
                series.ChartArea = myArea.Name;
                series.ShadowOffset = 1;
                series.YValuesPerPoint = 1;
                series.Color = colorUp;

                for (int i = 0; i < array.Count; i++)
                {
                    if (array[i] != 0)
                    {
                        series.Points.AddXY(i, array[i]);
                        series.Points[series.Points.Count - 1].ToolTip = array[i].ToString();
                    }
                    else
                    {
                        series.Points.AddXY(i, 0);
                    }

                    if (i > 1)
                    {
                        if (array[i] > array[i - 1])
                        {
                            if (colorUp != Color.FromArgb(0, 0, 0, 0))
                            {
                                series.Points[i].Color = colorUp;
                            }

                        }
                        else
                        {
                            if (colorDown != Color.FromArgb(0, 0, 0, 0))
                            {
                                series.Points[i].Color = colorDown;
                            }
                        }
                    }
                }

                PaintSeriesSafe(series);
                ReloadAreaSizes();
            }
            ResizeYAxisOnArea(myArea.Name);
        }
        
        /// <summary>
        /// прорисовать индикатор как две линии, последний элемент
        /// </summary>
        private void PaintLikeColumnLast(List<decimal> values, string nameSeries, Color colorUp, Color colorDown)
        {

            Series mySeriesUp = FindSeriesByNameSafe(nameSeries);
            decimal lastPointUp = values[values.Count - 1];
            mySeriesUp.Points.AddXY(mySeriesUp.Points.Count, lastPointUp);
            mySeriesUp.Points[mySeriesUp.Points.Count - 1].ToolTip = lastPointUp.ToString();

            if (values[values.Count - 1] > values[values.Count - 2])
            {
                if (colorUp != Color.FromArgb(0, 0, 0, 0))
                {
                    mySeriesUp.Points[mySeriesUp.Points.Count - 1].Color = colorUp;
                }
            }
            else
            {
                if (colorDown != Color.FromArgb(0, 0, 0, 0))
                {
                    mySeriesUp.Points[mySeriesUp.Points.Count - 1].Color = colorDown;
                }
            }
        }

        /// <summary>
        /// прорисовать индикатор как две линии, последний элемент
        /// </summary>
        private void RePaintLikeColumnLast(List<decimal> values, string nameSeries, Color colorUp, Color colorDown)
        {
            Series mySeriesUp = FindSeriesByNameSafe(nameSeries);
            decimal lastPoint = Convert.ToDecimal(values[values.Count - 1]);
            mySeriesUp.Points[mySeriesUp.Points.Count - 1].YValues = new[] { Convert.ToDouble(lastPoint) };
            mySeriesUp.Points[mySeriesUp.Points.Count - 1].ToolTip = lastPoint.ToString();

            if (values.Count != 1)
            {
                if (values[values.Count - 1] > values[values.Count - 2])
                {
                    if (colorUp != Color.FromArgb(0, 0, 0, 0))
                    {
                        mySeriesUp.Points[mySeriesUp.Points.Count - 1].Color = colorUp;
                    }
                }
                else
                {
                    if (colorDown != Color.FromArgb(0, 0, 0, 0))
                    {
                        mySeriesUp.Points[mySeriesUp.Points.Count - 1].Color = colorDown;
                    }
                }
            }
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
                    Thread.Sleep(100);
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
                    Thread.Sleep(100);
                    myArea = _chart.ChartAreas.FindByName(name);
                }
                catch (Exception)
                {
                    return null;
                }
            }

            return myArea;
        }

// Паттерны

        public bool IsPatternChart;

        public event Action<int> ClickToIndexEvent;

        public void PaintSingleCandlePattern(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }
            _myCandles = candles;
            PaintAllCandles(candles);
            ResizeYAxisOnArea("Prime");
        }

        public void PaintSingleVolumePattern(List<Candle> candles)
        {
            if (_chart != null && _chart.InvokeRequired)
            {
                _chart.Invoke(new Action<List<Candle>>(PaintSingleVolumePattern), candles);
                return;
            }
            if (candles == null)
            {
                return;
            }
            _myCandles = candles;
            Volume indicator = new Volume(false);
            indicator.NameArea = "Prime";
            indicator.NameSeries = "VolumePattern";
            indicator.Values = new List<decimal>();
            indicator.Name = "VolumePattern";

            for (int i = 0; i < candles.Count; i++)
            {
                indicator.Values.Add(candles[i].Volume);
            }

            if (IndicatorIsCreate(indicator.Name + "0") == false)
            {
                CreateSeries(GetChartArea(indicator.NameArea), indicator.TypeIndicator, indicator.NameSeries + "0");
            }

            PaintIndicator(indicator);

            ResizeYAxisOnArea("Prime");
        }

        public void PaintInDifColor(int indexStart, int indexEnd, string seriesName)
        {
            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action<int, int, string> (PaintInDifColor), indexStart, indexEnd, seriesName);
                return;
            }
            if (indexStart < 0 || indexEnd < 0)
            {
                return;
            }

            Series paintSeries = null;

            if (seriesName == "SeriesCandle")
            {
                paintSeries = FindSeriesByNameSafe("SeriesCandle");
            }
            else
            {
                for (int i = 0; i < _chart.Series.Count; i++)
                {
                    int indexOf = _chart.Series[i].Name.IndexOf(seriesName);
                    if (indexOf!= -1)
                    {
                        paintSeries = _chart.Series[i];
                        break;
                    }
                }
            }

            if (paintSeries == null)
            {
                return;
            }

            Color pointColor;

            if (_colorKeeper.ColorScheme == ChartColorScheme.White)
            {
                pointColor = Color.DodgerBlue;
            }
            else //if (_colorKeeper.ColorScheme == ChartColorScheme.Black)
            {
                pointColor = Color.WhiteSmoke;
            }

            for (int i = indexStart; i < indexEnd && i < paintSeries.Points.Count; i++)
            {
                paintSeries.Points[i].Color = pointColor;
                paintSeries.Points[i].BackSecondaryColor = pointColor;
                paintSeries.Points[i].BorderColor = pointColor;
            }
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

// переход по чарту

        /// <summary>
        /// переместить чарт к заданному времени
        /// </summary>
        /// <param name="time">время</param>
        public void GoChartToTime(DateTime time)
        {
            try
            {
                if (_myCandles == null ||
                _myCandles.Count < 50)
                {
                    return;
                }

                int candleIndex = GetTimeIndex(time);

                if (candleIndex == 0)
                {
                    return;
                }

                _chart.ChartAreas[0].AxisX.ScaleView.Size = 50;
                _chart.ChartAreas[0].AxisX.ScaleView.Position = candleIndex;

                ResizeYAxisOnArea("Prime");
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// переместить чарт к заданному времени
        /// </summary>
        public void GoChartToIndex(int index)
        {
            try
            {
                if (_myCandles == null ||
                _myCandles.Count < 50)
                {
                    return;
                }

                int candleIndex = index;

                if (candleIndex == 0)
                {
                    return;
                }

                if (_myCandles.Count > 200)
                {
                    _chart.ChartAreas[0].AxisX.ScaleView.Size = 150;
                }

                while (_chart.ChartAreas[0].AxisX.ScaleView.Position > 0 && 
                    _chart.ChartAreas[0].AxisX.ScaleView.Position > candleIndex)
                {
                    _chart.ChartAreas[0].AxisX.ScaleView.Position--;
                }

                while (_chart.ChartAreas[0].AxisX.ScaleView.Position > 0 && 
                    _chart.ChartAreas[0].AxisX.ScaleView.Position < candleIndex)
                {
                    _chart.ChartAreas[0].AxisX.ScaleView.Position++;
                }

                ResizeYAxisOnArea("Prime");
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

// работа с курсором и выделенем свечек на чарте

        void _chart_Click(object sender, EventArgs e)
        {
            try
            {
                if (_chart.Cursor == Cursors.SizeAll)
                {
                    return;
                }
                if (IsPatternChart)
                {
                    return;
                }
                if (_myCandles == null ||
                    _myCandles.Count == 0)
                {
                    return;
                }

                RePaintCursor();

                int index = (int)_chart.ChartAreas[0].CursorX.Position;

                if (index < 0 || (index >= _myCandles.Count && index != 1))
                {
                    return;
                }
                if(index == 1 && _myCandles.Count == 1)
                {
                    index = 0;
                }

                if (ClickToIndexEvent != null)
                {
                    ClickToIndexEvent(index);
                }

                List<Series> series = new List<Series>();

                for (int i = 0; i < _chart.Series.Count; i++)
                {
                    if (_chart.Series[i].Name != "Deal_BuySell" &&
                       _chart.Series[i].Name.Split('_')[0] != "Close" &&
                        _chart.Series[i].Name.Split('_')[0] != "Open" &&
                        _chart.Series[i].Name.Split('_')[0] != "Stop" &&
                        _chart.Series[i].Name.Split('_')[0] != "Profit")
                    {
                        series.Add(_chart.Series[i]);
                    }
                }


                if (_labelSeries == null)
                {
                    _labelSeries = new List<Series>();
                }

                for (int i = 0; i < series.Count; i++)
                {
                    if (index >= series[i].Points.Count ||
                        series[i].Name.EndsWith("label"))
                    {
                        continue;
                    }
                    Series labelSeries = _labelSeries.Find(ser => ser.Name == series[i].Name + "label"
                                                                  ||
                                                                  (ser.Name.EndsWith("label") && ser.Name == series[i].Name));

                    if (labelSeries == null)
                    {
                        labelSeries = new Series(series[i].Name + "label");
                        _labelSeries.Add(labelSeries);
                        labelSeries.ChartArea = series[i].ChartArea;
                        labelSeries.MarkerStyle = MarkerStyle.Diamond;
                        labelSeries.MarkerSize = 10;
                        labelSeries.ChartType = SeriesChartType.Point;
                        labelSeries.XAxisType = AxisType.Primary;
                        labelSeries.YAxisType = AxisType.Secondary;

                        _chart.Series.Add(labelSeries);

                    }

                    labelSeries.Color = series[i].Points[index].Color;

                    if (labelSeries.Points.Count == 0)
                    {
                        labelSeries.Points.Add(new DataPoint(0, 30 - i));
                    }

                    labelSeries.Points[0].Color = series[i].Points[index].Color;
                    labelSeries.Points[0].LabelForeColor = series[i].Points[index].Color;
                    labelSeries.Points[0].LabelAngle = 0;


                    if (series[i].Name == "SeriesCandle")
                    {
                        labelSeries.Points[0].Label = _myCandles[index].ToolTip;
                    }
                    else
                    {
                        labelSeries.Points[0].Label = series[i].Points[index].YValues[0].ToString();
                    }
                }

                ResizeSeriesLabels();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(),LogMessageType.Error);
            }
           
        }

        private List<Series> _labelSeries; 

        private void ResizeSeriesLabels()
        {
            try
            {
                if (_labelSeries == null ||
                _labelSeries.Count == 0)
                {
                    return;
                }

                int x = 0;

                if (_chart.ChartAreas[0].AxisX.ScaleView.IsZoomed)
                {
                    x = (int)_chart.ChartAreas[0].AxisX.ScaleView.Position;
                }

                if (_myCandles == null || 
                    x < 0 ||
                    x > _myCandles.Count)
                {
                    return;
                }

                for (int i = 0; i < _chart.ChartAreas.Count; i++)
                {
                    ChartArea area = _chart.ChartAreas[i];

                    List<Series> seriesFromMyChart = new List<Series>();

                    for (int i2 = 0; i2 < _labelSeries.Count; i2++)
                    {
                        if (_labelSeries[i2].ChartArea == area.Name)
                        {
                            seriesFromMyChart.Add(_labelSeries[i2]);
                        }
                    }

                    if (seriesFromMyChart.Count == 0)
                    {
                        continue;
                    }


                    double iterator = (area.AxisY2.Maximum - area.AxisY2.Minimum) / 15;

                    double height = area.AxisY2.Maximum - iterator;


                    for (int i2 = 0; i2 < seriesFromMyChart.Count; i2++)
                    {
                        if (seriesFromMyChart[i2].Points.Count == 0)
                        {
                            continue;
                        }
                        seriesFromMyChart[i2].Points[0].SetValueXY(x + 1, height);
                        height = height - iterator;
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.Message, LogMessageType.Error);
            }
        }

        /// <summary>
        /// прорисовывает на оси Y значение курсора
        /// </summary>
        private void RePaintCursor()
        {
            if(_myCandles == null)
            {
                return;
            }
            // ищем, на какой области сейчас курсор

            ChartArea area = null;

            for (int i = 0; i < _chart.ChartAreas.Count; i++)
            {
                if ( !double.IsNaN(_chart.ChartAreas[i].CursorY.Position))
                {
                    area = _chart.ChartAreas[i];
                    break;
                }
            }

            if (area == null)
            {
                return;
            }

            if (_areaSizes == null)
            {
                return;
            }

            ChartAreaSizes areaSize = _areaSizes.Find(size => size.Name == area.Name);

            if (areaSize == null)
            {
                return;
            }

            decimal value = Convert.ToDecimal(Math.Round(area.CursorY.Position, areaSize.Decimals));

            PaintLabelOnY2("Cursor", "Prime", value.ToString(_culture), value, _colorKeeper.ColorBackCursor, true);

            RePaintPrimeLines("Prime");
            CheckOverlap("Prime");
        }

// прорисовка горизонтальных линий на графике

        /// <summary>
        /// прорисовать надпись на оси Y2
        /// </summary>
        /// <param name="nameArea">название области</param>
        /// <param name="label">подпись</param>
        /// <param name="positionOnY">позиция линии</param>
        /// <param name="color">цвет подписи</param>
        /// <param name="isPrime">является ли подпись подписью перекрвающий другие</param>
        private void PaintLabelOnY2(string name, string nameArea, string label, decimal positionOnY, Color color, bool isPrime)
        {
            if (_myCandles == null)
            {
                return;
            }
            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action<string, string, string, decimal, Color, bool>(PaintLabelOnY2), name, nameArea, label, positionOnY, color, isPrime);
                return;
            }


            ChartArea area = GetChartArea(nameArea);

            if (area == null)
            {
                return;
            }

            double min = Convert.ToDouble(positionOnY);

            double max = Convert.ToDouble(positionOnY + Convert.ToDecimal(area.AxisY2.Interval) / 20);

            label = Convert.ToDecimal(Convert.ToDouble(label)).ToString();

            if (_labels == null)
            {
                _labels = new List<ChartYLabels>();
            }
            if (area.AxisY2.CustomLabels != null)
            { // удаляем лэйбл если он уже был на графике
                ChartYLabels oldlabel = _labels.Find(labels => labels.AreaName == nameArea && labels.SeriesName == name);

                if (oldlabel != null)
                {// нашли старую серию
                    string positon = oldlabel.Price;
                    for (int i = 0; i < area.AxisY2.CustomLabels.Count; i++)
                    {
                        if (area.AxisY2.CustomLabels[i].Text == positon)
                        {
                            area.AxisY2.CustomLabels[i].FromPosition = min;
                            area.AxisY2.CustomLabels[i].ToPosition = max;
                            area.AxisY2.CustomLabels[i].Text = label;
                            area.AxisY2.CustomLabels[i].ForeColor = color;
                            area.AxisY2.CustomLabels[i].MarkColor = color;
                            oldlabel.Price = label;
                            oldlabel.Color = color;
                            return;
                        }
                    }
                    oldlabel.Price = label;
                }
                else
                {
                    _labels.Add(new ChartYLabels{ AreaName = nameArea,
                        Price = label,SeriesName = name, IsPrime = isPrime,Color = color});
                }
            }

            CustomLabel labelNew = new CustomLabel(min, max, label, 0, LabelMarkStyle.LineSideMark);
            labelNew.ForeColor = color;
            labelNew.RowIndex = 0;
            labelNew.Name = name;
            labelNew.GridTicks = GridTickTypes.None;
            labelNew.MarkColor = color;

            area.AxisY2.CustomLabels.Add(labelNew);
        }

        private void ClearLabelOnY2(string name, string nameArea, Color color)
        {
            if (_myCandles == null)
            {
                return;
            }
            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action<string, string, Color>(ClearLabelOnY2), name, nameArea,color);
                return;
            }

            ChartArea area = GetChartArea(nameArea);

            if (area == null)
            {
                return;
            }

            if (_labels == null)
            {
                return;
            }

            if (area.AxisY2.CustomLabels == null)
            {
                return;
            }

            ChartYLabels oldlabel = _labels.Find(labels => labels.AreaName == nameArea && labels.SeriesName == name);

            if (oldlabel == null)
            {
                return;
            }

            _labels.Remove(oldlabel);

            string positon = Convert.ToDecimal(oldlabel.Price).ToString(_culture);
            for (int i = 0; i < area.AxisY2.CustomLabels.Count; i++)
            {
                if (area.AxisY2.CustomLabels[i].Text == positon && area.AxisY2.CustomLabels[i].ForeColor == color)
                {
                    area.AxisY2.CustomLabels.Remove(area.AxisY2.CustomLabels[i]);
                    return;
                }
            }
        }

        /// <summary>
        /// все подписи на графике
        /// </summary>
        private List<ChartYLabels> _labels;

        /// <summary>
        /// подписать стандартные горизонтальные линии на области
        /// </summary>
        private void RePaintPrimeLines(string areaName)
        {
            if (IsPatternChart)
            {
                return;
            }
            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action<string>(RePaintPrimeLines), areaName);
                return;
            }

            ChartArea area = GetChartArea(areaName);

            if(area == null)
            {
                return;
            }

            if(area.AxisY2.Interval == 0)
            {
                return;
            }

            ChartAreaSizes size = _areaSizes.Find(areaSize => areaSize.Name == areaName);

            if (size == null)
            {
                return;
            }

            double price = area.AxisY2.Minimum;

            if (areaName != "Prime")
            {
                price += area.AxisY2.Interval;
            }
            int name = 0;

            while (true)
            {

                if (areaName == "Prime" &&
                    price + area.AxisY2.Interval/2>= area.AxisY2.Maximum)
                {
                    break;
                }
                double lastPrice = price;

                if (price + area.AxisY2.Interval / 2 >= area.AxisY2.Maximum)
                {
                    PaintLabelOnY2(name.ToString(), areaName, area.AxisY2.Maximum.ToString(_culture),
                        Convert.ToDecimal(area.AxisY2.Maximum - area.AxisY2.Interval / 20), _colorKeeper.ColorText,
                        false);

                    ClearLabelOnY2((name + 1).ToString(), areaName, _colorKeeper.ColorText);
                    ClearLabelOnY2((name + 2).ToString(), areaName, _colorKeeper.ColorText);
                    break;
                }
                PaintLabelOnY2(name.ToString(), areaName, Math.Round(price, size.Decimals).ToString(_culture), Convert.ToDecimal(price),
                _colorKeeper.ColorText, false);

                price += area.AxisY2.Interval;

                if(lastPrice == Math.Round(price, size.Decimals))
                {
                    //PaintLabelOnY2(name.ToString(), areaName, Math.Round(price, size.Decimals+1).ToString(_culture), Convert.ToDecimal(price),_colorKeeper.ColorText, false);
                    ClearLabelOnY2((name).ToString(), areaName, _colorKeeper.ColorText);
                }

                name++;
                
            }

            CheckOverlap(areaName);
        }

        /// <summary>
        /// проверить перехлёст лэйблов на оси Y. И спрятать линии если нужно
        /// </summary>
        private void CheckOverlap(string nameArea)
        {
            if (_labels == null ||
                _labels.Count < 2)
            {
                return;
            }

            ChartArea area = GetChartArea(nameArea);

            for (int indLabel = 0; indLabel < _labels.Count; indLabel++)
            {// ищем лэйблы с пометкой IsPrime. Под ними надо всё расчистить
                if (_labels[indLabel].IsPrime == false)
                {
                    continue;
                }

                ChartYLabels label = _labels[indLabel];

                if (label.AreaName != nameArea)
                {
                    continue;
                }

                double max = Convert.ToDouble(label.Price) + area.AxisY2.Interval * 0.2;
                double min = Convert.ToDouble(label.Price) - area.AxisY2.Interval * 0.2;

                for (int indSecond = 0; indSecond < _labels.Count; indSecond++)
                {
                    if (indLabel == indSecond)
                    {
                        continue;
                    }

                    if (_labels[indSecond].IsPrime)
                    { // если два прайма друг на дружке, то оставляем
                        continue;
                    }


                    if (_labels[indSecond].AreaName != label.AreaName)
                    {
                        continue;
                    }

                    double price = Convert.ToDouble(_labels[indSecond].Price);
                    if (price < max &&
                        price > min)
                    { // нашли пересечение
                        ClearLabelOnY2(_labels[indSecond].SeriesName, nameArea, _labels[indSecond].Color);
                        indSecond--;
                    }
                }
            }
        }

        /// <summary>
        /// перерисовать цифры отображения индикаторов на правой оси
        /// </summary>
        private void RePaintRightLebels()
        {
            if (_chart.ChartAreas.Count == 0 ||
                _chart.Series.Count == 0)
            {
                return;
            }

            int index = 0;

            if (_chart.ChartAreas[0].AxisX.ScrollBar.IsVisible == false)
            {
                for (int i = 0; i < _chart.Series.Count;i++)
                {
                    if (_chart.Series[i].Points.Count > index)
                    {
                        index = _chart.Series[i].Points.Count-1;
                    }
                }
            }
            else
            {
                index = (int)_chart.ChartAreas[0].AxisX.ScaleView.Position + (int)_chart.ChartAreas[0].AxisX.ScaleView.Size;
            }

            if(index < 0)
            {
                return;
            }

            for (int i = 0; i < _chart.ChartAreas.Count;i++)
            {
                ChartArea area = _chart.ChartAreas[i];

                List<Series> mySeries = new List<Series>();

                for (int i2 = 0; i2 < _chart.Series.Count; i2++)
                {
                    if (_chart.Series[i2].ChartArea == area.Name)
                    {
                        mySeries.Add(_chart.Series[i2]);
                    }
                }

                if (mySeries.Count == 0)
                {
                    continue;
                }

                for (int i2 = 0; i2 < mySeries.Count; i2++)
                {
                    Series series = mySeries[i2];

                    if (series.Points.Count == 0 ||
                        series.Points.Count < index ||
                        series.ChartType == SeriesChartType.Point
                        //|| series.Points.Count +2 < _myCandles.Count
                        )
                    {
                        continue;
                    }

                   /* if (series.ChartType == SeriesChartType.Candlestick)
                    {
                        PaintLabelOnY2(series.Name + "Label", series.ChartArea,
                           _myCandles[index].Close.ToString(_culture),
                               _myCandles[index].Close, _colorKeeper.ColorBackCursor, true);

                    }
                    else
                    {
                    */
                        int realIndex = index;

                        if (index == series.Points.Count)
                        {
                            realIndex = series.Points.Count - 1;
                        }

                        int rounder = 0;

                        if (_areaSizes != null)
                        {
                            ChartAreaSizes size = _areaSizes.Find(sizes => sizes.Name == _chart.ChartAreas[i].Name);
                            if (size != null)
                            {
                                rounder = size.Decimals;
                            }
                        }

                        PaintLabelOnY2(series.Name + "Label", series.ChartArea,
                              (Math.Round(series.Points[realIndex].YValues[0], rounder)).ToString(_culture),
                                 (decimal)series.Points[realIndex].YValues[0], series.Points[realIndex].Color, true);
                  //  }
                }
            }
        }
        
        void _chart_AxisViewChanging(object sender, ViewEventArgs e)
        {
            RePaintRightLebels();
        }

// управление схлопывание областей

        /// <summary>
        /// спрятать все области кроме главной
        /// </summary>
        public void HideAreaOnChart()
        {
            try
            {
                for (int i = 0; _chart.ChartAreas != null && i < _chart.ChartAreas.Count; i++)
                {
                    if (_chart.ChartAreas[i].Name == "Prime")
                    {
                        _chart.ChartAreas[i].Position.Height = 100;

                    }
                    else
                    {
                        _chart.ChartAreas[i].Position.Height = 0;
                        _chart.ChartAreas[i].Position.Y = 100;
                    }
                }
                ReloadChartAreasSize();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// показать все области
        /// </summary>
        public void ShowAreaOnChart()
        {
            try
            {
                bool tickChartInclude = false;

                for (int i = 0; _chart.ChartAreas != null && i < _chart.ChartAreas.Count; i++)
                {
                    int tickHigh = 0;

                    if (GetChartArea("TradeArea") != null)
                    {
                        tickHigh = 25;
                    }

                    if (_chart.ChartAreas[i].Name == "Prime")
                    {
                        if (tickHigh != 0)
                        {
                            _chart.ChartAreas[i].Position.Height = 100 - (15 * (_chart.ChartAreas.Count - 2) + tickHigh);
                        }
                        else
                        {
                            _chart.ChartAreas[i].Position.Height = 100 - (15 * (_chart.ChartAreas.Count - 1));
                        }

                    }
                    else
                    {
                        if (_chart.ChartAreas[i].Name != "TradeArea")
                        {
                            if (tickChartInclude == false)
                            { // если тиковый график ещё не прошли
                                _chart.ChartAreas[i].Position.Height = 15;
                                _chart.ChartAreas[i].Position.Y = _chart.ChartAreas[0].Position.Y + _chart.ChartAreas[0].Position.Height + (i - 1) * 15;
                            }
                            else
                            { // надо учитывать высоту тикового графика
                                _chart.ChartAreas[i].Position.Height = 15;
                                _chart.ChartAreas[i].Position.Y = _chart.ChartAreas[0].Position.Y + _chart.ChartAreas[0].Position.Height + (i - 2) * 15 + tickHigh;
                            }
                        }
                        else
                        {
                            _chart.ChartAreas[i].Position.Height = tickHigh;
                            _chart.ChartAreas[i].Position.Y = _chart.ChartAreas[0].Position.Y + _chart.ChartAreas[0].Position.Height + (i - 1) * 15;
                            tickChartInclude = true;
                        }
                    }
                }
                ReloadChartAreasSize();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

// управление высотой областей

        private List<ChartAreaPosition> _areaPositions;

        private void _chart_MouseMove(object sender, MouseEventArgs e)
        {
            if (_chart.Cursor == Cursors.SizeAll)
            {
                return;
            }
            if (_chart.ChartAreas.Count < 2)
            {
                return;
            }

            if (e.Button == MouseButtons.Left &&
                _chart.Cursor == Cursors.Arrow)
            {
                return;
            }

            if (_areaPositions.Count != _chart.ChartAreas.Count ||
                _areaPositions.Find(pos => pos.RightPoint == 0) != null)
            {
                _areaPositions = new List<ChartAreaPosition>();
                ReloadChartAreasSize();
            }

            if (_chart.Cursor == Cursors.Hand || _chart.Cursor == Cursors.SizeWE ||
                _areaPositions == null ||
                _areaPositions.Count < 2)
            {
                return;
            }

            ChartAreaPosition myPosition = null;
            ChartAreaPosition positionBeforeUs = null;

            MouseEventArgs mouse = (MouseEventArgs) e;

            for (int i = 1; i < _areaPositions.Count; i++)
            {
                ChartAreaPosition pos = _areaPositions[i];

                if ((mouse.Button == MouseButtons.Left && _chart.Cursor == Cursors.SizeNS &&
                     pos.LeftPoint < e.X &&
                     pos.RightPoint > e.X &&
                     pos.UpPoint + pos.UpPoint*0.09 > e.Y &&
                     pos.UpPoint - pos.UpPoint*0.09 < e.Y) ||

                    (pos.LeftPoint < e.X &&
                     pos.RightPoint > e.X &&
                     pos.UpPoint + pos.UpPoint*0.04 > e.Y &&
                     pos.UpPoint - pos.UpPoint*0.04 < e.Y))
                {
                    positionBeforeUs = _areaPositions[i - 1];
                    myPosition = pos;
                    _chart.Cursor = Cursors.SizeNS;
                }
                else
                {
                    pos.ValueYMouseOnClickStart = 0;
                    pos.HeightAreaOnClick = 0;
                    pos.ValueYChartOnClick = 0;
                }
            }

            if (myPosition == null)
            {
                _chart.Cursor = Cursors.Arrow;
                return;
            }

            if (mouse.Button != MouseButtons.Left)
            {
                myPosition.ValueYMouseOnClickStart = 0;
            }

            // перетаскивание областей
            // Здесь у нас есть нажатая левая кнопка

            if (myPosition.ValueYMouseOnClickStart == 0)
            {
                myPosition.ValueYMouseOnClickStart = e.Y;
                myPosition.HeightAreaOnClick = myPosition.Area.Position.Height;
                myPosition.ValueYChartOnClick = myPosition.Area.Position.Y;
                myPosition.HeightAreaOnClickBeforeUs = positionBeforeUs.Area.Position.Height;

                return;
            }

            double persentMove = Math.Abs(myPosition.ValueYMouseOnClickStart - e.Y)/_host.Child.Height;

            if (double.IsInfinity(persentMove) ||
                persentMove == 0)
            {
                return;
            }

            double concateValue = 100*persentMove;

            if (myPosition.ValueYMouseOnClickStart < e.Y)
            {
                if (myPosition.Area.Position.Height < 10)
                {
                    return;
                }
                myPosition.Area.Position.Height = (float) (myPosition.HeightAreaOnClick - concateValue);
                myPosition.Area.Position.Y = (float) (myPosition.ValueYChartOnClick + concateValue);

                positionBeforeUs.Area.Position.Height = (float) (myPosition.HeightAreaOnClickBeforeUs + concateValue);
            }
            else if (myPosition.ValueYMouseOnClickStart > e.Y)
            {
                if (positionBeforeUs.Area.Position.Height < 10)
                {
                    return;
                }
                myPosition.Area.Position.Height = (float) (myPosition.HeightAreaOnClick + concateValue);
                myPosition.Area.Position.Y = (float) (myPosition.ValueYChartOnClick - concateValue);

                positionBeforeUs.Area.Position.Height = (float) (myPosition.HeightAreaOnClickBeforeUs - concateValue);
            }
            ReloadChartAreasSize();

        }

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

            MouseEventArgs mouse = (MouseEventArgs) e;

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

                if (double.IsNaN(_chart.ChartAreas[0].AxisX.ScaleView.Size))
                {
                    int max = 0;
                    for (int i = 0; i < _chart.Series.Count; i++)
                    {
                        if (_chart.Series[0].Points.Count > max)
                        {
                            max = _chart.Series[0].Points.Count;
                        }
                    }
                    myPosition.CountXValuesChartOnClickStart = max;
                }
                else
                {
                    myPosition.CountXValuesChartOnClickStart = (int) _chart.ChartAreas[0].AxisX.ScaleView.Size;
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


                int maxSize = 0;
                for (int i = 0; i < _chart.Series.Count; i++)
                {
                    if (_chart.Series[0].Points.Count > maxSize)
                    {
                        maxSize = _chart.Series[0].Points.Count;
                    }
                }


            if (myPosition.ValueXMouseOnClickStart < e.X)
            {
                if (myPosition.Area.Position.Height < 10)
                {
                    return;
                }

                double newVal = myPosition.CountXValuesChartOnClickStart +
                             myPosition.CountXValuesChartOnClickStart*persentMove*3;


                if (newVal > maxSize)
                {
                    _chart.ChartAreas[0].AxisX.ScaleView.Size = Double.NaN;
                }
                else
                {
                   
                    if (newVal + _chart.ChartAreas[0].AxisX.ScaleView.Position > maxSize)
                    {
                        _chart.ChartAreas[0].AxisX.ScaleView.Position = maxSize - newVal;
                    }

                    _chart.ChartAreas[0].AxisX.ScaleView.Size = newVal;
                    RePaintRightLebels();

                    if (_chart.ChartAreas[0].AxisX.ScaleView.Position + newVal > maxSize)
                    {
                        _chart.ChartAreas[0].AxisX.ScaleView.Position = maxSize - newVal;
                    }
                }
            }
            else if (myPosition.ValueXMouseOnClickStart > e.X)
            {
                double newVal = myPosition.CountXValuesChartOnClickStart -
               myPosition.CountXValuesChartOnClickStart * persentMove * 3;


                if (newVal < 5)
                {
                    _chart.ChartAreas[0].AxisX.ScaleView.Size = 5;
                }
                else
                {
                    if (!double.IsNaN(_chart.ChartAreas[0].AxisX.ScaleView.Size))
                    {
                        _chart.ChartAreas[0].AxisX.ScaleView.Position = _chart.ChartAreas[0].AxisX.ScaleView.Position + _chart.ChartAreas[0].AxisX.ScaleView.Size - newVal;
                    }
                    
                    _chart.ChartAreas[0].AxisX.ScaleView.Size = newVal;
                    if (_chart.ChartAreas[0].AxisX.ScaleView.Position + newVal > maxSize)
                    {
                        _chart.ChartAreas[0].AxisX.ScaleView.Position = maxSize - newVal;
                    }
                   
                }
            }

            ResizeYAxisOnArea("Prime");
            ResizeXAxis();
            ResizeSeriesLabels();
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

        private List<ChartAreaSizes> _areaSizes;

        /// <summary>
        /// взять актуальные мультипликаторы осей
        /// </summary>
        private void ReloadAreaSizes()
        {
            if (_areaSizes == null)
            {
                _areaSizes = new List<ChartAreaSizes>();
            }

            for (int indAreas = 0; indAreas < _chart.ChartAreas.Count; indAreas++)
            {
                ChartArea area = _chart.ChartAreas[indAreas];

                ChartAreaSizes size = _areaSizes.Find(areaSize => areaSize.Name == area.Name);

                if (size == null)
                {
                    size = new ChartAreaSizes(area.Name, 0, Convert.ToInt32(area.Position.Height), 1);
                    _areaSizes.Add(size);
                }

                if (area.Name == "Prime")
                {
                    size.Decimals = GetCandlesDecimal(_myCandles);
                    size.LineCount = size.High/10;
                }
                else if(area.Name != "Prime")
                {
                    size.Decimals = GetAreaDecimal(area.Name);
                    size.LineCount = size.High / 10;
                }

                if (size.LineCount < 3)
                {
                    size.LineCount = 3;
                }


                if (size.LineCount == 0)
                {
                    size.LineCount = 1;
                }
            }
        }

        
        private int GetCandlesDecimal(List<Candle> candles)
        {
            if (candles == null)
            {
                return 4;
            }
            decimal minPriceStep = decimal.MaxValue;
            int countFive = 0;

            CultureInfo culture = new CultureInfo("ru-RU");

            for (int i = 0; i < candles.Count && i < 20; i++)
            {
                Candle candleN = candles[i];


                decimal open = (decimal)Convert.ToDouble(candleN.Open);
                decimal high = (decimal)Convert.ToDouble(candleN.High);
                decimal low = (decimal)Convert.ToDouble(candleN.Low);
                decimal close = (decimal)Convert.ToDouble(candleN.Close);

                if (open == 0 &&
                    high == 0 &&
                    low == 0 &&
                    close == 0)
                {
                    continue;
                }

                if (open.ToString(culture).Split(',').Length > 1 ||
                    high.ToString(culture).Split(',').Length > 1 ||
                    low.ToString(culture).Split(',').Length > 1 ||
                    close.ToString(culture).Split(',').Length > 1)
                {
                    // если имеет место вещественная часть
                    int lenght = 1;

                    if (open.ToString(culture).Split(',').Length > 1 &&
                        open.ToString(culture).Split(',')[1].Length > lenght)
                    {
                        lenght = open.ToString(culture).Split(',')[1].Length;
                    }

                    if (high.ToString(culture).Split(',').Length > 1 &&
                        high.ToString(culture).Split(',')[1].Length > lenght)
                    {
                        lenght = high.ToString(culture).Split(',')[1].Length;
                    }

                    if (low.ToString(culture).Split(',').Length > 1 &&
                        low.ToString(culture).Split(',')[1].Length > lenght)
                    {
                        lenght = low.ToString(culture).Split(',')[1].Length;
                    }

                    if (close.ToString(culture).Split(',').Length > 1 &&
                        close.ToString(culture).Split(',')[1].Length > lenght)
                    {
                        lenght = close.ToString(culture).Split(',')[1].Length;
                    }

                    if (lenght == 1 && minPriceStep > 0.1m)
                    {
                        minPriceStep = 0.1m;
                    }
                    if (lenght == 2 && minPriceStep > 0.01m)
                    {
                        minPriceStep = 0.01m;
                    }
                    if (lenght == 3 && minPriceStep > 0.001m)
                    {
                        minPriceStep = 0.001m;
                    }
                    if (lenght == 4 && minPriceStep > 0.0001m)
                    {
                        minPriceStep = 0.0001m;
                    }
                    if (lenght == 5 && minPriceStep > 0.00001m)
                    {
                        minPriceStep = 0.00001m;
                    }
                    if (lenght == 6 && minPriceStep > 0.000001m)
                    {
                        minPriceStep = 0.000001m;
                    }
                    if (lenght == 7 && minPriceStep > 0.0000001m)
                    {
                        minPriceStep = 0.0000001m;
                    }
                    if (lenght == 8 && minPriceStep > 0.00000001m)
                    {
                        minPriceStep = 0.00000001m;
                    }
                    if (lenght == 9 && minPriceStep > 0.000000001m)
                    {
                        minPriceStep = 0.000000001m;
                    }
                    if (lenght == 10 && minPriceStep > 0.0000000001m)
                    {
                        minPriceStep = 0.0000000001m;
                    }
                    if (lenght == 11 && minPriceStep > 0.00000000001m)
                    {
                        minPriceStep = 0.00000000001m;
                    }
                }
                else
                {
                    // если вещественной части нет
                    int lenght = 1;

                    for (int i3 = open.ToString(culture).Length - 1; open.ToString(culture)[i3] == '0'; i3--)
                    {
                        lenght = lenght * 10;
                    }



                    int lengthLow = 1;

                    for (int i3 = low.ToString(culture).Length - 1; low.ToString(culture)[i3] == '0'; i3--)
                    {
                        lengthLow = lengthLow * 10;

                        if (lenght > lengthLow)
                        {
                            lenght = lengthLow;
                        }
                    }

                    int lengthHigh = 1;

                    for (int i3 = high.ToString(culture).Length - 1; high.ToString(culture)[i3] == '0'; i3--)
                    {
                        lengthHigh = lengthHigh * 10;

                        if (lenght > lengthHigh)
                        {
                            lenght = lengthHigh;
                        }
                    }

                    int lengthClose = 1;

                    for (int i3 = close.ToString(culture).Length - 1; close.ToString(culture)[i3] == '0'; i3--)
                    {
                        lengthClose = lengthClose * 10;

                        if (lenght > lengthClose)
                        {
                            lenght = lengthClose;
                        }
                    }
                    if (minPriceStep > lenght)
                    {
                        minPriceStep = lenght;
                    }

                    if (minPriceStep == 1 &&
                        open % 5 == 0 && high % 5 == 0 &&
                        close % 5 == 0 && low % 5 == 0)
                    {
                        countFive++;
                    }
                }
            }


            if (minPriceStep == 1 &&
                countFive == 20)
            {
                minPriceStep = 5;
            }

            // кол-во знаков после запятой

            int countZnak = 0;

            try
            {
                string[] valueMin2 =
                    minPriceStep.ToString(new CultureInfo("ru-RU")).Split(',');

                if (valueMin2.Length > 1 && valueMin2[1].Length > countZnak)
                {
                    countZnak = valueMin2[1].Length;
                }
            }
            catch (Exception)
            {
                countZnak = 0;
            }

           return countZnak;
        }

        private int GetAreaDecimal(string areaName)
        {
            SeriesCollection chartSeries = _chart.Series;

            List<Series> seriesOnArea = new List<Series>();

            for (int i = 0; chartSeries != null && i < chartSeries.Count; i++)
            {
                if (chartSeries[i].ChartArea == areaName)
                {
                    seriesOnArea.Add(chartSeries[i]);
                }
            }

            if (seriesOnArea.Count == 0)
            {
                return 0;
            }

           int maxDecimal = 0;

            for (int serIterator = 0; serIterator < seriesOnArea.Count; serIterator++)
            {
                if (seriesOnArea[serIterator].Points == null)
                {
                    continue;
                }
                for (int i = seriesOnArea[serIterator].Points.Count-1; i > -1 && i > seriesOnArea[serIterator].Points.Count-20; i--)
                {
                    if (seriesOnArea[serIterator].Points[i].YValues[0].ToString(_culture).Split(',').Length == 1)
                    {
                        continue;
                    }

                    int lengh = seriesOnArea[serIterator].Points[i].YValues[0].ToString(_culture).Split(',')[1].Length;
                    int lengh2 = seriesOnArea[serIterator].Points[i].YValues[0].ToString(_culture).Split(',')[0].Length;

                    bool find = false;

                    if (lengh > 1)
                    {
                        string value = seriesOnArea[serIterator].Points[i].YValues[0].ToString(_culture).Split(',')[1];
                        
                        for (int i2 = 2; i2 < value.Length; i2++)
                        {
                            if (value[i2] != '0' && value[i2 - 1] != '0' && value[i2 - 2] != '0')
                            {
                                lengh = i2+1;
                                find = true;
                                break;
                            }
                        }
                    }

                    if (lengh2 > 2 && lengh > 3)
                    {
                        lengh = 2;
                        break;
                    }

                    if (lengh > maxDecimal)
                    {
                        maxDecimal = lengh;
                    }
                    if (find == true)
                    { // нашли глубину
                        break;
                    }
                }
            }

            if (maxDecimal > 15)
            {
                maxDecimal = 15;
            }

            return maxDecimal;
        }

        /// <summary>
        /// изменились масштабы Оси Х
        /// </summary>
        void _chart_AxisViewChanged(object sender, ViewEventArgs e) 
        {
            try
            {
                ResizeYAxisOnArea("Prime");
                for (int i = 0; i < _chart.ChartAreas.Count; i++)
                {
                    if (_chart.ChartAreas[i].Name != "Prime")
                    {
                        ResizeYAxisOnArea(_chart.ChartAreas[i].Name);
                    }
                }
                ResizeXAxis();
                ResizeSeriesLabels();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// изменилось положение коретки скроллбара чарта
        /// </summary>
        void _chart_AxisScrollBarClicked(object sender, ScrollBarEventArgs e)
        {
            try
            {
                ResizeYAxisOnArea("Prime");

                for (int i = 0; i < _chart.ChartAreas.Count; i++)
                {
                    if (_chart.ChartAreas[i].Name != "Prime")
                    {
                        ResizeYAxisOnArea(_chart.ChartAreas[i].Name);
                    }
                }

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// поднялась ЛКМ
        /// </summary>
        void _chartForCandle_MouseUp(object sender, MouseEventArgs e) 
        {
            _mouseDown = false;
            _chart.Cursor = Cursors.Default;
        }

        /// <summary>
        /// опустилась ЛКМ
        /// </summary>
        private void _chartForCandle_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            if (IsPatternChart)
            {
                return;
            }
            _mouseDown = true;
            // принимаем событие в правой части хоста
            if (_areaPositions == null)
            {
                return;
            }

            _resizeArea = null;

            ChartAreaPosition myPosition = null;

            if (_areaPositions == null || 
                _areaPositions.Count == 0 || 
                _areaPositions[0].RightPoint == _chart.Width)
            {
                ReloadChartAreasSize();
            }

            if (_areaPositions == null)
            {
                return;
            }

            for (int i = 0; i < _areaPositions.Count; i++)
            {
                ChartAreaPosition pos = _areaPositions[i];

                if (pos.RightPoint < e.X && pos.UpPoint < e.Y && pos.DownPoint > e.Y)
                {
                    myPosition = pos;
                    break;
                }
            }

            if (myPosition == null)
            {
                return;
            }

            _resizeArea = myPosition.Area.Name;

            _firstY = e.Y;
            _chart.Cursor = Cursors.Hand;

        }

        private bool _mouseDown;

        /// <summary>
        /// Y по которому зажали ЛК
        /// </summary>
        private double _firstY;

        /// <summary>
        ///  Y текущий
        /// </summary>
        private double _lastY;

        /// <summary>
        /// область которую сейчас изменяем
        /// </summary>
        private string _resizeArea;

        /// <summary>
        /// двинулась мышь
        /// </summary>
        void _chartForCandle_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (_chart.Cursor != Cursors.Hand)
                {
                    return;
                }

                _lastY = e.Y;

                double mouseMove = _firstY - _lastY;

                _firstY = _lastY;

                if (_areaSizes == null)
                {
                    return;
                }

                ChartAreaSizes areaSize = _areaSizes.Find(size => size.Name == _resizeArea);

                if (areaSize == null)
                {
                    return;
                }

                if (mouseMove > 0)
                {
                    if (areaSize.Multiplier > 400000)
                    {
                        return;
                    }
                    if (areaSize.Multiplier > 3 || areaSize.Multiplier < -3)
                    {
                        areaSize.Multiplier = areaSize.Multiplier + Math.Abs(areaSize.Multiplier * 0.1);
                    }
                    else
                    {
                        areaSize.Multiplier += 1;
                    }

                }
                else if (mouseMove < 0)
                {
                    if (areaSize.Multiplier < -150)
                    {
                        return;
                    }
                    if (areaSize.Multiplier > 3 || areaSize.Multiplier < -3)
                    {
                        areaSize.Multiplier = areaSize.Multiplier - Math.Abs(areaSize.Multiplier * 0.1);
                    }
                    else
                    {
                        areaSize.Multiplier -= 1;
                    }

                }
                else
                {
                    return;
                }

                if (_resizeArea == "TradeArea")
                {
                    ResizeTickArea();
                }
                else
                {
                    ResizeYAxisOnArea(_resizeArea);
                }
               
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// изменить представление оси Y у области данных
        /// </summary>
        /// <param name="areaName">имя области данных</param>
        private void ResizeYAxisOnArea(string areaName)
        {
            if(areaName == "TradeArea")
            {
                return;
            }
            if (_isPaint == false)
            {
                return;
            }

            try
            {
                if (_chart.InvokeRequired)
                {
                    _chart.Invoke(new Action<string>(ResizeYAxisOnArea), areaName);
                    return;
                }

                ChartArea area = FindAreaByNameSafe(areaName);

                if (area == null)
                {
                    return;
                }

                Series candleSeries = FindSeriesByNameSafe("SeriesCandle"); // берём серию свечек с графика
                ChartArea candleArea = FindAreaByNameSafe("Prime"); // берём область для свечек с графика

                if (candleArea == null ||
                    candleSeries == null)
                {
// если какие-то данные не доступны. Выходим
                    return;
                }

                int firstX = 0; // первая отображаемая свеча
                int lastX = candleSeries.Points.Count; // последняя отображаемая свеча

                if (_chart.ChartAreas[0].AxisX.ScrollBar.IsVisible)
                {
// если уже выбран какой-то диапазон, назначаем первую и последнюю исходя из этого диапазона
                    firstX = Convert.ToInt32(candleArea.AxisX.ScaleView.Position);
                    lastX = Convert.ToInt32(candleArea.AxisX.ScaleView.Position) +
                            Convert.ToInt32(candleArea.AxisX.ScaleView.Size) + 1;
                }

                if (firstX < 0)
                {
                    firstX = 0;
                    lastX = firstX +
                            Convert.ToInt32(candleArea.AxisX.ScaleView.Size) + 1;
                }

                if (firstX == lastX ||
                    firstX > lastX ||
                    firstX < 0 ||
                    lastX <= 0)
                {

                    for (int i = 0; _chart.Series != null && i < _chart.Series.Count; i++)
                    {
                        if (_chart.Series[i].ChartArea == areaName)
                        {
                            if (_chart.Series[i].Points.Count > lastX)
                            {
                                lastX = _chart.Series[i].Points.Count;
                            }
                        }
                    }
                    if (firstX == lastX ||
                        firstX > lastX ||
                        firstX < 0 ||
                        lastX <= 0)
                    {
                        return;
                    }                    
                }

                if (areaName == "Prime" &&
                    lastX - firstX > 2050)
                {
                    firstX = lastX - 2000;
                    candleArea.AxisX.ScaleView.Size = 2000;
                    candleArea.AxisX.ScaleView.Position = firstX;
                    ResizeXAxis();
                    //return;
                }

                SeriesCollection chartSeries = _chart.Series;

                List<Series> seriesOnArea = new List<Series>();

                for (int i = 0; chartSeries != null && i < chartSeries.Count; i++)
                {
                    if (chartSeries[i].ChartArea == areaName)
                    {
                        seriesOnArea.Add(chartSeries[i]);
                    }
                }

                if (seriesOnArea.Count == 0)
                {
                    return;
                }

                if (SizeAxisXChangeEvent != null)
                {
                    SizeAxisXChangeEvent(lastX - firstX);
                }

                double max = double.MinValue;
                double min = double.MaxValue;

               for (int serIterator = 0; serIterator < seriesOnArea.Count; serIterator++)
               {

                   if(seriesOnArea[serIterator].Name == "Deal_BuySell")
                   {
                       continue;
                   }
                   SeriesYLength yLength = GetSeriesYLength(seriesOnArea[serIterator], firstX, lastX, candleSeries);

                   double minOnSeries = yLength.Ymin;

                   if (minOnSeries != double.MaxValue && 
                       minOnSeries < min)
                   {
                       min = minOnSeries;
                   }

                   double maxOnSeries = yLength.Ymax;

                   if (maxOnSeries > max)
                   {
                       max = maxOnSeries;
                   }

                  /* if (seriesOnArea[serIterator].ChartType == SeriesChartType.Column)
                   {
                       min = 0;
                   }*/
                }

                if (min == double.MaxValue ||
                    max == double.MinValue ||
                    max == min ||
                    max < min)
                {
                    return;
                }
                if (_areaSizes == null)
                {
                    ReloadAreaSizes();
                }

                

                ChartAreaSizes areaSize = _areaSizes.Find(size => size.Name == areaName);

                if (areaSize == null)
                {
                    return;
                }

                double value = (max - min) * (areaSize.Multiplier/100);

                max = Math.Round(Convert.ToDouble(max + value), areaSize.Decimals);
                min = Math.Round(Convert.ToDouble(min - value), areaSize.Decimals);

                if (min == double.MaxValue ||
                    max == 0 ||
                    max <= min)
                {
                    return;
                }

                area.AxisY2.Maximum = max;
                area.AxisY2.Minimum = min;

                int rounder = areaSize.Decimals + 2;
                if (rounder > 15)
                {
                    rounder = 15;
                    areaSize.Decimals = 5;
                }


                double interval = Math.Round((area.AxisY2.Maximum - area.AxisY2.Minimum) / areaSize.LineCount, rounder);

                area.AxisY2.Interval = interval;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            RePaintPrimeLines(areaName);
        }
        
        private List<SeriesYLength> _seriesYLengths = new List<SeriesYLength>();

        private SeriesYLength GetSeriesYLength(Series series, int start, int end, Series candleSeries)
        {
            SeriesYLength currentLength = _seriesYLengths.Find(yl => yl.Series.Name == series.Name);

            if(end >= series.Points.Count)
            {
                end = series.Points.Count - 1;
                if(start!= 0)
                {
                    start--;
                }
            }

            if (currentLength == null)
            {
                currentLength = new SeriesYLength();
                currentLength.Series = series;
                _seriesYLengths.Add(currentLength);
            }


            if (series.Points.Count < 100 ||
                end - start < 400)
            { // если у нас малое окно отображения, делаем всё в лоб
                currentLength.Xend = end;
                currentLength.Xstart = start;
                currentLength.Ymax = GetMaxFromSeries(series, start, end, candleSeries);
                currentLength.Ymin = GetMinFromSeries(series, start, end, candleSeries);
                currentLength.CountOneCandlesAdd = 0;
            }
            else if ((currentLength.Xend == end && currentLength.Xstart == start))
            { // отображение не изменилось
                return currentLength;
            }
            else if (currentLength.CountOneCandlesAdd < 20 &&
                    (currentLength.Xend +1 == end && currentLength.Xstart == start))
            { // отображение сдвинулось на один вправо

                int x = currentLength.Xend + 1;

                if (x >= series.Points.Count)
                {
                    x = series.Points.Count - 1;
                }
                if (series.Points[x].YValues.Max() > currentLength.Ymax)
                {
                    currentLength.Ymax = series.Points[x].YValues.Max();
                }
                if (series.Points[x].YValues.Min() < currentLength.Ymin)
                {
                    currentLength.Ymin = series.Points[x].YValues.Min();
                }
                currentLength.Xstart = start;
                currentLength.Xend = end;
                currentLength.CountOneCandlesAdd++;
            }
            else if (currentLength.CountOneCandlesAdd < 20 && 
                (currentLength.Xend + 1 == end && currentLength.Xstart + 1 == start))
            { // отображение сдвинулось на один вправо
                if (series.Points[currentLength.Xstart].YValues.Max() == currentLength.Ymax)
                {
                    currentLength.Ymax = series.Points[currentLength.Xstart + 1].YValues.Max();
                }
                if (series.Points[currentLength.Xstart].YValues.Min() == currentLength.Ymin)
                {
                    currentLength.Ymin = series.Points[currentLength.Xstart + 1].YValues.Min();
                }

                if (series.Points[currentLength.Xend +1].YValues.Max() > currentLength.Ymax)
                {
                    currentLength.Ymax = series.Points[currentLength.Xend + 1].YValues.Max();
                }
                if (series.Points[currentLength.Xend+1].YValues.Min() < currentLength.Ymin)
                {
                    currentLength.Ymin = series.Points[currentLength.Xend + 1].YValues.Min();
                }
                currentLength.Xstart = start;
                currentLength.Xend = end;
                currentLength.CountOneCandlesAdd ++;
            }
            else if (currentLength.Xend + 1 != end || currentLength.Xstart +1 != start ||
                currentLength.Ymax == 0 || currentLength.Ymin == 0 || currentLength.CountOneCandlesAdd>=20)
            { // нужно перезагрузить серию заного
                currentLength.Xend = end;
                currentLength.Xstart = start;
                currentLength.Ymax = GetMaxFromSeries(series, start, end, candleSeries);
                currentLength.Ymin = GetMinFromSeries(series, start, end, candleSeries);
                currentLength.CountOneCandlesAdd = 0;
            }


            return currentLength;
        }

        private double GetMaxFromSeries(Series series, int start, int end, Series candleSeries)
        {
            double max = Double.MinValue;

            double startVal = max;

            try
            {

                for (int i = start; series.Points.Count >= candleSeries.Points.Count - 1 && i <= end && i < series.Points.Count; )
                {
                    // серия паралельная свечкам, индикатор
                    if (series.Points[i].YValues.Max() > max &&
                        series.Points[i].YValues.Max() != 0)
                    {
                        max = series.Points[i].YValues.Max();
                    }

                    if (end - start > 500)
                    {
                        i += 2;
                    }
                    else if (end - start > 1500)
                    {
                        i += 10;
                    }
                    else
                    {
                        i += 1;
                    }
                }
            }
            catch (Exception)
            {

            }

            return max;
        }


        private double GetMinFromSeries(Series series, int start, int end, Series candleSeries)
        {
            double min = double.MaxValue;

            for (int i = start; series.Points.Count >= candleSeries.Points.Count - 1 && i <= end && i < series.Points.Count; )
            {
                double minOnPoint = series.Points[i].YValues.Min();
                if (minOnPoint < min &&
                    series.Points[i].YValues.Min() != 0)
                {
                    min = series.Points[i].YValues.Min();
                }

                if (end - start > 500)
                {
                    i += 2;
                }
                else if (end - start > 1500)
                {
                    i += 10;
                }
                else
                {
                    i += 1;
                }
            }
            return min;
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
            if(_myCandles == null ||
                _myCandles.Count ==0)
            {
                return;
            }
            ChartArea area = _chart.ChartAreas[0];

            double values = 0;

            int firstPos = 0;

            if (double.IsNaN(_chart.ChartAreas[0].AxisX.ScaleView.Size))
            {
                values = _myCandles.Count;
            }
            else
            {
                values = (int)_chart.ChartAreas[0].AxisX.ScaleView.Size;
                firstPos = (int) _chart.ChartAreas[0].AxisX.ScaleView.Position;
            }

            if (firstPos < 0 ||
                firstPos > _myCandles.Count)
            {
                return;
            }

            int labelCount = 4;


            if (_myCandles.Count <= 2)
            {
                labelCount = 2;
            }
            else if (_myCandles.Count == 3)
            {
                labelCount = 3;
            }

            area.AxisX.Interval = values/ labelCount;

            while(area.AxisX.CustomLabels.Count < labelCount)
            {
                area.AxisX.CustomLabels.Add(new CustomLabel());
            }
            while (area.AxisX.CustomLabels.Count > labelCount)
            {
                area.AxisX.CustomLabels.RemoveAt(0);
            }

            double value = firstPos + area.AxisX.Interval;

            if (labelCount < 4)
            {
                value = 0;
            }


            for (int i = 0; i < labelCount; i++)
            {
                area.AxisX.CustomLabels[i].FromPosition = value - area.AxisX.Interval * 0.7;
                area.AxisX.CustomLabels[i].ToPosition = value + area.AxisX.Interval * 0.7;
                area.AxisX.CustomLabels[i].Text = _myCandles[(int)value].TimeStart.ToString();
                
                value += area.AxisX.Interval;

                if(value >= _myCandles.Count)
                {
                    value = _myCandles.Count - 1;
                }
            }

        }

        /// <summary>
        /// изменить представление оси Y у области данных на которой отображаются тики
        /// </summary>
        private void ResizeTickArea()
        {
            ChartArea area = FindAreaByNameSafe("TradeArea");

            if (area == null)
            {
                return;
            }

            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action(ResizeTickArea));
                return;
            }

            try
            {
                area.AxisX.ScaleView.Scroll(area.AxisX.Maximum + 1);

                Series tickSeries = FindSeriesByNameSafe("trade");

                if (tickSeries == null)
                {
                    return;
                }

                double max = 0;
                double min = double.MaxValue;

                foreach (var point in tickSeries.Points)
                {
                    double val = point.YValues[0];

                    if (val != 0 && val > max)
                    {
                        max = point.YValues.Max();
                    }
                    if (val != 0 && val < min)
                    {
                        min = point.YValues.Min();
                    }
                }

                if (min == double.MaxValue ||
                    max == 0 ||
                    max == min ||
                    max < min)
                {
                    return;
                }

                if(_areaSizes == null ||
                    _areaSizes.Count == 0)
                {
                    return;
                }
                ChartAreaSizes areaSize = _areaSizes.Find(size => size.Name == "TradeArea");

                if (areaSize == null)
                {
                    return;
                }

                double value = (max - min) * (areaSize.Multiplier / 100);

                double maxResult = Math.Round(max + value, areaSize.Decimals);
                double minResult = Math.Round(min - value, areaSize.Decimals);

                if (maxResult <= minResult ||
                    minResult == 0)
                {
                    return;
                }


                area.AxisY2.Maximum = maxResult;
                area.AxisY2.Minimum = minResult;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
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

        /// <summary>
        /// изменилось представление по оси Х
        /// </summary>
        public event Action<int> SizeAxisXChangeEvent;
    }

    /// <summary>
    /// класс позволяющий передавать положение главного окна
    /// </summary>
    public class WindowCoordinate
    {
        /// <summary>
        /// левая точка Х у главного окна приложения
        /// </summary>
        public static decimal X;

        /// <summary>
        /// верхняя точка Y у главного окна приложения
        /// </summary>
        public static decimal Y;
    }

    /// <summary>
    /// объект для хранения подписей на оси Y
    /// </summary>
    public class ChartYLabels
    {
        /// <summary>
        /// Имя области
        /// </summary>
        public string AreaName;

        /// <summary>
        /// Имя серии
        /// </summary>
        public string SeriesName;

        /// <summary>
        /// цена
        /// </summary>
        public string Price;

        /// <summary>
        /// является ли линия линией перекрывающей другие
        /// </summary>
        public bool IsPrime;

        /// <summary>
        /// цвет серии данных
        /// </summary>
        public Color Color;
    }

    /// <summary>
    /// объект для хранения настроек осей
    /// </summary>
    public class ChartAreaSizes
    {
        public ChartAreaSizes(string name, int decimals, int high, int lineCount)
        {
            Decimals = decimals;
            Name = name;
            Multiplier = 1;
            High = high;
            LineCount = lineCount;
        }

        /// <summary>
        /// имя
        /// </summary>
        public string Name;

        /// <summary>
        /// точность
        /// </summary>
        public int Decimals;

        /// <summary>
        /// высота
        /// </summary>
        public int High;

        /// <summary>
        /// мультипликатор для центровки содержимого
        /// </summary>
        public double Multiplier;

        /// <summary>
        /// кол-во линий разделяющих область
        /// </summary>
        public int LineCount;
    }

    /// <summary>
    /// параметры расположения серии на оси Y
    /// </summary>
    public class SeriesYLength
    {
        /// <summary>
        /// серия
        /// </summary>
        public Series Series;

        /// <summary>
        /// начало отображения на оси X
        /// </summary>
        public int Xstart;

        /// <summary>
        /// конец отображения на оси X
        /// </summary>
        public int Xend;

        /// <summary>
        /// верхняя точка на оси Y
        /// </summary>
        public double Ymax;

        /// <summary>
        /// нижняя точка на оси Y
        /// </summary>
        public double Ymin;

        /// <summary>
        /// количество подряд приращения по одной свече
        /// </summary>
        public int CountOneCandlesAdd;

    }

    /// <summary>
    /// расположение даты на оси X
    /// </summary>
    public class TimeAxisXPoint
    {
        /// <summary>
        /// время 
        /// </summary>
        public DateTime PositionTime;

        /// <summary>
        /// номер на оси X
        /// </summary>
        public int PositionXPoint;
    }

    /// <summary>
    /// объект отражающий текущее положение области в окне. Абсолютные значения положения сторон областей.
    /// </summary>
    public class ChartAreaPosition
    {
        /// <summary>
        /// область
        /// </summary>
        public ChartArea Area;

        /// <summary>
        /// положение верхней точки 
        /// </summary>
        public double UpPoint;

        /// <summary>
        /// положение нижней точки 
        /// </summary>
        public double DownPoint;

        /// <summary>
        /// положение левой точки 
        /// </summary>
        public double LeftPoint;

        /// <summary>
        /// положение правой точки 
        /// </summary>
        public double RightPoint;

// переменные для настройки высоты областей

        /// <summary>
        /// значение по которому мы нажали на область
        /// </summary>
        public double ValueYMouseOnClickStart;

        /// <summary>
        /// высота области в момент когда мы её начали перетаскивать
        /// </summary>
        public double HeightAreaOnClick;

        /// <summary>
        /// точка Y области в момент когда мы её начали перетаскивать
        /// </summary>
        public double ValueYChartOnClick;

        /// <summary>
        /// высота верхней области в момент когда мы её начали перетаскивать
        /// </summary>
        public double HeightAreaOnClickBeforeUs;

// переменные для настройки ширины представления

        public double ValueXMouseOnClickStart;

        public int CountXValuesChartOnClickStart;
    }
}
