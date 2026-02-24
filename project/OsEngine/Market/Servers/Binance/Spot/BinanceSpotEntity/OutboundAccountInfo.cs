using System.Collections.Generic;

namespace OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity
{
    public class OutboundAccountInfo
    {
        public string subscriptionId { get; set; }
        public AccountData @event { get; set; }
    }

    public class AccountData
    {
        public string e { get; set; }
        public string E { get; set; }
        public string u { get; set; }
        public List<AccountBalance> B { get; set; }
    }

    public class AccountBalance
    {
        public string a { get; set; }
        public string f { get; set; }
        public string l { get; set; }
    }
}