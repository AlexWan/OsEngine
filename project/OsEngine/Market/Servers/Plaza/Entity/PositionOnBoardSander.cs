using OsEngine.Entity;
using System;
using System.Threading;

namespace OsEngine.Market.Servers.Plaza.Entity
{
    /// <summary>
    /// this class creates a loop to delay the transfer position at the start of the server, because sometimes the portfolio position comes before the portfolio itself
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
