using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Market.Servers.Bybit.Entities;
using OsEngine.Market.Servers.Bybit.Utilities;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Services;
using System;
using System.Collections.Generic;
using System.Threading;

namespace OsEngine.Market.Servers.Bybit.EntityCreators
{
    public static class BybitTradesCreator
    {
        public static List<Trade> Create(JToken data)
        {
            var trades = new List<Trade>();

            var data_items = data.Children();

            foreach (var data_item in data_items)
            {
                var trade = new Trade();

                DateTime time = data_item.SelectToken("time").Value<DateTime>();

                trade.Time = time;
                trade.SecurityNameCode = data_item.SelectToken("symbol").Value<string>();
                trade.Price = data_item.SelectToken("price").Value<decimal>();
                trade.Id = data_item.SelectToken("id").ToString();
                trade.Side = data_item.SelectToken("side").ToString() == "Sell" ? Side.Sell : Side.Buy;
                trade.Volume = data_item.SelectToken("qty").Value<decimal>();

                trades.Add(trade);
            }

            return trades;
        }



        public static List<Trade> GetTradesCollection(Client client, string security, int limit, int from_id)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();

            parameters.Add("symbol", security);
            parameters.Add("limit", limit.ToString());

            if (from_id != -1)
            {
                parameters.Add("from", from_id.ToString());
            }

            JToken account_response = BybitRestRequestBuilder.CreatePrivateGetQuery(client, "/v2/public/trading-records", parameters);

            string isSuccessfull = account_response.SelectToken("ret_msg").Value<string>();

            if (isSuccessfull == "OK")
            {
                var trades = BybitTradesCreator.Create(account_response.SelectToken("result"));

                return trades;
            }

            return null;
        }

        public static List<Trade> CreateFromWS(JToken data)
        {
            var trades = new List<Trade>();

            foreach (var data_item in data)
            {
                var trade = new Trade();

                DateTime time = data_item.SelectToken("timestamp").Value<DateTime>();

                trade.Time = time;
                trade.SecurityNameCode = data_item.SelectToken("symbol").Value<string>();
                trade.Price = data_item.SelectToken("price").Value<decimal>();
                trade.Id = data_item.SelectToken("trade_id").ToString();
                trade.Side = data_item.SelectToken("side").ToString() == "Sell" ? Side.Sell : Side.Buy;
                trade.Volume = data_item.SelectToken("size").Value<decimal>();
                

                trades.Add(trade);
            }

            return trades;
        }

        public static List<MyTrade> CreateMyTrades(JToken data)
        {
            var trades = new List<MyTrade>();

            foreach (var data_item in data)
            {
                var trade = new MyTrade();

                DateTime time = data_item.SelectToken("trade_time").Value<DateTime>();

                trade.Time = time;
                trade.SecurityNameCode = data_item.SelectToken("symbol").Value<string>();
                trade.Price = data_item.SelectToken("price").Value<decimal>();
                trade.NumberTrade = data_item.SelectToken("exec_id").ToString();
                trade.NumberOrderParent = data_item.SelectToken("order_id").ToString();
                trade.Side = data_item.SelectToken("side").ToString() == "Sell" ? Side.Sell : Side.Buy;
                trade.Volume = data_item.SelectToken("exec_qty").Value<decimal>();


                trades.Add(trade);
            }

            return trades;
        }
    }
}
