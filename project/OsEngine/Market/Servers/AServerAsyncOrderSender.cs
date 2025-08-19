/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Threading.Tasks;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers
{
    public class AServerAsyncOrderSender
    {
        public AServerAsyncOrderSender(int rateGateLimitMls)
        {
            if(rateGateLimitMls < 0)
            {
                rateGateLimitMls = 0;
            }

            if(rateGateLimitMls > 0)
            {
                _rateGate = new RateGate(1, TimeSpan.FromMilliseconds(rateGateLimitMls));
            }
        }

        private RateGate _rateGate;

        public void ExecuteAsync(OrderAserverSender order)
        {
            if (_rateGate != null)
            {
                _rateGate.WaitToProceed();
            }

            Task.Run(() => ExecuteOrderInRealizationEvent(order));
        }

        public event Action<OrderAserverSender> ExecuteOrderInRealizationEvent;
    }
}
