
namespace OsEngine.Market.Servers.GateIo.GateIoFutures.Entities
{
    public partial class CancelOrderResponse
    {
        public string id { get; set; }
        public string user { get; set; }
        public string contract { get; set; }
        public string create_time { get; set; }
        public string size { get; set; }
        public string iceberg { get; set; }
        public string left { get; set; }
        public string price { get; set; }
        public string fill_price { get; set; }
        public string mkfr { get; set; }
        public string tkfr { get; set; }
        public string tif { get; set; }
        public string refu { get; set; }
        public string is_reduce_only { get; set; }
        public string is_close { get; set; }
        public string is_liq { get; set; }
        public string text { get; set; }
        public string status { get; set; }
        public string finish_time { get; set; }
        public string finish_as { get; set; }
    }
}
