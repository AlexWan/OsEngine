using System.Collections.Generic;

namespace OsEngine.Market.Servers.Mexc.MexcSpot.Entity
{
    public class DepthsWebSocket
    {
        public string channel { get; set; }
        public MexcDepth publicLimitDepths { get; set; }
        public string symbol { get; set; }
        public string sendTime { get; set; }
    }

    public class MexcDepthRow
    {
        public string price { get; set; }
        public string quantity { get; set; }
    }

    public class MexcDepth
    {
        public List<MexcDepthRow> asks { get; set; }
        public List<MexcDepthRow> bids { get; set; }
        public string eventType { get; set; }
        public string version { get; set; }
    }
}
