using OsEngine.Entity;
using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.CoinEx.Spot.Entity
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
        public long checksum { get; set; }

        public static explicit operator MarketDepth(CexWsDepth cexDepth)
        {
            MarketDepth depth = new MarketDepth();
            depth.Time = CoinExServerRealization.ConvertToDateTimeFromUnixFromMilliseconds(cexDepth.updated_at);
            for (int k = 0; k < cexDepth.bids.Count; k++)
            {
                (string price, string size) = (cexDepth.bids[k][0], cexDepth.bids[k][1]);
                MarketDepthLevel newBid = new MarketDepthLevel();
                newBid.Price = price.ToString().ToDecimal();
                newBid.Bid = size.ToString().ToDecimal();
                if (newBid.Bid > 0)
                {
                    depth.Bids.Add(newBid);
                }
            }

            for (int k = 0; k < cexDepth.asks.Count; k++)
            {
                (string price, string size) = (cexDepth.asks[k][0], cexDepth.asks[k][1]);
                MarketDepthLevel newAsk = new MarketDepthLevel();
                newAsk.Price = price.ToString().ToDecimal();
                newAsk.Ask = size.ToString().ToDecimal();
                if (newAsk.Ask > 0)
                {
                    depth.Asks.Add(newAsk);
                }
            }

            return depth;
        }
    }

}
