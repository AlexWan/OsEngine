using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.ZB.EntityCreators
{
    class ZbTradesCreator
    {
        private const string NamePath = "channel";
        private const string DataPath = "data";
        private const string PricePath = "price";
        private const string VolumePath = "amount";
        private const string IdPath = "tid";
        private const string TimePath = "date";
        private const string DirectionPath = "type";

        public List<Trade> Create(string data)
        {
            var trades = new List<Trade>();
            var jt = JToken.Parse(data);
            var tradesData = jt.SelectTokens(DataPath).Children();
            var security = jt[NamePath].ToString().Split('_')[0];

            foreach (var trade in tradesData)
            {
                var time = trade[TimePath].ToString();

                var newTrade = new Trade();

                newTrade.Time = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(time));
                newTrade.SecurityNameCode = security;
                newTrade.Price = trade[PricePath].Value<decimal>();
                newTrade.Id = trade[IdPath].ToString();
                newTrade.Side = trade[DirectionPath].ToString() == "sell" ? Side.Sell : Side.Buy;
                newTrade.Volume = trade[VolumePath].Value<decimal>();

                trades.Add(newTrade);
            }

            return trades;
        }
    }
}