using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.MoexFixFastTwimeFutures.Entity
{
    public class OrderChange
    {
        public string NameID { get; set; }
        public int RptSeq { get; set; }
        public string MDEntryID { get; set; }
        public string Action { get; set; }
        public string OrderType { get; set; }
        public decimal Price { get; set; }
        public decimal Volume { get; set; }
    }
}
