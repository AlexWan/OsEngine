using System.Collections.Generic;

namespace OsEngine.Market.Servers.CoinEx.Futures.Entity
{
    public class ResponseRestMessage<T>
    {
        public string code { get; set; }
        public T data { get; set; }
        public string message { get; set; }
        public Pagination pagination { get; set; }
    }

    public class Pagination
    {
        public string total { get; set; }
        public string has_next { get; set; }
    }

    public class BalanceData
    {
        public string ccy { get; set; }
        public string available { get; set; }
        public string frozen { get; set; }
        public string margin { get; set; }
        public string unrealized_pnl { get; set; }
        public string transferrable { get; set; }
    }

    public class ResponseCandle
    {
        public string market { get; set; }
        public string open { get; set; }
        public string close { get; set; }
        public string high { get; set; }
        public string low { get; set; }
        public string volume { get; set; }
        public string value { get; set; }
        public string created_at { get; set; }
    }

    public class PositionData
    {
        public string position_id { get; set; }
        public string market { get; set; }
        public string market_type { get; set; }
        public string side { get; set; }
        public string margin_mode { get; set; }
        public string open_interest { get; set; }
        public string close_avbl { get; set; }
        public string ath_position_amount { get; set; }
        public string unrealized_pnl { get; set; }
        public string realized_pnl { get; set; }
        public string avg_entry_price { get; set; }
        public string cml_position_value { get; set; }
        public string max_position_value { get; set; }
        public string take_profit_price { get; set; }
        public string stop_loss_price { get; set; }
        public string take_profit_type { get; set; }
        public string stop_loss_type { get; set; }
        public string leverage { get; set; }
        public string margin_avbl { get; set; }
        public string ath_margin_size { get; set; }
        public string position_margin_rate { get; set; }
        public string maintenance_margin_rate { get; set; }
        public string maintenance_margin_value { get; set; }
        public string liq_price { get; set; }
        public string bkr_price { get; set; }
        public string adl_level { get; set; }
        public string settle_price { get; set; }
        public string settle_value { get; set; }
    }

    public class MarketInfoData
    {
        public string market { get; set; }
        public string contract_type { get; set; }
        public string taker_fee_rate { get; set; }
        public string maker_fee_rate { get; set; }
        public string min_amount { get; set; }
        public string base_ccy { get; set; }
        public string quote_ccy { get; set; }
        public string status { get; set; }
        public string base_ccy_precision { get; set; }
        public string quote_ccy_precision { get; set; }
        public string tick_size { get; set; }
        public string is_market_available { get; set; }
        public string is_copy_trading_available { get; set; }
        public List<string> leverage { get; set; }
        public string open_interest_volume { get; set; }
    }

    public class OrderRestResponse
    {
        public string order_id { get; set; }
        public string market { get; set; }
        public string market_type { get; set; }
        public string side { get; set; }
        public string type { get; set; }
        public string amount { get; set; }
        public string price { get; set; }
        public string unfilled_amount { get; set; }
        public string filled_amount { get; set; }
        public string filled_value { get; set; }
        public string client_id { get; set; }
        public string fee { get; set; }
        public string fee_ccy { get; set; }
        public string maker_fee_rate { get; set; }
        public string taker_fee_rate { get; set; }
        public string last_filled_amount { get; set; }
        public string last_filled_price { get; set; }
        public string realized_pnl { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
        public string status { get; set; }
    }

    public class MyTradeRestResponse
    {
        public string deal_id { get; set; }
        public string created_at { get; set; }
        public string market { get; set; }
        public string side { get; set; }
        public string order_id { get; set; }
        public string position_id { get; set; }
        public string price { get; set; }
        public string amount { get; set; }
        public string role { get; set; }
        public string fee { get; set; }
        public string fee_ccy { get; set; }
        public string realized_pnl { get; set; }
    }

    public class FundingRateRestResponse
    {
        public string market { get; set; }
        public string mark_price { get; set; }
        public string latest_funding_rate { get; set; }
        public string next_funding_rate { get; set; }
        public string max_funding_rate { get; set; }
        public string min_funding_rate { get; set; }
        public string latest_funding_time { get; set; }
        public string next_funding_time { get; set; }
    }

    public class FundingDataHistory
    {
        public string market { get; set; }
        public string funding_time { get; set; }
        public string theoretical_funding_rate { get; set; }
        public string actual_funding_rate { get; set; }
    }
}
