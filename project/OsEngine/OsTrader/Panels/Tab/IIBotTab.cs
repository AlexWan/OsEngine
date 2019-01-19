/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;
using OsEngine.Logging;

namespace OsEngine.OsTrader.Panels.Tab
{
    /// <summary>
    /// интерфейс вкладки для панели робота
    /// </summary>
    public interface IIBotTab
    {
        /// <summary>
        /// удалить все связанные 
        /// </summary>
        void Delete();

        /// <summary>
        /// очистить
        /// </summary>
        void Clear();

        /// <summary>
        /// остановить прорисовку вкладки
        /// </summary>
        void StopPaint();

        /// <summary>
        /// имя вкладки
        /// </summary>
        string TabName { get; set; }

        /// <summary>
        /// номер вкладки
        /// </summary>
        int TabNum { get; set; }

        event Action<string, LogMessageType> LogMessageEvent;
    }
}
