using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Market.Services;
using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.GateIo.EntityCreators
{
    class GateMarketDepthCreator : BaseMarketDepthUpdater
    {
        //{ "method": "depth.update",
        //"params": [true,
        //{"asks": [["218.42", "4.9363841455"], ["218.47", "1.4425"], ["218.53", "23.43"], ["218.57", "9.254"], ["218.64", "4.4376"], ["218.66", "1.82745046"], ["218.68", "3.8017"], ["218.72", "2.258"], ["218.73", "3.6649"], ["218.78", "27.51"], ["218.8", "1.15211108"], ["218.86", "2.969"], ["218.87", "3.10115136"], ["218.88", "0.00029001"], ["218.89", "9"], ["218.94", "0.3003"], ["218.96", "0.02776"], ["218.98", "51.262273"], ["219", "85.5001"], ["219.01", "5"], ["219.03", "5"], ["219.04", "0.467593"], ["219.14", "32.14"], ["219.16", "5"], ["219.26", "0.01627154"], ["219.4", "0.0075"], ["219.42", "4.59"], ["219.43", "4.29"], ["219.44", "7.76"], ["219.47", "10.68"]],
        //"bids": [["218.18", "1"], ["218.16", "6.6699"], ["218.15", "14.5652"], ["218.07", "9.887"], ["218.05", "1.4425"], ["218", "6.83740861"], ["217.98", "1.83487221"], ["217.93", "1.9358"], ["217.85", "0.03600217"], ["217.81", "0.0075"], ["217.78", "0.713"], ["217.76", "10.0242"], ["217.73", "5"], ["217.72", "9.001"], ["217.71", "5"], ["217.66", "5"], ["217.65", "0.1863"], ["217.61", "5"], ["217.54", "34.866"], ["217.53", "6.19569745"], ["217.48", "5.00552063"], ["217.45", "0.00632"], ["217.38", "27.15"], ["217.36", "1.48"], ["217.28", "0.0075"], ["217.26", "0.01627154"], ["217.25", "0.00484134"], ["217.13", "0.030506"], ["217.12", "1.37"], ["217.11", "1.08"]]},
        //"ETH_USDT"],
        //"id": null}

        public MarketDepth Create(string data)
        {
            var jt = JObject.Parse(data);

            var quotes = (JArray)jt["params"];

            if (quotes[0].ToString() == "True")
            {
                return CreateNew(quotes);
            }

            return Update(quotes);
        }

        private List<MarketDepth> _allDepths = new List<MarketDepth>();

        private MarketDepth CreateNew(JArray quotes)
        {
            var newDepth = new MarketDepth();

            newDepth.Time = DateTime.UtcNow;

            newDepth.SecurityNameCode = quotes[2].ToString();

            var needDepth = _allDepths.Find(d => d.SecurityNameCode == newDepth.SecurityNameCode);

            if (needDepth != null)
            {
                _allDepths.Remove(needDepth);
            }

            var bids = quotes[1]["bids"].Children();
            var asks = quotes[1]["asks"].Children();

            foreach (var bid in bids)
            {
                newDepth.Bids.Add(new MarketDepthLevel()
                {
                    Price = bid[0].Value<decimal>(),
                    Bid = bid[1].Value<decimal>(),
                });
            }

            foreach (var ask in asks)
            {
                newDepth.Asks.Add(new MarketDepthLevel()
                {
                    Price = ask[0].Value<decimal>(),
                    Ask = ask[1].Value<decimal>(),
                });
            }

            _allDepths.Add(newDepth);

            return newDepth.GetCopy();
        }

        private MarketDepth Update(JArray quotes)
        {
            var needDepth = _allDepths.Find(d => d.SecurityNameCode == quotes[2].ToString());

            if (needDepth == null)
            {
                throw new ArgumentNullException("GateIo: MarketDepth for updates not found");
            }

            if (quotes[1]["bids"] != null)
            {
                var bidsLevels = quotes[1]["bids"].Children();

                foreach (var bidLevel in bidsLevels)
                {
                    decimal price = bidLevel[0].Value<decimal>();
                    decimal bid = bidLevel[1].Value<decimal>();

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

            if (quotes[1]["asks"] != null)
            {
                var asksLevels = quotes[1]["asks"].Children();

                foreach (var askLevel in asksLevels)
                {
                    decimal price = askLevel[0].Value<decimal>();
                    decimal ask = askLevel[1].Value<decimal>();

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
