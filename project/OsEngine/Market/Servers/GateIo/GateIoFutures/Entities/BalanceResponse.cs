using Newtonsoft.Json;

namespace OsEngine.Market.Servers.GateIo.GateIoFutures.Entities
{
    public class BalanceResponse
    {
        [JsonProperty("balance")]
        public string Balance { get; set; }

        [JsonProperty("change")]
        public string Change { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("time")]
        public string Time { get; set; }

        [JsonProperty("time_ms")]
        public long TimeMs { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("user")]
        public string User { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }
    }
}
