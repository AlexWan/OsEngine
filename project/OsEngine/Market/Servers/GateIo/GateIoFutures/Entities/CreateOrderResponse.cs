using Newtonsoft.Json;

namespace OsEngine.Market.Servers.GateIo.GateIoFutures.Entities
{
    public class CreateOrderResponse
    {
        [JsonProperty("contract")]
        public string Contract { get; set; }

        [JsonProperty("create_time")]
        public string CreateTime { get; set; }

        [JsonProperty("create_time_ms")]
        public long CreateTimeMs { get; set; }

        [JsonProperty("fill_price")]
        public string FillPrice { get; set; }

        [JsonProperty("finish_as")]
        public string FinishAs { get; set; }

        [JsonProperty("finish_time")]
        public string FinishTime { get; set; }

        [JsonProperty("finish_time_ms")]
        public long FinishTimeMs { get; set; }

        [JsonProperty("iceberg")]
        public int Iceberg { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("is_close")]
        public bool IsClose { get; set; }

        [JsonProperty("is_liq")]
        public bool IsLiq { get; set; }

        [JsonProperty("is_reduce_only")]
        public bool IsReduceOnly { get; set; }

        [JsonProperty("left")]
        public int Left { get; set; }

        [JsonProperty("mkfr")]
        public string Mkfr { get; set; }

        [JsonProperty("price")]
        public string Price { get; set; }

        [JsonProperty("refr")]
        public int Refr { get; set; }

        [JsonProperty("refu")]
        public int Refu { get; set; }

        [JsonProperty("size")]
        public string Size { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("tif")]
        public string Tif { get; set; }

        [JsonProperty("tkfr")]
        public string Tkfr { get; set; }

        [JsonProperty("user")]
        public string User { get; set; }
    }
}
