/*
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;

namespace OsEngine.Charts.CandleChart.Elements
{

    /// <summary>
    /// объект для прорисовки на чарте. Точка
    /// </summary>
    public class PointElement : IChartElement
    {
        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="nameUniq">уникальное имя элемента</param>
        /// <param name="area">имя области данных</param>
        public PointElement(string nameUniq, string area)
        {
            UniqName = nameUniq;
            Area = area;
            Style = MarkerStyle.Square;
            Size = 7;
            Color = Color.DarkRed;
        }

        /// <summary>
        /// имя типа
        /// </summary>
        /// <returns>название класса</returns>
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
        public event Action<string> NeadToShowDialog;

        // исключительно для точки

        /// <summary>
        /// размер точки
        /// </summary>
        public int Size;

        /// <summary>
        /// стиль точки. Прим: MarkerStyle.Square;
        /// </summary>
        public MarkerStyle Style;

        /// <summary>
        /// цвет точки
        /// </summary>
        public Color Color;

        /// <summary>
        /// точка игрик на графике для точки
        /// </summary>
        public decimal Y;

        /// <summary>
        /// время прорисовки точки
        /// </summary>
        public DateTime TimePoint;

        /// <summary>
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
