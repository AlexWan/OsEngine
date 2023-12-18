using Newtonsoft.Json;

namespace OsEngine.Market.Servers.GateIo.GateIoFutures.Entities
{
    public class UserTradeResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("create_time")]
        public string CreateTime { get; set; }

        [JsonProperty("create_time_ms")]
        public long CreateTimeMs { get; set; }

        [JsonProperty("contract")]
        public string Contract { get; set; }

        [JsonProperty("order_id")]
        public string OrderId { get; set; }

        [JsonProperty("size")]
        public string Size { get; set; }

        [JsonProperty("price")]
        public string Price { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("fee")]
        public string Fee { get; set; }

        [JsonProperty("point_fee")]
        public string PointFee { get; set; }
    }
}
