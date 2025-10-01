using System.Collections.Generic;

namespace OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity
{
    public class RateLimit
    {
        public string interval { get; set; }
        public string intervalNum { get; set; }
        public string limit { get; set; }
        public string rateLimitType { get; set; }
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
        public string notional { get; set; }
    }

    public class Symbol
    {
        public string symbol { get; set; }
        public string status { get; set; }
        public string baseAsset { get; set; }
        public string contractType { get; set; }
        public string baseAssetPrecision { get; set; }
        public string quoteAsset { get; set; }
        public string quotePrecision { get; set; }
        public List<string> orderTypes { get; set; }
        public string icebergAllowed { get; set; }
        public List<Filter> filters { get; set; }
        public string pair { get; set; }
        public string deliveryDate { get; set; }
        public string onboardDate { get; set; }
        public string contractStatus { get; set; }
        public string pricePrecision { get; set; }
        public string quantityPrecision { get; set; }
        public string marginAsset { get; set; }
        public List<string> orderType { get; set; }
        public List<string> timeInForce { get; set; }
        public string contractSize { get; set; }
        public string equalQtyPrecision { get; set; }
        public string underlyingType { get; set; }
        public List<string> underlyingSubType { get; set; }
        public string settlePlan { get; set; }
        public string triggerProtect { get; set; }
        public string liquidationFee { get; set; }
        public string marketTakeBound { get; set; }
    }

    public class SecurityResponce
    {
        public string timezone { get; set; }
        public string serverTime { get; set; }
        public List<RateLimit> rateLimits { get; set; }
        public List<Asset> assets { get; set; }
        public List<object> exchangeFilters { get; set; }
        public List<Symbol> symbols { get; set; }
    }

    public class Asset
    {
        public string assetName { get; set; }
        public string marginAvailable { get; set; }
        public string autoAssetExchange { get; set; }
    }
}