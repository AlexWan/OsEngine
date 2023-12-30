using Newtonsoft.Json;

namespace OsEngine.Market.Servers.GateIo.GateIoFutures.Entities.Response
{
    public class GfTrades
    {
        [JsonProperty("channel")]
        public string Channel { get; set; }

        [JsonProperty("event")]
        public string Event { get; set; }

        [JsonProperty("time")]
        public long Time { get; set; }

        [JsonProperty("result")]
        public GfTradeResult[] Result { get; set; }
    }

    public class GfTradeResult
    {
        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("create_time")]
        public long CreateTime { get; set; }

        [JsonProperty("price")]
        public string Price { get; set; }

        [JsonProperty("contract")]
        public string Contract { get; set; }
    }
}
