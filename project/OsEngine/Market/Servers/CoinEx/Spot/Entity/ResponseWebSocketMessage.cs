using System.Collections.Generic;


namespace OsEngine.Market.Servers.CoinEx.Spot.Entity
{
    public class ResponseWebSocketMessage<T>
    {
        public string method { get; set; }
        public T data { get; set; }
        public string id { get; set; }
        public string message { get; set; }
        public string code { get; set; }
    }

    public class ResponseDeal
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

    public class ResponseDepthUpdate
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

    public class OrderWSData
    {
        public string order_id { get; set; }
        public string market { get; set; }
        public string margin_market { get; set; }
        public string type { get; set; }
        public string side { get; set; }
        public string price { get; set; }
        public string amount { get; set; }
        public string unfilled_amount { get; set; }
        public string filled_amount { get; set; }
        public string filled_value { get; set; }
        public string taker_fee_rate { get; set; }
        public string maker_fee_rate { get; set; }
        public string base_ccy_fee { get; set; }
        public string quote_ccy_fee { get; set; }
        public string discount_ccy_fee { get; set; }
        public string last_filled_amount { get; set; }
        public string last_filled_price { get; set; }
        public string client_id { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
    }

    public class ResponseUserDeal
    {
        public string deal_id { get; set; }
        public string created_at { get; set; }
        public string market { get; set; }
        public string side { get; set; }
        public string order_id { get; set; }
        public string client_id { get; set; }
        public string margin_market { get; set; }
        public string price { get; set; }
        public string amount { get; set; }
        public string role { get; set; }
        public string fee { get; set; }
        public string fee_ccy { get; set; }
    }

    public class ResponseWSBalance
    {
        public List<BalanceWSData> balance_list { get; set; }
    }

    public class BalanceWSData
    {
        public string margin_market { get; set; }
        public string ccy { get; set; }
        public string available { get; set; }
        public string frozen { get; set; }
        public string updated_at { get; set; }
    }
}
