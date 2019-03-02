/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Entity
{
    /// <summary>
    /// common position on the instrument on the exchange
    /// общая позиция по инструменту на бирже
    /// </summary>
    public class PositionOnBoard
    {
        /// <summary>
        /// position at the beginning of the session
        /// позиция на начало сессии
        /// </summary>
        public decimal ValueBegin;

        /// <summary>
        /// current volume
        /// текущий объём
        /// </summary>
        public decimal ValueCurrent;

        /// <summary>
        /// blocked volume
        /// заблокированный объем
        /// </summary>
        public decimal ValueBlocked;

        /// <summary>
        /// tool for which the position is open
        /// инструмент по которому открыта позиция
        /// </summary>
        public string SecurityNameCode;

        /// <summary>
        /// portfolio on which the position is open
        /// портфель по которому открыта позиция
        /// </summary>
        public string PortfolioName;

    }
}
