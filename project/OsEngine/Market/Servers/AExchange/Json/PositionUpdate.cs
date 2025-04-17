using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;

namespace OsEngine.Market.Servers.AE.Json
{
    public class WebSocketPositionUpdateMessage : WebSocketMessageBase
    {
        [JsonProperty("account")]
        public string AccountNumber { get; set; }

        [JsonProperty("ticker")]
        public string Ticker { get; set; }

        [JsonProperty("open_date")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime OpenDate { get; set; }

        [JsonProperty("shares")]
        public decimal Shares { get; set; }

        [JsonProperty("open_price")]
        public decimal OpenPrice { get; set; }
    }
}
