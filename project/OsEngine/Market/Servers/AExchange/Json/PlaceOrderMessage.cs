using Newtonsoft.Json;

namespace OsEngine.Market.Servers.AE.Json
{
    public class WebSocketPlaceOrderMessage : WebSocketMessageBase
    {
        [JsonProperty("account")]
        public string Account { get; set; }

        [JsonProperty("ticker")]
        public string Ticker { get; set; }

        [JsonProperty("price")]
        public decimal Price { get; set; }

        [JsonProperty("shares")]
        public decimal Shares { get; set; }

        [JsonProperty("ext_id", NullValueHandling = NullValueHandling.Ignore)]
        public string ExternalId { get; set; }

        [JsonProperty("comment", NullValueHandling = NullValueHandling.Ignore)]
        public string Comment { get; set; }

        public WebSocketPlaceOrderMessage()
        {
            Type = "PlaceOrder";
        }
    }
}
