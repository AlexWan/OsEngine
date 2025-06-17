using System.Collections.Generic;

namespace OsEngine.Market.Servers.OKX.Entity
{
    public class TradesDataResponse
    {
        public string code { get; set; }
        public string msg { get; set; }
        public List<TradeData> data { get; set; }
    }

    public class TradeData
    {
        public string instId { get; set; }
        public string side { get; set; }
        public string sz { get; set; }
        public string px { get; set; }
        public string tradeId { get; set; }
        public string ts { get; set; }
    }
}
