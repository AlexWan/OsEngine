using Newtonsoft.Json;
using System;

namespace OsEngine.Market.Servers.AE.Json
{
    public class WebSocketCancelOrderMessage : WebSocketMessageBase
    {
        [JsonProperty("account")]
        public string Account { get; set; }

        [JsonProperty("order_id", NullValueHandling = NullValueHandling.Ignore)]
        public long OrderId { get; set; }

        [JsonProperty("ticker", NullValueHandling = NullValueHandling.Ignore)]
        public string Ticker { get; set; }

        public WebSocketCancelOrderMessage()
        {
            Type = "CancelOrder";
        }
    }
}
