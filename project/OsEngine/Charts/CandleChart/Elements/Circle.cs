/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Drawing;

namespace OsEngine.Charts.CandleChart.Elements
{
    public class Circle : IChartElement
    {
        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="nameUniq">unique item name/уникальное имя элемента</param>
        /// <param name="nameArea">chart area/область на чарте</param>
        public Circle(string nameUniq, string area)
        {
            UniqName = nameUniq;
            Area = area;
            Color = Color.Gray;
            Font = new Font("Arial", 9, FontStyle.Bold);
            LabelTextColor = Color.White;
            TimeCenter = DateTime.MinValue;
            Diameter = 50;
            Thickness = 2;         
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
        /// circle signature
        /// подпись окружности
        /// </summary>
        public string Label;

        /// <summary>
        /// circle color
        /// цвет окружности
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
        /// value point Y on graph for center of circle
        /// значение точки Y на графике для центра окружности
        /// </summary>
        public decimal Y;

        /// <summary>
        /// time of center point of circle
        /// время точки центра окружности
        /// </summary>
        public DateTime TimeCenter;

        /// <summary>
        /// circle line thickness
        /// толщина линии окружности
        /// </summary>
        public int Thickness;

        /// <summary>
        /// diameter of circle 
        /// диаметр окружности
        /// </summary>
        public int Diameter;
    }
}