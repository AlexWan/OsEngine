namespace OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity
{
    public class MiniTickerResponse
    {
        public string stream { get; set; }
        public TickerItem data { get; set; }
    }

    public class TickerItem
    {
        public string e { get; set; }          // Event type
        public string E { get; set; }            // Event time
        public string s { get; set; }          // Symbol
        public string c { get; set; }          // Close price
        public string o { get; set; }          // Open price
        public string h { get; set; }          // High price
        public string l { get; set; }          // Low price
        public string v { get; set; }          // Total traded base asset volume
        public string q { get; set; }          // Total traded quote asset volume
    }
}
