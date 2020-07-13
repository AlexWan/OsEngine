using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OsEngine.Market.Servers.FTX.EntityCreators
{
    class FTXTradesCreator
    {
        private const string ResultPath = "result";
        private const string DataPath = "data";
        private const string SecurityNamePath = "market";
        private const string OrderIdPath = "orderId";
        private const string TradeIdPath = "tradeId";

        private const string PricePath = "price";
        private const string SizePath = "size";
        private const string IdPath = "id";
        private const string TimePath = "time";
        private const string SidePath = "side";

        public List<Trade> Create(JToken jt, string securityName)
        {
            var trades = new List<Trade>();
            var jProperties = jt.SelectTokens(ResultPath).Children();

            foreach (var jProperty in jProperties.Reverse())
            {
                var newTrade = new Trade();

                var time = DateTime.Parse(jProperty.SelectToken(TimePath).ToString()).ToUniversalTime();

                newTrade.Time = time;
                newTrade.SecurityNameCode = securityName;
                newTrade.Price = jProperty.SelectToken(PricePath).Value<decimal>();
                newTrade.Id = jProperty.SelectToken(IdPath).ToString();
                newTrade.Side = jProperty.SelectToken(SidePath).ToString() == "sell" ? Side.Sell : Side.Buy;
                newTrade.Volume = jProperty.SelectToken(SizePath).Value<decimal>();

                trades.Add(newTrade);
            }

            return trades;
        }

        public Trade Create (JToken jt)
        {
            var trade = new Trade();

            var securityName = jt.SelectToken(SecurityNamePath).ToString();
            var data = jt.SelectToken(DataPath).Children().First();

            trade.Time = data.SelectToken(TimePath).Value<DateTime>();
            trade.SecurityNameCode = securityName;
            trade.Price = data.SelectToken(PricePath).Value<decimal>();
            trade.Id = data.SelectToken(IdPath).ToString();
            trade.Side = data.SelectToken(SidePath).ToString() == "sell" ? Side.Sell : Side.Buy;
            trade.Volume = data.SelectToken(SizePath).Value<decimal>();

            return trade;
        }

        public MyTrade CreateMyTrade(JToken jt)
        {
            var data = jt.SelectToken(DataPath);
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