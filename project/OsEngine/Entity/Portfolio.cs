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
            if(string.IsNullOrEmpty(position.SecurityNameCode))
            {
                return;
            }

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

            if (_positionOnBoard.Count == 0)
            {
                _positionOnBoard.Add(position);
            }
            else if (position.SecurityNameCode == "USDT"
                || position.SecurityNameCode == "USDC"
                || position.SecurityNameCode == "USD"
                || position.SecurityNameCode == "RUB"
                || position.SecurityNameCode == "EUR")
            {
                _positionOnBoard.Insert(0, position);
            }
            else if (_positionOnBoard.Count == 1)
            {
                if (FirstIsBiggerThanSecond(position.SecurityNameCode, _positionOnBoard[0].SecurityNameCode))
                {
                    _positionOnBoard.Add(position);
                }
                else
                {
                    _positionOnBoard.Insert(0, position);
                }
            }
            else
            { // insert name sort

                bool isInsert = false;

                for (int i = 0; i < _positionOnBoard.Count; i++)
                {
                    if (_positionOnBoard[i].SecurityNameCode == "USDT"
                  || _positionOnBoard[i].SecurityNameCode == "USDC"
                  || _positionOnBoard[i].SecurityNameCode == "USD"
                  || _positionOnBoard[i].SecurityNameCode == "RUB"
                  || _positionOnBoard[i].SecurityNameCode == "EUR")
                    {
                        continue;
                    }

                    if (FirstIsBiggerThanSecond(
                        position.SecurityNameCode,
                        _positionOnBoard[i].SecurityNameCode) == false)
                    {
                        _positionOnBoard.Insert(i, position);
                        isInsert = true;
                        break;
                    }
                }

                if (isInsert == false)
                {
                    _positionOnBoard.Add(position);
                }
            }
        }

        private bool FirstIsBiggerThanSecond(string s1, string s2)
        {
            for (int i = 0; i < (s1.Length > s2.Length ? s2.Length : s1.Length); i++)
            {
                if (s1.ToCharArray()[i] < s2.ToCharArray()[i]) return false;
                if (s1.ToCharArray()[i] > s2.ToCharArray()[i]) return true;
            }
            return false;
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