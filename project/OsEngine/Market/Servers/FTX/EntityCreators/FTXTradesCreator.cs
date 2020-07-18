using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.FTX.EntityCreators
{
    class FTXTradesCreator
    {
        private const string SecurityNamePath = "market";
        private const string OrderIdPath = "orderId";
        private const string TradeIdPath = "tradeId";

        private const string PricePath = "price";
        private const string SizePath = "size";
        private const string IdPath = "id";
        private const string TimePath = "time";
        private const string SidePath = "side";

        public List<Trade> Create (JToken data, string securityName, bool isUtcTime = false)
        {
            var trades = new List<Trade>();
            var jProperties = data.Children();

            foreach (var jProperty in jProperties)
            {
                var trade = new Trade();

                DateTime time = jProperty.SelectToken(TimePath).Value<DateTime>();
                if (isUtcTime)
                {
                    time = time.ToUniversalTime();
                }

                trade.Time = time;
                trade.SecurityNameCode = securityName;
                trade.Price = jProperty.SelectToken(PricePath).Value<decimal>();
                trade.Id = jProperty.SelectToken(IdPath).ToString();
                trade.Side = jProperty.SelectToken(SidePath).ToString() == "sell" ? Side.Sell : Side.Buy;
                trade.Volume = jProperty.SelectToken(SizePath).Value<decimal>();

                trades.Add(trade);
            }

            return trades;
        }

        public MyTrade CreateMyTrade(JToken data)
        {
            var trade = new MyTrade();

            trade.SecurityNameCode = data.SelectToken(SecurityNamePath).ToString();
            trade.NumberOrderParent = data.SelectToken(OrderIdPath).ToString();
            trade.Price = data.SelectToken(PricePath).Value<decimal>();
            trade.Volume = data.SelectToken(SizePath).Value<decimal>();
            trade.Side = data.SelectToken(SidePath).ToString() == "sell" ? Side.Sell : Side.Buy;
            trade.NumberTrade = data.SelectToken(TradeIdPath).ToString();
            trade.Time = data.SelectToken(TimePath).Value<DateTime>();

            return trade;
        }
    }
}