/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Threading;
using OsEngine.Entity;

namespace OsEngine.Market.Servers.Entity
{
    /// <summary>
    /// этот класс создаёт петлю для задержки передачи позиции на старте сервера, 
    /// т.к. иногда позиция по портфелю приходит раньше самого портфеля
    /// </summary>
    public class PositionOnBoardSander
    {
        public PositionOnBoard PositionOnBoard;

        public void Go()
        {
            Thread.Sleep(5000);
            if (TimeSendPortfolio != null)
            {
                TimeSendPortfolio(PositionOnBoard);
            }
        }

        public event Action<PositionOnBoard> TimeSendPortfolio;
    }
}
