using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Bitfinex.BitfitnexEntity
{
    public class SubscriptionInformation
    {
        public string @event { get; set; }
        public string channel { get; set; }
        public int chanId { get; set; }
        public string pair { get; set; }
    }
}
