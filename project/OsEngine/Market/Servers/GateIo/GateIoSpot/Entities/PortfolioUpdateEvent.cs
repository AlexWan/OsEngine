namespace OsEngine.Market.Servers.GateIo.GateIoSpot.Entities
{
    public class CurrencyBalance
    {
        public string timestamp { get; set; }
        public string timestamp_ms { get; set; }
        public string user { get; set; }
        public string currency { get; set; }
        public string change { get; set; }
        public string total { get; set; }
        public string available { get; set; }
        public string freeze { get; set; }
        public string freeze_change { get; set; }
        public string change_type { get; set; }
    }
}
