using Newtonsoft.Json;

namespace OsEngine.Market.Servers.FTX.Entities
{
    public class MarketResponse
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("underlying")]
        public string Underlying { get; set; }

        [JsonProperty("baseCurrency")]
        public string BaseCurrency { get; set; }

        [JsonProperty("quoteCurrency")]
        public string QuoteCurrency { get; set; }

        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("ask")]
        public decimal Ask { get; set; }

        [JsonProperty("bid")]
        public decimal Bid { get; set; }

        [JsonProperty("last")]
        public decimal Last { get; set; }

        [JsonProperty("priceIncrement")]
        public decimal PriceIncrement { get; set; }

        [JsonProperty("sizeIncrement")]
        public decimal SizeIncrement { get; set; }

        [JsonProperty("restricted")]
        public bool Restricted { get; set; }
    }
}
