using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;

namespace OsEngine.Market.Servers.AE.Json
{
    public class WebSocketMessageBase
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("t")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime Timestamp { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }
}
