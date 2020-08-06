using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using OsEngine.Alerts;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Charts.ColorKeeper;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Logging;
using Grid = System.Windows.Controls.Grid;

namespace OsEngine.Charts.CandleChart
{
    public interface IChartPainter
    {
        //service сервис

        /// <summary>
        /// change candle timeframe for drawing
        /// изменить таймФрейм свечек для прорисовки
        /// </summary>
        /// <param name="timeFrameSpan"></param>
        /// <param name="timeFrame"></param>
        void SetNewTimeFrame(TimeSpan timeFrameSpan, TimeFrame timeFrame);

        /// <summary>
        /// to start drawing schedule
        /// начать прорисовку графика
        /// </summary>
        void StartPaintPrimeChart(Grid gridChart, WindowsFormsHost host, System.Windows.Shapes.Rectangle rectangle);
        
        /// <summary>
        /// Stop drawing chart
        /// остановить прорисовку графика
        /// </summary>
        void StopPaint();

        /// <summary>
        /// clear chart
        /// очистить график
        /// </summary>
        void ClearDataPointsAndSizeValue();

        void ClearSeries();

        /// <summary>
        /// delete all files related to class
        /// удалить все файлы связанные с классом
        /// </summary>
        void Delete();

        /// <summary>
        /// outgoing message for log
        /// исходящее сообщение для лога
        /// </summary>
        event Action<string, LogMessageType> LogMessageEvent;

        /// <summary>
        /// to set up a dark scheme for chart
        /// установить тёмную схему для чарта
        /// </summary>
        void SetBlackScheme();

        void SetPointSize(ChartPositionTradeSize pointSize);

        void SetPointType(PointType type);

        /// <summary>
        /// to set up a white scheme for chart
        /// установить белую схему для чарта
        /// </summary>
        void SetWhiteScheme();

        /// <summary>
        /// to repaint colors at chart
        /// перекрасить цвета у чарта
        /// </summary>
        void RefreshChartColor();

        int GetCursorSelectCandleNumber();

        decimal GetCursorSelectPrice();

        void RemoveCursor();

        /// <summary>
        /// create a data area on chart
        /// создать область данных на чарте
        /// </summary>
        /// <param name="nameArea">area name/имя области</param>
        /// <param name="height">area height/высота области</param>
        /// <returns></returns>
        string CreateArea(string nameArea, int height);

        /// <summary>
        /// create a series of data on area
        /// создать серию данных на области
        /// </summary>
        /// <returns>returns the name of data series. null in case of error/возвращается имя серии данных. null в случае ошибки</returns>
        string CreateSeries(string areaName, IndicatorChartPaintType indicatorType, string name);

        void ShowContextMenu(ContextMenu menu);

        List<string> GetAreasNames();

        /// <summary>
        /// removes the indicator from chart and, if it was the last one on tarea, then the area
        /// удаляет с графика индикатор и, если он был последний на области, то и область
        /// </summary>
        /// <param name="indicator">indicator that should be removed/индикатор который следует удалить</param>
        void DeleteIndicator(IIndicator indicator);

        /// <summary>
        /// is there an indicator created
        /// создан ли индикатор
        /// </summary>
        /// <param name="name">indicator name/имя индикатора</param>
        /// <returns>true / false</returns>
        bool IndicatorIsCreate(string name);

        /// <summary>
        /// whether the area is created
        /// создана ли область
        /// </summary>
        /// <param name="name">area name/имя области</param>
        /// <returns>try / false</returns>
        bool AreaIsCreate(string name);

        /// <summary>
        /// add candles to the drawing
        /// добавить свечки на прорисовку
        /// </summary>
        /// <param name="history">candles/свечи</param>
        void ProcessCandles(List<Candle> history);

        /// <summary>
        /// add ticks to the drawing
        /// добавить тики в прорисовку
        /// </summary>
        /// <param name="trades">ticks/тики</param>
        void ProcessTrades(List<Trade> trades);

        /// <summary>
        /// Create a tick data area
        /// создать область для тиковых данных
        /// </summary>
        void CreateTickArea();

        /// <summary>
        /// Delete tick data area
        /// удалить область для тиковых данных
        /// </summary>
        void DeleteTickArea();

        // Deals / сделки

        /// <summary>
        /// add positions to the drawing
        /// добавить позиции в прорисовку
        /// </summary>
        /// <param name="deals">deals/сделки</param>
        void ProcessPositions(List<Position> deals);

        // CUSTOM ELEMENTS ПОЛЬЗОВАТЕЛЬСКИЕ ЭЛЕМЕНТЫ

        /// <summary>
        /// add an item to the drawing
        /// добавить элемент в прорисовку
        /// </summary>
        /// <param name="element">element/элемент</param>
        void ProcessElem(IChartElement element);

        /// <summary>
        /// take the element off the chart
        /// убрать элемент с чарта
        /// </summary>
        void ProcessClearElem(IChartElement element);

        /// <summary>
        /// to draw a horizontal line across the entire chart
        /// прорисовать горизонтальную линию через весь чарт
        /// </summary>
        /// <param name="lineElement">line/линия</param>
        void PaintHorisiontalLineOnArea(LineHorisontal lineElement);

        /// <summary>
        /// draw a point on chart
        /// нарисовать на чарте точку
        /// </summary>
        /// <param name="point"></param>
        void PaintPoint(PointElement point);

        // Alerts АЛЕРТЫ

        void ClearAlerts(List<IIAlert> alertArray);

        void PaintAlert(AlertToChart alert);

        /// <summary>
        /// draw a line on a series
        /// прорисовать линию на серии
        /// </summary>
        void PaintOneLine(Series mySeries, List<Candle> candles,
            ChartAlertLine line, System.Drawing.Color colorLine, int borderWidth, System.Drawing.Color colorLabel, string label);

        // Indicators  ИНДИКАТОРЫ

        /// <summary>
        /// Add an indicator to the drawing
        /// добавить индикатор в прорисовку
        /// </summary>
        /// <param name="indicator">indicator/индикатор</param>
        void ProcessIndicator(IIndicator indicator);

        /// <summary>
        /// forcefully redrawn indicator on chart from beginning to end
        /// принудительно перерисоват индикатор на графике от начала до конца
        /// </summary>
        /// <param name="indicatorCandle">indicator/индикатор</param>
        void RePaintIndicator(IIndicator indicatorCandle);

        // Patterns  Паттерны

        bool IsPatternChart { get; set; }

        event Action<int> ClickToIndexEvent;

        void PaintSingleCandlePattern(List<Candle> candles);

        void PaintSingleVolumePattern(List<Candle> candles);

        void PaintInDifColor(int indexStart, int indexEnd, string seriesName);

        // chart transition переход по чарту

        /// <summary>
        /// move chart to a specified time
        /// переместить чарт к заданному времени
        /// </summary>
        /// <param name="time">time/время</param>
        void GoChartToTime(DateTime time);

        /// <summary>
        /// move chart to a specified time
        /// переместить чарт к заданному времени
        /// </summary>
        void GoChartToIndex(int index);

        // management of collapsing areas управление схлопывание областей

        /// <summary>
        /// hide all except main
        /// спрятать все области кроме главной
        /// </summary>
        void HideAreaOnChart();

        /// <summary>
        /// show all areas
        /// показать все области
        /// </summary>
        void ShowAreaOnChart();

        /// <summary>
        /// there's been a change int X-axis
        /// изменилось представление по оси Х
        /// </summary>
        event Action<int> SizeAxisXChangeEvent;

        event Action<ChartClickType> ChartClickEvent;

    }

    public enum ChartClickType
    {
        LeftButton,

        RightButton
    }
}
