﻿using System.Collections.Generic;

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
        public string coin { get; set; }                    // Currency (e.g., usdt)
        public string underlyingType { get; set; }          // Underlying type (1 = Coin-M, 2 = USDT-M)
        public string walletBalance { get; set; }           // Wallet balance
        public string openOrderMarginFrozen { get; set; }   // Margin frozen by open orders
        public string isolatedMargin { get; set; }          // Isolated margin
        public string crossedMargin { get; set; }          // Crossed margin 
        public string availableBalance { get; set; }
        public string coupon { get; set; }
        public string bonus { get; set; }
        public string depositCoupon { get; set; }
        public string openOrderFeeFrozen { get; set; }
    }

    public class XTFuturesUpdateOrder
    {
        public string symbol;          // Trading pair
        public string orderId;         // Order ID
        public string origQty;         // Original quantity
        public string avgPrice;        // Average price
        public string price;           // Order price
        public string executedQty;     // Executed quantity 
        public string orderSide;       // BUY or SELL
        public string timeInForce;     // Valid way
        public string positionSide;    // LONG or SHORT
        public string marginFrozen;    // Occupied margin
        public string sourceType;      // DEFAULT, ENTRUST, PROFIT (type of order source)
        public string type;            // Type (ORDER, etc.)
        public string state;           // Order state: NEW, PARTIALLY_FILLED, FILLED, etc.
        public string createdTime;     // Create time(ms)
        public string leverage;        // Leverage
        public string positionType;    // Position type: CROSSED or ISOLATED
        public string orderType;       // Order type: LIMIT or MARKET
    }

    public class XTFuturesPositionData
    {
        public string accountId { get; set; }
        public string accountType { get; set; }
        public string symbol { get; set; }
        public string contractType { get; set; }
        public string positionType { get; set; }
        public string positionSide { get; set; }
        public string positionSize { get; set; }
        public string closeOrderSize { get; set; }
        public string availableCloseSize { get; set; }
        public string realizedProfit { get; set; }
        public string entryPrice { get; set; }
        public string openOrderSize { get; set; }
        public string isolatedMargin { get; set; }
        public string openOrderMarginFrozen { get; set; }
        public string underlyingType { get; set; }
        public string leverage { get; set; }
        public string welfareAccount { get; set; }
        public string closeProfit { get; set; }
        public string totalFee { get; set; }
        public string totalFundFee { get; set; }
        public string markPrice { get; set; }
        public ProfitFixedLatest profitFixedLatest { get; set; }
        public string updatedTime { get; set; }
    }

    public class ProfitFixedLatest
    {
        public string value { get; set; } = string.Empty; 
    }
}
