using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using System;

namespace OsEngine.Market.Servers.FTX.EntityCreators
{
    class FTXOrderCreator
    {
        private const string IdPath = "id";
        private const string TimePath = "createdAt";
        private const string StatusPath = "status";
        private const string PricePath = "price";
        private const string SizePath = "size";
        private const string SecurityNamePath = "market";
        private const string SidePath = "side";
        private const string OrderTypePath = "type";

        public Order Create(JToken data)
        {
            var order = new Order();
            var orderMarketId = data.SelectToken(IdPath).ToString();
            var status = data.SelectToken(StatusPath).ToString();

            order.NumberMarket = orderMarketId;
            order.TimeCallBack = data.SelectToken(TimePath).Value<DateTime>();
            order.Price = data.SelectToken(PricePath).Value<decimal?>() ?? default;
            order.SecurityNameCode = data.SelectToken(SecurityNamePath).ToString();
            order.Side = data.SelectToken(SidePath).ToString() == "sell" ?
                Side.Sell :
                Side.Buy;
            order.Volume = data.SelectToken(SizePath).Value<decimal>();
            order.TypeOrder = data.SelectToken(OrderTypePath).ToString() == "limit" ?
                OrderPriceType.Limit :
                OrderPriceType.Market;
            order.State = ConvertOrderStatus(status);

            return order;
        }

        private OrderStateType ConvertOrderStatus(string status)
        {
            switch (status)
            {
                case "new":
                    return OrderStateType.Activ;
                case "open":
                    return OrderStateType.Patrial;
                case "closed":
                    return OrderStateType.Done;
                default:
                    return OrderStateType.None;
            }
        }
    }
}
