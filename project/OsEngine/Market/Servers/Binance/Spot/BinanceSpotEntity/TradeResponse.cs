namespace OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity
{
    public class Data
    {
        public string e { get; set; }
        public long E { get; set; }
        public string s { get; set; }
        public int t { get; set; }
        public string p { get; set; }
        public string q { get; set; }
        public int b { get; set; }
        public int a { get; set; }
        public long T { get; set; }
        public bool m { get; set; }
        public bool M { get; set; }
    }

    public class TradeResponse
    {
        public string stream { get; set; }
        public Data data { get; set; }
    }
}