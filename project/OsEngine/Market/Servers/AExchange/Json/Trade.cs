using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.AE.Json
{
    public class WebSocketTradeMessage : WebSocketMessageBase
    {
        [JsonProperty("moment")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime Moment { get; set; }

        [JsonProperty("order_id")]
        public string OrderId { get; set; }

        [JsonProperty("trade_id")]
        public string TradeId { get; set; }

        [JsonProperty("account")]
        public string AccountNumber { get; set; }

        [JsonProperty("ticker")]
        public string Ticker { get; set; }

        [JsonProperty("price")]
        public decimal Price { get; set; }

        [JsonProperty("shares")]
        public decimal Shares { get; set; }

        [JsonProperty("comment", NullValueHandling = NullValueHandling.Ignore)]
        public string Comment { get; set; }

        [JsonProperty("t_type")]
        public TradeType TradeType { get; set; }

        [JsonProperty("order_ext_id", NullValueHandling = NullValueHandling.Ignore)]
        public string OrderExternalId { get; set; }
    }

    public enum TradeType
    {
        Regular = 0,
        Commission = 1,
        Expiration = 2,
        ExpirationInMoney = 3,
        Referrer = 4,
        Rebate = 5,
        Cross = 6,
        Admin = 7
    }
}
