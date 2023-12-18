using Newtonsoft.Json;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.GateIo.GateIoSpot.Entities
{
    public class CurrencyBalance
    {
        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }

        [JsonProperty("timestamp_ms")]
        public string TimestampMs { get; set; }

        [JsonProperty("user")]
        public string User { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }

        [JsonProperty("change")]
        public string Change { get; set; }

        [JsonProperty("total")]
        public string Total { get; set; }

        [JsonProperty("available")]
        public string Available { get; set; }

        [JsonProperty("freeze")]
        public string Freeze { get; set; }

        [JsonProperty("freeze_change")]
        public string FreezeChange { get; set; }

        [JsonProperty("change_type")]
        public string ChangeType { get; set; }
    }

    public class PortfolioUpdateEvent
    {
        [JsonProperty("time")]
        public int Time { get; set; }

        [JsonProperty("time_ms")]
        public long TimeMs { get; set; }

        [JsonProperty("channel")]
        public string Channel { get; set; }

        [JsonProperty("event")]
        public string Event { get; set; }

        [JsonProperty("result")]
        public List<CurrencyBalance> Balances { get; set; }
    }

}
