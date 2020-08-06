using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.GateIo.Futures.Response;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.GateIo.Futures.Entities
{
    class GfOrderCreator
    {
        private string _portfolioNumber;

        private GfOrderCreator() { }

        public GfOrderCreator(string portfolioNumber)
        {
            _portfolioNumber = portfolioNumber;
        }

        public List<Order> Create(string data)
        {
            List<Order> orders = new List<Order>();

            var jt = JsonConvert.DeserializeObject<GfOrders>(data);

            foreach (var gfOrder in jt.Result)
            {

                if(gfOrder.Text == "web")
                {
                    continue;
                }

                var order = new Order();

                order.NumberUser = Convert.ToInt32(gfOrder.Text.Split('-')[1]);
                order.NumberMarket = gfOrder.Id.ToString();
                order.SecurityNameCode = gfOrder.Contract;
                order.Price = gfOrder.Price;
                order.Volume = Math.Abs(gfOrder.Size);
                order.Side = gfOrder.Size >= 0 ? Side.Buy : Side.Sell;
                order.TypeOrder = OrderPriceType.Limit;
                order.TimeCreate = TimeManager.GetDateTimeFromTimeStampSeconds(gfOrder.CreateTime);
                order.PortfolioNumber = _portfolioNumber;

                orders.Add(order);
            }

            return orders;
        }

        public List<MyTrade> CreateMyTrades(string data)
        {
            var myTradesOut = new List<MyTrade>();
            var jt = JsonConvert.DeserializeObject<GfMyTrades>(data);

            foreach (var myTrade in jt.Result)
            {
                var trade = new MyTrade();

                trade.SecurityNameCode = myTrade.Contract;
                trade.NumberOrderParent = myTrade.OrderId;
                trade.NumberTrade = myTrade.Id;
                trade.Price = Converter.StringToDecimal(myTrade.Price);
                trade.Volume = Math.Abs(myTrade.Size);
                trade.Time = TimeManager.GetDateTimeFromTimeStampSeconds(myTrade.CreateTime);
                trade.Side = myTrade.Size >= 0 ? Side.Buy : Side.Sell;

                myTradesOut.Add(trade);
            }

            return myTradesOut;
        }

        public List<Trade> TradesCreate(string data)
        {
            var trades = new List<Trade>();

            var jt = JsonConvert.DeserializeObject<GfTrades>(data);

            foreach (var trade in jt.Result)
            {
                var security = trade.Contract;

                var time = trade.CreateTime;

                var newTrade = new Trade();

                newTrade.Time = TimeManager.GetDateTimeFromTimeStampSeconds(time);
                newTrade.SecurityNameCode = security;
                newTrade.Price = Converter.StringToDecimal(trade.Price);
                newTrade.Id = trade.Id.ToString();
                newTrade.Volume = Math.Abs((decimal)trade.Size);
                newTrade.Side = trade.Size.ToString().StartsWith("-") ? Side.Sell : Side.Buy;


                trades.Add(newTrade);
            }

            return trades;
        }
    }
}
