/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using OsEngine.Logging;

namespace OsEngine.OsTrader.Panels.Tab
{
    /// <summary>
    /// tab interface for robot panel
    /// интерфейс вкладки для панели робота
    /// </summary>
    public interface IIBotTab
    {
        /// <summary>
        /// delete / 
        /// удалить 
        /// </summary>
        void Delete();

        /// <summary>
        /// clear / 
        /// очистить
        /// </summary>
        void Clear();

        /// <summary>
        /// stop drawing tabs / 
        /// остановить прорисовку вкладки
        /// </summary>
        void StopPaint();

        /// <summary>
        /// tab name /
        /// имя вкладки
        /// </summary>
        string TabName { get; set; }

        /// <summary>
        /// tab number / 
        /// номер вкладки
        /// </summary>
        int TabNum { get; set; }

        DateTime LastTimeCandleUpdate { get; set; }
        
        event Action<string, LogMessageType> LogMessageEvent;
    }
}
