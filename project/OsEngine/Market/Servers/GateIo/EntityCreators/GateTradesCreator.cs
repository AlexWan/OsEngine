using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Market.Servers.Entity;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.GateIo.EntityCreators
{
    class GateTradesCreator
    {
        public List<Trade> Create(string data)
        {
            var trades = new List<Trade>();

            var jt = JObject.Parse(data);

            var tradesData = (JArray)jt["params"];
            
            foreach (var trade in tradesData[1])
            {
                var security = tradesData[0].ToString();

                var time = trade["time"].Value<long>();

                var newTrade = new Trade();

                newTrade.Time = TimeManager.GetDateTimeFromTimeStampSeconds(time);
                newTrade.SecurityNameCode = security;
                newTrade.Price = trade["price"].Value<decimal>();
                newTrade.Id = trade["id"].ToString();
                newTrade.Side = trade["type"].ToString() == "sell" ? Side.Sell : Side.Buy;
                newTrade.Volume = trade["amount"].Value<decimal>();

                trades.Add(newTrade);
            }

            return trades;
        }
    }
}
