namespace OsEngine.Market.Servers.TwelveData.Entity
{
    public class WebSocketPriceMessage
    {
        public string @event { get; set; }
        public string symbol { get; set; }
        public string currency { get; set; }
        public string exchange { get; set; }
        public string type { get; set; }
        public string timestamp { get; set; }
        public string price { get; set; }
        public string day_volume { get; set; }
        public string bid { get; set; }
        public string ask { get; set; }
    }
}
