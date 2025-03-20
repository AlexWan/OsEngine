using System.Collections.Generic;

namespace OsEngine.Market.Servers.GateIo.GateIoFutures.Entities
{
    public class MdResponse
    {
        public string t { get; set; }
        public string contract { get; set; }
        public string id { get; set; }
        public List<Ask> asks { get; set; }
        public List<Bid> bids { get; set; }
    }

    public class Ask
    {
        public string p { get; set; }
        public string s { get; set; }
    }

    public class Bid
    {
        public string p { get; set; }
        public string s { get; set; }
    }
}
