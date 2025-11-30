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

    public class OrderOpenRestRespFut
    {
        public string avgPrice; //": "0.00000",
        public string clientOrderId; //": "abc",
        public string cumQuote; //": "0",
        public string executedQty; //": "0",
        public string orderId; //": 1917641,
        public string origQty; //": "0.40",
        public string origType; //": "TRAILING_STOP_MARKET",
        public string price; //": "0",
        public string reduceOnly; //": false,
        public string side; //": "BUY",
        public string positionSide; //": "SHORT",
        public string status; //": "NEW",
        public string stopPrice; //": "9300",                // please ignore when order type is TRAILING_STOP_MARKET
        public string closePosition; //": false,   // if Close-All
        public string symbol; //": "BTCUSDT",
        public string time; //": 1579276756075,              // order time
        public string timeInForce; //": "GTC",
        public string type; //": "TRAILING_STOP_MARKET",
        public string activatePrice; //": "9020",            // activation price, only return with TRAILING_STOP_MARKET order
        public string priceRate; //": "0.3",                 // callback rate, only return with TRAILING_STOP_MARKET order
        public string updateTime; //": 1579276756075,        // update time
        public string workingType; //": "CONTRACT_PRICE",
        public string priceProtect; //": false,            // if conditional order trigger is protected  
        public string priceMatch; //": "NONE",              //price match mode
        public string selfTradePreventionMode; //": "NONE", //self trading preventation mode
        public string goodTillDate; //": 0      //order pre-set auot cancel time for TIF GTD order
    }
}
