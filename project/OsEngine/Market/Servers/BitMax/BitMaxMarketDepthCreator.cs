using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Market.Services;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace OsEngine.Market.Servers.BitMax
{
    class BitMaxMarketDepthCreator : BaseMarketDepthUpdater
    {
        public MarketDepth Create(string data)
        {
            var depth = JsonConvert.DeserializeAnonymousType(data, new Depth());

            var need = _allDepths.Find(d => d.SecurityNameCode == depth.Symbol);

            if (need == null)
            {
                return CreateNew(depth);
            }

            return Update(depth);
        }

        private List<MarketDepth> _allDepths = new List<MarketDepth>();

        private MarketDepth CreateNew(Depth quotes)
        {
            var newDepth = new MarketDepth();

            newDepth.Time = DateTime.UtcNow;

            newDepth.SecurityNameCode = quotes.Symbol;

            var needDepth = _allDepths.Find(d => d.SecurityNameCode == newDepth.SecurityNameCode);

            if (needDepth != null)
            {
                _allDepths.Remove(needDepth);
            }

            var bids = quotes.Data.Bids;
            var asks = quotes.Data.Asks;

            foreach (var bid in bids)
            {
                newDepth.Bids.Add(new MarketDepthLevel()
                {
                    Price = bid[0].ToDecimal(),
                    Bid = bid[1].ToDecimal(),
                });
            }

            foreach (var ask in asks)
            {
                newDepth.Asks.Add(new MarketDepthLevel()
                {
                    Price = ask[0].ToDecimal(),
                    Ask = ask[1].ToDecimal(),
                });
            }

            _allDepths.Add(newDepth);

            return newDepth.GetCopy();
        }

        private MarketDepth Update(Depth quotes)
        {
            var needDepth = _allDepths.Find(d => d.SecurityNameCode == quotes.Symbol);

            if (needDepth == null)
            {
                throw new ArgumentNullException("BitMax: MarketDepth for updates not found");
            }

            if (quotes.Data.Bids != null)
            {
                var bidsLevels = quotes.Data.Bids;

                foreach (var bidLevel in bidsLevels)
                {
                    decimal price = bidLevel[0].ToDecimal();
                    decimal bid = bidLevel[1].ToDecimal();

                    if (bid != 0)
                    {
                        InsertLevel(price, bid, Side.Buy, needDepth);
                    }
                    else
                    {
                        DeleteLevel(price, Side.Buy, needDepth);
                    }
                }
                SortBids(needDepth.Bids);
            }

            if (quotes.Data.Asks!= null)
            {
                var asksLevels = quotes.Data.Asks;

                foreach (var askLevel in asksLevels)
                {
                    decimal price = askLevel[0].ToDecimal();
                    decimal ask = askLevel[1].ToDecimal();

                    if (ask != 0)
                    {
                        InsertLevel(price, ask, Side.Sell, needDepth);
                    }
                    else
                    {
                        DeleteLevel(price, Side.Sell, needDepth);
                    }
                }
                SortAsks(needDepth.Asks);
            }

            return needDepth.GetCopy();
        }
    }
}
