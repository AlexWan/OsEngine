
namespace OsEngine.Market.Servers.Binance.Futures.Entity
{
    public class OrderBinFutResp
    {
        public string s; //":"BTCUSDT",              // Symbol
        public string c; //":"TEST",                 // Client Order Id
        public string S; //":"SELL",                 // Side
        public string o; //":"LIMIT",                // Order Type
        public string f; //":"GTC",                  // Time in Force
        public string q; //":"0.001",                // Original Quantity
        public string p; //":"9910",                 // Price
        public string ap; //":"0",                   // Average Price
        public string sp; //":"0",                   // Stop Price
        public string x; //":"NEW",                  // Execution Type
        public string X; //":"NEW",                  // Order Status
        public string i; //":8886774,                // Order Id
        public string l; //":"0",                    // Order Last Filled Quantity
        public string z; //":"0",                    // Order Filled Accumulated Quantity
        public string L; //":"0",                    // Last Filled Price
        public string N; //": "USDT",                // Commission Asset, will not push if no commission
        public string n; //": "0",                   // Commission, will not push if no commission
        public string T; //":1568879465651,          // Order Trade Time
        public string t; //":0,                      // Trade Id
        public string b; //":"0",                    // Bids Notional
        public string a; //":"9.91",                 // Ask Notional
        public string m; //": false,                 // Is this trade the maker side?
        public string R; //":false,                  // Is this reduce only
        public string wt; //": "CONTRACT_PRICE"      // stop price working type
    }

    public class OrderUpdResponse
    {
        public string e; // ":"ORDER_TRADE_UPDATE",     // Event Type
        public string E; //":1568879465651,            // Event Time
        public string T; //": 1568879465650,           //  Transaction Time

        public OrderBinFutResp o; // order

    }
}
