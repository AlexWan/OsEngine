using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Market.Servers.Entity;
using System;

namespace OsEngine.Market.Servers.FTX.EntityCreators
{
    public class FTXMarketDepthCreator
    {
        private const string BidsPath = "bids";
        private const string AsksPath = "asks";
        private const string TimePath = "time";

        public MarketDepth Create(JToken data)
        {
            var marketDepth = new MarketDepth();
            var time = data.SelectToken(TimePath).Value<decimal>();
            var bids = data.SelectTokens(BidsPath).Children();
            var asks = data.SelectTokens(AsksPath).Children();

            marketDepth.Time = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(time)).ToLocalTime();

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

            return marketDepth;
        }
    }
}