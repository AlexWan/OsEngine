/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Entity
{
    /// <summary>
    /// portfolio (account) in the trading system and positions opened on this account
    /// портфель (счёт) в торговой системе и позиции открытые по этому счёту
    /// </summary>
    public class Portfolio
    {
        /// <summary>
        /// Account number
        /// номер счёта
        /// </summary>
        public string Number;

        /// <summary>
        /// deposit at the beginning of the session
        /// депозит на счёте на начало сессии
        /// </summary>
        public decimal ValueBegin;

        /// <summary>
        /// deposit amount now
        /// размер депозита сейчас
        /// </summary>
        public decimal ValueCurrent;

        /// <summary>
        /// blocked part of the deposit. And positions and bids
        /// блокированная часть депозита. И позициями и заявками
        /// </summary>
        public decimal ValueBlocked;

        /// <summary>
        /// session profit
        /// профит за сессию
        /// </summary>
        public decimal Profit;
        // then goes the storage of open positions in the system by portfolio
        // далее идёт хранилище открытых позиций в системе по портфелю

        private List<PositionOnBoard> _positionOnBoard;

        /// <summary>
        /// take positions on the portfolio in the trading system
        /// взять позиции по портфелю в торговой системе
        /// </summary>
        public List<PositionOnBoard> GetPositionOnBoard()
        {
            return _positionOnBoard;
        }

        /// <summary>
        /// update the position of the instrument in the trading system
        /// обновить позицию по инструменту в торговой системе
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
        /// clear all positions on the exchange
        /// очистить все позиции на бирже
        /// </summary>
        public void ClearPositionOnBoard()
        {
            _positionOnBoard = new List<PositionOnBoard>();
        }
    }
}

