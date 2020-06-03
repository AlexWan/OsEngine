/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;

namespace OsEngine.Charts.CandleChart.Elements
{

    /// <summary>
    /// an object to draw on chart. Point
    /// объект для прорисовки на чарте. Точка
    /// </summary>
    public class PointElement : IChartElement
    {
        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="nameUniq">unique element name/уникальное имя элемента</param>
        /// <param name="area">data area name/имя области данных</param>
        public PointElement(string nameUniq, string area)
        {
            UniqName = nameUniq;
            Area = area;
            Style = MarkerStyle.Square;
            Size = 7;
            Color = Color.DarkRed;
        }

        /// <summary>
        /// type name
        /// имя типа
        /// </summary>
        /// <returns>class name/название класса</returns>
        public string TypeName()
        {
            return GetType().Name;
        }

        /// <summary>
        /// unique name on chart
        /// уникальное имя на чарте
        /// </summary>
        public string UniqName { get; set; }

        /// <summary>
        /// chart area where element will be drawn
        /// область чарта на которой будет прорисован элемент
        /// </summary>
        public string Area { get; set; }

        /// <summary>
        /// update item on chart
        /// обновить элемент на чарте
        /// </summary>
        public void Refresh()
        {
            if (UpdeteEvent != null)
            {
                UpdeteEvent(this);
            }
        }

        /// <summary>
        /// uninstall an item from chart
        /// удалить элемент с чарта
        /// </summary>
        public void Delete()
        {
            if (DeleteEvent != null)
            {
                DeleteEvent(this);
            }
        }

        /// <summary>
        /// Display settings window
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            if (NeadToShowDialog != null)
            {
                NeadToShowDialog(UniqName.Split('&')[0]);
            }
        }

        /// <summary>
        /// it's necessary to update item on chart
        /// необходимо обновить элемент на чарте
        /// </summary>
        public event Action<IChartElement> UpdeteEvent;

        /// <summary>
        /// it's necessary to remove element from chart
        /// необходимо удалить элемент с чарта
        /// </summary>
        public event Action<IChartElement> DeleteEvent;

        /// <summary>
        /// user moved line on chart
        /// пользователь передвинул линию на чарте
        /// </summary>
        public event Action<IChartElement> ChangeOnChartEvent;

        /// <summary>
        /// need to call up menu
        /// необходимо вызвать меню
        /// </summary>
        public event Action<string> NeadToShowDialog;
        // exclusively for point
        // исключительно для точки

        /// <summary>
        /// point size
        /// размер точки
        /// </summary>
        public int Size;
        
        public string Label;

        /// <summary>
        /// style of point. Note: MarkerStyle.Square;
        /// стиль точки. Прим: MarkerStyle.Square;
        /// </summary>
        public MarkerStyle Style;

        /// <summary>
        /// point color
        /// цвет точки
        /// </summary>
        public Color Color;

        /// <summary>
        /// Point Y on chart for point
        /// точка игрик на графике для точки
        /// </summary>
        public decimal Y;

        /// <summary>
        /// point drawing time
        /// время прорисовки точки
        /// </summary>
        public DateTime TimePoint;

        /// <summary>
        /// Set new values for element
        /// установить для элемента новые значения
        /// </summary>
        /// <param name="value"></param>
        public void SetNewValueFromChart(decimal value)
        {
            Y = value;

            if (ChangeOnChartEvent != null)
            {
                ChangeOnChartEvent(this);
            }
        }
    }
}
