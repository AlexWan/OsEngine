using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.FTX.EntityCreators
{
    class FTXOrderCreator
    {
        private const string ResultPath = "result";
        private const string DataPath = "data";
        private const string IdPath = "id";
        private const string TimePath = "createdAt";
        private const string StatusPath = "status";

        private readonly Dictionary<string, Order> _myOrders = new Dictionary<string, Order>();

        public void AddMyOrder(Order order, JToken jt)
        {
            var result = jt.SelectToken(ResultPath);
            var orderMarketId = result.SelectToken(IdPath).ToString();

            order.NumberMarket = orderMarketId;
            order.TimeCallBack = result.SelectToken(TimePath).Value<DateTime>();
            order.TimeCreate= result.SelectToken(TimePath).Value<DateTime>();

            _myOrders.Add(orderMarketId, order);
        }

        public void RemoveMyOrder(Order order)
        {
            _myOrders.Remove(order.NumberMarket);
        }

        public Order Create(JToken jt)
        {
            var data = jt.SelectToken(DataPath);
            var orderMarketId = data.SelectToken(IdPath).ToString();
            var order = _myOrders[orderMarketId];
            var status = data.SelectToken(StatusPath).ToString();

            order.State = ConvertOrderStatus(status);
            
            return order;
        }

        private OrderStateType ConvertOrderStatus(string status)
        {
            switch (status)
            {
                case "new":
                    return OrderStateType.Pending;
                case "open":
                    return OrderStateType.Activ;
                case "closed":
                    return OrderStateType.Done;
                default:
                    return OrderStateType.None;
            }
        }
    }
}
