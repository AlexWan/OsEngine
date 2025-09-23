using Newtonsoft.Json;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.HTX.Swap.Entity
{
    public class ResponseRestMessage<T>
    {
        public string status { get; set; }

        [JsonProperty("err-code")]
        public string errcode { get; set; }
        [JsonProperty("err-msg")]
        public string errmsg { get; set; }
        public T data { get; set; }
        public string ts { get; set; }
    }

    public class SecuritiesInfo
    {
        public string symbol { get; set; }
        public string contract_code { get; set; }
        public string contract_size { get; set; }
        public string price_tick { get; set; }
        public string delivery_date { get; set; }
        public string delivery_time { get; set; }
        public string create_date { get; set; }
        public string contract_status { get; set; }
        public List<string> adjust { get; set; }
        public List<string> price_estimated { get; set; }
        public string settlement_date { get; set; }
        public string support_margin_mode { get; set; }
        public string business_type { get; set; }
        public string pair { get; set; }
        public string contract_type { get; set; }
        public string trade_partition { get; set; }
    }

    public class ResponseRest<T>
    {
        public string code { get; set; }
        public T data { get; set; }
        public string msg { get; set; }
        public string ts { get; set; }
    }

    public class PortfoliosUsdt
    {
        public List<CrossFutureRest> cross_future { get; set; }
        public string cross_margin_static { get; set; }
        public string cross_profit_unreal { get; set; }
        public string cross_risk_rate { get; set; }
        public List<CrossSwapRest> cross_swap { get; set; }
        public List<IsolatedSwapRest> isolated_swap { get; set; }
        public string margin_asset { get; set; }
        public string margin_balance { get; set; }
        public string margin_frozen { get; set; }
        public string margin_static { get; set; }
        public string userId { get; set; }
        public string withdraw_available { get; set; }
    }

    public class CrossFutureRest
    {
        public string business_type { get; set; }
        public string contract_code { get; set; }
        public string contract_type { get; set; }
        public string cross_max_available { get; set; }
        public string lever_rate { get; set; }
        public string margin_available { get; set; }
        public string margin_mode { get; set; }
        public string symbol { get; set; }
    }

    public class CrossSwapRest
    {
        public string business_type { get; set; }
        public string contract_code { get; set; }
        public string contract_type { get; set; }
        public string cross_max_available { get; set; }
        public string lever_rate { get; set; }
        public string margin_available { get; set; }
        public string margin_mode { get; set; }
        public string symbol { get; set; }
    }

    public class IsolatedSwapRest
    {
        public string contract_code { get; set; }
        public string lever_rate { get; set; }
        public string margin_available { get; set; }
        public string margin_mode { get; set; }
        public string symbol { get; set; }
        public string withdraw_available { get; set; }
    }

    public class ResponseMessagePortfoliosCoin
    {
        public List<Data> data { get; set; }

        public class Data
        {
            public string symbol { get; set; }
            public string margin_balance { get; set; }
            public string margin_available { get; set; }
            public string margin_frozen { get; set; }
        }
    }

    public class ResponseMessagePositionsCoin
    {
        public List<Data> data { get; set; }

        public class Data
        {
            public string contract_code { get; set; }
            public string symbol { get; set; }
            public string volume { get; set; }
            public string frozen { get; set; }
            public string profit_unreal { get; set; }
            public string margin_asset { get; set; }
            public string direction { get; set; }
            public string margin_mode { get; set; }
            public string position_mode { get; set; }
        }
    }

    public class ResponseCandles
    {
        public string amount { get; set; }
        public string close { get; set; }
        public string count { get; set; }
        public string high { get; set; }
        public string id { get; set; }
        public string low { get; set; }
        public string open { get; set; }
        public string trade_turnover { get; set; }
        public string vol { get; set; }
    }

    public class PlaceOrderResponse
    {
        public string order_id { get; set; }
        public string client_order_id { get; set; }
        public string order_id_str { get; set; }
    }

    public class ResponseAllOrders
    {
        public List<OrdersItem> orders { get; set; }
        public string total_page { get; set; }
        public string current_page { get; set; }
        public string total_size { get; set; }
    }

    public class OrdersItem
    {
        public string symbol { get; set; }
        public string contract_code { get; set; }
        public string volume { get; set; }
        public string price { get; set; }
        public string order_price_type { get; set; }
        public string order_type { get; set; }
        public string direction { get; set; }
        public string offset { get; set; }
        public string lever_rate { get; set; }
        public string order_id { get; set; }
        public string client_order_id { get; set; }
        public string created_at { get; set; }
        public string trade_volume { get; set; }
        public string trade_turnover { get; set; }
        public string fee { get; set; }
        public string trade_avg_price { get; set; }
        public string margin_frozen { get; set; }
        public string profit { get; set; }
        public string status { get; set; }
        public string order_source { get; set; }
        public string order_id_str { get; set; }
        public string fee_asset { get; set; }
        public string liquidation_type { get; set; }
        public string canceled_at { get; set; }
        public string margin_asset { get; set; }
        public string margin_mode { get; set; }
        public string margin_account { get; set; }
        public string is_tpsl { get; set; }
        public string update_time { get; set; }
        public string real_profit { get; set; }
        public string reduce_only { get; set; }
    }

    public class ResponseGetOrder
    {
        public string symbol { get; set; }
        public string contract_code { get; set; }
        public string volume { get; set; }
        public string price { get; set; }
        public string order_price_type { get; set; }
        public string order_type { get; set; }
        public string direction { get; set; }
        public string offset { get; set; }
        public string lever_rate { get; set; }
        public string status { get; set; }
        public string order_id { get; set; }
        public string created_at { get; set; }
        public string client_order_id { get; set; }
        public string margin_account { get; set; }
        public string margin_mode { get; set; }
    }

    public class ResponseMyTradesBySecurity
    {
        public string symbol { get; set; }
        public string contract_code { get; set; }
        public string instrument_price { get; set; }
        public string final_interest { get; set; }
        public string adjust_value { get; set; }
        public string lever_rate { get; set; }
        public string direction { get; set; }
        public string offset { get; set; }
        public string volume { get; set; }
        public string price { get; set; }
        public string created_at { get; set; }
        public string canceled_at { get; set; }
        public string order_source { get; set; }
        public string order_price_type { get; set; }
        public string margin_frozen { get; set; }
        public string profit { get; set; }
        public List<TradesItemRest> trades { get; set; }
        public string total_page { get; set; }
        public string current_page { get; set; }
        public string total_size { get; set; }
        public string liquidation_type { get; set; }
        public string fee_asset { get; set; }
        public string fee { get; set; }
        public string order_id { get; set; }
        public string order_id_str { get; set; }
        public string client_order_id { get; set; }
        public string order_type { get; set; }
        public string status { get; set; }
        public string trade_avg_price { get; set; }
        public string trade_turnover { get; set; }
        public string trade_volume { get; set; }
        public string margin_asset { get; set; }
        public string margin_mode { get; set; }
        public string margin_account { get; set; }
        public string is_tpsl { get; set; }
        public string real_profit { get; set; }
        public string reduce_only { get; set; }
        public string canceled_source { get; set; }
    }

    public class TradesItemRest
    {
        public string trade_id { get; set; }
        public string trade_price { get; set; }
        public string trade_volume { get; set; }
        public string trade_turnover { get; set; }
        public string trade_fee { get; set; }
        public string created_at { get; set; }
        public string role { get; set; }
        public string fee_asset { get; set; }
        public string real_profit { get; set; }
        public string profit { get; set; }
        public string id { get; set; }
        public string price { get; set; }
    }

    public class ResponseTrades
    {
        public string ch { get; set; }
        public string status { get; set; }
        public string ts { get; set; }
        public Tick tick { get; set; }

        public class Tick
        {
            public string id { get; set; }
            public string ts { get; set; }
            public List<Data> data { get; set; }
        }

        public class Data
        {
            public string id { get; set; }
            public string ts { get; set; }
            public string tradeid { get; set; }
            public string price { get; set; }
            public string amount { get; set; }
            public string direction { get; set; }
        }
    }

    public class OpenInterestInfo
    {
        public string volume { get; set; }
        public string amount { get; set; }
        public string symbol { get; set; }
        public string value { get; set; }
        public string contract_code { get; set; }
        public string trade_amount { get; set; }
        public string trade_volume { get; set; }
        public string trade_turnover { get; set; }
        public string business_type { get; set; }
        public string pair { get; set; }
        public string contract_type { get; set; }
    }

    public class FundingInfo
    {
        public string funding_rate { get; set; }
        public string contract_code { get; set; }
        public string symbol { get; set; }
        public string fee_asset { get; set; }
        public string funding_time { get; set; }
        public string estimated_rate { get; set; }
        public string next_funding_time { get; set; }
    }

    public class FundingData
    {
        public string total_page { get; set; }
        public string current_page { get; set; }
        public string total_size { get; set; }
        public List<FundingItemHistory> data { get; set; }
    }

    public class FundingItemHistory
    {
        public string avg_premium_index { get; set; }
        public string funding_rate { get; set; }
        public string funding_time { get; set; }
        public string realized_rate { get; set; }
        public string contract_code { get; set; }
        public string symbol { get; set; }
        public string fee_asset { get; set; }
    }
}
