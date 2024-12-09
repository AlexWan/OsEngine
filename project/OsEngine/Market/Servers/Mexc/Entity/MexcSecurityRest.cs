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
        public int baseAssetPrecision { get; set; }
        public string quoteAsset { get; set; }
        public int quotePrecision { get; set; }
        public int quoteAssetPrecision { get; set; }
        public int baseCommissionPrecision { get; set; }
        public int quoteCommissionPrecision { get; set; }
        public List<string> orderTypes { get; set; }
        public bool isSpotTradingAllowed { get; set; }
        public bool isMarginTradingAllowed { get; set; }
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
        public int tradeSideType { get; set; }
    }

    public class MexcSecurityList
    {
        public string timezone { get; set; }
        public long serverTime { get; set; }
        public List<object> rateLimits { get; set; }
        public List<object> exchangeFilters { get; set; }
        public List<MexcSecurity> symbols { get; set; }
    }


    public class MexcCandlesHistory : List<List<object>>
    {

    }
}