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
        public string user_id { get; set; } // ID of the user for whom the order was placed.
        public string order_id { get; set; } // Unique order ID
        public string app_id { get; set; } // Your kiteconnect app ID
        public string checksum { get; set; } // SHA-256 hash of (order_id + timestamp + api_secret)
        public string placed_by { get; set; } // ID of the user that placed the order.
                                              // This may different from the user's id for orders placed outside of Kite,
                                              // for instance, by dealers at the brokerage using dealer terminals.
        public string exchange_order_id { get; set; } // Exchange generated order id. Orders that don't reach the exchange have null ids
        public string parent_order_id { get; set; } // Order ID of the parent order (only applicable in case of multi-legged orders like CO)
        public string status { get; set; } // Current status of the order. The possible values are COMPLETE, REJECTED, CANCELLED, and UPDATE.
        public string status_message { get; set; } // Textual description of the order's status. Failed orders come with human readable explanation
        public string status_message_raw { get; set; } // Raw textual description of the failed order's status, as received from the OMS
        public string order_timestamp { get; set; } // Timestamp at which the order was registered by the API
        public string exchange_update_timestamp { get; set; } // Timestamp at which an order's state changed at the exchange
        public string exchange_timestamp { get; set; } // Timestamp at which the order was registered by the exchange.
                                                       // Orders that don't reach the exchange have null timestamps
        public string variety { get; set; } // Order variety (regular, amo, co etc.)
        public string exchange { get; set; } // Exchange
        public string tradingsymbol { get; set; } // Exchange tradingsymbol of the of the instrument
        public string instrument_token { get; set; } // The numerical identifier issued by the exchange representing the instrument
        public string order_type { get; set; } // Order type (MARKET, LIMIT etc.)
        public string transaction_type { get; set; } // BUY or SELL
        public string validity { get; set; } // Order validity
        public string product { get; set; } // Margin product to use for the order
        public string quantity { get; set; } // Quantity ordered
        public string disclosed_quantity { get; set; } // Quantity to be disclosed (may be different from actual quantity) to the public exchange orderbook. Only for equities
        public string price { get; set; } // Price at which the order was placed (LIMIT orders)
        public string trigger_price { get; set; } // Trigger price (for SL, SL-M, CO orders)
        public string average_price { get; set; } // Average price at which the order was executed (only for COMPLETE orders)
        public string filled_quantity { get; set; } // Quantity that has been filled
        public string unfilled_quantity { get; set; } // Quantity that has not filled
        public string pending_quantity { get; set; } // Pending quantity for open order
        public string cancelled_quantity { get; set; } // Quantity that had been cancelled
        public string market_protection { get; set; }
        public Meta meta { get; set; } // Map of arbitrary fields that the system may attach to an order
        public string tag { get; set; } // An optional tag to apply to an order to identify it (alphanumeric, max 20 chars)
        public string guid { get; set; }
    }
}
