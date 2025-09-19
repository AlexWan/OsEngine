using System.Collections.Generic;

namespace OsEngine.Market.Servers.ExMo.ExmoSpot.Entity
{
    public class ResponseWebSocketMessage<T>
    {
        public string ts { get; set; }
        public string @event { get; set; }
        public string topic { get; set; }
        public T data { get; set; }
    }

    public class WebSocketTrade
    {
        public string trade_id { get; set; }
        public string type { get; set; }
        public string price { get; set; }
        public string quantity { get; set; }
        public string amount { get; set; }
        public string date { get; set; }
    }

    public class OrderBookData
    {
        public List<List<string>> ask { get; set; }
        public List<List<string>> bid { get; set; }
    }

    public class MyTradeResponse
    {
        public string trade_id { get; set; }
        public string type { get; set; }
        public string price { get; set; }
        public string quantity { get; set; }
        public string amount { get; set; }
        public string date { get; set; }
        public string order_id { get; set; }
        public string pair { get; set; }
        public string exec_type { get; set; }
        public string commission_amount { get; set; }
        public string commission_currency { get; set; }
        public string commission_percent { get; set; }
    }

    public class WalletData
    {
        public string currency { get; set; }
        public string balance { get; set; }
        public string reserved { get; set; }
        public string staked { get; set; }
    }

    public class OrdersSnapshot
    {
        public string order_id { get; set; }
        public string parent_order_id { get; set; }
        public string client_id { get; set; }
        public string created { get; set; }
        public string pair { get; set; }
        public string price { get; set; }
        public string quantity { get; set; }
        public string original_quantity { get; set; }
        public string amount { get; set; }
        public string original_amount { get; set; }
        public string type { get; set; }
        public string status { get; set; }
        public string trigger_price { get; set; }
    }

    public class OrderUpdate
    {
        public string order_id { get; set; }
        public string client_id { get; set; }
        public string created { get; set; }
        public string pair { get; set; }
        public string quantity { get; set; }
        public string price { get; set; }
        public string original_quantity { get; set; }
        public string type { get; set; }
        public string status { get; set; }
        public string last_trade_id { get; set; }
        public string last_trade_price { get; set; }
        public string last_trade_quantity { get; set; }
    }
}
