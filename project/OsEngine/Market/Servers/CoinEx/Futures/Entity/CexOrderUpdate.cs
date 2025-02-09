using OsEngine.Entity;
using OsEngine.Market.Servers.CoinEx.Futures.Entity.Enums;
using System;

namespace OsEngine.Market.Servers.CoinEx.Futures.Entity
{
    // https://docs.coinex.com/api/v2/spot/order/ws/user-order#user-order-update-push
    // https://docs.coinex.com/api/v2/spot/order/http/list-pending-order#http-request
    // WS and REST
    struct CexOrderUpdate
    {
        public long order_id { get; set; }

        // Futures. Related stop order id
        public long stop_id { get; set; }

        // Market name
        public string market { get; set; }

        // Margin market name, null for non-margin markets
        public string margin_market { get; set; }

        // limit | market | maker_only | IOC | FOK
        public string type { get; set; }

        // Order side, buy or sell
        public string side { get; set; }

        // Amount - объём в единицах тикера
        // Value - объём в деньгах
        public string amount { get; set; }

        public string price { get; set; }

        // The remaining unfilled amount
        public string unfilled_amount { get; set; }

        // Futures. Filled volume
        public string filled_amount { get; set; }

        // Amount - объём в единицах тикера
        // Value - объём в деньгах
        public string filled_value { get; set; }

        public string taker_fee_rate { get; set; }

        public string maker_fee_rate { get; set; }

        // BaseCurrencyFee. Trading fee paid in base currency
        public string base_ccy_fee { get; set; }

        // QuoteCurrencyFee. Trading fee paid in quote currency
        public string quote_ccy_fee { get; set; }

        // DiscountCurrencyFee. Trading fee paid mainly in CET
        public string discount_ccy_fee { get; set; }

        // Futures. Trading fee charged
        public string FuturesFee { get; set; }

        // Futures. FuturesFeeCurrency. Trading fee currency
        public string fee_ccy { get; set; }

        // Filled amount of the last transaction
        public string last_filled_amount { get; set; }

        // Filled price of the last transaction
        public string last_filled_price { get; set; }

        // User-defined id
        public string client_id { get; set; }

        public long created_at { get; set; }

        public long updated_at { get; set; }

        public static explicit operator Order(CexOrderUpdate cexOrder)
        {
            Order order = new Order();

            order.NumberUser = Convert.ToInt32(cexOrder.client_id);

            order.SecurityNameCode = cexOrder.market;
            //order.SecurityClassCode = cexOrder.Market.Substring(cexOrder.Currency.Length);
            // Cex.Amount - объём в единицах тикера
            // Cex.Value - объём в деньгах
            order.Volume = cexOrder.amount.ToString().ToDecimal();
            order.VolumeExecute = cexOrder.filled_amount.ToString().ToDecimal(); // FIX Разобраться с названием параметра!

            //order.PortfolioNumber = this.PortfolioName;

            if (cexOrder.type == CexOrderType.LIMIT.ToString())
            {
                order.Price = cexOrder.price.ToString().ToDecimal();
                order.TypeOrder = OrderPriceType.Limit;
            }
            else if (cexOrder.type == CexOrderType.MARKET.ToString())
            {
                order.TypeOrder = OrderPriceType.Market;
                // TODO нужно заполнить цену ?
            }

            order.ServerType = ServerType.CoinExSpot;

            order.NumberMarket = cexOrder.order_id.ToString();

            order.TimeCallBack = CoinExServerRealization.ConvertToDateTimeFromUnixFromMilliseconds(cexOrder.updated_at);

            order.Side = (cexOrder.side == CexOrderSide.BUY.ToString()) ? OsEngine.Entity.Side.Buy : OsEngine.Entity.Side.Sell;

            return order;
        }

        public override string ToString()
        {
            string orderUpdate = string.Format(
                "Order Update:{0}Order ID: {1}{0}Client ID: {2}{0}Market: {3}{0}Type: {4}{0}Side: {5}{0}Amount: {6}{0}Price: {7}{0}Unfilled Amount: {8}{0}Filled Amount: {9}{0}Filled Value: {10}", Environment.NewLine
                , order_id
                , client_id
                , market
                , type
                , side
                , amount
                , price
                , unfilled_amount
                , filled_amount
                , filled_value
                );

            return orderUpdate;
        }
    }
}
