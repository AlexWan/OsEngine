using OsEngine.Entity;
using OsEngine.Market.Servers.CoinEx.Spot.Entity.Enums;
using System;


namespace OsEngine.Market.Servers.CoinEx.Spot.Entity
{
    /*
        https://docs.coinex.com/api/v2/spot/deal/http/list-user-order-deals#return-parameters
    */
    struct CexOrderTransaction
    {
        // Transaction id
        public long deal_id { get; set; }

        // Transaction timestamp, millisecond
        public long created_at { get; set; }

        // Order id
        public long order_id { get; set; }
        
        // Futures. Position id
        public long position_id { get; set; }

        // Market name
        public string market { get; set; }

        // Margin market, null for non-margin markets
        public string margin_market { get; set; }

        // Buy or sell
        public string side { get; set; }

        // Price
        public string price { get; set; }

        // Filled volume
        // Amount - объём в единицах тикера
        // Value - объём в деньгах
        public string amount { get; set; }

        // Taker or maker
        public string role { get; set; }

        // Trading fee charged
        public string fee { get; set; }

        // Trading fee currency
        public string fee_ccy { get; set; }

    }
}