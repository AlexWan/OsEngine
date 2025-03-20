
namespace OsEngine.Market.Servers.GateIo.GateIoFutures.Entities
{
    public class UserTradeResponse
    {
        public string id { get; set; }
        public string create_time { get; set; }
        public string create_time_ms { get; set; }
        public string contract { get; set; }
        public string order_id { get; set; }
        public string size { get; set; }
        public string price { get; set; }
        public string role { get; set; }
        public string text { get; set; }
        public string fee { get; set; }
        public string point_fee { get; set; }
        public string close_size { get; set; }
    }
}
