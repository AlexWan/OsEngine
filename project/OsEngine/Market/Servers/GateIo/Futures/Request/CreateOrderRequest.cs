using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.GateIo.Futures.Request
{
    public partial class CreateOrderRequst
    {
        [JsonProperty("contract")]
        public string Contract { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("iceberg")]
        public long Iceberg { get; set; }

        [JsonProperty("price")]
        public string Price { get; set; }

        [JsonProperty("tif")]
        public string Tif { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }
}
