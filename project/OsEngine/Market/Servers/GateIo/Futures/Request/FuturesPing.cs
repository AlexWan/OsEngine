using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.GateIo.Futures.Request
{
    public partial class FuturesPing
    {
        [JsonProperty("time")]
        public long Time { get; set; }

        [JsonProperty("channel")]
        public string Channel { get; set; }
    }

    public partial class FuturesPong
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
