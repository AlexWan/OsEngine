/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System.Collections.Generic;
using OsEngine.Entity;

namespace OsEngine.OsCapitalAllocation
{
    /// <summary>
    /// Один бот (стратегия): имя и список позиций.
    /// Один файл = одна стратегия, позиции из всех табов агрегируются.
    /// </summary>
    public class BotFile
    {
        /// <summary>
        /// Имя бота (стратегии).
        /// </summary>
        public string BotName = string.Empty;

        /// <summary>
        /// Путь к исходному файлу.
        /// </summary>
        public string Path = string.Empty;

        /// <summary>
        /// Позиции бота.
        /// </summary>
        public List<Position> Positions = new List<Position>();
    }
}
