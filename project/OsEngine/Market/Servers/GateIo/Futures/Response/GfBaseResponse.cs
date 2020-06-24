using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.GateIo.Futures.Response
{
    public partial class GfBaseResponse
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
        public GfBaseResponseResult Result { get; set; }
    }

    public partial class GfBaseResponseResult
    {
        [JsonProperty("status")]
        public string Status { get; set; }
    }
}
