/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using OsEngine.Alerts;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Charts.ColorKeeper;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Logging;
using Color = System.Drawing.Color;
using Grid = System.Windows.Controls.Grid;
using Rectangle = System.Windows.Shapes.Rectangle;
using OsEngine.Language;

namespace OsEngine.Charts.CandleChart
{
    /// <summary>
    ///Candles drawing wizard
    /// Мастер прорисовки свечного графика
    /// </summary>
    public class WinFormsChartPainter : IChartPainter
    {
        //service сервис
        
        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="name">name of robot that owns chart/имя робота, которому принадлежит чарт</param>
        /// <param name="startProgram"> program that creates a class object/программа создающая объект класса</param>
        public WinFormsChartPainter(string name, StartProgram startProgram)
        {
            try
            {
                _startProgram = startProgram;
                _colorKeeper = new ChartMasterColorKeeper(name);
                _colorKeeper.LogMessageEvent += SendLogMessage;
                _colorKeeper.NeedToRePaintFormEvent += _colorKeeper_NeedToRePaintFormEvent;

                CreateChart();
                _chart.Text = name;

                Task task = new Task(PainterThreadArea);
                task.Start();               
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(),LogMessageType.Error);
            }
            
        }

        private void _chart_Click1(object sender, EventArgs e)
        {
            if (((MouseEventArgs)e).Button == MouseButtons.Right)
            {
                if (ChartClickEvent != null)
                {
                    ChartClickEvent(ChartClickType.RightButton);
                }
            }
            if (((MouseEventArgs)e).Button == MouseButtons.Left)
            {
                if (ChartClickEvent != null)
                {
                    ChartClickEvent(ChartClickType.LeftButton);
                }
            }
        }

        public event Action<ChartClickType> ChartClickEvent;

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

        public Chart GetChart()
        {
            return _chart;
        }

        /// <summary>
        /// candles
        /// свечки
        /// </summary>
        private List<Candle> _myCandles;
        public List<Candle> myCandles //AVP ChartRoulette 
        {
            get
            {
                return _myCandles; ;
            }
        }
        /// <summary>
        /// Time frame of candles which are drawn in chart in the form of time
        /// таймфрейм свечек которые прорисовываются в чарте в виде времени
        /// </summary>
        private TimeSpan _timeFrameSpan;

        /// <summary>
        /// timeframe of candles that appear on chart
        /// таймфрейм свечек которые прорисовываются в чарте
        /// </summary>
        private TimeFrame _timeFrame;

        /// <summary>
        /// Timeframes for this chart were set
        /// таймфреймы для данного чарта устанавливались
        /// </summary>
        private bool _timeFrameIsActivate;

        /// <summary>
        /// change candle timeframe for drawing
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
        /// to start drawing schedule
        /// начать прорисовку графика
        /// </summary>
        public void StartPaintPrimeChart(Grid gridChart, WindowsFormsHost host, Rectangle rectangle) 
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
                    _host.Dispatcher.Invoke(new Action<Grid, WindowsFormsHost,Rectangle>(StartPaintPrimeChart),gridChart, host,rectangle);
                    return;
                }
                _host.Child = _chart;
                _host.Child.Show();
                _isPaint = true;

                if (_rectangle != null)
                {
                    _rectangle.Fill =
                    new SolidColorBrush(System.Windows.Media.Color.FromArgb(_colorKeeper.ColorBackChart.A,
                    _colorKeeper.ColorBackChart.R, _colorKeeper.ColorBackChart.G, _colorKeeper.ColorBackChart.B));
                }

                ResizeYAxisOnArea("Prime");
                ResizeSeriesLabels();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Stop drawing chart
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

        public void ShowContextMenu(// TODO ContextMenu больше не поддерживается. Взамен используйте ContextMenuStrip. Подробности см. в https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
ContextMenuStrip menu)
        {
           _chart.ContextMenuStrip = menu;
        }

        /// <summary>
        /// whether drawing graphics is enabled
        /// включена ли прорисовка графика
        /// </summary>
        private bool _isPaint;

        private DateTime _lastTimeClear;

        /// <summary>
        /// clear chart
        /// очистить график
        /// </summary>
        public void ClearDataPointsAndSizeValue() 
        {
            try
            {
                if (_chart == null)
                {
                    return;
                }

                if (_chart.InvokeRequired)
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
                bool needToBackChild = false;

                if (_host != null && _host.Child != null)
                {
                    _host.Child = null;
                    needToBackChild = true;
                }

                Series oldCandleSeries = FindSeriesByNameSafe("SeriesCandle");
                if (oldCandleSeries.Points.Count != 0)
                {
                    oldCandleSeries.Points.ClearFast();
                }
                    
                //_chartElements = new List<IChartElement>();

                if (FindSeriesByNameSafe("Deal_" + "BuySell") != null)
                {
                    _chart.Series.Remove(FindSeriesByNameSafe("Deal_" + "BuySell"));
                }

                for (int i = 0; _chart.Series != null && i < _chart.Series.Count; i++)
                {
                    // delete stops
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
                        _chart.Series[i].Points.ClearFast();
                    }
 
                }

                ClearZoom();
                _myCandles = null;

                if (needToBackChild)
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
            if(_chart == null)
            {
                return;
            }
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
        /// delete all files related to class
        /// удалить все файлы связанные с классом
        /// </summary>
        public void Delete()
        {
            _isDeleted = true;
        }

        private void ClearDelete()
        {
            try
            {
                if (_chart == null)
                {
                    return;
                }

                if (_chart.InvokeRequired)
                {
                    _chart.Invoke(new Action(ClearDelete));
                    return;
                }

                if (_chart != null)
                {
                    _chart.AxisScrollBarClicked -= _chart_AxisScrollBarClicked;
                    _chart.AxisViewChanged -= _chart_AxisViewChanged;
                    _chart.Click -= _chart_Click;
                    _chart.MouseDown -= _chartForCandle_MouseDown;
                    _chart.MouseUp -= _chartForCandle_MouseUp;
                    _chart.MouseMove -= _chartForCandle_MouseMove;
                    _chart.MouseMove -= _chartForCandle_MouseMove2ChartElement;
                    _chart.MouseDown -= _chartForCandle_MouseDown2ChartElement;
                    _chart.MouseUp -= _chartForCandle_MouseUp2ChartElement;
                    _chart.MouseLeave -= _chart_MouseLeave;
                    _chart.Click -= _chart_Click1;
                    _chart.MouseMove -= _chart_MouseMove;
                    _chart.MouseMove -= _chart_MouseMove2;
                    _chart.ClientSizeChanged -= _chart_ClientSizeChanged;
                    _chart.AxisViewChanging -= _chart_AxisViewChanging;
                    _chart.MouseUp -= _chart_MouseUp;

                    _chart.Series.Clear();
                    _chart.ChartAreas.Clear();
                    _chart.Dispose();
                    _chart = null;
                }

                if (_colorKeeper != null)
                {
                    _colorKeeper.LogMessageEvent -= SendLogMessage;
                    _colorKeeper.NeedToRePaintFormEvent -= _colorKeeper_NeedToRePaintFormEvent;
                    //_colorKeeper.Delete();
                    _colorKeeper = null;
                }

                if (_areaPositions != null)
                {
                    _areaPositions.Clear();
                    _areaPositions = null;
                }

                if (_areaSizes != null)
                {
                    _areaSizes.Clear();
                    _areaSizes = null;
                }

                _myCandles = null;
                _chartElements = null;
                _labelSeries = null;
                _timePoints = null;
                _candlesToPaint = null;
                _indicatorsToPaint = null;
                _positions = null;
                _alertsToPaint = null;
            }
            catch(Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// a message came in from the color keeper to log
        /// из хранителя цвета пришло сообщение в лог
        /// </summary>
        /// <param name="message">message/сообщение</param>
        /// <param name="type">message type/тип сообщения</param>
        void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                // if no one's subscribed to us and there's a mistake
                // если никто на нас не подписан и происходит ошибка
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// outgoing message for log
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string,LogMessageType> LogMessageEvent;

        // working with chart colors and areas работа с цветом чарта и областей

        /// <summary>
        /// color storage for chart
        /// хранилище цветов для чарта
        /// </summary>
        private ChartMasterColorKeeper _colorKeeper;

        /// <summary>
        /// to set up a dark scheme for chart
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
        /// to set up a white scheme for chart
        /// установить белую схему для чарта
        /// </summary>
        public void SetWhiteScheme()
        {
            _colorKeeper.SetWhiteScheme();
        }

        /// <summary>
        /// Incoming event: colors of chart have changed
        /// входящее событие: изменились цвета графика
        /// </summary>
        void _colorKeeper_NeedToRePaintFormEvent()
        {
            RefreshChartColor();
            // right axis redrawing
            // перерисовка правой оси

            for (int i = 0; _chart.ChartAreas.Count != 0 && i < _chart.ChartAreas.Count; i++)
            {
                RePaintPrimeLines(_chart.ChartAreas[i].Name);
            }
        }

        /// <summary>
        /// to repaint colors at chart
        /// перекрасить цвета у чарта
        /// </summary>
        public void RefreshChartColor() 
        {
            try
            {
                if (_chart == null)
                {
                    return;
                }
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

        // Create zone and series deletion создание удаление областей и серий

        /// <summary>
        /// to create chart. Creates an initial area and a series for drawing candles
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
                _chart.SuppressExceptions = true;

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
                _chart.AxisScrollBarClicked += _chart_AxisScrollBarClicked; // cursor event/событие передвижения курсора
                
                _chart.AxisViewChanged += _chart_AxisViewChanged; // scale event/событие изменения масштаба
                _chart.MouseWheel += _chart_MouseWheel; // scroll and zoom chart using mouse wheel/прокрутка и зум чарта колесиком мышки
                _chart.Click += _chart_Click;
                _chart.MouseDown += _chartForCandle_MouseDown;
                _chart.MouseUp += _chartForCandle_MouseUp;
                _chart.MouseMove += _chartForCandle_MouseMove;
                _chart.MouseMove += _chartForCandle_MouseMove2ChartElement;
                _chart.MouseDown += _chartForCandle_MouseDown2ChartElement;
                _chart.MouseUp += _chartForCandle_MouseUp2ChartElement;
                _chart.MouseLeave += _chart_MouseLeave;
                _chart.Click += _chart_Click1;
                _chart.MouseMove += _chart_MouseMove;
                _chart.MouseMove += _chart_MouseMove2;
                _chart.MouseUp += _chart_MouseUp;
                _chart.ClientSizeChanged += _chart_ClientSizeChanged;
                _chart.AxisViewChanging += _chart_AxisViewChanging;

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /*
* Primary objects on the chart are the data areas- ChartArea
* On the areas, then, you can create a series of data - Series
* Series in turn has data points (Point) through which we draw objects
*/

        /*
         * Первичными объектами на чарте являются области данных - ChartArea
         * На областях, затем, можно создавать серии данных - Series
         * У Series в свою очередь есть точки данных(Point), через которые мы прорисовываем объекты
         */

        /// <summary>
        /// create a data area on chart
        /// создать область данных на чарте
        /// </summary>
        /// <param name="nameArea">area name/имя области</param>
        /// <param name="height">area height/высота области</param>
        /// <returns></returns>
        public string CreateArea(string nameArea, int height)
        {
            try
            {
                ChartArea area = FindAreaByNameSafe(nameArea);

                if (area != null)
                {
                    return area.Name;
                }


                if (height > 100 || string.IsNullOrWhiteSpace(nameArea))
                {
                    return null;
                }
                // 1 the areas that have already been created need to be moved.
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

                    int totalLength = 100 - allHeght - height;

                    if (totalLength < 0)
                    {
                        totalLength = 1;
                    }

                    primeArea.Position.Height = totalLength;
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
                return newArea.Name;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// create a series of data on area
        /// создать серию данных на области
        /// </summary>
        /// <returns>returns the name of data series. null in case of error/возвращается имя серии данных. null в случае ошибки</returns>
        public string CreateSeries(string areaName, IndicatorChartPaintType indicatorType, string name)
        {
            try
            {
                ChartArea area = GetChartArea(areaName);

                Series newSeries = new Series(name);

                newSeries.ChartArea = area.Name;
                newSeries.YAxisType = AxisType.Secondary;

                if (indicatorType == IndicatorChartPaintType.Line)
                {
                    newSeries.ChartType = SeriesChartType.Line;
                }
                if (indicatorType == IndicatorChartPaintType.Column)
                {
                    newSeries.ChartType = SeriesChartType.Column;
                }
                if (indicatorType == IndicatorChartPaintType.Point)
                {
                    newSeries.ChartType = SeriesChartType.Point;
                }
                if (indicatorType == IndicatorChartPaintType.Candle)
                {
                    newSeries.ChartType = SeriesChartType.Candlestick;
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
        /// to take the data area by name
        /// взять область данных по имени
        /// </summary>
        /// <param name="name">area name/имя области</param>
        /// <returns>returns a data area or null/возвращается область данных или null</returns>
        private ChartArea GetChartArea(string name)
        {
            return FindAreaByNameSafe(name);
        }

        public List<string> GetAreasNames()
        {
            try
            {
                List<string> areas = new List<string>();

                Chart chart = _chart;
                for (int i = 0; i < chart.ChartAreas.Count; i++)
                {
                    areas.Add(chart.ChartAreas[i].Name);
                }

                return areas;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(),LogMessageType.Error);
                return null;
            }
        }

        public int GetCursorSelectCandleNumber()
        {
            ChartArea candleArea = GetChartArea("Prime");

            if(candleArea.CursorX == null)
            {
                return 0;
            }

            if(double.IsNaN(candleArea.CursorX.Position) ||
               double.IsInfinity(candleArea.CursorX.Position) ||
               double.IsPositiveInfinity(candleArea.CursorX.Position))
            {
                return 0;
            }

            try
            {
                return Convert.ToInt32(candleArea.CursorX.Position);
            }
            catch
            {
                return 0;
            }
        }

        public decimal GetCursorSelectPrice()
        {
            ChartArea candleArea = GetChartArea("Prime");

            if (candleArea == null ||
                candleArea.CursorY == null ||
                double.IsNaN(candleArea.CursorY.Position) ||
                double.IsInfinity(candleArea.CursorY.Position))
            {
                return 0;
            }

            try
            {
                return Convert.ToDecimal(candleArea.CursorY.Position);
            }
            catch
            {
                return 0;
            }

        }

        public void RemoveCursor()
        {
            ChartArea candleArea = GetChartArea("Prime");
            candleArea.CursorY.Position = double.NaN;
        }

        /// <summary>
        /// removes the indicator from chart and, if it was the last one on tarea, then the area
        /// удаляет с графика индикатор и, если он был последний на области, то и область
        /// </summary>
        /// <param name="indicator">indicator that should be removed/индикатор который следует удалить</param>
        public void DeleteIndicator(IIndicator indicator)
        {
            try
            {

                for (int i = 0; i < 30; i++)
                {
                    Series mySeries = FindSeriesByNameSafe(indicator.Name + i);

                    if (mySeries != null)
                    {
                        if (mySeries.Points.Count != 0)
                        {
                            ClearLabelOnY2(mySeries.Name + "Label", mySeries.ChartArea, mySeries.Points[0].Color);
                        }
                        mySeries.Points.ClearFast();
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
        /// is there an indicator created
        /// создан ли индикатор
        /// </summary>
        /// <param name="name">indicator name/имя индикатора</param>
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
        /// whether the area is created
        /// создана ли область
        /// </summary>
        /// <param name="name">area name/имя области</param>
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

        // work of flow of the drawing chart / работа потока прорисовывающего чарт

        /// <summary>
        /// the method in which the flow drawing chart works
        /// метод в котором работает поток прорисовывающий чарт
        /// </summary>
        private async void PainterThreadArea()
        {
            while (true)
            {
                try
                {
                    await Task.Delay(1000);

                    if (_isDeleted)
                    {
                        ClearDelete();

                        await Task.Delay(1000);

                        GC.Collect();
                        GC.WaitForPendingFinalizers();
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
                        _candlesToPaint = new ConcurrentQueue<List<Candle>>();
                        _indicatorsToPaint = new ConcurrentQueue<IIndicator>();
                        await Task.Delay(5000);
                    }
                    // checking to see if the candles are here.
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
                    // checking to see if the positions have come
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

                    if (!_stopLimits.IsEmpty)
                    {
                        List<PositionOpenerToStopLimit> positions = new List<PositionOpenerToStopLimit>();

                        while (!_stopLimits.IsEmpty)
                        {
                            _stopLimits.TryDequeue(out positions);
                        }

                        if (positions != null)
                        {
                            PaintStopLimits(positions);
                        }
                    }

                    // see if there are any new elements to draw on chart
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

                        for (int i = elements.Count - 1; i > -1; i--)
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
                    // check if there are any new elements to remove from tchart
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

                        for (int i = elements.Count - 1; i > -1; i--)
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
                    // check if there are any indicators to draw on chart
                    // проверяем, есть ли индикаторы для прорисовки на чарте

                    if (!_indicatorsToPaint.IsEmpty)
                    {
                        List<IIndicator> elements = new List<IIndicator>();

                        while (!_indicatorsToPaint.IsEmpty)
                        {
                            IIndicator newElement;
                            _indicatorsToPaint.TryDequeue(out newElement);

                            if (newElement != null)
                            {
                                elements.Add(newElement);
                            }
                        }

                        List<IIndicator> elementsWithoutRepiat = new List<IIndicator>();

                        for (int i = elements.Count - 1; i > -1; i--)
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

                    if (!_alertsToPaint.IsEmpty)
                    {
                        List<AlertToChart> elements = new List<AlertToChart>();

                        while (!_alertsToPaint.IsEmpty)
                        {
                            AlertToChart newElement;
                            _alertsToPaint.TryDequeue(out newElement);

                            if (newElement != null)
                            {
                                elements.Add(newElement);
                            }
                        }

                        List<AlertToChart> elementsWithoutRepiat = new List<AlertToChart>();

                        for (int i = elements.Count - 1; i > -1; i--)
                        {
                            if (elementsWithoutRepiat.Find(element => element.Name == elements[i].Name) == null)
                            {
                                elementsWithoutRepiat.Add(elements[i]);
                            }
                        }

                        for (int i = 0; i < elementsWithoutRepiat.Count; i++)
                        {
                            PaintAlert(elementsWithoutRepiat[i]);
                        }
                    }

                    if (_needMoveChartToTheRight)
                    {
                        _needMoveChartToTheRight = false;
                        MoveChartToTheRightLogic(_scaleSizeToMoveChartToTheRight);
                        _scaleSizeToMoveChartToTheRight = 0;
                    }

                    if (_startProgram == StartProgram.IsTester ||
                        _startProgram == StartProgram.IsOsOptimizer)
                    {
                        await Task.Delay(2000);
                    }
                }
                catch(Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    await Task.Delay(5000);
                }
            }
        }

        /// <summary>
        /// it's time to stop drawing a chart at all
        /// пора прекращать прорисовывать чарт на совсем
        /// </summary>
        private bool _isDeleted;

        /// <summary>
        /// line of candles to draw
        /// очередь со свечками, которые нужно прорисовать
        /// </summary>
        private ConcurrentQueue<List<Candle>> _candlesToPaint = new ConcurrentQueue<List<Candle>>();

        /// <summary>
        /// line of positions to draw
        /// очередь с позициями, которые нужно прорисовать
        /// </summary>
        private ConcurrentQueue<List<Position>> _positions = new ConcurrentQueue<List<Position>>();

        /// <summary>
        /// line of chart elements to be drawn
        /// очередь с элементами чарта, которые нужно прорисовать
        /// </summary>
        private ConcurrentQueue<IChartElement> _chartElementsToPaint = new ConcurrentQueue<IChartElement>();

        /// <summary>
        /// queue with chart elements that need to be deleted
        /// очередь с элементами чарта, которые нужно удалить
        /// </summary>
        private ConcurrentQueue<IChartElement> _chartElementsToClear = new ConcurrentQueue<IChartElement>();

        /// <summary>
        /// line of indicators to draw
        /// очередь с индикаторами, которые нужно прорисовать
        /// </summary>
        private ConcurrentQueue<IIndicator> _indicatorsToPaint = new ConcurrentQueue<IIndicator>();

        private ConcurrentQueue<AlertToChart> _alertsToPaint = new ConcurrentQueue<AlertToChart>();

        ConcurrentQueue<List<PositionOpenerToStopLimit>> _stopLimits = new ConcurrentQueue<List<PositionOpenerToStopLimit>>();


        // candles / свечи

        /// <summary>
        /// data drawing culture
        /// культура отрисовки данных
        /// </summary>
        private CultureInfo _culture = new CultureInfo("ru-RU");

        private CultureInfo _cultureInterfaces = OsLocalization.CurCulture;

        /// <summary>
        /// candle drawing
        /// прорисовать свечки
        /// </summary>
        /// <param name="history">candles/свечи</param>
        private void PaintCandles(List<Candle> history)
        {
            if (_mouseDown == true)
            {
                return;
            }
            try
            {
                if(_chart == null)
                {
                    return;
                }

                if (_isPaint == false)
                {
                    return;
                }

                if (history == null && _myCandles == null)
                {
                    return;
                }

                if (_chart.InvokeRequired)
                {
                    _chart.Invoke(new Action<List<Candle>>(PaintCandles), history);
                    return;
                }

                if (_isPaint == false)
                {
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
                    oldcandleSeries = FindSeriesByNameSafe("SeriesCandle");
                }

                if (oldcandleSeries == null)
                {
                    return;
                }

                if (_startProgram != StartProgram.IsTester)
                {
                    // in the actual connection, each new candle is calculated 
                    // actual multipliers for regions
                    // в реальном подключении, каждую новую свечу рассчитываем 
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
                else if (oldcandleSeries.Points.Count > 1 &&
                    history.Count == oldcandleSeries.Points.Count
                    && oldcandleSeries.Points[0].YValues[0] != Convert.ToDouble(history[0].Low))
                { // другой массив свечек
                    if (history.Count < oldcandleSeries.Points.Count)
                    {
                        ClearZoom();
                    }

                    ReloadAreaSizes();
                    PaintAllCandles(history);
                    ResizeSeriesLabels();
                    RePaintRightLebels();
                    ResizeYAxisOnArea("Prime", true);
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
        /// add candles to the drawing
        /// добавить свечки на прорисовку
        /// </summary>
        /// <param name="history">candles/свечи</param>
        public void ProcessCandles(List<Candle> history)
        {
            if ((_startProgram == StartProgram.IsTester) &&
                _host != null)
            {
                PaintCandles(history);
            }
            else
            {
                if (_candlesToPaint != null &&
                    _candlesToPaint.IsEmpty == false 
                    &&
                    _candlesToPaint.Count > 0)
                {
                    List<Candle> res;

                    while (_candlesToPaint.IsEmpty == false)
                        _candlesToPaint.TryDequeue(out res);
                }
                if (_candlesToPaint != null)
                {
                    _candlesToPaint.Enqueue(history);
                }
            }
        }

        /// <summary>
        /// redraw candle by index
        /// перерисовать свечку по индексу
        /// </summary>
        /// <param name="history">candles/свечи</param>
        /// <param name="index">index/индекс</param>
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
            if (_chart == null)
            {
                return;
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

                if(_startProgram == StartProgram.IsOsTrader)
                {
                    candleSeries.Points[index].ToolTip = history[index].ToolTip;
                }

                ChartArea candleArea = FindAreaByNameSafe("Prime");
                if (candleArea != null && candleArea.AxisX.ScrollBar.IsVisible &&
                    candleArea.AxisX.ScaleView.Position + candleArea.AxisX.ScaleView.Size + 10 >= candleSeries.Points.Count)
                // if you've already selected a range
                //если уже выбран какой-то диапазон
                {
                    // Shift the view to right
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
        /// draw last candle
        /// прорисовать последнюю свечку
        /// </summary>
        /// <param name="history">candles/свечи</param>
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

            if (_startProgram == StartProgram.IsOsTrader)
            {
                candleSeries.Points[candleSeries.Points.Count - 1].ToolTip = history[index].ToolTip;
            }
            // candleSeries.Points[candleSeries.Points.Count - 1].AxisLabel = history[lastIndex].TimeStart.ToString(_culture);

                ChartArea candleArea = FindAreaByNameSafe("Prime");
            if (candleArea != null && candleArea.AxisX.ScrollBar.IsVisible &&
                candleArea.AxisX.ScaleView.Position + candleArea.AxisX.ScaleView.Size + 10 >= candleSeries.Points.Count)
            // if you've already selected a range
            //если уже выбран какой-то диапазон
            {
                // Shift the view to right
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
        /// to draw all candles
        /// прорисовать все свечки
        /// </summary>
        /// <param name="history">candles/свечи</param>
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


                if (_startProgram == StartProgram.IsOsTrader)
                {
                    candleSeries.Points[candleSeries.Points.Count - 1].ToolTip = history[candleSeries.Points.Count - 1].ToolTip;
                }
                    // candleSeries.Points[candleSeries.Points.Count - 1].AxisLabel = history[i].TimeStart.ToString(new CultureInfo("ru-RU"));
                }

            ChartArea candleArea = FindAreaByNameSafe("Prime");
            if (candleArea != null && candleArea.AxisX.ScrollBar.IsVisible)
                // if you've already selected a range
            //если уже выбран какой-то диапазон
            {
                // Shift the view to right
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

        // Deals / сделки

        /// <summary>
        /// add positions to the drawing
        /// добавить позиции в прорисовку
        /// </summary>
        /// <param name="deals">deals/сделки</param>
        public void ProcessPositions(List<Position> deals)
        {
            if (_startProgram == StartProgram.IsTester &&
                _host != null)
            {
                PaintPositions(deals);
            }
            else
            {
                if (_positions != null &&_positions.IsEmpty == false

                    &&
                    _positions.Count > 0)
                {
                    List<Position> res;

                    while (_positions.IsEmpty == false)
                        _positions.TryDequeue(out res);
                }
                if (_positions != null)
                {
                    _positions.Enqueue(deals);
                }    
            }
        }

        /// <summary>
        /// plot dealss on chart
        /// прорисовать сделки на графике
        /// </summary>
        /// <param name="deals">deals/сделки</param>
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
                //  create points of purchase and sales
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

                if(_colorKeeper.PointType == PointType.Auto)
                {
                    if(MainWindow.DebuggerIsWork)
                    {
                        buySellSeries.MarkerStyle = MarkerStyle.Cross;
                    }
                    else
                    {
                        buySellSeries.MarkerStyle = MarkerStyle.Triangle;
                    }
                }
                else if (_colorKeeper.PointType == PointType.Cross)
                {
                    buySellSeries.MarkerStyle = MarkerStyle.Cross;
                }
                else if (_colorKeeper.PointType == PointType.Circle)
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

                // очищаем временные отметки сделок, если они не совпадают со свечками
                if (_timePoints != null && _timePoints.Count > 0)
                {
                    TimeAxisXPoint firstChartTP = _timePoints.Find(tp => tp.PositionXPoint > 0);

                    if (_myCandles != null 
                        && _myCandles.Count > 1 
                        && firstChartTP != null 
                        &&
                        (firstChartTP.PositionXPoint > _myCandles.Count - 1
                         || firstChartTP.PositionTime < _myCandles[firstChartTP.PositionXPoint].TimeStart
                         || (_myCandles.Count - 1 > firstChartTP.PositionXPoint &&
                             firstChartTP.PositionTime > _myCandles[firstChartTP.PositionXPoint + 1].TimeStart)))
                    {
                        _timePoints.Clear();
                    }
                }

                for (int i = 0; i < deals.Count; i++)
                {
                    Position position = deals[i];

                    if(position == null)
                    {
                        continue;
                    }

                    List<MyTrade> trades = position.MyTrades;

                    for (int indTrades = 0; indTrades < trades.Count; indTrades++)
                    {
                        MyTrade curTrade = trades[indTrades];

                        if (curTrade == null)
                        {
                            continue;
                        }

                        DateTime timePoint = curTrade.Time;

                        if (timePoint == DateTime.MinValue)
                        {
                            continue;
                        }

                        int xIndexPoint = 0;

                        if(_startProgram == StartProgram.IsTester
                            || _startProgram == StartProgram.IsOsOptimizer)
                        {
                            xIndexPoint = curTrade.NumberCandleInTester;

                            if(xIndexPoint > _myCandles.Count)
                            {
                                curTrade.NumberCandleInTester = 0;
                                xIndexPoint = 0;
                            }
                        }

                        if(xIndexPoint == 0)
                        {
                            xIndexPoint = GetTimeIndex(timePoint, curTrade.Price);
                        }

                        if (xIndexPoint == 0)
                        {
                            continue;
                        }

                        buySellSeries.Points.AddXY(xIndexPoint, curTrade.Price);

                        if (_startProgram == StartProgram.IsOsTrader)
                        {
                            buySellSeries.Points[buySellSeries.Points.Count - 1].ToolTip = curTrade.ToolTip;
                        }

                        if (buySellSeries.MarkerStyle == MarkerStyle.Triangle)
                        {
                            // drawing pictures
                            // прорисовываем картинки
                            double openViewSize = _myCandles.Count;

                            if (_chart.ChartAreas[0].AxisX.ScaleView.IsZoomed)
                            {
                                openViewSize = _chart.ChartAreas[0].AxisX.ScaleView.Size;
                            }
                            if (curTrade.Side == Side.Buy)
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

                        if (curTrade.Side == Side.Buy)
                        {
                            if (position.CloseOrders != null
                                && position.CloseOrders.FindAll(x => x != null && x.NumberMarket == curTrade.NumberOrderParent).Count > 0)
                            {
                                buySellSeries.Points[buySellSeries.Points.Count - 1].Color = Color.OrangeRed; 
                            }
                            else
                            {
                                buySellSeries.Points[buySellSeries.Points.Count - 1].Color = Color.Aqua;
                            }
                        }
                        else
                        {
                            if (position.CloseOrders != null
                                && position.CloseOrders.FindAll(x => x != null && x.NumberMarket == curTrade.NumberOrderParent).Count > 0)
                            {
                                buySellSeries.Points[buySellSeries.Points.Count - 1].Color = Color.Yellow;
                            }
                            else
                            {
                                buySellSeries.Points[buySellSeries.Points.Count - 1].Color = Color.BlueViolet;
                            }
                        }
                    }
                }

                for (int i = 0; i < deals.Count; i++)
                {
                    // going through open order limit
                    // проходим Лимит ОРДЕРА НА ОТКРЫТИИ

                    Position position = deals[i];

                    if (position == null)
                    {
                        continue;
                    }

                    if ((position.State == PositionStateType.Closing
                        || position.State == PositionStateType.Opening
                        || position.State == PositionStateType.Open)
                        &&
                        position.OpenOrders != null 
                        && position.OpenOrders.Count > 0)
                    {
                        for (int j = 0; j < position.OpenOrders.Count; j++)
                        {
                            Order curOrder = position.OpenOrders[j];

                            if (curOrder == null)
                            {
                                continue;
                            }

                            if (curOrder.State != OrderStateType.Active)
                            {
                                continue;
                            }

                            Series lineSeries = new Series("Open_" + position.Number + j.ToString());
                            lineSeries.ChartType = SeriesChartType.Line;
                            lineSeries.YAxisType = AxisType.Secondary;
                            lineSeries.XAxisType = AxisType.Secondary;
                            lineSeries.ChartArea = "Prime";
                            lineSeries.ShadowOffset = 1;
                            lineSeries.YValuesPerPoint = 1;

                            lineSeries.Points.AddXY(0, curOrder.Price);
                            lineSeries.Points.AddXY(_myCandles.Count + 500, curOrder.Price);

                            if (curOrder.Side == Side.Buy)
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
                }
                for (int i = 0; i < deals.Count; i++)
                {
                    // going through Order limit on close
                    // проходим Лимит ОРДЕРА НА ЗАКРЫТИИ
                    Position position = deals[i];

                    if (position == null)
                    {
                        continue;
                    }

                    if ((position.State == PositionStateType.Closing
                        || position.State == PositionStateType.Opening
                        || position.State == PositionStateType.Open)
                        &&
                        position.CloseOrders != null &&
                        position.CloseOrders.Count > 0)
                    {
                        for(int j = 0;j < position.CloseOrders.Count;j++)
                        {
                            Order curOrder = position.CloseOrders[j];

                            if(curOrder == null)
                            {
                                continue;
                            }

                            if(curOrder.State != OrderStateType.Active)
                            {
                                continue;
                            }

                            Series lineSeries = new Series("Close_" + position.Number + j.ToString());
                            lineSeries.ChartType = SeriesChartType.Line;
                            lineSeries.YAxisType = AxisType.Secondary;
                            lineSeries.XAxisType = AxisType.Secondary;
                            lineSeries.ChartArea = "Prime";
                            lineSeries.ShadowOffset = 1;
                            lineSeries.YValuesPerPoint = 1;


                            lineSeries.Points.AddXY(0, curOrder.Price);
                            lineSeries.Points.AddXY(_myCandles.Count + 500, curOrder.Price);

                            if (curOrder.Side == Side.Buy)
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
                }
                for (int i = 0; i < deals.Count; i++)
                {
                    // going through stop order
                    // проходим СТОП ОРДЕРА

                    Position position = deals[i];

                    if (position == null)
                    {
                        continue;
                    }

                    if (
                        (position.State == PositionStateType.Closing
                        || position.State == PositionStateType.Opening
                        || position.State == PositionStateType.Open) 
                        &&
                        position.StopOrderIsActive)
                    {
                        Series lineSeries = new Series("Stop_" + position.Number);
                        lineSeries.ChartType = SeriesChartType.StepLine;
                        lineSeries.YAxisType = AxisType.Secondary;
                        lineSeries.XAxisType = AxisType.Secondary;
                        lineSeries.ChartArea = "Prime";
                        lineSeries.ShadowOffset = 1;
                        lineSeries.YValuesPerPoint = 1;


                        lineSeries.Points.AddXY(0, position.StopOrderRedLine);
                        lineSeries.Points.AddXY(_myCandles.Count + 500, position.StopOrderRedLine);

                        if (position.Direction == Side.Sell)
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
                {
                    // going through order profit
                    // проходим ПРОФИТ ОРДЕРА

                    Position position = deals[i];

                    if (position == null)
                    {
                        continue;
                    }

                    if (
                       (position.State == PositionStateType.Closing
                        || position.State == PositionStateType.Opening
                        || position.State == PositionStateType.Open) 
                        &&
                        position.ProfitOrderIsActive)
                    {
                        Series lineSeries = new Series("Profit_" + position.Number);
                        lineSeries.ChartType = SeriesChartType.Line;
                        lineSeries.YAxisType = AxisType.Secondary;
                        lineSeries.XAxisType = AxisType.Secondary;
                        lineSeries.ChartArea = "Prime";
                        lineSeries.ShadowOffset = 1;
                        lineSeries.YValuesPerPoint = 1;

                        lineSeries.Points.AddXY(0, position.ProfitOrderRedLine);
                        lineSeries.Points.AddXY(_myCandles.Count + 500, position.ProfitOrderRedLine);

                        if (position.Direction == Side.Sell)
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
        /// to draw a series of deals safely
        /// прорисовать серию со сделками безопасно
        /// </summary>
        /// <param name="buySellSeries">series of points/серия точек</param>
        /// <param name="profitStopOrderSeries">stop order series/серии стоп ордеров</param>
        private void PaintDealSafe(Series buySellSeries, Series [] profitStopOrderSeries)
        {
            try
            {
                if (_chart == null)
                {
                    return;
                }
                if (_chart.InvokeRequired)
                {
                    _chart.Invoke(new Action<Series, Series[]>
                        (PaintDealSafe), buySellSeries, profitStopOrderSeries);
                    return;
                }
                // 1 Drawing up deals
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

                // 2 Стопы и Профиты. Обновляем или добавляем новую серию 

                for (int i = 0; profitStopOrderSeries != null && i < profitStopOrderSeries.Length; i++)
                {
                    bool isInArray = false;

                    for(int j = 0; j < _chart.Series.Count;j++)
                    {
                        Series curSeries = null;

                        try
                        {
                            curSeries = _chart.Series[j];
                        }
                        catch
                        {
                            continue;
                        }

                        if (curSeries.Name == profitStopOrderSeries[i].Name)
                        {
                            if (curSeries.Points == null ||
                                  curSeries.Points.Count == 0)
                            {
                                curSeries.Points.Add(profitStopOrderSeries[i].Points[0]);
                                curSeries.Points.Add(profitStopOrderSeries[i].Points[1]);
                            }
                            else if (curSeries.Points[0].YValues[0] !=
                                profitStopOrderSeries[i].Points[0].YValues[0])
                            {
                                curSeries.Points[0] = profitStopOrderSeries[i].Points[0];
                                curSeries.Points[1] = profitStopOrderSeries[i].Points[1];
                            }

                            if (curSeries.Points[1].XValue != profitStopOrderSeries[i].Points[1].XValue)
                            {
                                curSeries.Points[1].XValue = profitStopOrderSeries[i].Points[1].XValue;
                            }

                            isInArray = true;
                            break;
                        }
                    }
                    if(isInArray == false)
                    {
                        _chart.Series.Add(profitStopOrderSeries[i]);
                        RePaintRightLebels();
                    }
                }

                // 3 Стопы и Профиты. Удаляем отсутствующие линии

                for (int j = 0; j < _chart.Series.Count; j++)
                {
                    Series curSeries = null;

                    try
                    {
                        curSeries = _chart.Series[j];
                    }
                    catch
                    {
                        continue;
                    }

                    string name = curSeries.Name.Split('_')[0];

                    if (name == "Stop" ||
                        name == "Profit" ||
                        name == "Open" ||
                        name == "Close")
                    {
                        bool isInArray = false;

                        for (int i = 0; profitStopOrderSeries != null && i < profitStopOrderSeries.Length; i++)
                        {
                            if (profitStopOrderSeries[i].Name == curSeries.Name)
                            {
                                isInArray = true;
                                break;
                            }
                        }

                        if (isInArray == false)
                        {
                            ClearLabelOnY2(curSeries.Name + "Label", "Prime", curSeries.Color);
                            ReMoveSeriesSafe(curSeries);
                            j--;
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        // index search by time
        // поиск индекса по времени

        /// <summary>
        /// Time points on chart tied to the X-axis
        /// временные точки на графике привязанные к оси X
        /// </summary>
        private List<TimeAxisXPoint> _timePoints = new List<TimeAxisXPoint>();

        /// <summary>
        /// get a candle index by time
        /// взять индекс свечи по времени
        /// </summary>
        /// <param name="time">time/время</param>
        /// <returns>index. If time is not found, returns/индекс. Если время не найдено, возвращает 0</returns>
        private int GetTimeIndex(DateTime time, decimal price)
        {
            if (_timePoints == null)
            {
                _timePoints = new List<TimeAxisXPoint>();
            }
            TimeAxisXPoint point = _timePoints.Find(po => po != null && po.PositionTime == time);

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
                result = MagicSearch(time, _myCandles, 0, _myCandles.Count, price);
            }
           
            point = new TimeAxisXPoint();
            point.PositionTime = time;
            point.PositionXPoint = result;
            _timePoints.Add(point);

            return result;
        }

        /// <summary>
        /// Finding a point on the X-axis to the forehead
        /// поиск точки на оси Х в лоб
        /// </summary>
        /// <param name="time">time to get an index on the X-axis/время по которому нужно узнать индекс на оси Х</param>
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
        /// Recursive, fast method of finding a point on the X-axis
        /// рекурсивный, быстрый метод поиска точки на оси Х
        /// </summary>
        private int MagicSearch(DateTime time, List<Candle> candles, int start, int end, decimal price)
        {
            if(_startProgram == StartProgram.IsTester
                && candles[candles.Count-1].TimeStart <= time
                && 
                (price > candles[candles.Count-1].High || price < candles[candles.Count - 1].Low))
            {// при прорисовке в тестере, пришла последняя сделка, по ещё не прорисованной свече
                return candles.Count;
            }

            if (end - start < 50)
            {
                for (int i = start; i < end; i++)
                {
                    if (candles[i].TimeStart >= time)
                    {
                        if (price == -1)
                        {
                            return i;
                        }

                        while (candles[i].TimeStart > time)
                        {
                            if (i == 0)
                            {
                                return i;
                            }

                            if (candles[i].TimeStart < time)
                            {
                                return i;
                            }

                            if (price <= candles[i].High
                                && price >= candles[i].Low)
                            {
                                return i;
                            }
                            i--;
                        }

                        if(_timeFrame == TimeFrame.Sec1)
                        {
                            if (price <= candles[i].High
                                && price >= candles[i].Low)
                            {
                                return i;
                            }
                            else if (price <= candles[i - 1].High
                                    && price >= candles[i - 1].Low)
                            {
                                return i - 1;
                            }
                            else if (i > 3 &&
                                price <= candles[i - 2].High
                             && price >= candles[i - 2].Low)
                            {
                                return i - 2;
                            }
                            else if (i + 1 != end &&
                               price <= candles[i + 1].High
                               && price >= candles[i + 1].Low)
                            {
                                return i + 1;
                            }
                        }

                        return i;
                    }
                    if (candles[i].Trades != null &&
                        candles[i].Trades.Count != 0 &&
                        candles[i].Trades[candles[i].Trades.Count - 1].Time >= time)
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

                if (_timeFrame == TimeFrame.Sec1)
                {
                    return end - 1;
                }
                else
                {
                    return end;
                }
            }

            if (candles[start + (end - start)/2].TimeStart > time)
            {
                return MagicSearch(time, candles, start, start + (end - start) / 2,price);
            }
            else if (candles[start + (end - start) / 2].TimeStart < time)
            {
                return MagicSearch(time, candles, start + (end - start) / 2, end, price);
            }
            else
            {
                return start + (end - start) / 2;
            }
        }

        // Stop Limits / стоп Лимиты

        /// <summary>
        /// add stop limits to the drawing
        /// добавить Стоп-лимиты в прорисовку
        /// </summary>
        /// <param name="deals">deals/сделки</param>
        public void ProcessStopLimits(List<PositionOpenerToStopLimit> stopLimits)
        {
            if (_startProgram == StartProgram.IsTester &&
                           _host != null)
            {
                PaintStopLimits(stopLimits);
            }
            else
            {
                if (_stopLimits != null && _stopLimits.IsEmpty == false
                    &&
                    _stopLimits.Count > 0)
                {
                    List<PositionOpenerToStopLimit> res;

                    while (_stopLimits.IsEmpty == false)
                    {
                        _stopLimits.TryDequeue(out res);
                    }
                }
                if (_stopLimits != null)
                {
                    _stopLimits.Enqueue(stopLimits);
                }
            }
        }

        private void PaintStopLimits(List<PositionOpenerToStopLimit> stopLimits)
        {
            if (_myCandles == null)
            {
                return;
            }
            if (stopLimits == null || _myCandles == null)
            {
                return;
            }

            if (_isPaint == false)
            {
                return;
            }

            List<Series> stopLimitsSeries = new List<Series>();

            for (int i = 0; i < stopLimits.Count; i++)
            {
                // going through stop order
                // проходим СТОП ОРДЕРА

                PositionOpenerToStopLimit curStop = stopLimits[i];

                Series lineSeries = new Series("StopLimit_" + curStop.Number);
                lineSeries.ChartType = SeriesChartType.StepLine;
                lineSeries.YAxisType = AxisType.Secondary;
                lineSeries.XAxisType = AxisType.Secondary;
                lineSeries.ChartArea = "Prime";
                lineSeries.ShadowOffset = 1;
                lineSeries.YValuesPerPoint = 1;

                lineSeries.Points.AddXY(0, curStop.PriceRedLine);
                lineSeries.Points.AddXY(_myCandles.Count + 500, curStop.PriceRedLine);

                if (curStop.Side == Side.Buy)
                {
                    lineSeries.Color = Color.DarkCyan;
                }
                else
                {
                    lineSeries.Color = Color.MediumVioletRed;
                }

                stopLimitsSeries.Add(lineSeries);
            }

            PaintStopLimitsSafe(stopLimitsSeries);
        }

        private void PaintStopLimitsSafe(List<Series> stopLimitsSeries)
        {
            if (_chart == null)
            {
                return;
            }
            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action<List<Series>>
                    (PaintStopLimitsSafe), stopLimitsSeries);
                return;
            }

            for (int i = 0; _chart.Series != null && i < _chart.Series.Count; i++)
            {
                // deleting old
                // удаляем старые
                string name = _chart.Series[i].Name.Split('_')[0];

                if (name == "StopLimit")
                {
                    ClearLabelOnY2(_chart.Series[i].Name + "Label", "Prime", _chart.Series[i].Color);
                    _chart.Series.Remove(_chart.Series[i]);
                    i--;
                }
            }

            for (int i = 0; stopLimitsSeries != null && i < stopLimitsSeries.Count; i++)
            {
                _chart.Series.Add(stopLimitsSeries[i]);
            }
        }

        // CUSTOM ELEMENTS ПОЛЬЗОВАТЕЛЬСКИЕ ЭЛЕМЕНТЫ

        /// <summary>
        /// draw an element on chart
        /// прорисовать элемент на чарте
        /// </summary>
        /// <param name="element">element/элемент</param>
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
                    PaintHorizontalLineOnArea((LineHorisontal)element);
                }
                if (element.TypeName() == "Circle")
                {
                    PaintCircle((Elements.Circle)element);
                }
                if (element.TypeName() == "RectangleElement")
                {
                    PaintRectangleElement((Elements.RectangleElement)element);
                }
                if (element.TypeName() == "Line")
                {
                    PaintLineElemOnArea((Elements.Line)element);
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
        /// add an item to the drawing
        /// добавить элемент в прорисовку
        /// </summary>
        /// <param name="element">element/элемент</param>
        public void ProcessElem(IChartElement element)
        {
            if (_startProgram == StartProgram.IsTester &&
                _host != null)
            {
                PaintElem(element);
            }
            else
            {
                if (_chartElementsToPaint.IsEmpty == false
                    &&
                    _chartElementsToPaint.Count > 5)
                {
                    IChartElement res;

                    while (_chartElementsToPaint.IsEmpty == false &&
                           _chartElementsToPaint.Count > 20)
                        _chartElementsToPaint.TryDequeue(out res);
                }

                _chartElementsToPaint.Enqueue(element);
            }
        }

        /// <summary>
        /// items on chart
        /// элементы на чарте
        /// </summary>
        private List<IChartElement> _chartElements;

        private void ClearElem(IChartElement element)
        {
            try
            {
                if (_chart == null)
                {
                    return;
                }
                if (_chart.InvokeRequired)
                {
                    _chart.Invoke(new Action<IChartElement>(ClearElem), element);
                    return;
                }
                // If there is such a series on chart - delete
                // если есть такая серия на графике - удаляем
                if (_chart.Series != null && _chart.Series.Count != 0)
                {
                    Series mySeries = FindSeriesByNameSafe(element.UniqName);

                    if (mySeries != null)
                    {
                        _chart.Series.Remove(mySeries);
                        ClearLabelOnY2(mySeries.Name + "Label", mySeries.Name, mySeries.Color);
                    }

                    Series mySeriesPoint = FindSeriesByNameSafe(element.UniqName + "Point");

                    if(mySeriesPoint == null)
                    {
                        mySeriesPoint = FindSeriesByNameSafe(element.UniqName + "Points");
                    }

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
        /// take the element off the chart
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
                if (_chartElementsToClear.IsEmpty == false
                    &&
                    _chartElementsToClear.Count > 5)
                {
                    IChartElement res;

                    while (_chartElementsToClear.IsEmpty == false &&
                           _chartElementsToClear.Count > 200)
                        _chartElementsToClear.TryDequeue(out res);
                }

                _chartElementsToClear.Enqueue(element);
            }
        }

        public void PaintLineElemOnArea(Elements.Line lineElement)
        {
            if (lineElement.ValueYStart == 0)
            {
                return;
            }
            if (lineElement.ValueYEnd == 0)
            {
                return;
            }
            if (_myCandles == null)
            {
                return;
            }
            if (_chart == null)
            {
                return;
            }
            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action<Elements.Line>(PaintLineElemOnArea), lineElement);
                return;
            }

            Series newSeries = FindSeriesByNameSafe(lineElement.UniqName);

            if (newSeries == null)
            {
                newSeries = new Series(lineElement.UniqName);
                newSeries.ChartType = SeriesChartType.Line;
                newSeries.BorderWidth = lineElement.LineWidth;
                newSeries.Color = lineElement.Color;
                newSeries.ChartArea = lineElement.Area;
                newSeries.YAxisType = AxisType.Secondary;
                newSeries.XAxisType = AxisType.Primary;
            }

            if (!string.IsNullOrWhiteSpace(lineElement.Label))
            {
                newSeries.Label = lineElement.Label;
                newSeries.LabelForeColor = lineElement.LabelTextColor.Name == "0" ? Color.White : lineElement.LabelTextColor;
                newSeries.Font = lineElement.Font ?? new Font("Arial", 7);
                newSeries.LabelBackColor = lineElement.LabelBackColor.Name == "0" ? Color.Transparent : lineElement.LabelBackColor;
            }

            int firstIndex = 0;
            int secondIndex = _myCandles.Count - 1;

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

            if (lineElement.Label != null)
            {
                newSeries.Label = lineElement.Label;
            }

            if (newSeries.Points == null || newSeries.Points.Count == 0)
            {
                newSeries.Points.AddXY(firstIndex, lineElement.ValueYStart);
                newSeries.Points.AddXY(secondIndex, lineElement.ValueYEnd);
            }
            else
            {
                if (newSeries.Points[0].YValues[0] != (double)lineElement.ValueYStart ||
                    newSeries.Points[1].YValues[0] != (double)lineElement.ValueYEnd)
                {
                    ClearLabelOnY2(newSeries.Name + "Label", newSeries.ChartArea, newSeries.Color);
                    newSeries.Points[0].YValues[0] = (double)lineElement.ValueYStart;
                    newSeries.Points[1].YValues[0] = (double)lineElement.ValueYEnd;
                    RePaintRightLebels();
                }

                if (newSeries.Points[0].XValue != firstIndex ||
                    newSeries.Points[1].XValue != secondIndex)
                {
                    newSeries.Points[0].XValue = firstIndex;
                    newSeries.Points[1].XValue = secondIndex;
                }
            }

            PaintSeriesSafe(newSeries);
        }

        /// <summary>
        /// to draw a horizontal line across the entire chart
        /// прорисовать горизонтальную линию через весь чарт
        /// </summary>
        /// <param name="lineElement">line/линия</param>
        public void PaintHorizontalLineOnArea(LineHorisontal lineElement)
        {
            if (lineElement.Value == 0)
            {
                return;
            }
            if (_myCandles == null)
            {
                return;
            }
            if (_chart == null)
            {
                return;
            }
            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action<LineHorisontal>(PaintHorizontalLineOnArea), lineElement);
                return;
            }

            Series newSeries = FindSeriesByNameSafe(lineElement.UniqName);

            if(newSeries == null)
            {
                newSeries = new Series(lineElement.UniqName);
                newSeries.ChartType = SeriesChartType.Line;
                newSeries.BorderWidth = lineElement.LineWidth;
                newSeries.Color = lineElement.Color;
                newSeries.ChartArea = lineElement.Area;
                newSeries.YAxisType = AxisType.Secondary;
                newSeries.XAxisType = AxisType.Primary;
            }

            if (!string.IsNullOrWhiteSpace(lineElement.Label))
            {
                newSeries.Label = lineElement.Label;
                newSeries.LabelForeColor = lineElement.LabelTextColor.Name == "0" ? Color.White : lineElement.LabelTextColor;
                newSeries.Font = lineElement.Font ?? new Font("Arial", 7);
                newSeries.LabelBackColor = lineElement.LabelBackColor.Name == "0" ? Color.Transparent : lineElement.LabelBackColor;
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

            if(lineElement.Label != null)
            {
                newSeries.Label = lineElement.Label;
            }

            if (newSeries.Points == null || newSeries.Points.Count == 0)
            {
                newSeries.Points.AddXY(firstIndex, lineElement.Value);
                newSeries.Points.AddXY(secondIndex, lineElement.Value);
            }
            else
            {
                if (newSeries.Points[0].YValues[0] != (double)lineElement.Value ||
                    newSeries.Points[1].YValues[0] != (double)lineElement.Value)
                {
                    ClearLabelOnY2(newSeries.Name + "Label", newSeries.ChartArea, newSeries.Color);
                    newSeries.Points[0].YValues[0] = (double)lineElement.Value;
                    newSeries.Points[1].YValues[0] = (double)lineElement.Value;
                    RePaintRightLebels();
                }

                if (newSeries.Points[0].XValue != firstIndex ||
                    newSeries.Points[1].XValue != secondIndex)
                {
                    newSeries.Points[0].XValue = firstIndex;
                    newSeries.Points[1].XValue = secondIndex;
                }
            }

            PaintSeriesSafe(newSeries);

            if (!lineElement.CanResize)
            {
                // If can't move, we'll go out
                // если двигать нельзя, выходим
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
        /// draw a point on chart
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
            newSeries.Label = point.Label;
            newSeries.LabelForeColor = point.LabelTextColor.Name == "0" ? Color.White : point.LabelTextColor;
            newSeries.Font = point.Font ?? new Font("Arial", 7);
            newSeries.LabelBackColor = point.LabelBackColor.Name == "0" ? Color.Transparent : point.LabelBackColor;

            newSeries.Points.AddXY(index, point.Y);

            PaintSeriesSafe(newSeries);
        }

        /// <summary>
        /// draw a circle on chart
        /// нарисовать на чарте окружность
        /// </summary>
        /// <param name="circle"></param>
        public void PaintCircle(Circle circle)
        {
            if (circle.Y <= 0)
            {
                return;
            }
            if (_myCandles == null)
            {
                return;
            }

            int index = _myCandles.FindIndex(candle => candle.TimeStart >= circle.TimeCenter);

            if (index < 0 || index >= _myCandles.Count)
            {
                return;
            }

            Series newSeries = new Series(circle.UniqName + "Point");

            DataPoint point = new DataPoint(index, Convert.ToDouble(circle.Y));

            if (circle.Diameter > 1000)
            {
                point.MarkerSize = 1000;
            }
            else
            {
                point.MarkerSize = circle.Diameter;
            }
            if (circle.Thickness > 100)
            {
                point.MarkerBorderWidth = 100;
            }
            else
            {
                point.MarkerBorderWidth = circle.Thickness;
            }

            point.MarkerStyle = MarkerStyle.Circle;               
            point.Color = Color.FromArgb(0);                      
            point.MarkerBorderColor = circle.Color;               
            point.Label = circle.Label;                           
            point.LabelForeColor = circle.LabelTextColor;         
            point.LabelBackColor = circle.LabelBackColor;         

            newSeries.Points.Add(point);                    

            newSeries.YAxisType = AxisType.Secondary;         
            newSeries.XAxisType = AxisType.Primary;
            newSeries.Font = circle.Font;                    

            PaintSeriesSafe(newSeries);
        }

        /// <summary>
        /// draw a rectangle on chart
        /// нарисовать на чарте прямоугольник
        /// </summary>
        /// <param name="rectangle"></param>
        public void PaintRectangleElement(RectangleElement rectangle)
        {
            if ((rectangle.ValueYStart <= 0) || (rectangle.ValueYEnd <= 0))
            {
                return;
            }
            if (_myCandles == null)
            {
                return;
            }
            if (_chart == null)
            {
                return;
            }
            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action<RectangleElement>(PaintRectangleElement), rectangle);
                return;
            }

            Series newSeries = FindSeriesByNameSafe(rectangle.UniqName + "Points");

            if (newSeries == null)
            {
                newSeries = new Series(rectangle.UniqName + "Points");
                newSeries.ChartType = SeriesChartType.Line;
                newSeries.BorderWidth = rectangle.Thickness;
                newSeries.Color = rectangle.Color;
                newSeries.ChartArea = rectangle.Area;
                newSeries.YAxisType = AxisType.Secondary;
                newSeries.XAxisType = AxisType.Primary;
            }

            if (!string.IsNullOrWhiteSpace(rectangle.Label))
            {
                // устанавливаем шрифт для всей серии, но Label добавим позже только одной точке
                newSeries.LabelForeColor = rectangle.LabelTextColor.Name == "0" ? Color.White : rectangle.LabelTextColor;
                newSeries.Font = rectangle.Font ?? new Font("Arial", 7);
                newSeries.LabelBackColor = rectangle.LabelBackColor.Name == "0" ? Color.Transparent : rectangle.LabelBackColor;
            }

            int firstIndex = 0;
            int secondIndex = _myCandles.Count - 1;

            if (rectangle.TimeStart != DateTime.MinValue)
            {
                int index = _myCandles.FindIndex(candle => candle.TimeStart >= rectangle.TimeStart);
                if (index >= 0)
                {
                    firstIndex = index;
                }
            }

            if (rectangle.TimeEnd != DateTime.MaxValue)
            {
                int index = _myCandles.FindIndex(candle => candle.TimeStart >= rectangle.TimeEnd);

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

            if (newSeries.Points == null || newSeries.Points.Count == 0)
            {
                newSeries.Points.AddXY(firstIndex, rectangle.ValueYEnd);
                newSeries.Points.AddXY(firstIndex, rectangle.ValueYStart);
                newSeries.Points.AddXY(secondIndex, rectangle.ValueYStart);
                newSeries.Points.AddXY(secondIndex, rectangle.ValueYEnd);
                newSeries.Points.AddXY(firstIndex, rectangle.ValueYEnd);

                // label one point | подпись одной точки
                if (!string.IsNullOrWhiteSpace(rectangle.Label) && rectangle.LabelCorner > 0 && rectangle.LabelCorner < 5)
                {
                    newSeries.Points[rectangle.LabelCorner - 1].Label = rectangle.Label;
                }
            }
            else
            {
                // проверяем актуальность Y значений
                if (newSeries.Points[0].YValues[0] != (double)rectangle.ValueYEnd ||
                    newSeries.Points[1].YValues[0] != (double)rectangle.ValueYStart ||
                    newSeries.Points[2].YValues[0] != (double)rectangle.ValueYStart ||
                    newSeries.Points[3].YValues[0] != (double)rectangle.ValueYEnd ||
                    newSeries.Points[4].YValues[0] != (double)rectangle.ValueYEnd
                    )
                {
                    ClearLabelOnY2(newSeries.Name + "Label", newSeries.ChartArea, newSeries.Color);

                    newSeries.Points[0].YValues[0] = (double)rectangle.ValueYEnd;
                    newSeries.Points[1].YValues[0] = (double)rectangle.ValueYStart;
                    newSeries.Points[2].YValues[0] = (double)rectangle.ValueYStart;
                    newSeries.Points[3].YValues[0] = (double)rectangle.ValueYEnd;
                    newSeries.Points[4].YValues[0] = (double)rectangle.ValueYEnd;

                    // устанавливаем подпись для одной точки прямоугольника
                    for (int i = 0; i < newSeries.Points.Count - 1; i++)
                    {
                        if ((rectangle.LabelCorner - 1 == i) && (!string.IsNullOrWhiteSpace(rectangle.Label)))
                            newSeries.Points[i].Label = rectangle.Label;
                        else
                            newSeries.Points[i].Label = "";
                    }

                    RePaintRightLebels();
                }

                // проверяем актуальность X значений
                if (newSeries.Points[0].XValue != firstIndex ||
                    newSeries.Points[1].XValue != firstIndex ||
                    newSeries.Points[2].XValue != secondIndex ||
                    newSeries.Points[3].XValue != secondIndex ||
                    newSeries.Points[4].XValue != firstIndex)
                {
                    newSeries.Points[0].XValue = firstIndex;
                    newSeries.Points[1].XValue = firstIndex;
                    newSeries.Points[2].XValue = secondIndex;
                    newSeries.Points[3].XValue = secondIndex;
                    newSeries.Points[4].XValue = firstIndex;
                }
            }

            PaintSeriesSafe(newSeries);
        }


        // Drag and drop Custom items  Перетаскивание Пользовательских элементов

        /// <summary>
        /// element that user clicked
        /// элемент по которому пользователь произвёл клик
        /// </summary>
        private IChartElement _clickElement;

        /// <summary>
        /// if the drag and drop of element
        /// началось ли перетаскивание элемента
        /// </summary>
        private bool _isMoving;

        /// <summary>
        /// the value of line along the axis of players when press the button
        /// значение линии по оси игрик при нажатии на кнопку
        /// </summary>
        private decimal _yPoint;

        /// <summary>
        /// X-axis line value when the button pressed
        /// значение линии по оси икс при нажатии на кнопку
        /// </summary>
        private decimal _xPoint;

        /// <summary>
        /// cursor value along the axis of game when press button
        /// значение курсора по оси игрик при нажатии на кнопку
        /// </summary>
        private decimal _yMouse;

        /// <summary>
        /// X-axis cursor value when click on a point
        /// значение курсора по оси икс при нажатии на точку
        /// </summary>
        private decimal _xMouse;

        /// <summary>
        ///  mouse moved on schedule.
        /// двинулась мышь по графику
        /// </summary>
        void _chartForCandle_MouseMove2ChartElement(object sender, MouseEventArgs e)
        {
            if (IsPatternChart)
            {
                return;
            }

            if (_chart.Cursor != Cursors.SizeAll)
            {
                // if user hasn't pinched the item
                // если пользователь не зажал элемент
                return;

            }
// Y
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
                // round it up. 
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
                // X
                // икс
                decimal widthInPicsel = _host.Child.Width * Convert.ToDecimal(_chart.ChartAreas[0].InnerPlotPosition.Width / 100);
                decimal widthInInts = Convert.ToDecimal(_chart.ChartAreas[0].AxisX.ScaleView.ViewMaximum - _chart.ChartAreas[0].AxisX.ScaleView.ViewMinimum);

                decimal intsToPicsel = widthInInts / widthInPicsel;

                decimal moveMouseX = e.X - _xMouse;
                // This is right point on the X-axis.
                // это у нас нужная точка по оси икс
                int moveInInts = Convert.ToInt32(_xPoint + moveMouseX * intsToPicsel);
                // looking for what time it is.
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
        /// as been put on chart LCM.
        /// опустилась ЛКМ на чарт
        /// </summary>
        private void _chartForCandle_MouseDown2ChartElement(object sender, MouseEventArgs e) 
        {
            if (IsPatternChart)
            {
                return;
            }

            if (_chartElements == null)
            {
                return;
            }
            // need to find X and Y we clicked on.
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
                // Now we're checking to see if any of the lines are pressed
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
        /// LCM up off chart.
        /// поднялась ЛКМ с чарта
        /// </summary>
        void _chartForCandle_MouseUp2ChartElement(object sender, MouseEventArgs e)
        {
            if (IsPatternChart)
            {
                return;
            }

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

        // Alerts АЛЕРТЫ

        public void RemoveAlert(AlertToChart alertToChart)
        {
            if (_chart == null)
            {
                return;
            }
            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action<AlertToChart>(RemoveAlert), alertToChart);
                return;
            }

            try
            {
                for(int i = 0; alertToChart.Lines != null && i < alertToChart.Lines.Length;i++)
                {
                    string curLineSeries = "Alert_" + alertToChart.Name + i;

                    for (int i2 = 0; i2 < _chart.Series.Count; i2++)
                    {
                        if (_chart.Series[i2].Name == curLineSeries)
                        {
                            ClearLabelOnY2(
                                _chart.Series[i2].Name + "Label",
                                _chart.Series[i2].ChartArea,
                                _chart.Series[i2].Color);

                            ReMoveSeriesSafe(_chart.Series[i2]);
                            break;
                        }
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

        public bool HaveAlertOnChart(AlertToChart alertToChart)
        {

            try
            {
                // 1 проверяем время начала и конца серии

                if(_myCandles == null)
                {
                    return false;
                }

                if (_chart == null)
                {
                    return false;
                }

                if(alertToChart.Lines == null
                    || alertToChart.Lines.Length == 0)
                {
                    return false;
                }

                DateTime timeLastPoitn = alertToChart.Lines[0].TimeSecondPoint;

                // 2 проверямем последнее значение серии. Чтобы совпадало с тем что на чарте

                for (int i = 0; i < alertToChart.Lines.Length; i++)
                {
                    string curLineSeries = "Alert_" + alertToChart.Name + i;

                    ChartAlertLine curLine = alertToChart.Lines[i];

                    for (int i2 = 0; i2 < _chart.Series.Count; i2++)
                    {
                        if (_chart.Series[i2].Name == curLineSeries)
                        { // проверяем значение серии. Чтобы совпадала с серией в алерте

                            DataPoint firstPoint = _chart.Series[i2].Points[0];
                            DataPoint secondPoint = _chart.Series[i2].Points[1];

                            int x1 = 0;
                            int x2 = _myCandles.Count - 1;
                            decimal valueFirst = GetFirstLineAlertPointY(_myCandles, curLine);
                            decimal valieSecond = GetSecondLineAlertPointY(_myCandles, curLine);

                            if (Convert.ToDecimal(firstPoint.YValues[0]) != valueFirst
                                || Convert.ToDecimal(secondPoint.YValues[0]) != valieSecond
                                || Convert.ToInt32(firstPoint.XValue) != x1 
                                || Convert.ToInt32(secondPoint.XValue) != x2)
                            {
                                return false;
                            }

                            break;
                        }
                        if(i2 + 1 == _chart.Series.Count)
                        {
                            // не нашли такой линии вообще на чарте
                            return false;
                        }
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

            return true;
        }

        public void ProcessAlert(AlertToChart alert, bool needToWait)
        {
            if(_host == null)
            {
                return;
            }

            if ((_startProgram == StartProgram.IsTester)
                || needToWait == false)
            {
                PaintAlert(alert);
            }
            else
            {
                if (_alertsToPaint != null &&
                   _alertsToPaint.IsEmpty == false
                    &&
                    _alertsToPaint.Count > 25)
                {
                    AlertToChart res;

                    while (_alertsToPaint.IsEmpty == false &&
                           _alertsToPaint.Count > 25)

                        _alertsToPaint.TryDequeue(out res);
                }

                if (_alertsToPaint != null)
                {
                    _alertsToPaint.Enqueue(alert);
                }
            }
        }

        private void PaintAlert(AlertToChart alert)
        {
            if (_chart == null)
            {
                return;
            }
            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action<AlertToChart>(PaintAlert), alert);
                return;
            }

            PaintAlert(alert.Lines, alert.ColorLine, alert.BorderWidth, alert.ColorLabel, alert.Type, alert.Label,alert.Name);
        }

        /// <summary>
        /// draw alert lines on  chart
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

            RePaintRightLebels();
        }

        /// <summary>
        /// draw a line on a series
        /// прорисовать линию на серии
        /// </summary>
        public void PaintOneLine(Series mySeries, List<Candle> candles, ChartAlertLine line, Color colorLine, 
            int borderWidth, Color colorLabel, string label)
        {
            // 1 set to a series of settings
            // 1 устанавливаем на серию настройки

            mySeries.Color = colorLine;
            mySeries.ChartType = SeriesChartType.Line;
            mySeries.YAxisType = AxisType.Secondary;
            mySeries.BorderWidth = borderWidth;

            mySeries.LabelForeColor = colorLabel;
            mySeries.Label += " " + label;
            // 2 looking for an index of our points
            // 2 ищем индекс наших точек

            int x1 = 0;
            int x2 = candles.Count - 1;
            decimal valueFirst = GetFirstLineAlertPointY(candles, line);
            decimal valieSecond = GetSecondLineAlertPointY(candles, line);

            mySeries.Points.AddXY(x1, valueFirst);
            mySeries.Points.AddXY(x2, valieSecond);
        }

        private decimal GetFirstLineAlertPointY(List<Candle> candles, ChartAlertLine line)
        {
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
                return 0;
            }
            // 3 find out what kind of movement our candle line goes through.
            // 3 находим, какое движение проходит наша линия за свечку

            decimal stepCorner; //how long our line goes by candle// сколько наша линия проходит за свечку
            stepCorner = (line.ValueSecondPoint - line.ValueFirstPoint) / (secondPointIndex - firstPointIndex);


            decimal valueFirst = (firstPointIndex * -stepCorner) + line.ValueFirstPoint;

            return Math.Round(valueFirst,13);
        }

        private decimal GetSecondLineAlertPointY(List<Candle> candles, ChartAlertLine line)
        {
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
                return 0;
            }
            // 3 find out what kind of movement our candle line goes through.
            // 3 находим, какое движение проходит наша линия за свечку

            decimal stepCorner; //how long our line goes by candle// сколько наша линия проходит за свечку
            //stepCorner = (_secondPoint - _firstPoint) / (_numberCandleSecond - _numberCandleFirst);

            stepCorner = (line.ValueSecondPoint - line.ValueFirstPoint) / (secondPointIndex - firstPointIndex);

            decimal valieSecond = ((candles.Count - firstPointIndex) * stepCorner) + line.ValueFirstPoint;

            return Math.Round(valieSecond, 13);
        }

        // Indicators  ИНДИКАТОРЫ

        /// <summary>
        /// draw an indicator on chart
        /// прорисовать индикатор на графике
        /// </summary>
        /// <param name="indicator">indicator/индикатор</param>
        private void PaintIndicator(IIndicator indicator)
        {
            if (_mouseDown == true)
            {
                return;
            }
            try
            {
                if (string.IsNullOrWhiteSpace(indicator.NameSeries) &&
                    indicator.ValuesToChart != null)
                {
                    return;
                }
                if (_chart == null)
                {
                    return;
                }

                if (_isPaint == false)
                {
                    return;
                }

                if (_chart.InvokeRequired)
                {
                    _chart.Invoke(new Action<IIndicator>(PaintIndicator), indicator);
                    return;
                }

                if (_isPaint == false)
                {
                    return;
                }

                if (indicator.ValuesToChart != null)
                {
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

                    if (indicator.TypeIndicator == IndicatorChartPaintType.Line)
                    {
                        List<List<decimal>> valList = indicator.ValuesToChart;
                        List<Color> colors = indicator.Colors;
                        string name = indicator.Name;

                        for (int i = 0; i < valList.Count; i++)
                        {
                            PaintLikeLine(valList[i], colors[i], name + i,false);
                        }
                    }
                    if (indicator.TypeIndicator == IndicatorChartPaintType.Column)
                    {
                        List<List<decimal>> valList = indicator.ValuesToChart;
                        List<Color> colors = indicator.Colors;
                        string name = indicator.Name;

                        PaintLikeColumn(valList[0], colors[0], colors[1], name + 0,false);
                    }
                    if (indicator.TypeIndicator == IndicatorChartPaintType.Point)
                    {
                        List<List<decimal>> valList = indicator.ValuesToChart;
                        List<Color> colors = indicator.Colors;
                        string name = indicator.Name;

                        for (int i = 0; i < valList.Count; i++)
                        {
                            PaintLikePoint(valList[i], colors[i], name + i, false);
                        }
                    }
                    if (indicator.TypeIndicator == IndicatorChartPaintType.Candle)
                    {
                        List<List<decimal>> valList = indicator.ValuesToChart;
                        List<Color> colors = indicator.Colors;
                        string name = indicator.Name;

                        for (int i = 0; i < valList.Count; i++)
                        {
                            PaintLikeCandle(valList[i], colors[i], name + i, false);
                        }
                    }
                }
                else
                {
                    Aindicator ind = (Aindicator)indicator;
                    List<IndicatorDataSeries> series = ind.DataSeries;

                    for (int i = 0; series != null && i < series.Count; i++)
                    {
                        if (series[i].IsPaint == false)
                        {
                            continue;
                        }
                        if (series[i].ChartPaintType == IndicatorChartPaintType.Line)
                        {
                            PaintLikeLine(
                                series[i].Values, series[i].Color, indicator.Name + i,series[i].CanReBuildHistoricalValues);
                        }
                        else if (series[i].ChartPaintType == IndicatorChartPaintType.Column)
                        {
                            PaintLikeColumn(
                                series[i].Values, series[i].Color, series[i].Color, indicator.Name + i, series[i].CanReBuildHistoricalValues);
                        }
                        else if (series[i].ChartPaintType == IndicatorChartPaintType.Point)
                        {
                            PaintLikePoint(series[i].Values, series[i].Color, indicator.Name + i, series[i].CanReBuildHistoricalValues);
                        }
                        if (series[i].ChartPaintType == IndicatorChartPaintType.Candle)
                        {
                            PaintLikeCandle(series[i].Values, series[i].Color, indicator.Name + i, series[i].CanReBuildHistoricalValues);
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
        /// Add an indicator to the drawing
        /// добавить индикатор в прорисовку
        /// </summary>
        /// <param name="indicator">indicator/индикатор</param>
        public void ProcessIndicator(IIndicator indicator)
        {
            if ((_startProgram == StartProgram.IsTester
                 ||
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
                if (_indicatorsToPaint != null &&
                    _indicatorsToPaint.IsEmpty == false
                    &&
                    _indicatorsToPaint.Count > 25)
                {
                    IIndicator res;

                    while (_indicatorsToPaint.IsEmpty == false &&
                           _indicatorsToPaint.Count > 25)

                        _indicatorsToPaint.TryDequeue(out res);
                }

                if(_indicatorsToPaint != null)
                {
                    _indicatorsToPaint.Enqueue(indicator);
                }
            }
        }

        /// <summary>
        /// forcefully redrawn indicator on chart from beginning to end
        /// принудительно перерисоват индикатор на графике от начала до конца
        /// </summary>
        /// <param name="indicatorCandle">indicator/индикатор</param>
        public void RePaintIndicator(IIndicator indicatorCandle)
        {
            if (_mouseDown == true)
            {
                return;
            }

            if (_chart == null)
            {
                return;
            }

            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action<IIndicator>(RePaintIndicator), indicatorCandle);
                return;
            }

            try
            {
                if (_myCandles == null)
                {
                    return;
                }

                for (int i = 0; i < 150; i++)
                {
                    Series mySeries = FindSeriesByNameSafe(indicatorCandle.Name + i);
                    if (mySeries != null)
                    {
                        mySeries.Points.ClearFast();
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
        /// clear data series
        /// очистить серию данных
        /// </summary>
        /// <param name="series">series name/имя серии</param>
        private void ClearIndicatorSeries(Series series)
        {
            try
            {
                if (_chart == null)
                {
                    return;
                }
                if (_chart.InvokeRequired)
                {
                    _chart.Invoke(new Action<Series>(ClearIndicatorSeries), series);
                    return;
                }
                series.Points.ClearFast();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        // Point indicator Индикатор точки

        /// <summary>
        /// Draw an indicator as a point
        /// прорисовать индикатор как точки
        /// </summary>
        private void PaintLikePoint(List<decimal> values, Color color, string nameSeries, bool fullReloadOnNewCandle)
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

            bool isMyValues = mySeries.Points.Count != 0 && Convert.ToDouble(values[0]) == mySeries.Points[0].YValues[0];

            if (mySeries.Points.Count != 0 &&
               values.Count - 1 == mySeries.Points.Count
               && isMyValues)
            {
                // if draw only last point
                // если прорисовываем только последнюю точку
                PaintLikePointLast(values, nameSeries);
            }
            else if (mySeries.Points.Count != 0 &&
                values.Count == mySeries.Points.Count &&
                fullReloadOnNewCandle == false
                && isMyValues)
            {
                // redrawing the last point
                // перерисовываем последнюю точку
                RePaintLikePointLast(values, nameSeries);
            }
            else
            {
                // if we need to completely,so redraw whole indicator
                // если надо полностью перерисовываем весь индикатор

                List<decimal> array = values;

                Series series = new Series(nameSeries);
                series.ChartType = SeriesChartType.Point;
                series.MarkerStyle = MarkerStyle.Circle;
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

                        //series.Points[series.Points.Count - 1].ToolTip = array[i].ToString();
                    }
                }

                PaintSeriesSafe(series);

                if (myArea.Name != "Prime"
                    && myArea.AlignWithChartArea == "Prime")
                {
                    ChartArea primeArea = FindAreaByNameSafe(myArea.AlignWithChartArea);

                    if (primeArea != null &&
                        primeArea.AxisX.ScaleView != null &&
                        primeArea.AxisX.ScrollBar != null)
                    {
                        if (myArea.AxisX.ScaleView.Position != primeArea.AxisX.ScaleView.Position)
                        {
                            myArea.AxisX.ScaleView.Position = primeArea.AxisX.ScaleView.Position;
                            ResizeXAxis();
                            for (int i = 0; i < _chart.ChartAreas.Count; i++)
                            {
                                ResizeYAxisOnArea(_chart.ChartAreas[i].Name);
                            }
                        }
                    }
                }

                ReloadAreaSizes();
                ResizeSeriesLabels();
            }
            ResizeYAxisOnArea(myArea.Name);
        }

        /// <summary>
        /// draw last points
        ///  прорисовать последние точки
        /// </summary>
        private void PaintLikePointLast(List<decimal> values, string nameSeries)
        {
            Series mySeries = FindSeriesByNameSafe(nameSeries);

            decimal lastPoint = values[values.Count - 1];
            mySeries.Points.AddXY(values.Count - 1, lastPoint);
            //mySeries.Points[mySeries.Points.Count - 1].ToolTip = lastPoint.ToString();
        }

        /// <summary>
        /// redraw last point
        ///  перерисовать последнюю точку
        /// </summary>
        private void RePaintLikePointLast(List<decimal> values, string nameSeries)
        {
            Series mySeries = FindSeriesByNameSafe(nameSeries);
            decimal point2 = Convert.ToDecimal(values[values.Count - 1]);
            mySeries.Points[mySeries.Points.Count - 1].YValues = new[] { Convert.ToDouble(point2) };
            // mySeries.Points[mySeries.Points.Count - 1].ToolTip = point2.ToString();
        }

        // Line indicator Индикатор линия

        /// <summary>
        /// draw indicator as a line
        /// прорисовать индикатор как линию
        /// </summary>
        private void PaintLikeLine(List<decimal> values, Color color, string nameSeries,bool fullReloadOnNewCandle)
        {
            if (values == null ||
                values.Count == 0)
            {
                Series needClearSeries = FindSeriesByNameSafe(nameSeries);
                needClearSeries?.Points.ClearFast();
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

            bool isMyValues = mySeries.Points.Count != 0 && Convert.ToDouble(values[0]) == mySeries.Points[0].YValues[0];

            if (mySeries.Points.Count != 0 &&
                values.Count - 1 == mySeries.Points.Count &&
                fullReloadOnNewCandle == false &&
                isMyValues)
            {
                // if only draw last point
                // если прорисовываем только последнюю точку
                PaintLikeLineLast(values, nameSeries, color);
            }
            else if (mySeries.Points.Count != 0 &&
                values.Count == mySeries.Points.Count &&
                isMyValues)
            {
                // redraw last point
                // перерисовываем последнюю точку
                RePaintLikeLineLast(values, nameSeries, color);
            }
            else
            {
                // if it's needed completely redraw whole indicator
                // если надо полностью перерисовываем весь индикатор

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

                bool isStarted = false;

                for (int i = 0; i < array.Count; i++)
                {
                    // series.Points.AddXY(i, array[i]);
                    var point = new DataPoint(i, (double)array[i]);

                    if (array[i] == 0 && isStarted == false) 
                    {
                        point.IsEmpty = true;
                    }
                    else
                    {
                        isStarted = true;
                    }

                    series.Points.Add(point);
                }

                PaintSeriesSafe(series);

                if (myArea.Name != "Prime"
                    && myArea.AlignWithChartArea == "Prime")
                {
                    ChartArea primeArea = FindAreaByNameSafe(myArea.AlignWithChartArea);

                    if (primeArea != null &&
                        primeArea.AxisX.ScaleView != null &&
                        primeArea.AxisX.ScrollBar != null)
                    {
                        if (myArea.AxisX.ScaleView.Position != primeArea.AxisX.ScaleView.Position)
                        {
                            myArea.AxisX.ScaleView.Position = primeArea.AxisX.ScaleView.Position;
                            ResizeXAxis();
                            for (int i = 0; i < _chart.ChartAreas.Count; i++)
                            {
                                ResizeYAxisOnArea(_chart.ChartAreas[i].Name);
                            }
                        }
                    }
                }

                ReloadAreaSizes();
                ResizeSeriesLabels();
            }
            ResizeYAxisOnArea(myArea.Name);
        }

        /// <summary>
        /// draw indicator as a line, last element
        /// прорисовать индикатор как линию, последний элемент
        /// </summary>
        private void PaintLikeLineLast(List<decimal> values, string nameSeries, Color color)
        {
            Series mySeries = FindSeriesByNameSafe(nameSeries);
            mySeries.Color = color;
            decimal lastPoint = values[values.Count - 1];

            // mySeries.Points.AddXY(mySeries.Points.Count, lastPoint);

            var point = new DataPoint(mySeries.Points.Count, (double)lastPoint);

            if (lastPoint == 0 && values.FindIndex(v => v != 0) == -1)
            {
                point.IsEmpty = true;
            }
            mySeries.Points.Add(point);

            RePaintRightLebels();
        }

        /// <summary>
        /// redraw indicator as a line,last element
        /// перерисовать индикатор как линию, последний элемент
        /// </summary>
        private void RePaintLikeLineLast(List<decimal> values, string nameSeries, Color color)
        {
            Series mySeries = FindSeriesByNameSafe(nameSeries);
            mySeries.Color = color;
            decimal lastPoint = Convert.ToDecimal(values[values.Count - 1]);
            mySeries.Points[mySeries.Points.Count - 1].YValues = new[] { Convert.ToDouble(lastPoint) };

            if (lastPoint == 0 && values.FindIndex(v => v != 0) == -1)
            {
                mySeries.Points[mySeries.Points.Count - 1].IsEmpty = true;
            }
            else
            {
                mySeries.Points[mySeries.Points.Count - 1].IsEmpty = false;
            }

            RePaintRightLebels();
        }

        // Column indicator  Индикатор столбец

        /// <summary>
        /// draw as columns
        /// прорисовать как столбцы
        /// </summary>
        private void PaintLikeColumn(List<decimal> values, Color colorUp, Color colorDown, string nameSeries, bool fullReloadOnNewCandle)
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

            bool isMyValues = mySeries.Points.Count != 0 && Convert.ToDouble(values[0]) == mySeries.Points[0].YValues[0];

            if (mySeries.Points.Count != 0 &&
                values.Count - 1 == mySeries.Points.Count
                && isMyValues)
            {
                // if only draw last point
                // если прорисовываем только последнюю точку
                PaintLikeColumnLast(values, nameSeries, colorUp, colorDown);
            }
            else if (mySeries.Points.Count != 0 &&
                values.Count == mySeries.Points.Count &&
                fullReloadOnNewCandle == false 
                && isMyValues)
            {
                // redrawing last point
                // перерисовываем последнюю точку
                RePaintLikeColumnLast(values, nameSeries, colorUp, colorDown);
            }
            else
            {
                // if it needed completely redraw whole indicator
                // если надо полностью перерисовываем весь индикатор

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
                        //series.Points[series.Points.Count - 1].ToolTip = array[i].ToString();
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

                if (myArea.Name != "Prime"
                    && myArea.AlignWithChartArea == "Prime")
                {
                    ChartArea primeArea = FindAreaByNameSafe(myArea.AlignWithChartArea);

                    if (primeArea != null &&
                        primeArea.AxisX.ScaleView != null &&
                        primeArea.AxisX.ScrollBar != null)
                    {
                        if (myArea.AxisX.ScaleView.Position != primeArea.AxisX.ScaleView.Position)
                        {
                            myArea.AxisX.ScaleView.Position = primeArea.AxisX.ScaleView.Position;
                            ResizeXAxis();
                            for (int i = 0; i < _chart.ChartAreas.Count; i++)
                            {
                                ResizeYAxisOnArea(_chart.ChartAreas[i].Name);
                            }
                        }
                    }
                }

                ReloadAreaSizes();
                ResizeSeriesLabels();
            }
            ResizeYAxisOnArea(myArea.Name);
        }

        /// <summary>
        /// draw indicator as two lines,last element
        /// прорисовать индикатор как две линии, последний элемент
        /// </summary>
        private void PaintLikeColumnLast(List<decimal> values, string nameSeries, Color colorUp, Color colorDown)
        {

            Series mySeriesUp = FindSeriesByNameSafe(nameSeries);
            decimal lastPointUp = values[values.Count - 1];
            mySeriesUp.Points.AddXY(mySeriesUp.Points.Count, lastPointUp);
            //mySeriesUp.Points[mySeriesUp.Points.Count - 1].ToolTip = lastPointUp.ToString();

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
        /// draw indicator as two lines, last element
        /// прорисовать индикатор как две линии, последний элемент
        /// </summary>
        private void RePaintLikeColumnLast(List<decimal> values, string nameSeries, Color colorUp, Color colorDown)
        {
            Series mySeriesUp = FindSeriesByNameSafe(nameSeries);
            decimal lastPoint = Convert.ToDecimal(values[values.Count - 1]);
            mySeriesUp.Points[mySeriesUp.Points.Count - 1].YValues = new[] { Convert.ToDouble(lastPoint) };
            // mySeriesUp.Points[mySeriesUp.Points.Count - 1].ToolTip = lastPoint.ToString();

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
        /// Хранит последние значения для каждой серии (например, последнее значение Close для свечи).
        /// Используется для обновления последней точки графика без полной перерисовки. 
        private Dictionary<string, double> _lastValues = new Dictionary<string, double>();


        /// <summary>
        /// Определяет тип значения (High, Low, Close) по последней цифре имени серии.
        /// </summary>
        /// <param name="nameSeries">Имя серии, оканчивающееся на 0 (High), 1 (Low) или 2 (Close)</param>
        /// <returns>Индекс типа значения: 0 - High, 1 - Low, 2 - Close, иначе -1</returns>
        private int GetValueIndex(string nameSeries)
        {
            if (nameSeries.EndsWith("0")) return 0;  // High
            if (nameSeries.EndsWith("1")) return 1;  // Low
            if (nameSeries.EndsWith("2")) return 2;  // Close
            return -1;
        }

        /// <summary>
        /// Основной метод отрисовки данных как свечей. Поддерживает добавление новой свечи, перерисовку или полное обновление.
        /// </summary>
        /// <param name="values">Список значений для отображения</param>
        /// <param name="color">Цвет свечей</param>
        /// <param name="nameSeries">Имя серии, определяющее тип (High/Low/Close)</param>
        /// <param name="fullReloadOnNewCandle">Флаг, требующий полной перерисовки при добавлении новой свечи</param>
        private void PaintLikeCandle(List<decimal> values, Color color, string nameSeries, bool fullReloadOnNewCandle)
        {
            if (values == null || values.Count == 0)
                return;

            var seriesExist = FindSeriesByNameSafe(nameSeries);
            var area = FindAreaByNameSafe(seriesExist?.ChartArea);
            if (seriesExist == null || area == null)
                return;

            bool sameStart = seriesExist.Points.Count > 0
                          && Convert.ToDouble(values[0]) == seriesExist.Points[0].YValues[0];
            bool newPoint = sameStart && values.Count - 1 == seriesExist.Points.Count;
            bool repaint = sameStart && values.Count == seriesExist.Points.Count && !fullReloadOnNewCandle;

            if (newPoint)
                PaintLikeCandleLast(values, nameSeries, color);
            else if (repaint)
                RePaintLikeCandleLast(values, nameSeries, color);
            else
            {
                var series = new Series(nameSeries)
                {
                    ChartType = SeriesChartType.Candlestick,
                    YAxisType = AxisType.Secondary,
                    ChartArea = area.Name,
                    YValuesPerPoint = 4,
                    Color = color,
                    BackSecondaryColor = color,
                    BorderColor = color,
                    YValueMembers = "High,Low,Open,Close"
                };

                for (int i = 0; i < values.Count; i++)
                    AddDataPoint(series, i, values[i], nameSeries, color);

                PaintSeriesSafe(series);

                if (area.Name != "Prime" && area.AlignWithChartArea == "Prime")
                {
                    var primeArea = FindAreaByNameSafe(area.AlignWithChartArea);
                    if (primeArea?.AxisX.ScaleView != null)
                    {
                        area.AxisX.ScaleView.Position = primeArea.AxisX.ScaleView.Position;
                        ResizeXAxis();
                        foreach (var ca in _chart.ChartAreas)
                            ResizeYAxisOnArea(ca.Name);
                    }
                }
                ReloadAreaSizes();
                ResizeSeriesLabels();
            }
            ResizeYAxisOnArea(area.Name);
        }

        /// <summary>
        /// Добавляет новую точку в существующую серию свечей.
        /// </summary>
        /// <param name="values">Список значений</param>
        /// <param name="nameSeries">Имя серии</param>
        /// <param name="color">Цвет свечи</param>
        private void PaintLikeCandleLast(List<decimal> values, string nameSeries, Color color)
        {
            var seriesExist = FindSeriesByNameSafe(nameSeries);
            if (seriesExist == null || values.Count == 0)
                return;

            int i = values.Count - 1;
            AddDataPoint(seriesExist, i, values[i], nameSeries, color);
            _lastValues[nameSeries] = Convert.ToDouble(values[i]);
        }

        /// <summary>
        /// Обновляет последнюю точку в существующей серии свечей.
        /// </summary>
        /// <param name="values">Список значений</param>
        /// <param name="nameSeries">Имя серии</param>
        /// <param name="color">Цвет свечи</param>
        private void RePaintLikeCandleLast(List<decimal> values, string nameSeries, Color color)
        {
            var seriesExist = FindSeriesByNameSafe(nameSeries);
            if (seriesExist == null || seriesExist.Points.Count == 0)
                return;

            int i = values.Count - 1;
            FillDataPoint(seriesExist.Points[i], values[i], nameSeries, color);
            _lastValues[nameSeries] = Convert.ToDouble(values[i]);
        }

        /// <summary>
        /// Добавляет новую точку данных в серию.
        /// </summary>
        /// <param name="series">Серия, в которую добавляется точка</param>
        /// <param name="x">X-координата (номер свечи)</param>
        /// <param name="rawValue">Значение для отображения</param>
        /// <param name="nameSeries">Имя серии</param>
        /// <param name="color">Цвет точки</param>
        private void AddDataPoint(Series series, int x, decimal rawValue, string nameSeries, Color color)
        {
            var dp = new DataPoint { XValue = x };
            FillDataPoint(dp, rawValue, nameSeries, color);
            series.Points.Add(dp);
        }

        /// <summary>
        /// Заполняет данные точки графика в зависимости от типа (High, Low, Close).
        /// Формирует структуру свечи и настраивает внешний вид точки.
        /// </summary>
        /// <param name="dp">Точка данных</param>
        /// <param name="rawValue">Исходное значение</param>
        /// <param name="nameSeries">Имя серии, определяющее тип значения</param>
        /// <param name="color">Цвет точки</param>
        private void FillDataPoint(DataPoint dp, decimal rawValue, string nameSeries, Color color)
        {
            double v = Convert.ToDouble(rawValue);
            int idx = GetValueIndex(nameSeries);

            double high = 0, low = 0, open = 0, close = 0;
            bool body = false;

            if (idx == 0)
            {
                if (v > 0) { open = 0; close = v; body = false; }
                else { high = 0; low = v; }
            }
            else if (idx == 1)
            {
                if (v < 0) { open = 0; close = v; body = false; }
                else { high = v; low = 0; }
            }
            else if (idx == 2)
            {
                body = true;
                if (v > 0) { open = 0; close = v; }
                else { open = v; close = 0; }
            }

            dp.YValues = new[] { high, low, open, close };

            if (body)
            {
                dp.Color = Color.FromArgb(51, color);
                dp.BackSecondaryColor = dp.Color;
                dp.BorderColor = Color.FromArgb(220, color);
                dp.BorderWidth = 1;
            }
            else
            {
                dp.Color = Color.Transparent;
                dp.BackSecondaryColor = dp.Color;
                dp.BorderColor = Color.FromArgb(170, color);
                dp.BorderWidth = 2;
            }
        }

        /// <summary>
        /// add a series of data to chart safely
        /// добавить серию данных на чарт безопасно
        /// </summary>
        /// <param name="series">data series/серия данных</param>
        private void PaintSeriesSafe(Series series)
        {
            try
            {
                if (_chart == null)
                {
                    return;
                }
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
                            try
                            {
                                _chart.Series.Remove(FindSeriesByNameSafe(series.Name));
                                _chart.Series.Insert(i, series);
                                break;
                            }
                            catch
                            {
                                // ignore
                            }
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
        /// to remove a series of data from chart safely
        /// удалить серию данных с чарта безопасно
        /// </summary>
        /// <param name="series">data series/серия данных</param>
        private void ReMoveSeriesSafe(Series series)
        {
            try
            {
                if (_chart == null)
                {
                    return;
                }
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
        /// to find a series of data by name safely
        /// найти серию данных по имени безопасно
        /// </summary>
        /// <param name="name">series name/имя серии</param>
        /// <returns>a series of data. If not - null/серия данных. Если такой нет, то null</returns>
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
        /// Find data area by name safely
        /// найти область данных по имени безопасно
        /// </summary>
        /// <param name="name">area name/имя области</param>
        /// <returns>data area. If there isn't one, then - null/область данных. Если такой нет, то null</returns>
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

        // Patterns  Паттерны

        public bool IsPatternChart { get; set; }

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
            if (_chart == null)
            {
                return;
            }
            if (_chart.InvokeRequired)
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
                CreateSeries(indicator.NameArea, indicator.TypeIndicator, indicator.NameSeries + "0");
            }

            PaintIndicator(indicator);

            ResizeYAxisOnArea("Prime");
        }

        public void PaintInDifColor(int indexStart, int indexEnd, string seriesName)
        {
            if (_chart == null)
            {
                return;
            }
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

        // Crosshair disappearing control on chart управление исчезанием перекрестия на графике

        private void _chart_MouseLeave(object sender, EventArgs e)
        {
            for (int i = 0; i < _chart.ChartAreas.Count; i++)
            {
                _chart.ChartAreas[i].CursorX.Position = double.NaN;
                _chart.ChartAreas[i].CursorY.Position = 0;
            }
        }

        // chart transition переход по чарту

        /// <summary>
        /// move chart to a specified time
        /// переместить чарт к заданному времени
        /// </summary>
        /// <param name="time">time/время</param>
        public void GoChartToTime(DateTime time)
        {
            try
            {
                if (_myCandles == null ||
                _myCandles.Count < 50)
                {
                    return;
                }

                int candleIndex = GetTimeIndex(time, -1);

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
        /// move chart to a specified time
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

        // working with cursor and highlight candles on chart работа с курсором и выделенем свечек на чарте

        void _chart_Click(object sender, EventArgs e)
        {
            try
            {
                if (IsPatternChart)
                {
                    return;
                }

                if (_chart.Cursor == Cursors.SizeAll)
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
                    else if (series[i].ChartTypeName == "Candlestick")
                    {
                        double[] yVals = series[i].Points[index].YValues;

                        // Найти первое значение, которое ≠ 0
                        int foundIndex = Array.FindIndex(yVals, val => val != 0);

                        if (foundIndex >= 0)
                        {
                            labelSeries.Points[0].Label = ((decimal)yVals[foundIndex]).ToString();
                            labelSeries.Points[0].Color = series[i].Points[index].BorderColor;
                            labelSeries.Points[0].LabelForeColor = series[i].Points[index].BorderColor;
                        }
                        else
                        {
                            labelSeries.Points[0].Label = "";
                        }
                    }
                    else
                    {
                        labelSeries.Points[0].Label = ((decimal)series[i].Points[index].YValues[0]).ToString();
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
        /// draws the cursor value on the Y axis
        /// прорисовывает на оси Y значение курсора
        /// </summary>
        private void RePaintCursor()
        {
            if(_myCandles == null)
            {
                return;
            }
            // looking for area where cursor is now
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

        // Drawing horizontal lines on a chart прорисовка горизонтальных линий на графике

        /// <summary>
        /// to draw an inscription on Y2 axis
        /// прорисовать надпись на оси Y2
        /// </summary>
        /// <param name="nameArea">area name/название области</param>
        /// <param name="label">signature/подпись</param>
        /// <param name="positionOnY">Line position/позиция линии</param>
        /// <param name="color">signature color/цвет подписи</param>
        /// <param name="isPrime">if the signature is a signature that crosses the other/является ли подпись подписью перекрвающий другие</param>
        private void PaintLabelOnY2(string name, string nameArea, string label, decimal positionOnY, Color color, bool isPrime)
        {
            if (_myCandles == null)
            {
                return;
            }
            if (_chart == null)
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

            try
            {
                label = label.ToDecimal().ToString(CultureInfo.InvariantCulture);
            }
            catch
            {
                return;
            }

            if (_labels == null)
            {
                _labels = new List<ChartYLabels>();
            }
            if (area.AxisY2.CustomLabels != null)
            {
                // delete the label if it was already on schedule.
                // удаляем лэйбл если он уже был на графике
                ChartYLabels oldlabel = _labels.Find(labels => labels.AreaName == nameArea && labels.SeriesName == name);

                if (oldlabel != null)
                {
                    // found an old series
                    // нашли старую серию
                    string positon = oldlabel.Price;
                    for (int i = 0; i < area.AxisY2.CustomLabels.Count; i++)
                    {
                        if (area.AxisY2.CustomLabels[i] == null)
                        {
                            try
                            {
                                area.AxisY2.CustomLabels[i] = new CustomLabel(min, max, label, 0, LabelMarkStyle.LineSideMark);
                                area.AxisY2.CustomLabels.RemoveAt(i);
                                i--;
                                continue;
                            }
                            catch
                            {
                                continue;
                                // ignore
                            }
                        }

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
            if (_chart == null)
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

            string positon = oldlabel.Price.ToDecimal().ToString(CultureInfo.InvariantCulture);
            for (int i = 0; i < area.AxisY2.CustomLabels.Count; i++)
            {
                if(area.AxisY2.CustomLabels[i] == null)
                {
                    continue;
                }

                if (area.AxisY2.CustomLabels[i].Text == positon && area.AxisY2.CustomLabels[i].ForeColor == color)
                {
                    area.AxisY2.CustomLabels.Remove(area.AxisY2.CustomLabels[i]);
                    return;
                }
            }
        }

        /// <summary>
        /// all signatures on chart
        /// все подписи на графике
        /// </summary>
        private List<ChartYLabels> _labels;

        /// <summary>
        /// Sign standard horizontal lines on the area
        /// подписать стандартные горизонтальные линии на области
        /// </summary>
        private void RePaintPrimeLines(string areaName)
        {
            if (IsPatternChart)
            {
                return;
            }
            if (_chart == null)
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
        /// to check labels on Y-axis for overlapping labels. And hide lines if necessary
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
            {
                // looking for labels marked IsPrime. Underneath them we need to clear everything
                // ищем лэйблы с пометкой IsPrime. Под ними надо всё расчистить
                if (_labels[indLabel].IsPrime == false)
                {
                    continue;
                }

                ChartYLabels label = _labels[indLabel];

                if (label.AreaName != nameArea)
                {
                    continue;
                }

                double max = label.Price.ToDouble() + area.AxisY2.Interval * 0.2;
                double min = label.Price.ToDouble() - area.AxisY2.Interval * 0.2;

                for (int indSecond = 0; indSecond < _labels.Count; indSecond++)
                {
                    ChartYLabels curLabel = null; 

                    try
                    {
                        curLabel = _labels[indSecond];
                    }
                    catch
                    {
                        // ignore
                        continue;
                    }

                    if(curLabel == null)
                    {
                        continue;
                    }

                    if (indLabel == indSecond)
                    {
                        continue;
                    }

                    if (curLabel.IsPrime)
                    {
                        // If two primes are on each other, leave it
                        // если два прайма друг на дружке, то оставляем
                        continue;
                    }


                    if (curLabel.AreaName != label.AreaName)
                    {
                        continue;
                    }

                    double price = curLabel.Price.ToDouble();
                    if (price < max &&
                        price > min)
                    {
                        // found intersection
                        // нашли пересечение
                        ClearLabelOnY2(curLabel.SeriesName, nameArea, curLabel.Color);
                        indSecond--;
                    }
                }
            }
        }

        /// <summary>
        /// redraw digits of indicator display on right axis
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
                index = (int)(_chart.ChartAreas[0].AxisX.ScaleView.Position +_chart.ChartAreas[0].AxisX.ScaleView.Size);
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
                        series.ChartType == SeriesChartType.Point)
                    {
                        continue;
                    }

                    int realIndex = index;

                    if (index == series.Points.Count)
                    {
                        realIndex = series.Points.Count - 1;
                    }
                    else
                    {
                        //realIndex = series.Points.Count - (series.Points.Count - 1 - index);
                    }

                    if(realIndex >= series.Points.Count)
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

                    if (series.Name == "SeriesCandle")
                    {
                        PaintLabelOnY2(series.Name + "Label", series.ChartArea,
                            (Math.Round(series.Points[realIndex].YValues[3], rounder)).ToString(_culture),
                            (decimal)series.Points[realIndex].YValues[3], series.Points[realIndex].Color, true);
                    }
                    // RightLabel  Метки для Candle - High, Low, Close [справа] рисуем в цвете
                    else if (series.ChartTypeName == "Candlestick")
                    {
                        double[] yVals = series.Points[realIndex].YValues;
                        double selectedValue = yVals.FirstOrDefault(val => val != 0);

                        decimal closeVal = Convert.ToDecimal(selectedValue);

                        string closeText = closeVal.ToString(_culture);

                        PaintLabelOnY2(series.Name + "Label", series.ChartArea, closeText, closeVal, series.Points[realIndex].BorderColor, true);
                    }
                    else
                    {
                        decimal value = Convert.ToDecimal(series.Points[realIndex].YValues[0]);

                        value = (Math.Round(value, rounder));

                        int valueDecimalsCount = value.ToString().DecimalsCount();
                        string valueToPaint = value.ToString(CultureInfo.InvariantCulture);

                        if (rounder > 0 &&
                            valueDecimalsCount != 0)
                        {
                            while (valueToPaint.Length != 0
                                   && valueToPaint[valueToPaint.Length - 1] == '0')
                            {
                                valueToPaint = valueToPaint.Substring(0,valueToPaint.Length - 1);
                            }
                        }

                        decimal valueToPaintDecimal = value;

                        while (rounder > 0 &&
                            valueDecimalsCount < rounder)
                        {
                            decimal newValue = 0;

                            string stringToParse = value.ToString(CultureInfo.InvariantCulture);

                            if (valueDecimalsCount == 0
                                && stringToParse.Contains(".") == false)
                            {
                                stringToParse += ".1";
                            }
                            else
                            {
                                stringToParse += "1";
                            }

                            try
                            {
                                newValue = stringToParse.ToDecimal();
                            }
                            catch
                            {

                            }

                            value = newValue;

                            if (valueDecimalsCount == 0
                                && stringToParse.Contains(".") == false)
                            {
                                valueToPaint += ",0";
                            }
                            else
                            {
                                valueToPaint += "0";
                            }

                            valueDecimalsCount = value.ToString().DecimalsCount();

                            if(valueToPaint.Length > 30)
                            {
                                break;
                            }
                        }

                        PaintLabelOnY2(series.Name + "Label", series.ChartArea,
                            valueToPaint, valueToPaintDecimal, series.Points[realIndex].Color, true);
                    }
                }
            }
        }
        
        void _chart_AxisViewChanging(object sender, ViewEventArgs e)
        {
            RePaintRightLebels();
        }

        // management of collapsing areas управление схлопывание областей

        /// <summary>
        /// hide all except main
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
        /// show all areas
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
                            {
                                // if the tick chart hasn't passed yet.
                                // если тиковый график ещё не прошли
                                _chart.ChartAreas[i].Position.Height = 15;
                                _chart.ChartAreas[i].Position.Y = _chart.ChartAreas[0].Position.Y + _chart.ChartAreas[0].Position.Height + (i - 1) * 15;
                            }
                            else
                            {
                                // have to take into account height of tick chart
                                // надо учитывать высоту тикового графика
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

        // area height management управление высотой областей

        private List<ChartAreaPosition> _areaPositions;

        private void _chart_MouseMove(object sender, MouseEventArgs e)
        {
            if (IsPatternChart)
            {
                return;
            }

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
                     pos.UpPoint + pos.UpPoint*0.02 > e.Y &&
                     pos.UpPoint - pos.UpPoint*0.02 < e.Y))
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
                if(_chart.Cursor != Cursors.Arrow)
                {
                    _chart.Cursor = Cursors.Arrow;
                }
               
                return;
            }

            if (mouse.Button != MouseButtons.Left)
            {
                myPosition.ValueYMouseOnClickStart = 0;
            }
            // dragging and dropping areas
            // Here we have a left-hand button pressed
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

            double percentMove = Math.Abs(myPosition.ValueYMouseOnClickStart - e.Y)/_host.Child.Height;

            if (double.IsInfinity(percentMove) ||
                percentMove == 0)
            {
                return;
            }

            double concateValue = 100*percentMove;

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

        private void _chart_MouseUp(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < _chart.ChartAreas.Count; i++)
            {
                ResizeYAxisOnArea(_chart.ChartAreas[i].Name);
            }
        }

        private void _chart_MouseMove2(object sender, MouseEventArgs e)
        {
            if (IsPatternChart)
            {
                return;
            }

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

            if (_areaPositions == null)
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

            double mult = pos.DownPoint / 250000;

            if ((pos.LeftPoint < e.X &&
                 pos.RightPoint > e.X &&
                 e.Y > pos.DownPoint - pos.DownPoint * 0.05 &&
                 e.Y < pos.DownPoint - pos.DownPoint * (0.002 + mult))
                 && e.X > 20    //AVP ChartRoulette 
                ||
                (mouse.Button == MouseButtons.Left && _chart.Cursor == Cursors.SizeWE && pos.LeftPoint < e.X &&
                 pos.RightPoint > e.X &&
                 pos.DownPoint - 50 < e.Y &&
                 pos.DownPoint + 50 > e.Y))
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
                if (_chart.Cursor != Cursors.Arrow)
                {
                    _chart.Cursor = Cursors.Arrow;
                }
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


            double percentMove = Math.Abs(myPosition.ValueXMouseOnClickStart - e.X) / _host.Child.Width;

            if (double.IsInfinity(percentMove) ||
                percentMove == 0)
            {
                return;
            }

            //double concateValue = 100*percentMove*5;


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
                             myPosition.CountXValuesChartOnClickStart*percentMove*3;


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
               myPosition.CountXValuesChartOnClickStart * percentMove * 3;


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
        /// recalculate location of areas in window
        /// пересчитать расположение областей на окне
        /// </summary>
        private void ReloadChartAreasSize()
        {
            if (_areaPositions == null)
            {
                _areaPositions = new List<ChartAreaPosition>();
            }

            for (int i = 0; _chart != null &&
                i < _chart.ChartAreas.Count; i++)
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

        // axis scaling изменение масштабов осей

        private List<ChartAreaSizes> _areaSizes;
        public List<ChartAreaSizes> areaSizes //AVP ChartRoulette 
        {
            get
            {
                return _areaSizes;
            }
        }
        /// <summary>
        /// take current axle multipliers
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

            for (int i = 0; i < candles.Count && i < 1000; i++)
            {
                Candle candleN = candles[i];

                if(candleN == null)
                {
                    continue;
                }

                decimal open = candleN.Open;
                decimal high = candleN.High;
                decimal low = candleN.Low;
                decimal close = candleN.Close;

                if (open == 0 &&
                    high == 0 &&
                    low == 0 &&
                    close == 0)
                {
                    continue;
                }

                string openS = open.ToStringWithNoEndZero();
                string highS = high.ToStringWithNoEndZero();
                string lowS = low.ToStringWithNoEndZero();
                string closeS = close.ToStringWithNoEndZero();

                openS = openS.Replace(".", ",");
                highS = highS.Replace(".", ",");
                lowS = lowS.Replace(".", ",");
                closeS = closeS.Replace(".", ",");

                if (openS.Split(',').Length > 1 ||
                    highS.Split(',').Length > 1 ||
                    lowS.Split(',').Length > 1 ||
                    closeS.Split(',').Length > 1)
                {
                    // if there's a physical part
                    // если имеет место вещественная часть
                    int length = 1;

                    if (openS.Split(',').Length > 1 &&
                        openS.Split(',')[1].Length > length)
                    {
                        length = openS.Split(',')[1].Length;
                    }

                    if (highS.Split(',').Length > 1 &&
                        highS.Split(',')[1].Length > length)
                    {
                        length = highS.Split(',')[1].Length;
                    }

                    if (lowS.Split(',').Length > 1 &&
                        lowS.Split(',')[1].Length > length)
                    {
                        length = lowS.Split(',')[1].Length;
                    }

                    if (closeS.Split(',').Length > 1 &&
                        closeS.Split(',')[1].Length > length)
                    {
                        length = closeS.Split(',')[1].Length;
                    }

                    if (length == 1 && minPriceStep > 0.1m)
                    {
                        minPriceStep = 0.1m;
                    }
                    if (length == 2 && minPriceStep > 0.01m)
                    {
                        minPriceStep = 0.01m;
                    }
                    if (length == 3 && minPriceStep > 0.001m)
                    {
                        minPriceStep = 0.001m;
                    }
                    if (length == 4 && minPriceStep > 0.0001m)
                    {
                        minPriceStep = 0.0001m;
                    }
                    if (length == 5 && minPriceStep > 0.00001m)
                    {
                        minPriceStep = 0.00001m;
                    }
                    if (length == 6 && minPriceStep > 0.000001m)
                    {
                        minPriceStep = 0.000001m;
                    }
                    if (length == 7 && minPriceStep > 0.0000001m)
                    {
                        minPriceStep = 0.0000001m;
                    }
                    if (length == 8 && minPriceStep > 0.00000001m)
                    {
                        minPriceStep = 0.00000001m;
                    }
                    if (length == 9 && minPriceStep > 0.000000001m)
                    {
                        minPriceStep = 0.000000001m;
                    }
                    if (length == 10 && minPriceStep > 0.0000000001m)
                    {
                        minPriceStep = 0.0000000001m;
                    }
                    if (length == 11 && minPriceStep > 0.00000000001m)
                    {
                        minPriceStep = 0.00000000001m;
                    }
                    if (length == 12 && minPriceStep > 0.000000000001m)
                    {
                        minPriceStep = 0.000000000001m;
                    }
                }
                else
                {
                    // if there's no physical part
                    // если вещественной части нет
                    int length = 1;

                    try
                    {
                        for (int i3 = openS.Length - 1; openS[i3] == '0'; i3--)
                        {
                            length = length * 10;
                        }
                    }
                    catch
                    {
                        break;
                    }

                    int lengthLow = 1;

                    for (int i3 = lowS.Length - 1; lowS[i3] == '0'; i3--)
                    {
                        lengthLow = lengthLow * 10;

                        if (length > lengthLow)
                        {
                            length = lengthLow;
                        }
                    }

                    int lengthHigh = 1;

                    for (int i3 = highS.Length - 1; highS[i3] == '0'; i3--)
                    {
                        lengthHigh = lengthHigh * 10;

                        if (length > lengthHigh)
                        {
                            length = lengthHigh;
                        }
                    }

                    int lengthClose = 1;

                    for (int i3 = closeS.Length - 1; closeS[i3] == '0'; i3--)
                    {
                        lengthClose = lengthClose * 10;

                        if (length > lengthClose)
                        {
                            length = lengthClose;
                        }
                    }
                    if (minPriceStep > length)
                    {
                        minPriceStep = length;
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
            // number of decimal places
            // кол-во знаков после запятой

            int countZnak = 0;

            try
            {
                countZnak = minPriceStep.ToStringWithNoEndZero().DecimalsCount();
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
                if (chartSeries[i] == null)
                {
                    List<Series> noNullSeries = new List<Series>();

                    for(int j = 0;j < chartSeries.Count;j++)
                    {
                        if (chartSeries[j] == null)
                        {
                            continue;
                        }
                        else
                        {
                            noNullSeries.Add(chartSeries[j]);
                        }
                    }

                    _chart.Series.Clear();
                    i--;

                    for (int j = 0; j < noNullSeries.Count; j++)
                    {
                        _chart.Series.Add(noNullSeries[j]);
                    }

                    continue;
                }

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
                if (seriesOnArea[serIterator].Points == null ||
                    seriesOnArea[serIterator].Points.Count == 1)
                {
                    continue;
                }

                for (int i = seriesOnArea[serIterator].Points.Count-1; 
                    i > -1 && i > seriesOnArea[serIterator].Points.Count-10; i--)
                {
                    double value = seriesOnArea[serIterator].Points[i].YValues[0];

                    decimal valueInDecimal = value.ToStringWithNoEndZero().ToDecimal();

                    int lengh = valueInDecimal.ToString().DecimalsCount();
                    int lengh2 = seriesOnArea[serIterator].Points[i].YValues[0].ToString(_culture).Split(',')[0].Length;

                    if (lengh2 > 2 && lengh > 3)
                    {
                        lengh = 2;
                        break;
                    }

                    if (lengh > maxDecimal)
                    {
                        maxDecimal = lengh;
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
        /// size of axis X-axis has changed
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
        ///  position of scrollbar's scrolling barrel has changed.
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
        /// Scroll and Zoom(with Ctrl key pressed) chart using MouseWheel 
        /// прокрутка и изменение масштаба(с нажатым Ctrl) чарта с помощью колесика мыши
        /// </summary>
        private void _chart_MouseWheel(object sender, MouseEventArgs e)
        {
            try
            {
                if (_isPaint == false)
                {
                    return;
                }

                if (_myCandles == null)
                {
                    return;
                }

                if (Control.ModifierKeys == Keys.Control)
                {
                    if (e.Delta < 0)
                    {
                        double size = _chart.ChartAreas[0].AxisX.ScaleView.Size + 100 < 2000
                            ? _chart.ChartAreas[0].AxisX.ScaleView.Size + 100
                            : 2000;

                        if (size > _myCandles.Count)
                        {
                            size = _myCandles.Count;
                        }

                        _chart.ChartAreas[0].AxisX.ScaleView.Size = size;

                        if (_chart.ChartAreas[0].AxisX.ScaleView.Position + size > _myCandles.Count)
                        {
                            _chart.ChartAreas[0].AxisX.ScaleView.Position = _chart.ChartAreas[0].AxisX.ScaleView.Position - size;
                        }

                        if (_chart.ChartAreas[0].AxisX.ScaleView.Position < 0)
                        {
                            _chart.ChartAreas[0].AxisX.ScaleView.Position = 0;
                        }
                    }
                    else
                    {
                        double size = _chart.ChartAreas[0].AxisX.ScaleView.Size - 100 > 100
                            ? _chart.ChartAreas[0].AxisX.ScaleView.Size - 100
                            : 100;

                        _chart.ChartAreas[0].AxisX.ScaleView.Size = size;

                        if (_chart.ChartAreas[0].AxisX.ScaleView.Position + size > _myCandles.Count)
                        {
                            _chart.ChartAreas[0].AxisX.ScaleView.Position = _chart.ChartAreas[0].AxisX.ScaleView.Position - size;
                        }
                        if (_chart.ChartAreas[0].AxisX.ScaleView.Position < 0)
                        {
                            _chart.ChartAreas[0].AxisX.ScaleView.Position = 0;
                        }
                    }
                }
                else
                {
                    if (_chart.ChartAreas[0].AxisX.ScaleView.Size == double.NaN)
                    {
                        return;
                    }
                    double size = (int)_chart.ChartAreas[0].AxisX.ScaleView.Size / 4;

                    if (e.Delta > 0)
                    {
                        _chart.ChartAreas[0].AxisX.ScaleView.Position = _chart.ChartAreas[0].AxisX.ScaleView.Position + size
                            < _myCandles.Count - (int)_chart.ChartAreas[0].AxisX.ScaleView.Size
                                ? _chart.ChartAreas[0].AxisX.ScaleView.Position + size
                                : _myCandles.Count - (int)_chart.ChartAreas[0].AxisX.ScaleView.Size;
                    }
                    else
                    {
                        _chart.ChartAreas[0].AxisX.ScaleView.Position = _chart.ChartAreas[0].AxisX.ScaleView.Position - size > 0
                                ? _chart.ChartAreas[0].AxisX.ScaleView.Position - size
                                : 0;
                    }
                }


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
        /// LCM up
        /// поднялась ЛКМ
        /// </summary>
        void _chartForCandle_MouseUp(object sender, MouseEventArgs e) 
        {
            if (IsPatternChart)
            {
                return;
            }

            _mouseDown = false;
            _chart.Cursor = Cursors.Default;
        }

        /// <summary>
        /// LCM down
        /// опустилась ЛКМ
        /// </summary>
        private void _chartForCandle_MouseDown(object sender, MouseEventArgs e)
        {
            if (IsPatternChart)
            {
                return;
            }

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            _mouseDown = true;
            // accept event on the right side of host
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
        /// Y on which clamped LC
        /// Y по которому зажали ЛК
        /// </summary>
        private double _firstY;

        /// <summary>
        /// Y current
        ///  Y текущий
        /// </summary>
        private double _lastY;

        /// <summary>
        /// area we're changing right now.
        /// область которую сейчас изменяем
        /// </summary>
        private string _resizeArea;

        /// <summary>
        /// mouse moved
        /// двинулась мышь
        /// </summary>
        void _chartForCandle_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (IsPatternChart)
                {
                    return;
                }

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
        /// Change Y-axis view of data area
        /// изменить представление оси Y у области данных
        /// </summary>
        /// <param name="areaName">data area name/имя области данных</param>
        private void ResizeYAxisOnArea(string areaName, bool full = false)
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
                if (_chart == null)
                {
                    return;
                }
                if (_chart.InvokeRequired)
                {
                    _chart.Invoke(new Action<string, bool>(ResizeYAxisOnArea), areaName,full);
                    return;
                }

                ChartArea area = FindAreaByNameSafe(areaName);

                if (area == null)
                {
                    return;
                }

                Series candleSeries = FindSeriesByNameSafe("SeriesCandle"); // take a series of candles from chart/берём серию свечек с графика
                ChartArea candleArea = FindAreaByNameSafe("Prime"); //take area for candles from chart/ берём область для свечек с графика

                if (candleArea == null ||
                    candleSeries == null)
                {
                    // if some data isn't available. Let's go out
                    // если какие-то данные не доступны. Выходим
                    return;
                }

                int firstX = 0; //first displayed candle/ первая отображаемая свеча
                int lastX = candleSeries.Points.Count; //last displayed candle/ последняя отображаемая свеча

                if (_chart.ChartAreas[0].AxisX.ScrollBar != null
                    && double.IsNaN(candleArea.AxisX.ScaleView.Position) == false
                    && double.IsNaN(candleArea.AxisX.ScaleView.Size) == false)
                {
                    // If a range has already been selected, assign first and last one based on this range
                    // если уже выбран какой-то диапазон, назначаем первую и последнюю исходя из этого диапазона
                    try
                    {
                        firstX = Convert.ToInt32(candleArea.AxisX.ScaleView.Position);
                        lastX = Convert.ToInt32(candleArea.AxisX.ScaleView.Position) +
                                Convert.ToInt32(candleArea.AxisX.ScaleView.Size) + 1;
                    }
                    catch
                    {
                        return;
                    }
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

                if(LastXIndexChangeEvent != null 
                    && candleArea.AxisX.ScaleView != null
                    && chartSeries.Count > 0 
                    && chartSeries[0].Points != null
                    && double.IsNaN(candleArea.AxisX.ScaleView.Position) == false
                    && double.IsNaN(candleArea.AxisX.ScaleView.Size) == false)
                {
                    int allCandleCount = chartSeries[0].Points.Count;
                    int curPosition = Convert.ToInt32(candleArea.AxisX.ScaleView.Position);
                    int scaleSize = Convert.ToInt32(candleArea.AxisX.ScaleView.Size);

                    int xPositionFromRight =  + allCandleCount - (curPosition + scaleSize);

                    LastXIndexChangeEvent(xPositionFromRight);
                }

                double max = double.MinValue;
                double min = double.MaxValue;

               for (int serIterator = 0; serIterator < seriesOnArea.Count; serIterator++)
               {

                   if(seriesOnArea[serIterator].Name == "Deal_BuySell")
                   {
                       continue;
                   }
                   SeriesYLength yLength = GetSeriesYLength(seriesOnArea[serIterator], firstX, lastX, candleSeries, full);

                   double minOnSeries = yLength.Ymin;

                   if (minOnSeries != double.MaxValue && 
                       minOnSeries < min
                       && minOnSeries != 0)
                   {
                       min = minOnSeries;
                   }

                   double maxOnSeries = yLength.Ymax;

                   if (maxOnSeries != double.MinValue
                        && maxOnSeries > max)
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
                    max == double.MinValue ||
                    max == 0 ||
                    max == min ||
                    max <= min)
                {
                    return;
                }

                area.AxisY2.Maximum = max;
                area.AxisY2.Minimum = min;

                int rounder = areaSize.Decimals + 2;
                if (rounder > 15)
                {
                    rounder = areaSize.Decimals;
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

        private SeriesYLength GetSeriesYLength(Series series, int start, int end, Series candleSeries, bool full)
        {
            SeriesYLength currentLength = null;

            for (int i =0; i < _seriesYLengths.Count;i++)
            {
                if (_seriesYLengths[i] == null)
                {
                    _seriesYLengths.RemoveAt(i);
                    i--;
                    continue;
                }
                if (_seriesYLengths[i].Series.Name == series.Name)
                {
                    currentLength = _seriesYLengths[i];
                    break;
                }
            }

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
                end - start < 400 ||
                full == true)
            {
                // if we have a small display window, we do everything in forehead
                // если у нас малое окно отображения, делаем всё в лоб
                currentLength.Xend = end;
                currentLength.Xstart = start;
                currentLength.Ymax = GetMaxFromSeries(series, start, end, candleSeries);
                currentLength.Ymin = GetMinFromSeries(series, start, end, candleSeries);
                currentLength.CountOneCandlesAdd = 0;
            }
            else if ((currentLength.Xend == end && currentLength.Xstart == start))
            {
                // display hasn't changed
                // отображение не изменилось
                return currentLength;
            }
            else if (currentLength.CountOneCandlesAdd < 20 &&
                    (currentLength.Xend +1 == end && currentLength.Xstart == start))
            {
                // display has shifted one right
                // отображение сдвинулось на один вправо

                int x = currentLength.Xend + 1;

                if (x >= series.Points.Count)
                {
                    x = series.Points.Count - 1;
                }

                double max = series.Points[x].YValues.Max();

                if (max != 0 &&
                    max != double.NaN &&
                    max > currentLength.Ymax)
                {
                    currentLength.Ymax = max;
                }

                double min = series.Points[x].YValues.Min();

                if (min != 0
                    && min != double.NaN
                    && min < currentLength.Ymin)
                {
                    currentLength.Ymin = min;
                }
                currentLength.Xstart = start;
                currentLength.Xend = end;
                currentLength.CountOneCandlesAdd++;
            }
            else if (currentLength.CountOneCandlesAdd < 20 && 
                (currentLength.Xend + 1 == end && currentLength.Xstart + 1 == start))
            {
                // display has shifted one right
                // отображение сдвинулось на один вправо
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
            {
                // need to reboot series again.
                // нужно перезагрузить серию заного
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
                    // series of parallel candles,indicator
                    // серия паралельная свечкам, индикатор
                    double maxCurValue = series.Points[i].YValues.Max();

                    if (maxCurValue > max &&
                        maxCurValue != 0)
                    {
                        max = maxCurValue;
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
                    minOnPoint != 0)
                {
                    min = minOnPoint;
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

            if(min == double.MaxValue)
            {
                return 0;
            }

            return min;
        }

        /// <summary>
        /// redraw lines on the X-axis
        /// перерисовать линии на оси Икс
        /// </summary>
        private void ResizeXAxis()
        {
            if (_chart == null)
            {
                return;
            }
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
                if (area.AxisX.CustomLabels[i] == null)
                {
                    continue;
                }

                area.AxisX.CustomLabels[i].FromPosition = value - area.AxisX.Interval * 0.7;
                area.AxisX.CustomLabels[i].ToPosition = value + area.AxisX.Interval * 0.7;
                area.AxisX.CustomLabels[i].Text = _myCandles[(int)value].TimeStart.ToString(_cultureInterfaces);
                
                value += area.AxisX.Interval;

                if(value >= _myCandles.Count)
                {
                    value = _myCandles.Count - 1;
                }
            }

        }

        /// <summary>
        /// change Y-axis view of the data area where ticks displayed
        /// изменить представление оси Y у области данных на которой отображаются тики
        /// </summary>
        private void ResizeTickArea()
        {
            ChartArea area = FindAreaByNameSafe("TradeArea");

            if (area == null)
            {
                return;
            }
            if (_chart == null)
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
        /// reset zoom in
        /// сбросить увеличение
        /// </summary>
        private void ClearZoom() 
        {
            try
            {
                if (_chart == null)
                {
                    return;
                }
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

        public void MoveChartToTheRight(int scaleSize)
        {
            if ((_startProgram == StartProgram.IsTester) &&
                _host != null)
            {
                MoveChartToTheRightLogic(scaleSize);
            }
            else
            {
                _needMoveChartToTheRight = true;
                _scaleSizeToMoveChartToTheRight = scaleSize;
            }
        }

        private bool _needMoveChartToTheRight;

        private int _scaleSizeToMoveChartToTheRight;

        private void MoveChartToTheRightLogic(int scaleSize)
        {
            if (_chart == null)
            {
                return;
            }
            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action<int>(MoveChartToTheRightLogic), scaleSize);

                return;
            }

            if (_myCandles == null ||
                _myCandles.Count == 0)
            {
                return;
            }
            if (_chart.ChartAreas[0].AxisX.ScrollBar == null)
            {
                return;
            }

            double values = 0;

            if (double.IsNaN(_chart.ChartAreas[0].AxisX.ScaleView.Size))
            {
                values = _myCandles.Count;
            }
            else
            {
                values = (int)_chart.ChartAreas[0].AxisX.ScaleView.Size;
            }

            if (scaleSize != 0 &&
                scaleSize != values)
            {
                ChangeOpenScaleSize(scaleSize);
                values = scaleSize;
            }

            if (values == _myCandles.Count)
            {
                return;
            }

            _chart.ChartAreas[0].AxisX.ScaleView.Position = _myCandles.Count - values;
            ReloadAreaSizes();
            ResizeSeriesLabels();
            RePaintRightLebels();
            ResizeYAxisOnArea("Prime");
            ResizeXAxis();
        }

        public int OpenChartScale 
        {
            get
            {
                
                int value = 0;

                if (double.IsNaN(_chart.ChartAreas[0].AxisX.ScaleView.Size))
                {
                    return 0;
                }
                else
                {
                    value = (int)_chart.ChartAreas[0].AxisX.ScaleView.Size;
                }

                return value;
            }
            set
            {
                if(value < 5)
                {
                    return;
                }
                ChangeOpenScaleSize(value);
            }
        }

        private void ChangeOpenScaleSize(int value)
        {
            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action<int>(ChangeOpenScaleSize), value);
                return;
            }

            if(_chart.ChartAreas[0].AxisX.ScaleView != null &&
                value == _chart.ChartAreas[0].AxisX.ScaleView.Size)
            {
                return;
            }

            if(_chart.ChartAreas[0].AxisX.ScaleView.Size != value)
            {
                Series candlesSeries = _chart.Series[0];

                if(candlesSeries.Points.Count < value)
                {
                    return;
                }

                _chart.ChartAreas[0].AxisX.ScaleView.Size = value;
            }
        }

        public void SetAxisXSize(int size)
        {
            ChangeOpenScaleSize(size);
        }

        public void SetAxisXPositionFromRight(int xPosition)
        {
            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action<int>(SetAxisXPositionFromRight), xPosition);
                return;
            }

            if (_chart.ChartAreas[0].AxisX.ScaleView == null
                || double.IsNaN(_chart.ChartAreas[0].AxisX.ScaleView.Size))
            {
                return;
            }

            Series candleSeries = _chart.Series[0];

            int pointCount = candleSeries.Points.Count;

            int realPos = pointCount - xPosition - Convert.ToInt32(_chart.ChartAreas[0].AxisX.ScaleView.Size);

            if (realPos < 0 
                || candleSeries.Points.Count == 0
                || candleSeries.Points.Count < xPosition)
            {
                return;
            }

            if(_chart.ChartAreas[0].AxisX.ScaleView.Position != realPos)
            {
                _chart.ChartAreas[0].AxisX.ScaleView.Position = realPos;
                ResizeYAxisOnArea("Prime");
                ResizeXAxis();
            }
          
        }

        /// <summary>
        /// there's been a change int X-axis
        /// изменилось представление по оси Х
        /// </summary>
        public event Action<int> SizeAxisXChangeEvent;

        /// <summary>
        /// the distance from the right edge to the location of the view on the X axis has changed
        /// изменилось расстояние от правого края до расположения представления на оси X
        /// </summary>
        public event Action<int> LastXIndexChangeEvent;
    }

    /// <summary>
    /// class allowing to transfer position of main window
    /// класс позволяющий передавать положение главного окна
    /// </summary>
    public class WindowCoordinate
    {
        /// <summary>
        /// left point X at main application window
        /// левая точка Х у главного окна приложения
        /// </summary>
        public static decimal X;

        /// <summary>
        /// top point Y at main application window
        /// верхняя точка Y у главного окна приложения
        /// </summary>
        public static decimal Y;
    }

    /// <summary>
    /// object for storing signatures on Y-axis
    /// объект для хранения подписей на оси Y
    /// </summary>
    public class ChartYLabels
    {
        /// <summary>
        /// Name of area
        /// Имя области
        /// </summary>
        public string AreaName;

        /// <summary>
        /// Series Name
        /// Имя серии
        /// </summary>
        public string SeriesName;

        /// <summary>
        /// price
        /// цена
        /// </summary>
        public string Price;

        /// <summary>
        /// whether the line is a line overlapping with other lines
        /// является ли линия линией перекрывающей другие
        /// </summary>
        public bool IsPrime;

        /// <summary>
        /// data series color
        /// цвет серии данных
        /// </summary>
        public Color Color;
    }

    /// <summary>
    /// Object for storing axle settings
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
        /// name
        /// имя
        /// </summary>
        public string Name;

        /// <summary>
        /// accuracy
        /// точность
        /// </summary>
        public int Decimals;

        /// <summary>
        /// height
        /// высота
        /// </summary>
        public int High;

        /// <summary>
        /// content alignment multiplier
        /// мультипликатор для центровки содержимого
        /// </summary>
        public double Multiplier;

        /// <summary>
        /// Number of lines separating the area
        /// кол-во линий разделяющих область
        /// </summary>
        public int LineCount;
    }

    /// <summary>
    /// parameters of series location in Y-axis
    /// параметры расположения серии на оси Y
    /// </summary>
    public class SeriesYLength
    {
        /// <summary>
        /// series
        /// серия
        /// </summary>
        public Series Series;

        /// <summary>
        ///  Start of displaying on X-axis
        /// начало отображения на оси X
        /// </summary>
        public int Xstart;

        /// <summary>
        /// End of display in X-axis
        /// конец отображения на оси X
        /// </summary>
        public int Xend;

        /// <summary>
        /// top point in Y axis
        /// верхняя точка на оси Y
        /// </summary>
        public double Ymax;

        /// <summary>
        /// lower point on Y axis
        /// нижняя точка на оси Y
        /// </summary>
        public double Ymin;

        /// <summary>
        /// number of consecutive incremental increments of one candle each
        /// количество подряд приращения по одной свече
        /// </summary>
        public int CountOneCandlesAdd;

    }

    /// <summary>
    /// Date position on X axis
    /// расположение даты на оси X
    /// </summary>
    public class TimeAxisXPoint
    {
        /// <summary>
        /// time
        /// время 
        /// </summary>
        public DateTime PositionTime;

        /// <summary>
        /// X-axis number
        /// номер на оси X
        /// </summary>
        public int PositionXPoint;
    }

    /// <summary>
    /// object reflecting current position of area in the window. Absolute values of position of sides of regions.
    /// объект отражающий текущее положение области в окне. Абсолютные значения положения сторон областей.
    /// </summary>
    public class ChartAreaPosition
    {
        /// <summary>
        /// area
        /// область
        /// </summary>
        public ChartArea Area;

        /// <summary>
        /// top point position
        /// положение верхней точки 
        /// </summary>
        public double UpPoint;

        /// <summary>
        /// bottom point position
        /// положение нижней точки 
        /// </summary>
        public double DownPoint;

        /// <summary>
        /// left point position
        /// положение левой точки 
        /// </summary>
        public double LeftPoint;

        /// <summary>
        ///  position of right point
        /// положение правой точки 
        /// </summary>
        public double RightPoint;
        // Variables to adjust height of areas
        // переменные для настройки высоты областей

        /// <summary>
        /// value by which we clicked on the area
        /// значение по которому мы нажали на область
        /// </summary>
        public double ValueYMouseOnClickStart;

        /// <summary>
        ///  height of area when we started dragging it
        /// высота области в момент когда мы её начали перетаскивать
        /// </summary>
        public double HeightAreaOnClick;

        /// <summary>
        /// point Y of area when we started dragging it
        /// точка Y области в момент когда мы её начали перетаскивать
        /// </summary>
        public double ValueYChartOnClick;

        /// <summary>
        /// height of upper area when we started dragging it
        /// высота верхней области в момент когда мы её начали перетаскивать
        /// </summary>
        public double HeightAreaOnClickBeforeUs;
        // Variables to adjust the presentation width
        // переменные для настройки ширины представления

        public double ValueXMouseOnClickStart;

        public int CountXValuesChartOnClickStart;
    }
}
