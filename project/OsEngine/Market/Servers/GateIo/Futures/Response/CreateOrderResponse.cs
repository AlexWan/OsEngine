using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.GateIo.Futures.Response
{
    public partial class CreateOrderResponse
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("user")]
        public long User { get; set; }

        [JsonProperty("contract")]
        public string Contract { get; set; }

        [JsonProperty("create_time")]
        public long CreateTime { get; set; }

        [JsonProperty("size")]
        public decimal Size { get; set; }

        [JsonProperty("iceberg")]
        public long Iceberg { get; set; }

        [JsonProperty("left")]
        public long Left { get; set; }

        [JsonProperty("price")]
        public string Price { get; set; }

        [JsonProperty("fill_price")]
        public string FillPrice { get; set; }

        [JsonProperty("mkfr")]
        public string Mkfr { get; set; }

        [JsonProperty("tkfr")]
        public string Tkfr { get; set; }

        [JsonProperty("tif")]
        public string Tif { get; set; }

        [JsonProperty("refu")]
        public long Refu { get; set; }

        [JsonProperty("is_reduce_only")]
        public bool IsReduceOnly { get; set; }

        [JsonProperty("is_close")]
        public bool IsClose { get; set; }

        [JsonProperty("is_liq")]
        public bool IsLiq { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("finish_time")]
        public long FinishTime { get; set; }

        [JsonProperty("finish_as")]
        public string FinishAs { get; set; }
    }
}
