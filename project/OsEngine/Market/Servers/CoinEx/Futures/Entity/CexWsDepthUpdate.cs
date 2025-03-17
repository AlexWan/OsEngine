using System.Collections.Generic;

namespace OsEngine.Market.Servers.CoinEx.Futures.Entity
{
    struct CexWsDepthUpdate
    {
        public string market { get; set; }

        public bool is_full { get; set; }

        public CexWsDepth depth { get; set; }
    }

    struct CexWsDepth
    {
        // (Seller price | Seller size. During incremental push, a value of 0 indicates the depth at which the price needs to be deleted.)
        public List<string[]> asks { get; set; }

        // (Buyer price | Buyer size. During incremental push, a value of 0 indicates the depth at which the price needs to be deleted.)
        public List<string[]> bids { get; set; }

        // Latest price
        public string last { get; set; }

        // Timestamp (millisecond)
        public long updated_at { get; set; }

        // Data checksum
        public string checksum { get; set; }
    }
}