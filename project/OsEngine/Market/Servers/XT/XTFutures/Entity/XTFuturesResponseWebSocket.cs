using System.Collections.Generic;

namespace OsEngine.Market.Servers.XT.XTFutures.Entity
{

    public class XTFuturesResponseWebSocket<T>
    {
        public string topic { get; set; }
        public string @event { get; set; }
        public T data { get; set; }
    }

    public class XTFuturesPublicTrade
    {
        public string s { get; set; }          //symbol
        public string t { get; set; }          //timestamp
        public string p { get; set; }          //price
        public string a { get; set; }          //Quantity
        public string m { get; set; }          //"ask"
    }

    public class XTFuturesUpdateDepth
    {
        public string s { get; set; }              // symbol
        public string pu { get; set; }             // previousUpdateId
        public string fu { get; set; }             // firstUpdateId
        public string u { get; set; }              // lastUpdateId
        public List<List<string>> a { get; set; }  // asks: list of [price, quantity]
        public List<List<string>> b { get; set; }  // bids: list of [price, quantity]
    }

    public class XTFuturesSnapshotDepth
    {
        public string id { get; set; }             // lastUpdateId
        public string s { get; set; }              // trading pair (symbol)
        public List<List<string>> a { get; set; }  // asks: list of [price, quantity]
        public List<List<string>> b { get; set; }  // bids: list of [price, quantity]
        public string t { get; set; }              // timestamp
    }

    public class XTFuturesCandle
    {
        public string a { get; set; }  // Volume
        public string c { get; set; }  // Close price
        public string h { get; set; }  // Highest price
        public string l { get; set; }  // Lowest price
        public string o { get; set; }  // Open price
        public string s { get; set; }  // Trading pair (symbol)
        public string t { get; set; }  // Time (timestamp)
        public string v { get; set; }  // Turnover
    }

    public class XTFuturesResponsePortfolio
    {
        public string coin { get; set; }                   // Currency (e.g., usdt)
        public string underlyingType { get; set; }         // Underlying type (1 = Coin-M, 2 = USDT-M)
        public string walletBalance { get; set; }          // Wallet balance
        public string openOrderMarginFrozen { get; set; }  // Margin frozen by open orders
        public string isolatedMargin { get; set; }         // Isolated margin
        public string crossedMargin { get; set; }          // Crossed margin  
    }

    public class XTFuturesUpdateOrder
    {
        public string symbol { get; set; }
        public string orderId { get; set; }
        public string clientOrderId { get; set; }
        public string origQty { get; set; }
        public string price { get; set; }
        public string orderSide { get; set; }
        public string timeInForce { get; set; }
        public string positionSide { get; set; }
        public string marginFrozen { get; set; }
        public string sourceType { get; set; }
        public string type { get; set; }
        public string seqId { get; set; }
        public string state { get; set; }
        public string createdTime { get; set; }
        public string updatedTime { get; set; }
        public string leverage { get; set; }
        public string positionType { get; set; }
        public string orderType { get; set; }
    }
}
