using System;

namespace OsEngine.Market.Servers.MoexFixFastCurrency.Entity
{
    public class OrderChange
    {
        public string UniqueName { get; set; }
        public int RptSeq { get; set; }
        public string MDEntryID { get; set; }
        public string Action { get; set; }
        public string OrderType { get; set; }
        public decimal Price { get; set; }
        public decimal Volume { get; set; }
    }
}

