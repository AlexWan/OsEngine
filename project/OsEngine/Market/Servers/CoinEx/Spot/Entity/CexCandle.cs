
namespace OsEngine.Market.Servers.CoinEx.Spot.Entity
{
    /*
        https://docs.coinex.com/api/v2/spot/market/http/list-market-kline#http-request
    */
    struct CexCandle
    {
        // Market name [ETHUSDT]
        public string market { get; set; }

        // Opening price
        public string open { get; set; }

        // Closing price
        public string close { get; set; }

        // Highest price
        public string high { get; set; }

        // Lowest price
        public string low { get; set; }

        // Filled volume
        public string volume { get; set; }

        // Filled value
        public string value { get; set; }

        // Timestamp (millisecond)
        public long created_at { get; set; }
    }
}