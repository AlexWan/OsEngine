using OsEngine.Entity;
using OsEngine.Market.Servers.CoinEx.Spot.Entity.Enums;
using System;


namespace OsEngine.Market.Servers.CoinEx.Spot.Entity
{
    /*
        https://docs.coinex.com/api/v2/spot/deal/http/list-user-order-deals#return-parameters
    */
    struct CexTransaction
    {
        // Transaction id
        public long deal_id { get; set; }

        // Transaction timestamp, millisecond
        public long created_at { get; set; }

        // Market name
        public string market { get; set; }

        // Buy or sell
        public string side { get; set; }

        // Price
        public string price { get; set; }

        // Filled volume
        // Amount - объём в единицах тикера
        // Value - объём в деньгах
        public string amount { get; set; }

    }
}