namespace OsEngine.Market.Servers.CoinEx.Futures.Entity
{
    // https://docs.coinex.com/api/v2/futures/market/http/list-market#http-request
    public struct CexMarketInfoItem
    {
        // ETHUSDT
        public string market { get; set; }
        
        public string last { get; set; }

        // Futures only
        public string index_price { get; set; }

        // Futures only
        public string mark_price { get; set; }

        public string open { get; set; }
        
        public string close { get; set; }
        
        public string high { get; set; }
        
        public string low { get; set; }

        // Filled volume
        public string volume { get; set; }
        
        public string volume_sell { get; set; }
        
        public string volume_buy { get; set; }

        // Filled value
        public string value { get; set; }
        
        public long period { get; set; }
    }
}