
namespace OsEngine.Market.Servers.GateIoData.Entity
{
    public class GateDataTradeResponse
    {
        public string id { get; set; }
        public string create_time { get; set; }
        public string create_time_ms { get; set; }
        public string currency_pair { get; set; }
        public string side { get; set; }
        public string amount { get; set; }
        public string contract { get; set; }
        public string size { get; set; }
        public string price { get; set; }
    }
}
