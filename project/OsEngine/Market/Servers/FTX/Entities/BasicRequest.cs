using Newtonsoft.Json;

namespace OsEngine.Market.Servers.FTX.Entities
{
    public class BasicRequest
    {
        [JsonProperty("channel")]
        public ChannelTypeEnum Channel { get; set; }

        [JsonProperty("market")]
        public string Market { get; set; }

        [JsonProperty("op")]
        public OperationTypeEnum Operation { get; set; }
    }
}
