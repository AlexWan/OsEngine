/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.Mexc.Json
{
    public class MexcSecurity
    {
        public string symbol { get; set; }
        public string status { get; set; }
        public string baseAsset { get; set; }
        public string baseAssetPrecision { get; set; }
        public string quoteAsset { get; set; }
        public string quotePrecision { get; set; }
        public string quoteAssetPrecision { get; set; }
        public string baseCommissionPrecision { get; set; }
        public string quoteCommissionPrecision { get; set; }
        public List<string> orderTypes { get; set; }
        public string isSpotTradingAllowed { get; set; }
        public string isMarginTradingAllowed { get; set; }
        public string quoteAmountPrecision { get; set; }
        public string baseSizePrecision { get; set; }
        public List<string> permissions { get; set; }
        public List<object> filters { get; set; }
        public string maxQuoteAmount { get; set; }
        public string makerCommission { get; set; }
        public string takerCommission { get; set; }
        public string quoteAmountPrecisionMarket { get; set; }
        public string maxQuoteAmountMarket { get; set; }
        public string fullName { get; set; }
        public string tradeSideType { get; set; }
    }

    public class MexcSecurityList
    {
        public string timezone { get; set; }
        public string serverTime { get; set; }
        public List<string> rateLimits { get; set; }
        public List<string> exchangeFilters { get; set; }
        public List<MexcSecurity> symbols { get; set; }
    }

    public class MexcCandlesHistory : List<List<object>>
    {

    }
}