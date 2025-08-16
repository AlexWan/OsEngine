using System.Collections.Generic;

namespace OsEngine.Market.Servers.AscendexSpot.Entity
{
    public class AscendexSpotSecurityResponse
    {
        public string code { get; set; }
        public List<AscendexSpotSecurityData> data { get; set; }
    }

    public class AscendexSpotSecurityData
    {
        public string symbol { get; set; }
        public string displayName { get; set; }
        public string domain { get; set; }
        public string tradingStartTime { get; set; }
        public string collapseDecimals { get; set; }
        public string minQty { get; set; }
        public string maxQty { get; set; }
        public string minNotional { get; set; }
        public string maxNotional { get; set; }
        public string statusCode { get; set; }
        public string statusMessage { get; set; }
        public string tickSize { get; set; }
        public string useTick { get; set; }
        public string lotSize { get; set; }
        public string useLot { get; set; }
        public string commissionType { get; set; }
        public string commissionReserveRate { get; set; }
        public string qtyScale { get; set; }
        public string priceScale { get; set; }
        public string notionalScale { get; set; }
    }

    public class BalanceResponse
    {
        public string code { get; set; }
        public List<BalanceRest> data { get; set; }
    }

    public class BalanceRest
    {
        public string asset { get; set; }
        public string totalBalance { get; set; }
        public string availableBalance { get; set; }
    }
}