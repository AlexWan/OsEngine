using Newtonsoft.Json;

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

        [JsonProperty("amend_text")]
        public string AmendText { get; set; }
    }

    public partial class CreateOrderRequstDoubleModeClose
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

        [JsonProperty("amend_text")]
        public string AmendText { get; set; }

        //[JsonProperty("auto_size")]
        //public string AutoSize { get; set; }

        [JsonProperty("close")]
        public bool Close { get; set; }

        [JsonProperty("reduce_only")]
        public bool Reduce_only { get; set; }
    }
}
