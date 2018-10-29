namespace OsEngine.Market.Servers.Bitfinex.BitfitnexEntity
{
    public class NewOrderPayload
    {
        public string request { get; set; }
        public string nonce { get; set; }
        public string symbol { get; set; }
        public string amount { get; set; }
        public string price { get; set; }
        public string exchange { get; set; }
        public string side { get; set; }
        public string type { get; set; }
    }
}
