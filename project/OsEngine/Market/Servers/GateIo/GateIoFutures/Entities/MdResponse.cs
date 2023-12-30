using Newtonsoft.Json;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.GateIo.GateIoFutures.Entities
{
    public class MdResponse
    {
        [JsonProperty("t")]
        public string T { get; set; }

        [JsonProperty("contract")]
        public string Contract { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("asks")]
        public List<Ask> Asks { get; set; }

        [JsonProperty("bids")]
        public List<Bid> Bids { get; set; }
    }

    public class Ask
    {
        [JsonProperty("p")]
        public string P { get; set; }

        [JsonProperty("s")]
        public string S { get; set; }
    }

    public class Bid
    {
        [JsonProperty("p")]
        public string P { get; set; }

        [JsonProperty("s")]
        public string S { get; set; }
    }
}
