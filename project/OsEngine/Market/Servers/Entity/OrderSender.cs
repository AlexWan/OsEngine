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
    /// crutch class for sending a second-round order, when starting the server, the orders went ahead of the papers
    /// класс костыль, для отправки ордера на второй круг, если при запуске сервера ордера пошли раньше бумаг
    /// </summary>
    public class OrderSender
    {
        public Order Order;

        public void Sand()
        {
            Thread.Sleep(30000);
            if (UpdeteOrderEvent != null)
            {
                UpdeteOrderEvent(Order);
            }
        }

        public event Action<Order> UpdeteOrderEvent;
    }
}
