using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.GateIo.Futures.Response
{
    public partial class GfPosition
    {
        [JsonProperty("user", NullValueHandling = NullValueHandling.Ignore)]
        public long? User { get; set; }

        [JsonProperty("contract", NullValueHandling = NullValueHandling.Ignore)]
        public string Contract { get; set; }

        [JsonProperty("size", NullValueHandling = NullValueHandling.Ignore)]
        public long? Size { get; set; }

        [JsonProperty("leverage", NullValueHandling = NullValueHandling.Ignore)]
        public string Leverage { get; set; }

        [JsonProperty("risk_limit", NullValueHandling = NullValueHandling.Ignore)]
        public string RiskLimit { get; set; }

        [JsonProperty("leverage_max", NullValueHandling = NullValueHandling.Ignore)]
        public string LeverageMax { get; set; }

        [JsonProperty("maintenance_rate", NullValueHandling = NullValueHandling.Ignore)]
        public string MaintenanceRate { get; set; }

        [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)]
        public string Value { get; set; }

        [JsonProperty("margin", NullValueHandling = NullValueHandling.Ignore)]
        public string Margin { get; set; }

        [JsonProperty("entry_price", NullValueHandling = NullValueHandling.Ignore)]
        public string EntryPrice { get; set; }

        [JsonProperty("liq_price", NullValueHandling = NullValueHandling.Ignore)]
        public string LiqPrice { get; set; }

        [JsonProperty("mark_price", NullValueHandling = NullValueHandling.Ignore)]
        public string MarkPrice { get; set; }

        [JsonProperty("unrealised_pnl", NullValueHandling = NullValueHandling.Ignore)]
        public string UnrealisedPnl { get; set; }

        [JsonProperty("realised_pnl", NullValueHandling = NullValueHandling.Ignore)]
        public string RealisedPnl { get; set; }

        [JsonProperty("history_pnl", NullValueHandling = NullValueHandling.Ignore)]
        public string HistoryPnl { get; set; }

        [JsonProperty("last_close_pnl", NullValueHandling = NullValueHandling.Ignore)]
        public string LastClosePnl { get; set; }

        [JsonProperty("realised_point", NullValueHandling = NullValueHandling.Ignore)]
        public string RealisedPoint { get; set; }

        [JsonProperty("history_point", NullValueHandling = NullValueHandling.Ignore)]
        public string HistoryPoint { get; set; }

        [JsonProperty("adl_ranking", NullValueHandling = NullValueHandling.Ignore)]
        public long? AdlRanking { get; set; }

        [JsonProperty("pending_orders", NullValueHandling = NullValueHandling.Ignore)]
        public long? PendingOrders { get; set; }

        [JsonProperty("close_order", NullValueHandling = NullValueHandling.Ignore)]
        public CloseOrder CloseOrder { get; set; }
    }

    public partial class CloseOrder
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public long? Id { get; set; }

        [JsonProperty("price", NullValueHandling = NullValueHandling.Ignore)]
        public string Price { get; set; }

        [JsonProperty("is_liq", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsLiq { get; set; }
    }
}
