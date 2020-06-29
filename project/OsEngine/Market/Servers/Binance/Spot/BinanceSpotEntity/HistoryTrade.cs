using Newtonsoft.Json;

namespace OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity
{
    public class HistoryTrade
    {
        public string id;
        public string price;
        public string qty;
        public string quoteQty;
        public string time;
        public string isBuyerMaker;
        public string isBuyerMatch;
    }

    public partial class AgregatedHistoryTrade
    {
        [JsonProperty("a")]
        public long A { get; set; }

        [JsonProperty("p")]
        public string P { get; set; }

        [JsonProperty("q")]
        public string Q { get; set; }

        [JsonProperty("f")]
        public long F { get; set; }

        [JsonProperty("l")]
        public long L { get; set; }

        [JsonProperty("T")]
        public long T { get; set; }

        [JsonProperty("m")]
        public bool M { get; set; }
    }
}