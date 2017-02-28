/*
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Drawing;

namespace OsEngine.Charts.CandleChart.Elements
{
    // элемент для прорисовки на чарте: Линия
    public class LineHorisontal : IChartElement
    {

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="nameUniq">уникальное имя элемента</param>
        /// <param name="nameArea">область на чарте</param>
        /// <param name="canResizeOnChart">можно ли перетягивать линию за точку в основании</param>
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
        /// имя типа базового класса
        /// </summary>
        public string TypeName()
        {
            return GetType().Name;
        }

        /// <summary>
        /// уникальное имя на чарте
        /// </summary>
        public string UniqName { get; set; }

        /// <summary>
        /// область чарта на которой будет прорисован элемент
        /// </summary>
        public string Area { get; set; }

        /// <summary>
        /// необходимо обновить элемент на чарте
        /// </summary>
        public event Action<IChartElement> UpdeteEvent;

        /// <summary>
        /// необходимо удалить элемент с чарта
        /// </summary>
        public event Action<IChartElement> DeleteEvent;

        /// <summary>
        /// пользователь передвинул линию на чарте
        /// </summary>
        public event Action<IChartElement> ChangeOnChartEvent;

        /// <summary>
        /// необходимо вызвать меню
        /// </summary>
        public event Action<LineHorisontal, decimal, decimal> NeadToShowDialog;

        /// <summary>
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
        /// вызвать событие диалога
        /// </summary>
        public void ShowDialog(decimal x, decimal y)
        {
            if (NeadToShowDialog != null)
            {
                NeadToShowDialog(this, x, y);
            }
        }

        // элементы только линии

        /// <summary>
        /// значение первой точки. У
        /// </summary>
        public decimal Value;

        /// <summary>
        /// подпись линии
        /// </summary>
        public string Label;

        /// <summary>
        /// цвет линии
        /// </summary>
        public Color Color;

        /// <summary>
        /// время начала прорисовки линии
        /// </summary>
        public DateTime TimeStart;

        /// <summary>
        /// время конца прорисовки
        /// </summary>
        public DateTime TimeEnd;

        /// <summary>
        /// нужно ли разрешить пользователю перетаскивать элемент на чарте
        /// </summary>
        public bool CanResize;

    }
}
