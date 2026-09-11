using System.Collections.Generic;


namespace OsEngine.Market.Servers.OKX.Entity
{
    public class TradeDetailsResponse
    {
        public string code { get; set; }
        public string msg { get; set; }
        public List<TradeDetailsObject> data { get; set; }
    }

    public class TradeDetailsObject
    {
        public string instType { get; set; }
        public string instId { get; set; }
        public string tradeId { get; set; }
        public string ordId { get; set; }
        public string clOrdId { get; set; }
        public string billId { get; set; }
        public string tag { get; set; }
        public string fillPx { get; set; }
        public string fillSz { get; set; }
        public string side { get; set; }
        public string posSide { get; set; }
        public string execType { get; set; }
        public string feeCcy { get; set; }
        public string fee { get; set; }
        public string ts { get; set; }
    }
}
