namespace OsEngine.Market.Servers.CoinEx.Futures.Entity
{
    /*
        https://docs.coinex.com/api/v2/futures/deal/http/list-user-order-deals#return-parameters
    */
    struct CexTransaction
    {
        // Transaction id
        public long deal_id { get; set; }

        // Transaction timestamp, millisecond
        public long created_at { get; set; }

        public string order_id { get; set; }

        public string position_id { get; set; }

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

        // Taker or maker
        public string role { get; set; }

        // Trading fee charged
        public string fee { get; set; }

        // Trading fee currency
        public string fee_ccy { get; set; }

    }
}