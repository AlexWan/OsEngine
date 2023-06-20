/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Entity
{
    /// <summary>
    /// Portfolio (account) in the trading system and positions opened on this account
    /// </summary>
    public class Portfolio
    {
        /// <summary>
        /// Account number
        /// </summary>
        public string Number;

        /// <summary>
        /// Deposit at the beginning of the session
        /// </summary>
        public decimal ValueBegin;

        /// <summary>
        /// Deposit amount now
        /// </summary>
        public decimal ValueCurrent;

        /// <summary>
        /// Blocked part of the deposit. And positions and bids
        /// </summary>
        public decimal ValueBlocked;

        /// <summary>
        /// Session profit
        /// </summary>
        public decimal Profit;

        // then goes the storage of open positions in the system by portfolio

        private List<PositionOnBoard> _positionOnBoard;

        /// <summary>
        /// Take positions on the portfolio in the trading system
        /// </summary>
        public List<PositionOnBoard> GetPositionOnBoard()
        {
            return _positionOnBoard;
        }

        /// <summary>
        /// Update the position of the instrument in the trading system
        /// </summary>
        public void SetNewPosition(PositionOnBoard position)
        {
            if (_positionOnBoard != null && _positionOnBoard.Count != 0)
            {
                for (int i = 0; i < _positionOnBoard.Count; i ++)
                {
                    if (_positionOnBoard[i].SecurityNameCode == position.SecurityNameCode)
                    {
                        _positionOnBoard[i].ValueCurrent = position.ValueCurrent;
                        _positionOnBoard[i].ValueBlocked = position.ValueBlocked;

                        return;
                    }
                }
            }

            if (_positionOnBoard == null)
            {
                _positionOnBoard = new List<PositionOnBoard>();
            }

            _positionOnBoard.Add(position);
        }

        /// <summary>
        /// Clear all positions on the exchange
        /// </summary>
        public void ClearPositionOnBoard()
        {
            _positionOnBoard = new List<PositionOnBoard>();
        }
    }
}