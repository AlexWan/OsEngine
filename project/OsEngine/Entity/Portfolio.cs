/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Market;
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
        /// Profit or loss on open positions
        /// </summary>
        public decimal UnrealizedPnl;

        /// <summary>
        /// Connector to which the portfolio belongs
        /// </summary>
        public ServerType ServerType;

        /// <summary>
        /// Connector unique name in multi-connection mode
        /// </summary>
        public string ServerUniqueName = "";

        // then goes the storage of open positions in the system by portfolio

        public List<PositionOnBoard> PositionOnBoard;

        /// <summary>
        /// Take positions on the portfolio in the trading system
        /// </summary>
        public List<PositionOnBoard> GetPositionOnBoard()
        {
            return PositionOnBoard;
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

            if(string.IsNullOrEmpty(position.PortfolioName))
            {
                position.PortfolioName = Number;
            }

            if (PositionOnBoard != null && PositionOnBoard.Count != 0)
            {
                for (int i = 0; i < PositionOnBoard.Count; i ++)
                {
                    PositionOnBoard currentPosition = PositionOnBoard[i];

                    if (currentPosition.SecurityNameCode == position.SecurityNameCode)
                    {
                        currentPosition.ValueCurrent = position.ValueCurrent;
                        currentPosition.ValueBlocked = position.ValueBlocked;
                        currentPosition.UnrealizedPnl = position.UnrealizedPnl;

                        return;
                    }
                }
            }

            if (PositionOnBoard == null)
            {
                PositionOnBoard = new List<PositionOnBoard>();
            }

            if (PositionOnBoard.Count == 0)
            {
                PositionOnBoard.Add(position);
            }
            else if (position.SecurityNameCode == "USDT"
                || position.SecurityNameCode == "USDC"
                || position.SecurityNameCode == "USD"
                || position.SecurityNameCode == "RUB"
                || position.SecurityNameCode == "EUR")
            {
                PositionOnBoard.Insert(0, position);
            }
            else if (PositionOnBoard.Count == 1)
            {
                if (FirstIsBiggerThanSecond(position.SecurityNameCode, PositionOnBoard[0].SecurityNameCode))
                {
                    PositionOnBoard.Add(position);
                }
                else
                {
                    PositionOnBoard.Insert(0, position);
                }
            }
            else
            { // insert name sort

                bool isInsert = false;

                for (int i = 0; i < PositionOnBoard.Count; i++)
                {
                    if (PositionOnBoard[i].SecurityNameCode == "USDT"
                  || PositionOnBoard[i].SecurityNameCode == "USDC"
                  || PositionOnBoard[i].SecurityNameCode == "USD"
                  || PositionOnBoard[i].SecurityNameCode == "RUB"
                  || PositionOnBoard[i].SecurityNameCode == "EUR")
                    {
                        continue;
                    }

                    if (FirstIsBiggerThanSecond(
                        position.SecurityNameCode,
                        PositionOnBoard[i].SecurityNameCode) == false)
                    {
                        PositionOnBoard.Insert(i, position);
                        isInsert = true;
                        break;
                    }
                }

                if (isInsert == false)
                {
                    PositionOnBoard.Add(position);
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
            PositionOnBoard = new List<PositionOnBoard>();
        }
    }
}