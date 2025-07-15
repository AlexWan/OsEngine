using System.Collections.Generic;

namespace OsEngine.Market.Servers.BinanceData.Entity
{
    internal class BinanceSecurityResponse
    {
        public string timezone { get; set; }
        public string serverTime { get; set; }
        public List<object> exchangeFilters { get; set; }
        public List<BinanceSecurityInfo> symbols { get; set; }
    }

    public class BinanceSecurityInfo
    {
        public string symbol { get; set; }
        public string status { get; set; }
        public string baseAsset { get; set; }
        public string contractType { get; set; }
        public string baseAssetPrecision { get; set; }
        public string quoteAsset { get; set; }
        public string quotePrecision { get; set; }
        public List<string> orderTypes { get; set; }
    }
}
