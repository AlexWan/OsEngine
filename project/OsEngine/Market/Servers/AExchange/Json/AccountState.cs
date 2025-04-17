using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.AE.Json
{
    public class WebSocketAccountStateMessage : WebSocketMessageBase
    {
        [JsonProperty("account")]
        public string AccountNumber { get; set; }

        [JsonProperty("moment")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime Moment { get; set; }

        [JsonProperty("money", NullValueHandling = NullValueHandling.Ignore)]
        public decimal? Money { get; set; }

        [JsonProperty("gm", NullValueHandling = NullValueHandling.Ignore)]
        public decimal? GuaranteeMargin { get; set; }

        [JsonProperty("money_free", NullValueHandling = NullValueHandling.Ignore)]
        public decimal? FreeMoney { get; set; }

        [JsonProperty("fee", NullValueHandling = NullValueHandling.Ignore)]
        public decimal? Fee { get; set; }
    }
}
