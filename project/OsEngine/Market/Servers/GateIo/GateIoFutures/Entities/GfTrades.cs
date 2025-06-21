using System.Collections.Generic;

namespace OsEngine.Market.Servers.GateIo.GateIoFutures.Entities.Response
{
    public class GfTrades
    {
        public string channel { get; set; }
        public string @event { get; set; }
        public string time { get; set; }
        public List<GfTradeResult> result { get; set; }
    }

    public class GfTradeResult
    {
        public string size { get; set; }
        public string id { get; set; }
        public string create_time { get; set; }
        public string price { get; set; }
        public string contract { get; set; }
    }
}
