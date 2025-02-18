/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Drawing;

namespace OsEngine.Charts.CandleChart.Elements
{
    public class RectangleElement : IChartElement
    {
        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="nameUniq">unique item name/уникальное имя элемента</param>
        /// <param name="nameArea">chart area/область на чарте</param>
        public RectangleElement(string nameUniq, string area)
        {
            UniqName = nameUniq;
            Area = area;
            Color = Color.Gray;
            Font = new Font("Arial", 9, FontStyle.Bold);
            LabelTextColor = Color.Green;
            LabelCorner = 2;
            Thickness = 2;
            TimeStart = DateTime.MinValue;
            TimeEnd = DateTime.MaxValue;
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
            if (UpdateEvent != null)
            {
                UpdateEvent(this);
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
        /// it's necessary to update item on chart
        /// необходимо обновить элемент на чарте
        /// </summary>
        public event Action<IChartElement> UpdateEvent;

        /// <summary>
        /// it's necessary to remove element from chart
        /// необходимо удалить элемент с чарта
        /// </summary>
        public event Action<IChartElement> DeleteEvent;

        /// <summary>
        /// rectangle label
        /// подпись прямоугольника
        /// </summary>
        public string Label;

        /// <summary>
        /// label corner, 1 to 4 (0 - off) counter clockwise;
        /// угол расположения подписи от 1 до 4 (0 - выкл) против часовой стрелки
        /// </summary>
        public int LabelCorner;

        /// <summary>
        /// rectangle color
        /// цвет прямоугольника
        /// </summary>
        public Color Color;

        /// <summary>
        /// label back color
        /// цвет фона на котором текст
        /// </summary>
        public Color LabelBackColor;

        /// <summary>
        /// label text color
        /// цвет текста лейбла
        /// </summary>
        public Color LabelTextColor;

        /// <summary>
        /// label font and text size 
        /// шрифт и размер текста лейбла
        /// </summary>
        public Font Font;

        /// <summary>
        /// value of Y point on graph for rectangle beginning
        /// значение начальной точки Y на графике прямоугольника
        /// </summary>
        public decimal ValueYStart;

        /// <summary>
        /// value of Y point on graph for rectangle end
        /// значение конечной точки Y на графике прямоугольника
        /// </summary>
        public decimal ValueYEnd;

        /// <summary>
        /// time of start point of rectangle
        /// время начальной точки прямоугольника
        /// </summary>
        public DateTime TimeStart;

        /// <summary>
        /// time of end point of rectangle
        /// время конечной точки прямоугольника
        /// </summary>
        public DateTime TimeEnd;

        /// <summary>
        /// rectangle line thickness
        /// толщина линии прямоугольника
        /// </summary>
        public int Thickness;

    }
}