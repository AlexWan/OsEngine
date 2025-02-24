using OsEngine.Entity;
using OsEngine.Market.Servers.CoinEx.Spot.Entity.Enums;
using System;

namespace OsEngine.Market.Servers.CoinEx.Spot.Entity
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
        // Значение в валюте, [USDT]
        public string amount { get; set; }

        public string price { get; set; }

        // The remaining unfilled amount
        public string unfilled_amount { get; set; }

        // Filled volume в единицах тикера {TON}
        //
        public string filled_amount { get; set; }

        // Amount - объём в единицах тикера
        // Value - объём в деньгах
        // Filled Value в валюте [USDT]
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
        //public string FuturesFee { get; set; }

        // Futures. FuturesFeeCurrency. Trading fee currency
        //public string fee_ccy { get; set; }

        // Filled amount of the last transaction
        public string last_filled_amount { get; set; }

        // Filled price of the last transaction
        public string last_filled_price { get; set; }

        // User-defined id
        public string client_id { get; set; }

        public long created_at { get; set; }

        public long updated_at { get; set; }

    }
}