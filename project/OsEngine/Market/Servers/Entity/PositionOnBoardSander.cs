/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Threading;
using OsEngine.Entity;

namespace OsEngine.Market.Servers.Entity
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
