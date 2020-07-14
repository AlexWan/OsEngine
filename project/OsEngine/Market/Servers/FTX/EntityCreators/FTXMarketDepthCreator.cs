using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.FTX.EntityCreators
{
    public class FTXMarketDepthCreator
    {
        private const string NamePath = "market";
        private const string BidsPath = "bids";
        private const string AsksPath = "asks";
        private const string TimePath = "time";
        private const string DataPath = "data";

        private Dictionary<string, MarketDepth> _securityMarketDepths = new Dictionary<string, MarketDepth>();

        public MarketDepth Create(JToken jt)
        {
            var securityNameCode = jt.SelectToken(NamePath).ToString();
            var marketDepth = new MarketDepth();
            var data = jt.SelectToken(DataPath);
            var time = data.SelectToken(TimePath).Value<decimal>();
            var bids = data.SelectTokens(BidsPath).Children();
            var asks = data.SelectTokens(AsksPath).Children();

            marketDepth = new MarketDepth();
            marketDepth.Time = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(time));
            marketDepth.SecurityNameCode = securityNameCode;

            foreach (var bid in bids)
            {
                marketDepth.Bids.Add(new MarketDepthLevel()
                {
                    Price = bid[0].Value<decimal>(),
                    Bid = bid[1].Value<decimal>(),
                });
            }

            foreach (var ask in asks)
            {
                marketDepth.Asks.Add(new MarketDepthLevel()
                {
                    Price = ask[0].Value<decimal>(),
                    Ask = ask[1].Value<decimal>(),
                });
            }

            if (_securityMarketDepths.ContainsKey(securityNameCode))
            {
                _securityMarketDepths[securityNameCode] = marketDepth;
            }
            else
            {
                _securityMarketDepths.Add(securityNameCode, marketDepth);
            }

            return marketDepth.GetCopy();
        }

        public MarketDepth Update(JToken jt)
        {
            var securityNameCode = jt.SelectToken(NamePath).ToString();
            var marketDepth = _securityMarketDepths[securityNameCode];
            var data = jt.SelectToken(DataPath);
            var time = data.SelectToken(TimePath).Value<decimal>();
            var bids = data.SelectTokens(BidsPath).Children();
            var asks = data.SelectTokens(AsksPath).Children();

            marketDepth.Time = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(time));

            foreach (var bid in bids)
            {
                var bidPrice = bid[0].Value<decimal>();
                var bidSize = bid[1].Value<decimal>();
                var marketDepthLevelForUpdate = marketDepth.Bids.Find(x => x.Price == bidPrice);

                if(marketDepthLevelForUpdate != null)
                {
                    if(bidSize != 0)
                    {
                        marketDepthLevelForUpdate.Bid = bidSize;
                    }
                    else
                    {
                        marketDepth.Bids.Remove(marketDepthLevelForUpdate);
                    }
                }
                else
                {
                    var index = marketDepth.Bids.FindIndex(x => x.Price < bidPrice);
                    marketDepth.Bids.Insert(index >= 0 ? index : marketDepth.Bids.Count, new MarketDepthLevel()
                    {
                        Price = bidPrice,
                        Bid = bidSize,
                    });
                }
            }

            foreach (var ask in asks)
            {
                var askPrice = ask[0].Value<decimal>();
                var askSize = ask[1].Value<decimal>();
                var marketDepthLevelForUpdate = marketDepth.Asks.Find(x => x.Price == askPrice);

                if (marketDepthLevelForUpdate != null)
                {
                    if (askSize != 0)
                    {
                        marketDepthLevelForUpdate.Ask = askSize;
                    }
                    else
                    {
                        marketDepth.Asks.Remove(marketDepthLevelForUpdate);
                    }
                }
                else
                {
                    var index = marketDepth.Asks.FindIndex(x => x.Price > askPrice);
                    marketDepth.Asks.Insert(index >= 0 ? index : marketDepth.Asks.Count,new MarketDepthLevel()
                    {
                        Price = askPrice,
                        Ask = askSize,
                    });
                }
            }

            return marketDepth.GetCopy();
        }
    }
}