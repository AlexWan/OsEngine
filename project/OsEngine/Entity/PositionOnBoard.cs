/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Entity
{
    /// <summary>
    /// общая позиция по инструменту на бирже
    /// </summary>
    public class PositionOnBoard
    {
        /// <summary>
        /// позиция на начало сессии
        /// </summary>
        public decimal ValueBegin;

        /// <summary>
        /// текущий объём
        /// </summary>
        public decimal ValueCurrent;

        /// <summary>
        /// заблокированный объем
        /// </summary>
        public decimal ValueBlocked;

        /// <summary>
        /// инструмент по которому открыта позиция
        /// </summary>
        public string SecurityNameCode;

        /// <summary>
        /// портфель по которому открыта позиция
        /// </summary>
        public string PortfolioName;

    }
}
