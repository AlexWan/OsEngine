
namespace OsEngine.Market.Servers.GateIoData.Entity
{
    public class GateSecurityResponse
    {
        public string id;
        public string Base;
        public string quote;
        public string fee;
        public string min_base_amount;
        public string min_quote_amount;
        public string amount_precision;
        public string precision;
        public string trade_status;
        public string sell_start;
        public string buy_start;
    }

    public class GateFutSecurityInfo
    {
        public string funding_rate_indicative { get; set; }
        public string mark_price_round { get; set; }
        public string funding_offset { get; set; }
        public string in_delisting { get; set; }
        public string risk_limit_base { get; set; }
        public string interest_rate { get; set; }
        public string index_price { get; set; }
        public string order_price_round { get; set; }
        public string order_size_min { get; set; }
        public string ref_rebate_rate { get; set; }
        public string name { get; set; }
        public string ref_discount_rate { get; set; }
        public string order_price_deviate { get; set; }
        public string maintenance_rate { get; set; }
        public string mark_type { get; set; }
        public string funding_interval { get; set; }
        public string type { get; set; }
        public string risk_limit_step { get; set; }
        public string leverage_min { get; set; }
        public string funding_rate { get; set; }
        public string last_price { get; set; }
        public string mark_pric { get; set; }
        public string order_size_max { get; set; }
        public string funding_next_apply { get; set; }
        public string config_change_time { get; set; }
        public string position_size { get; set; }
        public string trade_size { get; set; }
        public string quanto_multiplier { get; set; }
        public string funding_impact_value { get; set; }
        public string leverage_max { get; set; }
        public string risk_limit_max { get; set; }
        public string maker_fee_rate { get; set; }
        public string taker_fee_rate { get; set; }
        public string orders_limit { get; set; }
        public string trade_id { get; set; }
        public string orderbook_id { get; set; }
    }
}
