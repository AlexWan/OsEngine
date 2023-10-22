namespace OsEngine.Market.Servers.Alor.Dto
{
    public class AlorSecurity
    {
        public string symbol { get; set; }
        public string shortname { get; set; }
        public string description { get; set; }
        public string exchange { get; set; }
        public string type { get; set; }
        public decimal lotsize { get; set; }
        public decimal facevalue { get; set; }
        public string cfiCode { get; set; }
        public string cancellation { get; set; }
        public decimal minstep { get; set; }
        public decimal rating { get; set; }
        public decimal marginbuy { get; set; }
        public decimal marginsell { get; set; }
        public decimal marginrate { get; set; }
        public decimal pricestep { get; set; }
        public decimal priceMax { get; set; }
        public decimal priceMi { get; set; }
        public decimal theorPrice { get; set; }
        public decimal theorPriceLimit { get; set; }
        public decimal volatility { get; set; }
        public string currency { get; set; }
        public string board { get; set; }
        public string primary_board { get; set; }
        public int tradingStatus { get; set; }
        public string tradingStatusInfo { get; set; }
        public decimal priceMultiplier { get; set; }
        public string complexProductCategory { get; set; }
    }
}