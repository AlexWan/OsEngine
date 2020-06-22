using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.GateIo.Futures.Response
{
    public partial class GfOrders
    {
        [JsonProperty("channel")]
        public string Channel { get; set; }

        [JsonProperty("event")]
        public string Event { get; set; }

        [JsonProperty("time")]
        public long Time { get; set; }

        [JsonProperty("result")]
        public GfOrdersResult[] Result { get; set; }
    }

    public partial class GfOrdersResult
    {
        [JsonProperty("contract")]
        public string Contract { get; set; }

        [JsonProperty("user")]
        public string User { get; set; }

        [JsonProperty("create_time")]
        public long CreateTime { get; set; }

        [JsonProperty("fill_price")]
        public long FillPrice { get; set; }

        [JsonProperty("finish_as")]
        public string FinishAs { get; set; }

        [JsonProperty("iceberg")]
        public long Iceberg { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("is_reduce_only")]
        public bool IsReduceOnly { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("is_close")]
        public long IsClose { get; set; }

        [JsonProperty("is_liq")]
        public long IsLiq { get; set; }

        [JsonProperty("left")]
        public long Left { get; set; }

        [JsonProperty("mkfr")]
        public double Mkfr { get; set; }

        [JsonProperty("price")]
        public long Price { get; set; }

        [JsonProperty("refu")]
        public long Refu { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("tif")]
        public string Tif { get; set; }

        [JsonProperty("finish_time")]
        public long FinishTime { get; set; }

        [JsonProperty("tkfr")]
        public double Tkfr { get; set; }
    }
}
