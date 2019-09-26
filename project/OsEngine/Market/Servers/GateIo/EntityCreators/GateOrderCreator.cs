using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.GateIo.EntityCreators
{
    public class GateOrderCreator
    {
        private string _portfolioNumber;

        private GateOrderCreator(){}

        public GateOrderCreator(string portfolioNumber)
        {
            _portfolioNumber = portfolioNumber;
        }
        //{"method": "order.update",
        //"params": [1, {"id": 6313386586, "market": "LTC_USDT", "tif": 1, "user": 2786800,
        //"ctime": 1569389575.1675429, "mtime": 1569389575.1675429, "price": "50", "amount": "0.1",
        //"iceberg": "0", "left": "0.1", "deal_point_fee": "0", "orderType": 1, "type": 2, "dealFee": "0",
        //"filledAmount": "0", "filledTotal": "0", "text": "t-124"}],
        //"id": null}

        private Dictionary<string, decimal> _myOrderVolumes = new Dictionary<string, decimal>();

        public Order Create(string data)
        {
            var jt = JToken.Parse(data)["params"];

            var stateQuery = jt[0].Value<int>();

            var channelData = jt[1];

            var order = new Order();

            order.NumberUser = Convert.ToInt32(channelData["text"].Value<string>().Split('-')[1]);
            order.NumberMarket = channelData["id"].Value<string>();
            order.SecurityNameCode = channelData["market"].Value<string>();
            order.Price = channelData["price"].Value<decimal>();
            order.Volume = channelData["amount"].Value<decimal>();
            order.Side = channelData["type"].Value<int>() == 2 ? Side.Buy : Side.Sell;
            order.TypeOrder = OrderPriceType.Limit;
            order.TimeCreate = TimeManager.GetDateTimeFromTimeStampSeconds(channelData["ctime"].Value<long>());
            order.PortfolioNumber = _portfolioNumber;

            var filledAmount = channelData["filledAmount"].Value<decimal>();

            if (filledAmount != 0)
            {
                if (!_myOrderVolumes.ContainsKey(order.NumberMarket))
                {
                    if (filledAmount != order.Volume)
                    {
                        _myOrderVolumes.Add(order.NumberMarket, filledAmount);
                    }

                    NewMyTrade?.Invoke(CreateMyTrade(channelData, order, filledAmount));
                }
                else
                {
                    var needVolume = _myOrderVolumes[order.NumberMarket];
                    if (filledAmount > needVolume)
                    {
                        var currentTradeVolume = filledAmount - needVolume;
                        _myOrderVolumes[order.NumberMarket] += currentTradeVolume;
                        NewMyTrade?.Invoke(CreateMyTrade(channelData, order, currentTradeVolume));
                    }
                }
            }

            if (stateQuery == 1)
            {
                order.State = OrderStateType.Activ;
            }

            if (stateQuery == 3 && filledAmount == order.Volume)
            {
                order.State = OrderStateType.Done;
                _myOrderVolumes.Remove(order.NumberMarket);
            }
            if (stateQuery == 3 && filledAmount == 0)
            {
                order.State = OrderStateType.Cancel;
            }

            return order;
        }

        private MyTrade CreateMyTrade(JToken channelData, Order order, decimal tradeVolume)
        {
            var trade = new MyTrade();

            trade.SecurityNameCode = order.SecurityNameCode;
            trade.NumberOrderParent = order.NumberMarket;
            trade.NumberTrade = TimeManager.GetUnixTimeStampMilliseconds().ToString();
            trade.Price = order.Price;
            trade.Volume = tradeVolume;
            trade.Time = TimeManager.GetDateTimeFromTimeStampSeconds(channelData["mtime"].Value<long>());
            trade.Side = order.Side;

            return trade;
        }
        
        public event Action<MyTrade> NewMyTrade;
    }
}
