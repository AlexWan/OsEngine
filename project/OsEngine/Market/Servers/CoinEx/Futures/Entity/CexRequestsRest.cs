using OsEngine.Entity;
using System;
using System.Collections.Generic;
using OsEngine.Market.Servers.CoinEx.Spot.Entity.Enums;
using System.Globalization;
using System.Text;

namespace OsEngine.Market.Servers.CoinEx.Futures.Entity
{
    public class CexRequestRest
    {
        public Dictionary<string, Object> parameters = new Dictionary<string, Object>();

        public CexRequestRest() { }

        public override string ToString()
        {
            return "?" + createQueryString(parameters);
        }

        public static string createQueryString(Dictionary<string, Object> args)
        {
            StringBuilder queryBuilder = new StringBuilder();
            foreach (KeyValuePair<string, Object> arg in args)
            {
                queryBuilder.AppendFormat("{0}={1}&", arg.Key, arg.Value);
            }
            return queryBuilder.ToString().Trim(new char[] { '&' });
        }
    }

    // https://docs.coinex.com/api/v2/spot/order/http/list-pending-order
    public class CexRequestPendingOrders : CexRequestRest
    {
        public CexRequestPendingOrders(string marketType, string market = null, long? userOrderId = null, int limit = 1000)
        {
            parameters.Add("market_type", marketType);
            parameters.Add("limit", limit);
            if (userOrderId != null)
            {
                parameters.Add("client_id", userOrderId);
            }
            if (market != null)
            {
                parameters.Add("market", market);
            }
        }
    }

    // https://docs.coinex.com/api/v2/futures/order/http/cancel-all-order
    public class CexRequestCancelAllOrders : CexRequestRest
    {
        public CexRequestCancelAllOrders(string marketType, string security)
        {
            parameters.Add("market_type", marketType);
            parameters.Add("market", security);
        }
    }

    // https://docs.coinex.com/api/v2/spot/order/http/cancel-order
    public class CexRequestCancelOrder : CexRequestRest
    {
        public CexRequestCancelOrder(string marketType, string orderId, string market)
        {
            parameters.Add("market_type", marketType);
            parameters.Add("market", market);
            parameters.Add("order_id", (long)orderId.ToString().ToDecimal());
        }
    }

    // https://docs.coinex.com/api/v2/spot/order/http/put-order#http-request
    public class CexRequestSendOrder : CexRequestRest
    {
        public CexRequestSendOrder(string marketType, Order order)
        {
            string cexOrderSide = order.Side switch
            {
                Side.Buy => CexOrderSide.BUY.ToString(),
                _ => CexOrderSide.SELL.ToString(),
            };
            parameters.Add("market", order.SecurityNameCode);
            parameters.Add("market_type", marketType);
            parameters.Add("side", cexOrderSide);
            parameters.Add("amount", order.Volume.ToString(CultureInfo.InvariantCulture).Replace(",", "."));
            parameters.Add("client_id", order.NumberUser.ToString());
            //parameters.Add("ccy", order.SecurityClassCode);

            if (order.TypeOrder == OrderPriceType.Limit)
            {
                parameters.Add("type", CexOrderType.LIMIT.ToString());
                parameters.Add("price", order.Price.ToString(CultureInfo.InvariantCulture).Replace(",", "."));
            }
            else if (order.TypeOrder == OrderPriceType.Market)
            {
                parameters.Add("type", CexOrderType.MARKET.ToString());
            }
        }
    }

    // https://docs.coinex.com/api/v2/spot/order/http/get-order-status
    public class CexRequestOrderStatus : CexRequestRest
    {
        public CexRequestOrderStatus(string orderId, string market)
        {
            parameters.Add("market", market);
            parameters.Add("order_id", (long)orderId.ToDecimal());
        }
    }

    // https://docs.coinex.com/api/v2/spot/deal/http/list-user-order-deals#http-request
    public class CexRequestOrderDeals : CexRequestRest
    {
        public CexRequestOrderDeals(string marketType, string orderId, string market, int limit = 100)
        {
            parameters.Add("market", market);
            parameters.Add("order_id", (long)orderId.ToDecimal());
            parameters.Add("market_type", marketType);
            parameters.Add("limit", limit);
        }
    }

    // https://docs.coinex.com/api/v2/spot/order/http/edit-order
    public class CexRequestEditOrder : CexRequestRest
    {
        public CexRequestEditOrder(string marketType, Order order, decimal newPrice)
        {
            parameters.Add("market", order.SecurityNameCode);
            parameters.Add("order_id", (long)order.NumberMarket.ToDecimal());
            parameters.Add("market_type", marketType);
            parameters.Add("price", newPrice.ToString(CultureInfo.InvariantCulture).Replace(",", "."));
        }
    }

    // https://docs.coinex.com/api/v2/spot/market/http/list-market-kline
    public class CexRequestGetKLines : CexRequestRest
    {
        public CexRequestGetKLines(string market, string period, int limit, string priceType = "latest_price")
        {
            parameters.Add("market", market);
            parameters.Add("period", period);
            parameters.Add("limit", limit);
            parameters.Add("price_type", priceType);
        }
    }

    // https://docs.coinex.com/api/v2/spot/market/http/list-market-deals
    public class CexRequestGetDeals : CexRequestRest
    {
        public CexRequestGetDeals(string market, int limit = 1000, long lastId = 0)
        {
            parameters.Add("market", market);
            parameters.Add("last_id", lastId);
            parameters.Add("limit", limit);
        }
    }

    // https://docs.coinex.com/api/v2/futures/position/http/list-pending-position
    public class CexRequestGetPendingPosition : CexRequestRest
    {
        public CexRequestGetPendingPosition(string marketType, string market = null, int limit = 1000, int page = 1)
        {
            parameters.Add("market_type", marketType);
            parameters.Add("limit", limit);
            if (!string.IsNullOrEmpty(market))
            {
                parameters.Add("market", market);
            }
            if (page > 1)
            {
                parameters.Add("page", page);
            }
        }
    }
}