/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Entity
{
    /// <summary>
    /// портфель (счёт) в торговой системе и позиции открытые по этому счёту
    /// </summary>
    public class Portfolio
    {
        /// <summary>
        /// номер счёта
        /// </summary>
        public string Number;

        /// <summary>
        /// депозит на счёте на начало сессии
        /// </summary>
        public decimal ValueBegin;

        /// <summary>
        /// размер депозита сейчас
        /// </summary>
        public decimal ValueCurrent;

        /// <summary>
        /// блокированная часть депозита. И позициями и заявками
        /// </summary>
        public decimal ValueBlocked;

        /// <summary>
        /// профит за сессию
        /// </summary>
        public decimal Profit;

        // далее идёт хранилище открытых позиций в системе по портфелю

        private List<PositionOnBoard> _positionOnBoard;

        /// <summary>
        /// взять позиции по портфелю в торговой системе
        /// </summary>
        public List<PositionOnBoard> GetPositionOnBoard()
        {
            return _positionOnBoard;
        }

        /// <summary>
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
                        _positionOnBoard[i] = position;
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
        /// очистить все позиции на бирже
        /// </summary>
        public void ClearPositionOnBoard()
        {
            _positionOnBoard = new List<PositionOnBoard>();
        }
    }
}

