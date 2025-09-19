using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OsEngine.Market.Servers.AE.Json
{
    public class OrderPendingMessage : WebSocketMessageBase
    {
        [JsonProperty("moment")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime Moment { get; set; }

        [JsonProperty("ext_id", NullValueHandling = NullValueHandling.Ignore)]
        public string ExternalId { get; set; }

        [JsonProperty("order_id")]
        public string OrderId { get; set; }
    }

    public class OrderRejectedMessage : WebSocketMessageBase
    {
        [JsonProperty("moment")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime Moment { get; set; }

        [JsonProperty("order_id", NullValueHandling = NullValueHandling.Ignore)]
        public string OrderId { get; set; }

        [JsonProperty("msg")]
        public string Message { get; set; }

        [JsonProperty("ext_id", NullValueHandling = NullValueHandling.Ignore)]
        public string ExternalId { get; set; }
    }

    public class OrderCanceledMessage : WebSocketMessageBase
    {
        [JsonProperty("moment")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime Moment { get; set; }

        [JsonProperty("order_id")]
        public string OrderId { get; set; }

        [JsonProperty("ext_id", NullValueHandling = NullValueHandling.Ignore)]
        public string ExternalId { get; set; }
    }

    public class OrderFilledMessage : WebSocketMessageBase
    {
        [JsonProperty("moment")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime Moment { get; set; }

        [JsonProperty("order_id")]
        public string OrderId { get; set; }

        [JsonProperty("ext_id", NullValueHandling = NullValueHandling.Ignore)]
        public string ExternalId { get; set; }

        [JsonProperty("price")]
        public decimal Price { get; set; }

        [JsonProperty("shares")]
        public decimal Shares { get; set; }

        [JsonProperty("shares_rest")]
        public decimal SharesRemaining { get; set; }
    }
}
