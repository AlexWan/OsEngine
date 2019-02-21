/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;

namespace OsEngine.Entity
{
    /// <summary>
    /// информация по инструменту. Level 1
    /// </summary>
    public class SecurityLevelOne
    {
        /// <summary>
        /// бумага в формате Os.Engine
        /// </summary>
        public Security Security;

        /// <summary>
        /// Лучший спрос
        /// </summary>
        public decimal Highbid;

        /// <summary>
        /// Лотов на покупку по лучшей
        /// </summary>
        public int Biddepth;

        /// <summary>
        /// Совокупный спрос
        /// </summary>
        public int Biddeptht;

        /// <summary>
        /// Лучшее предложение
        /// </summary>
        public decimal Lowoffer;

        /// <summary>
        /// Лотов на продажу по лучшей
        /// </summary>
        public int Offerdepth;

        /// <summary>
        /// Совокупное предложение
        /// </summary>
        public int Offerdeptht;

        /// <summary>
        /// Изменение к закрытию предыдущего дня
        /// </summary>
        public decimal Change;

        /// <summary>
        ///	Лотов в последней сделке
        /// </summary>
        public int Qty;

        /// <summary>
        ///	Закрытие
        /// </summary>
        public decimal Closeprice;

        /// <summary>
        ///  Время последней Поля: TIME SETTLEDATE
        /// </summary>
        public DateTime DateTime;

        /// <summary>
        /// NUMTRADES	Сделок за сегодня
        /// </summary>
        public int Numtrades;
    }
}
