using System.Collections.Generic;

namespace OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity
{
    public class AccountBalance
    {
        public string a { get; set; }
        public string f { get; set; }
        public string l { get; set; }
    }

    public class OutboundAccountInfo
    {
        public string e { get; set; }
        public long E { get; set; }
        public int m { get; set; }
        public int t { get; set; }
        public int b { get; set; }
        public int s { get; set; }
        public bool T { get; set; }
        public bool W { get; set; }
        public bool D { get; set; }
        public long u { get; set; }
        public List<AccountBalance> B { get; set; }
    }
}