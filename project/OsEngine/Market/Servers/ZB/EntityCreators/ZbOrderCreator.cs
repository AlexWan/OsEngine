using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.ZB.EntityCreators
{
    class ZbOrderCreator
    {
        private const string NamePath = "channel";
        private const string StatusPath = "success";
        private const string NumberPath = "no";
        private const string DataPath = "data";

        private readonly List<Order> _myOrders = new List<Order>();

        public void AddMyOrder(Order order)
        {
            _myOrders.Add(order);
        }

        public Order Create(string data)
        {
            var jt = JToken.Parse(data);

            var stateQuery = GetValueByKey<string>(StatusPath, jt);

            var channelData = GetValueByKey<string>(NamePath, jt).Split('_');

            var channel = channelData[1];

            var numberUser = GetValueByKey<int>(NumberPath, jt);

            var order = _myOrders.Find(o => o.NumberUser == numberUser);

            if (order == null)
            {
                return null;
            }

            var innerData = jt.SelectToken(DataPath);

            if (channel == "order")
            {
                if (stateQuery == "True")
                {
                    order.State = OrderStateType.Activ;
                    order.NumberMarket = innerData["entrustId"].Value<string>();
                    NewTrackedOrder?.Invoke(order);
                }
                else
                {
                    order.State = OrderStateType.Fail;
                }
            }
            else if (channel == "getorder")
            {
                var state = innerData["status"].Value<string>();
                var time = TimeManager.GetDateTimeFromTimeStamp(innerData["trade_date"].Value<long>());

                if (state == "3")
                {
                    order.State = OrderStateType.Activ;
                    order.TimeCreate = time;
                }
                if (state == "0")
                {
                    order.State = OrderStateType.Patrial;
                }
                else if (state == "1")
                {
                    order.State = OrderStateType.Cancel;
                    DeleteOrder(order);
                }
                else if (state == "2")
                {
                    order.State = OrderStateType.Done;
                    DeleteOrder(order);
                }

                var tradeVolume = innerData["trade_amount"].Value<decimal>();
                var volumeExecute = order.VolumeExecute;

                if (tradeVolume > volumeExecute)
                {
                    NewMyTrade?.Invoke(CreateMyTrade(innerData, order, tradeVolume - volumeExecute));
                }
            }

            return order;
        }

        private MyTrade CreateMyTrade(JToken jt, Order order, decimal tradeVolume)
        {
            var trade = new MyTrade();

            trade.SecurityNameCode = order.SecurityNameCode;
            trade.NumberOrderParent = order.NumberMarket;
            trade.Price = jt["trade_price"].Value<decimal>();
            trade.Volume = tradeVolume;
            trade.Side = jt["type"].Value<int>() == 1 ? Side.Buy : Side.Sell;
            trade.NumberTrade = jt["id"].Value<string>();
            trade.Time = TimeManager.GetDateTimeFromTimeStamp(jt["trade_date"].Value<long>());

            return trade;
        }

        private void DeleteOrder(Order order)
        {
            DeleteTrackedOrder?.Invoke(order);
            _myOrders.Remove(order);
        }

        private T GetValueByKey<T>(string key, JToken jt)
        {
            return jt[key].Value<T>();
        }

        public event Action<Order> NewTrackedOrder;
        public event Action<Order> DeleteTrackedOrder;
        public event Action<MyTrade> NewMyTrade;
    }
}
