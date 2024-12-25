namespace OsEngine.Market.Servers.KiteConnect.Json
{
    public class ResponseWebSocketKiteConnect<T>
    {
        public string type { get; set; }
        public string id { get; set; }
        public T data { get; set; }
    }
    public class OrderData
    {
        public string account_id { get; set; }
        public string unfilled_quantity { get; set; }
        public string checksum { get; set; } // 
        public string placed_by { get; set; }
        public string order_id { get; set; }
        public string exchange_order_id { get; set; }
        public string parent_order_id { get; set; } // 
        public string status { get; set; }
        public string status_message { get; set; } // 
        public string status_message_raw { get; set; } // 
        public string order_timestamp { get; set; }
        public string exchange_update_timestamp { get; set; }
        public string exchange_timestamp { get; set; }
        public string variety { get; set; }
        public string exchange { get; set; }
        public string tradingsymbol { get; set; }
        public string instrument_token { get; set; }
        public string order_type { get; set; }
        public string transaction_type { get; set; }
        public string validity { get; set; }
        public string product { get; set; }
        public string quantity { get; set; }
        public string disclosed_quantity { get; set; }
        public string price { get; set; }
        public string trigger_price { get; set; }
        public string average_price { get; set; }
        public string filled_quantity { get; set; }
        public string pending_quantity { get; set; }
        public string cancelled_quantity { get; set; }
        public string market_protection { get; set; }
        //public object meta { get; set; } // 
        public string tag { get; set; } // 
        public string guid { get; set; }
    }
}
