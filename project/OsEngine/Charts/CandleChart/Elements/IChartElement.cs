/*
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;

namespace OsEngine.Charts.CandleChart.Elements
{
    /// <summary>
    /// интерфейс для создания элементов чарта. Линий, точек и т.д.
    /// </summary>
    public interface IChartElement
    {
        /// <summary>
        /// имя базового класса
        /// </summary>
        string TypeName();

        /// <summary>
        /// уникальное имя на чарте
        /// </summary>
        string UniqName { get; set; }

        /// <summary>
        /// область чарта на которой будет прорисован элемент
        /// </summary>
        string Area { get; set; }

        /// <summary>
        /// необходимо обновить элемент на чарте
        /// </summary>
        event Action<IChartElement> UpdeteEvent;

        /// <summary>
        /// необходимо удалить элемент с чарта навсегда
        /// </summary>
        event Action<IChartElement> DeleteEvent;

        /// <summary>
        /// обновить элемент на чарте
        /// </summary>
        void Refresh();

        /// <summary>
        /// удалить элемент с чарта
        /// </summary>
        void Delete();

    }
}
