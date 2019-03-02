/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;

namespace OsEngine.Entity
{
    /// <summary>
    /// information on the instrument. Level 1
    /// информация по инструменту. Level 1
    /// </summary>
    public class SecurityLevelOne
    {
        /// <summary>
        /// security in Os.Engine format
        /// бумага в формате Os.Engine
        /// </summary>
        public Security Security;

        /// <summary>
        /// best did
        /// Лучший спрос
        /// </summary>
        public decimal Highbid;

        /// <summary>
        /// Lots to buy the best
        /// Лотов на покупку по лучшей
        /// </summary>
        public int Biddepth;

        /// <summary>
        /// Total demand
        /// Совокупный спрос
        /// </summary>
        public int Biddeptht;

        /// <summary>
        /// Best offer
        /// Лучшее предложение
        /// </summary>
        public decimal Lowoffer;

        /// <summary>
        /// Lots for sale at the best
        /// Лотов на продажу по лучшей
        /// </summary>
        public int Offerdepth;

        /// <summary>
        /// Total offer
        /// Совокупное предложение
        /// </summary>
        public int Offerdeptht;

        /// <summary>
        /// Change in the closing of the previous day
        /// Изменение к закрытию предыдущего дня
        /// </summary>
        public decimal Change;

        /// <summary>
        /// Lots in the last transaction
        ///	Лотов в последней сделке
        /// </summary>
        public int Qty;

        /// <summary>
        ///	Закрытие
        /// Close
        /// </summary>
        public decimal Closeprice;

        /// <summary>
        /// time of the last Field: TIME SETTLEDATE
        ///  Время последней Поля: TIME SETTLEDATE
        /// </summary>
        public DateTime DateTime;

        /// <summary>
        /// NUMTRADES Transactions for today
        /// NUMTRADES	Сделок за сегодня
        /// </summary>
        public int Numtrades;
    }
}
