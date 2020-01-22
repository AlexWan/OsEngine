using System.Collections.Generic;

namespace OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity
{
    public class RateLimit
    {
        public string rateLimitType { get; set; }
        public string interval { get; set; }
        public int limit { get; set; }
    }

    public class Filter
    {
        public string filterType { get; set; }
        public string minPrice { get; set; }
        public string maxPrice { get; set; }
        public string tickSize { get; set; }
        public string minQty { get; set; }
        public string maxQty { get; set; }
        public string stepSize { get; set; }
        public string minNotional { get; set; }
    }

    public class Symbol
    {
        public string symbol { get; set; }
        public string status { get; set; }
        public string baseAsset { get; set; }
        public int baseAssetPrecision { get; set; }
        public string quoteAsset { get; set; }
        public int quotePrecision { get; set; }
        public List<string> orderTypes { get; set; }
        public bool icebergAllowed { get; set; }
        public List<Filter> filters { get; set; }
    }

    public class SecurityResponce
    {
        public string timezone { get; set; }
        public long serverTime { get; set; }
        public List<RateLimit> rateLimits { get; set; }
        public List<object> exchangeFilters { get; set; }
        public List<Symbol> symbols { get; set; }
    }
}