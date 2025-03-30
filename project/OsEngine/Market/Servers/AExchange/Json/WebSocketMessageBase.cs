using Newtonsoft.Json;
using OsEngine.Market.Servers.AE.Json;


namespace OsEngine.Market.Servers.AE.Json
{
    public class WebSocketMessageBase
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("t")]
        public long Timestamp { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }
}
