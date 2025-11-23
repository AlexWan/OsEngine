using System.Collections.Generic;

namespace OsEngine.Market.Servers.CoinEx.Futures.Entity
{
    public class ResponseWebSocketMessage<T>
    {
        public string method { get; set; }
        public T data { get; set; }
        public string id { get; set; }
        public string message { get; set; }
        public string code { get; set; }
    }

    public class ResponseTrade
    {
        public string market { get; set; }
        public List<DealData> deal_list { get; set; }
    }

    public class DealData
    {
        public string deal_id { get; set; }
        public string created_at { get; set; }
        public string side { get; set; }
        public string price { get; set; }
        public string amount { get; set; }
    }

    public class ResponseDepth
    {
        public string market { get; set; }
        public string is_full { get; set; }
        public DepthData depth { get; set; }
    }

    public class DepthData
    {
        public List<List<string>> asks { get; set; }
        public List<List<string>> bids { get; set; }
        public string last { get; set; }
        public string updated_at { get; set; }
        public string checksum { get; set; }
    }

    public class ResponseWSOrder
    {
        public string @event { get; set; }
        public OrderWSData order { get; set; }
    }

    public class ResponseWSBalance
    {
        public List<BalanceWSData> balance_list { get; set; }
    }

    public class BalanceWSData
    {
        public string ccy { get; set; }
        public string available { get; set; }
        public string frozen { get; set; }
        public string margin { get; set; }
        public string transferrable { get; set; }
        public string unrealized_pnl { get; set; }
        public string equity { get; set; }
    }

    public class OrderWSData
    {
        public string order_id { get; set; }
        public string stop_id { get; set; }
        public string market { get; set; }
        public string side { get; set; }
        public string type { get; set; }
        public string amount { get; set; }
        public string price { get; set; }
        public string unfilled_amount { get; set; }
        public string filled_amount { get; set; }
        public string filled_value { get; set; }
        public string fee { get; set; }
        public string fee_ccy { get; set; }
        public string taker_fee_rate { get; set; }
        public string maker_fee_rate { get; set; }
        public string client_id { get; set; }
        public string last_filled_amount { get; set; }
        public string last_filled_price { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
    }

    public class ResponseUserDeal
    {
        public string deal_id { get; set; }
        public string created_at { get; set; }
        public string order_id { get; set; }
        public string client_id { get; set; }
        public string position_id { get; set; }
        public string market { get; set; }
        public string side { get; set; }
        public string price { get; set; }
        public string amount { get; set; }
        public string role { get; set; }
        public string fee { get; set; }
        public string fee_ccy { get; set; }
    }

    public class ResponseWSState
    {
        public List<StateWSData> state_list { get; set; }
    }
    public class StateWSData
    {
        public string market { get; set; }
        public string last { get; set; }
        public string open { get; set; }
        public string close { get; set; }
        public string high { get; set; }
        public string low { get; set; }
        public string volume { get; set; }
        public string value { get; set; }
        public string volume_sell { get; set; }
        public string volume_buy { get; set; }
        public string open_interest_size { get; set; }
        public string insurance_fund_size { get; set; }
        public string mark_price { get; set; }
        public string index_price { get; set; }
        public string latest_funding_rate { get; set; }
        public string next_funding_rate { get; set; }
        public string latest_funding_time { get; set; }
        public string next_funding_time { get; set; }
        public string period { get; set; }
    }
}
