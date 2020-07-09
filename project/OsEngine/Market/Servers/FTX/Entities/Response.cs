using Newtonsoft.Json;

namespace OsEngine.Market.Servers.FTX.Entities
{
    public class Response<T>
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("result")]
        public T Result { get; set; }
    }
}
