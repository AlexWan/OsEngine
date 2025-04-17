using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using System;
using Newtonsoft.Json.Linq;
using OsEngine.Market.Servers.AE.Json;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.AE.Json
{
    public class InstrumentDefinition
    {
        [JsonProperty("ticker")]
        public string Ticker { get; set; }

        [JsonProperty("full_name")]
        public string FullName { get; set; }

        [JsonProperty("type")]
        [JsonConverter(typeof(StringEnumConverter))]
        public InstrumentType Type { get; set; }

        [JsonProperty("parent", NullValueHandling = NullValueHandling.Ignore)]
        public string Parent { get; set; }

        [JsonProperty("price_step", NullValueHandling = NullValueHandling.Ignore)]
        public decimal? PriceStep { get; set; }

        [JsonProperty("shares_step", NullValueHandling = NullValueHandling.Ignore)]
        public decimal? SharesStep { get; set; }

        [JsonProperty("exp_date", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime? ExpDate { get; set; }

        [JsonProperty("strike", NullValueHandling = NullValueHandling.Ignore)]
        public decimal? Strike { get; set; }

        [JsonProperty("o_type", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(StringEnumConverter))]
        public OptionType? OType { get; set; }

        [JsonProperty("p_s_price", NullValueHandling = NullValueHandling.Ignore)]
        public decimal? PSPrice { get; set; }

        [JsonProperty("lot_vol", NullValueHandling = NullValueHandling.Ignore)]
        public decimal? LotVol { get; set; }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum InstrumentType
    {
        Equity,
        Futures,
        Option,
        Index
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum OptionType
    {
        Put,
        Call
    }

    public class WebSocketInstrumentsMessage : WebSocketMessageBase
    {
        [JsonProperty("instruments")]
        public List<InstrumentDefinition> Instruments { get; set; }
    }
}
