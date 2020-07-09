using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.GateIo.Futures.Request
{
    public partial class GfAccount
        {
        [JsonProperty("order_margin")]
        public string OrderMargin { get; set; }

        [JsonProperty("point")]
        public string Point { get; set; }

        [JsonProperty("history")]
        public CanselOrderResponseHistory History { get; set; }

        [JsonProperty("unrealised_pnl")]
        public string UnrealisedPnl { get; set; }

        [JsonProperty("total")]
        public string Total { get; set; }

        [JsonProperty("available")]
        public string Available { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }

        [JsonProperty("position_margin")]
        public string PositionMargin { get; set; }

        [JsonProperty("user")]
        public long User { get; set; }
    }

    public partial class CanselOrderResponseHistory
    {
        [JsonProperty("dnw")]
        public decimal Dnw { get; set; }

        [JsonProperty("pnl")]
        public string Pnl { get; set; }

        [JsonProperty("point_refr")]
        public string PointRefr { get; set; }

        [JsonProperty("refr")]
        public string Refr { get; set; }

        [JsonProperty("point_fee")]
        public string PointFee { get; set; }

        [JsonProperty("fund")]
        public string Fund { get; set; }

        [JsonProperty("fee")]
        public string Fee { get; set; }

        [JsonProperty("point_dnw")]
        public string PointDnw { get; set; }
    }

}
