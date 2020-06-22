using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.GateIo.Futures.Entities
{
    public partial class GfSecurity
    {
        [JsonProperty("funding_rate_indicative")]
        public string FundingRateIndicative { get; set; }

        [JsonProperty("mark_price_round")]
        public string MarkPriceRound { get; set; }

        [JsonProperty("funding_offset")]
        public long FundingOffset { get; set; }

        [JsonProperty("in_delisting")]
        public bool InDelisting { get; set; }

        [JsonProperty("risk_limit_base")]
        public string RiskLimitBase { get; set; }

        [JsonProperty("interest_rate")]
        public string InterestRate { get; set; }

        [JsonProperty("index_price")]
        public string IndexPrice { get; set; }

        [JsonProperty("order_price_round")]
        public string OrderPriceRound { get; set; }

        [JsonProperty("order_size_min")]
        public decimal OrderSizeMin { get; set; }

        [JsonProperty("ref_rebate_rate")]
        public string RefRebateRate { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("ref_discount_rate")]
        public string RefDiscountRate { get; set; }

        [JsonProperty("order_price_deviate")]
        public string OrderPriceDeviate { get; set; }

        [JsonProperty("maintenance_rate")]
        public string MaintenanceRate { get; set; }

        [JsonProperty("mark_type")]
        public string MarkType { get; set; }

        [JsonProperty("funding_interval")]
        public decimal FundingInterval { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("risk_limit_step")]
        public string RiskLimitStep { get; set; }

        [JsonProperty("leverage_min")]
        public string LeverageMin { get; set; }

        [JsonProperty("funding_rate")]
        public string FundingRate { get; set; }

        [JsonProperty("last_price")]
        public string LastPrice { get; set; }

        [JsonProperty("mark_price")]
        public string MarkPrice { get; set; }

        [JsonProperty("order_size_max")]
        public decimal OrderSizeMax { get; set; }

        [JsonProperty("funding_next_apply")]
        public decimal FundingNextApply { get; set; }

        [JsonProperty("config_change_time")]
        public long ConfigChangeTime { get; set; }

        [JsonProperty("position_size")]
        public decimal PositionSize { get; set; }

        [JsonProperty("trade_size")]
        public decimal TradeSize { get; set; }

        [JsonProperty("quanto_multiplier")]
        public string QuantoMultiplier { get; set; }

        [JsonProperty("funding_impact_value")]
        public string FundingImpactValue { get; set; }

        [JsonProperty("leverage_max")]
        public string LeverageMax { get; set; }

        [JsonProperty("risk_limit_max")]
        public string RiskLimitMax { get; set; }

        [JsonProperty("maker_fee_rate")]
        public string MakerFeeRate { get; set; }

        [JsonProperty("taker_fee_rate")]
        public string TakerFeeRate { get; set; }

        [JsonProperty("orders_limit")]
        public decimal OrdersLimit { get; set; }

        [JsonProperty("trade_id")]
        public long TradeId { get; set; }

        [JsonProperty("orderbook_id")]
        public long OrderbookId { get; set; }
    }
}
