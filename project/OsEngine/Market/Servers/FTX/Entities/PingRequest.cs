using Newtonsoft.Json;

namespace OsEngine.Market.Servers.FTX.Entities
{
    public class PingRequest
    {
        [JsonProperty("op")]
        public OperationTypeEnum Operation => OperationTypeEnum.Ping;
    }
}
