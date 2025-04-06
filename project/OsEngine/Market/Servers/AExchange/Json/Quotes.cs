using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;

namespace OsEngine.Market.Servers.AE.Json
{
    public class WebSocketQuoteMessage : WebSocketMessageBase
    {
        [JsonProperty("tr")] public string Ticker { get; set; }

        [JsonProperty("b", NullValueHandling = NullValueHandling.Ignore)]
        public decimal? Bid { get; set; }

        [JsonProperty("bv", NullValueHandling = NullValueHandling.Ignore)]
        public decimal? BidVolume { get; set; }

        [JsonProperty("a", NullValueHandling = NullValueHandling.Ignore)]
        public decimal? Ask { get; set; }

        [JsonProperty("av", NullValueHandling = NullValueHandling.Ignore)]
        public decimal? AskVolume { get; set; }

        [JsonProperty("l", NullValueHandling = NullValueHandling.Ignore)]
        public decimal? LastPrice { get; set; }

        [JsonProperty("lv", NullValueHandling = NullValueHandling.Ignore)]
        public decimal? LastVolume { get; set; }

        [JsonProperty("lt", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime? LastTradeTime { get; set; }

        [JsonProperty("v", NullValueHandling = NullValueHandling.Ignore)]
        public decimal? Volatility { get; set; }

        public WebSocketQuoteMessage()
        {
            Type = "Q"; // Automatically set message type
        }
    }
}
