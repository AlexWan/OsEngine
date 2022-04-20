using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Bybit.EntityCreators
{
    public static class BybitOrderCreator
    {
        public static List<Order> Create(JToken data)
        {
            var orders = new List<Order>();

            foreach (var data_item in data)
            {
                var order = new Order();

                if (data_item.SelectToken("order_type").Value<string>() == "Limit")
                    order.TypeOrder = OrderPriceType.Limit;
                if (data_item.SelectToken("order_type").Value<string>() == "Market")
                    order.TypeOrder = OrderPriceType.Market;

                DateTime time;
                try
                {
                    time = data_item.SelectToken("timestamp").Value<DateTime>();
                }
                catch
                {
                    try
                    {
                        time = data_item.SelectToken("update_time").Value<DateTime>();
                    }
                    catch
                    {
                        time = data_item.SelectToken("create_time").Value<DateTime>();
                    }
                }

                string status = data_item.SelectToken("order_status").Value<string>();

                //Created - order has been accepted by the system but not yet put through the matching engine
                //Rejected - order has been triggered but failed to be placed(e.g.due to insufficient margin)
                //New - order has been placed successfully
                //PartiallyFilled
                //Filled
                //Cancelled
                //PendingCancel - matching engine has received the cancelation request but it may not be canceled successfully

                if(status == "New" ||
                    status == "PartiallyFilled" ||
                    status == "Created")
                {
                    order.State = OrderStateType.Activ;
                }
                else if (status == "Filled")
                {
                    order.State = OrderStateType.Done;
                }
                else if (status == "Rejected")
                {
                    order.State = OrderStateType.Fail;
                }
                else if (status == "Cancelled" ||
                    status == "PendingCancel")
                {
                    order.State = OrderStateType.Cancel;
                }

                order.TimeCreate = time;
                order.SecurityNameCode = data_item.SelectToken("symbol").Value<string>();
                order.Price = data_item.SelectToken("price").Value<decimal>();
                order.NumberMarket = data_item.SelectToken("order_id").ToString();
                order.Side = data_item.SelectToken("side").ToString() == "Sell" ? Side.Sell : Side.Buy;
                order.Volume = data_item.SelectToken("qty").Value<decimal>();

                int num;
                bool isNum = int.TryParse(data_item.SelectToken("order_link_id").Value<string>(), out num);
                if (isNum)
                    order.NumberUser = Convert.ToInt32(data_item.SelectToken("order_link_id").Value<string>());


                orders.Add(order);
                }

            return orders;
                }
    }
}
