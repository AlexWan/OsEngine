using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.GateIo.Futures.Response
{
    public partial class GfMyTrades
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
        public GfMyTradesResult[] Result { get; set; }
    }

    public partial class GfMyTradesResult
    {
        [JsonProperty("contract")]
        public string Contract { get; set; }

        [JsonProperty("create_time")]
        public long CreateTime { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("order_id")]
        public string OrderId { get; set; }

        [JsonProperty("price")]
        public string Price { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }
    }
}
