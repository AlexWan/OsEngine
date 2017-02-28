/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Threading;
using OsEngine.Entity;

namespace OsEngine.Market.Servers.Entity
{
    /// <summary>
    /// класс костыль для пересылки моих сделок, в том случае если моя сделка пришла раньше чем пришёл ордер по этой сделке
    /// </summary>
    public class MyTradeSender
    {
        public MyTrade Trade;

        public int Loop;

        public void Sand()
        {
            Thread.Sleep(500);
            if (UpdeteTradeEvent != null)
            {
                UpdeteTradeEvent(Trade, Loop);
            }
        }

        public event Action<MyTrade, int> UpdeteTradeEvent;
    }
}
