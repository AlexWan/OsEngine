using System;
using OsEngine.Entity;

namespace OsEngine.Market.Servers.MoexFixFastCurrency.Entity
{
    public class WaitingTrade
    {
        public string UniqueName { get; set; }
        public int RptSeq { get; set; }
        public Trade Trade;
    }
}
