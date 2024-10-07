using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.MoexFixFastTwimeFutures.Entity
{
    public class MarketDataGroup
    {
        public string FeedType { get; set; }
        public string MarketID { get; set; }
        public string Label { get; set; }
        public List<FastConnection> FastConnections { get; set; }
    }

    public class FastConnection
    {
        public string Type { get; set; }
        public string Feed { get; set; }
        public string MulticastIP { get; set; }
        public string SrsIP { get; set; }
        public int Port { get; set; }
    }
}
