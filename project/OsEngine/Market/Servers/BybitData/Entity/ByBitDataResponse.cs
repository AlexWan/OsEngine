using System.Collections.Generic;

namespace OsEngine.Market.Servers.BybitData.Entity
{
    public class ByBitDataResponse<T>
    {
        public string retCode { get; set; }
        public string retMsg { get; set; }
        public T result { get; set; }
        public string time { get; set; }
    }

    public class ListSymbols
    {
        public string category { get; set; }
        public List<Symbol> list { get; set; }
        public string nextPageCursor { get; set; }
    }

    public class Symbol
    {
        public string symbol { get; set; }
        public string contractType { get; set; }
        public string status { get; set; }
        public string baseCoin { get; set; }
        public string quoteCoin { get; set; }
        public string launchTime { get; set; }
        public string deliveryTime { get; set; }
        public string deliveryFeeRate { get; set; }
        public string priceScale { get; set; }
        public string unifiedMarginTrade { get; set; }
        public string fundingInterval { get; set; }
        public string settleCoin { get; set; }
        public string copyTrading { get; set; }
        public string upperFundingRate { get; set; }
        public string lowerFundingRate { get; set; }
        public string optionsType { get; set; }
    }

    public class BybitDataCandlesResponse<T>
    {
        public string retCode { get; set; }
        public string retMsg { get; set; }
        public RetResalt<T> result { get; set; }
        public string time { get; set; }
    }

    public class RetResalt<T>
    {
        public string category { get; set; }
        public string nextPageCursor { get; set; }
        public List<T> list { get; set; }
    }
}

