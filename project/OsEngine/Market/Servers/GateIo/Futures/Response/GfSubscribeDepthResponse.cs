using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.GateIo.Futures.Response
{
    public partial class GfSubscribeDepthResponseAll
    {
        [JsonProperty("channel")]
        public string Channel { get; set; }

        [JsonProperty("event")]
        public string Event { get; set; }

        [JsonProperty("time")]
        public long Time { get; set; }

        [JsonProperty("result")]
        public GfSubscribeDepthResponseAllResult Result { get; set; }
    }

    public partial class GfSubscribeDepthResponseAllResult
    {
        [JsonProperty("contract")]
        public string Contract { get; set; }

        [JsonProperty("asks")]
        public DepthPoint[] Asks { get; set; }

        [JsonProperty("bids")]
        public DepthPoint[] Bids { get; set; }
    }

    public partial class DepthPoint
    {
        [JsonProperty("p")]
        public string P { get; set; }

        [JsonProperty("s")]
        public decimal S { get; set; }
    }

    public partial class GfSubscribeDepthResponseUpdate
    {
        [JsonProperty("channel")]
        public string Channel { get; set; }

        [JsonProperty("event")]
        public string Event { get; set; }

        [JsonProperty("time")]
        public long Time { get; set; }

        [JsonProperty("result")]
        public GfSubscribeDepthResponseUpdateResult[] Result { get; set; }
    }

    public partial class GfSubscribeDepthResponseUpdateResult
    {
        [JsonProperty("p")]
        public string P { get; set; }

        [JsonProperty("s")]
        public decimal S { get; set; }

        [JsonProperty("c")]
        public string C { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }
    }
}
