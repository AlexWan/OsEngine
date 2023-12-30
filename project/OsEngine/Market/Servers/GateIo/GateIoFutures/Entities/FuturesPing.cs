using Newtonsoft.Json;

namespace OsEngine.Market.Servers.GateIo.Futures.Request
{
    public class FuturesPing
    {
        [JsonProperty("time")]
        public long Time { get; set; }

        [JsonProperty("channel")]
        public string Channel { get; set; }
    }

    public class FuturesPong
    {
        [JsonProperty("time")]
        public long Time { get; set; }

        [JsonProperty("channel")]
        public string Channel { get; set; }

        [JsonProperty("event")]
        public string Event { get; set; }

        [JsonProperty("error")]
        public object Error { get; set; }

        [JsonProperty("result")]
        public object Result { get; set; }
    }
}
