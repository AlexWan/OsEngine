using System.Collections.Generic;

namespace OsEngine.Market.Servers.HTX.Swap.Entity
{
    public class ResponseWebSocketMessage<T>
    {
        public string op { get; set; }
        public string topic { get; set; }
        public string ts { get; set; }
        public string @event { get; set; }
        public T data { get; set; }
        public string uid { get; set; }
    }

    public class PositionsItem
    {
        public string symbol { get; set; }
        public string contract_code { get; set; }
        public string volume { get; set; }
        public string available { get; set; }
        public string frozen { get; set; }
        public string cost_open { get; set; }
        public string cost_hold { get; set; }
        public string profit_unreal { get; set; }
        public string profit_rate { get; set; }
        public string profit { get; set; }
        public string position_margin { get; set; }
        public string lever_rate { get; set; }
        public string direction { get; set; }
        public string last_price { get; set; }
        public string margin_asset { get; set; }
        public string margin_mode { get; set; }
        public string margin_account { get; set; }
        public string position_mode { get; set; }
        public string adl_risk_percent { get; set; }
        public string risk_rate { get; set; }
        public string withdraw_available { get; set; }
        public string liquidation_price { get; set; }
        public string trade_partition { get; set; }
    }

    public class PortfolioItem
    {
        public string symbol { get; set; }
        public string margin_asset { get; set; }
        public string margin_static { get; set; }
        public string cross_margin_static { get; set; }
        public string margin_balance { get; set; }
        public string cross_profit_unreal { get; set; }
        public string margin_frozen { get; set; }
        public string withdraw_available { get; set; }
        public string cross_risk_rate { get; set; }
        public List<CrossSwap> cross_swap { get; set; }
        public List<object> cross_future { get; set; }
        public List<IsolatedSwap> isolated_swap { get; set; }
    }

    public class CrossSwap
    {
        public string symbol { get; set; }
        public string contract_code { get; set; }
        public string margin_mode { get; set; }
        public string margin_available { get; set; }
        public string lever_rate { get; set; }
        public string contract_type { get; set; }
        public string business_type { get; set; }
        public string cross_max_available { get; set; }
    }

    public class IsolatedSwap
    {
        public string symbol { get; set; }
        public string contract_code { get; set; }
        public string margin_mode { get; set; }
        public string margin_available { get; set; }
        public string lever_rate { get; set; }
        public string contract_type { get; set; }
        public string business_type { get; set; }
        public string cross_max_available { get; set; }
    }

    public class ResponseChannelTrades
    {
        public Tick tick { get; set; }

        public string ch { get; set; }

        public class Tick
        {
            public List<Data> data { get; set; }
        }

        public class Data
        {
            public string ts { get; set; }
            public string id { get; set; }
            public string price { get; set; }
            public string amount { get; set; }
            public string direction { get; set; }
        }
    }

    public class ResponseChannelBook
    {
        public string ch { get; set; }
        public string ts { get; set; }
        public Tick tick { get; set; }

        public class Tick
        {
            public List<List<string>> asks { get; set; }
            public List<List<string>> bids { get; set; }
            public string ts { get; set; }

        }
    }

    public class ResponseChannelUpdateOrder
    {
        public string symbol { get; set; }
        public string contract_type { get; set; }
        public string contract_code { get; set; }
        public string volume { get; set; }
        public string price { get; set; }
        public string order_price_type { get; set; }
        public string direction { get; set; }
        public string offset { get; set; }
        public string status { get; set; }
        public string lever_rate { get; set; }
        public string order_id { get; set; }
        public string ts { get; set; }
        public string created_at { get; set; }
        public string client_order_id { get; set; }
        public string margin_mode { get; set; }
        public string margin_account { get; set; }
        public string reduce_only { get; set; }
        public List<TradeItem> trade { get; set; }
    }

    public class TradeItem
    {
        public string id { get; set; }
        public string trade_id { get; set; }
        public string created_at { get; set; }
        public string trade_volume { get; set; }
        public string trade_price { get; set; }
        public string orderSource { get; set; }
        public string eventType { get; set; }
        public string symbol { get; set; }
        public string clientOrderId { get; set; }
        public string orderStatus { get; set; }
        public string orderId { get; set; }
        public string type { get; set; }
        public string lastActTime { get; set; }
    }

    public class ResponsePingPrivate
    {
        public string ts { get; set; }
    }

    public class ResponsePingPublic
    {
        public string ping { get; set; }
    }

    public class FundingItem
    {
        public string symbol { get; set; }
        public string contract_code { get; set; }
        public string fee_asset { get; set; }
        public string funding_time { get; set; }
        public string funding_rate { get; set; }
        public string settlement_time { get; set; }
        public string estimated_rate { get; set; }
    }
}

