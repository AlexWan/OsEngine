using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Market.Servers.Entity;
using System;
using System.Linq;

namespace OsEngine.Market.Servers.ZB.EntityCreators
{
    public class ZbMarketDepthCreator
    {
        private const string NamePath = "channel";
        private const string BidsPath = "bids";
        private const string AsksPath = "asks";
        private const string TimePath = "timestamp";

        public MarketDepth Create(string data)
        {
            var jt = JToken.Parse(data);

            var time = jt[TimePath].ToString();

            var newDepth = new MarketDepth();

            newDepth.Time = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(time));

            newDepth.SecurityNameCode = jt[NamePath].ToString().Split('_')[0];

            var bids = jt.SelectTokens(BidsPath).Children();

            var asks = jt.SelectTokens(AsksPath).Children();

            foreach (var bid in bids)
            {
                newDepth.Bids.Add(new MarketDepthLevel()
                {
                    Price = bid[0].Value<decimal>(),
                    Bid = bid[1].Value<decimal>(),
                });
            }

            foreach (var ask in asks.Reverse())
            {
                newDepth.Asks.Add(new MarketDepthLevel()
                {
                    Price = ask[0].Value<decimal>(),
                    Ask = ask[1].Value<decimal>(),
                });
            }

            return newDepth;
        }
    }
}