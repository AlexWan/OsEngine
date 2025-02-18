/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Drawing;

namespace OsEngine.Charts.CandleChart.Elements
{
    // Drawing element on chart: Line
    // элемент для прорисовки на чарте: Линия
    public class LineHorisontal : IChartElement
    {

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="nameUniq">unique item name/уникальное имя элемента</param>
        /// <param name="nameArea">chart area/область на чарте</param>
        /// <param name="canResizeOnChart">it possible to pull line by point at base/можно ли перетягивать линию за точку в основании</param>
        public LineHorisontal(string nameUniq, string nameArea, bool canResizeOnChart)
        {
            UniqName = nameUniq;
            Area = nameArea;
            Color = Color.DarkRed;
            CanResize = canResizeOnChart;

            TimeStart = DateTime.MinValue;
            TimeEnd = DateTime.MaxValue;
        }

        /// <summary>
        /// base class type name
        /// имя типа базового класса
        /// </summary>
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
        /// user moved line on chart
        /// пользователь передвинул линию на чарте
        /// </summary>
        public event Action<IChartElement> ChangeOnChartEvent;

        /// <summary>
        /// need to call up menu
        /// необходимо вызвать меню
        /// </summary>
        public event Action<LineHorisontal, decimal, decimal> NeedToShowDialog;

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
        /// Set new values for element
        /// установить для элемента новые значения
        /// </summary>
        /// <param name="value"></param>
        public void SetNewValueFromChart(decimal value)
        {
            Value = value;

            if (ChangeOnChartEvent != null)
            {
                ChangeOnChartEvent(this);
            }
        }

        /// <summary>
        /// trigger a dialogue event
        /// вызвать событие диалога
        /// </summary>
        public void ShowDialog(decimal x, decimal y)
        {
            if (NeedToShowDialog != null)
            {
                NeedToShowDialog(this, x, y);
            }
        }
        // line items only
        // элементы только линии

        /// <summary>
        /// value of first point. У
        /// значение первой точки. У
        /// </summary>
        public decimal Value;

        /// <summary>
        /// line signature
        /// подпись линии
        /// </summary>
        public string Label;

        /// <summary>
        /// line color
        /// цвет линии
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
        /// толщина линии. От 1 до 10 пикселей
        /// </summary>
        public int LineWidth = 1;

        /// <summary>
        /// line drawing start time
        /// время начала прорисовки линии
        /// </summary>
        public DateTime TimeStart;

        /// <summary>
        /// end of drawing time
        /// время конца прорисовки
        /// </summary>
        public DateTime TimeEnd;

        /// <summary>
        /// whether to allow user to drag and drop an item on chart
        /// нужно ли разрешить пользователю перетаскивать элемент на чарте
        /// </summary>
        public bool CanResize;

    }
}
