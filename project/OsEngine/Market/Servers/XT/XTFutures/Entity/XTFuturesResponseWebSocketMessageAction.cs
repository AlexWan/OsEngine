using System.Collections.Generic;

namespace OsEngine.Market.Servers.XT.XTFutures.Entity
{
    public class XTFuturesResponseWebSocketMessage
    {
        public string id { get; set; }                          //call back ID
        public string code { get; set; }                        //result 0=success;1=fail;2=listenKey invalid
        public string msg { get; set; }                         //Message string, "token expire"
    }

    public class XTFuturesResponseWebSocketMessageAction<T>
    {
        public string topic { get; set; }
        public string @event { get; set; }
        public T data { get; set; }
    }

    public class XTFuturesWsTrade
    {
        public string s { get; set; }                       //"btc_usdt", symbol        
        public string i { get; set; }                      //"6316559590087222000",  trade id
        public string t { get; set; }                    //"1655992403617", trade time
        public string p { get; set; }                   //"43000", trade price
        public string q { get; set; }                //"0.21",  qty，trade quantity
        public string b { get; set; }                 //"true" whether is buyerMaker or not
    }

    public class XTFuturesResponseWebSocketUpdateDepth
    {

        public string s { get; set; } // symbol
        public long pu { get; set; }  // previousUpdateId
        public long fu { get; set; }  // firstUpdateId
        public long u { get; set; }   // lastUpdateId
        public List<List<string>> a { get; set; } // asks: list of [price, quantity]
        public List<List<string>> b { get; set; }  // bids: list of [price, quantity]
    }

    public class XTFuturesResponseWebSocketSnapshotDepth
    {
        public string id { get; set; }   // lastUpdateId
        public string s { get; set; }    // trading pair (symbol)
        public List<List<string>> a { get; set; }  // asks: list of [price, quantity]
        public List<List<string>> b { get; set; } // bids: list of [price, quantity]
        public long t { get; set; }      // timestamp
    }

    public class XTFuturesResponseWebSocketPortfolio
    {
        public string coin { get; set; }  // Currency (e.g., usdt)
        public string underlyingType { get; set; }  // Underlying type (1 = Coin-M, 2 = USDT-M)
        public string walletBalance { get; set; }  // Wallet balance
        public string openOrderMarginFrozen { get; set; }  // Margin frozen by open orders
        public string isolatedMargin { get; set; }  // Isolated margin
        public string crossedMargin { get; set; }  // Crossed margin  
    }

    public class PositionData
    {
        public string symbol { get; set; }  // Trading pair symbol (e.g., btc_usdt)
        public string contractType { get; set; }  // Contract type ("PERPETUAL", "DELIVERY")
        public string positionType { get; set; }  // Position type ("ISOLATED", "CROSSED")
        public string positionSide { get; set; }  // Position side ("LONG", "SHORT")
        public string positionSize { get; set; }  // Position quantity
        public string closeOrderSize { get; set; }  // Pending close order size (contracts)
        public string availableCloseSize { get; set; }  // Available contracts to close
        public string realizedProfit { get; set; }  // Realized profit and loss
        public string entryPrice { get; set; }  // Average entry price
        public string isolatedMargin { get; set; }  // Isolated margin
        public string openOrderMarginFrozen { get; set; }  // Margin occupied by open orders
        public string underlyingType { get; set; }  // Underlying type ("COIN_BASED", "U_BASED")
        public string leverage { get; set; }  // Leverage
    }
    public class XTFuturesResponseWebSocketOrder
    {
        public string symbol { get; set; }        // Trading pair
        public string orderId { get; set; }       // Order ID
        public string origQty { get; set; }       // Original quantity
        public string avgPrice { get; set; }      // Average price
        public string price { get; set; }         // Price
        public string executedQty { get; set; }   // Executed quantity
        public string orderSide { get; set; }     // BUY or SELL
        public string timeInForce { get; set; }   // Valid way (GTC, IOC, FOK, GTX)
        public string positionSide { get; set; }  // Position side (LONG, SHORT)
        public string marginFrozen { get; set; }  // Occupied margin
        public string sourceType { get; set; }    // Order source (DEFAULT, ENTRUST, PROFIT)
        public string type { get; set; }          // Type (ORDER, etc.)
        public string state { get; set; }         // State (NEW, PARTIALLY_FILLED, FILLED, CANCELED, etc.)
        public string createdTime { get; set; }   // Create time (timestamp)
        public string leverage { get; set; }      // Leverage
        public string positionType { get; set; }  // Position type (CROSSED, ISOLATED)
        public string orderType { get; set; }     // Order type (LIMIT, MARKET)
    }
}
