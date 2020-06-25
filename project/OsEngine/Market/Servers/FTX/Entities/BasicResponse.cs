using Newtonsoft.Json;

namespace OsEngine.Market.Servers.FTX.Entities
{
    public class BasicResponse
    {
        [JsonProperty("channel")]
        public ChannelTypeEnum Channel { get; set; }

        [JsonProperty("market")]
        public string Market { get; set; }

        [JsonProperty("type")]
        public TypeEnum Type { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("msg")]
        public string Message { get; set; }

        [JsonProperty("data")]
        public string Data { get; set; }
    }
}
